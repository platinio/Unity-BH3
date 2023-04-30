using System.Collections.Generic;
using Platinio.GraphCore;
using Platinio.SDK.AnimationEvents;
using Platinio.SDK.DamageSystem;
using Unity.VisualScripting;
using UnityEngine;

namespace Platinio.BehaviourTree
{
    [GraphCreateMenu("Gameplay/Skills/Physical Attack")]
    public class PhysicalAttack : GameplayNode
    {
        [Serialize] [Inspectable] private float m_damage;
        [Serialize] [Inspectable] private string m_stateName = "";
        [Serialize] [Inspectable] private int m_layer = 0;
        [Serialize] [Inspectable] private float m_normalizeTransitionDiration = 0.15f;
        [Serialize] [Inspectable] private float m_normalizeTimeOffset = 0.0f;
        
        public override string NodeName => "Physical Attack";

        private bool m_physicalAttackEnd = false;

        public override void OnEnter()
        {
            m_physicalAttackEnd = false;
            
            PlayAnimation();
            if (!VariableDeclarations.IsDefined("AnimationEventListener")) return;
            
            var animationEventListener = VariableDeclarations.Get<AnimationEventListener>("AnimationEventListener");
            animationEventListener.RegisterEvent("OnPhysicalAttackStart", OnPhysicalAttackStart);
            animationEventListener.RegisterEvent("OnPhysicalAttackEnd", OnPhysicalAttackEnd);
        }

        private void OnPhysicalAttackStart(AnimationEvent animationEvent)
        {
            var entity = VariableDeclarations.Get<IAIEntity>("Entity");
            var physicalWeapons = entity.Weapons;

            foreach (var physicalWeapon in physicalWeapons)
            {
                physicalWeapon.UpdateState(WeaponState.Damage);
                physicalWeapon.OnHit += OnWeaponHit;
            }
        }

        private void OnWeaponHit(RaycastHit raycastHit)
        {
            var damageable = raycastHit.collider.gameObject.GetComponent<Damageable>();
            if (damageable == null) return;

            var damageConfig = new DamageConfig()
            {
                Damage = m_damage,
                DamageType = DamageType.Physical
            };
            
            damageable.DoDamage(new DamageInfo(damageConfig, Machine.gameObject));
        }

        private void OnPhysicalAttackEnd(AnimationEvent animationEvent)
        {
            m_physicalAttackEnd = true;
            
            if (!VariableDeclarations.IsDefined("PhysicalWeapons")) return;

            var physicalWeapons = VariableDeclarations.Get<List<PhysicalWeapon>>("PhysicalWeapons");

            foreach (var physicalWeapon in physicalWeapons)
            {
                physicalWeapon.UpdateState(WeaponState.Idle);
                physicalWeapon.OnHit -= OnWeaponHit;
            }
        }

        private void PlayAnimation()
        {
            if (!VariableDeclarations.IsDefined("Animator")) return;
            var animator = VariableDeclarations.Get<Animator>("Animator");

            animator.CrossFade(m_stateName, m_normalizeTransitionDiration, m_layer, m_normalizeTimeOffset);
        }

        public override ExecutionStatus OnUpdate()
        {
            if (m_physicalAttackEnd) return ExecutionStatus.Success;
            return ExecutionStatus.Running;
        }

        public override void OnExit()
        {
            if (!VariableDeclarations.IsDefined("AnimationEventListener")) return;
            
            var animationEventListener = VariableDeclarations.Get<AnimationEventListener>("AnimationEventListener");
            animationEventListener.UnregisterEvent("OnPhysicalAttackStart", OnPhysicalAttackStart);
            animationEventListener.UnregisterEvent("OnPhysicalAttackEnd", OnPhysicalAttackEnd);
        }
    }
}

