using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ArcaneOnyx.BH3.Samples
{
    public sealed class BuildingPlacer : MonoBehaviour
    {
        [SerializeField] private SelectionBox selection;
        [SerializeField] private PlayerCommands commands;
        [SerializeField] private Treasury treasury;
        [SerializeField] private Transform buildingsParent;
        [SerializeField] private BuildingKind[] kinds;
        [SerializeField] private Material freeGhostMaterial;
        [SerializeField] private Material blockedGhostMaterial;

        private Camera viewCamera;
        private BuildingKind placing;
        private BuildingGhost ghost;

        public IReadOnlyList<BuildingKind> Kinds => kinds;
        public BuildingKind Placing => placing;

        private void Awake() => viewCamera = Camera.main;

        public void BeginPlacing(BuildingKind kind)
        {
            if (placing != null || !treasury.CanAfford(kind.WoodCost, kind.GoldCost)) return;

            placing = kind;
            ghost = new BuildingGhost(kind, freeGhostMaterial, blockedGhostMaterial);
            selection.enabled = false;
            commands.enabled = false;
        }

        private void Update()
        {
            if (placing == null) return;

            FollowCursor();
            HandleCancel();
            HandlePlace();
        }

        private void FollowCursor()
        {
            Ray cursorRay = viewCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(cursorRay, out RaycastHit hit)) ghost.ShowAt(hit.point);
        }

        private void HandleCancel()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            bool cancelled = (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) || (mouse != null && mouse.rightButton.wasPressedThisFrame);
            if (cancelled) StopPlacing();
        }

        private void HandlePlace()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame || HudCursor.IsOverTheHud || !ghost.SpotIsFree) return;

            if (treasury.TrySpend(placing.WoodCost, placing.GoldCost)) RaiseSite(placing, ghost.Spot);
            StopPlacing();
        }

        private void RaiseSite(BuildingKind kind, Vector3 spot)
        {
            Building site = Instantiate(kind.Prefab, spot, Quaternion.identity, buildingsParent);
            site.name = kind.Id;
            commands.OrderBuild(site.gameObject);
        }

        private void StopPlacing()
        {
            ghost.Hide();
            ghost = null;
            placing = null;
            selection.enabled = true;
            commands.enabled = true;
        }
    }
}
