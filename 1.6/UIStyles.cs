using UnityEngine;
using Verse;

namespace RPGDialog
{
	public static class UIStyles
	{
		private static bool s_initialized;
		private static float s_lastDialogFontSize = -1f;
		private static float s_lastDialogButtonFontSize = -1f;
		private static float s_lastChoiceButtonFontSize = -1f;
		public static GUIStyle NameStyle { get; private set; }
		public static GUIStyle BodyStyle { get; private set; }
		public static GUIStyle FactionStyle { get; private set; }
		public static GUIStyle RelationStyle { get; private set; }
		public static GUIStyle PageLabelStyle { get; private set; }
		public static GUIStyle PageLabelMeasureStyle { get; private set; }
		public static GUIStyle TypingStyle { get; private set; }
		public static GUIStyle ButtonStyle { get; private set; }
		public static GUIStyle ChoiceButtonStyle { get; private set; }
		private static readonly System.Collections.Generic.Dictionary<string, float> s_textWidthCache = new System.Collections.Generic.Dictionary<string, float>();
		private static readonly GUIContent s_tempContent = new GUIContent();

		public static void EnsureInitialized()
		{
			float dialogFontSize = RPGDialogMod.settings?.dialogFontSize ?? 24f;
			float dialogButtonFontSize = RPGDialogMod.settings?.dialogButtonFontSize ?? 20f;
			float choiceButtonFontSize = RPGDialogMod.settings?.choiceButtonFontSize ?? 18f;

			if (s_initialized && Mathf.Approximately(s_lastDialogFontSize, dialogFontSize) &&
				Mathf.Approximately(s_lastDialogButtonFontSize, dialogButtonFontSize) &&
				Mathf.Approximately(s_lastChoiceButtonFontSize, choiceButtonFontSize))
			{
				return;
			}
			
			s_textWidthCache.Clear();
			s_lastDialogFontSize = dialogFontSize;
			s_lastDialogButtonFontSize = dialogButtonFontSize;
			s_lastChoiceButtonFontSize = choiceButtonFontSize;

			NameStyle = new GUIStyle(Text.fontStyles[(int)GameFont.Medium]) { fontSize = (int)dialogFontSize, alignment = TextAnchor.UpperCenter };
			BodyStyle = new GUIStyle(Text.fontStyles[(int)GameFont.Small]) { fontSize = (int)dialogFontSize, alignment = TextAnchor.UpperLeft, wordWrap = true };
			BodyStyle.richText = true;
			
			FactionStyle = new GUIStyle(Text.fontStyles[(int)GameFont.Small]) { alignment = TextAnchor.UpperCenter };
			FactionStyle.normal.textColor = Color.gray;
			
			RelationStyle = new GUIStyle(Text.fontStyles[(int)GameFont.Small]) { alignment = TextAnchor.UpperCenter };
			
			PageLabelStyle = new GUIStyle(Text.fontStyles[(int)GameFont.Small]) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
			PageLabelMeasureStyle = new GUIStyle(Text.fontStyles[(int)GameFont.Small]) { fontSize = 18 };
			
			TypingStyle = new GUIStyle(Text.fontStyles[(int)GameFont.Small]) { alignment = TextAnchor.MiddleLeft, fontSize = 24 };
			
			ButtonStyle = new GUIStyle(Text.fontStyles[(int)GameFont.Small]) { fontSize = (int)dialogButtonFontSize, alignment = TextAnchor.MiddleCenter };
			ButtonStyle.hover.background = TexUI.HighlightTex;

			ChoiceButtonStyle = new GUIStyle(Text.fontStyles[(int)GameFont.Small]) { fontSize = (int)choiceButtonFontSize, alignment = TextAnchor.MiddleCenter };
			ChoiceButtonStyle.hover.background = TexUI.HighlightTex;
			
			s_initialized = true;
		}

		public static void Reset()
		{
			s_initialized = false;
			s_textWidthCache.Clear();
			s_lastDialogFontSize = -1f;
			s_lastDialogButtonFontSize = -1f;
			s_lastChoiceButtonFontSize = -1f;
		}

		public static float MeasureTextWidth(GUIStyle style, string text)
		{
			if (string.IsNullOrEmpty(text)) return 0f;
			if (!s_textWidthCache.TryGetValue(text, out float width))
			{
				s_tempContent.text = text;
				Vector2 size = style.CalcSize(s_tempContent);
				s_tempContent.text = null;
				width = size.x;
				s_textWidthCache[text] = width;
			}
			return width;
		}
	}
}


