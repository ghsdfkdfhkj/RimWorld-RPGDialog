using UnityEngine;
using Verse;

namespace RPGDialog
{
    public static class SkipButtonHandler
    {
        private static bool skipRequested = false;
        private static float cachedButtonWidth = -1f;

        public static float DrawSkipButton(float rightX, float y, ref bool isSkipped)
        {
            if (cachedButtonWidth < 0f)
            {
                cachedButtonWidth = UIStyles.MeasureTextWidth(UIStyles.ButtonStyle, "SKIP") + 20f;
            }
            Rect rect = new Rect(rightX - cachedButtonWidth, y, cachedButtonWidth, 30f);

            var originalColor = GUI.color;
            if (isSkipped)
            {
                GUI.color = new Color(0.5f, 0.5f, 0.5f);
            }

            if (GUI.Button(rect, "SKIP", UIStyles.ButtonStyle) && !isSkipped)
            {
                skipRequested = true;
                isSkipped = true;
            }

            GUI.color = originalColor;
            return cachedButtonWidth;
        }

        public static bool IsSkipRequested()
        {
            if (skipRequested)
            {
                skipRequested = false; // Reset after check
                return true;
            }
            return false;
        }
    }
}
