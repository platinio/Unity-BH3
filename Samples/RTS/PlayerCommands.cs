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
            if (!NavMesh.SamplePosition(hit.point, out NavMeshHit navMeshHit, MaxDistanceToNavMesh, NavMesh.AllAreas)) return;

            ordersIssued++;
            foreach (Selectable unit in selection.Selected) OrderMove(unit.gameObject, navMeshHit.position);
        }

        private void OrderMove(GameObject unit, Vector3 point)
        {
            AgentVariableWriter facts = AgentVariableWriter.On(unit);
            facts.Write(this, ClickToMoveFacts.Destination, point);
            facts.Write(this, ClickToMoveFacts.OrderNumber, ordersIssued);
        }
    }
}
