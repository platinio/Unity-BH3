using ArcaneOnyx.BehaviorTree;
using UnityEngine;
using UnityEngine.AI;

namespace ArcaneOnyx.BH3.Samples
{
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(Unit), typeof(BoxCollider), typeof(NavMeshObstacle))]
    public sealed class Building : MonoBehaviour
    {
        private const float Height = 5f;

        [SerializeField] private BuildingKind kind;
        [SerializeField] private bool startsBuilt;

        private float workDone;

        public BuildingKind Kind => kind;
        public float Progress01 => kind.BuildSeconds <= 0f ? 1f : Mathf.Clamp01(workDone / kind.BuildSeconds);
        public bool IsBuilt => Progress01 >= 1f;
        public Vector3 RallyPoint => transform.TransformPoint(kind.RallyOffset);

        private void Awake()
        {
            if (kind != null) Apply();
        }

        public void Apply(BuildingKind newKind)
        {
            kind = newKind;
            Apply();
        }

        public void Apply()
        {
            Dress();
            SizeFootprint();
            GetComponent<BehaviorTreeMachine>().nest.macro = kind.Tree;
            if (startsBuilt) workDone = kind.BuildSeconds;
            Publish();
        }

        public void AddWork(float seconds)
        {
            if (IsBuilt) return;

            workDone += seconds;
            if (IsBuilt) Unit.Of(gameObject).RestoreRestingPose();
            Publish();
        }

        private void Dress()
        {
            Transform model = transform.Find("Model");
            if (model == null) model = PlaceModel();

            Unit.Of(gameObject).AdoptBody(model, model.GetComponentsInChildren<Renderer>());
        }

        private Transform PlaceModel()
        {
            GameObject model = Instantiate(kind.Model, transform);
            model.name = "Model";
            model.transform.localScale = Vector3.one * kind.ModelScale;
            return model.transform;
        }

        private void SizeFootprint()
        {
            float width = kind.FootprintRadius * 2f;
            var size = new Vector3(width, Height, width);
            var centre = new Vector3(0f, Height / 2f, 0f);

            BoxCollider footprint = GetComponent<BoxCollider>();
            footprint.size = size;
            footprint.center = centre;

            NavMeshObstacle obstacle = GetComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.size = size;
            obstacle.center = centre;
            obstacle.carving = true;
        }

        private void Publish()
        {
            AgentVariableWriter facts = AgentVariableWriter.On(gameObject);
            facts.Write(this, RtsFacts.IsBuilt, IsBuilt);
            facts.Write(this, RtsFacts.Progress01, Progress01);
        }
    }
}
