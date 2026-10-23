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
            if (mouse == null || !mouse.rightButton.wasPressedThisFrame || HudCursor.IsOverTheHud || selection.Selected.Count == 0) return;

            Ray clickRay = viewCamera.ScreenPointToRay(mouse.position.ReadValue());
            if (!Physics.Raycast(clickRay, out RaycastHit hit)) return;

            Command(hit);
        }

        private void Command(RaycastHit hit)
        {
            Combatant combatant = hit.collider.GetComponentInParent<Combatant>();
            if (combatant != null)
            {
                if (combatant.Faction == Faction.Enemy && combatant.IsAlive) OrderAttack(combatant);
                return;
            }

            Building building = hit.collider.GetComponentInParent<Building>();
            if (building != null)
            {
                if (!building.IsBuilt) OrderBuild(building.gameObject);
                return;
            }

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

        private void OrderAttack(Combatant enemy)
        {
            ordersIssued++;
            foreach (Selectable unit in selection.Selected)
            {
                if (unit.TryGetComponent(out Sight _)) AttackMoveTo(unit.gameObject, enemy.transform.position);
                else Target(unit.gameObject, enemy);
            }
        }

        private void AttackMoveTo(GameObject unit, Vector3 point)
        {
            AgentVariableWriter facts = CancelAllOrders(unit);
            facts.Write(this, ClickToMoveFacts.Destination, point);
            facts.Write(this, ClickToMoveFacts.OrderNumber, ordersIssued);
        }

        private void Target(GameObject unit, Combatant enemy)
        {
            AgentVariableWriter facts = CancelAllOrders(unit);
            facts.Write(this, CombatFacts.Target, enemy.gameObject);
            facts.Write(this, CombatFacts.HasTarget, true);
        }

        public void OrderBuild(GameObject site)
        {
            ordersIssued++;
            foreach (Selectable unit in selection.Selected)
            {
                if (!unit.TryGetComponent(out Carrier _)) continue;

                AgentVariableWriter facts = CancelAllOrders(unit.gameObject);
                facts.Write(this, RtsFacts.Site, site);
                facts.Write(this, RtsFacts.BuildOrder, ordersIssued);
            }
        }

        private void OrderGather(GameObject resource)
        {
            ordersIssued++;
            foreach (Selectable unit in selection.Selected)
            {
                if (!unit.TryGetComponent(out Carrier _)) continue;

                AgentVariableWriter facts = CancelAllOrders(unit.gameObject);
                facts.Write(this, RtsFacts.Resource, resource);
                facts.Write(this, RtsFacts.GatherOrder, ordersIssued);
            }
        }

        private void OrderMove(Vector3 point)
        {
            ordersIssued++;
            foreach (Selectable unit in selection.Selected)
            {
                AgentVariableWriter facts = CancelAllOrders(unit.gameObject);
                facts.Write(this, CombatFacts.HasTarget, false);
                facts.Write(this, RtsFacts.HoldsFire, true);
                facts.Write(this, ClickToMoveFacts.Destination, point);
                facts.Write(this, ClickToMoveFacts.OrderNumber, ordersIssued);
            }
        }

        private AgentVariableWriter CancelAllOrders(GameObject unit)
        {
            AgentVariableWriter facts = AgentVariableWriter.On(unit);
            facts.Write(this, RtsFacts.HoldsFire, false);
            CancelOrder(facts, ClickToMoveFacts.OrderNumber, ClickToMoveFacts.CompletedOrder);
            CancelOrder(facts, RtsFacts.GatherOrder, RtsFacts.CompletedGatherOrder);
            CancelOrder(facts, RtsFacts.BuildOrder, RtsFacts.CompletedBuildOrder);
            return facts;
        }

        private void CancelOrder(AgentVariableWriter facts, string orderKey, string completedKey)
        {
            object issued = facts.Read(orderKey);
            if (issued != null) facts.Write(this, completedKey, issued);
        }
    }
}
