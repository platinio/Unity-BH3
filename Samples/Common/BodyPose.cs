using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    public readonly struct BodyPose
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly Vector3 Scale;

        public BodyPose(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            Position = position;
            Rotation = rotation;
            Scale = scale;
        }

        public static BodyPose Of(Transform body) => new(body.localPosition, body.localRotation, body.localScale);

        public BodyPose Scaled(float factor) => new(Position * factor, Rotation, Scale * factor);

        public void ApplyTo(Transform body)
        {
            body.localPosition = Position;
            body.localRotation = Rotation;
            body.localScale = Scale;
        }
    }
}
