using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using System.Collections.Generic;
using System;
using System.Reflection;
using System.Linq;
using Verse.Sound;

namespace RPGDialog
{
    [StaticConstructorOnStartup]
    public static class RPGDialog_EchoColony_Compat
    {
        private static bool useRPGDialogStyle = true;

        static RPGDialog_EchoColony_Compat()
        {
            if (AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "EchoColony"))
            {
                Log.Message("[RPGDialog] EchoColony detected. Applying compatibility patches.");
                var harmony = new Harmony("esvn.RPGDialog.EchoColonyCompat");

                Type chatGizmoType = AccessTools.TypeByName("EchoColony.Patch_ChatGizmo");
                if (chatGizmoType == null) {
                    Log.Error("[RPGDialog] EchoColony compatibility patch failed: Could not find type 'EchoColony.Patch_ChatGizmo'.");
                    return;
                }

                PatchGizmoMethod(harmony, chatGizmoType, "CreateIndividualChatGizmo", nameof(IndividualGizmo_Postfix));
                PatchGizmoMethod(harmony, chatGizmoType, "CreateGroupChatGizmo", nameof(GroupGizmo_Postfix));
            }
        }

        private static void PatchGizmoMethod(Harmony harmony, Type owner, string methodName, string postfixName)
        {
            var method = AccessTools.Method(owner, methodName);
            if (method != null) {
                harmony.Patch(method, postfix: new HarmonyMethod(typeof(RPGDialog_EchoColony_Compat), postfixName));
                Log.Message($"[RPGDialog] Successfully patched '{methodName}'.");
            } else {
                Log.Error($"[RPGDialog] EchoColony compatibility patch failed: Could not find method '{methodName}'.");
            }
        }
        
        private static void IndividualGizmo_Postfix(Command_Action __result, Pawn pawn)
        {
            __result.action = () => OpenChatWindow(pawn);
        }

        private static void GroupGizmo_Postfix(Command_Action __result, Pawn pawn, List<Pawn> nearbyColonists)
        {
            var participants = nearbyColonists.Where(p => p != null && !p.Dead && !p.Destroyed && p.Spawned).ToList();
            participants.Insert(0, pawn);
            __result.action = () => OpenChatWindow(participants);
        }

        public static void OpenChatWindow(object context)
        {
            if (useRPGDialogStyle) {
                Find.WindowStack.Add(new RPGStyleEchoColonyWindow(context));
            } else {
                Type windowType = context is List<Pawn> ? AccessTools.TypeByName("EchoColony.ColonistGroupChatWindow") : AccessTools.TypeByName("EchoColony.ColonistChatWindow");
                var window = (Window)Activator.CreateInstance(windowType, context);
                Find.WindowStack.Add(window);
                Find.WindowStack.Add(new SwitchToRPGStyleButtonWindow(context));
            }
        }

        public static void SetRPGStyle(bool newStyle)
        {
            useRPGDialogStyle = newStyle;
        }
    }
    
    public class SwitchToRPGStyleButtonWindow : Window
    {
        private object chatContext;
        public override Vector2 InitialSize => new Vector2(40f, 40f);
        protected override float Margin => 0;

        public SwitchToRPGStyleButtonWindow(object context)
        {
            this.chatContext = context;
            this.drawShadow = false;
            this.absorbInputAroundWindow = false;
            this.preventCameraMotion = false;
            this.layer = WindowLayer.Super;
            this.closeOnAccept = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Window echoWindow = Find.WindowStack.Windows.FirstOrDefault(w => w.GetType().FullName.StartsWith("EchoColony.Colonist") && w.GetType().FullName.EndsWith("ChatWindow"));
            if (echoWindow != null) {
                this.windowRect.x = echoWindow.windowRect.xMax - 45;
                this.windowRect.y = echoWindow.windowRect.y - 45;
                
                Rect switchButtonRect = new Rect(5f, 5f, 30f, 30f);
                TooltipHandler.TipRegion(switchButtonRect, "RPDia_SwitchToRPG".Translate());
                if (Widgets.ButtonImage(switchButtonRect, TexUI.RotLeftTex))
                {
                    RPGDialog_EchoColony_Compat.SetRPGStyle(true);
                    echoWindow.Close();
                    RPGDialog_EchoColony_Compat.OpenChatWindow(chatContext);
                    this.Close();
                }
            } else {
                this.Close();
            }

            // Ensure styles are initialized; UIStyles will internally reinitialize only when settings actually change
            UIStyles.EnsureInitialized();
        }
    }

    public class RPGStyleEchoColonyWindow : Window
    {
        private object chatContext;
        private Window originalWindowInstance;
        private RPGStyleInputWindow inputWindow;
        private bool isGroupChat => chatContext is List<Pawn>;

        private int currentPage = 0;
        private List<int> pageStartIndices = new List<int>();
        private string cachedFullText = "";
        private int cachedHistoryCount = -1;
        private string cachedLastLine = "";
        private const int LinesPerPage = 6;
        
        private bool isTypingEffectActive = false;
        private float typingStartTime = 0f;
        private string lastMessageToType = "";
        private string lastMessageSpeaker = "";

        private string currentDateSeparator = "";

        // Optimized: Cache GUIStyles to avoid creating them every frame
        private GUIStyle dateStyle;
        private GUIStyle nameStyle;
        private GUIStyle pageButtonStyle;
        private GUIStyle pageLabelStyle;
        private GUIStyle typingStyle;
		
        private Type myModSettingsType;
        private FieldInfo enableTTSField;
        private FieldInfo modelSourceField;
        private object myModSettingsInstance;
        private object player2ModelSourceValue;

        public override Vector2 InitialSize => new Vector2(UI.screenWidth * 0.45f, UI.screenHeight * 0.30f);
        
        public RPGStyleEchoColonyWindow(object context)
        {
            this.chatContext = context;
            this.forcePause = !SettingsCore.settings.dontPauseOnOpen;
            this.draggable = false;
            this.preventCameraMotion = false;
            this.absorbInputAroundWindow = false;
            this.closeOnAccept = false;

            // Initialize styles once
            dateStyle = new GUIStyle(Text.fontStyles[(int)GameFont.Small]) { normal = { textColor = Color.gray } };
            nameStyle = new GUIStyle(Text.fontStyles[(int)GameFont.Medium]) { alignment = TextAnchor.UpperCenter };
            pageButtonStyle = new GUIStyle(Text.fontStyles[(int)GameFont.Small]) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
            pageButtonStyle.hover.background = TexUI.HighlightTex;
            pageLabelStyle = new GUIStyle(Text.fontStyles[(int)GameFont.Small]) { alignment = TextAnchor.MiddleCenter };
            typingStyle = new GUIStyle(Text.fontStyles[(int)GameFont.Small]) { alignment = TextAnchor.MiddleCenter };

            try
            {
                myModSettingsType = AccessTools.TypeByName("EchoColony.MyMod");
                if (myModSettingsType != null)
                {
                    var settingsField = AccessTools.Field(myModSettingsType, "Settings");
                    myModSettingsInstance = settingsField?.GetValue(null);
                    if (myModSettingsInstance != null)
                    {
                        enableTTSField = AccessTools.Field(myModSettingsInstance.GetType(), "enableTTS");
                        modelSourceField = AccessTools.Field(myModSettingsInstance.GetType(), "modelSource");
                        var modelSourceEnum = AccessTools.TypeByName("EchoColony.ModelSource");
                        if (modelSourceEnum != null)
                        {
                            player2ModelSourceValue = Enum.Parse(modelSourceEnum, "Player2");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error($"[RPGDialog] Error during EchoColony compatibility initialization: {e}");
            }
        }

        public override void PreOpen()
        {
            base.PreOpen();
            EnsureOriginalInstance();
            if (originalWindowInstance != null)
            {
                this.inputWindow = new RPGStyleInputWindow(this);
                Find.WindowStack.Add(this.inputWindow);
            }
        }

        public override void PostClose()
        {
            base.PostClose();
            this.inputWindow?.Close();
        }
        
        private void EnsureOriginalInstance()
        {
            if (originalWindowInstance == null) {
                Type windowType = isGroupChat ? AccessTools.TypeByName("EchoColony.ColonistGroupChatWindow") : AccessTools.TypeByName("EchoColony.ColonistChatWindow");
                originalWindowInstance = (Window)Activator.CreateInstance(windowType, chatContext);
            }
        }
        
        protected override void SetInitialSizeAndPosition()
        {
            Rect rect = new Rect(0f, 0f, InitialSize.x, InitialSize.y);
            const float margin = 125f;
            switch (SettingsCore.settings.echoPosition)
            {
                case WindowPosition.Top: rect.y = margin; break;
                case WindowPosition.Middle: rect.y = (UI.screenHeight - rect.height) / 2f; break;
                case WindowPosition.Bottom: rect.y = UI.screenHeight - rect.height - margin; break;
            }
            rect.x = (UI.screenWidth - rect.width) / 2f;
            this.windowRect = rect;
        }
        
        public void SendMessage(string message)
        {
            var traverse = Traverse.Create(originalWindowInstance);
            var inputField = traverse.Field(isGroupChat ? "userMessage" : "input");
            inputField.SetValue(message);

            string methodName = isGroupChat ? "StartGroupConversation" : "SendMessage";
            if (isGroupChat)
                AccessTools.Method(originalWindowInstance.GetType(), methodName).Invoke(originalWindowInstance, new object[] { message });
            else
                AccessTools.Method(originalWindowInstance.GetType(), methodName).Invoke(originalWindowInstance, null);
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (originalWindowInstance == null) {
                Widgets.Label(inRect, "Error: Could not create EchoColony window instance.");
                return;
            }

            var traverse = Traverse.Create(originalWindowInstance);
            // Keep window position in sync with settings every frame so changes apply immediately
            Rect rect = this.windowRect;
            const float margin = 125f;
            switch (SettingsCore.settings.echoPosition)
            {
                case WindowPosition.Top: rect.y = margin; break;
                case WindowPosition.Middle: rect.y = (UI.screenHeight - rect.height) / 2f; break;
                case WindowPosition.Bottom: rect.y = UI.screenHeight - rect.height - margin; break;
            }
            rect.x = (UI.screenWidth - rect.width) / 2f;
            this.windowRect = rect;
            
            Rect switchButtonRect = new Rect(inRect.width - 60f, 0f, 30f, 30f);
            TooltipHandler.TipRegion(switchButtonRect, "RPDia_SwitchToDefault".Translate());
            if (Widgets.ButtonImage(switchButtonRect, TexUI.RotRightTex))
            {
                RPGDialog_EchoColony_Compat.SetRPGStyle(false);
                this.Close();
                RPGDialog_EchoColony_Compat.OpenChatWindow(chatContext);
                return;
            }

            Rect xCloseButtonRect = new Rect(inRect.width - 30f, 0f, 30f, 30f);
            if (Widgets.CloseButtonFor(xCloseButtonRect)) this.Close();

            if (!string.IsNullOrEmpty(currentDateSeparator))
            {
                Text.Anchor = TextAnchor.UpperCenter;
                Rect dateRect = new Rect(0, 5f, inRect.width, 30f);
                Widgets.Label(dateRect, currentDateSeparator);
                Text.Anchor = TextAnchor.UpperLeft;
            }

            Rect portraitRect = new Rect(18f, 18f, 150f, 200f);
            Rect nameRect = new Rect(portraitRect.x, portraitRect.yMax + 5f, portraitRect.width, 30f);
            
            if (isGroupChat) {
                 Widgets.DrawBox(portraitRect);
                 Widgets.Label(portraitRect.ContractedBy(10f), "Group Chat");
                 GUI.Label(nameRect, "Group", nameStyle);
            } else {
                Pawn pawn = chatContext as Pawn;
                if(pawn != null) {
                    Texture portrait = PortraitsCache.Get(pawn, portraitRect.size, Rot4.South, default(Vector3), 1.0f);
                    GUI.DrawTexture(portraitRect, portrait);
                    GUI.Label(nameRect, pawn.LabelShort, nameStyle);
                }
            }
            
            float textX = portraitRect.xMax + 18f;
            float textWidth = inRect.width - textX - 18f;
            float textY = 18f + 20f;
            // Use configured UIStyles for consistent sizing
            UIStyles.EnsureInitialized();
            UIStyles.EnsureInitialized();
            GUIStyle bodyStyle = UIStyles.BodyStyle;
            // Calculate lines per page dynamically (24px -> 6 lines)
            float baseFontSize = 24f;
            int baseLines = 6;
            float currentFontSize = bodyStyle.fontSize;
            int linesPerPage = Mathf.Max(1, Mathf.RoundToInt(baseLines * (baseFontSize / (currentFontSize > 0 ? currentFontSize : baseFontSize))));
            Rect textDisplayRect = new Rect(textX, textY, textWidth, bodyStyle.lineHeight * linesPerPage + 2f);

            List<string> rawHistory = GetRawChatHistory(traverse, isGroupChat);
            string lastLineRaw = rawHistory.LastOrDefault() ?? "";

            if (rawHistory.Count != cachedHistoryCount || lastLineRaw != cachedLastLine)
            {
                cachedHistoryCount = rawHistory.Count;
                cachedLastLine = lastLineRaw;
                
                List<string> formattedHistory = GetFormattedChatHistory(rawHistory);
                cachedFullText = string.Join(Environment.NewLine, formattedHistory);
                pageStartIndices = CalculatePageStartIndices(rawHistory, formattedHistory, bodyStyle, textWidth, linesPerPage);
                currentPage = pageStartIndices.Count > 0 ? pageStartIndices.Count - 1 : 0;

                string playerPrefix = "EchoColony.UserPrefix".Translate().Trim();
                if (!lastLineRaw.StartsWith("[USER]") && !lastLineRaw.Trim().StartsWith("You::") && !lastLineRaw.Trim().StartsWith(playerPrefix) && !lastLineRaw.StartsWith("EchoColony.") && !lastLineRaw.Trim().EndsWith("..."))
                {
                    isTypingEffectActive = true;
                    typingStartTime = Time.realtimeSinceStartup;
                    
                    string lastLineFormatted = formattedHistory.LastOrDefault() ?? "";
                    int colonIndex = lastLineFormatted.IndexOf(":");
                    if (colonIndex != -1)
                    {
                        lastMessageSpeaker = lastLineFormatted.Substring(0, colonIndex + 1);
                        lastMessageToType = lastLineFormatted.Substring(colonIndex + 1);
                    }
                    else
                    {
                        lastMessageSpeaker = "";
                        lastMessageToType = lastLineFormatted;
                    }
                } else {
                    isTypingEffectActive = false;
                }
            }

            int totalPages = pageStartIndices.Any() ? pageStartIndices.Count : 1;
            currentPage = Mathf.Clamp(currentPage, 0, totalPages - 1);
            
            int pageStartIndex = pageStartIndices.Any() ? pageStartIndices[currentPage] : 0;
            int pageEndIndex = (currentPage + 1 < totalPages) ? pageStartIndices[currentPage + 1] : cachedFullText.Length;
            string pageText = cachedFullText.Substring(pageStartIndex, pageEndIndex - pageStartIndex);
            
            
            bool isLastPage = currentPage == totalPages - 1;

                if (isTypingEffectActive && isLastPage && SettingsCore.settings.typingEffectEnabled)
                {
                    int lastLineInPageIdx = pageText.LastIndexOf(lastMessageSpeaker);
                    if (lastLineInPageIdx != -1)
                    {
                        string textBeforeTyping = pageText.Substring(0, lastLineInPageIdx) + lastMessageSpeaker;

                        float elapsed = Time.realtimeSinceStartup - typingStartTime;
                        int charsToShow = (int)(elapsed * 35f);
                        charsToShow = Mathf.Min(lastMessageToType.Length, charsToShow);

                        // Compute visible chars = visible chars before typing + charsToShow
                        int visibleBefore = RichTypingRenderer.CountVisibleChars(textBeforeTyping);
                        int visibleCount = visibleBefore + charsToShow;

                        RichTypingRenderer.DrawRichTypedLabel(textDisplayRect, pageText, visibleCount, bodyStyle);

                        if (charsToShow >= lastMessageToType.Length)
                        {
                            isTypingEffectActive = false;
                        }
                    }
                    else
                    {
                        RichTypingRenderer.DrawRichTypedLabel(textDisplayRect, pageText, RichTypingRenderer.CountVisibleChars(pageText), bodyStyle);
                        isTypingEffectActive = false;
                    }
                }
                else
                {
                    GUI.Label(textDisplayRect, pageText, bodyStyle);
                }
            
            // Play Button
            if (IsTTSEnabled() && !isGroupChat && !isTypingEffectActive && !(rawHistory.LastOrDefault() ?? "").StartsWith("[USER]"))
            {
                Rect playButtonRect = new Rect(textDisplayRect.xMax - 35f, textDisplayRect.y, 35f, 35f);
                TooltipHandler.TipRegion(playButtonRect, "EchoColony.PlayAudio".Translate());
                if (GUI.Button(playButtonRect, "♪", pageButtonStyle)) // Re-use pageButtonStyle
                {
                    PlayPageAudio(pageText);
                }
            }
            
            float buttonY = inRect.height - 50f;
            if (totalPages > 1)
            {
                Rect prevRect = new Rect(textX, buttonY, 120f, 35f);
                if (currentPage > 0)
                {
                    if (GUI.Button(prevRect, "Back".Translate(), pageButtonStyle))
                    {
                        currentPage--;
                        SoundStarter.PlayOneShotOnCamera(SoundDefOf.PageChange);
                    }
                }

                Rect nextRect = new Rect(inRect.width - 120f - 10f, buttonY, 120f, 35f);
                if (currentPage < totalPages - 1)
                {
                    if (GUI.Button(nextRect, "Next".Translate(), pageButtonStyle))
                    {
                        currentPage++;
                        SoundStarter.PlayOneShotOnCamera(SoundDefOf.PageChange);
                    }
                }

                string pageLabel = $"{currentPage + 1} / {totalPages}";
                Rect pageRect = new Rect(prevRect.xMax, buttonY, nextRect.x - prevRect.xMax, 35f);
                GUI.Label(pageRect, pageLabel, pageLabelStyle);
            }
            
            if (isTypingEffectActive && isLastPage)
            {
                int dotCount = Mathf.FloorToInt(Time.time * 2f) % 3 + 1;
                string dots = new string('.', dotCount);
                Rect typingRect = new Rect(textX, buttonY, textWidth, 35f);
                GUI.Label(typingRect, dots, typingStyle);
            }
        }

        private List<int> CalculatePageStartIndices(List<string> rawHistory, List<string> formattedHistory, GUIStyle style, float width, int linesPerPage)
        {
            // Optimized: Use a simple list initially and convert to HashSet only if needed, though direct add is fine.
            var finalIndices = new List<int> { 0 };
            if (formattedHistory == null || formattedHistory.Count == 0) return finalIndices;

            // Use same text-generation settings as TypingLayoutCache so page splitting matches
            var settings = new TextGenerationSettings {
                font = style.font, fontSize = style.fontSize, fontStyle = style.fontStyle,
                scaleFactor = 1f, generationExtents = new Vector2(width, float.MaxValue),
                horizontalOverflow = HorizontalWrapMode.Wrap, verticalOverflow = VerticalWrapMode.Overflow,
            };
            var textGenerator = new TextGenerator();

            int totalCharCount = 0;
            int turnCharStartIndex = 0;
            var turnLines = new List<string>();

            Action processTurn = () =>
            {
                if (!turnLines.Any()) return;

                string turnText = string.Join(Environment.NewLine, turnLines);
                textGenerator.Populate(turnText, settings);

                if (textGenerator.lineCount > linesPerPage)
                {
                    int lineCountInTurn = 0;
                    for (int j = 0; j < textGenerator.lines.Count; j++)
                    {
                        lineCountInTurn++;
                        if (lineCountInTurn % linesPerPage == 0 && j + 1 < textGenerator.lines.Count)
                        {
                            finalIndices.Add(turnCharStartIndex + textGenerator.lines[j + 1].startCharIdx);
                        }
                    }
                }
                turnLines.Clear();
                turnCharStartIndex = totalCharCount;
            };

            int formattedIdx = 0;
            for (int i = 0; i < rawHistory.Count; i++)
            {
                if (i > 0 && rawHistory[i].StartsWith("[USER]"))
                {
                    processTurn();
                    finalIndices.Add(totalCharCount);
                }

                if (!rawHistory[i].StartsWith("[DATE_SEPARATOR]") && formattedIdx < formattedHistory.Count)
                {
                    string line = formattedHistory[formattedIdx];
                    turnLines.Add(line);
                    totalCharCount += line.Length + Environment.NewLine.Length;
                    formattedIdx++;
                }
            }
            processTurn(); // Process the last turn

            var sortedIndices = new List<int>(new HashSet<int>(finalIndices));
            sortedIndices.Sort();
            return sortedIndices;
        }

        private List<string> GetRawChatHistory(Traverse traverse, bool isGroupChat)
        {
            if (isGroupChat) {
                var session = traverse.Field("session").GetValue();
                if (session != null) return Traverse.Create(session).Field("History").GetValue<List<string>>() ?? new List<string>();
            } else {
                return traverse.Property("chatLog").GetValue<List<string>>() ?? new List<string>();
            }
            return new List<string>();
        }

        private List<string> GetFormattedChatHistory(List<string> rawHistory)
        {
            if (rawHistory == null) return new List<string>();

            // Optimized: Use a single loop instead of multiple LINQ calls.
            var formatted = new List<string>();
            string dateLine = null;
            
            foreach(var s in rawHistory)
            {
                if (s.StartsWith("[DATE_SEPARATOR]"))
                {
                    dateLine = s;
                }
                else
                {
                    if (s.StartsWith("[USER]"))
                    {
                        formatted.Add(s.Substring(6).TrimStart());
                    }
                    else if (s.Trim().StartsWith("You::"))
                    {
                        formatted.Add(s.Replace("You::", "").TrimStart());
                    }
                    else
                    {
                        formatted.Add(s);
                    }
                }
            }

            if (dateLine != null) {
                currentDateSeparator = dateLine.Substring("[DATE_SEPARATOR]".Length).Trim();
            }

            return formatted;
        }

        private bool IsTTSEnabled()
        {
            if (enableTTSField != null && myModSettingsInstance != null && modelSourceField != null && player2ModelSourceValue != null)
            {
                var currentModelSource = modelSourceField.GetValue(myModSettingsInstance);
                if (player2ModelSourceValue.Equals(currentModelSource))
                {
                    return (bool)enableTTSField.GetValue(myModSettingsInstance);
                }
            }
            return false;
        }

        private void PlayPageAudio(string pageText)
        {
            try
            {
                if (isGroupChat) return;
                Pawn pawn = chatContext as Pawn;
                if (pawn == null) return;

                if (string.IsNullOrEmpty(pageText)) return;

                var chatGameCompType = AccessTools.TypeByName("EchoColony.ChatGameComponent");
                var instanceProp = AccessTools.Property(chatGameCompType, "Instance");
                var chatGameCompInstance = instanceProp.GetValue(null);
                
                var getVoiceMethod = AccessTools.Method(chatGameCompType, "GetVoiceForPawn");
                string voiceId = (string)getVoiceMethod.Invoke(chatGameCompInstance, new object[] { pawn });

                if (string.IsNullOrEmpty(voiceId)) return;

                var cleanTextMethod = AccessTools.Method(originalWindowInstance.GetType(), "CleanTextForTTS", new Type[] { typeof(string) });
                string textToSpeak = (string)cleanTextMethod.Invoke(originalWindowInstance, new object[] { pageText });
                
                if (string.IsNullOrWhiteSpace(textToSpeak)) return;

                var ttsManagerType = AccessTools.TypeByName("EchoColony.TTSManager");
                var speakMethod = AccessTools.Method(ttsManagerType, "Speak");
                
                var myStoryModCompType = AccessTools.TypeByName("EchoColony.MyStoryModComponent");
                if (myStoryModCompType == null)
                {
                    Log.Error("[RPGDialog] Could not find EchoColony.MyStoryModComponent type.");
                    return;
                }
                
                var instanceField = AccessTools.Field(myStoryModCompType, "Instance");
                var myStoryModCompInstance = instanceField?.GetValue(null);

                if (myStoryModCompInstance == null)
                {
                    Log.Message("[RPGDialog] MyStoryModComponent.Instance is null. Creating new instance.");
                    GameObject gameObject = new GameObject("MyStoryModComponent");
                    UnityEngine.Object.DontDestroyOnLoad(gameObject);
                    myStoryModCompInstance = gameObject.AddComponent(myStoryModCompType);
                    instanceField.SetValue(null, myStoryModCompInstance);
                }

                var speakCoroutine = speakMethod.Invoke(null, new object[] { textToSpeak, voiceId, Type.Missing, Type.Missing, Type.Missing });
                
                var startCoroutineMethod = AccessTools.Method(myStoryModCompType, "StartCoroutine", new Type[] { typeof(System.Collections.IEnumerator) });
                startCoroutineMethod.Invoke(myStoryModCompInstance, new object[] { speakCoroutine });
            }
            catch (Exception e)
            {
                Log.Error($"[RPGDialog] Error playing TTS audio: {e}");
            }
        }
    }

    public class RPGStyleInputWindow : Window
    {
        private RPGStyleEchoColonyWindow parent;
        private string currentInput = "";

        public override Vector2 InitialSize => new Vector2(parent.InitialSize.x, 70f);

        public RPGStyleInputWindow(RPGStyleEchoColonyWindow parent)
        {
            this.parent = parent;
            this.drawShadow = true;
            this.absorbInputAroundWindow = false;
            this.preventCameraMotion = false;
            this.layer = WindowLayer.Dialog;
            this.doCloseButton = false;
            this.doWindowBackground = true;
            this.closeOnAccept = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            this.windowRect.width = parent.windowRect.width;
            this.windowRect.height = 70f;
            this.windowRect.x = parent.windowRect.x;
            this.windowRect.y = parent.windowRect.yMax + 10f;
            
            float rightMargin = 10f;
            float buttonWidth = 100f;
            float spacing = 10f;
            float leftMargin = 10f;

            Rect sendButtonRect = new Rect(inRect.width - rightMargin - buttonWidth, 0f, buttonWidth, 30f);
            float inputWidth = sendButtonRect.x - spacing - leftMargin;
            Rect inputRect = new Rect(leftMargin, 0f, inputWidth, 30f);

            bool inputIsEmpty = string.IsNullOrWhiteSpace(currentInput);
            bool sendTriggered = false;

            // Handle Enter key press before the text area to ensure the event is captured.
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return && GUI.GetNameOfFocusedControl() == "RPGDialogInputField")
            {
                if (!Event.current.shift)
                {
                    if (!inputIsEmpty)
                    {
                        sendTriggered = true;
                    }
                    // Consume the event to prevent the text area from processing it (which would add a new line).
                    Event.current.Use();
                }
                // If Shift is held, we do nothing and let the TextArea handle the event normally to create a new line.
            }

            GUI.SetNextControlName("RPGDialogInputField");
            currentInput = GUI.TextArea(inputRect, currentInput, 500);
            
            if (Find.WindowStack.Windows.LastOrDefault() == this) {
                UI.FocusControl("RPGDialogInputField", this);
            }

            // Recalculate after potential user input in TextArea
            inputIsEmpty = string.IsNullOrWhiteSpace(currentInput);
            
            if (inputIsEmpty) {
                GUI.color = Color.gray;
            }
            if (Widgets.ButtonText(sendButtonRect, "EchoColony.SendButton".Translate()) && !inputIsEmpty) {
                sendTriggered = true;
            }
            GUI.color = Color.white;

            if (sendTriggered && !inputIsEmpty)
            {
                parent.SendMessage(currentInput);
                currentInput = "";
                GUI.FocusControl(null); // Unfocus to prevent resending on next frame or other weirdness.
            }
        }
        
        protected override void SetInitialSizeAndPosition()
        {
            // Position is set manually
        }
    }
}
