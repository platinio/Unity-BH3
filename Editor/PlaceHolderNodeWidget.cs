using Unity.VisualScripting;

namespace Platinio.BehaviourTree
{
    [Widget(typeof(PlaceHolderNode))]
    public class PlaceHolderNodeWidget : BehaviourTreeNodeElementWidget
    {
        public PlaceHolderNodeWidget(BehaviourTreeCanvas canvas, BehaviourTreeNode element) : base(canvas, element)
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