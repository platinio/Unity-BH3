using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace ArcaneOnyx.BH3.Samples
{
    public static class TweenExtensions
    {
        public static Tween ScaleTo(this Transform transform, Vector3 targetScale, float duration,
            Func<float, float> easing = null)
        {
            Vector3 startScale = transform.localScale;

            return Play(transform, duration, easing,
                progress => transform.localScale = Vector3.LerpUnclamped(startScale, targetScale, progress));
        }

        public static Tween PunchScale(this Transform transform, float strength, float duration)
        {
            Vector3 originalScale = transform.localScale;
            Vector3 punchedScale = originalScale * (1f + strength);

            return Play(transform, duration, Easing.Linear,
                progress => transform.localScale = Vector3.Lerp(originalScale, punchedScale, ThereAndBack(progress)));
        }

        public static Tween MoveTo(this Transform transform, Vector3 targetPosition, float duration,
            Func<float, float> easing = null)
        {
            Vector3 startPosition = transform.position;

            return Play(transform, duration, easing,
                progress => transform.position = Vector3.LerpUnclamped(startPosition, targetPosition, progress));
        }

        public static Tween Hop(this Transform transform, float height, float duration)
        {
            Vector3 groundedPosition = transform.localPosition;
            Vector3 apexPosition = groundedPosition + Vector3.up * height;

            return Play(transform, duration, Easing.Linear,
                progress => transform.localPosition = Vector3.Lerp(groundedPosition, apexPosition, ThereAndBack(progress)));
        }

        public static Tween LeanAbout(this Unit unit, Vector3 localAxis, float fromAngle, float toAngle,
            float duration, Func<float, float> easing)
        {
            Transform body = unit.Body;
            BodyPose resting = unit.RestingPose;

            return Play(unit, duration, easing, progress =>
            {
                Quaternion lean = Quaternion.AngleAxis(Mathf.Lerp(fromAngle, toAngle, progress), localAxis);
                body.localRotation = lean * resting.Rotation;
                body.localPosition = lean * resting.Position;
            });
        }

        public static Tween ReturnToRestingPose(this Unit unit, float duration)
        {
            Transform body = unit.Body;
            BodyPose resting = unit.RestingPose;
            Vector3 startPosition = body.localPosition;
            Quaternion startRotation = body.localRotation;

            return Play(unit, duration, Easing.OutQuad, progress =>
            {
                body.localPosition = Vector3.Lerp(startPosition, resting.Position, progress);
                body.localRotation = Quaternion.Slerp(startRotation, resting.Rotation, progress);
            });
        }

        public static Tween ShakePosition(this Transform transform, float strength, float duration)
        {
            Vector3 originalPosition = transform.localPosition;

            return Play(transform, duration, Easing.Linear, progress =>
            {
                float fadingStrength = strength * (1f - progress);
                transform.localPosition = originalPosition + Random.insideUnitSphere * fadingStrength;
            });
        }

        public static Tween ColorTo(this Renderer renderer, Color targetColor, float duration,
            Func<float, float> easing = null)
        {
            Color startColor = renderer.material.color;

            return Play(renderer, duration, easing,
                progress => renderer.material.color = Color.LerpUnclamped(startColor, targetColor, progress));
        }

        public static Tween FlashColor(this Unit unit, Color flashColor, float duration)
        {
            IReadOnlyList<Renderer> renderers = unit.Renderers;
            var originalColors = new Color[renderers.Count];
            for (int i = 0; i < renderers.Count; i++) originalColors[i] = renderers[i].material.color;

            return Play(unit, duration, Easing.Linear, progress =>
            {
                float blend = ThereAndBack(progress);
                for (int i = 0; i < renderers.Count; i++)
                {
                    renderers[i].material.color = Color.Lerp(originalColors[i], flashColor, blend);
                }
            });
        }

        private static Tween Play(Object owner, float duration, Func<float, float> easing, Action<float> applyProgress)
        {
            var tween = new Tween(owner, duration, easing ?? Easing.OutQuad, applyProgress);
            TweenRunner.Add(tween);
            return tween;
        }

        private static float ThereAndBack(float progress)
        {
            float distanceFromHalfway = Mathf.Abs(2f * progress - 1f);
            return Easing.OutQuad(1f - distanceFromHalfway);
        }
    }
}
