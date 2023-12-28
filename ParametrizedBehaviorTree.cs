using System.Collections.Generic;
using Platinio.BehaviorTree;
using UnityEngine;

namespace Platinio
{
    [CreateAssetMenu(menuName = "Visual Scripting/Parameterized Behavior Tree Graph")]
    public class ParametrizedBehaviorTree : ScriptableObject
    {
        [SerializeField] private BehaviorTreeGraphAsset behaviorTreeGraphAsset;
        [SerializeReference, SubclassSelector] private List<BlockVariable> defaultParameters;

        public BehaviorTreeGraphAsset BehaviorTreeGraphAsset
        {
            get
            {
                if (behaviorTreeGraphAsset == null)
                {
                    Debug.LogError($"Behavior tree graph asset is null for parametrized behavior tree {name}");
                    return null;
                }

                return behaviorTreeGraphAsset;
            }
        } 
    }
}

