using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    public sealed class Projectile : MonoBehaviour
    {
        private const float HitDistance = 0.4f;
        private const float LifetimeSeconds = 4f;

        [SerializeField] private float speed = 14f;

        private Transform target;
        private int damage;

        public void Launch(GameObject newTarget, int newDamage)
        {
            target = newTarget.transform;
            damage = newDamage;
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

            if (target.TryGetComponent(out Health targetHealth)) targetHealth.Damage(damage);
            Destroy(gameObject);
        }
    }
}
