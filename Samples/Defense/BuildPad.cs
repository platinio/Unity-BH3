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
            Transform body = AgentBody.Find(built.gameObject);
            Vector3 restingScale = BodyPose.Of(body).RestingScale;
            body.localScale = Vector3.zero;
            body.ScaleTo(restingScale, RiseSeconds, Easing.OutBack);
        }
    }
}
