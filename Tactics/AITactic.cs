using System.Collections.Generic;
using ArcaneOnyx.AIEntities;
using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.ScriptableObjectDatabase;
using Platinio.Considerations;
using UnityEngine;

namespace ArcaneOnyx.AIDesigner
{
    public enum TacticWeight
    {
        Lowest = 0,
        Low = 1,
        Medium = 2,
        High = 3,
        Hightest = 4
    }
    
    [System.Serializable]
    public class AITactic : ScriptableItem
    {
        [SerializeField, TextArea] protected string description;
        [SerializeField] protected TacticWeight weight;

        [Header("Evaluation")] [SerializeReference, SubclassSelector]
        private List<IScoreEvaluator> scoreEvaluators;
        
        [Header("Tactic Config")] 
        [SerializeField] private bool canBeRestarted;
        [SerializeField] private ConfigurableBehavior configurableBehavior;
      
        public int Weight => (int) weight;
        public bool CanBeRestarted => canBeRestarted;
        public BehaviorTreeGraphAsset BehaviorTreeGraphAsset => configurableBehavior.BehaviorTreeGraphAsset;

        public void OnEnter(AIEntity entity)
        {
            
        }

        public void OnExit(AIEntity entity)
        {
            
        }
        
        public float Evaluate(AIEntity evaluator)
        {
            float score = 1;
            foreach (var scoreEvaluator in scoreEvaluators)
            {
                score *= scoreEvaluator.Evaluate(evaluator);
            }

            return score;
        }
    }
}

