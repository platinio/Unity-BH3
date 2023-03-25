namespace Platinio.BehaviourTree
{
    public class Decorator : ContainerNode
    {
        public override int MaxChildren => 1;
        
        public override void OnEnter()
        {
            if (GetChildren().Count == 0) return;
            
            var task = GetChildren()[0];
            task.OnNodeEnter();
        }
    }
}