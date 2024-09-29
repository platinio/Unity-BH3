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

        public override void SortChildren()
        {
            children = GetChildren().OrderBy(_ => Random.value).ToList();
        }
    }
}