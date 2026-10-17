using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    [RequireComponent(typeof(DefenseGame))]
    public sealed class Spawner : MonoBehaviour
    {
        private const int FirstMixedWave = 3;

        [SerializeField] private Transform spawnPoint;
        [SerializeField] private PathRoute route;
        [SerializeField] private Walker walkerPrefab;
        [SerializeField] private EnemyKind[] kinds;

        private DefenseGame game;

        public DefenseGame Game => game != null ? game : game = GetComponent<DefenseGame>();

        public int WaveSize(int wave) => 3 + wave;

        public EnemyKind KindFor(int wave, int indexInWave) =>
            wave < FirstMixedWave ? kinds[0] : kinds[(wave + indexInWave) % kinds.Length];

        public void Spawn(EnemyKind kind)
        {
            Walker walker = Instantiate(walkerPrefab, spawnPoint.position, spawnPoint.rotation);
            walker.name = kind.name;
            walker.GetComponent<Enemy>().Apply(kind);
            walker.Follow(route, Game);
            walker.gameObject.SetActive(true);
        }
    }
}
