using System.Collections.Generic;
using System.IO;
using Unity.VisualScripting;
using UnityEditor;
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
            }
        }

        private static ScriptGraphAssetsRepository instance;

        [SerializeField, HideInInspector] private List<ScriptGraphAssetKeyValuePair> repository = new();
        [SerializeField, HideInInspector] private List<ScriptGraphAsset> uniqueAssets = new();

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

        public List<ScriptGraphAsset> GetScriptGraphAssets(string key)
        {
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

        public void AddScriptGraphAsset(string key, ScriptGraphAsset scriptGraphAsset)
        {
            if (uniqueAssets.Contains(scriptGraphAsset)) return;
            
            uniqueAssets.Add(scriptGraphAsset);
            repository.Add(new ScriptGraphAssetKeyValuePair(key, scriptGraphAsset));
            
            EditorUtility.SetDirty(this);
        }

        public void RemoveScriptGraphAsset(string key, ScriptGraphAsset scriptGraphAsset)
        {
            uniqueAssets.Remove(scriptGraphAsset);
            
            for (int i = repository.Count - 1; i >= 0; i--)
            {
                var scriptGraphAssetKeyValuePair = repository[i];
                if (scriptGraphAssetKeyValuePair.Value == scriptGraphAsset)
                {
                    repository.RemoveAt(i);
                    EditorUtility.SetDirty(this);
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

