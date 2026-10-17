using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    [RequireComponent(typeof(Selectable))]
    public sealed class SelectionRing : MonoBehaviour
    {
        [SerializeField] private GameObject ring;

        private Selectable selectable;

        private void Awake()
        {
            selectable = GetComponent<Selectable>();
            ring.SetActive(selectable.IsSelected);
        }

        private void OnEnable() => selectable.SelectionChanged += ring.SetActive;

        private void OnDisable() => selectable.SelectionChanged -= ring.SetActive;
    }
}
