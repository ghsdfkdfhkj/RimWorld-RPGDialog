using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using System;
using System.Linq;
using System.Collections.Generic;

namespace RPGDialog
{
    public enum WindowPosition { Top, Middle, Bottom }
    public enum DefaultSpeaker { Storyteller, Leader, ReligiousLeader }

    public class RPGDialogSettings : ModSettings
    {
        public WindowPosition position = WindowPosition.Top;
        public WindowPosition echoPosition = WindowPosition.Top;
        public bool dontPauseOnOpen = true;
        public bool openWindowOnEvent = true;
        public bool useFactionLeaderForOtherFactions = false;
        public bool typingEffectEnabled = true;
        public bool typingSoundEnabled = true;
        public float typingSpeed = 35f;
        public float typingSoundVolume = 0.6f;
        public float dialogFontSize = 24f;
        public float dialogButtonFontSize = 20f;
        public float choiceButtonFontSize = 18f;
        public float windowWidthScale = 0.45f;
        public float windowHeightScale = 0.30f;
        public DefaultSpeaker defaultSpeaker = DefaultSpeaker.Storyteller;
        public Dictionary<string, string> storytellerNames = new Dictionary<string, string>();
        public bool showTraderPortrait = true;
        public bool showNegotiatorPortrait = true;
        public Dictionary<string, string> customStorytellerTypingSounds = new Dictionary<string, string>();

        public override void ExposeData()
        {
            Scribe_Values.Look(ref position, "position", WindowPosition.Top);
            Scribe_Values.Look(ref echoPosition, "echoPosition", WindowPosition.Top);
            Scribe_Values.Look(ref dontPauseOnOpen, "dontPauseOnOpen", true);
            Scribe_Values.Look(ref openWindowOnEvent, "openWindowOnEvent", true);
            Scribe_Values.Look(ref useFactionLeaderForOtherFactions, "useFactionLeaderForOtherFactions", false);
            Scribe_Values.Look(ref typingEffectEnabled, "typingEffectEnabled", true);
            Scribe_Values.Look(ref typingSoundEnabled, "typingSoundEnabled", true);
            Scribe_Values.Look(ref typingSpeed, "typingSpeed", 35f);
            Scribe_Values.Look(ref typingSoundVolume, "typingSoundVolume", 0.6f);
            Scribe_Values.Look(ref dialogFontSize, "dialogFontSize", 24f);
            Scribe_Values.Look(ref dialogButtonFontSize, "dialogButtonFontSize", 20f);
            Scribe_Values.Look(ref choiceButtonFontSize, "choiceButtonFontSize", 18f);
            Scribe_Values.Look(ref windowWidthScale, "windowWidthScale", 0.45f);
            Scribe_Values.Look(ref windowHeightScale, "windowHeightScale", 0.30f);
            Scribe_Values.Look(ref defaultSpeaker, "defaultSpeaker", DefaultSpeaker.Storyteller);
            Scribe_Collections.Look(ref storytellerNames, "storytellerNames", LookMode.Value, LookMode.Value);
            Scribe_Values.Look(ref showTraderPortrait, "showTraderPortrait", true);
            Scribe_Values.Look(ref showNegotiatorPortrait, "showNegotiatorPortrait", true);
            Scribe_Collections.Look(ref customStorytellerTypingSounds, "customStorytellerTypingSounds", LookMode.Value, LookMode.Value);
            
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (storytellerNames == null) storytellerNames = new Dictionary<string, string>();
                if (customStorytellerTypingSounds == null) customStorytellerTypingSounds = new Dictionary<string, string>();
            }
            
            base.ExposeData();
        }

        public void ResetToDefaults()
        {
            position = WindowPosition.Top;
            echoPosition = WindowPosition.Top;
            dontPauseOnOpen = true;
            openWindowOnEvent = true;
            useFactionLeaderForOtherFactions = false;
            typingEffectEnabled = true;
            typingSoundEnabled = true;
            typingSpeed = 35f;
            typingSoundVolume = 0.6f;
            dialogFontSize = 24f;
            dialogButtonFontSize = 20f;
            choiceButtonFontSize = 18f;
            windowWidthScale = 0.45f;
            windowHeightScale = 0.30f;
            defaultSpeaker = DefaultSpeaker.Storyteller;
            showTraderPortrait = true;
            showNegotiatorPortrait = true;
            storytellerNames?.Clear();
            customStorytellerTypingSounds?.Clear();
            UIStyles.Reset();
        }
    }

    public class RPGDialogMod : Mod
    {
        public static RPGDialogSettings settings;
        public static ModContentPack ModContent { get; private set; }
        private Vector2 leftScrollPosition = Vector2.zero;
        private Vector2 storytellerNameScrollPosition = Vector2.zero;
        private Vector2 entityScrollPosition = Vector2.zero;

        private List<string> availableSounds;
        private List<StorytellerDef> availableStorytellers;
        private SortedDictionary<string, List<Pawn>> pawnsByFaction;
        private string selectedDefName;
        private string searchQuery = "";
        
        private bool storytellersExpanded = false;
        private Dictionary<string, bool> factionExpansionStates = new Dictionary<string, bool>();
        private bool staticContentLoaded = false;
        private Dictionary<string, bool> soundFileExistsCache = new Dictionary<string, bool>();

        private bool SoundFileExistsFor(string key)
        {
            if (soundFileExistsCache.TryGetValue(key, out bool exists))
            {
                return exists;
            }

            string typingSoundPath = System.IO.Path.Combine(ModContent.RootDir, "Sounds", "Typing");
            bool fileExists = System.IO.File.Exists(System.IO.Path.Combine(typingSoundPath, key + ".wav")) ||
                              System.IO.File.Exists(System.IO.Path.Combine(typingSoundPath, key + ".ogg"));
        
            soundFileExistsCache[key] = fileExists;
            return fileExists;
        }

        private string GetEffectiveStorytellerSound(StorytellerDef def)
        {
            if (settings.customStorytellerTypingSounds.TryGetValue(def.defName, out string customSound))
            {
                return customSound;
            }
            if (SoundFileExistsFor(def.defName))
            {
                return def.defName;
            }
            return "Default";
        }
        
        public RPGDialogMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<RPGDialogSettings>();
            if (settings.customStorytellerTypingSounds == null)
            {
                settings.customStorytellerTypingSounds = new Dictionary<string, string>();
            }
            ModContent = content;
        }

        private void InitializeStaticContentIfNeeded()
        {
            if (staticContentLoaded) return;

            availableSounds = new List<string> { "Default" };
            try
            {
                string soundsPath = System.IO.Path.Combine(ModContent.RootDir, "Sounds", "Typing");
                if (System.IO.Directory.Exists(soundsPath))
                {
                    availableSounds.AddRange(System.IO.Directory.GetFiles(soundsPath, "*.wav")
                        .Select(System.IO.Path.GetFileNameWithoutExtension));
                }
            }
            catch (Exception e) { Log.Error($"Error loading custom typing sounds: {e.Message}"); }
            
            availableStorytellers = DefDatabase<StorytellerDef>.AllDefsListForReading;
            
            staticContentLoaded = true;
        }

        private void RefreshPawnList()
        {
            pawnsByFaction = new SortedDictionary<string, List<Pawn>>();
            
            if (Current.Game == null || Find.World == null) return;

            // Optimized: Avoid LINQ .Where().ToList() to reduce allocations.
            foreach (var pawn in PawnsFinder.AllMapsAndWorld_Alive)
            {
                if (!(pawn.RaceProps?.Humanlike ?? false)) continue;
                
                string factionLabel = pawn.Faction?.Name ?? "RPDia_NoFaction".Translate();
                if (!pawnsByFaction.ContainsKey(factionLabel))
                {
                    pawnsByFaction[factionLabel] = new List<Pawn>();
                    if (!factionExpansionStates.ContainsKey(factionLabel))
                    {
                        factionExpansionStates[factionLabel] = false;
                    }
                }
                pawnsByFaction[factionLabel].Add(pawn);
            }
        }

        private string GetDefaultSpeakerLabel(DefaultSpeaker speaker)
        {
            switch (speaker)
            {
                case DefaultSpeaker.Storyteller: return "Storyteller".Translate();
                case DefaultSpeaker.Leader: return PreceptDefOf.IdeoRole_Leader.LabelCap;
                case DefaultSpeaker.ReligiousLeader: return PreceptDefOf.IdeoRole_Moralist.LabelCap;
                default: return speaker.ToString();
            }
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            InitializeStaticContentIfNeeded();
            if (Current.Game != null)
            {
                RefreshPawnList();
            }
            
            Color darkHeaderColor = new Color(0.12f, 0.12f, 0.12f);
            Color darkHeaderBorderColor = new Color(0.35f, 0.35f, 0.35f);

            // ##### 왼쪽 영역 #####
            Rect leftOutRect = new Rect(inRect.x, inRect.y, inRect.width * 0.48f, inRect.height);
            // Estimate the total height of the content. A bit of extra space is fine.
            float leftScrollViewHeight = 750f; 
            Rect leftViewRect = new Rect(0f, 0f, leftOutRect.width - 16f, leftScrollViewHeight);

            Widgets.BeginScrollView(leftOutRect, ref leftScrollPosition, leftViewRect);

            Listing_Standard leftListing = new Listing_Standard();
            leftListing.Begin(leftViewRect);

            leftListing.CheckboxLabeled("RPDia_DontPause".Translate(), ref settings.dontPauseOnOpen, "RPDia_DontPauseTooltip".Translate());
            leftListing.CheckboxLabeled("RPDia_OpenWindowOnEvent".Translate(), ref settings.openWindowOnEvent, "RPDia_OpenWindowOnEventTooltip".Translate());
            leftListing.GapLine();

            leftListing.CheckboxLabeled("RPDia_UseOtherFactionNarrator".Translate(), ref settings.useFactionLeaderForOtherFactions, "RPDia_UseOtherFactionNarratorTooltip".Translate());
            leftListing.CheckboxLabeled("RPDia_ShowTraderPortrait".Translate(), ref settings.showTraderPortrait, "RPDia_ShowTraderPortraitTooltip".Translate());
            leftListing.CheckboxLabeled("RPDia_ShowNegotiatorPortrait".Translate(), ref settings.showNegotiatorPortrait, "RPDia_ShowNegotiatorPortraitTooltip".Translate());
            
            if (ModsConfig.IdeologyActive)
            {
                leftListing.Gap(6f);
                leftListing.Label("RPDia_DefaultNarrator".Translate() + ": " + GetDefaultSpeakerLabel(settings.defaultSpeaker));
                if (leftListing.ButtonText(GetDefaultSpeakerLabel(settings.defaultSpeaker)))
                {
                    FloatMenuUtility.MakeMenu(
                        Enum.GetNames(typeof(DefaultSpeaker)),
                        (string str) => GetDefaultSpeakerLabel((DefaultSpeaker)Enum.Parse(typeof(DefaultSpeaker), str)),
                        (string str) => () => { settings.defaultSpeaker = (DefaultSpeaker)Enum.Parse(typeof(DefaultSpeaker), str); UIStyles.Reset(); });
                }
            }
            leftListing.GapLine();

            leftListing.Label("RPDia_WindowPosition".Translate());
            if (leftListing.ButtonText(settings.position.ToString()))
            {
                FloatMenuUtility.MakeMenu(Enum.GetNames(typeof(WindowPosition)), (string str) => str, (string str) => () => { settings.position = (WindowPosition)Enum.Parse(typeof(WindowPosition), str); });
            }

            if (AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "EchoColony"))
            {
                leftListing.Gap(6f);
                leftListing.Label("RPDia_Echo_WindowPosition".Translate());
                if (leftListing.ButtonText(settings.echoPosition.ToString()))
                {
                    FloatMenuUtility.MakeMenu(Enum.GetNames(typeof(WindowPosition)), (string str) => str, (string str) => () => { settings.echoPosition = (WindowPosition)Enum.Parse(typeof(WindowPosition), str); });
                }
            }
            
            leftListing.Gap(6f);
            leftListing.Label("RPDia_DialogFontSize".Translate() + ": " + Mathf.RoundToInt(settings.dialogFontSize).ToString() + "px");
            settings.dialogFontSize = Widgets.HorizontalSlider(leftListing.GetRect(22f), settings.dialogFontSize, 12f, 28f, roundTo: 1f);
            
            leftListing.Gap(6f);
            leftListing.Label("RPDia_DialogButtonFontSize".Translate() + ": " + Mathf.RoundToInt(settings.dialogButtonFontSize).ToString() + "px");
            settings.dialogButtonFontSize = Widgets.HorizontalSlider(leftListing.GetRect(22f), settings.dialogButtonFontSize, 10f, 24f, roundTo: 1f);
            
            leftListing.Gap(6f);
            leftListing.Label("RPDia_ChoiceButtonFontSize".Translate() + ": " + Mathf.RoundToInt(settings.choiceButtonFontSize).ToString() + "px");
            settings.choiceButtonFontSize = Widgets.HorizontalSlider(leftListing.GetRect(22f), settings.choiceButtonFontSize, 10f, 24f, roundTo: 1f);
            leftListing.GapLine();

            leftListing.Label("RPDia_WindowWidth".Translate() + ": " + settings.windowWidthScale.ToStringPercent());
            settings.windowWidthScale = Widgets.HorizontalSlider(leftListing.GetRect(22f), settings.windowWidthScale, 0.25f, 0.8f);
            
            leftListing.Gap(6f);
            leftListing.Label("RPDia_WindowHeight".Translate() + ": " + settings.windowHeightScale.ToStringPercent());
            settings.windowHeightScale = Widgets.HorizontalSlider(leftListing.GetRect(22f), settings.windowHeightScale, 0.25f, 0.8f);
            leftListing.GapLine();

            leftListing.CheckboxLabeled("RPDia_TypingEffect".Translate(), ref settings.typingEffectEnabled, "RPDia_TypingEffectTooltip".Translate());
            if (settings.typingEffectEnabled)
            {
                leftListing.Gap(6f);
                leftListing.CheckboxLabeled("RPDia_TypingSound".Translate(), ref settings.typingSoundEnabled, "RPDia_TypingSoundTooltip".Translate());
                if (settings.typingSoundEnabled)
                {
                    leftListing.Label("RPDia_TypingSoundVolume".Translate() + ": " + settings.typingSoundVolume.ToStringPercent());
                    settings.typingSoundVolume = leftListing.Slider(settings.typingSoundVolume, 0f, 1f);
                }
                
                leftListing.Gap(6f);
                leftListing.Label("RPDia_TypingSpeed".Translate() + ": " + Mathf.RoundToInt(settings.typingSpeed).ToString() + " " + "RPDia_CharsPerSecond".Translate());
                settings.typingSpeed = Widgets.HorizontalSlider(leftListing.GetRect(22f), settings.typingSpeed, 20f, 60f, roundTo: 1f);
            }

            leftListing.Gap(24f);

            if (leftListing.ButtonText("RPDia_ResetSettings".Translate()))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("RPDia_ResetSettings_Confirm".Translate(), () =>
                {
                    settings.ResetToDefaults();
                }, true));
            }

            leftListing.End();
            Widgets.EndScrollView();

            // ##### 오른쪽 영역 #####
            Rect rightRect = new Rect(inRect.x + inRect.width * 0.52f, inRect.y, inRect.width * 0.48f, inRect.height);
            
            float storytellerHeight = inRect.height * 0.4f;
            Rect storytellerRect = new Rect(rightRect.x, rightRect.y, rightRect.width, storytellerHeight);
            float gap = 12f; // <-- 간격을 24f에서 12f로 줄였습니다.
            Rect soundSettingsRect = new Rect(rightRect.x, rightRect.y + storytellerHeight + gap, rightRect.width, inRect.height - storytellerHeight - gap);

            // --- 스토리텔러 이름 설정 (위) ---
            Widgets.Label(new Rect(storytellerRect.x, storytellerRect.y, storytellerRect.width, 30f), "RPDia_StorytellerNameSettings".Translate());
            Rect scrollContainerRect = new Rect(storytellerRect.x, storytellerRect.y + 30f, storytellerRect.width, storytellerRect.height - 30f);
            
            var storytellerDefs = DefDatabase<StorytellerDef>.AllDefs.ToList();
            float storytellerContentHeight = storytellerDefs.Count * 32f;
            Rect viewRect = new Rect(0, 0, scrollContainerRect.width - 16f, storytellerContentHeight);

            Widgets.BeginScrollView(scrollContainerRect, ref storytellerNameScrollPosition, viewRect);
            Listing_Standard innerStorytellerListing = new Listing_Standard();
            innerStorytellerListing.Begin(viewRect);
            foreach (var storytellerDef in storytellerDefs)
            {
                string currentName = StorytellerNameDatabase.GetStorytellerName(storytellerDef);
                string newName = innerStorytellerListing.TextEntryLabeled(storytellerDef.label, currentName);
                if (newName != currentName)
                {
                    StorytellerNameDatabase.SetStorytellerName(storytellerDef, newName);
                }
            }
            innerStorytellerListing.End();
            Widgets.EndScrollView();
            
            // --- 커스텀 타이핑 사운드 설정 (아래) ---
            Widgets.Label(new Rect(soundSettingsRect.x, soundSettingsRect.y, soundSettingsRect.width, 30f), "RPDia_CustomTypingSoundSettings".Translate());
            
            float buttonHeight = 30f;
            float boxHeight = soundSettingsRect.height - 30f - buttonHeight - 12f - 36f; // Adjusted for the extra button
            Rect boxRect = new Rect(soundSettingsRect.x, soundSettingsRect.y + 30f, soundSettingsRect.width, boxHeight);
            Widgets.DrawBox(boxRect);

            Rect innerBoxRect = boxRect.ContractedBy(10f);
            float leftColumnWidth = innerBoxRect.width / 2 - 5f;

            Rect searchRect = new Rect(innerBoxRect.x, innerBoxRect.y, leftColumnWidth, 24f);
            Rect textFieldRect = new Rect(searchRect.x, searchRect.y, searchRect.width - 24f, searchRect.height);
            Rect clearButtonRect = new Rect(textFieldRect.xMax, searchRect.y, 24f, 24f);

            searchQuery = Widgets.TextField(textFieldRect, searchQuery);

            if (!string.IsNullOrEmpty(searchQuery))
            {
                if (Widgets.ButtonImage(clearButtonRect, TexButton.CloseXSmall))
                {
                    searchQuery = "";
                    GUI.FocusControl(null);
                    SoundDefOf.Tick_Low.PlayOneShotOnCamera(null);
                }
            }

            Rect entityListRect = new Rect(innerBoxRect.x, searchRect.yMax + 4f, leftColumnWidth, innerBoxRect.height - searchRect.height - 4f);
            Rect soundListRect = new Rect(innerBoxRect.x + innerBoxRect.width / 2 + 5f, innerBoxRect.y, innerBoxRect.width / 2 - 5f, innerBoxRect.height);

            // --- 개체 목록 (아코디언 UI) ---
            var filteredPawnsByFaction = new SortedDictionary<string, List<Pawn>>();
            if (pawnsByFaction != null)
            {
                // Optimized: Avoid LINQ in a frequently-called UI method.
                foreach (var entry in pawnsByFaction)
                {
                    if (string.IsNullOrEmpty(searchQuery))
                    {
                        filteredPawnsByFaction[entry.Key] = entry.Value;
                        continue;
                    }
                    
                    List<Pawn> filteredPawns = null; // Lazy initialization
                    foreach (var pawn in entry.Value)
                    {
                        if (pawn.Name.ToStringShort.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            if (filteredPawns == null) filteredPawns = new List<Pawn>();
                            filteredPawns.Add(pawn);
                        }
                    }

                    if (filteredPawns != null)
                    {
                        filteredPawnsByFaction[entry.Key] = filteredPawns;
                    }
                }
            }

            float availableWidthForText = entityListRect.width - 16f;
            float minItemHeight = 30f;
            float entityViewHeight = 0f;

            string storytellerHeaderText = $"{"Storyteller".Translate()} ({availableStorytellers.Count}) {(storytellersExpanded ? "▲" : "▼")}";
            float storytellerHeaderHeight = Mathf.Max(minItemHeight, Text.CalcHeight(storytellerHeaderText, availableWidthForText - 10f));
            entityViewHeight += storytellerHeaderHeight;

            if (storytellersExpanded)
            {
                foreach (var storyteller in availableStorytellers)
                {
                    string effectiveSound = GetEffectiveStorytellerSound(storyteller);
                    string label = $"  {storyteller.LabelCap} ({effectiveSound})";
                    entityViewHeight += Mathf.Max(minItemHeight, Text.CalcHeight(label, availableWidthForText - 10f));
                }
            }
            entityViewHeight += 12f; // GapLine

            foreach (var entry in filteredPawnsByFaction)
            {
                string factionLabel = entry.Key;
                List<Pawn> pawns = entry.Value;
                bool isExpanded = factionExpansionStates.TryGetValue(factionLabel, out bool expanded) && expanded;
                string factionHeaderText = $"  {factionLabel} ({pawns.Count}) {(isExpanded ? "▲" : "▼")}";
                float factionHeaderHeight = Mathf.Max(minItemHeight, Text.CalcHeight(factionHeaderText, availableWidthForText - 10f));
                entityViewHeight += factionHeaderHeight;

                if (isExpanded)
                {
                    foreach (var pawn in entry.Value)
                    {
                        PawnSoundSettings.Current.customPawnTypingSounds.TryGetValue(pawn.ThingID, out string currentSound);
                        string label = $"    {pawn.Name.ToStringShort} ({currentSound ?? "Default"})";
                        entityViewHeight += Mathf.Max(minItemHeight, Text.CalcHeight(label, availableWidthForText - 15f));
                    }
                }
            }

            Rect entityViewRect = new Rect(0, 0, entityListRect.width - 16f, entityViewHeight);
            Widgets.BeginScrollView(entityListRect, ref entityScrollPosition, entityViewRect);
            Listing_Standard entityListing = new Listing_Standard();
            entityListing.Begin(entityViewRect);
            
            string storytellerHeaderTextDraw = $"{"Storyteller".Translate()} ({availableStorytellers.Count}) {(storytellersExpanded ? "▲" : "▼")}";
            float storytellerHeaderHeightDraw = Mathf.Max(minItemHeight, Text.CalcHeight(storytellerHeaderTextDraw, entityViewRect.width - 10f));
            Rect storytellerHeaderRect = entityListing.GetRect(storytellerHeaderHeightDraw);
            Widgets.DrawBoxSolidWithOutline(storytellerHeaderRect, darkHeaderColor, darkHeaderBorderColor);
            if (Widgets.ButtonInvisible(storytellerHeaderRect))
            {
                storytellersExpanded = !storytellersExpanded;
                SoundDefOf.Tick_Tiny.PlayOneShotOnCamera(null);
            }
            Text.Anchor = TextAnchor.MiddleLeft;
            Rect storytellerLabelRect = storytellerHeaderRect;
            storytellerLabelRect.xMin += 10f;
            Widgets.Label(storytellerLabelRect, storytellerHeaderTextDraw);
            Text.Anchor = TextAnchor.UpperLeft;

            if (storytellersExpanded)
            {
                foreach (var storyteller in availableStorytellers)
                {
                    string effectiveSound = GetEffectiveStorytellerSound(storyteller);
                    string label = $"  {storyteller.LabelCap} ({effectiveSound})";
                    float itemHeight = Mathf.Max(minItemHeight, Text.CalcHeight(label, entityViewRect.width - 10f));
                    Rect itemRect = entityListing.GetRect(itemHeight);
                    bool isSelected = selectedDefName == storyteller.defName;
                    
                    Widgets.DrawOptionBackground(itemRect, isSelected);

                    if (Widgets.ButtonInvisible(itemRect))
                    {
                        selectedDefName = storyteller.defName;
                        SoundDefOf.Tick_Tiny.PlayOneShotOnCamera(null);
                    }
                    
                    Text.Anchor = TextAnchor.MiddleLeft;
                    Rect labelRect = itemRect;
                    labelRect.xMin += 10f;
                    Widgets.Label(labelRect, label);
                    Text.Anchor = TextAnchor.UpperLeft;
                }
            }
            entityListing.GapLine(12f);
            
            foreach (var entry in filteredPawnsByFaction)
            {
                string factionLabel = entry.Key;
                List<Pawn> pawns = entry.Value;
                bool isExpanded = factionExpansionStates.TryGetValue(factionLabel, out bool expanded) && expanded;
                
                string factionHeaderText = $"  {factionLabel} ({pawns.Count}) {(isExpanded ? "▲" : "▼")}";
                float factionHeaderHeight = Mathf.Max(minItemHeight, Text.CalcHeight(factionHeaderText, entityViewRect.width - 10f));
                Rect factionHeaderRect = entityListing.GetRect(factionHeaderHeight);

                Widgets.DrawBoxSolidWithOutline(factionHeaderRect, darkHeaderColor, darkHeaderBorderColor);
                if (Widgets.ButtonInvisible(factionHeaderRect))
                {
                    factionExpansionStates[factionLabel] = !isExpanded;
                    SoundDefOf.Tick_Tiny.PlayOneShotOnCamera(null);
                }
                Text.Anchor = TextAnchor.MiddleLeft;
                Rect factionLabelRect = factionHeaderRect;
                factionLabelRect.xMin += 10f;
                Widgets.Label(factionLabelRect, factionHeaderText);
                Text.Anchor = TextAnchor.UpperLeft;

                if (isExpanded)
                {
                    foreach (var pawn in pawns)
                    {
                        PawnSoundSettings.Current.customPawnTypingSounds.TryGetValue(pawn.ThingID, out string currentSound);
                        string label = $"    {pawn.Name.ToStringShort} ({currentSound ?? "Default"})";
                        float itemHeight = Mathf.Max(minItemHeight, Text.CalcHeight(label, entityViewRect.width - 15f));
                        Rect itemRect = entityListing.GetRect(itemHeight);
                        bool isSelected = selectedDefName == pawn.ThingID;

                        Widgets.DrawOptionBackground(itemRect, isSelected);

                        if (Widgets.ButtonInvisible(itemRect))
                        {
                            selectedDefName = pawn.ThingID;
                            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera(null);
                        }
                        
                        Text.Anchor = TextAnchor.MiddleLeft;
                        Rect labelRect = itemRect;
                        labelRect.xMin += 15f; // Indent for pawns
                        Widgets.Label(labelRect, label);
                        Text.Anchor = TextAnchor.UpperLeft;
                    }
                }
            }
            
            entityListing.End();
            Widgets.EndScrollView();
            
            // --- 사운드 목록 ---
            if (!string.IsNullOrEmpty(selectedDefName))
            {
                Listing_Standard soundOptionsListing = new Listing_Standard();
                soundOptionsListing.Begin(soundListRect);
                
                bool isStoryteller = DefDatabase<StorytellerDef>.GetNamed(selectedDefName, false) != null;
                string currentSoundForDef;

                if (isStoryteller)
                {
                    var def = DefDatabase<StorytellerDef>.GetNamed(selectedDefName);
                    currentSoundForDef = GetEffectiveStorytellerSound(def);
                }
                else
                {
                    PawnSoundSettings.Current.customPawnTypingSounds.TryGetValue(selectedDefName, out currentSoundForDef);
                }
                if (currentSoundForDef == null) currentSoundForDef = "Default";
                
                foreach (var sound in availableSounds)
                {
                    Rect itemRect = soundOptionsListing.GetRect(30f);

                    // 항목 선택 버튼
                    Rect selectionButtonRect = new Rect(itemRect.x, itemRect.y, itemRect.width - 40f, itemRect.height);
                    bool isSelected = sound == currentSoundForDef;
                    Widgets.DrawOptionBackground(selectionButtonRect, isSelected);
                    if (Widgets.ButtonInvisible(selectionButtonRect))
                    {
                        if (isStoryteller)
                        {
                            var def = DefDatabase<StorytellerDef>.GetNamed(selectedDefName);
                            if (sound == def.defName)
                            {
                                settings.customStorytellerTypingSounds.Remove(selectedDefName);
                            }
                            else
                            {
                                settings.customStorytellerTypingSounds[selectedDefName] = sound;
                            }
                        }
                        else
                        {
                            var pawn = PawnsFinder.AllMapsAndWorld_Alive.FirstOrDefault(p => p.ThingID == selectedDefName);
                            if (pawn != null)
                            {
                                PawnSoundSettings.SetPawnTypingSound(pawn, sound);
                            }
                        }
                        SoundDefOf.Tick_Tiny.PlayOneShotOnCamera(null);
                    }

                    // 미리 듣기 버튼
                    Rect previewButtonRect = new Rect(itemRect.xMax - 30f, itemRect.y + (itemRect.height - 24f) / 2, 24f, 24f);
                    if (Widgets.ButtonImage(previewButtonRect, TexButton.SpeedButtonTextures[1]))
                    {
                        TypingSoundUtility.PlayPreviewSound(sound);
                    }
                    TooltipHandler.TipRegion(previewButtonRect, "RPDia_PreviewSound".Translate());
                    
                    Text.Anchor = TextAnchor.MiddleLeft;
                    Rect labelRect = selectionButtonRect;
                    labelRect.xMin += 10f;
                    Widgets.Label(labelRect, sound);
                    Text.Anchor = TextAnchor.UpperLeft;
                }
                soundOptionsListing.End();
            }

            // 폴더 열기 버튼
            Rect folderButtonRect = new Rect(soundSettingsRect.x, boxRect.yMax + 12f, soundSettingsRect.width, buttonHeight);
            if (Widgets.ButtonText(folderButtonRect, "RPDia_OpenTypingSoundsFolder".Translate()))
            {
                try { 
                    string path = System.IO.Path.Combine(ModContent.RootDir, "Sounds", "Typing");
                    System.IO.Directory.CreateDirectory(path);
                    System.Diagnostics.Process.Start(path); 
                }
                catch (Exception e) { Log.Error($"Could not open custom typing sounds folder: {e.Message}"); }
            }
            
            Rect portraitFolderButtonRect = new Rect(soundSettingsRect.x, folderButtonRect.yMax + 6f, soundSettingsRect.width, buttonHeight);
            if (Widgets.ButtonText(portraitFolderButtonRect, "RPDia_OpenPortraitsFolder".Translate()))
            {
                try { 
                    string path = System.IO.Path.Combine(ModContent.RootDir, "Textures", "UI", "Storyteller");
                    System.IO.Directory.CreateDirectory(path);
                    System.Diagnostics.Process.Start(path);
                }
                catch (Exception e) { Log.Error($"Could not open custom portraits folder: {e.Message}"); }
            }

            base.DoSettingsWindowContents(inRect);
        }

        public override string SettingsCategory()
        {
            return "RPG Dialog";
        }
    }
}
