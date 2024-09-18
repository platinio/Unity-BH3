using System.Collections.Generic;
using ArcaneOnyx.Share;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Sample
{
    public class Weapon : MonoBehaviour
    {
        [SerializeField] private List<ConfigurableProjectileLauncher> projectileLaunchers;
        [SerializeField] private GameEntity owner;
        [SerializeField] private float shootRate;
        
        protected ShootInfo cacheShootInfo;
        private float lastShootTime = 0;
        
        protected virtual ShootInfo CreateShootInfo()
        {
            cacheShootInfo.sender = owner;
            return cacheShootInfo;
        }

        public void Shoot()
        {
            if (Time.time - lastShootTime < shootRate) return;
            
            lastShootTime = Time.time;
            foreach (var projectileLauncher in projectileLaunchers)
            {
                projectileLauncher.Launch(owner, CreateShootInfo());
            }   
        }
    }

}

