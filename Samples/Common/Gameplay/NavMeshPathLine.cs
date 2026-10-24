using UnityEngine;
using UnityEngine.AI;

namespace ArcaneOnyx.BH3.Samples
{
    [RequireComponent(typeof(NavMeshAgent), typeof(LineRenderer))]
    public sealed class NavMeshPathLine : MonoBehaviour
    {
        [SerializeField] private float heightAboveGround = 0.05f;

        private NavMeshAgent navigation;
        private LineRenderer line;
        private Vector3[] corners = new Vector3[16];

        private void Awake()
        {
            navigation = GetComponent<NavMeshAgent>();
            line = GetComponent<LineRenderer>();
            line.positionCount = 0;
        }

        private void LateUpdate()
        {
            if (IsWalking) DrawTheRemainingPath();
            else line.positionCount = 0;
        }

        private bool IsWalking =>
            navigation.enabled && navigation.isOnNavMesh && navigation.hasPath && !navigation.isStopped
            && navigation.remainingDistance > navigation.stoppingDistance;

        private void DrawTheRemainingPath()
        {
            int cornerCount = CopyTheCorners();
            line.positionCount = cornerCount;
            line.SetPosition(0, Raised(transform.position));
            for (int i = 1; i < cornerCount; i++) line.SetPosition(i, Raised(corners[i]));
        }

        private int CopyTheCorners()
        {
            int cornerCount = navigation.path.GetCornersNonAlloc(corners);
            while (cornerCount == corners.Length)
            {
                corners = new Vector3[corners.Length * 2];
                cornerCount = navigation.path.GetCornersNonAlloc(corners);
            }

            return cornerCount;
        }

        private Vector3 Raised(Vector3 point) => point + Vector3.up * heightAboveGround;
    }
}
