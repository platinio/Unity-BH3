using System;
using System.Collections.Generic;
using System.Linq;
using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using Object = UnityEngine.Object;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Flow/Run Behavior Tree Graph")]
    public class RunBehaviorTreeGraphNode : GameplayNode, IRefreshesContractPorts
    {
        [Serialize, Inspectable]
        private BehaviorTreeGraphAsset behaviorTreeGraphAsset;

        private BehaviorTreeGraphAsset behaviorTreeGraphAssetInstance = null;

        /// <summary>
        /// The sub-tree's contract as this node remembers it, and the only thing <see cref="Definition"/>
        /// reads. See <see cref="BehaviorTreeGraphParameter"/> for why it is copied rather than looked up.
        /// </summary>
        [Serialize]
        private List<BehaviorTreeGraphParameter> parameters = new();

        [DoNotSerialize]
        private readonly Dictionary<string, ValueInput> parameterPorts = new();

        [DoNotSerialize]
        private BehaviorTreeVariableScope innerScope;

        public IReadOnlyList<BehaviorTreeGraphParameter> Parameters => parameters;

        public BehaviorTreeGraphAsset BehaviorTreeGraphAsset => behaviorTreeGraphAsset;
        public BehaviorTreeGraph BehaviorTreeGraphInstance => BehaviorTreeGraphAssetInstance.graph;
      
        public bool HasBehaviorTreeGraphInstance => behaviorTreeGraphAssetInstance != null;
       
        public void SetBehaviorTreeGraphAsset(BehaviorTreeGraphAsset asset)
        {
            behaviorTreeGraphAsset = asset;
            behaviorTreeGraphAssetInstance = null;
        }

        public override string Description => "Executes BehaviorTreeGraphAsset";

        public BehaviorTreeGraphAsset BehaviorTreeGraphAssetInstance
        {
            get
            {
                if (behaviorTreeGraphAssetInstance == null)
                {
                    behaviorTreeGraphAssetInstance = Object.Instantiate(behaviorTreeGraphAsset);
                }

                return behaviorTreeGraphAssetInstance;
            }
        }
      
        public override string NodeName
        {
            get
            {
                if (behaviorTreeGraphAsset == null) return "Missing Graph!";
                return behaviorTreeGraphAsset.name;
            }
        }
        
        /// <summary>
        /// Declares one port per remembered parameter. Reads <see cref="parameters"/> and nothing else, so it
        /// is deterministic from this node's own serialized data and cannot be affected by whether the
        /// sub-tree asset happens to be loaded yet.
        /// </summary>
        protected override void Definition()
        {
            base.Definition();

            parameterPorts.Clear();

            if (parameters == null) return;

            foreach (var parameter in parameters)
            {
                if (parameter == null || string.IsNullOrEmpty(parameter.Name) || parameter.Type == null) continue;
                if (parameterPorts.ContainsKey(parameter.Name)) continue;

                // Optional declares a default so the port is safe to leave unconnected; required declares none,
                // which makes an unconnected one the unset-port case bt_verify already reports.
                var port = parameter.Optional
                    ? ValueInput(parameter.Type, parameter.Name, parameter.DefaultValue)
                    : ValueInput(parameter.Type, parameter.Name);

                parameterPorts[parameter.Name] = port;
            }
        }

        /// <summary>
        /// Rebuilds the remembered contract from the sub-tree's own required and optional declarations.
        /// <para>
        /// Returns one line per connection this cost. A refresh that removes a port removes whatever fed it,
        /// and an author who is not told has no way to notice until the value silently stops arriving —
        /// <c>DescribeContractDrift</c> warns beforehand, but only if somebody ran it. Reporting is the
        /// mitigation; there is deliberately no undo.
        /// </para>
        /// </summary>
        public List<string> RefreshParameters()
        {
            var lost = DescribeConnectionsLostByRefresh();

            parameters = ReadContract();

            Define();
            PortsChanged();

            return lost;
        }

        /// <summary>
        /// Which currently-connected ports a refresh will stop declaring, named with what feeds them. Computed
        /// before the rebuild, because afterwards the connection has already changed shape.
        ///
        /// <para>
        /// Not dropped: Define() runs NodePreservation.RestoreTo, which keeps the removed port as an invalid
        /// ghost under the same key and reattaches the wire to it as an invalid connection, drawn red, and
        /// a later contract that declares the parameter again brings the wire back. The previous wording
        /// said "dropped", which was never what happened -- see VisualScriptGraphVariable, where the same
        /// refresh was probed against the live editor.
        /// </para>
        /// </summary>
        private List<string> DescribeConnectionsLostByRefresh()
        {
            var lost = new List<string>();
            if (parameters == null || parameters.Count == 0) return lost;

            var current = ReadContract();

            foreach (var parameter in parameters)
            {
                if (parameter?.Name == null) continue;
                if (current.Exists(candidate => candidate.Name == parameter.Name)) continue;
                if (!parameterPorts.TryGetValue(parameter.Name, out var port)) continue;

                var source = ContractPorts.DescribeWhatFeeds(port);
                if (source == null) continue;

                lost.Add($"'{NodeName}': parameter '{parameter.Name}' is no longer declared; its connection " +
                         $"from {source} is invalid until the sub-tree declares it again or it is rewired.");
            }

            return lost;
        }

        /// <summary>What the sub-tree declares today, required first and then optional.</summary>
        private List<BehaviorTreeGraphParameter> ReadContract()
        {
            var contract = new List<BehaviorTreeGraphParameter>();
            if (behaviorTreeGraphAsset == null) return contract;

            Collect(contract, behaviorTreeGraphAsset.requiredDeclarations, false);
            Collect(contract, behaviorTreeGraphAsset.optionalDeclarations, true);

            return contract;
        }

        private static void Collect(List<BehaviorTreeGraphParameter> contract, VariableDeclarations declarations, bool optional)
        {
            if (declarations == null) return;

            foreach (var declaration in declarations)
            {
                if (string.IsNullOrEmpty(declaration.name)) continue;
                if (contract.Exists(parameter => parameter.Name == declaration.name)) continue;

                // The declared value carries the type. A required entry's value is only ever a type carrier,
                // so its default is dropped -- a required port has to be connected.
                var type = declaration.value?.GetType() ?? typeof(object);

                contract.Add(new BehaviorTreeGraphParameter(
                    declaration.name, type, optional, optional ? declaration.value : null));
            }
        }

        /// <summary>
        /// How the remembered contract differs from what the sub-tree declares now, one line per difference and
        /// empty when they agree. This is the check that makes the copy safe: a renamed or retyped parameter
        /// shows up here instead of silently unwiring this node.
        /// </summary>
        public List<string> DescribeContractDrift()
        {
            var drift = new List<string>();
            if (behaviorTreeGraphAsset == null) return drift;

            var current = ReadContract();
            var remembered = parameters ?? new List<BehaviorTreeGraphParameter>();

            foreach (var parameter in current)
            {
                var match = remembered.Find(candidate => candidate?.Name == parameter.Name);

                if (match == null)
                {
                    drift.Add($"'{parameter.Name}' is declared by {behaviorTreeGraphAsset.name} but has no port here.");
                }
                else if (!match.Matches(parameter))
                {
                    drift.Add($"'{parameter.Name}' is {parameter} in {behaviorTreeGraphAsset.name} but {match} here.");
                }
            }

            foreach (var parameter in remembered)
            {
                if (parameter == null || current.Exists(candidate => candidate.Name == parameter.Name)) continue;

                drift.Add($"'{parameter.Name}' has a port here but {behaviorTreeGraphAsset.name} no longer declares it. " +
                          "Refreshing will remove the port and drop whatever feeds it.");
            }

            return drift;
        }

        /// <summary>
        /// What is wrong with this call site, for the canvas to draw. The sub-tree node has carried the same
        /// invisible-drift problem as the Function node since parameters were added to it, so it reports
        /// through the same capability rather than getting its own answer.
        /// </summary>
        public override void CollectProblems(List<NodeProblem> into)
        {
            base.CollectProblems(into);

            if (behaviorTreeGraphAsset == null)
            {
                into.Add(new NodeProblem(NodeProblemSeverity.Error,
                    "No sub-tree assigned, so this node runs nothing.",
                    "Pick a tree in this node's inspector."));
                return;
            }

            foreach (var line in DescribeContractDrift())
            {
                into.Add(new NodeProblem(NodeProblemSeverity.Error, line, "Refresh Parameters.",
                    new RefreshContractPortsRepair("Refresh Parameters")));
            }

            // An unfed required parameter is just an unset port, which the base reports for every node.
        }

        public override void OnAwake()
        {
            BehaviorTreeGraphInstance.OnAwake();
        }

        public void ThrowIfCausesRecursion()
        {
            var runStack = new Stack<BehaviorTreeGraphAsset>(new[] { behaviorTreeGraphAsset });
            if (!GraphWillCauseRecursion(runStack)) return;
          
            string stackTraceLog = "";
               
            var runStackList = runStack.ToList();
            string firstNode = runStack.First().name;
                
            while (runStackList.Count > 0)
            {
                var graphAsset = runStackList[0];
                stackTraceLog += $"{graphAsset.name} ->";
                runStackList.RemoveAt(0);
            }

            stackTraceLog += $" {firstNode}";
               
            throw new Exception("RunBehaviorTreeNode causes recursion stack trace: " + stackTraceLog);
        }

        /// <summary>
        /// Reads the parameter ports and writes them into the branch's own scope before it runs.
        /// <para>
        /// Done on enter rather than once at awake so a parameter fed by a variable read or a script graph is
        /// re-evaluated every time the branch starts, the way an argument is evaluated at each call.
        /// </para>
        /// </summary>
        public override void OnEnter()
        {
            ApplyParameters();

            BehaviorTreeGraphInstance.OnEnter();
        }

        private void ApplyParameters()
        {
            if (innerScope == null || parameters == null) return;

            foreach (var parameter in parameters)
            {
                if (parameter?.Name == null) continue;
                if (!parameterPorts.TryGetValue(parameter.Name, out var port)) continue;

                // A required port with nothing connected throws here, naming the parameter and this node,
                // rather than failing later inside the branch with no sign of where the value was owed.
                try
                {
                    innerScope.Set(parameter.Name, port.GetValue());
                }
                catch (Exception e)
                {
                    throw new Exception(
                        $"'{NodeName}' cannot supply '{parameter.Name}' to {behaviorTreeGraphAsset?.name}: " +
                        "the port has nothing connected and declares no default.", e);
                }
            }
        }

        public bool GraphWillCauseRecursion(Stack<BehaviorTreeGraphAsset> runStack)
        {
            var graphAsset = runStack.Peek();
            if (graphAsset == null) return false;
            
            foreach (var node in graphAsset.graph.Nodes)
            {
                if (node is RunBehaviorTreeGraphNode runBehaviorTreeGraphNode)
                {
                    if (runStack.Contains(runBehaviorTreeGraphNode.behaviorTreeGraphAsset)) return true;
                    
                    runStack.Push(runBehaviorTreeGraphNode.behaviorTreeGraphAsset);
                    if (GraphWillCauseRecursion(runStack)) return true;

                    runStack.Pop();
                }
            }

            return false;
        }

        public override ExecutionStatus OnUpdate()
        {
            return BehaviorTreeGraphInstance.OnUpdate();
        }

        /// <summary>
        /// Passes the frame's late hook down into the branch, the way <see cref="OnEnter"/> and
        /// <see cref="OnUpdate"/> already pass theirs.
        ///
        /// <para>
        /// Without this a node that overrides the hook works at the top level and silently stops the day
        /// someone refactors its branch into a sub-tree — a behaviour change with no error, no warning, and
        /// no diff at the node itself. The two hooks were the only ones the forwarding missed.
        /// </para>
        ///
        /// <para>
        /// Guarded on <see cref="HasBehaviorTreeGraphInstance"/> for the reason <see cref="OnExit"/> is:
        /// reading the instance <em>clones the asset on first access</em>, so an unguarded read here would
        /// make a per-frame hook instantiate a branch that had never run. The graph reached this way runs
        /// the same <c>IsRunning</c> filter over its own nodes, so nesting costs a walk per running branch
        /// rather than a hook on an idle one.
        /// </para>
        /// </summary>
        public override void OnLateUpdate()
        {
            if (!HasBehaviorTreeGraphInstance) return;

            BehaviorTreeGraphInstance.OnLateUpdate();
        }

        /// <inheritdoc cref="OnLateUpdate"/>
        public override void OnFixedUpdate()
        {
            if (!HasBehaviorTreeGraphInstance) return;

            BehaviorTreeGraphInstance.OnFixedUpdate();
        }

        /// <summary>
        /// Exits the branch's own nodes, the way <see cref="OnEnter"/> and <see cref="OnUpdate"/> reach into it.
        /// </summary>
        public override void OnExit()
        {
            base.OnExit();

            if (!HasBehaviorTreeGraphInstance) return;

            foreach (var node in BehaviorTreeGraphInstance.Nodes)
            {
                node.OnNodeExit();
            }
        }

        /// <summary>
        /// Releases the instance this call site made, and everything nested inside it.
        /// <para>
        /// The clone is per call site per agent — that is what stops two uses of one branch sharing state,
        /// and it is also why nothing else can free it. The machine destroys only the root instance it made
        /// itself, and <see cref="BehaviorTreeGraph.OnDestroy"/> cascades node destruction without knowing
        /// what any node owns. So until this existed, every sub-tree clone outlived the agent that created
        /// it, for the whole session — multiplying with exactly the nested, modular trees this tool
        /// encourages, and invisible until a wave-based scene has spawned a few hundred agents.
        /// </para>
        /// <para>
        /// Reads the <em>field</em>, never <see cref="BehaviorTreeGraphAssetInstance"/>. The property's getter
        /// instantiates on demand, so asking it here would manufacture a clone purely in order to destroy it —
        /// and on a call site the agent never entered, that is a clone brought into existence for the first
        /// time at teardown. <c>ExitingASubTreeThatNeverRanDoesNotInstantiateIt</c> pins the same trap for
        /// <see cref="OnExit"/>.
        /// </para>
        /// </summary>
        public override void OnDestroy()
        {
            base.OnDestroy();

            if (behaviorTreeGraphAssetInstance == null) return;

            // Innermost first: a nested call site inside this branch owns a clone of its own, and this walk is
            // the only thing that reaches it. Destroying the outer instance first would cut the path to it and
            // leak precisely the deep trees that cost the most.
            behaviorTreeGraphAssetInstance.graph.OnDestroy();

            // Outside play mode Destroy refuses and the object survives, which would make this method quietly
            // do nothing in exactly the edit-mode tests written to prove it works. ReactiveGuard reads the
            // clock through the same switch for the same reason.
            if (UnityEngine.Application.isPlaying) Object.Destroy(behaviorTreeGraphAssetInstance);
            else Object.DestroyImmediate(behaviorTreeGraphAssetInstance);

            behaviorTreeGraphAssetInstance = null;
        }

        public override void SetMachine(IGraphMachine machine)
        {
            base.SetMachine(machine);

            var nodes = BehaviorTreeGraphInstance.Nodes;
            foreach (var node in nodes)
            {
                node.SetMachine(machine);
            }
        }

        /// <summary>
        /// Opens a scope around the instance this node runs and hands that one to the sub-tree, so the branch
        /// gets its own variables while still seeing the caller's through the parent link.
        /// </summary>
        public override void SetVariableScope(BehaviorTreeVariableScope scope)
        {
            base.SetVariableScope(scope);

            var instance = BehaviorTreeGraphAssetInstance;
            if (instance == null) return;

            innerScope = new BehaviorTreeVariableScope(instance.declarations, scope);
            innerScope.SeedDefaults(instance.optionalDeclarations);

            // The scope chain is already one scope per call site, so it is what the recorder numbers to tell
            // two uses of the same branch apart. Registered here because this is the moment the call site
            // comes into existence.
            Debugging.BehaviorTreeRecorder.RegisterCallSite(this, innerScope, scope);

            foreach (var node in instance.graph.Nodes)
            {
                node.SetVariableScope(innerScope);
            }
        }

        /// <summary>Reaches the branch's nodes, the way <see cref="SetMachine"/> and
        /// <see cref="SetVariableScope"/> do — otherwise everything inside a sub-tree goes unrecorded.</summary>
        public override void SetFlightRecorder(Debugging.BehaviorTreeFlightRecorder recorder)
        {
            base.SetFlightRecorder(recorder);

            var instance = BehaviorTreeGraphAssetInstance;
            if (instance == null) return;

            foreach (var node in instance.graph.Nodes)
            {
                node.SetFlightRecorder(recorder);
            }
        }

        public override void CanvasUpdate()
        {
            base.CanvasUpdate();

            foreach (var node in BehaviorTreeGraphInstance.Nodes)
            {
               node.CanvasUpdate(); 
            }
        }
    }
}

