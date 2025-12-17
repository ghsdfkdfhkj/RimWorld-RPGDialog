using RimWorld;
using UnityEngine;
using Verse;
using System;
using System.Linq;

namespace RPGDialog
{
    public static class AppearanceTab
    {
        public static void Draw(Rect inRect, SettingsData settings)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            listing.CheckboxLabeled("RPDia_ShowTraderPortrait".Translate(), ref settings.showTraderPortrait, "RPDia_ShowTraderPortraitTooltip".Translate());
            listing.CheckboxLabeled("RPDia_ShowNegotiatorPortrait".Translate(), ref settings.showNegotiatorPortrait, "RPDia_ShowNegotiatorPortraitTooltip".Translate());
            listing.GapLine();

            listing.Label("RPDia_WindowPosition".Translate());
            if (listing.ButtonText(settings.position.ToString()))
            {
                FloatMenuUtility.MakeMenu(Enum.GetNames(typeof(WindowPosition)), (string str) => str, (string str) => () => { settings.position = (WindowPosition)Enum.Parse(typeof(WindowPosition), str); });
            }

            if (AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "EchoColony"))
            {
                listing.Gap(6f);
                listing.Label("RPDia_Echo_WindowPosition".Translate());
                if (listing.ButtonText(settings.echoPosition.ToString()))
                {
                    FloatMenuUtility.MakeMenu(Enum.GetNames(typeof(WindowPosition)), (string str) => str, (string str) => () => { settings.echoPosition = (WindowPosition)Enum.Parse(typeof(WindowPosition), str); });
                }
            }
            
            listing.Gap(6f);
            listing.Label("RPDia_DialogFontSize".Translate() + ": " + Mathf.RoundToInt(settings.dialogFontSize).ToString() + "px");
            settings.dialogFontSize = Widgets.HorizontalSlider(listing.GetRect(22f), settings.dialogFontSize, 12f, 28f, roundTo: 1f);
            
            listing.Gap(6f);
            listing.Label("RPDia_DialogButtonFontSize".Translate() + ": " + Mathf.RoundToInt(settings.dialogButtonFontSize).ToString() + "px");
            settings.dialogButtonFontSize = Widgets.HorizontalSlider(listing.GetRect(22f), settings.dialogButtonFontSize, 10f, 24f, roundTo: 1f);
            
            listing.Gap(6f);
            listing.Label("RPDia_ChoiceButtonFontSize".Translate() + ": " + Mathf.RoundToInt(settings.choiceButtonFontSize).ToString() + "px");
            settings.choiceButtonFontSize = Widgets.HorizontalSlider(listing.GetRect(22f), settings.choiceButtonFontSize, 10f, 24f, roundTo: 1f);
            listing.GapLine();

            listing.Label("RPDia_WindowWidth".Translate() + ": " + settings.windowWidthScale.ToStringPercent());
            settings.windowWidthScale = Widgets.HorizontalSlider(listing.GetRect(22f), settings.windowWidthScale, 0.25f, 0.8f);
            
            listing.Gap(6f);
            listing.Label("RPDia_WindowHeight".Translate() + ": " + settings.windowHeightScale.ToStringPercent());
            settings.windowHeightScale = Widgets.HorizontalSlider(listing.GetRect(22f), settings.windowHeightScale, 0.25f, 0.8f);

            listing.End();
        }
    }
}
