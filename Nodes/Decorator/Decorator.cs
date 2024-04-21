namespace Platinio.BehaviorTree
{
    public class Decorator : ContainerNode
    {
        public override int MaxChildrenLimit => 1;
        
        public override void OnEnter()
        {
            if (GetChildren().Count == 0) return;
            
            var task = GetChildren()[0];
            task.OnNodeEnter();
        }
    }
}