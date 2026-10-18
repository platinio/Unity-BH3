using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace ArcaneOnyx.BH3.Samples
{
    public sealed class RtsHud : MonoBehaviour
    {
        [SerializeField] private Treasury treasury;
        [SerializeField] private SelectionBox selection;
        [SerializeField] private BuildingPlacer placer;
        [SerializeField] private TrainingCommands training;
        [SerializeField] private RtsGame game;
        [SerializeField] private Text resources;
        [SerializeField] private Text hint;
        [SerializeField] private Text banner;

        private readonly StringBuilder line = new();
        private readonly Dictionary<string, int> selectedByKind = new();

        private void Update()
        {
            resources.text = ResourcesAndSelection();
            hint.text = Hint();
            banner.text = game.IsWon ? "Victory" : "";
        }

        private string ResourcesAndSelection()
        {
            line.Clear();
            line.Append("Wood ").Append(treasury.Wood).Append("   Gold ").Append(treasury.Gold);
            string selected = SelectionSummary();
            if (selected.Length > 0) line.Append("   |   ").Append(selected);
            return line.ToString();
        }

        private string SelectionSummary()
        {
            Building building = training.SelectedBuilding;
            if (building != null) return BuildingSummary(building);

            selectedByKind.Clear();
            foreach (Selectable selectable in selection.Selected)
            {
                if (!selectable.TryGetComponent(out RtsUnit unit) || unit.Kind == null) continue;
                selectedByKind.TryGetValue(unit.Kind.Id, out int count);
                selectedByKind[unit.Kind.Id] = count + 1;
            }

            var parts = new List<string>();
            foreach (KeyValuePair<string, int> kind in selectedByKind) parts.Add(kind.Value + " " + Plural(kind.Key, kind.Value));
            return string.Join(", ", parts);
        }

        private static string BuildingSummary(Building building)
        {
            string summary = building.Kind.Id;
            if (!building.IsBuilt) return summary + ", " + Mathf.RoundToInt(building.Progress01 * 100f) + "% built";
            if (building.TryGetComponent(out ProductionQueue queue) && queue.Count > 0) summary += ", queue " + queue.Count;
            return summary;
        }

        private static string Plural(string kind, int count) => count == 1 ? kind : kind + "s";

        private string Hint()
        {
            if (placer.Placing != null) return "Click to place the " + placer.Placing.Id + ", right-click or Escape to cancel";

            Building building = training.SelectedBuilding;
            if (building != null) return building.IsBuilt ? TrainingHint(building) : "Right-click it with Workers selected to help build";
            if (selection.Selected.Count == 0) return "Drag to select units, click a building to select it";

            return WorkersAreSelected() ? PlacementHint() + "   Right-click: move, gather a tree or rock, build a site" : "Right-click: move, or attack an enemy";
        }

        private bool WorkersAreSelected()
        {
            foreach (Selectable selectable in selection.Selected)
            {
                if (selectable.TryGetComponent(out Carrier _)) return true;
            }

            return false;
        }

        private string PlacementHint()
        {
            line.Clear();
            for (int i = 0; i < placer.Kinds.Count; i++)
            {
                BuildingKind kind = placer.Kinds[i];
                line.Append('[').Append(i + 1).Append("] ").Append(kind.Id).Append(" (").Append(Cost(kind.WoodCost, kind.GoldCost)).Append(")  ");
            }

            return line.ToString();
        }

        private string TrainingHint(Building building)
        {
            line.Clear();
            UnitKind[] trains = building.Kind.Trains;
            for (int i = 0; i < trains.Length; i++)
            {
                line.Append('[').Append(i + 1).Append("] ").Append(trains[i].Id).Append(" (").Append(Cost(trains[i].WoodCost, trains[i].GoldCost)).Append(")  ");
            }

            return trains.Length > 0 ? line.ToString() : "This building trains nothing";
        }

        private static string Cost(int wood, int gold)
        {
            if (wood > 0 && gold > 0) return wood + " wood, " + gold + " gold";
            return wood > 0 ? wood + " wood" : gold + " gold";
        }
    }
}
