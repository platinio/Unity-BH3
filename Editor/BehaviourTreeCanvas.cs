using System;
using System.Collections.Generic;
using Platinio.GraphCore;
using Unity.VisualScripting;

namespace Platinio.BehaviourTree
{
    [Canvas(typeof(BehaviourTreeGraph))]
    public class BehaviourTreeCanvas : BaseCanvas<BehaviourTreeGraph, BehaviourTreeNode, BehaviourTreeTransition>
    {
        public BehaviourTreeCanvas(BehaviourTreeGraph graph) : base(graph) { }

        protected override IEnumerable<Type> GetValidNodes() => new List<Type>()
        {
            typeof(Composite),
            typeof(ContainerNode),
            typeof(Decorator),
            typeof(GameplayNode)
        };
    }
}
