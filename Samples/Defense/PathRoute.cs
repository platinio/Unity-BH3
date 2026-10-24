using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    public sealed class PathRoute : MonoBehaviour
    {
        public int Count => transform.childCount;

        public Vector3 Start => PointAt(0);

        public Vector3 PointAt(int index) => transform.GetChild(index).position;
    }
}
