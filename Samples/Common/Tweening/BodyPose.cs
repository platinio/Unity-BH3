using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    public sealed class BodyPose : MonoBehaviour
    {
        public Vector3 RestingPosition { get; private set; }
        public Quaternion RestingRotation { get; private set; }
        public Vector3 RestingScale { get; private set; }

        private void Awake() => Capture();

        public void Capture()
        {
            RestingPosition = transform.localPosition;
            RestingRotation = transform.localRotation;
            RestingScale = transform.localScale;
        }

        public void RestoreInstantly()
        {
            transform.localPosition = RestingPosition;
            transform.localRotation = RestingRotation;
            transform.localScale = RestingScale;
        }

        public static BodyPose Of(Transform body) =>
            body.TryGetComponent(out BodyPose pose) ? pose : body.gameObject.AddComponent<BodyPose>();
    }
}
