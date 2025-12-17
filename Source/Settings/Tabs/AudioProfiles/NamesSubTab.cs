using RimWorld;
using UnityEngine;
using Verse;
using System.Collections.Generic;
using System.Linq;

namespace RPGDialog
{
    public static class NamesSubTab
    {
        private static Vector2 storytellerNameScrollPosition = Vector2.zero;

        public static void Draw(Rect inRect, SettingsData settings)
        {
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 30f), "RPDia_StorytellerNameSettings".Translate());
            
            Rect scrollContainerRect = new Rect(inRect.x, inRect.y + 30f, inRect.width, inRect.height - 30f);
            
            var storytellerDefs = DefDatabase<StorytellerDef>.AllDefs.ToList();
            float storytellerContentHeight = storytellerDefs.Count * 32f;
            Rect viewRect = new Rect(0, 0, scrollContainerRect.width - 16f, storytellerContentHeight);

            Widgets.BeginScrollView(scrollContainerRect, ref storytellerNameScrollPosition, viewRect);
            Listing_Standard innerStorytellerListing = new Listing_Standard();
            innerStorytellerListing.Begin(viewRect);
            
            foreach (var storytellerDef in storytellerDefs)
            {
                Rect lineRect = innerStorytellerListing.GetRect(30f);
                
                float labelWidth = lineRect.width * 0.4f;
                float textWidth = lineRect.width - labelWidth - 10f;

                Rect labelRect = new Rect(lineRect.x, lineRect.y, labelWidth, 30f);
                Rect textRect = new Rect(labelRect.xMax, lineRect.y, textWidth, 30f);

                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(labelRect, storytellerDef.label);
                Text.Anchor = TextAnchor.UpperLeft;
                
                string currentName = StorytellerNameDatabase.GetStorytellerName(storytellerDef);
                string newName = Widgets.TextField(textRect, currentName);
                if (newName != currentName)
                {
                    StorytellerNameDatabase.SetStorytellerName(storytellerDef, newName);
                }
            }
            innerStorytellerListing.End();
            Widgets.EndScrollView();
        }
    }
}
