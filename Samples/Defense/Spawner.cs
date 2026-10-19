using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    [RequireComponent(typeof(DefenseGame))]
    public sealed class Spawner : MonoBehaviour
    {
        private const int FirstMixedWave = 3;

        [SerializeField] private Transform spawnPoint;
        [SerializeField] private PathRoute route;
        [SerializeField] private Walker[] enemies;

        private DefenseGame game;

        public DefenseGame Game => game != null ? game : game = GetComponent<DefenseGame>();

        public int WaveSize(int wave) => 3 + wave;

        public Walker EnemyFor(int wave, int indexInWave) =>
            wave < FirstMixedWave ? enemies[0] : enemies[(wave + indexInWave) % enemies.Length];

        public void Spawn(Walker enemy)
        {
            Walker walker = Instantiate(enemy, spawnPoint.position, spawnPoint.rotation);
            walker.name = enemy.name;
            walker.Follow(route, Game);
            walker.gameObject.SetActive(true);
        }
    }
}
