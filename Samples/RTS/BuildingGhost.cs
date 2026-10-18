using UnityEngine;
using UnityEngine.AI;

namespace ArcaneOnyx.BH3.Samples
{
    public sealed class BuildingGhost
    {
        private const float MaxDistanceToNavMesh = 0.5f;

        private readonly BuildingKind kind;
        private readonly Material freeMaterial;
        private readonly Material blockedMaterial;
        private readonly GameObject body;
        private readonly Renderer[] renderers;

        public BuildingGhost(BuildingKind kind, Material freeMaterial, Material blockedMaterial)
        {
            this.kind = kind;
            this.freeMaterial = freeMaterial;
            this.blockedMaterial = blockedMaterial;
            body = Object.Instantiate(kind.Prefab.Body.gameObject);
            body.name = kind.Id + " ghost";
            renderers = body.GetComponentsInChildren<Renderer>();
            foreach (Collider collider in body.GetComponentsInChildren<Collider>()) Object.Destroy(collider);
        }

        public Vector3 Spot { get; private set; }
        public bool SpotIsFree { get; private set; }

        public void ShowAt(Vector3 spot)
        {
            Spot = spot;
            SpotIsFree = IsFree(spot);
            body.transform.position = spot;
            Tint(SpotIsFree ? freeMaterial : blockedMaterial);
        }

        public void Hide() => Object.Destroy(body);

        private bool IsFree(Vector3 spot)
        {
            if (!NavMesh.SamplePosition(spot, out _, MaxDistanceToNavMesh, NavMesh.AllAreas)) return false;

            foreach (Collider nearby in Physics.OverlapSphere(spot, kind.FootprintRadius))
            {
                if (nearby.GetComponentInParent<Building>() != null) return false;
                if (nearby.GetComponentInParent<ResourceNode>() != null) return false;
            }

            return true;
        }

        private void Tint(Material material)
        {
            foreach (Renderer renderer in renderers)
            {
                var materials = new Material[renderer.sharedMaterials.Length];
                for (int i = 0; i < materials.Length; i++) materials[i] = material;
                renderer.sharedMaterials = materials;
            }
        }
    }
}
