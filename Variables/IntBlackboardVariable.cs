namespace Platinio.BehaviorTree
{
    public class IntBlackboardVariable : BlackboardVariable<int>
    {
        public IntBlackboardVariable() { }

        public IntBlackboardVariable(int value)
        {
            this.value = value;
        }
    }
}