using System.Collections.Generic;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Sample
{
    public class CoverSystem : MonoBehaviour
    {
        private CoverPoint[] coverPoints;

        public IReadOnlyCollection<CoverPoint> CoverPoints => coverPoints;
        
        private void Awake()
        {
            coverPoints = GetComponentsInChildren<CoverPoint>();
        }
    }
}