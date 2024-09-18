using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree
{
    [Widget(typeof(PlaceHolderNode))]
    public class PlaceHolderNodeWidget : BehaviorTreeNodeElementWidget
    {
        public PlaceHolderNodeWidget(BehaviorTreeCanvas canvas, BehaviorTreeNode element) : base(canvas, element)
        {
        }

        public override void HandleInput()
        {
            base.HandleInput();

            var owner = (element as PlaceHolderNode)?.Owner;
            if (!graph.elements.Contains(owner)) graph.elements.Remove(element);
        }
    }
}