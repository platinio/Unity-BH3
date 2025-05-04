using System;
using UnityEngine;
using UnityEngine.AI;

namespace ArcaneOnyx.Share
{
    public class AIEntityAnimator : MonoBehaviour
    {
        [SerializeField] private NavMeshAgent navAgent;
        [SerializeField] private float maxMovementSpeed;
        [SerializeField] private float dampTime;
        [SerializeField] private float minMovementSpeed;
      
        private Animator animator;
        
        private static readonly int Forward = Animator.StringToHash("Forward");
        private static readonly int Horizontal = Animator.StringToHash("Horizontal");
        private static readonly int Movement = Animator.StringToHash("Movement");

        private void Awake()
        {
            animator = GetComponentInChildren<Animator>();
        }

        private void Update()
        {
            float h = Vector3.Dot(transform.right, navAgent.desiredVelocity) / maxMovementSpeed;
            float f = Vector3.Dot(transform.forward, navAgent.desiredVelocity) / maxMovementSpeed;

            
            float currentDamp = h + f < Mathf.Epsilon ? 0.01f : dampTime;
            
            animator.SetFloat(Forward, f, currentDamp, Time.deltaTime);
            animator.SetFloat(Horizontal, h, currentDamp, Time.deltaTime);

            float movement = Mathf.Abs(h) + Mathf.Abs(f);
            animator.SetFloat(Movement, movement);
        }
    }
}

