using System;
using ArcaneOnyx.Share;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Sample
{
    public struct ShootInfo : ICloneable
    {
        public Vector3 dir;
        public float dmg;
        public float speed;
        public float range;
        public float hitForce;
        public float gravity;
        public int maxBounceCount;
        public LayerMask hitLayer;
        public Action<Damageable> hitCallback;
        public GameObject hitEffect;
        public GameEntity sender;

        public object Clone()
        {
            ShootInfo clone = new ShootInfo();
            clone.dir = dir;
            clone.dmg = dmg;
            clone.speed = speed;
            clone.range = range;
            clone.hitForce = hitForce;
            clone.gravity = gravity;
            clone.hitLayer = hitLayer;
            clone.hitCallback = hitCallback;
            clone.hitEffect = hitEffect;
            clone.sender = sender;

            return clone;
        }
    }
}