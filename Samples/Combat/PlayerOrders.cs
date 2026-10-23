using ArcaneOnyx.BehaviorTree;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace ArcaneOnyx.BH3.Samples
{
    public sealed class PlayerOrders : MonoBehaviour
    {
        private const float MaxDistanceToNavMesh = 1f;

        [SerializeField] private Combatant player;
        [SerializeField] private DestinationMarker destinationMarker;

        private Camera viewCamera;
        private AgentVariableWriter playerFacts;
        private int ordersIssued;
        private bool playerWasAlive = true;

        private void Awake()
        {
            viewCamera = Camera.main;
            playerFacts = AgentVariableWriter.On(player.gameObject);
        }

        private void Update()
        {
            HandleClick();
            ForgetOrdersWhenPlayerDies();
        }

        private void HandleClick()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

            Ray clickRay = viewCamera.ScreenPointToRay(mouse.position.ReadValue());
            if (!Physics.Raycast(clickRay, out RaycastHit hit)) return;

            Combatant clicked = hit.collider.GetComponentInParent<Combatant>();
            if (clicked != null && player.IsEnemyOf(clicked))
            {
                OrderAttack(clicked);
            }
            else if (NavMesh.SamplePosition(hit.point, out NavMeshHit navMeshHit, MaxDistanceToNavMesh, NavMesh.AllAreas))
            {
                OrderMove(navMeshHit.position);
            }
        }

        private void OrderAttack(Combatant enemy)
        {
            CancelPendingMove();
            playerFacts.Write(this, CombatFacts.Target, enemy.gameObject);
            playerFacts.Write(this, CombatFacts.HasTarget, true);
        }

        private void OrderMove(Vector3 point)
        {
            DropTarget();
            ordersIssued++;
            playerFacts.Write(this, ClickToMoveFacts.Destination, point);
            playerFacts.Write(this, ClickToMoveFacts.OrderNumber, ordersIssued);
            destinationMarker.ShowAt(point);
        }

        private void ForgetOrdersWhenPlayerDies()
        {
            bool playerJustDied = playerWasAlive && !player.IsAlive;
            playerWasAlive = player.IsAlive;
            if (!playerJustDied) return;

            DropTarget();
            CancelPendingMove();
        }

        private void DropTarget() => playerFacts.Write(this, CombatFacts.HasTarget, false);

        private void CancelPendingMove() => playerFacts.Write(this, ClickToMoveFacts.CompletedOrder, ordersIssued);
    }
}
