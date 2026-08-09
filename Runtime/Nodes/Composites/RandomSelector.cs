using System.Linq;
using ArcaneOnyx.GraphCore;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Composite/Random Selector")]
    public class RandomSelector : Selector
    {
        public override string NodeName => "Random Selector";
        protected override string NodeIconPath => "NodeIcons/RandomSelector";
        public override string Description => "Executes child nodes in random order.\nExecution ends when any child node returns SUCCESS.";

        public override void OnExit()
        {
            base.OnExit();
            SortChildren();
        }

        public override void SortChildren()
        {
            children = GetChildren().OrderBy(_ => Random.value).ToList();
        }
    }
}