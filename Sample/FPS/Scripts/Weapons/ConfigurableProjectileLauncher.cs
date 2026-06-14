// TODO: ServiceLocator submodule removed — restore projectile-pool resolution via the UnityExtensions service registry.
// using ArcaneOnyx.ServiceLocator;
using ArcaneOnyx.Share;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Sample
{
    public class ConfigurableProjectileLauncher : MonoBehaviour
    {
        [Header("Launcher Config")]
        [SerializeField] private LayerMask layerMask;
        [SerializeField] protected Transform spawnPosition;
        [SerializeField] private float dmg;
        [SerializeField] private float range;
        [SerializeField] private float speed;

        public Transform SpawnPosition => spawnPosition;

        public Projectile Launch(GameEntity gameEntity, ShootInfo shootInfo)
        {
            shootInfo.dir = spawnPosition.forward;
            shootInfo.dmg = dmg;
            shootInfo.speed = speed;
            shootInfo.range = range;
            shootInfo.hitLayer = layerMask;

            // TODO: ServiceLocator removed — resolve the pool, spawn and launch the projectile once resolution is restored.
            // var projectilePool = ServicesContainer.Resolve<IProjectilePool>();
            // var projectile = projectilePool.Instantiate(spawnPosition.position, Quaternion.identity);
            // projectile.Launch(shootInfo);
            // return projectile;
            return null;
        }
    }
}