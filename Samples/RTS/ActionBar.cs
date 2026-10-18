using System.Collections.Generic;
using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    public sealed class ActionBar : MonoBehaviour
    {
        [SerializeField] private SelectionBox selection;
        [SerializeField] private Treasury treasury;
        [SerializeField] private BuildingPlacer placer;
        [SerializeField] private TrainingCommands training;
        [SerializeField] private ActionButton buttonPrefab;
        [SerializeField] private RectTransform row;

        private readonly List<ActionButton> shown = new();

        private void OnEnable() => selection.Changed += Rebuild;

        private void OnDisable() => selection.Changed -= Rebuild;

        private void Start() => Rebuild();

        private void Update()
        {
            foreach (ActionButton button in shown) button.ShowAffordable(button.IsAffordableFrom(treasury));
        }

        private void Rebuild()
        {
            Clear();

            Building building = training.SelectedBuilding;
            if (building != null) ShowWhatItTrains(building);
            else if (WorkersAreSelected()) ShowWhatTheyBuild();
        }

        private void ShowWhatTheyBuild()
        {
            foreach (BuildingKind kind in placer.Kinds)
            {
                BuildingKind wanted = kind;
                Add(kind.Id, kind.WoodCost, kind.GoldCost, () => placer.BeginPlacing(wanted));
            }
        }

        private void ShowWhatItTrains(Building building)
        {
            if (!building.IsBuilt) return;

            foreach (UnitKind kind in building.Kind.Trains)
            {
                UnitKind wanted = kind;
                Add(kind.Id, kind.WoodCost, kind.GoldCost, () => training.Train(building, wanted));
            }
        }

        private void Add(string what, int wood, int gold, System.Action onClick)
        {
            ActionButton button = Instantiate(buttonPrefab, row);
            button.Show(what, wood, gold, onClick);
            shown.Add(button);
        }

        private void Clear()
        {
            foreach (ActionButton button in shown) Destroy(button.gameObject);
            shown.Clear();
        }

        private bool WorkersAreSelected()
        {
            foreach (Selectable selected in selection.Selected)
            {
                if (selected.TryGetComponent(out Carrier _)) return true;
            }

            return false;
        }
    }
}
