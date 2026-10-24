using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    public sealed class DestinationMarker : MonoBehaviour
    {
        [SerializeField] private float popDuration = 0.25f;

        private Vector3 restingScale;

        private void Awake() => restingScale = transform.localScale;

        public void ShowAt(Vector3 point)
        {
            transform.position = point;
            transform.localScale = Vector3.zero;
            transform.ScaleTo(restingScale, popDuration, Easing.OutBack);
        }
    }
}
