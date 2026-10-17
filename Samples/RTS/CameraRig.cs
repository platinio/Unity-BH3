using UnityEngine;
using UnityEngine.InputSystem;

namespace ArcaneOnyx.BH3.Samples
{
    public sealed class CameraRig : MonoBehaviour
    {
        [SerializeField] private float panSpeed = 20f;
        [SerializeField] private float edgeWidthPixels = 12f;
        [SerializeField] private Rect mapBounds = new(-30f, -30f, 60f, 60f);

        private void Update()
        {
            Vector2 pan = KeyboardPan() + EdgePan();
            if (pan == Vector2.zero) return;

            Vector3 moved = transform.position + new Vector3(pan.x, 0f, pan.y).normalized * (panSpeed * Time.deltaTime);
            moved.x = Mathf.Clamp(moved.x, mapBounds.xMin, mapBounds.xMax);
            moved.z = Mathf.Clamp(moved.z, mapBounds.yMin, mapBounds.yMax);
            transform.position = moved;
        }

        private static Vector2 KeyboardPan()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return Vector2.zero;

            Vector2 pan = Vector2.zero;
            if (keyboard.wKey.isPressed) pan.y += 1f;
            if (keyboard.sKey.isPressed) pan.y -= 1f;
            if (keyboard.dKey.isPressed) pan.x += 1f;
            if (keyboard.aKey.isPressed) pan.x -= 1f;
            return pan;
        }

        private Vector2 EdgePan()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || !Application.isFocused) return Vector2.zero;

            Vector2 cursor = mouse.position.ReadValue();
            Vector2 pan = Vector2.zero;
            if (cursor.x <= edgeWidthPixels) pan.x -= 1f;
            if (cursor.x >= Screen.width - edgeWidthPixels) pan.x += 1f;
            if (cursor.y <= edgeWidthPixels) pan.y -= 1f;
            if (cursor.y >= Screen.height - edgeWidthPixels) pan.y += 1f;
            return pan;
        }
    }
}
