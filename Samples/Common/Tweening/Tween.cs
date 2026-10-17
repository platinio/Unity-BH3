using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ArcaneOnyx.BH3.Samples
{
    public sealed class Tween
    {
        private readonly Object owner;
        private readonly float duration;
        private readonly Func<float, float> easing;
        private readonly Action<float> applyProgress;
        private float elapsedTime;

        public Tween(Object owner, float duration, Func<float, float> easing, Action<float> applyProgress)
        {
            this.owner = owner;
            this.duration = duration;
            this.easing = easing;
            this.applyProgress = applyProgress;
        }

        public bool IsPlaying { get; private set; } = true;

        public bool HasLostItsOwner => owner == null;

        public bool BelongsTo(Object candidate) => ReferenceEquals(owner, candidate);

        public void Advance(float deltaTime)
        {
            if (!IsPlaying) return;

            elapsedTime = Mathf.Min(elapsedTime + deltaTime, duration);
            applyProgress(easing(Progress));
            IsPlaying = elapsedTime < duration;
        }

        public void Kill() => IsPlaying = false;

        private float Progress => duration > 0f ? elapsedTime / duration : 1f;
    }
}
