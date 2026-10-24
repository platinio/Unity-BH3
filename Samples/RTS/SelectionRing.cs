using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    [RequireComponent(typeof(Selectable))]
    public sealed class SelectionRing : MonoBehaviour
    {
        private const float RoomAroundTheFootprint = 1.15f;

        [SerializeField] private GameObject ring;

        private Selectable selectable;

        private void Awake()
        {
            selectable = GetComponent<Selectable>();
            if (TryGetComponent(out Building building)) WidenTo(building.Kind.FootprintRadius);
            ring.SetActive(selectable.IsSelected);
        }

        private void OnEnable() => selectable.SelectionChanged += ring.SetActive;

        private void OnDisable() => selectable.SelectionChanged -= ring.SetActive;

        private void WidenTo(float footprintRadius)
        {
            float width = footprintRadius * 2f * RoomAroundTheFootprint;
            ring.transform.localScale = new Vector3(width, ring.transform.localScale.y, width);
        }
    }
}
