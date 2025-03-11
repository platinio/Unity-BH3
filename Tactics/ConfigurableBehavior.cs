using System.Collections.Generic;
using ArcaneOnyx.BehaviorTree;
using Platinio;
using UnityEngine;

namespace ArcaneOnyx.AIDesigner
{
    [System.Serializable]
    public class ConfigurableBehavior
    {
        [SerializeField, Parameterized] private BehaviorTreeGraphAsset behaviorTreeGraphAsset;
        [SerializeReference, HideInInspector] private List<IBlockVariable> parameters;

        public BehaviorTreeGraphAsset BehaviorTreeGraphAsset => behaviorTreeGraphAsset;
    }
}