using UnityEngine;
using UnityEngine.InputSystem;

namespace ArcaneOnyx.BH3.Samples
{
    public sealed class TrainingCommands : MonoBehaviour
    {
        [SerializeField] private SelectionBox selection;
        [SerializeField] private Treasury treasury;

        public Building SelectedBuilding =>
            selection.Selected.Count == 1 && selection.Selected[0].TryGetComponent(out Building building) ? building : null;

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Building building = SelectedBuilding;
            if (keyboard == null || building == null || !building.IsBuilt) return;

            if (keyboard.digit1Key.wasPressedThisFrame) Train(building, 0);
            if (keyboard.digit2Key.wasPressedThisFrame) Train(building, 1);
        }

        private void Train(Building building, int kindIndex)
        {
            UnitKind[] trains = building.Kind.Trains;
            if (kindIndex >= trains.Length || !building.TryGetComponent(out ProductionQueue queue)) return;

            UnitKind kind = trains[kindIndex];
            if (treasury.TrySpend(kind.WoodCost, kind.GoldCost)) queue.Enqueue(kind);
        }
    }
}
