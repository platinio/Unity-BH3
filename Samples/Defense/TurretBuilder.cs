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
            HandleClick();
        }

        private void HandleSelectionKeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.digit1Key.wasPressedThisFrame) selected = 0;
            if (keyboard.digit2Key.wasPressedThisFrame) selected = Mathf.Min(1, kinds.Length - 1);
            if (keyboard.digit3Key.wasPressedThisFrame) selected = Mathf.Min(2, kinds.Length - 1);
        }

        private void HandleClick()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

            Ray clickRay = viewCamera.ScreenPointToRay(mouse.position.ReadValue());
            if (!Physics.Raycast(clickRay, out RaycastHit hit)) return;

            BuildPad pad = hit.collider.GetComponentInParent<BuildPad>();
            if (pad != null) pad.TryBuild(Selected, turretPrefab, game);
        }
    }
}
