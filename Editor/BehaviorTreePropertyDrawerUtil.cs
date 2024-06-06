using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine.SceneManagement;

namespace Platinio.BehaviorTree
{
    public static class BehaviorTreePropertyDrawerUtil
    {
        public static List<VariableDeclaration> GetVariableDeclarations(Type validType = null)
        {
            List<VariableDeclaration> variableDeclarations = new();

            var selectedMachine = BehaviorTreeCanvas.GetSelectedBehaviorTreeMachine();
            if (selectedMachine != null)
            {
                AddVariableDeclarations(variableDeclarations, selectedMachine.Variables.declarations);
            }
            
            var graphAsset = BehaviorTreeCanvas.GetBehaviorTreeGraphAsset();
            if (graphAsset != null)
            {
                AddVariableDeclarations(variableDeclarations, graphAsset.declarations);
            }

            AddVariableDeclarations(variableDeclarations, Variables.Scene(SceneManager.GetActiveScene()));
            AddVariableDeclarations(variableDeclarations, Variables.Application);
            AddVariableDeclarations(variableDeclarations, Variables.Saved);

            return variableDeclarations;
        }

        private static void AddVariableDeclarations(List<VariableDeclaration> variableDeclarationList, VariableDeclarations variableDeclarations, Type validType = null)
        {
            if (variableDeclarations == null) return;
            
            var variablesEnumerator = variableDeclarations.GetEnumerator();

            while (variablesEnumerator.MoveNext())
            {
                var current = variablesEnumerator.Current;
                string typeHandleIdetification = current.typeHandle.Identification;
                
                if (current == null || (validType != null && validType.AssemblyQualifiedName != typeHandleIdetification)) continue;
                
                if (variableDeclarationList.Where(x => x.name == current.name).FirstOrDefault() != null) continue;
                
                variableDeclarationList.Add(current);
            }
        }
    }
}