using System.Linq;
using Platinio.GraphCore;
using UnityEngine;

namespace Platinio.BehaviorTree
{
    [GraphCreateMenu("Composite/Random Sequence")]
    public class RandomSequence : Sequence
    {
        public override string NodeName => "Random Sequence";
        protected override string NodeIconPath => "NodeIcons/RandomSequence";

        public override void SortChildren()
        {
            m_children = GetChildren().OrderBy(_ => Random.value).ToList();
        }
    }
}