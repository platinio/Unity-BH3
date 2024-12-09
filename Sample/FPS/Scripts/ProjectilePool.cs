using Platinio.SDK.DependencyInjection;
using UnityEngine;
using UnityEngine.Pool;

namespace ArcaneOnyx.BehaviorTree.Sample
{
    public interface IProjectilePool
    {
        Projectile Instantiate(Vector3 position, Quaternion rotation);
        void Destroy(Projectile bullet);
    }

    public class ProjectilePool : MonoBehaviour, IProjectilePool
    {
        [SerializeField] private Projectile projectilePrefab;
        [SerializeField] private int maxPoolSize;
        
        private IObjectPool<Projectile> pool;

        private void Awake()
        {
            ServicesContainer.Register(typeof(IProjectilePool), this);
            pool = new LinkedPool<Projectile>(CreateBullet, OnTakeFromPool, OnReturnedToPool, OnDestroyPoolObject, false, maxPoolSize);
        }
        
        Projectile CreateBullet()
        {
            return Instantiate(projectilePrefab);
        }

        // Called when an item is returned to the pool using Release
        void OnReturnedToPool(Projectile bullet)
        {
            bullet.gameObject.SetActive(false);
            bullet.transform.parent = transform;
            bullet.Reset();
        }

        // Called when an item is taken from the pool using Get
        void OnTakeFromPool(Projectile bullet)
        {
            bullet.gameObject.SetActive(true);
            bullet.transform.parent = null;
        }

        // If the pool capacity is reached then any items returned will be destroyed.
        // We can control what the destroy behavior does, here we destroy the GameObject.
        void OnDestroyPoolObject(Projectile bullet)
        {
            Destroy(bullet.gameObject);
        }

        public Projectile Instantiate(Vector3 position, Quaternion rotation)
        {
            var projectile = pool.Get();
            projectile.transform.position = position;
            projectile.transform.rotation = rotation;
            return projectile;
        } 

        public void Destroy(Projectile bullet) => pool.Release(bullet);
    }
}

