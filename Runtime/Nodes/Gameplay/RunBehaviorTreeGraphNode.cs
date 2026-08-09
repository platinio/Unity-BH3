using System;
using System.Collections.Generic;
using System.Linq;
using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using Object = UnityEngine.Object;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Gameplay/Run Behavior Tree Graph")]
    public class RunBehaviorTreeGraphNode : GameplayNode
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

        /// <summary>
        /// Whether the branch has already been instantiated, without instantiating it.
        /// <para>
        /// <see cref="BehaviorTreeGraphAssetInstance"/> clones on first read, so a tool that merely wants to
        /// look inside a running tree would clone an asset per call site just by asking. Inspection code tests
        /// this first and skips what has not been entered yet.
        /// </para>
        /// </summary>
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
        /// Deliberately explicit rather than automatic. Ports are matched by key, so silently redeclaring them
        /// when a branch changes would drop every connection whose name no longer exists, at every call site
        /// at once, with nothing said. Refreshing on request keeps that a decision someone makes and can see
        /// the result of; <see cref="DescribeContractDrift"/> is how they find out it is needed.
        /// </para>
        /// </summary>
        public void RefreshParameters()
        {
            parameters = ReadContract();

            // Define() re-runs Definition(), which is what actually builds the ports; PortsChanged() only
            // announces that they moved. Connections are re-resolved by key, so anything feeding a port whose
            // name survived stays wired, and anything feeding a name that did not is dropped — the change
            // DescribeContractDrift warns about before it happens.
            Define();
            PortsChanged();
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
        /// <para>
        /// The instance is cloned per node, so two call sites running the same branch asset hold two separate
        /// scopes and cannot overwrite one another. The sub-tree's optional declarations seed it, which is
        /// what lets a branch ship a usable default instead of demanding the agent declare everything.
        /// </para>
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

