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
        public override string Description => "Executes child nodes in random order.\nExecution ends when any child node returns FAILURE.";

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