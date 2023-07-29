using Platinio.Considerations;
using Platinio.GraphCore;

namespace Platinio.BehaviorTree
{
    [GraphCreateMenu("Composite/Create Score Selector")]
    public class ScoreSelector : Composite
    {
        protected override string NodeIconPath => "NodeIcons/Selector";
        public override string NodeName => "Score Selector";

        private int m_selectedChildIndex = 0;
        
        public override void OnEnter()
        {
            DecisionScoreResult maxScoreResult = default;

            for (int n = 0; n < GetChildren().Count; n++)
            {
                var child = GetChildren()[n];
                var scoreResult = child.CalculateScore();

                if (scoreResult.Value > maxScoreResult.Value)
                {
                    maxScoreResult = scoreResult;
                    m_selectedChildIndex = n;
                }
            }
            
            Machine.Variables.Set("DecisionScoreResult", maxScoreResult);
            GetChildren()[m_selectedChildIndex].OnNodeEnter();
            OnTraverseChildren(maxScoreResult);
        }

        private void OnTraverseChildren(DecisionScoreResult scoreResult)
        {
            if (GetChildren().Count == 0) return;
            GetChildren()[m_selectedChildIndex].OnTraverse(scoreResult);
        }


        public override ExecutionStatus OnUpdate()
        {
            if (GetChildren().Count == 0) return ExecutionStatus.Success;

            var task = GetChildren()[m_selectedChildIndex];
            var result = task.OnUpdate();

            if (result != ExecutionStatus.Running)
            {
                task.OnNodeExit();
                return result;
            }

            return result;
        }
    }
}