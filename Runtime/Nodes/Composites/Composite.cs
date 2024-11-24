namespace ArcaneOnyx.BehaviorTree
{
    public class Composite : ContainerNode
    {
        protected int currentExecutingChildIndex = 0;
       
        public override void OnAwake()
        {
            currentExecutingChildIndex = 0;
        }
    }
}