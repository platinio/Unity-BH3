using System;
using System.Collections.Generic;
using System.Linq;
using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// Base class for all behavior tree nodes
    /// </summary>
    public class BehaviorTreeNode : BaseGraphNode<BehaviorTreeGraph, BehaviorTreeNode, BehaviorTreeTransition>, IBehaviorTreeNode
    {
        [DoNotSerialize]
        public IPortCollection<ControlInput> controlInputs { get; }

        [DoNotSerialize]
        public IPortCollection<ControlOutput> controlOutputs { get; }

        [DoNotSerialize]
        public IPortCollection<ValueInput> valueInputs { get; }

        [DoNotSerialize]
        public IPortCollection<ValueOutput> valueOutputs { get; }

        [DoNotSerialize]
        public IPortCollection<InvalidInput> invalidInputs { get; }

        [DoNotSerialize]
        public IPortCollection<InvalidOutput> invalidOutputs { get; }

        [DoNotSerialize]
        public IEnumerable<IInputPort> inputs => LinqUtility.Concat<IInputPort>(controlInputs, valueInputs, invalidInputs);

        [DoNotSerialize]
        public IEnumerable<IOutputPort> outputs => LinqUtility.Concat<IOutputPort>(controlOutputs, valueOutputs, invalidOutputs);

        [DoNotSerialize]
        public IEnumerable<IInputPort> validInputs => LinqUtility.Concat<IInputPort>(controlInputs, valueInputs);

        [DoNotSerialize]
        public IEnumerable<IOutputPort> validOutputs => LinqUtility.Concat<IOutputPort>(controlOutputs, valueOutputs);

        [DoNotSerialize]
        public IEnumerable<IPort> ports => LinqUtility.Concat<IPort>(inputs, outputs);

        [DoNotSerialize]
        public IEnumerable<IPort> invalidPorts => LinqUtility.Concat<IPort>(invalidInputs, invalidOutputs);

        [DoNotSerialize]
        public IEnumerable<IPort> validPorts => LinqUtility.Concat<IPort>(validInputs, validOutputs);

        [DoNotSerialize]
        private List<ConditionalExecution> conditionalExecutions;

        /// <summary>
        /// The guards armed on this node, created on first use.
        /// <para>
        /// Lazily rather than by field initialiser because the field is <see cref="DoNotSerializeAttribute"/>
        /// and a deserialized node can arrive with it null, its initialiser never having run. That went
        /// unnoticed while the only code path touched the list of nodes that <em>own</em> a guard; the moment
        /// anything walked every node it threw.
        /// <para>
        /// Precisely: the initialiser is skipped when the serializer cannot call a constructor, which is when
        /// the type has no parameterless one — it then materialises the object directly. So this is a
        /// property of the node <em>type</em>, not of deserialization in general.
        /// <c>NodeTypes_AreConstructibleByTheSerializer</c> keeps every node type constructible, which
        /// is the real fix; lazily initialising is what makes a field safe even if one ever is not.
        /// </para>
        /// </para>
        /// </summary>
        private List<ConditionalExecution> Guards => conditionalExecutions ??= new List<ConditionalExecution>();

        [DoNotSerialize]
        private Dictionary<Guid, int> conditionalExecutionIndexCache;

        /// <summary>
        /// Where a guard's index is remembered, created on first use for the same reason
        /// <see cref="Guards"/> is — a field initialiser is not guaranteed to have run.
        /// </summary>
        private Dictionary<Guid, int> ConditionalExecutionIndexCache =>
            conditionalExecutionIndexCache ??= new Dictionary<Guid, int>();

        public virtual bool CanUseConditionalExecutions => true;
        public event Action onPortsChanged;
        public IConnectionCollection<IPortRelation, IPort, IPort> relations { get; private set; }
        
        [DoNotSerialize]
        public IEnumerable<IPortConnection> connections => ports.SelectMany(p => p.connections);
        
        [DoNotSerialize]
        public virtual bool isControlRoot { get; protected set; } = false;
        
        [Serialize]
        public Vector2 position { get; set; }

        public virtual bool ShowIcon => false;

        public virtual bool CanCopy => true;
        public virtual bool CanDuplicate => true;
        public virtual bool CanCut => true;
        public override bool  DrawInSubTree => true;

        public virtual string Description => string.Empty;

        /// <summary>
        /// What is wrong with this node right now, for the canvas to draw and for tooling to report. Adding
        /// nothing means the node is fine, which is the default.
        ///
        /// <para>
        /// Declared here rather than behind a capability interface because <b>every</b> node can be wrong —
        /// there is no node for which the question is meaningless, so an interface would separate nothing.
        /// The capability-not-type rule the guard walks follow applies where a capability is genuinely
        /// selective, as with <see cref="IDeclaresWatchedKeys"/>; it does not apply to something universal.
        /// Being on the base also means a new node's author finds this among the members they already
        /// override, rather than having to know an interface exists.
        /// </para>
        ///
        /// <para>
        /// Report what the node already knows: this is read to draw a canvas, and although the result is
        /// cached against an invalidation counter rather than recomputed per frame, an override that walks a
        /// whole graph is still felt on every edit to a large tree.
        /// </para>
        ///
        /// <para>
        /// Problems only an outside rule can see are contributed by registering a provider with
        /// <c>NodeProblemCache</c> instead, so a lint does not have to become a property of the thing it
        /// inspects. Always call <c>base.CollectProblems</c> when overriding.
        /// </para>
        /// </summary>
        public virtual void CollectProblems(List<NodeProblem> into)
        {
            // Every node can have this one, which is why it is here rather than repeated per node: a port
            // with nothing connected and no inline default throws MissingValuePortInputException the first
            // time it is read. 22 shipped nodes declare at least one such port, and until now the only
            // warning was bt_verify, which a designer never runs.
            foreach (var port in valueInputs)
            {
                if (port == null || !port.IsUnfedRequired) continue;

                into.Add(new NodeProblem(NodeProblemSeverity.Error,
                    $"Input '{port.key}' has nothing connected and declares no default, so reading it throws.",
                    "Connect a value, or feed it a literal."));
            }
        }

        public void PortsChanged()
        {
            onPortsChanged?.Invoke();
        }
        
        public override void AfterAdd()
        {
            // Important to define before notifying instances
            Define();

            base.AfterAdd();
        }

        public IGraphElementDebugData CreateDebugData()
        {
            return new Unit.DebugData();
        }

        [DoNotSerialize]
        public virtual bool canDefine => true;
        [DoNotSerialize]
        public bool isDefined { get; private set; }
        public bool failedToDefine { get; }
        public Exception definitionException { get; }

       
        public void AddConditionalExecution(ConditionalExecution conditionalExecution)
        {
            Guards.Add(conditionalExecution);
            hasPollableGuards = null;
        }

        /// <summary>
        /// Drops every guard armed on this node, so <see cref="BehaviorTreeGraph.OnAwake"/> can rebuild the
        /// list from the graph rather than append to whatever a previous call left behind.
        /// <para>
        /// Rebuilding from source is what makes arming idempotent: a guard deleted between two awakes
        /// disappears, which a dedupe-on-insert scheme would not manage. See
        /// <see cref="BehaviorTreeGraph.AddConditionalExecutionNodes"/> for why that matters.
        /// </para>
        /// </summary>
        public void ClearConditionalExecutions()
        {
            Guards.Clear();
            hasPollableGuards = null;
        }

        public void Define()
        {
            var preservation = NodePreservation.Preserve(this);

            // A node needs to undefine even if it wasn't defined,
            // because there might be invalid ports and connections
            // that we need to clear to avoid duplicates on definition.
            Undefine();

            if (canDefine)
            {
                try
                {
                    Definition();
                    isDefined = true;
                    //definitionException = null;
                    AfterDefine();
                }
                catch (Exception ex)
                {
                    Undefine();
                    //definitionException = ex;
                    Debug.LogWarning($"Failed to define {this}:\n{ex}");
                }
            }

            preservation.RestoreTo(this);
        }

        protected virtual void Definition() { }
        
        protected virtual void AfterDefine() { }

        private void Undefine()
        {
            // Because a node is always undefined on definition,
            // even if it wasn't defined before, we make sure the user
            // code for undefinition can safely presume it was defined.
            if (isDefined)
            {
                BeforeUndefine();
            }

            Disconnect();
            defaultValues?.Clear();
            controlInputs?.Clear();
            controlOutputs?.Clear();
            valueInputs?.Clear();
            valueOutputs?.Clear();
            invalidInputs?.Clear();
            invalidOutputs?.Clear();
            relations?.Clear();
            isDefined = false;
        }
        
        protected virtual void BeforeUndefine() { }
        
        public override void BeforeRemove()
        {
            base.BeforeRemove();
            Disconnect();
        }

        public void EnsureDefined()
        {
            
        }

        public void RemoveUnconnectedInvalidPorts()
        {
            
        }
        
        public void Disconnect()
        {
            // Can't use a foreach because invalid ports may get removed as they disconnect
            while (ports.Any(p => p.hasAnyConnection))
            {
                ports.First(p => p.hasAnyConnection).Disconnect();
            }
        }

        [Serialize]
        public Dictionary<string, object> defaultValues { get; private set; }

        /// <summary>
        /// Declares a port. <b>A key is unique across everything the node exposes</b>, and that is
        /// enforced rather than assumed: every port collection is a <c>KeyedCollection</c>, so adding a
        /// second port under a live key throws <see cref="ArgumentException"/>.
        /// <para>
        /// It has to hold, because a key is the only thing that identifies a port afterwards.
        /// <see cref="defaultValues"/> is a <c>Dictionary&lt;string, object&gt;</c> with no room for a
        /// direction or a type, and <c>NodePreservation</c> resolves a stored connection back to a live
        /// port with <c>inputs.Single(p =&gt; p.key == key)</c> on every <see cref="Define"/> -- which
        /// throws on two matches rather than picking one.
        /// </para>
        /// <para>
        /// The throw surfaces oddly, so it is worth knowing what it looks like: <see cref="Define"/>
        /// catches whatever <see cref="Definition"/> throws, logs a warning and undefines the node, so a
        /// duplicate key presents as a node that arrives with <em>no ports at all</em>.
        /// <c>CreateMenuNodes_DefineSuccessfully</c> is the gate that turns that into a failing test
        /// rather than a warning nobody reads in a build.
        /// </para>
        /// </summary>
        protected ValueInput ValueInput<T>(string key)
        {
            return ValueInput(typeof(T), key);
        }
        
        protected ValueInput ValueInput<T>(string key, object defaultValue)
        {
            return ValueInput(typeof(T), key, defaultValue);
        }
        
        protected ValueInput ValueInput(Type type, string key)
        {
            var port = new ValueInput(key, type);
            valueInputs.Add(port);
            return port;
        }
        
        protected ValueInput ValueInput(Type type, string key, object defaultValue)
        {
            var port = new ValueInput(key, type);
            valueInputs.Add(port);
            port.SetDefaultValue(defaultValue);
            return port;
        }
        
        protected ValueOutput ValueOutput(Type type, string key)
        {
            var port = new ValueOutput(key, type);
            valueOutputs.Add(port);
            return port;
        }

        protected ValueOutput ValueOutput(Type type, string key, GetPortValue getValue)
        {
            var port = new ValueOutput(key, type, getValue);
            valueOutputs.Add(port);
            return port;
        }

        protected ValueOutput ValueOutput<T>(string key)
        {
            return ValueOutput(typeof(T), key);
        }

        protected ValueOutput ValueOutput<T>(string key, GetPortValue getValue)
        {
            return ValueOutput(typeof(T), key, getValue);
        }
        
        protected BehaviorTreeNode() : base()
        {
            controlInputs = new PortCollection<ControlInput>(this);
            controlOutputs = new PortCollection<ControlOutput>(this);
            valueInputs = new PortCollection<ValueInput>(this);
            valueOutputs = new PortCollection<ValueOutput>(this);
            invalidInputs = new PortCollection<InvalidInput>(this);
            invalidOutputs = new PortCollection<InvalidOutput>(this);
            relations = new ConnectionCollection<IPortRelation, IPort, IPort>();
            defaultValues = new Dictionary<string, object>();
        }

        protected BehaviorTreeMachine BehaviorTreeMachine => Machine as BehaviorTreeMachine;

        [DoNotSerialize]
        private BehaviorTreeVariableScope variableScope;

        /// <summary>
        /// The variables this node can see. Assigned alongside the machine when the graph loads, so a node
        /// inside a sub-tree resolves against that sub-tree's own instance before searching outward — see
        /// <see cref="BehaviorTreeVariableScope"/>.
        /// <para>
        /// Falls back to a scope over the root tree's declarations when nothing assigned one, so any path that
        /// loads a graph without calling <see cref="SetVariableScope"/> behaves exactly as it did before.
        /// </para>
        /// </summary>
        [DoNotSerialize]
        public BehaviorTreeVariableScope VariableScope
        {
            get
            {
                if (variableScope != null) return variableScope;

                var instance = BehaviorTreeMachine?.GraphInstance;
                if (instance == null) return null;

                variableScope = new BehaviorTreeVariableScope(instance.declarations);
                return variableScope;
            }
        }

        /// <summary>
        /// Hands this node the scope it resolves variables against. Mirrors <c>SetMachine</c> — the machine
        /// calls it across the root graph, and <see cref="RunBehaviorTreeGraphNode"/> overrides it to build a
        /// child scope and pass that one down instead.
        /// </summary>
        public virtual void SetVariableScope(BehaviorTreeVariableScope scope)
        {
            variableScope = scope;
        }

        [DoNotSerialize]
        private Debugging.BehaviorTreeFlightRecorder flightRecorder;

        /// <summary>
        /// Where this node reports what it did, or null when nothing is recording.
        /// <para>
        /// Injected rather than fetched from the machine so a node can be recorded without one. The
        /// composites and decorators drive their children purely through the lifecycle hooks and never touch
        /// the machine, which is what lets a tree be ticked in an edit-mode test — and a recorder that only
        /// worked with a live machine could not be tested by the same means.
        /// </para>
        /// </summary>
        [DoNotSerialize]
        public Debugging.BehaviorTreeFlightRecorder FlightRecorder => flightRecorder;

        /// <summary>
        /// Hands this node the recorder to report to. Mirrors <see cref="SetVariableScope"/>;
        /// <see cref="RunBehaviorTreeGraphNode"/> overrides it to reach the nodes inside its branch.
        /// </summary>
        public virtual void SetFlightRecorder(Debugging.BehaviorTreeFlightRecorder recorder)
        {
            flightRecorder = recorder;
        }

        /// <summary>
        /// What an embedded Visual Scripting graph sees, as the flat collection those entry points take.
        /// Collapses the scope chain so a script graph inside a branch reads that branch's values first and
        /// the agent's underneath — matching what every other read in the tree resolves to.
        /// <para>
        /// A node in the root tree hands over the root declarations unchanged, so nothing is copied in the
        /// common case; only a node inside a sub-tree pays for the merge.
        /// </para>
        /// </summary>
        [DoNotSerialize]
        protected VariableDeclarations ScriptGraphVariables =>
            VariableScope?.Flatten() ?? BehaviorTreeMachine?.GraphInstance?.declarations;

        public virtual int MaxChildrenLimit => 0;
        
        protected GameObject GetTargetGameObject(GameObjectBlackboardVariable gameObjectVariable)
        {
            var target = gameObjectVariable.GetValue(BehaviorTreeMachine, VariableScope);
            return target == null ? gameObject : target;
        }
        
        protected T GetComponent<T>(ValueInput valueInput) where T : Component
        {
            var component = valueInput.GetComponent<T>();
            if (component == null) return gameObject.GetComponent<T>();

            return component;
        }

        /// <summary>
        /// Resolves a component from a port, and says so when it cannot find one.
        ///
        /// <para>
        /// <b>Call this when the node is entered, not at wake and not per tick.</b> A port is not a fixed
        /// reference — what it points at can differ between one entry and the next — so a component resolved
        /// once in <c>OnAwake</c> answers with whatever the port held the first time and never looks again,
        /// which is a node quietly ignoring its own wire for the lifetime of the object. Resolving in
        /// <c>OnUpdate</c> is correct but pays the lookup on every tick of a node that may run for many.
        /// <c>OnEnter</c> is the one that is both. <c>PortReadConventionTests</c> enforces it.
        /// </para>
        ///
        /// <para>
        /// The failure is the point of the method existing rather than callers writing
        /// <see cref="GetComponent{T}(ValueInput)"/> and a null check. A tree dropped onto a prefab that has
        /// no <c>Rigidbody</c> — or no <c>NavMeshAgent</c>, or no <c>AudioSource</c> — is an ordinary
        /// authoring mistake, and the honest answer to it is one line naming the node and the port, followed
        /// by a failed node. The alternative, which several nodes shipped, is a
        /// <c>NullReferenceException</c> thrown from inside the node that takes the branch down saying
        /// nothing about why.
        /// </para>
        ///
        /// <para>
        /// The message names the port through <c>port.key</c> rather than a hard-coded word, so it cannot
        /// drift from the port it is describing — which had already happened once, in a copied comment that
        /// called <c>PlayAudio</c>'s <c>AudioSource</c> port "Target".
        /// </para>
        ///
        /// <para>
        /// <paramref name="component"/> is written on every path, so a caller holding it in a field always
        /// has this entry's answer and never the previous entry's. A node whose work happens in
        /// <c>OnUpdate</c> may therefore ignore the return value and test that field there instead — the
        /// report has already been made either way, and the field cannot be a previous entry's component.
        /// </para>
        /// </summary>
        /// <returns>False when nothing was found, in which case the node should fail rather than continue.</returns>
        protected bool TryResolve<T>(ValueInput port, out T component) where T : Component
        {
            component = GetComponent<T>(port);

            if (component != null) return true;

            Debug.LogError(
                $"'{NodeName}' found no {typeof(T).Name} on its {port.key} or on the agent itself.",
                gameObject);

            return false;
        }
        
        public IReadOnlyCollection<ConditionalExecution> ConditionalExecutions => Guards;
        
        public int GetConditionalIndex(ConditionalExecution conditionalExecution)
        {
            if (ConditionalExecutionIndexCache.TryGetValue(conditionalExecution.guid, out int index))
            {
                return index;
            }

            index = 0;
            
            foreach (var graphElement in graph.elements)
            {
                if (graphElement is ConditionalExecution conditionalExecutionNode)
                {
                    if (conditionalExecutionNode.Owner == this)
                    {
                        if (conditionalExecutionNode == conditionalExecution) break;
                        index++;
                    }
                }
            }

            ConditionalExecutionIndexCache[conditionalExecution.guid] = index;
            return index;
        }
        
        public int CountConditionalExecutions()
        {
            int count = 0;
            
            foreach (var graphElement in graph.elements)
            {
                if (graphElement is ConditionalExecution conditionalExecutionNode)
                {
                    if (conditionalExecutionNode.Owner == this)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        public void ClearConditionalExecutionIndexCache()
        {
            ConditionalExecutionIndexCache.Clear();
        }

        /// <summary>
        /// Evaluates the guards armed on this node, recording each, and returns the first that answered
        /// false — or null when every one of them held.
        /// <para>
        /// One walk for what used to be two verbatim copies, in <see cref="OnNodeEnter"/> and
        /// <see cref="OnUpdateInternal"/>. Both are <c>sealed override</c>, so no subclass can intercept
        /// them and the guard filter has to live inside them; collapsing the copies is what stops that
        /// filter from being a rule written twice and enforced once.
        /// </para>
        /// </summary>
        /// <param name="abortingOnly">
        /// When true, considers only guards that claim <see cref="ConditionalExecution.StopsItsOwnBranch"/>. This
        /// is the whole of the entry-only change: a plain <see cref="ConditionalExecution"/> reports false
        /// there and so is skipped once its owner is running, while entry still asks every guard.
        /// </param>
        /// <param name="writeStatus">
        /// Whether a guard records its answer in its own <c>LastExecutionStatus</c>. False for the
        /// preemption poll, which asks guards on nodes that are <em>not</em> running: writing there would
        /// make the canvas and the why-panel show a live result for an idle branch, a visible change nobody
        /// asked for.
        /// </param>
        /// <param name="fresh">
        /// Whether guards must recompute rather than answer from cache. True only at entry — see
        /// <see cref="ConditionalExecution.Ask"/> for why that asymmetry is worth paying for.
        /// </param>
        private ConditionalExecution FirstFailingGuard(bool abortingOnly, bool writeStatus = true, bool fresh = false)
        {
            var guards = Guards;

            // Indexed rather than foreach: this runs on every node on every tick, and a guard's evaluation
            // can reach arbitrary user code, so the list is walked without an enumerator allocation.
            for (int i = 0; i < guards.Count; i++)
            {
                var conditionalExecution = guards[i];

                if (abortingOnly && !conditionalExecution.StopsItsOwnBranch) continue;

                bool passed = writeStatus
                    ? conditionalExecution.EvaluateInternal(fresh)
                    : conditionalExecution.Ask(fresh);

                Debugging.BehaviorTreeRecorder.GuardEval(this, conditionalExecution, passed);

                if (!passed) return conditionalExecution;
            }

            return null;
        }

        [DoNotSerialize]
        private bool? hasPollableGuards;

        /// <summary>
        /// Whether any guard here can bid to take control from a lower-priority sibling.
        /// <para>
        /// Cached, because a composite asks this of its higher-priority children on every tick and the
        /// answer only changes when guards are re-armed — which happens once, in
        /// <see cref="BehaviorTreeGraph.OnAwake"/>. Both mutators below reset it, so the cache cannot
        /// outlive the list it summarises. Opt-in is what keeps the default at zero: a selector whose
        /// children carry no reactive guards pays one bool check per child and nothing else.
        /// </para>
        /// </summary>
        public bool HasTakeOverGuard
        {
            get
            {
                if (hasPollableGuards.HasValue) return hasPollableGuards.Value;

                var guards = Guards;
                bool any = false;

                for (int i = 0; i < guards.Count; i++)
                {
                    if (!guards[i].TakesOverLowerPriority) continue;

                    any = true;
                    break;
                }

                hasPollableGuards = any;
                return any;
            }
        }

        /// <summary>
        /// Whether every guard here answers true right now — the same question <see cref="OnNodeEnter"/>
        /// asks, without entering anything.
        /// <para>
        /// <b>Every</b> guard, not just the preempting ones, and that is the whole correctness of the poll.
        /// Suppose Attack carries a reactive <c>targetInRange</c> and a plain <c>hasAttackToken</c>. Polling
        /// only the reactive one would abort Idle the moment the target came into range, then fail Attack's
        /// real entry check on the token, fall through, and restart Idle from scratch — killing it for
        /// nothing, potentially every tick. One poll, one answer: <em>would this child enter right now?</em>
        /// </para>
        /// <para>
        /// Mixing the two kinds on one node is therefore deliberate and useful: the reactive guard is the
        /// trigger, and the conditional is a veto that participates in the decision without being able to
        /// fire it.
        /// </para>
        /// </summary>
        public bool WouldEnterNow() => FirstFailingGuard(abortingOnly: false, writeStatus: false) == null;

        /// <summary>The first guard here that claims the right to preempt, or null. Names the bid in a recording.</summary>
        public ConditionalExecution FirstTakeOverGuard()
        {
            var guards = Guards;

            for (int i = 0; i < guards.Count; i++)
            {
                if (guards[i].TakesOverLowerPriority) return guards[i];
            }

            return null;
        }

        /// <summary>
        /// The guard that turned this node away at its last entry attempt, and the frame that attempt
        /// happened on. Together they let the same-frame tick reuse the entry verdict instead of taking it
        /// again — see <see cref="OnUpdateInternal"/>.
        /// <para>
        /// Deliberately not initialised. A <c>[DoNotSerialize]</c> field on a deserialized node can arrive
        /// with its initialiser never having run, so the frame stamp is only ever trusted when the guard
        /// reference beside it is non-null — which nothing but <see cref="OnNodeEnter"/> can make true.
        /// </para>
        /// </summary>
        [DoNotSerialize]
        private ConditionalExecution entryRefusal;

        [DoNotSerialize]
        private int entryRefusalFrame;

        /// <summary>
        /// Asks <em>every</em> guard, of either kind. Entry is the one walk that is never filtered: a
        /// doorman that no longer interrupts must still decide entry exactly as it always did.
        /// <para>
        /// The verdict is recorded, because the tick that follows it in the same frame needs the answer and
        /// must not go and get its own — see <see cref="OnUpdateInternal"/>.
        /// </para>
        /// </summary>
        public sealed override void OnNodeEnter()
        {
            var failed = FirstFailingGuard(abortingOnly: false, fresh: true);

            entryRefusal = failed;
            entryRefusalFrame = Time.frameCount;

            if (failed != null)
            {
                // Never started, as opposed to started and killed. The two read differently to whoever is
                // asking why this branch did not happen, so they are recorded as different events.
                Debugging.BehaviorTreeRecorder.NodeSkipped(this, failed);
                return;
            }

            Debugging.BehaviorTreeRecorder.NodeEnter(this);

            base.OnNodeEnter();
        }

        /// <summary>
        /// Records the exit and the status it ended on.
        /// <para>
        /// <see cref="BaseGraphNode{TGraph,TNode,TNodeTransition}.OnNodeExit"/> is called on nodes that never
        /// ran and does nothing for them, so <see cref="BaseGraphNode{TGraph,TNode,TNodeTransition}.IsRunning"/>
        /// is read first — base clears it — and only a real exit is recorded. Without that the recording fills
        /// with exits for branches a selector never reached.
        /// </para>
        /// </summary>
        public sealed override void OnNodeExit()
        {
            bool wasRunning = IsRunning;

            base.OnNodeExit();

            if (wasRunning) Debugging.BehaviorTreeRecorder.NodeExit(this, LastExecutionStatus);
        }

        /// <summary>
        /// Asks only the guards that claim <see cref="ConditionalExecution.StopsItsOwnBranch"/>.
        /// <para>
        /// A plain <see cref="ConditionalExecution"/> does not, so it no longer interrupts a branch it
        /// already admitted. That is a deliberate breaking change, and it is what makes the node class
        /// usable for the things it was always meant to express: a <c>RandomChance</c> gate that re-rolled
        /// every frame killed its branch within one tick, and an expensive one-shot validation could not be
        /// afforded at all.
        /// </para>
        /// </summary>
        public sealed override ExecutionStatus OnUpdateInternal()
        {
            // A composite ticks a child on the same frame it declined to enter it — Selector calls
            // OnNodeEnter and then OnUpdateInternal in one iteration, and a refused entry returns early
            // rather than stopping the tick. So the entry decision has to be honoured here too, or a branch
            // whose doorman turned it away runs anyway.
            //
            // Honoured, not retaken. Re-asking the guards is not the same question asked twice: nothing
            // promises a guard is stable within a frame. A RandomChance re-rolls, and a Function reading a
            // fact another agent writes can change under it — so entry could answer false and the tick that
            // follows it answer true, and then OnUpdate runs on a node whose OnEnter never did. That is the
            // stale-state hazard exactly: a Wait ticking a timer it never set, a rotation slerping from a
            // pose it never captured. Reusing the verdict makes "would this node enter" one decision per
            // frame, which is both cheaper and the only answer that can be consistent.
            //
            // The frame stamp is what keeps it a verdict rather than a memory. Parallel and Entry enter
            // their children once and then tick them every frame afterwards, so a refusal must not outlive
            // the frame that produced it, or a branch turned away once could never start again.
            if (!IsRunning && entryRefusal != null && entryRefusalFrame == Time.frameCount)
            {
                LastExecutionStatus = ExecutionStatus.Failure;
                return ExecutionStatus.Failure;
            }

            // Once the node is running the entry decision is spent, and only a guard that claims the right
            // to interrupt gets a say.
            var failed = FirstFailingGuard(abortingOnly: IsRunning);

            if (failed != null)
            {
                // The guard that did it is named here and nowhere else: by the time the parent composite
                // sees the Failure, which guard caused it is gone.
                //
                // A composite ticks a child on the same frame it declined to enter it, so this also runs
                // for nodes that never started. Only a node that was running can be aborted; the other
                // case was already recorded as skipped by OnNodeEnter, and recording it twice would
                // report every declined branch as though something had interrupted it.
                if (IsRunning) Debugging.BehaviorTreeRecorder.NodeAborted(this, failed);

                LastExecutionStatus = ExecutionStatus.Failure;
                return ExecutionStatus.Failure;
            }

            return base.OnUpdateInternal();
        }
    }
}

