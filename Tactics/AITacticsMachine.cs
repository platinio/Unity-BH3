using System.Collections.Generic;
using ArcaneOnyx.AIEntities;
using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.GraphCore;
using UnityEngine;

namespace ArcaneOnyx.AIDesigner
{
    public class AITacticsMachine : MonoBehaviour
    {
        private HashSet<AITactic> tactics = new();
        private AITactic activeTactic = null;
        private AITactic lastActiveTactic = null;
        private BehaviorTreeGraphAsset activeBehaviorAssetInstance;
        private BehaviorTreeMachine behaviorTreeMachine;
        private Dictionary<string, float> tacticExecutionLog = new();
        private AIEntity entity;
        
        public AITactic LastActiveTactic => lastActiveTactic;
        public AITactic ActiveTactic => activeTactic;
        public IEnumerable<AITactic> Tactics => tactics;
        public Dictionary<string, float> TacticExecutionLog => tacticExecutionLog;


        private void Start()
        {
            GetComponents();
        }

        private void OnDestroy()
        {
            BehaviorTreeGraphInstanceRepository.DestroyEntityInstances(entity);
        }
        
        private void GetComponents()
        {
            behaviorTreeMachine = GetComponent<BehaviorTreeMachine>();
            entity = GetComponent<AIEntity>();
        }
        
        private void Update()
        {
            TryChangeTactic();
        }
        
        private void ChangeTactic(AITactic newTactic)
        {
            if (newTactic == null) return;
            
            //call OnExit
            if (activeBehaviorAssetInstance != null) activeBehaviorAssetInstance.graph.OnExit();
            if (activeTactic != null) activeTactic.OnExit(entity);

            //Update behavior
            lastActiveTactic = activeTactic;
            activeTactic = newTactic;
            activeBehaviorAssetInstance = BehaviorTreeGraphInstanceRepository.GetOrCreateBehaviorTreeGraphInstance(entity, activeTactic.BehaviorTreeGraphAsset);
                
            //call OnEnter
            activeTactic.OnEnter(entity);
            activeBehaviorAssetInstance.graph.OnEnter();
                
            //switch behavior tree
            Debug.Log($"{transform.parent.name} transition {activeTactic.Name} -> {newTactic.Name}");
            behaviorTreeMachine.Switch(activeBehaviorAssetInstance);
            tacticExecutionLog[activeTactic.Name] = Time.time;
        }
        
        public void AddTactic(AITactic tactic)
        {
            tactics.Add(tactic);
            BehaviorTreeGraphInstanceRepository.CreateBehaviorTreeInstance(entity, tactic);
        }
        
        private void TryChangeTactic()
        {
            GetBestScoreTactic(out var newTactic, out var newTacticScore);
            if (newTactic == null || newTacticScore < Mathf.Epsilon) return;
            
            //change behavior if we dont have any active behavior
            if (activeTactic == null)
            {
                ChangeTactic(newTactic);
                return;
            }
            
            //avoid restarting the same behavior if still running
            if (activeTactic == newTactic && (behaviorTreeMachine.LastExecutionStatus == ExecutionStatus.Running && !activeTactic.CanBeRestarted)) return;

            //if current block already finished just replace it
            if (behaviorTreeMachine.LastExecutionStatus != ExecutionStatus.Running || activeTactic == newTactic)
            {
                ChangeTactic(newTactic);
                return;
            }

            int activeBlockWeight = activeTactic.Weight;
            
            //just replace it is the weight is bigger or if they have the same weight use the score
            if (activeBlockWeight < newTactic.Weight || 
                (newTactic.Weight == activeBlockWeight && newTacticScore > activeTactic.Evaluate(entity)))
            {
                ChangeTactic(newTactic);
            }
        }
        
        private void GetBestScoreTactic(out AITactic bestTactic, out float bestScore)
        {
            bestScore = float.MinValue;
            bestTactic = null;
            int bestWeight = int.MinValue;

            foreach (var tactic in Tactics)
            {
                var score = tactic.Evaluate(entity);
                if ((score > Mathf.Epsilon && tactic.Weight > bestWeight) || 
                    (tactic.Weight == bestWeight && score > bestScore))
                {
                    bestScore = score;
                    bestTactic = tactic;
                    bestWeight = tactic.Weight;
                }
            }
        }
    }
}