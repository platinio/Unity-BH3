using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Platinio
{
    public class AIEntityAnimator : MonoBehaviour
    {
        [SerializeField] private NavMeshAgent navAgent;
        [SerializeField] private Animator animator;
        [SerializeField] private float maxMovementSpeed;
        [SerializeField] private float dampTime;
      
        private static readonly int Forward = Animator.StringToHash("Forwards");
        private static readonly int Horizontal = Animator.StringToHash("Horizontal");

        private void Update()
        {
            float h = Vector3.Dot(transform.right, navAgent.desiredVelocity) / maxMovementSpeed;
            float f = Vector3.Dot(transform.forward, navAgent.desiredVelocity) / maxMovementSpeed;

            float currentDamp = h + f < Mathf.Epsilon ? 0.01f : dampTime;
            
            animator.SetFloat(Forward, f, currentDamp, Time.deltaTime);
            animator.SetFloat(Horizontal, h, currentDamp, Time.deltaTime);
        }
    }
}

