
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RPGDialog
{
    [HarmonyPatch(typeof(Window), "WindowOnGUI")]
    public static class TradePortraits
    {
        private static GUIStyle nameStyle;

        private static void InitStyles()
        {
            if (nameStyle == null)
            {
                nameStyle = new GUIStyle(Text.CurFontStyle)
                {
                    fontSize = 24,
                    alignment = TextAnchor.MiddleCenter
                };
            }
        }

        private static void Postfix(Window __instance)
        {
            // We only want to draw portraits when the Trade window is being drawn.
            if (!(__instance is Dialog_Trade tradeWindow))
            {
                return;
            }

            // Proceed with drawing the portraits since the trade window is active.
            InitStyles();
            if (RPGDialogMod.settings.showTraderPortrait)
            {
                DrawTraderPortrait(tradeWindow.windowRect);
            }

            if (RPGDialogMod.settings.showNegotiatorPortrait)
            {
                DrawNegotiatorPortrait(tradeWindow.windowRect);
            }
        }

        private static void DrawTraderPortrait(Rect windowRect)
        {
            Pawn trader = TradeSession.trader as Pawn;
            if (trader == null) return;

            float portraitWidth = 150f;
            float portraitHeight = 200f;
            float padding = 10f;
            float nameLabelHeight = 35f;
            float gap = 5f;
            float verticalOffset = 50f; // Offset from the top of the trade window

            float containerWidth = portraitWidth + padding * 2;
            float containerHeight = portraitHeight + padding * 2 + nameLabelHeight + gap;

            float yPos = windowRect.y + verticalOffset;
            Rect containerRect = new Rect(windowRect.xMax + 10, yPos, containerWidth, containerHeight);

            // Draw window-like background
            Widgets.DrawWindowBackground(containerRect);
            
            // Draw portrait
            Rect portraitRect = new Rect(containerRect.x + padding, containerRect.y + padding, portraitWidth, portraitHeight);
            GUI.DrawTexture(portraitRect, PortraitsCache.Get(trader, new Vector2(portraitWidth, portraitHeight), Rot4.South, default, 1f, true, true, true, true));
            
            // Draw trader name (inside container, below portrait)
            Rect nameRect = new Rect(containerRect.x, portraitRect.yMax + gap, containerWidth, nameLabelHeight);
            
            GUI.Label(nameRect, trader.Name.ToStringShort, nameStyle);
        }

        private static void DrawNegotiatorPortrait(Rect windowRect)
        {
            Pawn negotiator = TradeSession.playerNegotiator;
            if (negotiator == null) return;

            float portraitWidth = 150f;
            float portraitHeight = 200f;
            float padding = 10f;
            float nameLabelHeight = 35f; // Keep this the same as the trader's
            float gap = 5f;
            float verticalOffset = 50f; // Offset from the top of the trade window

            float containerWidth = portraitWidth + padding * 2;
            // Use the exact same height calculation as the trader's portrait
            float containerHeight = portraitHeight + padding * 2 + nameLabelHeight + gap;

            float yPos = windowRect.y + verticalOffset;
            Rect containerRect = new Rect(windowRect.x - containerWidth - 10, yPos, containerWidth, containerHeight);

            // Draw window-like background
            Widgets.DrawWindowBackground(containerRect);

            // Draw portrait
            Rect portraitRect = new Rect(containerRect.x + padding, containerRect.y + padding, portraitWidth, portraitHeight);
            GUI.DrawTexture(portraitRect, PortraitsCache.Get(negotiator, new Vector2(portraitWidth, portraitHeight), Rot4.South, default, 1f, true, true, true, true));
            
            // Draw negotiator name at the bottom
            Rect nameRect = new Rect(containerRect.x, portraitRect.yMax + gap, containerWidth, nameLabelHeight);
            GUI.Label(nameRect, negotiator.Name.ToStringShort, nameStyle);

            // Draw Social Skill, overlapping the portrait
            Rect skillRect = new Rect(portraitRect.x, portraitRect.yMax - 22f, portraitRect.width, 22f);
            
            // Add a semi-transparent background for readability
            Widgets.DrawRectFast(skillRect, new Color(0, 0, 0, 0.5f));

            string socialSkillString = $"{SkillDefOf.Social.LabelCap}: {negotiator.skills.GetSkill(SkillDefOf.Social).Level}";
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(skillRect, socialSkillString);

            // Reset text settings
            Text.Anchor = TextAnchor.UpperLeft;
        }
    }
}
