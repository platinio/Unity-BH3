using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    [InitializeOnLoad]
    public class SetHandleClipboardVirtual
    {
        static SetHandleClipboardVirtual()
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
                string scriptPath = packageCachePath + $"/{foundDir.Name}/Editor/VisualScripting.Core/Canvases/VisualScriptingCanvas.cs";

                if (File.Exists(scriptPath))
                {
                    string script = File.ReadAllText(scriptPath);
                    if (script.Contains("protected virtual void HandleClipboard()")) return;
                    
                    script = script.Replace("private void HandleClipboard()", "protected virtual void HandleClipboard()");
                    File.WriteAllText(scriptPath, script);
                }
            }
        }
    }
}