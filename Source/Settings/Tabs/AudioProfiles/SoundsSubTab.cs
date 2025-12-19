using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using System.Collections.Generic;
using System.Linq;
using System;

namespace RPGDialog
{
    public static class SoundsSubTab
    {
        private static Vector2 entityScrollPosition = Vector2.zero;
        private static Vector2 soundListScrollPosition = Vector2.zero;
        private static string selectedDefName;
        private static string searchQuery = "";
        private static bool storytellersExpanded = false;
        public static Dictionary<string, bool> factionExpansionStates = new Dictionary<string, bool>();

        public static void Draw(Rect inRect, SettingsData settings) 
        {
            Color darkHeaderColor = new Color(0.12f, 0.12f, 0.12f);
            Color darkHeaderBorderColor = new Color(0.35f, 0.35f, 0.35f);

            // Header
            Rect headerRect = new Rect(inRect.x, inRect.y, inRect.width, 30f);
            Widgets.Label(headerRect, "RPDia_CustomTypingSoundSettings".Translate());
            
            float refreshSoundButtonWidth = 120f;
            Rect refreshRect = new Rect(headerRect.xMax - refreshSoundButtonWidth, headerRect.y, refreshSoundButtonWidth, 24f);
            if (Widgets.ButtonText(refreshRect, "RPDia_RefreshSounds".Translate()))
            {
                AudioProfilesTab.ReloadSounds();
                SoundDefOf.Tick_High.PlayOneShotOnCamera(null);
            }
            TooltipHandler.TipRegion(refreshRect, "RPDia_RefreshSoundsTooltip".Translate());

            float buttonHeight = 30f;
            // Space for Folder Button at bottom
            float contentHeight = inRect.height - 30f - buttonHeight - 12f;
            Rect boxRect = new Rect(inRect.x, inRect.y + 30f, inRect.width, contentHeight);
            Widgets.DrawBox(boxRect);

            Rect innerBoxRect = boxRect.ContractedBy(10f);
            float leftColumnWidth = innerBoxRect.width / 2 - 5f;

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

            Rect entityListRect = new Rect(innerBoxRect.x, searchRect.yMax + 4f, leftColumnWidth, innerBoxRect.height - searchRect.height - 4f);
            Rect soundListRect = new Rect(innerBoxRect.x + innerBoxRect.width / 2 + 5f, innerBoxRect.y, innerBoxRect.width / 2 - 5f, innerBoxRect.height);

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

            float availableWidthForText = entityListRect.width - 16f;
            float minItemHeight = 30f;
            float entityViewHeight = 0f;

            string storytellerHeaderText = $"{"Storyteller".Translate()} ({AudioProfilesTab.availableStorytellers.Count}) {(storytellersExpanded ? "▲" : "▼")}";
            float storytellerHeaderHeight = Mathf.Max(minItemHeight, Text.CalcHeight(storytellerHeaderText, availableWidthForText - 10f));
            entityViewHeight += storytellerHeaderHeight;

            if (storytellersExpanded)
            {
                foreach (var storyteller in AudioProfilesTab.availableStorytellers)
                {
                    string effectiveSound = AudioProfilesTab.GetEffectiveStorytellerSound(storyteller, settings);
                    string label = $"  {storyteller.LabelCap} ({effectiveSound})";
                    entityViewHeight += Mathf.Max(minItemHeight, Text.CalcHeight(label, availableWidthForText - 10f));
                }
            }
            entityViewHeight += 12f; 

            foreach (var entry in filteredPawnsByFaction)
            {
                string factionLabel = entry.Key;
                List<Pawn> pawns = entry.Value;
                bool isExpanded = factionExpansionStates.TryGetValue(factionLabel, out bool expanded) && expanded;
                string factionHeaderText = $"  {factionLabel} ({pawns.Count}) {(isExpanded ? "▲" : "▼")}";
                float factionHeaderHeight = Mathf.Max(minItemHeight, Text.CalcHeight(factionHeaderText, availableWidthForText - 10f));
                entityViewHeight += factionHeaderHeight;

                if (isExpanded)
                {
                    foreach (var pawn in entry.Value)
                    {
                        PawnSoundSettings.Current.customPawnTypingSounds.TryGetValue(pawn.ThingID, out string currentSound);
                        string label = $"    {pawn.Name.ToStringShort} ({currentSound ?? "Default"})";
                        entityViewHeight += Mathf.Max(minItemHeight, Text.CalcHeight(label, availableWidthForText - 15f));
                    }
                }
            }

            Rect entityViewRect = new Rect(0, 0, entityListRect.width - 16f, entityViewHeight);
            Widgets.BeginScrollView(entityListRect, ref entityScrollPosition, entityViewRect);
            Listing_Standard entityListing = new Listing_Standard();
            entityListing.Begin(entityViewRect);
            
            string storytellerHeaderTextDraw = $"{"Storyteller".Translate()} ({AudioProfilesTab.availableStorytellers.Count}) {(storytellersExpanded ? "▲" : "▼")}";
            float storytellerHeaderHeightDraw = Mathf.Max(minItemHeight, Text.CalcHeight(storytellerHeaderTextDraw, entityViewRect.width - 10f));
            Rect storytellerHeaderRect = entityListing.GetRect(storytellerHeaderHeightDraw);
            Widgets.DrawBoxSolidWithOutline(storytellerHeaderRect, darkHeaderColor, darkHeaderBorderColor);
            if (Widgets.ButtonInvisible(storytellerHeaderRect))
            {
                storytellersExpanded = !storytellersExpanded;
                SoundDefOf.Tick_Tiny.PlayOneShotOnCamera(null);
            }
            Text.Anchor = TextAnchor.MiddleLeft;
            Rect storytellerLabelRect = storytellerHeaderRect;
            storytellerLabelRect.xMin += 10f;
            Widgets.Label(storytellerLabelRect, storytellerHeaderTextDraw);
            Text.Anchor = TextAnchor.UpperLeft;

            if (storytellersExpanded)
            {
                foreach (var storyteller in AudioProfilesTab.availableStorytellers)
                {
                    string effectiveSound = AudioProfilesTab.GetEffectiveStorytellerSound(storyteller, settings);
                    string label = $"  {storyteller.LabelCap} ({effectiveSound})";
                    float itemHeight = Mathf.Max(minItemHeight, Text.CalcHeight(label, entityViewRect.width - 10f));
                    Rect itemRect = entityListing.GetRect(itemHeight);
                    bool isSelected = selectedDefName == storyteller.defName;
                    
                    Widgets.DrawOptionBackground(itemRect, isSelected);

                    if (Widgets.ButtonInvisible(itemRect))
                    {
                        selectedDefName = storyteller.defName;
                        SoundDefOf.Tick_Tiny.PlayOneShotOnCamera(null);
                    }
                    
                    Text.Anchor = TextAnchor.MiddleLeft;
                    Rect labelRect = itemRect;
                    labelRect.xMin += 10f;
                    Widgets.Label(labelRect, label);
                    Text.Anchor = TextAnchor.UpperLeft;
                }
            }
            entityListing.GapLine(12f);
            
            foreach (var entry in filteredPawnsByFaction)
            {
                string factionLabel = entry.Key;
                List<Pawn> pawns = entry.Value;
                bool isExpanded = factionExpansionStates.TryGetValue(factionLabel, out bool expanded) && expanded;
                
                string factionHeaderText = $"  {factionLabel} ({pawns.Count}) {(isExpanded ? "▲" : "▼")}";
                float factionHeaderHeight = Mathf.Max(minItemHeight, Text.CalcHeight(factionHeaderText, entityViewRect.width - 10f));
                Rect factionHeaderRect = entityListing.GetRect(factionHeaderHeight);

                Widgets.DrawBoxSolidWithOutline(factionHeaderRect, darkHeaderColor, darkHeaderBorderColor);
                if (Widgets.ButtonInvisible(factionHeaderRect))
                {
                    factionExpansionStates[factionLabel] = !isExpanded;
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
                        PawnSoundSettings.Current.customPawnTypingSounds.TryGetValue(pawn.ThingID, out string currentSound);
                        string label = $"    {pawn.Name.ToStringShort} ({currentSound ?? "Default"})";
                        float itemHeight = Mathf.Max(minItemHeight, Text.CalcHeight(label, entityViewRect.width - 15f));
                        Rect itemRect = entityListing.GetRect(itemHeight);
                        bool isSelected = selectedDefName == pawn.ThingID;

                        Widgets.DrawOptionBackground(itemRect, isSelected);

                        if (Widgets.ButtonInvisible(itemRect))
                        {
                            selectedDefName = pawn.ThingID;
                            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera(null);
                        }
                        
                        Text.Anchor = TextAnchor.MiddleLeft;
                        Rect labelRect = itemRect;
                        labelRect.xMin += 15f; 
                        Widgets.Label(labelRect, label);
                        Text.Anchor = TextAnchor.UpperLeft;
                    }
                }
            }
            
            entityListing.End();
            Widgets.EndScrollView();

            // --- Sound Selection ---
             if (!string.IsNullOrEmpty(selectedDefName))
            {
                bool isStoryteller = DefDatabase<StorytellerDef>.GetNamed(selectedDefName, false) != null;
                string currentSoundForDef;

                if (isStoryteller)
                {
                    var def = DefDatabase<StorytellerDef>.GetNamed(selectedDefName);
                    currentSoundForDef = AudioProfilesTab.GetEffectiveStorytellerSound(def, settings);
                }
                else
                {
                    PawnSoundSettings.Current.customPawnTypingSounds.TryGetValue(selectedDefName, out currentSoundForDef);
                }
                if (currentSoundForDef == null) currentSoundForDef = "Default";
                
                float soundContentHeight = AudioProfilesTab.availableSounds.Count * 30f;
                Rect soundViewRect = new Rect(0, 0, soundListRect.width - 16f, soundContentHeight);
                Widgets.BeginScrollView(soundListRect, ref soundListScrollPosition, soundViewRect); // Using soundListRect from above
                Listing_Standard soundOptionsListing = new Listing_Standard();
                soundOptionsListing.Begin(soundViewRect);

                foreach (var sound in AudioProfilesTab.availableSounds)
                {
                    Rect itemRect = soundOptionsListing.GetRect(30f);

                    Rect selectionButtonRect = new Rect(itemRect.x, itemRect.y, itemRect.width - 40f, itemRect.height);
                    bool isSelected = sound == currentSoundForDef;
                    Widgets.DrawOptionBackground(selectionButtonRect, isSelected);
                    if (Widgets.ButtonInvisible(selectionButtonRect))
                    {
                        if (isStoryteller)
                        {
                            var def = DefDatabase<StorytellerDef>.GetNamed(selectedDefName);
                            if (sound == def.defName)
                            {
                                settings.customStorytellerTypingSounds.Remove(selectedDefName);
                            }
                            else
                            {
                                settings.customStorytellerTypingSounds[selectedDefName] = sound;
                            }
                        }
                        else
                        {
                            var pawn = PawnsFinder.AllMapsAndWorld_Alive.FirstOrDefault(p => p.ThingID == selectedDefName);
                            if (pawn != null)
                            {
                                PawnSoundSettings.SetPawnTypingSound(pawn, sound);
                            }
                        }
                        SoundDefOf.Tick_Tiny.PlayOneShotOnCamera(null);
                    }

                    Rect previewButtonRect = new Rect(itemRect.xMax - 30f, itemRect.y + (itemRect.height - 24f) / 2, 24f, 24f);
                    if (Widgets.ButtonImage(previewButtonRect, TexButton.SpeedButtonTextures[1]))
                    {
                        TypingSoundUtility.PlayPreviewSound(sound);
                    }
                    TooltipHandler.TipRegion(previewButtonRect, "RPDia_PreviewSound".Translate());
                    
                    Text.Anchor = TextAnchor.MiddleLeft;
                    Rect labelRect = selectionButtonRect;
                    labelRect.xMin += 10f;
                    Widgets.Label(labelRect, sound);
                    Text.Anchor = TextAnchor.UpperLeft;
                }
                soundOptionsListing.End();
                Widgets.EndScrollView();
            }

            // Folder Buttons
            Rect folderButtonRect = new Rect(inRect.x, boxRect.yMax + 12f, inRect.width, 30f);
            if (Widgets.ButtonText(folderButtonRect, "RPDia_OpenTypingSoundsFolder".Translate()))
            {
                try { 
                    string path = System.IO.Path.Combine(SettingsCore.ModContent.RootDir, "Sounds", "Typing");
                    FileUtils.OpenDirectory(path);
                }
                catch (Exception e) { Log.Error($"Could not open custom typing sounds folder: {e.Message}"); }
            }
            TooltipHandler.TipRegion(folderButtonRect, "RPDia_TypingSoundsFolderTooltip".Translate());
        }
    }
}
