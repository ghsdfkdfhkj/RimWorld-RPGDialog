using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using System.Collections.Generic;
using System.Linq;
using System;

namespace RPGDialog
{
    public static class PortraitsSubTab
    {
        private static Vector2 portraitEntityScrollPosition = Vector2.zero;
        private static string selectedPortraitEntityKey;
        private static bool portraitStorytellersExpanded = false;
        public static Dictionary<string, bool> portraitFactionExpansionStates = new Dictionary<string, bool>();

        public static void Draw(Rect inRect, SettingsData settings)
        {
            // Header: Refresh Button & Status
            Rect headerRect = new Rect(inRect.x, inRect.y, inRect.width, 30f);
            
            float refreshButtonWidth = 140f;
            Rect refreshRect = new Rect(headerRect.xMax - refreshButtonWidth, headerRect.y, refreshButtonWidth, 24f);
            if (Widgets.ButtonText(refreshRect, "RPDia_RefreshPortraits".Translate()))
            {
                AudioProfilesTab.ReloadPortraits();
                SoundDefOf.Tick_High.PlayOneShotOnCamera(null);
            }
            TooltipHandler.TipRegion(refreshRect, "RPDia_RefreshPortraitsTooltip".Translate());

            if (!string.IsNullOrEmpty(AudioProfilesTab.lastPortraitScanStatus))
            {
                 float statusWidth = Text.CalcSize(AudioProfilesTab.lastPortraitScanStatus).x + 10f;
                 Rect statusRect = new Rect(refreshRect.x - statusWidth - 10f, headerRect.y, statusWidth, 24f);
                 Text.Anchor = TextAnchor.MiddleRight;
                 GUI.color = Color.gray;
                 Widgets.Label(statusRect, AudioProfilesTab.lastPortraitScanStatus);
                 GUI.color = Color.white;
                 Text.Anchor = TextAnchor.UpperLeft;
            }
            
            Rect boxRect = new Rect(inRect.x, inRect.y + 30f, inRect.width, inRect.height - 30f - 40f); // Space for folder button
            Widgets.DrawBox(boxRect);
            Rect innerBoxRect = boxRect.ContractedBy(10f);
            
            float leftColumnWidth = innerBoxRect.width * 0.4f;
            Rect listRect = new Rect(innerBoxRect.x, innerBoxRect.y, leftColumnWidth, innerBoxRect.height);
            Rect detailRect = new Rect(listRect.xMax + 10f, innerBoxRect.y, innerBoxRect.width - leftColumnWidth - 10f, innerBoxRect.height);
            
            Widgets.DrawLineVertical(listRect.xMax + 5f, listRect.y, listRect.height);

            // Left: Entity List
            DrawPortraitEntityList(listRect);

            // Right: Details
            DrawPortraitDetails(detailRect, settings);

             // Bottom: Folder Button
            Rect folderButtonRect = new Rect(inRect.x, boxRect.yMax + 5f, 200f, 30f);
            if (Widgets.ButtonText(folderButtonRect, "RPDia_OpenPortraitsFolder".Translate()))
            {
                try { 
                    string path = System.IO.Path.Combine(SettingsCore.ModContent.RootDir, "Textures", "UI", "Storyteller");
                    System.IO.Directory.CreateDirectory(path);
                    System.Diagnostics.Process.Start(path);
                }
                catch (Exception e) { Log.Error($"Could not open custom portraits folder: {e.Message}"); }
            }
            TooltipHandler.TipRegion(folderButtonRect, "RPDia_PortraitsFolderTooltip".Translate());
        }

        private static void DrawPortraitEntityList(Rect rect)
        {
            float availableWidthForText = rect.width - 16f;
            float minItemHeight = 30f;
            float viewHeight = 0f;
            
            Color darkHeaderColor = new Color(0.12f, 0.12f, 0.12f);
            Color darkHeaderBorderColor = new Color(0.35f, 0.35f, 0.35f);

            // Calc height
            // Storytellers
            viewHeight += minItemHeight; 
            if (portraitStorytellersExpanded)
            {
                viewHeight += AudioProfilesTab.availableStorytellers.Count * minItemHeight;
            }
            viewHeight += 12f;

            // Colonists
            var playerPawns = new List<Pawn>();
            if (AudioProfilesTab.pawnsByFaction != null)
            {
                 foreach(var entry in AudioProfilesTab.pawnsByFaction)
                 {
                     foreach(var p in entry.Value)
                     {
                         if(p.Faction != null && p.Faction.IsPlayer) playerPawns.Add(p);
                     }
                 }
            }
            
            string colonistsHeader = "RPDia_Colonists".Translate();
            viewHeight += minItemHeight;
            if (portraitFactionExpansionStates.ContainsKey("Colonists") && portraitFactionExpansionStates["Colonists"])
            {
                viewHeight += playerPawns.Count * minItemHeight;
            }

            Rect viewRect = new Rect(0, 0, rect.width - 16f, viewHeight);
            Widgets.BeginScrollView(rect, ref portraitEntityScrollPosition, viewRect);
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(viewRect);

            // Storytellers
            Rect stHeaderRatio = listing.GetRect(minItemHeight);
            Widgets.DrawBoxSolidWithOutline(stHeaderRatio, darkHeaderColor, darkHeaderBorderColor);
            if (Widgets.ButtonInvisible(stHeaderRatio)) portraitStorytellersExpanded = !portraitStorytellersExpanded;
            Widgets.Label(new Rect(stHeaderRatio.x + 5f, stHeaderRatio.y, stHeaderRatio.width, stHeaderRatio.height), $"{"Storyteller".Translate()} {(portraitStorytellersExpanded ? "▲" : "▼")}");
            
            if (portraitStorytellersExpanded)
            {
                foreach(var st in AudioProfilesTab.availableStorytellers)
                {
                    Rect itemRect = listing.GetRect(minItemHeight);
                    bool isSelected = selectedPortraitEntityKey == st.defName;
                    Widgets.DrawOptionBackground(itemRect, isSelected);
                    if (Widgets.ButtonInvisible(itemRect)) selectedPortraitEntityKey = st.defName;
                    Widgets.Label(new Rect(itemRect.x + 10f, itemRect.y, itemRect.width, itemRect.height), st.LabelCap);
                }
            }
            
            listing.Gap(12f);

            // Colonists
            Rect colHeaderRect = listing.GetRect(minItemHeight);
            bool colExpanded = portraitFactionExpansionStates.ContainsKey("Colonists") && portraitFactionExpansionStates["Colonists"];
             Widgets.DrawBoxSolidWithOutline(colHeaderRect, darkHeaderColor, darkHeaderBorderColor);
            if (Widgets.ButtonInvisible(colHeaderRect)) portraitFactionExpansionStates["Colonists"] = !colExpanded;
             Widgets.Label(new Rect(colHeaderRect.x + 5f, colHeaderRect.y, colHeaderRect.width, colHeaderRect.height), $"{"Colonists".Translate()} {(colExpanded ? "▲" : "▼")}"); // Using "Colonists" key if exists or fallback
             
             if (colExpanded)
             {
                 foreach(var pawn in playerPawns)
                 {
                    Rect itemRect = listing.GetRect(minItemHeight);
                    bool isSelected = selectedPortraitEntityKey == pawn.ThingID;
                    Widgets.DrawOptionBackground(itemRect, isSelected);
                    if (Widgets.ButtonInvisible(itemRect)) selectedPortraitEntityKey = pawn.ThingID;
                    Widgets.Label(new Rect(itemRect.x + 10f, itemRect.y, itemRect.width, itemRect.height), pawn.Name.ToStringShort);
                 }
             }

            listing.End();
            Widgets.EndScrollView();
        }

        private static void DrawPortraitDetails(Rect rect, SettingsData settings)
        {
            if (selectedPortraitEntityKey.NullOrEmpty())
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(rect, "RPDia_NoPortraitSelected".Translate());
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            // identify entity
            string label = selectedPortraitEntityKey;
            StorytellerDef stDef = AudioProfilesTab.availableStorytellers.FirstOrDefault(s => s.defName == selectedPortraitEntityKey);
            Pawn pawn = null;
            if (stDef != null) label = stDef.label;
            else 
            {
                 pawn = PawnsFinder.AllMapsAndWorld_Alive.FirstOrDefault(p => p.ThingID == selectedPortraitEntityKey);
                 if (pawn != null) label = pawn.Name.ToStringShort;
            }

            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 30f), "RPDia_CurrentPortrait".Translate() + ": " + label);

            // Draw current portrait
            Rect textureRect = new Rect(rect.x + (rect.width - 200f)/2f, rect.y + 40f, 200f, 250f);
            Texture2D tex = PortraitLoader.TryLoadCustomPortrait(selectedPortraitEntityKey);
            
            if (tex != null)
            {
                GUI.DrawTexture(textureRect, tex, ScaleMode.ScaleToFit);
            }
            else
            {
                Widgets.DrawBox(textureRect); // Placeholder
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(textureRect, "Default / None");
                Text.Anchor = TextAnchor.UpperLeft;
            }

            Rect btnRect = new Rect(rect.x + (rect.width - 150f)/2f, textureRect.yMax + 20f, 150f, 30f);
            if (Widgets.ButtonText(btnRect, "RPDia_ChangePortrait".Translate()))
            {
                Find.WindowStack.Add(new Dialog_PortraitSelector(selectedPortraitEntityKey, label));
            }

             Rect resetRect = new Rect(rect.x + (rect.width - 150f)/2f, btnRect.yMax + 10f, 150f, 30f);
             if (Widgets.ButtonText(resetRect, "RPDia_ResetToDefault".Translate()))
            {
                settings.customPortraitMappings.Remove(selectedPortraitEntityKey);
                PortraitLoader.ClearCache();
            }
        }
    }
}
