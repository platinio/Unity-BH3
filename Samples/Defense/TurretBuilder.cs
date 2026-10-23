using UnityEngine;
using UnityEngine.InputSystem;

namespace ArcaneOnyx.BH3.Samples
{
    public sealed class TurretBuilder : MonoBehaviour
    {
        [SerializeField] private DefenseGame game;
        [SerializeField] private Turret turretPrefab;
        [SerializeField] private TurretKind[] kinds;

        private Camera viewCamera;
        private int selected;

        public TurretKind Selected => kinds[selected];
        public TurretKind[] Kinds => kinds;

        private void Awake() => viewCamera = Camera.main;

        private void Update()
        {
            HandleSelectionKeys();
            HandlePadClick();
        }

        public void Select(TurretKind kind)
        {
            int index = System.Array.IndexOf(kinds, kind);
            if (index >= 0) selected = index;
        }

        private void HandleSelectionKeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.digit1Key.wasPressedThisFrame) SelectAt(0);
            if (keyboard.digit2Key.wasPressedThisFrame) SelectAt(1);
            if (keyboard.digit3Key.wasPressedThisFrame) SelectAt(2);
        }

        private void SelectAt(int index) => selected = Mathf.Min(index, kinds.Length - 1);

        private void HandlePadClick()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame || HudCursor.IsOverTheHud) return;

            Ray clickRay = viewCamera.ScreenPointToRay(mouse.position.ReadValue());
            if (!Physics.Raycast(clickRay, out RaycastHit hit)) return;

            BuildPad pad = hit.collider.GetComponentInParent<BuildPad>();
            if (pad != null) pad.TryBuild(Selected, turretPrefab, game);
        }
    }
}
