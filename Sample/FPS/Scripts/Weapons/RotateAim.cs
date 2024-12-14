using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Sample
{
    public class RotateAim : MonoBehaviour
    {
        [SerializeField] private Transform spineBone;
        [SerializeField] private Transform aimTransform;
        [SerializeField] private Transform targetTransform;
        [SerializeField] private float aimSpeed = 2.0f;
        
        private Quaternion lastFrameRot;
        private Quaternion animDesireRotation;

        private void Start()
        {
            lastFrameRot = spineBone.rotation;
        }
        
        private void LateUpdate()
        { 
            animDesireRotation = spineBone.rotation;
            spineBone.rotation = lastFrameRot;

            Quaternion targetRotation = GetTargetRotation();
            spineBone.rotation = Quaternion.Slerp(lastFrameRot, targetRotation, Time.deltaTime * aimSpeed);
       
            lastFrameRot = spineBone.rotation;
        }

        private Quaternion GetTargetRotation()
        {
            if (targetTransform == null) return animDesireRotation;
            
            Vector3 desireDir = (targetTransform.position - aimTransform.position).normalized;
            Quaternion targetRot = Quaternion.FromToRotation(aimTransform.forward, desireDir);
            targetRot = Quaternion.Euler(0.0f, targetRot.eulerAngles.y, targetRot.eulerAngles.z);
            
            return targetRot * spineBone.rotation;
        }

        public void CleanTarget()
        {
            targetTransform = null;
        }

        public void UpdateTarget(Transform target)
        {
            targetTransform = target;
        }
    }
}
