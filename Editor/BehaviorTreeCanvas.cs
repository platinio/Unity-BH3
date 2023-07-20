using System;
using System.Collections.Generic;
using Platinio.GraphCore;
using Unity.VisualScripting;

namespace Platinio.BehaviorTree
{
    [Canvas(typeof(BehaviorTreeGraph))]
    public class BehaviorTreeCanvas : BaseCanvas<BehaviorTreeGraph, BehaviorTreeNode, BehaviorTreeTransition>
    {
        public BehaviorTreeCanvas(BehaviorTreeGraph graph) : base(graph) { }

        protected override IEnumerable<Type> GetValidNodes() => new List<Type>()
        {
            typeof(Composite),
            typeof(ContainerNode),
            typeof(Decorator),
            typeof(GameplayNode)
        };
    }
}
