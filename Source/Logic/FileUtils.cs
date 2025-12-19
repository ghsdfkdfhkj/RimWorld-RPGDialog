using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using Verse;

namespace RPGDialog
{
    public static class FileUtils
    {
        public static void OpenDirectory(string path)
        {
            if (string.IsNullOrEmpty(path)) return;

            if (!Directory.Exists(path))
            {
                try
                {
                    Directory.CreateDirectory(path);
                }
                catch (Exception e)
                {
                    Log.Error($"[RPGDialog] Could not create directory {path}: {e.Message}");
                    return;
                }
            }

            try
            {
                if (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor)
                {
                    Process.Start(path);
                }
                else if (Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.OSXEditor)
                {
                    Process.Start("open", $"\"{path}\"");
                }
                else // Linux and others
                {
                    Process.Start("xdg-open", $"\"{path}\"");
                }
            }
            catch (Exception e)
            {
                Log.Warning($"[RPGDialog] Failed to open directory {path}: {e.Message}");
            }
        }

        public static string FindFileCaseInsensitive(string directory, string filename)
        {
            if (!Directory.Exists(directory)) return null;

            // First check strictly (fast path for Windows/correctly named files)
            string exactPath = Path.Combine(directory, filename);
            if (File.Exists(exactPath)) return exactPath;

            // Iterate to find case-insensitive match
            try
            {
                foreach (var file in Directory.GetFiles(directory))
                {
                    string name = Path.GetFileName(file);
                    if (name.Equals(filename, StringComparison.OrdinalIgnoreCase))
                    {
                        return file;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warning($"[RPGDialog] Error searching for file {filename} in {directory}: {e.Message}");
            }

            return null;
        }
    }
}
