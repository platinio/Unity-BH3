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

        public override string NodeName => "Play Audio";
        public override string Description => "Plays an audio clip";

        protected override void Definition()
        {
            base.Definition();
            AudioSource = ValueInput<Object>(nameof(AudioSource), null);
        }

        public override ExecutionStatus OnUpdate()
        {
            var audioPlayer = GetComponent<AudioSource>(AudioSource);
            audioPlayer.clip = audioClip;
            audioPlayer.Play();

            return ExecutionStatus.Success;
        }
    }
}