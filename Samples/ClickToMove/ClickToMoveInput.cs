using ArcaneOnyx.BehaviorTree;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace ArcaneOnyx.BH3.Samples
{
    public sealed class ClickToMoveInput : MonoBehaviour
    {
        private const float MaxDistanceToNavMesh = 1f;

        [SerializeField] private GameObject agent;
        [SerializeField] private Transform destinationMarker;
        [SerializeField] private float markerPopDuration = 0.25f;

        private Camera viewCamera;
        private AgentVariableWriter agentFacts;
        private Vector3 markerRestingScale;
        private int ordersIssued;

        private void Awake()
        {
            viewCamera = Camera.main;
            agentFacts = AgentVariableWriter.On(agent);
            markerRestingScale = destinationMarker.localScale;
        }

        private void Update()
        {
            if (!WasNavMeshClickedThisFrame(out Vector3 clickedPoint)) return;

            IssueOrder(clickedPoint);
            PopMarkerAt(clickedPoint);
        }

        private bool WasNavMeshClickedThisFrame(out Vector3 pointOnNavMesh)
        {
            pointOnNavMesh = default;

            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return false;

            Ray clickRay = viewCamera.ScreenPointToRay(mouse.position.ReadValue());
            if (!Physics.Raycast(clickRay, out RaycastHit hit)) return false;
            if (!NavMesh.SamplePosition(hit.point, out NavMeshHit navMeshHit, MaxDistanceToNavMesh, NavMesh.AllAreas)) return false;

            pointOnNavMesh = navMeshHit.position;
            return true;
        }

        private void IssueOrder(Vector3 point)
        {
            ordersIssued++;
            agentFacts.Write(this, ClickToMoveFacts.Destination, point);
            agentFacts.Write(this, ClickToMoveFacts.OrderNumber, ordersIssued);
        }

        private void PopMarkerAt(Vector3 point)
        {
            destinationMarker.position = point;
            destinationMarker.localScale = Vector3.zero;
            destinationMarker.ScaleTo(markerRestingScale, markerPopDuration, Easing.OutBack);
        }
    }
}
