using System.Collections.Generic;
using System.IO;
using Unity.VisualScripting;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    public class ScriptGraphAssetsRepository : ScriptableObject
    {
        private const string DirectoryPath = "Assets/BehaviorTree.Generated";
        private const string AssetsPath = "Assets/BehaviorTree.Generated/ScriptGraphAssetsRepository.asset";
        
        public static ScriptGraphAssetsRepository Instance
        {
            get
            {
                #if UNITY_EDITOR
                if (instance == null)
                {
                    instance = AssetDatabase.LoadAssetAtPath<ScriptGraphAssetsRepository>(AssetsPath);
                    if (instance == null)
                    {
                        if (!Directory.Exists(DirectoryPath)) Directory.CreateDirectory(DirectoryPath);
                        
                        instance = CreateInstance(typeof(ScriptGraphAssetsRepository)) as ScriptGraphAssetsRepository;
                        AssetDatabase.CreateAsset(instance, AssetsPath);
                    }
                }

                return instance;
                #else
                return null;
                #endif
            }
        }

        private static ScriptGraphAssetsRepository instance;

        [SerializeField, HideInInspector] private List<ScriptGraphAssetKeyValuePair> repository = new();
        [SerializeField, HideInInspector] private List<ScriptGraphAsset> uniqueAssets = new();

        private static Dictionary<Object, string> discoveredAssetGuid = new();
        
        public void RemoveInvalid()
        {
            for (int i = repository.Count - 1; i >= 0; i--)
            {
                if (repository[i].Value == null) repository.RemoveAt(i);
            }

            for (int i = uniqueAssets.Count - 1; i >= 0; i--)
            {
                if (uniqueAssets[i] == null) uniqueAssets.RemoveAt(i);
            }
        }

        private string GetAssetGuid(Object asset)
        {
#if UNITY_EDITOR
            if (discoveredAssetGuid.TryGetValue(asset, out var guid)) return guid;
            
            string key = GlobalObjectId.GetGlobalObjectIdSlow(asset).assetGUID.ToString();
            discoveredAssetGuid[asset] = key;
            return key;
#else
            return null;
#endif
        }

        public List<ScriptGraphAsset> GetScriptGraphAssets(Object asset)
        {
            string key = GetAssetGuid(asset);
            List<ScriptGraphAsset> result = new();
            
            foreach (var scriptGraphAssetKeyValuePair in repository)
            {
                if (scriptGraphAssetKeyValuePair.Key == key)
                {
                    result.Add(scriptGraphAssetKeyValuePair.Value);
                }
            }

            return result;
        }

        public void AddScriptGraphAsset(Object asset, ScriptGraphAsset scriptGraphAsset)
        {
            if (uniqueAssets.Contains(scriptGraphAsset)) return;
            
            string key = GetAssetGuid(asset);
            
            uniqueAssets.Add(scriptGraphAsset);
            repository.Add(new ScriptGraphAssetKeyValuePair(key, scriptGraphAsset));
            
            #if UNITY_EDITOR
            EditorUtility.SetDirty(this);
            #endif
        }

        public void RemoveScriptGraphAsset(ScriptGraphAsset scriptGraphAsset)
        {
            uniqueAssets.Remove(scriptGraphAsset);
            
            for (int i = repository.Count - 1; i >= 0; i--)
            {
                var scriptGraphAssetKeyValuePair = repository[i];
                if (scriptGraphAssetKeyValuePair.Value == scriptGraphAsset)
                {
                    repository.RemoveAt(i);
                    #if UNITY_EDITOR
                    EditorUtility.SetDirty(this);
                    #endif
                    return;
                }
            }
        }
    }

    [System.Serializable]
    public class ScriptGraphAssetKeyValuePair
    {
        [SerializeField] public string Key;
        [SerializeField] public ScriptGraphAsset Value;

        public ScriptGraphAssetKeyValuePair(string key, ScriptGraphAsset value)
        {
            Key = key;
            Value = value;
        }
    }

}

