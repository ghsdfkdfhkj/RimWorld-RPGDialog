using RimWorld;
using UnityEngine;
using Verse;
using System;


namespace RPGDialog
{
    public static class GeneralTab
    {
        public static void Draw(Rect inRect, SettingsData settings)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            listing.CheckboxLabeled("RPDia_DontPause".Translate(), ref settings.dontPauseOnOpen, "RPDia_DontPauseTooltip".Translate());
            listing.CheckboxLabeled("RPDia_OnlyShowInGame".Translate(), ref settings.onlyShowInGame, "RPDia_OnlyShowInGameTooltip".Translate());
            listing.CheckboxLabeled("RPDia_OpenWindowOnEvent".Translate(), ref settings.openWindowOnEvent, "RPDia_OpenWindowOnEventTooltip".Translate());
            listing.GapLine();

            // Auto Mode Settings
            listing.Label("RPDia_AutoModeSettings".Translate());
            listing.CheckboxLabeled("RPDia_AutoMode_CloseAtEnd".Translate(), ref settings.autoMode_CloseAtEnd, "RPDia_AutoMode_CloseAtEndTooltip".Translate());
            listing.GapLine();

            listing.CheckboxLabeled("RPDia_UseOtherFactionNarrator".Translate(), ref settings.useFactionLeaderForOtherFactions, "RPDia_UseOtherFactionNarratorTooltip".Translate());

            if (ModsConfig.IdeologyActive)
            {
                listing.Gap(6f);
                listing.Label("RPDia_DefaultNarrator".Translate() + ": " + GetDefaultSpeakerLabel(settings.defaultSpeaker));
                if (listing.ButtonText(GetDefaultSpeakerLabel(settings.defaultSpeaker)))
                {
                    FloatMenuUtility.MakeMenu(
                        Enum.GetNames(typeof(DefaultSpeaker)),
                        (string str) => GetDefaultSpeakerLabel((DefaultSpeaker)Enum.Parse(typeof(DefaultSpeaker), str)),
                        (string str) => () => { settings.defaultSpeaker = (DefaultSpeaker)Enum.Parse(typeof(DefaultSpeaker), str); UIStyles.Reset(); });
                }
            }
            
            listing.End();
        }

        private static string GetDefaultSpeakerLabel(DefaultSpeaker speaker)
        {
            switch (speaker)
            {
                case DefaultSpeaker.Storyteller: return "Storyteller".Translate();
                case DefaultSpeaker.Leader: return PreceptDefOf.IdeoRole_Leader.LabelCap;
                case DefaultSpeaker.ReligiousLeader: return PreceptDefOf.IdeoRole_Moralist.LabelCap;
                default: return speaker.ToString();
            }
        }
    }
}
