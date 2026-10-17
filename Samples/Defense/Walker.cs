using ArcaneOnyx.BehaviorTree;
using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    [RequireComponent(typeof(Enemy), typeof(Health), typeof(AgentVariableWriter))]
    public sealed class Walker : MonoBehaviour
    {
        private DefenseGame game;
        private PathRoute route;
        private int nextWaypoint;
        private bool bountyPaid;
        private AgentVariableWriter facts;
        private Health health;

        private AgentVariableWriter Facts => facts != null ? facts : facts = AgentVariableWriter.On(gameObject);

        private void Awake() => health = GetComponent<Health>();

        private void OnEnable() => health.Changed += PayBountyOnDeath;

        private void OnDisable() => health.Changed -= PayBountyOnDeath;

        public void Follow(PathRoute newRoute, DefenseGame newGame)
        {
            route = newRoute;
            game = newGame;
            nextWaypoint = 0;
            PublishNextWaypoint();
        }

        public void Advance()
        {
            nextWaypoint++;
            PublishNextWaypoint();
        }

        public void Breach() => game.LoseLife();

        private void PublishNextWaypoint()
        {
            bool reachedEnd = nextWaypoint >= route.Count;
            Facts.Write(this, DefenseFacts.ReachedEnd, reachedEnd);
            if (!reachedEnd) Facts.Write(this, DefenseFacts.NextWaypoint, route.PointAt(nextWaypoint));
        }

        private void PayBountyOnDeath()
        {
            if (!health.IsDead || bountyPaid || game == null) return;

            bountyPaid = true;
            game.AddPoints(GetComponent<Enemy>().Kind.Bounty);
        }
    }
}
