using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Platinio.BehaviorTree
{
   /*
    * There is a problem in the visual scripting package trying to open a behavior tree with their version of GraphWindow
    * Because of this I have to decrease their OnOpenAsset from Int32.MinValue to Int32.MinValue + 1 so Behavior Trees have a chance
    * to open their own editor windows, this code goes to the package cache and modify the script directly
    */
    
    [InitializeOnLoad]
    public class PatchVisualScripting
    {
        static PatchVisualScripting()
        {
            StartVisualScriptingPatchLoop();
        }

        private static void StartVisualScriptingPatchLoop()
        {
            try
            {
                UpdateVisualScriptingOnOpenAssetOrder();
            }
            catch (Exception e)
            {
                Debug.LogError($"Unable to patch visual scripting package, there may be problems when trying to open a behavior treee asset Exception: {e}");
            }
           
            
            AssemblyReloadEvents.afterAssemblyReload -= StartVisualScriptingPatchLoop;
            AssemblyReloadEvents.afterAssemblyReload += StartVisualScriptingPatchLoop;
        }

        private static void UpdateVisualScriptingOnOpenAssetOrder()
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
                    
                    Debug.Log("Visual scripting patched!");
                }
            }
        }
    }
}