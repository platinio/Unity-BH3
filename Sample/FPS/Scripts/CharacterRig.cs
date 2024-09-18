using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace ArcaneOnyx.BehaviorTree.Sample
{
    public class CharacterRig : MonoBehaviour
    {
        [SerializeField] private List<RigKeyValuePair> rigKeyValuePairList;

        private Dictionary<string, Rig> rigDict;
        private Coroutine rigEnableRoutine;
        private Coroutine rigDisableRoutine;
        private string activeRigKey = null;

        private void Awake()
        {
            rigDict = new Dictionary<string, Rig>();
            foreach (var rigKeyValuePair in rigKeyValuePairList)
            {
                if (rigDict.ContainsKey(rigKeyValuePair.Key)) continue;
                rigDict.Add(rigKeyValuePair.Key, rigKeyValuePair.Rig);
            }
        }
        
        public void DisableActiveRig(float t = 0.25f)
        {
            if (string.IsNullOrEmpty(activeRigKey)) return;

            DisableRig(activeRigKey, t);
            activeRigKey = null;
        }

        public void EnableRig(string key, float t = 0.25f)
        {
            if (!rigDict.ContainsKey(key))
            {
                Debug.LogError($"can't find rig with key {key}");
                return;
            }

            if (string.IsNullOrEmpty(key))
            {
                Debug.LogError("Invalid rig name");
                return;
            }

            //rig is already enable or on his way to be activated
            if (activeRigKey == key) return;

            if (rigDisableRoutine != null) StopCoroutine(rigDisableRoutine);
            rigDisableRoutine = null;

            rigDisableRoutine = StartCoroutine(LerpRigWeight(activeRigKey, 1.0f, 0.0f, t));

            if (rigEnableRoutine != null) StopCoroutine(rigEnableRoutine);
            rigEnableRoutine = null;

            rigEnableRoutine = StartCoroutine(LerpRigWeight(key, 0.0f, 1.0f, t));
            
            activeRigKey = key;
        }

        private IEnumerator LerpRigWeight(string key, float from, float to, float t)
        {
            if (string.IsNullOrEmpty(key)) yield break;

            float lerp = 0;
            float currentTime = 0;

            while (lerp <= 1)
            {
                float w = Mathf.Lerp(from, to, lerp);
                SetRigWeightNow(key, w);
                
                currentTime += Time.deltaTime;
                lerp = currentTime / t;
              
                yield return null;
            }
            
            SetRigWeightNow(key, to);
        }

        public void DisableRig(string key, float t = 0.25f)
        {
            if (rigDisableRoutine != null) StopCoroutine(rigDisableRoutine);
            rigDisableRoutine = null;
        }

        private void SetRigWeightNow(string key, float v)
        {
            if (rigDict.TryGetValue(key, out var rig))
            {
                rig.weight = v;
            }
        }
    }

    [Serializable]
    public class RigKeyValuePair
    {
        public string Key;
        public Rig Rig;
    }
}

