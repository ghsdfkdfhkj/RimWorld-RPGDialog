using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RPGDialog
{
	public static class PortraitLoader
	{
		private static readonly Dictionary<string, Texture2D> portraitCache = new Dictionary<string, Texture2D>();

		public static Texture2D TryLoadCustomPortrait(string storytellerDefName)
		{
			if (portraitCache.TryGetValue(storytellerDefName, out Texture2D cachedPortrait)) return cachedPortrait;
			string texturePath = $"UI/Storyteller/{storytellerDefName}";
			Texture2D portrait = ContentFinder<Texture2D>.Get(texturePath, reportFailure: false);
			portraitCache[storytellerDefName] = portrait; // can be null, cache miss
			return portrait;
		}
	}
}


