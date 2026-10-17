using System.Collections.Generic;
using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    public sealed class TweenRunner : MonoBehaviour
    {
        private static TweenRunner instance;

        private readonly List<Tween> tweens = new();

        public static void Add(Tween tween) => Instance.tweens.Add(tween);

        public static void KillAll(Object owner)
        {
            foreach (Tween tween in Instance.tweens)
            {
                if (tween.BelongsTo(owner)) tween.Kill();
            }
        }

        public static bool IsAnimating(Object owner) =>
            Instance.tweens.Exists(tween => tween.IsPlaying && tween.BelongsTo(owner));

        public static void Advance(float deltaTime) => Instance.AdvanceEveryTween(deltaTime);

        private void Update() => AdvanceEveryTween(Time.deltaTime);

        private void AdvanceEveryTween(float deltaTime)
        {
            for (int index = 0; index < tweens.Count; index++)
            {
                Tween tween = tweens[index];

                if (tween.HasLostItsOwner) tween.Kill();
                else tween.Advance(deltaTime);
            }

            tweens.RemoveAll(tween => !tween.IsPlaying);
        }

        private static TweenRunner Instance => instance != null ? instance : instance = CreateHiddenRunner();

        private static TweenRunner CreateHiddenRunner()
        {
            var host = new GameObject(nameof(TweenRunner));

            // HideAndDontSave keeps an edit-mode runner out of the saved scene, but an object carrying it
            // outlives play mode and leaks, so in play mode the runner hides and rides DontDestroyOnLoad.
            if (Application.isPlaying)
            {
                host.hideFlags = HideFlags.HideInHierarchy;
                DontDestroyOnLoad(host);
            }
            else
            {
                host.hideFlags = HideFlags.HideAndDontSave;
            }

            return host.AddComponent<TweenRunner>();
        }
    }
}
