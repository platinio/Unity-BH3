using UnityEngine;
using UnityEngine.InputSystem;

namespace ArcaneOnyx.BH3.Samples
{
    public sealed class TurretBuilder : MonoBehaviour
    {
        [SerializeField] private DefenseGame game;
        [SerializeField] private Turret turretPrefab;
        [SerializeField] private TurretKind[] kinds;
        [SerializeField] private Material freeGhostMaterial;
        [SerializeField] private Material blockedGhostMaterial;

        private Camera viewCamera;
        private TurretKind placing;
        private TurretGhost ghost;

        public TurretKind Placing => placing;
        public TurretKind[] Kinds => kinds;

        private void Awake() => viewCamera = Camera.main;

        public void BeginPlacing(TurretKind kind)
        {
            if (!game.CanAfford(kind.Cost)) return;

            if (placing == null) ghost = new TurretGhost(turretPrefab, freeGhostMaterial, blockedGhostMaterial);
            placing = kind;
        }

        private void Update()
        {
            HandleShortcutKeys();
            if (placing == null) return;

            FollowCursor();
            HandleCancel();
            HandleBuild();
        }

        private void HandleShortcutKeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.digit1Key.wasPressedThisFrame) BeginPlacingAt(0);
            if (keyboard.digit2Key.wasPressedThisFrame) BeginPlacingAt(1);
            if (keyboard.digit3Key.wasPressedThisFrame) BeginPlacingAt(2);
        }

        private void BeginPlacingAt(int index)
        {
            if (index < kinds.Length) BeginPlacing(kinds[index]);
        }

        private void FollowCursor()
        {
            Ray cursorRay = viewCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (!Physics.Raycast(cursorRay, out RaycastHit hit)) return;

            BuildPad pad = hit.collider.GetComponentInParent<BuildPad>();
            if (pad != null && pad.IsFree) ghost.ShowOnPad(pad);
            else ghost.ShowBlockedAt(hit.point);
        }

        private void HandleCancel()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            bool cancelled = (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) || (mouse != null && mouse.rightButton.wasPressedThisFrame);
            if (cancelled) StopPlacing();
        }

        private void HandleBuild()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame || HudCursor.IsOverTheHud || !ghost.IsOverFreePad) return;

            ghost.Pad.TryBuild(placing, turretPrefab, game);
            StopPlacing();
        }

        private void StopPlacing()
        {
            ghost.Hide();
            ghost = null;
            placing = null;
        }
    }
}
