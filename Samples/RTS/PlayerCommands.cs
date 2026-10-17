using ArcaneOnyx.BehaviorTree;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace ArcaneOnyx.BH3.Samples
{
    public sealed class PlayerCommands : MonoBehaviour
    {
        private const float MaxDistanceToNavMesh = 1f;

        [SerializeField] private SelectionBox selection;

        private Camera viewCamera;
        private int ordersIssued;

        private void Awake() => viewCamera = Camera.main;

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.rightButton.wasPressedThisFrame || selection.Selected.Count == 0) return;

            Ray clickRay = viewCamera.ScreenPointToRay(mouse.position.ReadValue());
            if (!Physics.Raycast(clickRay, out RaycastHit hit)) return;

            Command(hit);
        }

        private void Command(RaycastHit hit)
        {
            ResourceNode resource = hit.collider.GetComponentInParent<ResourceNode>();
            if (resource != null)
            {
                OrderGather(resource.gameObject);
            }
            else if (NavMesh.SamplePosition(hit.point, out NavMeshHit navMeshHit, MaxDistanceToNavMesh, NavMesh.AllAreas))
            {
                OrderMove(navMeshHit.position);
            }
        }

        private void OrderGather(GameObject resource)
        {
            ordersIssued++;
            foreach (Selectable unit in selection.Selected)
            {
                if (!unit.TryGetComponent(out Carrier _)) continue;

                AgentVariableWriter facts = AgentVariableWriter.On(unit.gameObject);
                CancelOrder(facts, ClickToMoveFacts.OrderNumber, ClickToMoveFacts.CompletedOrder);
                facts.Write(this, RtsFacts.Resource, resource);
                facts.Write(this, RtsFacts.GatherOrder, ordersIssued);
            }
        }

        private void OrderMove(Vector3 point)
        {
            ordersIssued++;
            foreach (Selectable unit in selection.Selected)
            {
                AgentVariableWriter facts = AgentVariableWriter.On(unit.gameObject);
                CancelOrder(facts, RtsFacts.GatherOrder, RtsFacts.CompletedGatherOrder);
                facts.Write(this, ClickToMoveFacts.Destination, point);
                facts.Write(this, ClickToMoveFacts.OrderNumber, ordersIssued);
            }
        }

        private void CancelOrder(AgentVariableWriter facts, string orderKey, string completedKey)
        {
            object issued = facts.Read(orderKey);
            if (issued != null) facts.Write(this, completedKey, issued);
        }
    }
}
