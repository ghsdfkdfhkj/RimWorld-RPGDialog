using System.Collections.Generic;
using UnityEngine;
using Verse;
using System.Linq;

namespace RPGDialog
{
	public static class PortraitLoader
	{
		private static readonly Dictionary<string, Texture2D> portraitCache = new Dictionary<string, Texture2D>();
        private static string manualTexturePath = null;

        private static string GetManualTexturePath()
        {
            if (manualTexturePath == null)
            {
                manualTexturePath = System.IO.Path.Combine(SettingsCore.ModContent.RootDir, "Textures", "UI", "Storyteller");
            }
            return manualTexturePath;
        }

        public static void ClearCache()
        {
            portraitCache.Clear();
        }

        public static List<string> GetAllManualPortraits()
        {
            var list = new List<string>();
            try
            {
                string path = GetManualTexturePath();
                if (System.IO.Directory.Exists(path))
                {
                    list.AddRange(System.IO.Directory.GetFiles(path, "*.png").Select(System.IO.Path.GetFileNameWithoutExtension));
                    list.AddRange(System.IO.Directory.GetFiles(path, "*.jpg").Select(System.IO.Path.GetFileNameWithoutExtension));
                }
            }
            catch {}
            return list;
        }

		public static Texture2D TryLoadCustomPortrait(string storytellerDefName)
		{
			if (portraitCache.TryGetValue(storytellerDefName, out Texture2D cachedPortrait)) return cachedPortrait;
            
            // Check for Mapping first
            string targetFileName = storytellerDefName;
            if (SettingsCore.settings.customPortraitMappings.TryGetValue(storytellerDefName, out string mapped))
            {
                targetFileName = mapped;
            }

            // Try loading manually from disk first (Hot Reload support)
            try 
            {
                string path = GetManualTexturePath();
                string pngPath = System.IO.Path.Combine(path, targetFileName + ".png");
                if (System.IO.File.Exists(pngPath))
                {
                    Texture2D tex = LoadTextureFromFile(pngPath);
                    if (tex != null)
                    {
                        portraitCache[storytellerDefName] = tex;
                        return tex;
                    }
                }
                string jpgPath = System.IO.Path.Combine(path, targetFileName + ".jpg");
                if (System.IO.File.Exists(jpgPath))
                {
                     Texture2D tex = LoadTextureFromFile(jpgPath);
                    if (tex != null)
                    {
                        portraitCache[storytellerDefName] = tex;
                        return tex;
                    }
                }
            }
            catch (System.Exception e)
            {
                Log.Warning($"[RPGDialog] Failed to load manual portrait for {targetFileName}: {e.Message}");
            }

            // Fallback to ContentFinder (Standard loading)
			string texturePath = $"UI/Storyteller/{targetFileName}";
			Texture2D portrait = ContentFinder<Texture2D>.Get(texturePath, reportFailure: false);
			portraitCache[storytellerDefName] = portrait; 
			return portrait;
		}

        private static Texture2D LoadTextureFromFile(string path)
        {
            byte[] fileData = System.IO.File.ReadAllBytes(path);
            Texture2D tex = new Texture2D(2, 2);
            tex.LoadImage(fileData); 
            tex.Compress(true);
            tex.filterMode = FilterMode.Trilinear;
            tex.anisoLevel = 2;
            tex.Apply(true, true);
            return tex;
        }
	}
}


