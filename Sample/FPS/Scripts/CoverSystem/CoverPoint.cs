using Platinio.AI;
using UnityEngine;

namespace Platinio.BehaviorTree.Sample
{
    public class CoverPoint : MonoBehaviour
    {
        private AIEntity entityInCover;

        public bool IsOcuppied => entityInCover != null;
    }
}

