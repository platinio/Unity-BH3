using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    public sealed class ProductionBar : MonoBehaviour
    {
        private const float ClearanceAboveTheRoof = 0.8f;

        [SerializeField] private ProductionQueue queue;
        [SerializeField] private Transform fill;

        private Camera viewCamera;
        private Vector3 fullScale;

        private void Awake()
        {
            viewCamera = Camera.main;
            fullScale = fill.localScale;
            FloatAboveTheRoof();
        }

        private void LateUpdate()
        {
            bool training = queue.Count > 0;
            fill.gameObject.SetActive(training);
            if (!training) return;

            transform.rotation = viewCamera.transform.rotation;
            Vector3 scale = fullScale;
            scale.x = fullScale.x * queue.Progress01;
            fill.localScale = scale;
        }

        private void FloatAboveTheRoof()
        {
            Unit unit = Unit.Of(transform.parent.gameObject);
            var roof = new Bounds(transform.parent.position, Vector3.zero);
            foreach (Renderer renderer in unit.Renderers) roof.Encapsulate(renderer.bounds);

            Vector3 above = transform.position;
            above.y = roof.max.y + ClearanceAboveTheRoof;
            transform.position = above;
        }
    }
}
