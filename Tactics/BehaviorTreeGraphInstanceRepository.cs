using System.Collections.Generic;
using ArcaneOnyx.AIEntities;
using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.GraphCore;
using UnityEngine;

namespace ArcaneOnyx.AIDesigner
{
    public static class BehaviorTreeGraphInstanceRepository
    {
        private static EntityBehaviorTreeGraphRepository entityBehaviorTreeGraphAssetRepository = new();

        public static void DestroyEntityInstances(AIEntity entity)
        {
            entityBehaviorTreeGraphAssetRepository.Destroy(entity);
        }

        public static bool TryGetBehaviorTreeGraphInstance(AIEntity entity, BehaviorTreeGraphAsset graphAsset, out BehaviorTreeGraphAsset graphAssetInstance)
        {
            return entityBehaviorTreeGraphAssetRepository.TryGetBehaviorTreeGraphInstance(entity, graphAsset, out graphAssetInstance);
        }
        
        public static BehaviorTreeGraphAsset GetOrCreateBehaviorTreeGraphInstance(AIEntity entity, BehaviorTreeGraphAsset graphAsset)
        {
            if (entityBehaviorTreeGraphAssetRepository.TryGetBehaviorTreeGraphInstance(entity, graphAsset, out var graphAssetInstance))
            {
                return graphAssetInstance;
            }

            return entityBehaviorTreeGraphAssetRepository.Create(entity, graphAsset);
        }

        public static BehaviorTreeGraphAsset CreateBehaviorTreeInstance(AIEntity entity, AITactic tactic)
        {
            return entityBehaviorTreeGraphAssetRepository.Create(entity, tactic.BehaviorTreeGraphAsset);
        }
    }

    public class EntityBehaviorTreeGraphRepository
    {
        private Dictionary<AIEntity, Dictionary<BehaviorTreeGraphAsset, BehaviorTreeGraphAsset>> entityBehaviorTreeGraphAssets = new();

        public bool TryGetBehaviorTreeGraphInstance(AIEntity entity, BehaviorTreeGraphAsset graphAsset,out BehaviorTreeGraphAsset graphAssetInstance)
        {
            graphAssetInstance = null;
            return entityBehaviorTreeGraphAssets.TryGetValue(entity, out var graphInstanceDict) &&
                    graphInstanceDict.TryGetValue(graphAsset, out graphAssetInstance);
        }
        
        public BehaviorTreeGraphAsset Create(AIEntity entity, BehaviorTreeGraphAsset behaviorTreeGraphAsset)
        {
            if (entityBehaviorTreeGraphAssets.TryGetValue(entity, out var graphInstanceDict))
            {
                if (graphInstanceDict.ContainsKey(behaviorTreeGraphAsset)) return graphInstanceDict[behaviorTreeGraphAsset];
                
                var instance = CreateBehaviorTreeGraphInstance(entity, behaviorTreeGraphAsset);
                graphInstanceDict.Add(behaviorTreeGraphAsset, instance);
                return instance;
            }
            else
            {
                var instance = CreateBehaviorTreeGraphInstance(entity, behaviorTreeGraphAsset);
                entityBehaviorTreeGraphAssets[entity] =
                    new Dictionary<BehaviorTreeGraphAsset, BehaviorTreeGraphAsset>()
                    {
                        { behaviorTreeGraphAsset, instance }
                    };

                return instance;
            }
        }

        private BehaviorTreeGraphAsset CreateBehaviorTreeGraphInstance(AIEntity entity, BehaviorTreeGraphAsset behaviorTreeGraphAsset)
        {
            var instance = Object.Instantiate(behaviorTreeGraphAsset);
            
            foreach (var node in instance.graph.Nodes)
            {
                node.SetMachine(entity.gameObject.GetComponentInChildren<IGraphMachine>());
            }
            
            instance.graph.OnAwake();
            return instance;
        }

        public void Destroy(AIEntity entity)
        {
            if (entityBehaviorTreeGraphAssets.TryGetValue(entity, out var graphInstanceDict))
            {
                foreach (var graphAssetKeyValuePair in graphInstanceDict)
                {
                    if (graphAssetKeyValuePair.Value == null) continue;
                    Object.Destroy(graphAssetKeyValuePair.Value);
                }

                entityBehaviorTreeGraphAssets.Remove(entity);
            }
        }
    }

}

