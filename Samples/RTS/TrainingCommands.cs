using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    public sealed class TrainingCommands : MonoBehaviour
    {
        [SerializeField] private SelectionBox selection;
        [SerializeField] private Treasury treasury;

        public Building SelectedBuilding =>
            selection.Selected.Count == 1 && selection.Selected[0].TryGetComponent(out Building building) ? building : null;

        public void Train(Building building, UnitKind kind)
        {
            if (!building.IsBuilt || !building.TryGetComponent(out ProductionQueue queue)) return;
            if (treasury.TrySpend(kind.WoodCost, kind.GoldCost)) queue.Enqueue(kind);
        }
    }
}
