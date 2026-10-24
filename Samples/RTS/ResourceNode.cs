using System.Collections.Generic;
using ArcaneOnyx.BehaviorTree;
using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    public sealed class ResourceNode : MonoBehaviour
    {
        private const float ShrinkSeconds = 0.3f;
        private const float EmptyScale = 0.3f;

        public static readonly List<ResourceNode> All = new();

        [SerializeField] private Resource kind;
        [SerializeField] private int startingAmount = 60;
        [SerializeField] private int amountPerSwing = 5;
        [SerializeField] private Transform model;

        private int remaining;
        private Vector3 fullScale;
        private AgentVariableWriter facts;

        public Resource Kind => kind;
        public int Remaining => remaining;
        public bool IsEmpty => remaining <= 0;

        private void Awake()
        {
            remaining = startingAmount;
            fullScale = model.localScale;
            facts = AgentVariableWriter.On(gameObject);
            Publish();
        }

        private void OnEnable() => All.Add(this);

        private void OnDisable() => All.Remove(this);

        public int Harvest(int wanted)
        {
            int given = Mathf.Min(wanted, Mathf.Min(amountPerSwing, remaining));
            remaining -= given;
            Publish();
            Shrink();
            if (IsEmpty) Invoke(nameof(Hide), ShrinkSeconds);
            return given;
        }

        private void Shrink()
        {
            float fullness = (float)remaining / startingAmount;
            model.ScaleTo(fullScale * Mathf.Lerp(EmptyScale, 1f, fullness), ShrinkSeconds);
        }

        private void Hide() => gameObject.SetActive(false);

        private void Publish() => facts.Write(this, RtsFacts.Remaining, remaining);

        public static GameObject NearestLike(GameObject from, GameObject like)
        {
            if (like == null || !like.TryGetComponent(out ResourceNode wanted)) return null;

            ResourceNode nearest = Nearest(from.transform.position, wanted.Kind);
            return nearest != null ? nearest.gameObject : null;
        }

        public static ResourceNode Nearest(Vector3 from, Resource kind)
        {
            ResourceNode nearest = null;
            float nearestDistance = float.PositiveInfinity;

            foreach (ResourceNode node in All)
            {
                if (node.Kind != kind || node.IsEmpty) continue;

                float distance = Vector3.Distance(from, node.transform.position);
                if (distance >= nearestDistance) continue;

                nearest = node;
                nearestDistance = distance;
            }

            return nearest;
        }
    }
}
