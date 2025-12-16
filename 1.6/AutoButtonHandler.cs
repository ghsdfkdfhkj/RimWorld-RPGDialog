using UnityEngine;
using Verse;
using System.Collections.Generic;

namespace RPGDialog
{
    public static class AutoButtonHandler
    {
        private static bool autoEnabled = false;
        private static float autoPageTurnTime = -1f;
        
        // --- Hyper-Optimized Trail effect data using a Circular Array ---
        private struct TrailPoint { public Vector2 position; public float creationTime; }
        private const int MaxTrailPoints = 120; // Sufficient for 1s trail at 60fps * 2 points/frame
        private static readonly TrailPoint[] trailPoints = new TrailPoint[MaxTrailPoints];
        private static int currentTrailIndex = 0;
        
        private const float TrailDuration = 0.2f; // Reduced from 0.35f for a shorter trail
        private const float RotationSpeed = 400f;
        private const float SquareSize = 3f;
        private static float cachedButtonWidth = -1f;

        public static float DrawAutoButton(float rightX, float y)
        {
            if (cachedButtonWidth < 0)
            {
                cachedButtonWidth = UIStyles.MeasureTextWidth(UIStyles.ButtonStyle, "AUTO") + 20f;
            }
            Rect rect = new Rect(rightX - cachedButtonWidth, y, cachedButtonWidth, 30f);

            var originalColor = GUI.color;
            if (!autoEnabled)
            {
                GUI.color = new Color(0.5f, 0.5f, 0.5f);
            }

            if (GUI.Button(rect, "AUTO", UIStyles.ButtonStyle))
            {
                autoEnabled = !autoEnabled;
                if (!autoEnabled)
                {
                    autoPageTurnTime = -1f;
                    // Clear the array to prevent stale particles on re-enable
                    System.Array.Clear(trailPoints, 0, MaxTrailPoints);
                }
            }

            GUI.color = originalColor;

            if (autoEnabled && Event.current.type == EventType.Repaint)
            {
                float currentTime = Time.realtimeSinceStartup;

                // 1. Add two new points by overwriting old ones in the circular array
                float perimeter = (rect.width + rect.height) * 2f;
                float distance = (currentTime * (RotationSpeed / 2f)) % perimeter;

                Vector2 GetPointOnRect(float d)
                {
                    if (d < rect.width) return new Vector2(rect.xMin + d, rect.yMin);
                    d -= rect.width;
                    if (d < rect.height) return new Vector2(rect.xMax, rect.yMin + d);
                    d -= rect.height;
                    if (d < rect.width) return new Vector2(rect.xMax - d, rect.yMax);
                    d -= rect.width;
                    return new Vector2(rect.xMin, rect.yMax - d);
                }

                // Add point 1
                trailPoints[currentTrailIndex].position = GetPointOnRect(distance);
                trailPoints[currentTrailIndex].creationTime = currentTime;
                currentTrailIndex = (currentTrailIndex + 1) % MaxTrailPoints;

                // Add point 2
                trailPoints[currentTrailIndex].position = GetPointOnRect((distance + perimeter / 2f) % perimeter);
                trailPoints[currentTrailIndex].creationTime = currentTime;
                currentTrailIndex = (currentTrailIndex + 1) % MaxTrailPoints;

                // 2. Draw all valid points in the array
                Color trailColor = Color.white;
                for (int i = 0; i < MaxTrailPoints; i++)
                {
                    var point = trailPoints[i];
                    float age = currentTime - point.creationTime;

                    // Skip drawing if the point is too old or uninitialized (creationTime == 0)
                    if (age > TrailDuration) continue;
                    
                    trailColor.a = 1f - (age / TrailDuration);
                    GUI.color = trailColor;

                    Rect squareRect = new Rect(point.position.x - SquareSize / 2, point.position.y - SquareSize / 2, SquareSize, SquareSize);
                    GUI.DrawTexture(squareRect, BaseContent.WhiteTex);
                }
                GUI.color = originalColor;
            }
            
            return cachedButtonWidth;
        }

        public static void Disable()
        {
            if (autoEnabled)
            {
                autoEnabled = false;
                autoPageTurnTime = -1f;
                System.Array.Clear(trailPoints, 0, MaxTrailPoints);
            }
        }

        public static bool IsAutoEnabled()
        {
            return autoEnabled;
        }

        public static void Update(bool isTyping, bool isLastPage)
        {
            if (!autoEnabled || isTyping)
            {
                autoPageTurnTime = -1f; // Reset timer if typing or disabled
                return;
            }

            if (autoPageTurnTime < 0)
            {
                autoPageTurnTime = Time.realtimeSinceStartup + 2.0f; // Set timer for 2.0 seconds delay
            }
        }

        public static bool ShouldTurnPage()
        {
            if (autoEnabled && autoPageTurnTime > 0 && Time.realtimeSinceStartup > autoPageTurnTime)
            {
                autoPageTurnTime = -1f; // Reset timer
                return true;
            }
            return false;
        }
    }
}
