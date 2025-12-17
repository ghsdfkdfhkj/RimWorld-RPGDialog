using RimWorld;
using UnityEngine;
using Verse;

namespace RPGDialog
{
    public static class TypingTab
    {
        public static void Draw(Rect inRect, SettingsData settings)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            listing.CheckboxLabeled("RPDia_TypingEffect".Translate(), ref settings.typingEffectEnabled, "RPDia_TypingEffectTooltip".Translate());
            if (settings.typingEffectEnabled)
            {
                listing.Gap(6f);
                listing.CheckboxLabeled("RPDia_TypingSound".Translate(), ref settings.typingSoundEnabled, "RPDia_TypingSoundTooltip".Translate());
                if (settings.typingSoundEnabled)
                {
                    listing.Label("RPDia_TypingSoundVolume".Translate() + ": " + settings.typingSoundVolume.ToStringPercent());
                    settings.typingSoundVolume = listing.Slider(settings.typingSoundVolume, 0f, 1f);
                }
                
                listing.Gap(6f);
                listing.Label("RPDia_TypingSpeed".Translate() + ": " + Mathf.RoundToInt(settings.typingSpeed).ToString() + " " + "RPDia_CharsPerSecond".Translate());
                settings.typingSpeed = Widgets.HorizontalSlider(listing.GetRect(22f), settings.typingSpeed, 20f, 60f, roundTo: 1f);
            }

            listing.End();
        }
    }
}
