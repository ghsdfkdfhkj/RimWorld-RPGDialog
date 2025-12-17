using RimWorld;
using UnityEngine;
using Verse;
using System.Collections.Generic;

namespace RPGDialog
{
    public enum WindowPosition { Top, Middle, Bottom }
    public enum DefaultSpeaker { Storyteller, Leader, ReligiousLeader }

    public class SettingsData : ModSettings
    {
        public WindowPosition position = WindowPosition.Top;
        public WindowPosition echoPosition = WindowPosition.Top;
        public bool dontPauseOnOpen = true;
        public bool onlyShowInGame = true;
        public bool openWindowOnEvent = true;
        public bool useFactionLeaderForOtherFactions = false;
        public bool typingEffectEnabled = true;
        public bool typingSoundEnabled = true;
        public float typingSpeed = 35f;
        public float typingSoundVolume = 0.6f;
        public float dialogFontSize = 24f;
        public float dialogButtonFontSize = 20f;
        public float choiceButtonFontSize = 18f;
        public float windowWidthScale = 0.45f;
        public float windowHeightScale = 0.30f;
        public DefaultSpeaker defaultSpeaker = DefaultSpeaker.Storyteller;
        public Dictionary<string, string> storytellerNames = new Dictionary<string, string>();
        public bool showTraderPortrait = true;
        public bool showNegotiatorPortrait = true;
        public Dictionary<string, string> customStorytellerTypingSounds = new Dictionary<string, string>();
        public Dictionary<string, string> customPortraitMappings = new Dictionary<string, string>();
        
        // Auto Mode Settings
        public bool autoMode_CloseAtEnd = true;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref position, "position", WindowPosition.Top);
            Scribe_Values.Look(ref echoPosition, "echoPosition", WindowPosition.Top);
            Scribe_Values.Look(ref dontPauseOnOpen, "dontPauseOnOpen", true);
            Scribe_Values.Look(ref onlyShowInGame, "onlyShowInGame", true);
            Scribe_Values.Look(ref openWindowOnEvent, "openWindowOnEvent", true);
            Scribe_Values.Look(ref useFactionLeaderForOtherFactions, "useFactionLeaderForOtherFactions", false);
            Scribe_Values.Look(ref typingEffectEnabled, "typingEffectEnabled", true);
            Scribe_Values.Look(ref typingSoundEnabled, "typingSoundEnabled", true);
            Scribe_Values.Look(ref typingSpeed, "typingSpeed", 35f);
            Scribe_Collections.Look(ref customPortraitMappings, "customPortraitMappings", LookMode.Value, LookMode.Value);
            if (customPortraitMappings == null) customPortraitMappings = new Dictionary<string, string>();
            Scribe_Values.Look(ref typingSoundVolume, "typingSoundVolume", 0.6f);
            Scribe_Values.Look(ref dialogFontSize, "dialogFontSize", 24f);
            Scribe_Values.Look(ref dialogButtonFontSize, "dialogButtonFontSize", 20f);
            Scribe_Values.Look(ref choiceButtonFontSize, "choiceButtonFontSize", 18f);
            Scribe_Values.Look(ref windowWidthScale, "windowWidthScale", 0.45f);
            Scribe_Values.Look(ref windowHeightScale, "windowHeightScale", 0.30f);
            Scribe_Values.Look(ref defaultSpeaker, "defaultSpeaker", DefaultSpeaker.Storyteller);
            Scribe_Collections.Look(ref storytellerNames, "storytellerNames", LookMode.Value, LookMode.Value);
            Scribe_Values.Look(ref showTraderPortrait, "showTraderPortrait", true);
            Scribe_Values.Look(ref showNegotiatorPortrait, "showNegotiatorPortrait", true);
            Scribe_Collections.Look(ref customStorytellerTypingSounds, "customStorytellerTypingSounds", LookMode.Value, LookMode.Value);
            
            Scribe_Values.Look(ref autoMode_CloseAtEnd, "autoMode_CloseAtEnd", true);
            
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (storytellerNames == null) storytellerNames = new Dictionary<string, string>();
                if (customStorytellerTypingSounds == null) customStorytellerTypingSounds = new Dictionary<string, string>();
            }
            
            base.ExposeData();
        }

        public void ResetToDefaults()
        {
            position = WindowPosition.Top;
            echoPosition = WindowPosition.Top;
            dontPauseOnOpen = true;
            onlyShowInGame = true;
            openWindowOnEvent = true;
            useFactionLeaderForOtherFactions = false;
            typingEffectEnabled = true;
            typingSoundEnabled = true;
            typingSpeed = 35f;
            typingSoundVolume = 0.6f;
            dialogFontSize = 24f;
            dialogButtonFontSize = 20f;
            choiceButtonFontSize = 18f;
            windowWidthScale = 0.45f;
            windowHeightScale = 0.30f;
            defaultSpeaker = DefaultSpeaker.Storyteller;
            showTraderPortrait = true;
            showNegotiatorPortrait = true;
            storytellerNames?.Clear();
            customStorytellerTypingSounds?.Clear();
            customPortraitMappings?.Clear();
            UIStyles.Reset();
        }
    }
}
