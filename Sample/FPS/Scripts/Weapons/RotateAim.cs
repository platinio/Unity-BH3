using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Sample
{
    public class RotateAim : MonoBehaviour
    {
        [SerializeField] private Transform spineBone;
        [SerializeField] private Transform bulletSpawnTransform;
        [SerializeField] private Transform targetTransform;
        
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
            Quaternion rot = Quaternion.FromToRotation(bulletSpawnTransform.forward, desireDir) * spineBone.rotation;
            spineBone.rotation = Quaternion.Slerp(lastFrameRot, rot, Time.deltaTime * 10);
            lastFrameRot = spineBone.rotation;
        }
        
        public void UpdateTarget(Transform target)
        {
            targetTransform = target;
        }
    }
}
