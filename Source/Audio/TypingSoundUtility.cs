using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using RimWorld;
using RuntimeAudioClipLoader;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace RPGDialog
{
	public static class TypingSoundUtility
	{
		private static readonly Dictionary<string, SoundDef> soundCache = new Dictionary<string, SoundDef>();
		private static readonly Dictionary<string, string> soundPathCache = new Dictionary<string, string>();

		public static SoundDef GetTypingSound(Pawn speaker)
		{
			string soundKey = "Default";

			if (speaker != null)
			{
				// Pawn speaker
				if (PawnSoundSettings.Current != null && PawnSoundSettings.Current.customPawnTypingSounds.TryGetValue(speaker.ThingID, out string pawnSound))
				{
					soundKey = pawnSound;
				}
			}
			else
			{
				// Storyteller
				var storytellerDef = Find.Storyteller?.def;
				if (storytellerDef != null)
				{
					if (RPGDialogMod.settings.customStorytellerTypingSounds.TryGetValue(storytellerDef.defName, out string storytellerSound))
					{
						soundKey = storytellerSound;
					}
					else if (SoundFileExists(storytellerDef.defName, out _))
					{
						soundKey = storytellerDef.defName;
					}
				}
			}

			return GetSoundDef(soundKey);
		}

		public static SoundDef GetSoundDef(string soundKey)
        {
            if (string.IsNullOrEmpty(soundKey) || soundKey == "Default")
            {
                return SoundDefOf.Tick_Tiny;
            }

            if (soundCache.TryGetValue(soundKey, out SoundDef cachedSound))
            {
                return cachedSound;
            }

            if (SoundFileExists(soundKey, out string fullPath))
            {
                var managerType = AccessTools.TypeByName("RuntimeAudioClipLoader.Manager");
                if (managerType != null)
                {
                    var loadMethod = managerType.GetMethod("Load", new[] { typeof(string), typeof(bool), typeof(bool), typeof(bool) });
                    if (loadMethod != null)
                    {
                        AudioClip clip = (AudioClip)loadMethod.Invoke(null, new object[] { fullPath, false, false, false });
                        if (clip != null)
                        {
                            var subSound = new SubSoundDef
                            {
                                onCamera = true,
                                volumeRange = new FloatRange(50f, 50f)
                            };
                            var resolvedGrain = new ResolvedGrain_Clip(clip);
                            var resolvedGrainsList = Traverse.Create(subSound).Field("resolvedGrains").GetValue<List<ResolvedGrain>>();
                            resolvedGrainsList.Add(resolvedGrain);

                            var newSoundDef = new SoundDef
                            {
                                defName = $"RPDia_Dynamic_{soundKey}",
                                sustain = false,
                                context = SoundContext.Any,
                                maxSimultaneous = 4,
                                subSounds = new List<SubSoundDef> { subSound }
                            };
                            subSound.parentDef = newSoundDef;
                            soundCache[soundKey] = newSoundDef;
                            return newSoundDef;
                        }
                    }
                }
            }
            return SoundDefOf.Tick_Tiny;
        }

		public static void PlayPreviewSound(string soundKey)
        {
            SoundDef sound = GetSoundDef(soundKey);
            if (sound != null)
            {
                SoundInfo info = SoundInfo.OnCamera(MaintenanceType.None);
                info.volumeFactor = RPGDialogMod.settings.typingSoundVolume;
                sound.PlayOneShot(info);
            }
        }

		private static bool SoundFileExists(string key, out string fullPath)
		{
			if (soundPathCache.TryGetValue(key, out fullPath))
			{
				return fullPath != null;
			}
			string typingSoundPath = Path.Combine(RPGDialogMod.ModContent.RootDir, "Sounds", "Typing").Replace('\\', '/');
			string wav = Path.Combine(typingSoundPath, key + ".wav").Replace('\\', '/');
			if (File.Exists(wav))
			{
				fullPath = wav;
				soundPathCache[key] = fullPath;
				return true;
			}
			string ogg = Path.Combine(typingSoundPath, key + ".ogg").Replace('\\', '/');
			if (File.Exists(ogg))
			{
				fullPath = ogg;
				soundPathCache[key] = fullPath;
				return true;
			}
			fullPath = null;
			soundPathCache[key] = null;
			return false;
		}
	}
}


