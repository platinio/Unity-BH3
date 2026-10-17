using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    public sealed class HealthBar : MonoBehaviour
    {
        [SerializeField] private Health health;
        [SerializeField] private Transform fill;

        private Camera viewCamera;
        private Vector3 fullScale;

        private void Awake()
        {
            viewCamera = Camera.main;
            fullScale = fill.localScale;
        }

        private void OnEnable() => health.Changed += Redraw;

        private void OnDisable() => health.Changed -= Redraw;

        private void Start() => Redraw();

        private void LateUpdate() => transform.rotation = viewCamera.transform.rotation;

        private void Redraw()
        {
            Vector3 scale = fullScale;
            scale.x = fullScale.x * health.Fraction;
            fill.localScale = scale;
        }
    }
}
