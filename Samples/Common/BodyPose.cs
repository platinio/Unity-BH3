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

        public static BodyPose Between(BodyPose from, BodyPose to, float progress) => new(
            Vector3.Lerp(from.Position, to.Position, progress),
            Quaternion.Slerp(from.Rotation, to.Rotation, progress),
            Vector3.Lerp(from.Scale, to.Scale, progress));

        public BodyPose Scaled(float factor) => new(Position * factor, Rotation, Scale * factor);

        public void ApplyTo(Transform body)
        {
            body.localPosition = Position;
            body.localRotation = Rotation;
            body.localScale = Scale;
        }
    }
}
