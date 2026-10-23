using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    public sealed class BuildPad : MonoBehaviour
    {
        private const float RiseSeconds = 0.3f;

        private Turret turret;

        public bool IsFree => turret == null;

        public bool TryBuild(TurretKind kind, Turret prefab, DefenseGame game)
        {
            if (!IsFree || !game.TrySpend(kind.Cost)) return false;

            turret = Instantiate(prefab, transform.position, Quaternion.identity);
            turret.name = kind.Id + " turret";
            turret.Apply(kind);
            turret.gameObject.SetActive(true);
            RiseFromTheGround(turret);
            return true;
        }

        private static void RiseFromTheGround(Turret built)
        {
            built.transform.localScale = Vector3.zero;
            built.transform.ScaleTo(Vector3.one, RiseSeconds, Easing.OutBack);
        }
    }
}
