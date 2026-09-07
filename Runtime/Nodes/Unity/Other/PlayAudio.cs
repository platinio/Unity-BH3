using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Audio/Play Audio")]
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

            TryResolve(AudioSource, out audioPlayer);
        }

        public override ExecutionStatus OnUpdate()
        {
            if (audioPlayer == null) return ExecutionStatus.Failure;

            audioPlayer.clip = audioClip;
            audioPlayer.Play();

            return ExecutionStatus.Success;
        }
    }
}