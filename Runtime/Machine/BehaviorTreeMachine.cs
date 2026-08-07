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
        
        public ExecutionStatus LastExecutionStatus => lastExecutionStatus;

        public BehaviorTreeGraphAsset GraphInstance => graphInstance;
        public BehaviorTreeGraphAsset GraphAsset => nest.macro;
        public BehaviorTreeGraphAsset OriginalMacro { get; private set; }

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
            Variables.declarations.Set("This", gameObject);
            
            if (hasGraph)
            {
                graphInstance = null;
                OriginalMacro = nest.macro;

                if (nest.macro != null)
                {
                    graphInstance = Instantiate(nest.macro);
                    nest.SwitchToEmbed(graphInstance.graph);
                }
                
                behaviorTreeGraph = nest.embed;
                var graph = graphInstance == null ? nest.embed : graphInstance.graph;
                
                //throws an exception if subgraphs causes recursion
                graph.ThrowIfCausesRecursion();
                
                var nodes = behaviorTreeGraph.Nodes;
                foreach (var node in nodes)
                {
                    node.SetMachine(this);
                }

                // Before the scopes are built, because building them is when call sites are registered and a
                // call site can only be registered against a recorder that already exists.
                Debugging.BehaviorTreeRecorder.Attach(this, OriginalMacro != null ? OriginalMacro.name : name);
                Debugging.BehaviorTreeRecorder.Bind(behaviorTreeGraph, FlightRecorder);

               #if UNITY_EDITOR
                try
                {
                    OverrideGraphAndSubGraphVariables(graphInstance, graph);
                    behaviorTreeGraph.OnAwake();
                }
                catch (Exception e)
                {
                    string macroName = nest?.macro?.name;
                    Debug.LogError($"BehaviorTree = {macroName} Method = OnAwake() Exception = {e}", gameObject);
                    throw;
                }
                #else
                OverrideGraphAndSubGraphVariables(graphInstance, graph);
                behaviorTreeGraph.OnAwake();
                #endif
            }
        }

        /// <summary>
        /// Builds the root scope and opens a nested one for every sub-tree.
        /// <para>
        /// The root scope holds the agent's variables, so anything an agent declares is visible to every
        /// branch however deeply nested. Each <c>RunBehaviorTreeGraphNode</c> then opens a child scope around
        /// its own instance, seeded with that branch's optional defaults — so a branch reads its own values
        /// first and the agent's only when it declares none, and its writes stay inside it.
        /// </para>
        /// </summary>
        private void OverrideGraphAndSubGraphVariables(BehaviorTreeGraphAsset graphAsset, BehaviorTreeGraph graph)
        {
            OverrideGraphVariables(graphAsset);

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

        public void Switch(BehaviorTreeGraphAsset behaviorTreeGraphAsset)
        {
            if (behaviorTreeGraphAsset == null) return;
            
            graphInstance = behaviorTreeGraphAsset;
            lastExecutionStatus = ExecutionStatus.Running;
            behaviorTreeGraph = behaviorTreeGraphAsset.graph;
            
            var nodes = behaviorTreeGraph.Nodes;

            foreach (var node in nodes)
            {
                node.SetMachine(this);
            }

            Debugging.BehaviorTreeRecorder.Bind(behaviorTreeGraph, FlightRecorder);

            OverrideGraphAndSubGraphVariables(behaviorTreeGraphAsset, behaviorTreeGraphAsset.graph);
            nest.SwitchToEmbed(behaviorTreeGraph);
        }

        private void Start()
        {
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
                if (lastExecutionStatus == ExecutionStatus.Success || lastExecutionStatus == ExecutionStatus.Failure) return;

                // Opens the tick before the tree runs, so everything the tree does this frame is stamped with
                // the same tick and ordered within it.
                Debugging.BehaviorTreeRecorder.BeginTick(this);

                lastExecutionStatus = behaviorTreeGraph.OnUpdate();

            }
        }

        private void LateUpdate()
        {
            if (hasGraph && behaviorTreeGraph != null)
            {
                behaviorTreeGraph.OnLateUpdate();
            }
        }

        private void FixedUpdate()
        {
            if (hasGraph && behaviorTreeGraph != null)
            {
                behaviorTreeGraph.OnFixedUpdate();
            }
        } 
        

        protected override void OnDestroy()
        {
            Debugging.BehaviorTreeRecorder.Detach(this);

            if (hasGraph && behaviorTreeGraph != null)
            {
                behaviorTreeGraph.OnDestroy();
            }

            if (graphInstance)
            {
                Destroy(graphInstance);
            }
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