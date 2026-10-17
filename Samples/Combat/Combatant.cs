using System.Collections.Generic;
using ArcaneOnyx.BehaviorTree;
using UnityEngine;
using UnityEngine.AI;

namespace ArcaneOnyx.BH3.Samples
{
    public enum Faction
    {
        Player,
        Enemy
    }

    [RequireComponent(typeof(Health), typeof(NavMeshAgent), typeof(Knockback))]
    public sealed class Combatant : MonoBehaviour
    {
        public static readonly List<Combatant> All = new();

        [SerializeField] private Faction faction;

        private Health health;
        private NavMeshAgent navAgent;
        private BodyPose bodyPose;
        private Vector3 spawnPosition;
        private Quaternion spawnRotation;

        public Faction Faction => faction;
        public bool IsAlive => !health.IsDead;
        public Vector3 EyePosition => transform.position + Vector3.up;

        private void Awake()
        {
            health = GetComponent<Health>();
            navAgent = GetComponent<NavMeshAgent>();
            bodyPose = BodyPose.Of(AgentBody.Find(gameObject));
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
            AgentVariableWriter.On(gameObject).Write(this, CombatFacts.Home, spawnPosition);
        }

        private void OnEnable() => All.Add(this);

        private void OnDisable() => All.Remove(this);

        public bool IsEnemyOf(Combatant other) => other.faction != faction;

        public void Respawn()
        {
            navAgent.Warp(spawnPosition);
            transform.rotation = spawnRotation;
            bodyPose.RestoreInstantly();
            health.Revive();
        }
    }
}
