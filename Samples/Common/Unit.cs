using System.Collections.Generic;
using ArcaneOnyx.BehaviorTree;
using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    public sealed class Unit : MonoBehaviour
    {
        [SerializeField] private Transform body;
        [SerializeField] private Renderer[] renderers;
        [SerializeField] private Transform[] parts;
        [SerializeField] private Transform weaponSocket;

        private BodyPose authoredBodyPose;
        private BodyPose[] authoredPartPoses;
        private bool hasAuthoredPoses;
        private float bodyScale = 1f;
        private Color[] restingColors;

        public Transform Body => body;
        public IReadOnlyList<Renderer> Renderers => renderers;
        public IReadOnlyList<Transform> Parts => parts;
        public Transform WeaponSocket => weaponSocket;
        public BodyPose RestingPose => AuthoredBodyPose.Scaled(bodyScale);

        private BodyPose AuthoredBodyPose
        {
            get
            {
                if (!hasAuthoredPoses) CaptureAuthoredPoses();
                return authoredBodyPose;
            }
        }

        private Color[] RestingColors => restingColors ??= CaptureRestingColors();

        private void Awake()
        {
            if (!hasAuthoredPoses) CaptureAuthoredPoses();
        }

        public Transform Part(string partName)
        {
            foreach (Transform part in parts)
            {
                if (part.name == partName) return part;
            }

            throw new MissingReferenceException($"{name} has no part named '{partName}'; its Unit lists: {PartNames()}.");
        }

        public BodyPose RestingPoseOf(Transform part)
        {
            if (!hasAuthoredPoses) CaptureAuthoredPoses();
            return authoredPartPoses[System.Array.IndexOf(parts, part)];
        }

        public void Resize(float scale)
        {
            bodyScale = scale;
            RestoreRestingPose();
        }

        public void RestoreRestingPose()
        {
            RestingPose.ApplyTo(body);
            for (int i = 0; i < parts.Length; i++) RestingPoseOf(parts[i]).ApplyTo(parts[i]);
        }

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

        private void CaptureAuthoredPoses()
        {
            authoredBodyPose = BodyPose.Of(body);
            authoredPartPoses = new BodyPose[parts.Length];
            for (int i = 0; i < parts.Length; i++) authoredPartPoses[i] = BodyPose.Of(parts[i]);
            hasAuthoredPoses = true;
        }

        private Color[] CaptureRestingColors()
        {
            var colors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) colors[i] = renderers[i].material.color;
            return colors;
        }

        private string PartNames()
        {
            var names = new string[parts.Length];
            for (int i = 0; i < parts.Length; i++) names[i] = parts[i].name;
            return string.Join(", ", names);
        }

        public static Unit Of(BTContext ctx) => Of(ctx.gameObject);

        public static Unit Of(GameObject agent) =>
            agent.TryGetComponent(out Unit unit)
                ? unit
                : throw new MissingComponentException($"{agent.name} has no Unit; add one and point it at the body, its renderers and its parts.");
    }
}
