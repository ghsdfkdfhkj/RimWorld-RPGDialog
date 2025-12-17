using RimWorld;
using UnityEngine;
using Verse;
using System.Collections.Generic;
using System.Linq;
using System;

using Verse.Sound;

namespace RPGDialog
{
    public class AudioProfilesTab
    {
        // Profile Sub-Tabs
        private enum ProfileSubTab { Names, Portraits, Sounds }
        private static ProfileSubTab currentProfileSubTab = ProfileSubTab.Names;

        // Audio Profile State - Exposing these as public static for sub-tabs to access
        public static List<string> availableSounds;
        public static List<StorytellerDef> availableStorytellers;
        public static SortedDictionary<string, List<Pawn>> pawnsByFaction;
        public static bool staticContentLoaded = false;

        public static Dictionary<string, bool> soundFileExistsCache = new Dictionary<string, bool>();

        // Helper Methods from Mod class
        public static bool SoundFileExistsFor(string key)
        {
            if (soundFileExistsCache.TryGetValue(key, out bool exists))
            {
                return exists;
            }

            string typingSoundPath = System.IO.Path.Combine(SettingsCore.ModContent.RootDir, "Sounds", "Typing");
            bool fileExists = System.IO.File.Exists(System.IO.Path.Combine(typingSoundPath, key + ".wav")) ||
                              System.IO.File.Exists(System.IO.Path.Combine(typingSoundPath, key + ".ogg"));
        
            soundFileExistsCache[key] = fileExists;
            return fileExists;
        }

        public static string GetEffectiveStorytellerSound(StorytellerDef def, SettingsData settings)
        {
            if (settings.customStorytellerTypingSounds.TryGetValue(def.defName, out string customSound))
            {
                return customSound;
            }
            if (SoundFileExistsFor(def.defName))
            {
                return def.defName;
            }
            return "Default";
        }
        
        private static void InitializeStaticContentIfNeeded()
        {
            if (staticContentLoaded) return;

            ReloadSounds();
            ReloadPortraits();
            
            availableStorytellers = DefDatabase<StorytellerDef>.AllDefsListForReading;
            
            staticContentLoaded = true;
        }

        public static void ReloadSounds()
        {
            availableSounds = new List<string> { "Default" };
            soundFileExistsCache.Clear();
            TypingSoundUtility.ClearCache();
            try
            {
                string soundsPath = System.IO.Path.Combine(SettingsCore.ModContent.RootDir, "Sounds", "Typing");
                if (System.IO.Directory.Exists(soundsPath))
                {
                    var wavs = System.IO.Directory.GetFiles(soundsPath, "*.wav");
                    var oggs = System.IO.Directory.GetFiles(soundsPath, "*.ogg");
                    
                    availableSounds.AddRange(wavs.Select(System.IO.Path.GetFileNameWithoutExtension));
                    availableSounds.AddRange(oggs.Select(System.IO.Path.GetFileNameWithoutExtension));
                }
            }
            catch (Exception e) 
            { 
                Log.Error($"Error loading custom typing sounds: {e.Message}");
            }
        }

        public static void ReloadPortraits()
        {
            PortraitLoader.ClearCache();
            try
            {
                string path = System.IO.Path.Combine(SettingsCore.ModContent.RootDir, "Textures", "UI", "Portraits");
                if (System.IO.Directory.Exists(path))
                {
                    var pngs = System.IO.Directory.GetFiles(path, "*.png");
                    var jpgs = System.IO.Directory.GetFiles(path, "*.jpg");
                }
            }
            catch
            {
                // lastPortraitScanStatus = "Error: " + e.Message;
            }
        }

        private static void RefreshPawnList()
        {
            pawnsByFaction = new SortedDictionary<string, List<Pawn>>();
            
            if (Current.Game == null || Find.World == null) return;

            foreach (var pawn in PawnsFinder.AllMapsAndWorld_Alive)
            {
                if (!(pawn.RaceProps?.Humanlike ?? false)) continue;
                
                string factionLabel = pawn.Faction?.Name ?? "RPDia_NoFaction".Translate();
                if (!pawnsByFaction.ContainsKey(factionLabel))
                {
                    pawnsByFaction[factionLabel] = new List<Pawn>();
                    if (!SoundsSubTab.factionExpansionStates.ContainsKey(factionLabel))
                    {
                        SoundsSubTab.factionExpansionStates[factionLabel] = false;
                    }
                }
                pawnsByFaction[factionLabel].Add(pawn);
            }
        }

        public static void Draw(Rect inRect, SettingsData settings)
        {
            InitializeStaticContentIfNeeded();
            if (Current.Game != null)
            {
                 RefreshPawnList(); 
            }

             // Sub-Tabs
            List<TabRecord> tabs = new List<TabRecord>();
            tabs.Add(new TabRecord("RPDia_TabNames".Translate(), () => currentProfileSubTab = ProfileSubTab.Names, currentProfileSubTab == ProfileSubTab.Names));
            tabs.Add(new TabRecord("RPDia_TabPortraits".Translate(), () => currentProfileSubTab = ProfileSubTab.Portraits, currentProfileSubTab == ProfileSubTab.Portraits));
            tabs.Add(new TabRecord("RPDia_TabSounds".Translate(), () => currentProfileSubTab = ProfileSubTab.Sounds, currentProfileSubTab == ProfileSubTab.Sounds));

            Rect tabRect = new Rect(inRect.x, inRect.y, inRect.width, 30f);
            TabDrawer.DrawTabs(tabRect, tabs);

            Rect contentRect = new Rect(inRect.x, inRect.y + 40f, inRect.width, inRect.height - 40f);

            switch (currentProfileSubTab)
            {
                case ProfileSubTab.Names:
                    NamesSubTab.Draw(contentRect, settings);
                    break;
                case ProfileSubTab.Portraits:
                    PortraitsSubTab.Draw(contentRect, settings);
                    break;
                case ProfileSubTab.Sounds:
                    SoundsSubTab.Draw(contentRect, settings);
                    break;
            }
        }
    }
}
