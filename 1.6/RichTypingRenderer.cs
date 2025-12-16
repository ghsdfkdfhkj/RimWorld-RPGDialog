using System.Text;
using UnityEngine;
using System.Collections.Generic;

namespace RPGDialog
{
	public static class RichTypingRenderer
	{
		private static readonly TextGenerator s_layoutGen = new TextGenerator();
		// Optimized: Reuse StringBuilder and Stack to avoid GC allocation
		private static readonly StringBuilder sb = new StringBuilder();
		private static readonly Stack<string> openTags = new Stack<string>();

		public static void DrawRichTypedLabel(Rect rect, string richText, int visibleCount, GUIStyle style)
		{
			if (string.IsNullOrEmpty(richText) || visibleCount <= 0)
			{
				return;
			}

			int totalVisibleChars = CountVisibleChars(richText);
			if (visibleCount >= totalVisibleChars)
			{
				GUI.Label(rect, richText, style);
				return;
			}
			
			sb.Clear();
			openTags.Clear();
			int visibleCharsCounted = 0;
			
			for (int i = 0; i < richText.Length; i++)
			{
				if (visibleCharsCounted >= visibleCount)
				{
					break;
				}

				if (richText[i] == '<')
				{
					int tagEndIndex = richText.IndexOf('>', i);
					if (tagEndIndex != -1)
					{
						string tag = richText.Substring(i, tagEndIndex - i + 1);
						sb.Append(tag);

						if (tag.StartsWith("</"))
						{
							if (openTags.Count > 0)
							{
								openTags.Pop();
							}
						}
						else if (!tag.EndsWith("/>"))
						{
                            int nameEndIndex = tag.IndexOfAny(new char[] { '=', ' ', '>' });
                            if (nameEndIndex > 1)
                            {
                                string tagName = tag.Substring(1, nameEndIndex - 1);
                                openTags.Push(tagName);
                            }
						}
						i = tagEndIndex;
						continue;
					}
				}

				sb.Append(richText[i]);
				visibleCharsCounted++;
			}
			
			while (openTags.Count > 0)
			{
				string tagToClose = openTags.Pop();
				sb.Append("</");
				sb.Append(tagToClose);
				sb.Append(">");
			}
			
			GUI.Label(rect, sb.ToString(), style);
		}

		public static int CountVisibleChars(string input)
		{
			if (string.IsNullOrEmpty(input)) return 0;
			int count = 0;
			for (int i = 0; i < input.Length; i++)
			{
				char c = input[i];
				if (c == '<')
				{
					int j = input.IndexOf('>', i);
					if (j == -1) { count += input.Length - i; break; }
					i = j;
					continue;
				}
				count++;
			}
			return count;
		}
	}
}


