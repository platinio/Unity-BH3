using ArcaneOnyx.Share;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Sample
{
    public class CoverPoint : MonoBehaviour
    {
        private AIEntity entityInCover;

        public bool IsOcuppied => entityInCover != null;
    }
}

