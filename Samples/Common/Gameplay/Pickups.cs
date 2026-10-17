using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    public static class Pickups
    {
        public static bool CanEquipWeapon(GameObject taker, WeaponDefinition weapon) =>
            weapon != null && taker.TryGetComponent(out Weapon held) && held.Definition != weapon;

        public static bool GiveWeapon(GameObject taker, WeaponDefinition weapon)
        {
            if (!CanEquipWeapon(taker, weapon)) return false;

            taker.GetComponent<Weapon>().Equip(weapon);
            return true;
        }

        public static bool IsHurt(GameObject taker) =>
            taker.TryGetComponent(out Health health) && !health.IsDead && !health.IsFull;

        public static bool Heal(GameObject taker, int amount)
        {
            if (!IsHurt(taker)) return false;

            taker.GetComponent<Health>().Heal(amount);
            return true;
        }
    }
}
