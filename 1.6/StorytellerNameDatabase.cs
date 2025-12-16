using Verse;
using RimWorld;

namespace RPGDialog
{
    public static class StorytellerNameDatabase
    {
        public static string GetStorytellerName(StorytellerDef def)
        {
            if (RPGDialogMod.settings.storytellerNames.TryGetValue(def.defName, out string customName) && !string.IsNullOrEmpty(customName))
            {
                return customName;
            }
            return def.label;
        }

        public static void SetStorytellerName(StorytellerDef def, string name)
        {
            if (string.IsNullOrEmpty(name) || name == def.label)
            {
                RPGDialogMod.settings.storytellerNames.Remove(def.defName);
            }
            else
            {
                RPGDialogMod.settings.storytellerNames[def.defName] = name;
            }
        }
    }
}
