using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ArcaneOnyx.BH3.Samples
{
    public enum Faction
    {
        Player,
        Enemy
    }

    [RequireComponent(typeof(Health), typeof(NavMeshAgent))]
    public sealed class Combatant : MonoBehaviour
    {
        public static readonly List<Combatant> All = new();

        [SerializeField] private Faction faction;

        private Health health;
        private NavMeshAgent navAgent;
        private Transform body;
        private Vector3 bodyRestingScale;
        private Vector3 spawnPosition;
        private Quaternion spawnRotation;

        public Faction Faction => faction;
        public bool IsAlive => !health.IsDead;
        public Vector3 EyePosition => transform.position + Vector3.up;

        private void Awake()
        {
            health = GetComponent<Health>();
            navAgent = GetComponent<NavMeshAgent>();
            body = AgentBody.Find(gameObject);
            bodyRestingScale = body.localScale;
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
        }

        private void OnEnable() => All.Add(this);

        private void OnDisable() => All.Remove(this);

        public bool IsEnemyOf(Combatant other) => other.faction != faction;

        public void Respawn()
        {
            navAgent.Warp(spawnPosition);
            transform.rotation = spawnRotation;
            body.localScale = bodyRestingScale;
            health.Revive();
        }
    }
}
