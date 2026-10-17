using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    public sealed class Projectile : MonoBehaviour
    {
        private const float HitDistance = 0.4f;
        private const float LifetimeSeconds = 4f;

        [SerializeField] private float speed = 14f;

        private Transform target;
        private WeaponDefinition weapon;
        private Vector3 launchedFrom;

        public void Launch(GameObject newTarget, WeaponDefinition firedBy)
        {
            target = newTarget.transform;
            weapon = firedBy;
            launchedFrom = transform.position;
            Destroy(gameObject, LifetimeSeconds);
        }

        private void Update()
        {
            if (target == null)
            {
                Destroy(gameObject);
                return;
            }

            Vector3 aimPoint = target.position + Vector3.up;
            transform.position = Vector3.MoveTowards(transform.position, aimPoint, speed * Time.deltaTime);
            if (Vector3.Distance(transform.position, aimPoint) > HitDistance) return;

            weapon.Land(target.gameObject, launchedFrom);
            Destroy(gameObject);
        }
    }
}
