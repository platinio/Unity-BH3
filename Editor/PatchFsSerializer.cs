using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.VisualScripting.FullSerializer;
using UnityEditor;
using UnityEngine;

namespace Platinio.BehaviorTree
{
    [InitializeOnLoad]
    public class PatchFsSerializer
    {
        static PatchFsSerializer()
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
                string scriptPath = packageCachePath + $"/{foundDir.Name}/Runtime/VisualScripting.Core/Dependencies/FullSerializer/fsSerializer.cs";

                if (File.Exists(scriptPath))
                {
                    string script = File.ReadAllText(scriptPath);
                    if (script.Contains("BehaviorTree_Type_MissingType")) return;
                    
                    script = script.Replace("internal static readonly string TypeName_MissingType = \"Unity.VisualScripting.MissingType\";",
                        "internal static readonly string TypeName_MissingType = \"Unity.VisualScripting.MissingType\"; \n" +
                        "internal static readonly string BehaviorTree_TypeName_MissingType = \"Platinio.BehaviorTree.MissingType\";");
                    
                    
                    script = script.Replace("static readonly Type Type_MissingType = RuntimeCodebase.DeserializeType(TypeName_MissingType);",
                        "static readonly Type Type_MissingType = RuntimeCodebase.DeserializeType(TypeName_MissingType); \n" +
                        "static readonly Type BehaviorTree_Type_MissingType = RuntimeCodebase.DeserializeType(BehaviorTree_TypeName_MissingType);");

                    script = script.Replace("if (IsVisualScriptingUnit(data))\n" +
                                            "                {\n" +
                                            "                    " +
                                            "//We store a copy of the node as a string in the hopes of being able to re-instantiate it later.\n" +
                                            "                    dict[Key_UnitFormerValue] = new fsData(data.ToString());\n\n" +

                                            "                    // We store the type that the unit should be, we will try to re-instantiate it if it becomes available again.\n" +
                                            "                    dict[Key_UnitFormerType] = typeNameData;\n" +
                                            "                    dict[Key_InstanceType] = new fsData(TypeName_MissingType);\n\n" +

                                            "                    // TODO: Ideally this would display as an error in the console instead of a warning. Using fsResult.Fail() aborts the deserialization.\n" +
                                            "                    deserializeResult += fsResult.Warn($\"Type definition for '{typeName}' is missing.\\nConverted '{typeName}' unit to '{TypeName_MissingType}'. Did you delete the type's script file?\");\n\n" +
                                            "                    return Type_MissingType;\n" +
                                            "                }", "if (IsVisualScriptingUnit(data))\n" +
                                                                 "                {\n" +
                                                                 "                    //We store a copy of the node as a string in the hopes of being able to re-instantiate it later.\n" +
                                                                 "                    dict[Key_UnitFormerValue] = new fsData(data.ToString());\n\n" +
                                                                 "" +
                                                                 "                    // We store the type that the unit should be, we will try to re-instantiate it if it becomes available again.\n" +
                                                                 "                    dict[Key_UnitFormerType] = typeNameData;\n" +
                                                                 "                    dict[Key_InstanceType] =  data.ToString().Contains(\"Platinio.BehaviorTree\")? new fsData(BehaviorTree_TypeName_MissingType) : new fsData(TypeName_MissingType);\n\n" +
                                                                 "" +
                                                                 "                    // TODO: Ideally this would display as an error in the console instead of a warning. Using fsResult.Fail() aborts the deserialization.\n" +
                                                                 "                    deserializeResult += fsResult.Warn($\"Type definition for '{typeName}' is missing.\\nConverted '{typeName}' unit to '{TypeName_MissingType}'. Did you delete the type's script file?\");\n\n" +
                                                                 "" +
                                                                 "                    return data.ToString().Contains(\"Platinio.BehaviorTree\")? BehaviorTree_Type_MissingType: Type_MissingType;\n" +
                                                                 "                }");

                    script = script.Replace("if (IsVisualScriptingUnit(data))\n" +
                                            "                {\n" +
                                            "                    // We store the type that the unit should be, we will try to re-instantiate it if it becomes valid again.\n" +
                                            "                    dict[Key_UnitFormerType] = typeNameData;\n" +
                                            "                    dict[Key_InstanceType] = new fsData(TypeName_MissingType);\n\n" +
                                            "" +
                                            "                    // TODO: Ideally this would display as an error in the console instead of a warning. Using fsResult.Fail() aborts the deserialization.\n" +
                                            "                    deserializeResult += fsResult.Warn($\"Type '{typeName}' is no longer assignable to '{defaultType.FullName}'. Did you remove inheritance from '{TypeName_Unit}'?\\nConverted '{typeName}' unit to '{TypeName_MissingType}'.\");\n\n" +
                                            "" +
                                            "                    return Type_MissingType;\n" +
                                            "                }", "if (IsVisualScriptingUnit(data))\n" +
                                                                 "                {\n" +
                                                                 "                    // We store the type that the unit should be, we will try to re-instantiate it if it becomes valid again.\n" +
                                                                 "                    dict[Key_UnitFormerType] = typeNameData;\n" +
                                                                 "                    dict[Key_InstanceType] = typeNameData.ToString().Contains(\"Platinio.BehaviorTree\")? new fsData(BehaviorTree_TypeName_MissingType) : new fsData(TypeName_MissingType);\n\n" +
                                                                 "" +
                                                                 "                    // TODO: Ideally this would display as an error in the console instead of a warning. Using fsResult.Fail() aborts the deserialization.\n" +
                                                                 "                    deserializeResult += fsResult.Warn($\"Type '{typeName}' is no longer assignable to '{defaultType.FullName}'. Did you remove inheritance from '{TypeName_Unit}'?\\nConverted '{typeName}' unit to '{TypeName_MissingType}'.\");\n\n" +
                                                                 "" +
                                                                 "                    return typeNameData.ToString().Contains(\"Platinio.BehaviorTree\")? BehaviorTree_Type_MissingType: Type_MissingType;;\n" +
                                                                 "                }");
                    
                    File.WriteAllText(scriptPath, script);
                   
                    
                    Debug.Log("Visual scripting patched!");
                }
            }
        }
    }

}

