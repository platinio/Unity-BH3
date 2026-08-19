using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Unity/Other/Play Audio")]
    public class PlayAudio : GameplayNode
    {
        [DoNotSerialize]
        public ValueInput AudioSource { get; private set; }
       
        [Serialize, Inspectable] 
        private AudioClip audioClip;

        private AudioSource audioPlayer;

        public override string NodeName => "Play Audio";
        public override string Description => "Plays an audio clip";

        protected override void Definition()
        {
            base.Definition();
            AudioSource = ValueInput<Object>(nameof(AudioSource), null);
        }

        public override void OnEnter()
        {
            base.OnEnter();

            // Resolved per entry rather than once, because Target is a port: what it points at can
            // differ between one entry and the next.
            audioPlayer = GetComponent<AudioSource>(AudioSource);
        }

        public override ExecutionStatus OnUpdate()
        {
            audioPlayer.clip = audioClip;
            audioPlayer.Play();

            return ExecutionStatus.Success;
        }
    }
}