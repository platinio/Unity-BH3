using System;
using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    [RequireComponent(typeof(Variables))]
    public class BehaviorTreeMachine : BaseMachine<BehaviorTreeGraph, BehaviorTreeGraphAsset, BehaviorTreeNode, BehaviorTreeTransition>
    {
        private BehaviorTreeGraph behaviorTreeGraph;
        private ExecutionStatus lastExecutionStatus = ExecutionStatus.Inactive;
        private BehaviorTreeGraphAsset graphInstance = null;

        /// <summary>
        /// Whether <see cref="Awake"/> has run. <see cref="Switch"/> refuses before it, because everything a
        /// tree needs to come alive — the <c>Variables</c> component, the recorder — is set up there.
        /// </summary>
        private bool hasAwoken;

        /// <summary>
        /// Whether <see cref="Start"/> has run. <see cref="Switch"/> reads it to decide whether entering the
        /// new tree is its job or still Start's, so a tree is entered exactly once however early it arrives.
        /// </summary>
        private bool hasStarted;
        
        public ExecutionStatus LastExecutionStatus => lastExecutionStatus;

        /// <summary>
        /// The tree this agent is actually ticking, or null before <see cref="Awake"/> and after teardown.
        ///
        /// <para>
        /// The one answer to "what graph is this machine running", and not the same question as
        /// <see cref="GraphInstance"/>. That property is the private <em>clone of an asset</em>, which an
        /// agent whose tree is authored into the scene never has: <see cref="AwakenTree"/> takes the
        /// null-macro path, leaves <c>graphInstance</c> null, and runs <c>nest.embed</c> directly. Asking
        /// the clone what is running therefore answers "nothing" for a perfectly ordinary agent — which is
        /// how an embedded-graph agent came to have an empty topology in the why-inspector and an event log
        /// of bare guids in the flight recorder window.
        /// </para>
        ///
        /// <para>
        /// For a macro-backed agent the two agree by construction, because <c>AwakenTree</c> switches the
        /// nest to the clone's graph before reading it back. So this is the general answer rather than a
        /// second one.
        /// </para>
        /// </summary>
        public BehaviorTreeGraph RunningGraph => behaviorTreeGraph;

        /// <summary>
        /// True once the root has returned Success or Failure, which is final: a tree that has finished is
        /// not ticked again, and the only way back is <see cref="Switch"/>, which replaces the tree
        /// outright.
        ///
        /// <para>
        /// Every shipped tree parks a Repeater under Entry, so in practice the root never returns anything
        /// terminal and this stays false forever -- which is why the three Unity callbacks were free to
        /// disagree about it. Update stopped; LateUpdate and FixedUpdate kept forwarding to the graph after
        /// the halt. They ask the same question now.
        /// </para>
        /// </summary>
        public bool HasFinished =>
            lastExecutionStatus == ExecutionStatus.Success || lastExecutionStatus == ExecutionStatus.Failure;

        public BehaviorTreeGraphAsset GraphInstance => graphInstance;
        public BehaviorTreeGraphAsset GraphAsset => nest.macro;
        public BehaviorTreeGraphAsset OriginalMacro { get; private set; }

        /// <summary>
        /// The agent variable every machine publishes itself under, so a tree can read the GameObject it is
        /// running on.
        ///
        /// <para>
        /// A const because it is a contract between this line and every read site, and a misspelling on
        /// either side of a string literal compiles and then simply finds nothing.
        /// </para>
        /// </summary>
        public const string SelfVariableKey = "This";

        /// <summary>
        /// This agent's black box, or null outside the editor and dev builds. Assigned through
        /// <see cref="Debugging.BehaviorTreeRecorder"/>, which is what makes it disappear from a shipped build.
        /// </summary>
        public Debugging.BehaviorTreeFlightRecorder FlightRecorder { get; private set; }

        public void SetFlightRecorder(Debugging.BehaviorTreeFlightRecorder recorder)
        {
            FlightRecorder = recorder;
        }

        protected override void Awake()
        {
            base.Awake();
            Variables = GetComponent<Variables>();
            Variables.declarations.Set(SelfVariableKey, gameObject);

            if (!hasGraph) return;

            OriginalMacro = nest.macro;

            // Before the scopes are built, because building them is when call sites are registered and a
            // call site can only be registered against a recorder that already exists. Attached once per
            // agent rather than per tree: Switch rebinds the new graph to this same recorder, so a
            // recording spans the swap instead of restarting at it.
            Debugging.BehaviorTreeRecorder.Attach(this, OriginalMacro != null ? OriginalMacro.name : name);

            // Everything Switch depends on now exists, so it is allowed from here on.
            hasAwoken = true;

            AwakenTree(nest.macro);
        }

        /// <summary>
        /// Brings <paramref name="macro"/> alive on this agent and makes it the tree <c>Update</c> ticks.
        /// <para>
        /// The single answer to "how does a tree come alive", shared by <see cref="Awake"/> and
        /// <see cref="Switch"/>. It was written twice, and the second copy drifted: <c>Switch</c> skipped both
        /// the <c>Instantiate</c> and the <c>OnAwake</c>, so a switched-in tree ran the shared project asset
        /// with empty composite child lists and guards that had never been armed onto their owners — an agent
        /// that silently ignored every precondition in the tree it had just been handed.
        /// </para>
        /// <para>
        /// A null <paramref name="macro"/> is the embedded-graph case, not an error: the tree is authored into
        /// the scene on <c>nest.embed</c>, there is nothing to clone, and <see cref="ReleaseTree"/> must not
        /// destroy it because the agent does not own it.
        /// </para>
        /// </summary>
        private void AwakenTree(BehaviorTreeGraphAsset macro)
        {
            // The try/catch spans the whole method, and is unconditional rather than editor-only. Both matter:
            // ThrowIfCausesRecursion below throws for exactly the authoring mistake a designer is most likely
            // to make, and a switch has already destroyed the outgoing tree by the time it does -- so an
            // exception escaping here unnamed and unlogged leaves an agent with no tree and no explanation.
            // The catch only adds the tree's name and rethrows, and that context is worth more in a player
            // build, not less.
            try
            {
                // Instantiated so per-agent runtime state -- composite indices, guard caches, WaitTime timers,
                // all [DoNotSerialize] fields on node instances -- lives on a private copy. Running the asset
                // directly makes one shared definition carry per-run state: two agents on the same tree
                // overwrite each other, and in the editor the asset collects that state and dirties on disk.
                graphInstance = macro != null ? Instantiate(macro) : null;

                if (graphInstance != null) nest.SwitchToEmbed(graphInstance.graph);

                behaviorTreeGraph = nest.embed;

                //throws an exception if subgraphs causes recursion
                behaviorTreeGraph.ThrowIfCausesRecursion();

                var nodes = behaviorTreeGraph.Nodes;
                foreach (var node in nodes)
                {
                    node.SetMachine(this);
                }

                Debugging.BehaviorTreeRecorder.Bind(behaviorTreeGraph, FlightRecorder);

                // A fresh tree has not run, so it is not halted. Update stops ticking on a terminal root
                // status and only this reopens it -- which is what lets a switch revive an agent whose
                // previous tree finished.
                lastExecutionStatus = ExecutionStatus.Inactive;

                OverrideGraphVariables(graphInstance);
                BuildVariableScopes(graphInstance, behaviorTreeGraph);
                behaviorTreeGraph.OnAwake();
            }
            catch (Exception e)
            {
                string macroName = macro != null ? macro.name : null;
                Debug.LogError($"BehaviorTree = {macroName} Method = AwakenTree() Exception = {e}", gameObject);

                // Nothing half-built is left behind for Update to tick. A tree that never finished waking has
                // empty composite child lists, so Entry would return Success on the very next frame and the
                // agent would halt looking exactly like a tree that had simply finished -- the loudest failure
                // in the codebase reduced to an agent standing still. Releasing also frees the clone, which
                // ReleaseTree is otherwise the only thing that does.
                ReleaseTree();

                throw;
            }
        }

        /// <summary>
        /// Stops the tree this agent is running and frees it.
        /// <para>
        /// Exit before destroy, and in that order: exiting is what hands a running branch its <c>OnExit</c> so
        /// it can put its own toys away, and <c>OnDestroy</c> alone never reaches that. The clone is then
        /// released because the agent is the only owner of it -- one leaked <c>ScriptableObject</c> per swap is
        /// invisible until a boss has changed phase a few hundred times.
        /// </para>
        /// <para>
        /// Only <see cref="graphInstance"/> is destroyed. An embedded graph was authored into the scene and
        /// belongs to it, and a <c>Destroy</c> aimed at a project asset is an error Unity refuses.
        /// </para>
        /// <para>
        /// <b>This runs author-written logic during <c>OnDestroy</c>.</b> A node's <c>OnExit</c> can be an
        /// arbitrary script graph (<c>VisualScriptingNode.OnExitGraph</c>), so an exit graph now fires while
        /// the agent is being torn down as well as on a normal branch exit. That is the price of the fix — a
        /// branch cannot both clean up after itself and never be told it stopped — but it means an exit graph
        /// must not assume its sibling components are still alive, because destruction order between them is
        /// not defined.
        /// </para>
        /// </summary>
        private void ReleaseTree()
        {
            if (behaviorTreeGraph == null) return;

            behaviorTreeGraph.OnExit();
            behaviorTreeGraph.OnDestroy();

            if (graphInstance != null) Destroy(graphInstance);

            behaviorTreeGraph = null;
            graphInstance = null;
        }

        /// <summary>
        /// Builds the root variable scope and hands it to every node, so each sub-tree can open a nested one
        /// from it.
        /// <para>
        /// The root scope holds the agent's variables, so anything an agent declares is visible to every
        /// branch however deeply nested. Each <c>RunBehaviorTreeGraphNode</c> then opens a child scope around
        /// its own instance, seeded with that branch's optional defaults — so a branch reads its own values
        /// first and the agent's only when it declares none, and its writes stay inside it.
        /// </para>
        /// <para>
        /// Scopes only. Pushing the agent's <c>Variables</c> into the tree's declarations is a separate step
        /// with its own method, <see cref="OverrideGraphVariables"/>, and both are called from
        /// <see cref="AwakenTree"/>.
        /// </para>
        /// </summary>
        private void BuildVariableScopes(BehaviorTreeGraphAsset graphAsset, BehaviorTreeGraph graph)
        {
            if (graph == null || graphAsset == null) return;

            var rootScope = new BehaviorTreeVariableScope(graphAsset.declarations);
            rootScope.SeedDefaults(graphAsset.optionalDeclarations);

            Debugging.BehaviorTreeRecorder.BindRootScope(this, rootScope);

            // Each node opens its own child scope from here; RunBehaviorTreeGraphNode overrides
            // SetVariableScope to do exactly that and recurse.
            foreach (var behaviorTreeNode in graph.Nodes)
            {
                behaviorTreeNode.SetVariableScope(rootScope);
            }
        }

        private void OverrideGraphVariables(BehaviorTreeGraphAsset graphAsset)
        {
            if (graphAsset == null || graphAsset.declarations == null) return;

            foreach (var variableDeclaration in Variables.declarations)
            {
                graphAsset.declarations.Set(variableDeclaration.name, variableDeclaration.value);
            }
        }

        /// <summary>
        /// Replaces the tree this agent is running: a boss changing phase, an NPC entering combat, a possessed
        /// unit handed a different brain.
        /// <para>
        /// The whole of it is put the old tree down, bring the new one up, and enter it. Nothing about "how a
        /// tree comes alive" is restated here -- that lives in <see cref="AwakenTree"/> and is shared with
        /// <see cref="Awake"/>, because a second copy of that rule is exactly what went stale last time.
        /// </para>
        /// </summary>
        public void Switch(BehaviorTreeGraphAsset behaviorTreeGraphAsset)
        {
            if (behaviorTreeGraphAsset == null) return;

            // Refused rather than attempted, because succeeding here is worse than failing. Unity does not
            // order Awake between components, so a caller in another component's Awake may land before ours:
            // the Variables component this reads through would be unassigned, and -- since SwitchToEmbed
            // clears nest.macro -- our own Awake would afterwards read that null as "embedded graph", drop the
            // reference to the instance made here and leave it with no owner to free it. Silent corruption,
            // decided by component order. Saying so is the only honest answer.
            if (!hasAwoken)
            {
                Debug.LogError(
                    $"Switch was called on '{name}' before its own Awake had run, so the agent is not ready to "
                    + "receive a tree and the call was ignored. Unity does not order Awake between components "
                    + "-- call Switch from Start or later.", gameObject);

                return;
            }

            ReleaseTree();
            AwakenTree(behaviorTreeGraphAsset);

            // Entered here rather than inside AwakenTree because Awake must not: Unity calls Start afterwards
            // and that is where the first tree is entered. Guarding on hasStarted keeps it to exactly one
            // entry per tree in the window between the two, where a caller in another component's Start or in
            // OnEnable can otherwise be entered a second time by our own Start.
            if (hasStarted) behaviorTreeGraph.OnEnter();
        }

        private void Start()
        {
            hasStarted = true;

            if (hasGraph && behaviorTreeGraph != null)
            {
                behaviorTreeGraph.OnEnter();
            }
        }

        private void Update()
        {
            if (hasGraph && behaviorTreeGraph != null)
            {

#if UNITY_EDITOR
                foreach (var node in behaviorTreeGraph.Nodes)
                {
                    node.CanvasUpdate();
                }
#endif
                // After the canvas refresh on purpose: a finished tree still draws, it just stops running.
                if (HasFinished) return;

                // Opens the tick before the tree runs, so everything the tree does this frame is stamped with
                // the same tick and ordered within it.
                Debugging.BehaviorTreeRecorder.BeginTick(this);

                lastExecutionStatus = behaviorTreeGraph.OnUpdate();

            }
        }

        private void LateUpdate()
        {
            if (hasGraph && behaviorTreeGraph != null && !HasFinished)
            {
                behaviorTreeGraph.OnLateUpdate();
            }
        }

        private void FixedUpdate()
        {
            if (hasGraph && behaviorTreeGraph != null && !HasFinished)
            {
                behaviorTreeGraph.OnFixedUpdate();
            }
        } 
        

        protected override void OnDestroy()
        {
            // Detached first so the registry is cleaned even if a node's exit throws on the way down.
            Debugging.BehaviorTreeRecorder.Detach(this);

            // The same teardown a switch performs -- a dying agent owes its running branch the same OnExit a
            // replaced one does, and keeping a second hand-rolled copy here is how Switch drifted from Awake.
            ReleaseTree();

            // Awake went through the base, so OnDestroy has to as well. The base releases the cached
            // reference and uninstantiates the nest, which is what takes this machine back out of
            // GraphInstances and out of the editor's debug-data map. Without it every destroyed agent left
            // one entry in each for the rest of the session, and only a domain reload ever cleared them.
            base.OnDestroy();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (hasGraph && behaviorTreeGraph != null)
            {
                behaviorTreeGraph.OnApplicationPause(pauseStatus);
            }
        }

        private void OnDrawGizmos()
        {
            if (hasGraph && behaviorTreeGraph != null)
            {
                behaviorTreeGraph.OnDrawGizmos();
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (hasGraph && behaviorTreeGraph != null)
            {
                behaviorTreeGraph.OnDrawGizmosSelected();
            }
        }

        public override BehaviorTreeGraph DefaultGraph()
        {
            return new BehaviorTreeGraph();
        }
    }
}