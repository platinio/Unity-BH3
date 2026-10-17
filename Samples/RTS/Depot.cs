using System.Collections.Generic;
using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    public sealed class Depot : MonoBehaviour
    {
        public static readonly List<Depot> All = new();

        [SerializeField] private Treasury treasury;

        private void OnEnable() => All.Add(this);

        private void OnDisable() => All.Remove(this);

        public void Receive(Carrier carrier)
        {
            treasury.Add(carrier.Kind, carrier.Amount);
            carrier.Empty();
        }

        public static Depot Nearest(Vector3 from)
        {
            Depot nearest = null;
            float nearestDistance = float.PositiveInfinity;

            foreach (Depot depot in All)
            {
                float distance = Vector3.Distance(from, depot.transform.position);
                if (distance >= nearestDistance) continue;

                nearest = depot;
                nearestDistance = distance;
            }

            return nearest;
        }
    }
}
