using System.Linq;
using ArcaneOnyx.GraphCore;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Composite/Random Sequence")]
    public class RandomSequence : Sequence
    {
        public override string NodeName => "Random Sequence";
        protected override string NodeIconPath => "NodeIcons/RandomSequence";

        public override void SortChildren()
        {
            children = GetChildren().OrderBy(_ => Random.value).ToList();
        }
    }
}