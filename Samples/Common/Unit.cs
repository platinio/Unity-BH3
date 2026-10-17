using System.Collections.Generic;
using ArcaneOnyx.BehaviorTree;
using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    public sealed class Unit : MonoBehaviour
    {
        [SerializeField] private Transform body;
        [SerializeField] private Renderer[] renderers;

        private BodyPose authoredPose;
        private bool hasAuthoredPose;
        private float bodyScale = 1f;
        private Color[] restingColors;

        public Transform Body => body;
        public IReadOnlyList<Renderer> Renderers => renderers;
        public BodyPose RestingPose => AuthoredPose.Scaled(bodyScale);

        private BodyPose AuthoredPose
        {
            get
            {
                if (!hasAuthoredPose) CaptureAuthoredPose();
                return authoredPose;
            }
        }

        private Color[] RestingColors => restingColors ??= CaptureRestingColors();

        private void Awake()
        {
            if (!hasAuthoredPose) CaptureAuthoredPose();
        }

        public void Resize(float scale)
        {
            bodyScale = scale;
            RestoreRestingPose();
        }

        public void RestoreRestingPose() => RestingPose.ApplyTo(body);

        public void Dress(Material material)
        {
            foreach (Renderer renderer in renderers) renderer.sharedMaterial = material;
            restingColors = null;
        }

        public void RestoreRestingColors()
        {
            Color[] colors = RestingColors;
            for (int i = 0; i < renderers.Length; i++) renderers[i].material.color = colors[i];
        }

        private void CaptureAuthoredPose()
        {
            authoredPose = BodyPose.Of(body);
            hasAuthoredPose = true;
        }

        private Color[] CaptureRestingColors()
        {
            var colors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) colors[i] = renderers[i].material.color;
            return colors;
        }

        public static Unit Of(BTContext ctx) => Of(ctx.gameObject);

        public static Unit Of(GameObject agent) =>
            agent.TryGetComponent(out Unit unit)
                ? unit
                : throw new MissingComponentException($"{agent.name} has no Unit; add one and point it at the body and its renderers.");
    }
}
