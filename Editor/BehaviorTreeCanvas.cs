using System;
using System.Collections.Generic;
using System.Linq;
using Platinio.GraphCore;
using Unity.VisualScripting;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Platinio.BehaviorTree
{
    [Canvas(typeof(BehaviorTreeGraph))]
    public class BehaviorTreeCanvas : BaseCanvas<BehaviorTreeGraph, BehaviorTreeNode, BehaviorTreeTransition>
    {
        public BehaviorTreeCanvas(BehaviorTreeGraph graph) : base(graph) { }

        public static BehaviorTreeCanvas OpenBehaviorTreeCanvas;
        public static BehaviorTreeGraphAsset OpenBehaviorTreeGraphAsset;
        
        public Vector2 connectionEnd { get; set; }
        public bool isCreatingConnection => connectionSource != null &&
                                            connectionSource.behaviorTreeNode != null;        
        public IBehaviorTreePort connectionSource { get; set; }
        
        
        protected override void HandleHighPriorityInput()
        {
            if (isCreatingConnection)
            {
                if (e.IsMouseDown(MouseButton.Left))
                {
                    connectionEnd = mousePosition;
                    NewUnitContextual();
                    e.Use();
                }
                else if (e.IsFree(EventType.KeyDown) && e.keyCode == KeyCode.Escape)
                {
                    CancelConnection();
                    e.Use();
                }
            }

            base.HandleHighPriorityInput();
        }
        
        public void CancelConnection()
        {
            connectionSource = null;
        }
        
        public void NewUnitContextual()
        {
            var filter = UnitOptionFilter.Any;
            filter.GraphHashCode = graph.GetHashCode();

            if (connectionSource is BehaviorTreeValueInput)
            {
                var valueInput = (BehaviorTreeValueInput)connectionSource;
                filter.CompatibleOutputType = valueInput.type;
                filter.Expose = false;
                filter.NoConnection = false;
                NewUnit(mousePosition, GetNewUnitOptions(filter), (unit) => CompleteContextualConnection(valueInput, unit.CompatibleValueOutput(valueInput.type)));
            }
            else if (connectionSource is BehaviorTreeValueOutput)
            {
                var valueOutput = (BehaviorTreeValueOutput)connectionSource;
                filter.CompatibleInputType = valueOutput.type;
                filter.NoConnection = false;
                NewUnit(mousePosition, GetNewUnitOptions(filter), (unit) => CompleteContextualConnection(valueOutput, unit.CompatibleValueInput(valueOutput.type)));
            }
            else if (connectionSource is BehaviorTreeControlInput)
            {
                var controlInput = (BehaviorTreeControlInput)connectionSource;
                filter.NoControlOutput = false;
                filter.NoConnection = false;
                NewUnit(mousePosition, GetNewUnitOptions(filter), (unit) => CompleteContextualConnection(controlInput, unit.controlOutputs.First()));
            }
            else if (connectionSource is BehaviorTreeControlOutput)
            {
                var controlOutput = (BehaviorTreeControlOutput)connectionSource;
                filter.NoControlInput = false;
                filter.NoConnection = false;
                NewUnit(mousePosition, GetNewUnitOptions(filter), (unit) => CompleteContextualConnection(controlOutput, unit.controlInputs.First()));
            }
        }
        
        private void NewUnit(Vector2 unitPosition, UnitOptionTree options, Action<IBehaviorTreeNode> then = null)
        {
            delayCall += () =>
            {
                var activatorPosition = new Rect(e.mousePosition, new Vector2(200, 1));

                var context = this.context;

                LudiqGUI.FuzzyDropdown
                (
                    activatorPosition,
                    options,
                    null,
                    delegate (object _option)
                    {
                        context.BeginEdit();
                        if (_option is IUnitOption)
                        {
                            var option = (IUnitOption)_option;
                            var unit = option.InstantiateUnit();
                            AddUnit(unit, unitPosition);
                            option.PreconfigureUnit(unit);
                            //then?.Invoke(unit);
                            GUI.changed = true;
                        }
                        else
                        {
                            if ((Type)_option == typeof(StickyNote))
                            {
                                NewSticky(unitPosition);
                            }
                        }

                        context.EndEdit();
                    }
                );
            };
        }
        
        private UnitOptionTree GetNewUnitOptions(UnitOptionFilter filter)
        {
            var options = new UnitOptionTree(new GUIContent("Node"));

            options.filter = filter;
            options.reference = reference;

            if (filter.CompatibleOutputType == typeof(object))
            {
                options.surfaceCommonTypeLiterals = true;
            }

            return options;
        }
        
        public void AddUnit(IUnit unit, Vector2 position)
        {
            UndoUtility.RecordEditedObject("Create Node");
            unit.guid = Guid.NewGuid();
            unit.position = position.PixelPerfect();
            //graph.units.Add(unit);
            selection.Select(unit);
            GUI.changed = true;
        }
        
        private void CompleteContextualConnection(IBehaviorTreePort source, IBehaviorTreePort destination)
        {
            source.ValidlyConnectTo(destination);
            Cache();
            var unitPosition = this.Widget<IBehaviorTreeWidget>(destination.behaviorTreeNode).position.position;
            var portPosition = this.Widget<IBehaviorTreePortWidget>(destination).handlePosition.center.PixelPerfect();
            var offset = portPosition - unitPosition;
            destination.behaviorTreeNode.position -= offset;
            this.Widget(destination.behaviorTreeNode).Reposition();
            connectionSource = null;
            GUI.changed = true;
        }
        
        
        
        
        
        
        
        
        
        
        
        
        
        
        
        
        
        
        
        
        
        
        [OnOpenAsset(int.MinValue)]
        public static bool OnOpenAsset(int instanceID, int line)
        {
            UnityObject obj = EditorUtility.InstanceIDToObject(instanceID);
            if (!(obj is BehaviorTreeGraphAsset)) return false;
            
            GraphReference reference = null;
            if (obj is IMacro macro)
                reference = GraphReference.New(macro, true);
            else if (obj is IGraphRoot root)
                reference = GraphReference.New(root, false);
            if (obj is IGraphNesterElement nesterElement)
                reference = LudiqGraphsEditorUtility.editedContext.value.reference.ChildReference(nesterElement, false);
            if (reference == null)
                return false;
            GraphCore.GraphWindow.OpenActive(reference);
            return true;
        }
        
        protected override IEnumerable<Type> GetValidNodes() => new List<Type>()
        {
            typeof(Composite),
            typeof(ContainerNode),
            typeof(Decorator),
            typeof(GameplayNode),
            typeof(Condition)
        };

        public override void Open()
        {
            base.Open();
            
            OpenBehaviorTreeCanvas = this;
        }

        public static BehaviorTreeGraphAsset GetBehaviorTreeGraphAsset()
        {
            if (OpenBehaviorTreeCanvas == null || OpenBehaviorTreeCanvas.context == null) return null;
        
            var gameObject = OpenBehaviorTreeCanvas.context.reference.gameObject;
            if (gameObject != null)
            {
                return gameObject.GetComponent<BehaviorTreeMachine>().GraphAsset;
            }

            return OpenBehaviorTreeCanvas.context.reference.scriptableObject as BehaviorTreeGraphAsset;
        }

        public static BehaviorTreeMachine GetSelectedBehaviorTreeMachine()
        {
            if (OpenBehaviorTreeCanvas == null || OpenBehaviorTreeCanvas.context == null) return null;
            
            var gameObject = OpenBehaviorTreeCanvas.context.reference.gameObject;
            if (gameObject != null)
            {
                return gameObject.GetComponent<BehaviorTreeMachine>();
            }

            return null;
        }
    }
}
