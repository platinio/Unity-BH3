using ArcaneOnyx.Share;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Sample
{
    public class ConfigurableProjectileLauncher : MonoBehaviour
    {
        [Header("Launcher Config")]
        [SerializeField] private LayerMask layerMask;
        [SerializeField] private Projectile projectilePrefab;
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
            
            var projectile = Instantiate(projectilePrefab, spawnPosition.position, Quaternion.identity);
            projectile.Launch(shootInfo);
            return projectile;
        }
    }
}