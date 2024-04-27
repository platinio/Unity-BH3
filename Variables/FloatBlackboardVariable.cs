namespace Platinio.BehaviorTree
{
    [System.Serializable]
    public class FloatBlackboardVariable : BlackboardVariable<float>
    {
        public FloatBlackboardVariable() { }
        
        public FloatBlackboardVariable(float value)
        {
            this.value = value;
        }
    }
}

