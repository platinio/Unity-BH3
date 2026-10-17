using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace ArcaneOnyx.BH3.Samples
{
    public sealed class BuildingPlacer : MonoBehaviour
    {
        private const float MaxDistanceToNavMesh = 0.5f;

        [SerializeField] private SelectionBox selection;
        [SerializeField] private PlayerCommands commands;
        [SerializeField] private Treasury treasury;
        [SerializeField] private Building buildingPrefab;
        [SerializeField] private Transform buildingsParent;
        [SerializeField] private BuildingKind[] kinds;
        [SerializeField] private Material freeGhostMaterial;
        [SerializeField] private Material blockedGhostMaterial;

        private Camera viewCamera;
        private BuildingKind placing;
        private GameObject ghost;
        private Vector3 ghostPoint;
        private bool ghostSpotIsFree;

        public IReadOnlyList<BuildingKind> Kinds => kinds;
        public BuildingKind Placing => placing;

        private void Awake() => viewCamera = Camera.main;

        private void Update()
        {
            if (placing == null)
            {
                HandlePickKeys();
                return;
            }

            FollowCursor();
            HandleCancel();
            HandlePlace();
        }

        private void HandlePickKeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !WorkersAreSelected()) return;

            if (keyboard.digit1Key.wasPressedThisFrame) BeginPlacing(0);
            if (keyboard.digit2Key.wasPressedThisFrame) BeginPlacing(1);
        }

        private bool WorkersAreSelected()
        {
            bool anyWorker = false;
            foreach (Selectable selected in selection.Selected)
            {
                if (selected.SelectsAlone) return false;
                anyWorker |= selected.TryGetComponent(out Carrier _);
            }

            return anyWorker;
        }

        private void BeginPlacing(int kindIndex)
        {
            if (kindIndex >= kinds.Length) return;

            BuildingKind kind = kinds[kindIndex];
            if (!treasury.CanAfford(kind.WoodCost, kind.GoldCost)) return;

            placing = kind;
            ghost = Instantiate(kind.Model);
            ghost.name = kind.Id + " ghost";
            ghost.transform.localScale = Vector3.one * kind.ModelScale;
            foreach (Collider collider in ghost.GetComponentsInChildren<Collider>()) Destroy(collider);
            selection.enabled = false;
            commands.enabled = false;
        }

        private void FollowCursor()
        {
            Mouse mouse = Mouse.current;
            Ray cursorRay = viewCamera.ScreenPointToRay(mouse.position.ReadValue());
            if (!Physics.Raycast(cursorRay, out RaycastHit hit)) return;

            ghostPoint = hit.point;
            ghostSpotIsFree = IsFree(ghostPoint);
            ghost.transform.position = ghostPoint;
            Tint(ghostSpotIsFree ? freeGhostMaterial : blockedGhostMaterial);
        }

        private bool IsFree(Vector3 point)
        {
            if (!NavMesh.SamplePosition(point, out _, MaxDistanceToNavMesh, NavMesh.AllAreas)) return false;

            foreach (Collider nearby in Physics.OverlapSphere(point, placing.FootprintRadius))
            {
                if (nearby.GetComponentInParent<Building>() != null) return false;
                if (nearby.GetComponentInParent<ResourceNode>() != null) return false;
            }

            return true;
        }

        private void Tint(Material material)
        {
            foreach (Renderer renderer in ghost.GetComponentsInChildren<Renderer>())
            {
                var materials = new Material[renderer.sharedMaterials.Length];
                for (int i = 0; i < materials.Length; i++) materials[i] = material;
                renderer.sharedMaterials = materials;
            }
        }

        private void HandleCancel()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            bool cancelled = (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) || (mouse != null && mouse.rightButton.wasPressedThisFrame);
            if (cancelled) StopPlacing();
        }

        private void HandlePlace()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame || !ghostSpotIsFree) return;

            if (treasury.TrySpend(placing.WoodCost, placing.GoldCost)) RaiseSite(placing, ghostPoint);
            StopPlacing();
        }

        private void RaiseSite(BuildingKind kind, Vector3 point)
        {
            Building site = Instantiate(buildingPrefab, point, Quaternion.identity, buildingsParent);
            site.name = kind.Id;
            site.Apply(kind);
            site.gameObject.SetActive(true);
            commands.OrderBuild(site.gameObject);
        }

        private void StopPlacing()
        {
            Destroy(ghost);
            ghost = null;
            placing = null;
            selection.enabled = true;
            commands.enabled = true;
        }
    }
}
