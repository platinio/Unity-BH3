using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    public sealed class TurretGhost
    {
        private readonly Material freeMaterial;
        private readonly Material blockedMaterial;
        private readonly GameObject root;
        private readonly Renderer[] renderers;

        public TurretGhost(Turret prefab, Material freeMaterial, Material blockedMaterial)
        {
            this.freeMaterial = freeMaterial;
            this.blockedMaterial = blockedMaterial;
            root = new GameObject("Turret ghost");
            GameObject body = Object.Instantiate(prefab.GetComponent<Unit>().Body.gameObject, root.transform);
            renderers = body.GetComponentsInChildren<Renderer>();
            foreach (Collider collider in body.GetComponentsInChildren<Collider>()) Object.Destroy(collider);
        }

        public BuildPad Pad { get; private set; }

        public bool IsOverFreePad => Pad != null;

        public void ShowOnPad(BuildPad pad)
        {
            Pad = pad;
            Place(pad.transform.position, freeMaterial);
        }

        public void ShowBlockedAt(Vector3 spot)
        {
            Pad = null;
            Place(spot, blockedMaterial);
        }

        public void Hide() => Object.Destroy(root);

        private void Place(Vector3 spot, Material tint)
        {
            root.transform.position = spot;
            foreach (Renderer renderer in renderers) renderer.sharedMaterial = tint;
        }
    }
}
