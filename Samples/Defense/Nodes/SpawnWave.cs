using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.GraphCore;
using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    [GraphCreateMenu("Samples/Defense/Spawn Wave")]
    public class SpawnWave : GameplayNode
    {
        [Unity.VisualScripting.DoNotSerialize]
        public ValueInput SecondsBetweenEnemies { get; private set; }

        public override string NodeName => "Spawn Wave";
        public override string Description => "Spawns this wave's enemies at the gate, one every SecondsBetweenEnemies, and succeeds once the last one is out.";

        private sealed class Memory
        {
            public int Wave;
            public int ToSpawn;
            public int Spawned;
            public float NextSpawnAt;
        }

        protected override void Definition()
        {
            base.Definition();

            SecondsBetweenEnemies = ValueInput<float>(nameof(SecondsBetweenEnemies), 0.7f);
        }

        public override void OnEnter(BTContext ctx)
        {
            Spawner spawner = ctx.GetComponent<Spawner>();
            Memory memory = ctx.Memory<Memory>();
            memory.Wave = spawner.Game.Wave;
            memory.ToSpawn = spawner.WaveSize(memory.Wave);
            memory.Spawned = 0;
            memory.NextSpawnAt = Time.time;
        }

        public override ExecutionStatus OnUpdate(BTContext ctx)
        {
            Memory memory = ctx.Memory<Memory>();
            if (memory.Spawned >= memory.ToSpawn) return ExecutionStatus.Success;
            if (Time.time < memory.NextSpawnAt) return ExecutionStatus.Running;

            Spawner spawner = ctx.GetComponent<Spawner>();
            spawner.Spawn(spawner.EnemyFor(memory.Wave, memory.Spawned));
            memory.Spawned++;
            memory.NextSpawnAt = Time.time + ctx.GetValue<float>(SecondsBetweenEnemies);
            return memory.Spawned >= memory.ToSpawn ? ExecutionStatus.Success : ExecutionStatus.Running;
        }
    }
}
