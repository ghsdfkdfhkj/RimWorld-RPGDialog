using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace RPGDialog
{
    [StaticConstructorOnStartup]
    public class Main
    {
        static Main()
        {
            var harmony = new Harmony("esvn.RPGDialog");
            harmony.PatchAll();
        }
    }

    [HarmonyPatch(typeof(Dialog_NodeTree), "DoWindowContents")]
    public static class DialogNodeTree_DoWindowContents_Patch
    {
        // Simple tracker to mark instances where our mod's custom drawing is active.
        public static class ModDialogTracker
        {
            private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Dialog_NodeTree, object> s_marked = new System.Runtime.CompilerServices.ConditionalWeakTable<Dialog_NodeTree, object>();
            public static void Mark(Dialog_NodeTree win) { try { s_marked.Add(win, new object()); } catch { } }
            public static void Unmark(Dialog_NodeTree win) { try { s_marked.Remove(win); } catch { } }
            public static bool IsMarked(Dialog_NodeTree win) { return s_marked.TryGetValue(win, out _); }
            public static bool AnyMarked()
            {
                try
                {
                    // Check any window in the stack for marked Dialog_NodeTree
                    foreach (var w in Find.WindowStack.Windows)
                    {
                        if (w is Dialog_NodeTree d && s_marked.TryGetValue(d, out _)) return true;
                    }
                }
                catch { }
                return false;
            }
        }
        public static bool useVanillaWindow = false;

        // Session state to support concurrent dialogs (e.g. multiplayer or mod conflicts)
        private class DialogSession
        {
            public Dictionary<DiaNode, int> nodePageCache = new Dictionary<DiaNode, int>();
            public Dictionary<DiaNode, HashSet<int>> fullyTypedPagesCache = new Dictionary<DiaNode, HashSet<int>>();
            public int visibleCharsOnPage = 0;
            public float lastCharTypedTime = 0f;
            public float lastSoundTime = 0f;
            public DiaNode currentNodeForTyping = null;
            public HashSet<DiaNode> clickedChoiceNodes = new HashSet<DiaNode>();
            public bool isSkipped = false;
        }

        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Dialog_NodeTree, DialogSession> sessions = new System.Runtime.CompilerServices.ConditionalWeakTable<Dialog_NodeTree, DialogSession>();

        private static ChoiceMenuWindow s_activeChoiceMenu = null;

        public static void Notify_ChoiceMenuClosed()
        {
            s_activeChoiceMenu = null;
        }

        // Keep only minimal helpers here; heavy work moved to helper classes
        [HarmonyPrefix]
        public static bool Prefix(Dialog_NodeTree __instance, Rect inRect)
        {
            // Mark this instance so derived vanilla classes know we're using mod drawing
            ModDialogTracker.Mark(__instance);

            // FAILSAFE: If enabled, only show RPG Dialog when actually playing the game (Map/World).
            // This prevents the dialog from capturing UI in Main Menu, Pawn Creation, etc.
            if (SettingsCore.settings.onlyShowInGame && Current.ProgramState != ProgramState.Playing)
            {
                return true;
            }

            if (useVanillaWindow)
            {
                __instance.preventCameraMotion = true;
                __instance.absorbInputAroundWindow = true;
                return true;
            }

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                __instance.Close();
                Event.current.Use();
                return false;
            }

            // Apply settings
            if (SettingsCore.settings.dontPauseOnOpen) __instance.forcePause = false;
            __instance.preventCameraMotion = false;
            __instance.absorbInputAroundWindow = false;

            TextAnchor originalAnchor = Text.Anchor;
            try
            {
                float scale = 1.0f;
                var node = Traverse.Create(__instance).Field<DiaNode>("curNode").Value;

                // Ensure global UI styles initialized
                UIStyles.EnsureInitialized();

                // Get or create session
                if (!sessions.TryGetValue(__instance, out DialogSession session))
                {
                    session = new DialogSession();
                    sessions.Add(__instance, session);
                }

                // When node changes, reset typing state and invalidate caches
                if (node != session.currentNodeForTyping)
                {
                    session.currentNodeForTyping = node;
                    session.visibleCharsOnPage = 0;
                    session.lastCharTypedTime = Time.realtimeSinceStartup;
                    session.nodePageCache.Remove(node);
                    session.fullyTypedPagesCache.Remove(node);
                    TypingLayoutCache.InvalidateNode(node);
                    session.clickedChoiceNodes.Clear(); // Clear the clicked nodes history
                    session.isSkipped = false; // Reset skip state for new node
                }

                // Setup window rect using settings
                __instance.windowRect.width = UI.screenWidth * SettingsCore.settings.windowWidthScale * scale;
                __instance.windowRect.height = UI.screenHeight * SettingsCore.settings.windowHeightScale * scale;

                // Button layout from right to left
                float currentButtonX = inRect.width;

                // Close button
                currentButtonX -= 30f;
                Rect xCloseButtonRect = new Rect(currentButtonX, 0f, 30f, 30f);
                MouseoverSounds.DoRegion(xCloseButtonRect);
                if (Widgets.CloseButtonFor(xCloseButtonRect))
                {
                    SoundStarter.PlayOneShotOnCamera(SoundDefOf.TabClose);
                    Find.WindowStack.TryRemove(__instance, doCloseSound: false);
                    Event.current.Use();
                }

                // Vanilla switch button
                currentButtonX -= 30f;
                Rect vanillaButtonRect = new Rect(currentButtonX, 0f, 30f, 30f);
                TooltipHandler.TipRegion(vanillaButtonRect, "RPDia_SwitchToDefault".Translate());
                if (Widgets.ButtonImage(vanillaButtonRect, TexUI.RotRightTex))
                {
                    useVanillaWindow = true;
                    __instance.forcePause = true;
                    __instance.windowRect = new Rect(
                        (UI.screenWidth - __instance.InitialSize.x) / 2f,
                        (UI.screenHeight - __instance.InitialSize.y) / 2f,
                        __instance.InitialSize.x,
                        __instance.InitialSize.y
                    );
                }

                
                currentButtonX -= 30f; // Gap before Auto button

                // Auto button
                float autoButtonWidth = AutoButtonHandler.DrawAutoButton(currentButtonX, 0f);
                currentButtonX -= autoButtonWidth;

                currentButtonX -= 10f; // Gap between buttons

                // Skip button
                float skipButtonWidth = SkipButtonHandler.DrawSkipButton(currentButtonX, 0f, ref session.isSkipped);
                currentButtonX -= skipButtonWidth;

                // Resolve text (TaggedString -> resolved rich text)
                string rawDialogText = node.text.Resolve();
                var speakerTraverse = Traverse.Create(__instance).Field("speaker");
                Pawn speakerPawn = speakerTraverse.GetValue<Pawn>();

                bool radioMode = Traverse.Create(__instance).Field<bool>("radioMode").Value;

                float margin = 18f * scale;
                float yOffset = -5f * scale;
                Rect portraitRect = new Rect(margin, margin + yOffset, 150f * scale, 200f * scale);
                string speakerName = "RPDia_Unknown".Translate();

                // Determine faction to display first
                Faction factionToDisplay = null;
                if (__instance is Dialog_NodeTreeWithFactionInfo factionDialog)
                {
                    factionToDisplay = Traverse.Create(factionDialog).Field<Faction>("faction").Value;
                }
                if (factionToDisplay == null && speakerPawn != null) factionToDisplay = speakerPawn.Faction;

                // speaker portrait/name resolved after faction detection
                Rect nameRect = new Rect(portraitRect.x, portraitRect.yMax + (5f * scale), portraitRect.width, 30f * scale);
                Texture resolvedPortrait = null;
                string resolvedName = null;
                var resolvedPawn = NarratorSelector.ResolveNarratorPawn(__instance, speakerPawn, factionToDisplay, out resolvedName, out resolvedPortrait);
                if (resolvedPortrait != null)
                {
                    GUI.DrawTexture(portraitRect, resolvedPortrait, ScaleMode.ScaleAndCrop);
                }
                else
                {
                    Widgets.DrawBox(portraitRect);
                }

                if (resolvedPawn != null)
                {
                    if (Widgets.ButtonInvisible(portraitRect, doMouseoverSound: false) && Event.current.button == 1)
                    {
                        var contextMenuOptions = new List<FloatMenuOption>
                        {
                            new FloatMenuOption("DefInfoTip".Translate(), () => Find.WindowStack.Add(new Dialog_InfoCard(resolvedPawn)))
                        };
                        Find.WindowStack.Add(new FloatMenu(contextMenuOptions));
                        Event.current.Use();
                    }
                }
                
                GUI.Label(nameRect, resolvedName ?? speakerName, UIStyles.NameStyle);

                if (factionToDisplay != null && !factionToDisplay.IsPlayer && !factionToDisplay.def.permanentEnemy)
                {
                    var faction = factionToDisplay;
                    Rect lastRect = nameRect;

                    if (!radioMode)
                    {
                        Rect factionNameRect = new Rect(lastRect.x, lastRect.yMax, lastRect.width, 25f * scale);
                        GUI.Label(factionNameRect, faction.Name, UIStyles.FactionStyle);
                        lastRect = factionNameRect;
                    }

                    var relation = faction.RelationWith(Faction.OfPlayer);
                    UIStyles.RelationStyle.normal.textColor = relation.kind.GetColor();
                    string relationLabel = relation.kind.GetLabel();
                    string relationText = $"{"Goodwill".Translate().CapitalizeFirst()}: {faction.PlayerGoodwill.ToStringWithSign()} ({relationLabel})";
                    Rect relationRect = new Rect(lastRect.x, lastRect.yMax - 10f * scale, lastRect.width, 25f * scale);
                    GUI.Label(relationRect, relationText, UIStyles.RelationStyle);
                    lastRect = relationRect;
                }

                // Text area setup
                float textX = portraitRect.xMax + margin;
                float textWidth = inRect.width - textX - margin;
                float textY = margin + (20f * scale);
                
                // --- Dynamic line calculation based on window height ---
                // The total height available for text is the window height minus margins and button areas.
                float availableTextHeight = inRect.height - textY - (50f * scale); // 50f is an approximation for the bottom button/padding area
                Rect textDisplayRect = new Rect(textX, textY, textWidth, availableTextHeight);

                // Calculate how many lines can fit into the available height.
                int linesPerPage = 0;
                if (UIStyles.BodyStyle.lineHeight > 0)
                {
                    linesPerPage = Mathf.FloorToInt(availableTextHeight / UIStyles.BodyStyle.lineHeight);
                }
                linesPerPage = Mathf.Max(1, linesPerPage); // Ensure at least one line is shown

                // Ensure pages prepared
                TypingLayoutCache.EnsurePagesBuilt(node, rawDialogText, UIStyles.BodyStyle, textWidth, linesPerPage);
                var pageStartIndices = TypingLayoutCache.GetPageStarts(node);

                // --- Button State Logic (Moved to after cache is built) ---
                int currentPageForButton = session.nodePageCache.TryGetValue(node, out int page) ? page : 0;
                bool isLastPageForButton = currentPageForButton >= (pageStartIndices.Count - 1);
                bool isTypingForButton = IsTyping(session, node, currentPageForButton);
                
                if (isLastPageForButton && !isTypingForButton)
                {
                    session.isSkipped = true;
                }

                int totalPages = pageStartIndices.Count;
                if (!session.nodePageCache.ContainsKey(node)) session.nodePageCache[node] = 0;
                int currentPage = Mathf.Clamp(session.nodePageCache[node], 0, totalPages > 0 ? totalPages - 1 : 0);

                if (SkipButtonHandler.IsSkipRequested() && totalPages > 0)
                {
                    currentPage = totalPages - 1;
                    session.nodePageCache[node] = currentPage;
                    
                    if (!session.fullyTypedPagesCache.ContainsKey(node)) session.fullyTypedPagesCache[node] = new HashSet<int>();
                    for (int i = 0; i < totalPages; i++)
                    {
                        session.fullyTypedPagesCache[node].Add(i);
                    }
                    string lastPageRichText = TypingLayoutCache.GetPageRichTexts(node)[currentPage];
                    session.visibleCharsOnPage = RichTypingRenderer.CountVisibleChars(lastPageRichText);
                }

                string pageTextRaw = TypingLayoutCache.GetPageTexts(node)[currentPage];
                string pageTextRich = TypingLayoutCache.GetPageRichTexts(node)[currentPage];

                bool isTyping = false;

                if (SettingsCore.settings.typingEffectEnabled)
                {
                    bool pageHasBeenTyped = session.fullyTypedPagesCache.TryGetValue(node, out var typedPages) && typedPages.Contains(currentPage);

                    if (!pageHasBeenTyped)
                    {
                        int totalVisibleOnPage = RichTypingRenderer.CountVisibleChars(pageTextRich);
                        if (session.visibleCharsOnPage < totalVisibleOnPage)
                        {
                            isTyping = true;
                            if (Event.current.type == EventType.Repaint)
                            {
                                float speed = SettingsCore.settings.typingSpeed;
                                if (speed <= 0) speed = 35f; // Safety check
                                float delay = 1.0f / speed;
                                
                                float timeSinceLastChar = Time.realtimeSinceStartup - session.lastCharTypedTime;
                                if (timeSinceLastChar >= delay)
                                {
                                    int charsToAdd = Mathf.FloorToInt(timeSinceLastChar / delay);
                                    session.visibleCharsOnPage += charsToAdd;
                                    session.lastCharTypedTime += charsToAdd * delay; 
                                }
                            }

                            if (Event.current.type == EventType.Repaint && SettingsCore.settings.typingSoundEnabled && Time.realtimeSinceStartup - session.lastSoundTime > 0.08f)
                            {
                                SoundDef typingSound = GetTypingSound(resolvedPawn);
                                if (typingSound != null)
                                {
                                    SoundInfo info = SoundInfo.OnCamera(MaintenanceType.None);
                                    info.volumeFactor = SettingsCore.settings.typingSoundVolume;
                                    typingSound.PlayOneShot(info);
                                }
                                session.lastSoundTime = Time.realtimeSinceStartup;
                            }

                            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
                            {
                                session.visibleCharsOnPage = totalVisibleOnPage;
                                Event.current.Use();
                            }
                        }
                        else
                        {
                            if (!session.fullyTypedPagesCache.ContainsKey(node)) session.fullyTypedPagesCache[node] = new HashSet<int>();
                            session.fullyTypedPagesCache[node].Add(currentPage);
                        }
                    }
                }

                bool isLastPage = currentPage >= totalPages - 1;
                AutoButtonHandler.Update(isTyping, isLastPage);
                if (AutoButtonHandler.ShouldTurnPage())
                {
                    if (!isLastPage)
                    {
                        SoundStarter.PlayOneShotOnCamera(SoundDefOf.PageChange);
                        if (!session.fullyTypedPagesCache.ContainsKey(node)) session.fullyTypedPagesCache[node] = new HashSet<int>();
                        session.fullyTypedPagesCache[node].Add(currentPage); // Mark current page as seen
                        session.nodePageCache[node]++; // Increment for next frame
                        session.visibleCharsOnPage = 0; // Reset typing for next page
                        session.lastCharTypedTime = Time.realtimeSinceStartup; // Reset typing timer
                    }
                    else
                    {
                        // Auto-Advance logic at the last page
                        if (node.options.NullOrEmpty())
                        {
                            if (SettingsCore.settings.autoMode_CloseAtEnd)
                            {
                                __instance. Close();
                            }
                            // Otherwise wait for manual close
                        }
                        else if (node.options.Count == 1)
                        {
                            // Do nothing automatically for single choice, as requested.
                            // User must click manually.
                        }
                        // If multiple choices exist, do NOT auto-choose. Wait for user input.
                    }
                }

                if (isTyping)
                {
                    int visibleCountForDraw = Mathf.Clamp(session.visibleCharsOnPage, 0, RichTypingRenderer.CountVisibleChars(pageTextRich));
                    RichTypingRenderer.DrawRichTypedLabel(textDisplayRect, pageTextRich, visibleCountForDraw, UIStyles.BodyStyle);
                }
                else
                {
                    GUI.Label(textDisplayRect, pageTextRich, UIStyles.BodyStyle);
                }

                // Buttons area (unchanged logic, but using UIStyles for styles/measurement)
                var options = node.options;
                float buttonHeight = 35f * scale;
                float buttonY = inRect.height - buttonHeight - (15f * scale);

                if (isTyping)
                {
                    float prevButtonWidth = 100f * scale;
                    float nextButtonWidth = 100f * scale;
                    Rect prevRect = new Rect(margin + (150f * scale), buttonY, prevButtonWidth, buttonHeight);
                    Rect nextRect = new Rect(inRect.width - margin - nextButtonWidth, buttonY, nextButtonWidth, buttonHeight);
                    Rect pageContainerRect = new Rect(prevRect.xMax, buttonY, nextRect.x - prevRect.xMax, buttonHeight);

                    string pageLabelText = $"{currentPage + 1} / {totalPages}";
                    float pageLabelTextWidth = UIStyles.MeasureTextWidth(UIStyles.PageLabelMeasureStyle, pageLabelText);
                    float centeredTextStartX = pageContainerRect.x + (pageContainerRect.width - pageLabelTextWidth) / 2f + 4f;

                    int dotCount = Mathf.FloorToInt(Time.time * 2f) % 3 + 1;
                    string dots = new string('.', dotCount);
                    Rect typingRect = new Rect(centeredTextStartX, buttonY, pageContainerRect.width, buttonHeight);
                    GUI.Label(typingRect, dots, UIStyles.TypingStyle);
                }
                else
                {
                    if (totalPages > 1)
                    {
                        float prevButtonWidth = 120f * scale;
                        float nextButtonWidth = 120f * scale;
                        Rect prevRect = new Rect(portraitRect.xMax + margin - 20f * scale, buttonY, prevButtonWidth, buttonHeight);
                        MouseoverSounds.DoRegion(prevRect);
                        if (currentPage > 0 && GUI.Button(prevRect, "Back".Translate(), UIStyles.ButtonStyle))
                        {
                            AutoButtonHandler.Disable(); // Turn off auto mode when going back
                            SoundStarter.PlayOneShotOnCamera(SoundDefOf.PageChange);
                            session.nodePageCache[node]--;
                        }

                        Rect nextRect = new Rect(inRect.width - margin - nextButtonWidth, buttonY, nextButtonWidth, buttonHeight);
                        MouseoverSounds.DoRegion(nextRect);
                        if (isLastPage)
                        {
                            if (options.NullOrEmpty())
                            {
                                if (GUI.Button(nextRect, "Close".Translate(), UIStyles.ButtonStyle)) __instance.Close();
                            }
                        }
                        else
                        {
                            if (GUI.Button(nextRect, "Next".Translate(), UIStyles.ButtonStyle))
                            {
                                SoundStarter.PlayOneShotOnCamera(SoundDefOf.PageChange);
                                if (!session.fullyTypedPagesCache.ContainsKey(node)) session.fullyTypedPagesCache[node] = new HashSet<int>();
                                session.fullyTypedPagesCache[node].Add(currentPage);

                                session.nodePageCache[node]++;
                                session.visibleCharsOnPage = 0;
                                session.lastCharTypedTime = Time.realtimeSinceStartup;
                            }
                        }

                        string pageLabel = $"{currentPage + 1} / {totalPages}";
                        float pageLabelX = prevRect.xMax;
                        float pageLabelWidth = nextRect.x - prevRect.xMax;
                        Rect pageRect = new Rect(pageLabelX - 3f * scale, buttonY + 5f * scale, pageLabelWidth, buttonHeight);
                        GUI.Label(pageRect, pageLabel, UIStyles.PageLabelStyle);
                    }

                    if (isLastPage)
                    {
                        if (!options.NullOrEmpty())
                        {
                            float buttonPadding = portraitRect.xMax + margin;
                            float buttonSpacing = 10f * scale;
                            float currentX = inRect.width - margin;

                            // --- Final Button Architecture: Text-Based Separation ---

                            if (options.Count < 3)
                            {
                                // Rule 1: If total options are less than 3, ALL buttons are independent.
                                foreach (var option in options)
                                {
                                    var info = GetOptionInfo(option);
                                    string text = info.Text;
                                    if (info.Disabled && !string.IsNullOrEmpty(info.DisabledReason)) text += $" ({info.DisabledReason})";

                                    float width = UIStyles.MeasureTextWidth(UIStyles.ButtonStyle, text) + 20f * scale;
                                    Rect buttonRect = new Rect(currentX - width, buttonY, width, buttonHeight);
                                    MouseoverSounds.DoRegion(buttonRect);

                                    var oldColor = GUI.color;
                                    if (info.Disabled) GUI.color = new Color(0.5f, 0.5f, 0.5f);
                                    if (GUI.Button(buttonRect, text, UIStyles.ButtonStyle) && !info.Disabled) info.Activate();
                                    GUI.color = oldColor;
                                    currentX -= (width + buttonSpacing);
                                }
                            }
                            else 
                            {
                                // Rule 2: If total options are 3 or more, group them, separating independent buttons by text.
                                // Optimized version: Avoid LINQ .Where, .ToList, .Except which create new lists (GC churn)
                                var independentButtonTexts = new HashSet<string> { "Close".Translate(), "PostponeLetter".Translate() };
                                List<DiaOption> independentButtons = null; // Lazy initialization
                                List<DiaOption> choices = null;

                                foreach (var option in options)
                                {
                                    if (independentButtonTexts.Contains(GetOptionInfo(option).Text))
                                    {
                                        if (independentButtons == null) independentButtons = new List<DiaOption>();
                                        independentButtons.Add(option);
                                    }
                                    else
                                    {
                                        if (choices == null) choices = new List<DiaOption>();
                                        choices.Add(option);
                                    }
                                }
                                
                                // a. Render all independent buttons first.
                                if (independentButtons != null)
                                {
                                    foreach (var button in independentButtons)
                                    {
                                        var info = GetOptionInfo(button);
                                        string text = info.Text;
                                        if (info.Disabled && !string.IsNullOrEmpty(info.DisabledReason)) text += $" ({info.DisabledReason})";

                                        float width = UIStyles.MeasureTextWidth(UIStyles.ButtonStyle, text) + 20f * scale;
                                        Rect buttonRect = new Rect(currentX - width, buttonY, width, buttonHeight);
                                        MouseoverSounds.DoRegion(buttonRect);

                                        var oldColor = GUI.color;
                                        if (info.Disabled) GUI.color = new Color(0.5f, 0.5f, 0.5f);
                                        if (GUI.Button(buttonRect, text, UIStyles.ButtonStyle) && !info.Disabled) info.Activate();
                                        GUI.color = oldColor;
                                        currentX -= (width + buttonSpacing);
                                    }
                                }
                                
                                // b. Render ALL other buttons under a single "Choices" group.
                                if (choices != null && choices.Count > 0)
                                {
                                    if (choices.Count == 1)
                                    {
                                        // If only one choice remains, draw it directly.
                                        var info = GetOptionInfo(choices[0]);
                                        string text = info.Text;
                                        if (info.Disabled && !string.IsNullOrEmpty(info.DisabledReason)) text += $" ({info.DisabledReason})";

                                        float width = UIStyles.MeasureTextWidth(UIStyles.ButtonStyle, text) + 20f * scale;
                                        Rect buttonRect = new Rect(currentX - width, buttonY, width, buttonHeight);
                                        MouseoverSounds.DoRegion(buttonRect);

                                        var oldColor = GUI.color;
                                        if (info.Disabled) GUI.color = new Color(0.5f, 0.5f, 0.5f);
                                        if (GUI.Button(buttonRect, text, UIStyles.ButtonStyle) && !info.Disabled) info.Activate();
                                        GUI.color = oldColor;
                                    }
                                    else
                                    {
                                        // Otherwise, group them.
                                        string text = "RPDia_Choices".Translate();
                                        bool allDisabled = choices.TrueForAll(o => GetOptionInfo(o).Disabled);

                                        float width = UIStyles.MeasureTextWidth(UIStyles.ButtonStyle, text) + 20f * scale;
                                        Rect buttonRect = new Rect(currentX - width, buttonY, width, buttonHeight);
                                        
                                        // Only draw the pulsing glow if this node's choices haven't been opened yet.
                                        if (!allDisabled && !session.clickedChoiceNodes.Contains(node))
                                        {
                                            DrawPulsingGlow(buttonRect);
                                        }
                                        MouseoverSounds.DoRegion(buttonRect);

                                        var oldColor = GUI.color;
                                        if (allDisabled) GUI.color = new Color(0.5f, 0.5f, 0.5f);
                                        if (GUI.Button(buttonRect, text, UIStyles.ButtonStyle) && !allDisabled)
                                        {
                                            if (s_activeChoiceMenu != null)
                                            {
                                                SoundDefOf.TabClose.PlayOneShotOnCamera();
                                                s_activeChoiceMenu.Close();
                                                s_activeChoiceMenu = null;
                                            }
                                            else
                                            {
                                                SoundDefOf.Click.PlayOneShotOnCamera();
                                                session.clickedChoiceNodes.Add(node); // Mark this node's choices as opened.
                                                Action<DiaOption> onSelect = (DiaOption selectedOption) => {
                                                    var t = Traverse.Create(selectedOption);
                                                    t.Method("Activate").GetValue();
                                                    s_activeChoiceMenu = null; // Mark as closed after selection
                                                };
                                                s_activeChoiceMenu = new ChoiceMenuWindow(choices, onSelect, __instance.windowRect, SettingsCore.settings.position, __instance);
                                                Find.WindowStack.Add(s_activeChoiceMenu);
                                            }
                                        }
                                        GUI.color = oldColor;
                                    }
                                }
                            }
                        }
                        else if (totalPages <= 1)
                        {
                            float closeButtonWidth = 120f * scale;
                            Rect closeButtonRect = new Rect(inRect.width - margin - closeButtonWidth, buttonY, closeButtonWidth, buttonHeight);
                            MouseoverSounds.DoRegion(closeButtonRect);
                            if (GUI.Button(closeButtonRect, "Close".Translate(), UIStyles.ButtonStyle)) __instance.Close();
                        }
                    }
                }
            }
            finally
            {
                Text.Anchor = originalAnchor;
            }
            return false;
        }

        private static void DrawPulsingGlow(Rect rect)
        {
            if (Event.current.type != EventType.Repaint) return;

            // Use a sine wave for smooth pulsing, oscillating between 0 and 1
            float sine = (Mathf.Sin(Time.time * 3f) + 1f) / 2f;
            Color glowColor = new Color(1f, 0.92f, 0.016f, 1f);

            // 1. Outer Glow (larger, more transparent)
            glowColor.a = sine * 0.4f; // Max alpha 0.4
            Rect outerRect = rect.ExpandedBy(6f);
            
            GUI.color = glowColor;
            Widgets.DrawHighlight(outerRect);

            // 2. Inner Glow (button-sized, more opaque and intense)
            // Pulse alpha from a minimum of 0.3 up to 1.0 for a strong beat
            glowColor.a = 0.3f + sine * 0.7f;

            GUI.color = glowColor;
            Widgets.DrawHighlight(rect);

            // Reset GUI color
            GUI.color = Color.white;
        }

        private struct OptionInfo
        {
            public string Text;
            public bool Disabled;
            public string DisabledReason;
            public Action Activate;
        }

        private static bool IsTyping(DialogSession session, DiaNode node, int currentPage)
        {
            if (!SettingsCore.settings.typingEffectEnabled) return false;

            bool pageHasBeenTyped = session.fullyTypedPagesCache.TryGetValue(node, out var typedPages) && typedPages.Contains(currentPage);
            if (pageHasBeenTyped) return false;

            // This is now safe because this method is only called after EnsurePagesBuilt has run.
            string pageTextRich = TypingLayoutCache.GetPageRichTexts(node)[currentPage];
            int totalVisibleOnPage = RichTypingRenderer.CountVisibleChars(pageTextRich);

            return session.visibleCharsOnPage < totalVisibleOnPage;
        }

        private static OptionInfo GetOptionInfo(DiaOption option)
        {
            var t = Traverse.Create(option);
            string text = t.Field<string>("text").Value;
            bool disabled = t.Field<bool>("disabled").Value;
            string reason = t.Field<string>("disabledReason").Value;
            Action activate = () => t.Method("Activate").GetValue();
            return new OptionInfo { Text = text, Disabled = disabled, DisabledReason = reason, Activate = activate };
        }

        // Sound helper now forwarded to TypingSoundUtility
        private static SoundDef GetTypingSound(Pawn speaker) => TypingSoundUtility.GetTypingSound(speaker);

        [HarmonyPostfix]
        public static void Postfix(Dialog_NodeTree __instance)
        {
            // Unmark when done
            ModDialogTracker.Unmark(__instance);

            if (useVanillaWindow)
            {
                // Draw a small button on vanilla window to switch back to RPG style
                Rect switchButtonRect = new Rect(__instance.windowRect.width - 80f, 5f, 30f, 30f);
                TooltipHandler.TipRegion(switchButtonRect, "RPDia_SwitchToRPG".Translate());
                if (Widgets.ButtonImage(switchButtonRect, TexUI.RotLeftTex))
                {
                    useVanillaWindow = false;
                }
            }
            else
            {
                Rect rect = __instance.windowRect;
                const float margin = 125f;
                switch (SettingsCore.settings.position)
                {
                    case WindowPosition.Top:
                        rect.y = margin;
                        break;
                    case WindowPosition.Middle:
                        rect.y = (UI.screenHeight - rect.height) / 2;
                        break;
                    case WindowPosition.Bottom:
                        rect.y = UI.screenHeight - rect.height - margin;
                        break;
                }
                rect.x = (UI.screenWidth - rect.width) / 2;
                __instance.windowRect = rect;
                // Ensure styles are initialized and will only reinitialize when settings actually changed
                UIStyles.EnsureInitialized();
            }
        }
    }
    [HarmonyPatch(typeof(LetterStack), "ReceiveLetter", new Type[] { typeof(Letter), typeof(string), typeof(int), typeof(bool) })]
    public static class LetterStack_ReceiveLetter_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Letter let)
        {
            if (let.arrivalTick > Find.TickManager.TicksGame) return;

            if (SettingsCore.settings.openWindowOnEvent && let is ChoiceLetter choiceLetter && let.CanShowInLetterStack)
            {
                choiceLetter.OpenLetter();
            }
        }
    }
}