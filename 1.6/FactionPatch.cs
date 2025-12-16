using System;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RPGDialog
{
    // Suppress vanilla faction drawing only while vanilla Dialog_NodeTreeWithFactionInfo.DoWindowContents runs.
    public static class FactionPatch
    {
        [ThreadStatic]
        private static bool s_suppressDuringVanillaDialog;

        [HarmonyPatch(typeof(Dialog_NodeTreeWithFactionInfo), "DoWindowContents")]
        public static class Dialog_NodeTreeWithFactionInfo_DoWindowContents_PrefixPostfix
        {
            public static void Prefix(Dialog_NodeTreeWithFactionInfo __instance, Rect inRect)
            {
                s_suppressDuringVanillaDialog = true;
            }

            public static void Postfix(Dialog_NodeTreeWithFactionInfo __instance)
            {
                s_suppressDuringVanillaDialog = false;
            }
        }

        [HarmonyPatch(typeof(FactionUIUtility), "DrawRelatedFactionInfo")]
        public static class FactionUIUtility_DrawRelatedFactionInfo_Prefix
        {
            public static bool Prefix(Rect rect, Faction faction, ref float curY)
            {
                try
                {
                    if (s_suppressDuringVanillaDialog)
                    {
                        // Skip vanilla faction drawing only when we're inside vanilla Dialog_NodeTreeWithFactionInfo
                        return false;
                    }
                }
                catch { }
                return true;
            }
        }
    }
}


