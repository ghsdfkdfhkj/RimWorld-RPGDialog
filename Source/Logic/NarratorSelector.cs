using System;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;

namespace RPGDialog
{
    public static class NarratorSelector
    {
        // Resolve speaker pawn, speaker name and portrait to use in dialog windows
        public static Pawn ResolveNarratorPawn(Dialog_NodeTree windowInstance, Pawn originalSpeaker, Faction factionToDisplay, out string speakerName, out Texture portrait)
        {
            speakerName = null;
            portrait = null;

            Pawn chosen = originalSpeaker;

            // If the message is from another faction and the setting is enabled, its leader should be the speaker.
            if (RPGDialogMod.settings.useFactionLeaderForOtherFactions && factionToDisplay != null && !factionToDisplay.IsPlayer && factionToDisplay.leader != null)
            {
                chosen = factionToDisplay.leader;
            }
            else
            {
                // Only check player's settings if it's not a message from another faction's leader
                var defaultSpeaker = RPGDialogMod.settings.defaultSpeaker;

                if (defaultSpeaker == DefaultSpeaker.Leader)
                {
                    Pawn leader = Faction.OfPlayer.leader;
                    if (leader != null)
                    {
                        chosen = leader;
                    }
                }
                else if (defaultSpeaker == DefaultSpeaker.ReligiousLeader)
                {
                    Pawn religiousLeader = null;
                    Ideo ideo = Faction.OfPlayer.ideos.PrimaryIdeo;
                    if (ideo != null)
                    {
                        foreach (Precept_Role role in ideo.RolesListForReading)
                        {
                            if (role.def == PreceptDefOf.IdeoRole_Moralist)
                            {
                                religiousLeader = role.ChosenPawns().FirstOrDefault();
                                if (religiousLeader != null)
                                {
                                    break;
                                }
                            }
                        }
                    }

                    if (religiousLeader != null)
                    {
                        chosen = religiousLeader;
                    }
                }
            }

            if (chosen != null)
            {
                speakerName = chosen.LabelShort;
                portrait = PortraitsCache.Get(chosen, new Vector2(150f, 200f), Rot4.South, default(Vector3), 1.0f);
                return chosen;
            }

            // No pawn chosen -> use storyteller if available
            var storytellerDef = Find.Storyteller?.def;
            if (storytellerDef != null)
            {
                speakerName = StorytellerNameDatabase.GetStorytellerName(storytellerDef);
                portrait = PortraitLoader.TryLoadCustomPortrait(storytellerDef.defName) ?? storytellerDef.portraitLargeTex;
            }
            else
            {
                speakerName = "RPDia_Narrator".Translate();
                portrait = null;
            }

            return null;
        }
    }
}


