using Platinio.GraphCore;

namespace Platinio.BehaviourTree
{
    [GraphCreateMenu("Composite/Create Score Selector")]
    public class ScoreSelector : Composite
    {
        protected override string NodeIconPath => "NodeIcons/Selector";
        public override string NodeName => "Score Selector";

        private int m_selectedChildIndex = 0;
        
        public override void OnEnter()
        {
            float maxScore = float.MinValue;
            int selectedVariableScoreIndex = -1;
            
            for (int n = 0; n < GetChildren().Count; n++)
            {
                var child = GetChildren()[n];
                float score = child.CalculateScore(out int bestVariableScoreIndex);

                if (score > maxScore)
                {
                    selectedVariableScoreIndex = bestVariableScoreIndex;
                    m_selectedChildIndex = n;
                    maxScore = score;
                }
            }
            
            OnTraverseChildren(selectedVariableScoreIndex);
        }

        private void OnTraverseChildren(int bestScoreIndex)
        {
            if (GetChildren().Count == 0) return;
            GetChildren()[m_selectedChildIndex].OnTraverse(bestScoreIndex);
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