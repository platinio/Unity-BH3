using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Platinio.BehaviorTree
{
    [InitializeOnLoad]
    public class UpdateVisualScriptingOpenAssetPriority
    {
        static UpdateVisualScriptingOpenAssetPriority()
        {
            StartVisualScriptingPatchLoop();
        }

        private static void StartVisualScriptingPatchLoop()
        {
            try
            {
                PatchVisualScripting();
            }
            catch (Exception e)
            {
                Debug.LogError($"Unable to patch visual scripting package, there may be problems when trying to open a behavior treee asset Exception: {e}");
            }
           
            
            AssemblyReloadEvents.afterAssemblyReload -= StartVisualScriptingPatchLoop;
            AssemblyReloadEvents.afterAssemblyReload += StartVisualScriptingPatchLoop;
        }
        
        private static void PatchVisualScripting()
        {
            string partialName = "com.unity.visualscripting@";
            string packageCachePath = Application.dataPath + "/../Library/PackageCache";

            DirectoryInfo packageCache = new DirectoryInfo(packageCachePath);
           
            DirectoryInfo[] dirsInDir = packageCache.GetDirectories("*" + partialName + "*.*");
            
            foreach (DirectoryInfo foundDir in dirsInDir )
            {
                string scriptPath = packageCachePath + $"/{foundDir.Name}/Editor/VisualScripting.Core/Inspection/Inspector.cs";

                if (File.Exists(scriptPath))
                {
                    string script = File.ReadAllText(scriptPath);
                    if (script.Contains("[OnOpenAsset(Int32.MinValue + 1)]")) return;
                    
                    script = script.Replace("[OnOpenAsset(Int32.MinValue)]", "[OnOpenAsset(Int32.MinValue + 1)]");
                    File.WriteAllText(scriptPath, script);
                }
            }
        }
    }
}