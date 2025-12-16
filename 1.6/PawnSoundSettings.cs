
using System.Collections.Generic;
using RimWorld.Planet;
using Verse;

namespace RPGDialog
{
    public class PawnSoundSettings : GameComponent
    {
        private static PawnSoundSettings current;
        public Dictionary<string, string> customPawnTypingSounds = new Dictionary<string, string>();

        public PawnSoundSettings(Game game)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref customPawnTypingSounds, "customPawnTypingSounds", LookMode.Value, LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (customPawnTypingSounds == null)
                {
                    customPawnTypingSounds = new Dictionary<string, string>();
                }
            }
        }

        public static PawnSoundSettings Current
        {
            get
            {
                if (current == null)
                {
                    current = Verse.Current.Game.GetComponent<PawnSoundSettings>();
                    if (current == null)
                    {
                        current = new PawnSoundSettings(Verse.Current.Game);
                        Verse.Current.Game.components.Add(current);
                    }
                }

                return current;
            }
        }
        
        public static void Reset()
        {
            current = null;
        }

        public override void GameComponentTick()
        {
            if (current == null)
            {
                current = this;
            }
        }

        public static string GetPawnTypingSound(Pawn pawn)
        {
            if (pawn != null && Current != null && Current.customPawnTypingSounds.TryGetValue(pawn.ThingID, out var sound))
            {
                return sound;
            }
            return null;
        }

        public static void SetPawnTypingSound(Pawn pawn, string sound)
        {
            if (pawn == null || Current == null) return;
            if (sound == "Default")
            {
                if (Current.customPawnTypingSounds.ContainsKey(pawn.ThingID))
                {
                    Current.customPawnTypingSounds.Remove(pawn.ThingID);
                }
            }
            else
            {
                Current.customPawnTypingSounds[pawn.ThingID] = sound;
            }
        }
    }
}
