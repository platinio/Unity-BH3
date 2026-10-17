using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    public static class Pickups
    {
        public static bool GiveWeapon(GameObject taker, WeaponDefinition weapon)
        {
            if (weapon == null || !taker.TryGetComponent(out Weapon held)) return false;

            held.Equip(weapon);
            return true;
        }

        public static bool Heal(GameObject taker, int amount)
        {
            if (!taker.TryGetComponent(out Health health) || health.IsDead) return false;

            health.Heal(amount);
            return true;
        }
    }
}
