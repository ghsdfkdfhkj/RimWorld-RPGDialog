using RimWorld;
using UnityEngine;
using Verse;
using System.Collections.Generic;

namespace RPGDialog
{
    public class SettingsCore : Mod
    {
        public static SettingsData settings;
        public static ModContentPack ModContent { get; private set; }

        // Tabs
        private enum SettingsTab { General, Appearance, Typing, AudioProfiles }
        private SettingsTab currentTab = SettingsTab.General;
        
        public SettingsCore(ModContentPack content) : base(content)
        {
            settings = GetSettings<SettingsData>();
            if (settings.customStorytellerTypingSounds == null)
            {
                settings.customStorytellerTypingSounds = new Dictionary<string, string>();
            }
            ModContent = content;
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
             // Initialize static content if needed - delegated to appropriate tabs or handled here if global
             // AudioProfilesTab might need initialization logic called

            if (Current.Game != null && currentTab == SettingsTab.AudioProfiles)
            {
                 // Logic handled in AudioProfilesTab.RefreshPawnList() if needed
                 // But AudioProfilesTab needs to be static or instantiated?
                 // Let's make Tabs static helper classes for now as they were just methods
            }
            
            // Draw Tabs
            List<TabRecord> tabs = new List<TabRecord>();
            tabs.Add(new TabRecord("RPDia_TabGeneral".Translate(), () => currentTab = SettingsTab.General, currentTab == SettingsTab.General));
            tabs.Add(new TabRecord("RPDia_TabAppearance".Translate(), () => currentTab = SettingsTab.Appearance, currentTab == SettingsTab.Appearance));
            tabs.Add(new TabRecord("RPDia_TabTyping".Translate(), () => currentTab = SettingsTab.Typing, currentTab == SettingsTab.Typing));
            tabs.Add(new TabRecord("RPDia_TabAudioProfiles".Translate(), () => currentTab = SettingsTab.AudioProfiles, currentTab == SettingsTab.AudioProfiles));

            Rect tabRect = new Rect(inRect.x, inRect.y + 45f, inRect.width, 30f);
            TabDrawer.DrawTabs(tabRect, tabs);

            Rect contentRect = new Rect(inRect.x, inRect.y + 85f, inRect.width, inRect.height - 85f - 40f); // -40f for bottom reset button
            
            // Reset Button Area at the bottom
            Rect resetRect = new Rect(inRect.x, inRect.y + inRect.height - 35f, inRect.width, 30f);

            switch (currentTab)
            {
                case SettingsTab.General:
                    GeneralTab.Draw(contentRect, settings);
                    break;
                case SettingsTab.Appearance:
                    AppearanceTab.Draw(contentRect, settings);
                    break;
                case SettingsTab.Typing:
                    TypingTab.Draw(contentRect, settings);
                    break;
                case SettingsTab.AudioProfiles:
                    AudioProfilesTab.Draw(contentRect, settings);
                    break;
            }

            // Draw Reset Button (Always visible)
            if (Widgets.ButtonText(resetRect, "RPDia_ResetSettings".Translate()))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("RPDia_ResetSettings_Confirm".Translate(), () =>
                {
                    settings.ResetToDefaults();
                }, true));
            }

            base.DoSettingsWindowContents(inRect);
        }

        public override string SettingsCategory()
        {
            return "RPG Dialog";
        }
    }
}
