using UnityEngine;
using UnityEngine.Events;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// Code for activate the ragdoll on this character
    /// </summary>
    public class Ragdoll : MonoBehaviour
    {
        /// <summary>
        /// Has the character something like a sword? put it here
        /// </summary>
        [SerializeField] private Transform[] equipment = null;
        [SerializeField] private UnityEvent onEnableRagdoll = null;

        public UnityEvent OnEnableRagdoll => onEnableRagdoll;

        protected Rigidbody[] rbArray = null;

        protected virtual void Awake()
        {
            rbArray = GetComponentsInChildren<Rigidbody>();
            SetKinematic(true);
        }

        /// <summary>
        /// Enable the ragdoll
        /// </summary>
        public virtual void EnableRagdoll()
        {
            onEnableRagdoll.Invoke();
            SetKinematic(false);

            for (int n = 0 ; n < equipment.Length ; n++)
            {
                equipment[n].parent = null;                
            }
        }
        
        public virtual void SetKinematic(bool newValue)
        {
            for (int n = 0 ; n < rbArray.Length ; n++)
            {
                rbArray[n].isKinematic = newValue;
            }
        }

        private void OnDestroy()
        {
            for (int n = 0; n < equipment.Length; n++)
            {
                if (equipment[n] != null)
                {
                    Destroy(equipment[n].gameObject);
                }
            }
        }
    }
}