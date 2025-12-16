using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;
using RimWorld;
using HarmonyLib;

namespace RPGDialog
{
    public class ChoiceMenuWindow : ImmediateWindow
    {
        private readonly List<DiaOption> choices;
        private readonly Action<DiaOption> onSelect;
        private readonly Rect anchorRect;
        private readonly WindowPosition chatWindowPosition;
        private readonly Window parentWindow;

        private const float WindowPadding = 5f;

        public override Vector2 InitialSize
        {
            get
            {
                // Width is fixed to the anchor (chat window)
                float width = anchorRect.width;

                // Calculate height dynamically based on text content of each button
                float totalHeight = WindowPadding * 2; // Top and bottom padding
                foreach (var choice in choices)
                {
                    var info = GetOptionInfo(choice);
                    string text = info.Text;
                    if (info.Disabled && !string.IsNullOrEmpty(info.DisabledReason))
                    {
                        text += $" ({info.DisabledReason})";
                    }
                    // Calculate the height needed for this text within the button, and add some padding
                    totalHeight += Text.CalcHeight(text, width - 20f) + 16f; // 16f vertical padding
                }
                
                return new Vector2(width, totalHeight);
            }
        }

        public ChoiceMenuWindow(List<DiaOption> choices, Action<DiaOption> onSelect, Rect anchorRect, WindowPosition chatWindowPosition, Window parent)
        {
            this.choices = choices;
            this.onSelect = onSelect;
            this.anchorRect = anchorRect;
            this.chatWindowPosition = chatWindowPosition;
            this.parentWindow = parent;

            closeOnClickedOutside = false;
            absorbInputAroundWindow = false;
            doCloseX = false;
            drawShadow = true;
            focusWhenOpened = true;
        }

        public override void WindowUpdate()
        {
            base.WindowUpdate();
            // If the parent window is closed, this window should close as well.
            if (!Find.WindowStack.IsOpen(parentWindow))
            {
                this.Close();
            }
        }

        public override void PostClose()
        {
            base.PostClose();
            // Notify the chat window patch that this choice menu is now closed.
            DialogNodeTree_DoWindowContents_Patch.Notify_ChoiceMenuClosed();
        }

        public override void PostOpen()
        {
            base.PostOpen();
            Vector2 size = InitialSize;
            float yPos;
            float gap = 10f;

            if (this.chatWindowPosition == WindowPosition.Bottom)
            {
                yPos = this.anchorRect.y - size.y - gap;
            }
            else
            {
                yPos = this.anchorRect.yMax + gap;
            }
            
            windowRect = new Rect(this.anchorRect.x, yPos, size.x, size.y);
        }
        
        public override void DoWindowContents(Rect inRect)
        {
            Widgets.DrawWindowBackground(inRect);

            float y = WindowPadding;
            Color disabledColor = new Color(0.5f, 0.5f, 0.5f);
            foreach (var choice in choices)
            {
                var info = GetOptionInfo(choice);
                string text = info.Text;
                if (info.Disabled && !string.IsNullOrEmpty(info.DisabledReason))
                {
                    text += $" ({info.DisabledReason})";
                }

                // Calculate dynamic height for this specific button, and add some padding
                float buttonHeight = Text.CalcHeight(text, inRect.width - 20f) + 16f;
                Rect buttonRect = new Rect(10f, y, inRect.width - 20f, buttonHeight);

                MouseoverSounds.DoRegion(buttonRect); // Add mouseover sound
                var oldColor = GUI.color;
                if (info.Disabled) GUI.color = disabledColor;
                
                if (GUI.Button(buttonRect, text, UIStyles.ChoiceButtonStyle))
                {
                    if (!info.Disabled)
                    {
                        onSelect(choice);
                        Close();
                    }
                    else
                    {
                        SoundDefOf.ClickReject.PlayOneShotOnCamera();
                    }
                }
                
                GUI.color = oldColor;
                y += buttonHeight; // Increment y by the actual height of the button
            }
        }

        private struct OptionInfo
        {
            public string Text;
            public bool Disabled;
            public string DisabledReason;
        }

        private static OptionInfo GetOptionInfo(DiaOption option)
        {
            var t = Traverse.Create(option);
            return new OptionInfo
            {
                Text = t.Field<string>("text").Value,
                Disabled = t.Field<bool>("disabled").Value,
                DisabledReason = t.Field<string>("disabledReason").Value
            };
        }
    }
}
