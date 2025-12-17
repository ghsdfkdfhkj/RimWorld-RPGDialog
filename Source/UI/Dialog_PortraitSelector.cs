using System.Collections.Generic;
using UnityEngine;
using Verse;
using RimWorld;

namespace RPGDialog
{
    public class Dialog_PortraitSelector : Window
    {
        private string mappingKey;
        private string label;
        private List<string> availablePortraits;
        private Vector2 scrollPosition;
        private const float ItemSize = 120f;
        private const float Spacing = 10f;

        public override Vector2 InitialSize => new Vector2(600f, 600f);

        public Dialog_PortraitSelector(string mappingKey, string label)
        {
            this.mappingKey = mappingKey;
            this.label = label;
            this.availablePortraits = PortraitLoader.GetAllManualPortraits();
            this.doCloseX = true;
            this.forcePause = true;
            this.absorbInputAroundWindow = true;
        }

        public Dialog_PortraitSelector(StorytellerDef storyteller) : this(storyteller.defName, storyteller.label) { }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, inRect.width, 30f), "RPDia_SelectPortraitFor".Translate(label));
            Text.Font = GameFont.Small;

            if (Widgets.ButtonText(new Rect(0, 40f, 150f, 30f), "RPDia_ResetToDefault".Translate()))
            {
                 SettingsCore.settings.customPortraitMappings.Remove(mappingKey);
                 PortraitLoader.ClearCache();
                 Close();
            }

            Rect listRect = new Rect(0, 80f, inRect.width, inRect.height - 80f);
            
            int columns = Mathf.FloorToInt((listRect.width - 16f) / (ItemSize + Spacing));
            if (columns < 1) columns = 1;
            
            int rows = Mathf.CeilToInt((float)availablePortraits.Count / columns);
            float viewHeight = rows * (ItemSize + Spacing);

            Rect viewRect = new Rect(0, 0, listRect.width - 16f, viewHeight);
            
            Widgets.BeginScrollView(listRect, ref scrollPosition, viewRect);
            
            for (int i = 0; i < availablePortraits.Count; i++)
            {
                string portraitName = availablePortraits[i];
                int col = i % columns;
                int row = i / columns;
                
                Rect itemRect = new Rect(col * (ItemSize + Spacing), row * (ItemSize + Spacing), ItemSize, ItemSize);
                
                // Draw Texture
                // We fake a "defName" load by passing the filename directly to the loader, 
                // but since PortraitLoader now uses mapping, we need to be careful.
                // Actually, PortraitLoader.TryLoadCustomPortrait logic expects a DefName and looks for DefName.png
                // We want to verify what "portraitName" looks like.
                // Let's just load it raw for the preview using a temp mapped call or helper.
                // Better: Create a helper in PortraitLoader to load by specific name, or just hijack the logic.
                
                // We'll temporarily map it for preview? No, that's messy.
                // Let's use the actual file path for preview.
                // Accessing private/internal logic would be cleaner if exposed.
                // Let's assume TryLoadCustomPortrait works if we pass the filename as if it were a defname, 
                // AND there is no mapping for that filename key. 
                
                // Use GetRawPortrait to bypass any mappings and see the actual file content
                Texture2D tex = PortraitLoader.GetRawPortrait(portraitName);
                if (tex != null)
                {
                    GUI.DrawTexture(itemRect, tex, ScaleMode.ScaleToFit);
                }
                
                Widgets.DrawHighlightIfMouseover(itemRect);
                if (Widgets.ButtonInvisible(itemRect))
                {
                    SettingsCore.settings.customPortraitMappings[mappingKey] = portraitName;
                    PortraitLoader.ClearCache();
                    Close();
                }
                
                Rect labelRect = new Rect(itemRect.x, itemRect.yMax - 20f, itemRect.width, 20f);
                Widgets.DrawBoxSolid(labelRect, new Color(0, 0, 0, 0.5f));
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(labelRect, portraitName);
                Text.Anchor = TextAnchor.UpperLeft;
            }

            Widgets.EndScrollView();
        }
    }
}
