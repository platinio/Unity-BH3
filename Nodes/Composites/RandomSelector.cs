using System.Linq;
using Platinio.GraphCore;
using UnityEngine;

namespace Platinio.BehaviourTree
{
    [GraphCreateMenu("Composite/Random Selector")]
    public class RandomSelector : Selector
    {
        public override string NodeName => "Random Selector";
        protected override string NodeIconPath => "NodeIcons/RandomSelector";

        public override void SortChildren()
        {
            m_children = GetChildren().OrderBy(_ => Random.value).ToList();
        }
    }
}