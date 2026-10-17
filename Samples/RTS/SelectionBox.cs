using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ArcaneOnyx.BH3.Samples
{
    public sealed class SelectionBox : MonoBehaviour
    {
        private const float DragThresholdPixels = 6f;

        [SerializeField] private RectTransform box;

        private Camera viewCamera;
        private Canvas canvas;
        private RectTransform canvasRect;
        private Vector2 pressPosition;
        private bool isPressed;
        private bool isDragging;
        private readonly List<Selectable> selected = new();

        public IReadOnlyList<Selectable> Selected => selected;

        private void Awake()
        {
            viewCamera = Camera.main;
            canvas = box.GetComponentInParent<Canvas>();
            canvasRect = canvas.GetComponent<RectTransform>();
            box.anchorMin = Vector2.zero;
            box.anchorMax = Vector2.zero;
            box.pivot = Vector2.zero;
            box.gameObject.SetActive(false);
        }

        private void Update()
        {
            ForgetTheGone();

            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            Vector2 cursor = mouse.position.ReadValue();
            if (mouse.leftButton.wasPressedThisFrame) BeginPress(cursor);
            if (isPressed && mouse.leftButton.isPressed) ContinuePress(cursor);
            if (isPressed && mouse.leftButton.wasReleasedThisFrame) Release(cursor);
        }

        private void BeginPress(Vector2 cursor)
        {
            isPressed = true;
            isDragging = false;
            pressPosition = cursor;
        }

        private void ContinuePress(Vector2 cursor)
        {
            if (!isDragging && Vector2.Distance(pressPosition, cursor) < DragThresholdPixels) return;

            isDragging = true;
            DrawBox(ScreenRect(pressPosition, cursor));
        }

        private void Release(Vector2 cursor)
        {
            isPressed = false;
            box.gameObject.SetActive(false);

            if (isDragging) SelectInside(ScreenRect(pressPosition, cursor));
            else SelectUnder(cursor);
        }

        private static Rect ScreenRect(Vector2 corner, Vector2 oppositeCorner)
        {
            Vector2 min = Vector2.Min(corner, oppositeCorner);
            Vector2 max = Vector2.Max(corner, oppositeCorner);
            return new Rect(min, max - min);
        }

        private void DrawBox(Rect screenRect)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenRect.min, canvas.worldCamera, out Vector2 localMin);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenRect.max, canvas.worldCamera, out Vector2 localMax);

            box.gameObject.SetActive(true);
            box.anchoredPosition = localMin + canvasRect.rect.size * canvasRect.pivot;
            box.sizeDelta = localMax - localMin;
        }

        private void SelectInside(Rect screenRect)
        {
            Clear();
            foreach (Selectable candidate in Selectable.All)
            {
                if (candidate.SelectsAlone) continue;
                if (screenRect.Contains(viewCamera.WorldToScreenPoint(candidate.transform.position))) Add(candidate);
            }
        }

        private void SelectUnder(Vector2 cursor)
        {
            Clear();
            Ray clickRay = viewCamera.ScreenPointToRay(cursor);
            if (!Physics.Raycast(clickRay, out RaycastHit hit)) return;

            Selectable clicked = hit.collider.GetComponentInParent<Selectable>();
            if (clicked != null) Add(clicked);
        }

        private void Add(Selectable selectable)
        {
            selected.Add(selectable);
            selectable.Select();
        }

        private void Clear()
        {
            foreach (Selectable selectable in selected) selectable.Deselect();
            selected.Clear();
        }

        private void ForgetTheGone() => selected.RemoveAll(selectable => selectable == null || !selectable.isActiveAndEnabled);
    }
}
