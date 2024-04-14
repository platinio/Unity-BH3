using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace Platinio.BehaviorTree
{
    public static class BehaviorTreeGraphDrawer
    {
        public static List<Rect> DrawTransition(IGraph graph, BehaviorTreeTransitionWidget transitionWidget, WidgetElementState widgetElementState, Vector2 offset, float lineWidth = 2.0f, float minDistanceFromNodeToTransition = 30.0f)
        {
            var oldColor = GUI.color;
            if (transitionWidget.element.destination.IsRunning) GUI.color = Color.green;
            else GUI.color = Color.white;
           
            
            List<Rect> lineRects = new List<Rect>();
            Vector2 destinationCenter = transitionWidget.destinationEdgeCenter;
            Vector2 sourceCenter = transitionWidget.sourceEdgeCenter;

            //draw transition first section
            float closerDestPositionY = GetCloserVerticalNodePosition(graph, transitionWidget).y;
            float verticalDistanceFromSourceToCloserDest = closerDestPositionY - sourceCenter.y;
            float firstSectionHeight = Mathf.Clamp(verticalDistanceFromSourceToCloserDest, minDistanceFromNodeToTransition, float.MaxValue) / 2.0f;
            Vector2 transitionFirstSectionSize = new Vector2(lineWidth, firstSectionHeight);
            Rect firstSectionRect = new Rect(sourceCenter, transitionFirstSectionSize);

            firstSectionRect.position += offset;
         
            GUI.DrawTexture(firstSectionRect, Texture2D.whiteTexture);
            lineRects.Add(firstSectionRect);
            
            //draw transition second section
            float horizontalDistanceFromSourceToCloserDest = Mathf.Abs(sourceCenter.x - destinationCenter.x);
            Vector2 secondSectionHorizontalOffset = destinationCenter.x < sourceCenter.x ? Vector2.right * horizontalDistanceFromSourceToCloserDest : Vector2.zero;
            Vector2 secondSectionPosition = sourceCenter + (Vector2.up * firstSectionHeight) - secondSectionHorizontalOffset;
            
            Vector2 secondSectionSize = new Vector2(horizontalDistanceFromSourceToCloserDest + lineWidth, lineWidth);
            Rect secondSectionRect = new Rect(secondSectionPosition, secondSectionSize);
            
            secondSectionRect.position += offset;
           
            GUI.DrawTexture(secondSectionRect, Texture2D.whiteTexture);
            lineRects.Add(secondSectionRect);
            
            //draw transition third section
            float sourceDestCenterDiffLineEdge = (sourceCenter.y - destinationCenter.y) - (minDistanceFromNodeToTransition / 2.0f);
            float thisSectionVerticalOffset = destinationCenter.y < sourceCenter.y ? sourceDestCenterDiffLineEdge : firstSectionHeight;
            Vector2 sourceDestVerticalDir = destinationCenter.y < sourceCenter.y ? Vector2.down : Vector2.up;
            Vector2 thirdSectionPositionOffset = sourceDestVerticalDir * thisSectionVerticalOffset;
            
            Vector2 sourceDestHorizontalDirection = destinationCenter.x < sourceCenter.x? Vector2.right : Vector2.left;
            Vector2 thirdSectionHorizontalOffset = sourceDestHorizontalDirection * horizontalDistanceFromSourceToCloserDest;
            Vector2 thirdSectionPosition = sourceCenter + thirdSectionPositionOffset - thirdSectionHorizontalOffset;
            
            //calculate last section size
            float upperVerticalPosition = (sourceCenter.y + closerDestPositionY) / 2.0f;
            float minUpperVerticalPosition = sourceCenter.y + (minDistanceFromNodeToTransition / 2.0f);
            
            if (upperVerticalPosition < minUpperVerticalPosition) upperVerticalPosition = minUpperVerticalPosition;
            
            float lastSectionHeight = Mathf.Abs(upperVerticalPosition - destinationCenter.y);
            if (destinationCenter.y < sourceCenter.y) lastSectionHeight -= (minDistanceFromNodeToTransition / 2.0f);

            Rect thirdSectionRect = new Rect(thirdSectionPosition, new Vector2(lineWidth, lastSectionHeight));
            
            thirdSectionRect.position += offset;
           
            GUI.DrawTexture(thirdSectionRect, Texture2D.whiteTexture);
            lineRects.Add(thirdSectionRect);

            GUI.color = oldColor;
            return lineRects;
        }

        private static Vector2 GetCloserVerticalNodePosition(IGraph graph, BehaviorTreeTransitionWidget transitionWidget)
        {
            Vector2 closerDestinationPosition = transitionWidget.destinationEdgeCenter;
            
            foreach (var graphElement in graph.elements)
            {
                if (graphElement is BehaviorTreeTransition transition)
                {
                    if (transition.source == transitionWidget.element.source)
                    {
                        Vector2 destPosition = transition.destination.Position.GetEdgeCenter(transitionWidget.DestinationEdge);
                        if (destPosition.y < closerDestinationPosition.y) closerDestinationPosition = destPosition;
                    }
                }
            }

            return closerDestinationPosition;
        }
    }

    public struct WidgetElementState
    {
        public bool IsHover;
        public bool IsActive;
        public bool HasKeyboardFocus;
        public bool On;
    }
}