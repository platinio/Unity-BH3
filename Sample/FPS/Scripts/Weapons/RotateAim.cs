using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Sample
{
    public class RotateAim : MonoBehaviour
    {
        [SerializeField] private Transform spineBone;
        [SerializeField] private Transform bulletSpawnTransform;
        [SerializeField] private Transform targetTransform;
        [SerializeField] private float aimSpeed = 2.0f;
        
        private Quaternion lastFrameRot;

        private void Start()
        {
            lastFrameRot = spineBone.rotation;
        }
        
        private void LateUpdate()
        { 
            if (targetTransform == null) return;
            
            spineBone.rotation = lastFrameRot;
            
            Vector3 desireDir = (targetTransform.position - bulletSpawnTransform.position).normalized;

            Quaternion targetRot = Quaternion.FromToRotation(bulletSpawnTransform.forward, desireDir);
            targetRot = Quaternion.Euler(0.0f, targetRot.eulerAngles.y, targetRot.eulerAngles.z);
            
            Quaternion rot = targetRot * spineBone.rotation;
            spineBone.rotation = Quaternion.Slerp(lastFrameRot, rot, Time.deltaTime * aimSpeed);
       
            lastFrameRot = spineBone.rotation;
        }
        
        public void UpdateTarget(Transform target)
        {
            targetTransform = target;
        }
    }
}
