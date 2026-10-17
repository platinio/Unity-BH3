using UnityEngine;
using UnityEngine.AI;

namespace ArcaneOnyx.BH3.Samples
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class Knockback : MonoBehaviour
    {
        private const float Drag = 8f;
        private const float RestSpeed = 0.1f;

        private NavMeshAgent navAgent;
        private Vector3 velocity;

        private void Awake() => navAgent = GetComponent<NavMeshAgent>();

        public void Push(Vector3 impulse) => velocity += impulse;

        private void Update()
        {
            if (velocity.magnitude < RestSpeed) return;

            navAgent.Move(velocity * Time.deltaTime);
            velocity = Vector3.MoveTowards(velocity, Vector3.zero, Drag * Time.deltaTime);
        }
    }
}
