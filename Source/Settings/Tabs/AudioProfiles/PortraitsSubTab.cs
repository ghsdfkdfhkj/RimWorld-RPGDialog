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
        private static string searchQuery = "";
        public static Dictionary<string, bool> portraitFactionExpansionStates = new Dictionary<string, bool>();

        public static void Draw(Rect inRect, SettingsData settings)
        {
            Color darkHeaderColor = new Color(0.12f, 0.12f, 0.12f);
            Color darkHeaderBorderColor = new Color(0.35f, 0.35f, 0.35f);

            // Header: Just Title (Refresh removed)
            Rect headerRect = new Rect(inRect.x, inRect.y, inRect.width, 30f);
            Widgets.Label(headerRect, "RPDia_CustomPortraitSettings".Translate());
            
            // Removed Refresh Button and Status Text logic
            
            float buttonHeight = 30f;
            // Space for Folder Button at bottom
            float contentHeight = inRect.height - 30f - buttonHeight - 12f;
            Rect boxRect = new Rect(inRect.x, inRect.y + 30f, inRect.width, contentHeight);
            
            Widgets.DrawBox(boxRect);
            Rect innerBoxRect = boxRect.ContractedBy(10f);
            
            float leftColumnWidth = innerBoxRect.width * 0.4f;
            
            // Search Bar Logic
            Rect searchRect = new Rect(innerBoxRect.x, innerBoxRect.y, leftColumnWidth, 24f);
            Rect textFieldRect = new Rect(searchRect.x, searchRect.y, searchRect.width - 24f, searchRect.height);
            Rect clearButtonRect = new Rect(textFieldRect.xMax, searchRect.y, 24f, 24f);

            searchQuery = Widgets.TextField(textFieldRect, searchQuery);

            if (!string.IsNullOrEmpty(searchQuery))
            {
                if (Widgets.ButtonImage(clearButtonRect, TexButton.CloseXSmall))
                {
                    searchQuery = "";
                    GUI.FocusControl(null);
                    SoundDefOf.Tick_Low.PlayOneShotOnCamera(null);
                }
            }

            Rect listRect = new Rect(innerBoxRect.x, searchRect.yMax + 4f, leftColumnWidth, innerBoxRect.height - searchRect.height - 4f);
            Rect detailRect = new Rect(listRect.xMax + 10f, innerBoxRect.y, innerBoxRect.width - leftColumnWidth - 10f, innerBoxRect.height);
            
            Widgets.DrawLineVertical(listRect.xMax + 5f, listRect.y, listRect.height);

            // Left: Entity List
            DrawPortraitEntityList(listRect, settings);

            // Right: Details
            DrawPortraitDetails(detailRect, settings);

             // Bottom: Folder Button
            Rect folderButtonRect = new Rect(inRect.x, boxRect.yMax + 12f, inRect.width, 30f);
            if (Widgets.ButtonText(folderButtonRect, "RPDia_OpenPortraitsFolder".Translate()))
            {
                try { 
                    string path = System.IO.Path.Combine(SettingsCore.ModContent.RootDir, "Textures", "UI", "Portraits");
                    FileUtils.OpenDirectory(path);
                }
                catch (Exception e) { Log.Error($"Could not open custom portraits folder: {e.Message}"); }
            }
            TooltipHandler.TipRegion(folderButtonRect, "RPDia_PortraitsFolderTooltip".Translate());
        }

        private static void DrawPortraitEntityList(Rect rect, SettingsData settings)
        {
            float availableWidthForText = rect.width - 16f;
            float minItemHeight = 30f;
            float viewHeight = 0f;
            
            Color darkHeaderColor = new Color(0.12f, 0.12f, 0.12f);
            Color darkHeaderBorderColor = new Color(0.35f, 0.35f, 0.35f);

            // --- Entity List (Accordion UI) ---
            var filteredPawnsByFaction = new SortedDictionary<string, List<Pawn>>();
            if (AudioProfilesTab.pawnsByFaction != null)
            {
                foreach (var entry in AudioProfilesTab.pawnsByFaction)
                {
                    if (string.IsNullOrEmpty(searchQuery))
                    {
                        filteredPawnsByFaction[entry.Key] = entry.Value;
                        continue;
                    }
                    
                    List<Pawn> filteredPawns = null;
                    foreach (var pawn in entry.Value)
                    {
                        if (pawn.Name.ToStringShort.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            if (filteredPawns == null) filteredPawns = new List<Pawn>();
                            filteredPawns.Add(pawn);
                        }
                    }

                    if (filteredPawns != null)
                    {
                        filteredPawnsByFaction[entry.Key] = filteredPawns;
                    }
                }
            }

            // Calc height
            // Storytellers
            string storytellerHeaderText = $"{"Storyteller".Translate()} ({AudioProfilesTab.availableStorytellers.Count}) {(portraitStorytellersExpanded ? "▲" : "▼")}";
            float storytellerHeaderHeight = Mathf.Max(minItemHeight, Text.CalcHeight(storytellerHeaderText, availableWidthForText - 10f));

            viewHeight += storytellerHeaderHeight; 
            if (portraitStorytellersExpanded)
            {
                viewHeight += AudioProfilesTab.availableStorytellers.Count * minItemHeight;
            }
            viewHeight += 12f; // Gap/Separator space

            // Factions
            foreach (var entry in filteredPawnsByFaction)
            {
                string factionLabel = entry.Key;
                List<Pawn> pawns = entry.Value;
                bool isExpanded = portraitFactionExpansionStates.TryGetValue(factionLabel, out bool expanded) && expanded;
                string factionHeaderText = $"  {factionLabel} ({pawns.Count}) {(isExpanded ? "▲" : "▼")}";
                float factionHeaderHeight = Mathf.Max(minItemHeight, Text.CalcHeight(factionHeaderText, availableWidthForText - 10f));
                viewHeight += factionHeaderHeight;

                if (isExpanded)
                {
                    viewHeight += pawns.Count * minItemHeight;
                }
            }

            Rect viewRect = new Rect(0, 0, rect.width - 16f, viewHeight);
            Widgets.BeginScrollView(rect, ref portraitEntityScrollPosition, viewRect);
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(viewRect);

            // Storytellers
            Rect stHeaderRatio = listing.GetRect(storytellerHeaderHeight);
            Widgets.DrawBoxSolidWithOutline(stHeaderRatio, darkHeaderColor, darkHeaderBorderColor);
            if (Widgets.ButtonInvisible(stHeaderRatio))
            {
                portraitStorytellersExpanded = !portraitStorytellersExpanded;
                 SoundDefOf.Tick_Tiny.PlayOneShotOnCamera(null);
            }
            
            Text.Anchor = TextAnchor.MiddleLeft;
            Rect stLabelRect = stHeaderRatio;
            stLabelRect.xMin += 10f;
            Widgets.Label(stLabelRect, storytellerHeaderText);
            Text.Anchor = TextAnchor.UpperLeft;
            
            if (portraitStorytellersExpanded)
            {
                foreach(var st in AudioProfilesTab.availableStorytellers)
                {
                    Rect itemRect = listing.GetRect(minItemHeight);
                    bool isSelected = selectedPortraitEntityKey == st.defName;
                    Widgets.DrawOptionBackground(itemRect, isSelected);
                    if (Widgets.ButtonInvisible(itemRect)) 
                    {
                        selectedPortraitEntityKey = st.defName;
                        SoundDefOf.Tick_Tiny.PlayOneShotOnCamera(null);
                    }
                    
                    bool isModified = settings.customPortraitMappings.ContainsKey(st.defName);
                    string displayLabel = st.LabelCap + (isModified ? " *" : "");
                    if (isModified) GUI.color = Color.cyan;

                    Text.Anchor = TextAnchor.MiddleLeft;
                    Rect labelRect = itemRect;
                     labelRect.xMin += 10f;
                    Widgets.Label(labelRect, displayLabel);
                     Text.Anchor = TextAnchor.UpperLeft;
                     GUI.color = Color.white;
                }
            }
            
            listing.GapLine(12f); // Separator Line

            // Pawns by Faction
            foreach (var entry in filteredPawnsByFaction)
            {
                string factionLabel = entry.Key;
                List<Pawn> pawns = entry.Value;
                bool isExpanded = portraitFactionExpansionStates.TryGetValue(factionLabel, out bool expanded) && expanded;
                
                string factionHeaderText = $"  {factionLabel} ({pawns.Count}) {(isExpanded ? "▲" : "▼")}";
                float factionHeaderHeight = Mathf.Max(minItemHeight, Text.CalcHeight(factionHeaderText, availableWidthForText - 10f));
                Rect factionHeaderRect = listing.GetRect(factionHeaderHeight);

                Widgets.DrawBoxSolidWithOutline(factionHeaderRect, darkHeaderColor, darkHeaderBorderColor);
                if (Widgets.ButtonInvisible(factionHeaderRect))
                {
                    portraitFactionExpansionStates[factionLabel] = !isExpanded;
                    SoundDefOf.Tick_Tiny.PlayOneShotOnCamera(null);
                }
                Text.Anchor = TextAnchor.MiddleLeft;
                Rect factionLabelRect = factionHeaderRect;
                factionLabelRect.xMin += 10f;
                Widgets.Label(factionLabelRect, factionHeaderText);
                Text.Anchor = TextAnchor.UpperLeft;

                if (isExpanded)
                {
                    foreach (var pawn in pawns)
                    {
                        Rect itemRect = listing.GetRect(minItemHeight);
                        bool isSelected = selectedPortraitEntityKey == pawn.ThingID;

                        Widgets.DrawOptionBackground(itemRect, isSelected);

                        if (Widgets.ButtonInvisible(itemRect))
                        {
                            selectedPortraitEntityKey = pawn.ThingID;
                            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera(null);
                        }
                        
                        // Check for modification
                        bool isModified = settings.customPortraitMappings.ContainsKey(pawn.ThingID);
                        string displayLabel = pawn.Name.ToStringShort + (isModified ? " *" : "");
                        if (isModified) GUI.color = Color.cyan;

                        Text.Anchor = TextAnchor.MiddleLeft;
                        Rect labelRect = itemRect;
                        labelRect.xMin += 15f; 
                        Widgets.Label(labelRect, displayLabel);
                        Text.Anchor = TextAnchor.UpperLeft;
                        GUI.color = Color.white;
                    }
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

            // Calculate available space for texture to prevent button overflow
            float buttonAreaHeight = 30f + 10f + 30f; // ChangeBtn + Gap + ResetBtn
            float headerSpace = 30f + 15f; // Label + Gap
            float maxTextureHeight = rect.height - headerSpace - buttonAreaHeight - 10f; // Extra padding
            
            // Draw current portrait
            float textureHeight = Mathf.Min(250f, maxTextureHeight); // Use 250f or less if constrained
            Rect textureRect = new Rect(rect.x + (rect.width - 200f)/2f, rect.y + 40f, 200f, textureHeight);
            
            // Try loading custom portrait first
            Texture tex = PortraitLoader.TryLoadCustomPortrait(selectedPortraitEntityKey);

            // If no custom portrait, try fallback to vanilla/mod default
            if (tex == null)
            {
                 if (stDef != null)
                 {
                     string portPath = (string)typeof(StorytellerDef).GetField("portraitLarge")?.GetValue(stDef);
                     if (!portPath.NullOrEmpty())
                     {
                         tex = ContentFinder<Texture2D>.Get(portPath, reportFailure: false);
                     }
                 }
                 else if (pawn != null)
                 {
                     tex = PortraitsCache.Get(pawn, new Vector2(175f, 175f), Rot4.South, new Vector3(0f, 0f, 0f), 1f, true, true, true, true, null, null, false, null);
                 }
            }
            
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

            Rect btnRect = new Rect(rect.x + (rect.width - 150f)/2f, textureRect.yMax + 10f, 150f, 30f);
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
