using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;

namespace RPGDialog
{
	public static class TypingLayoutCache
	{
		private static readonly Dictionary<DiaNode, List<int>> pageStartIndicesCache = new Dictionary<DiaNode, List<int>>();
		private static readonly Dictionary<DiaNode, float> pageCalcWidthCache = new Dictionary<DiaNode, float>();
		private static readonly Dictionary<DiaNode, int> pageLinesCache = new Dictionary<DiaNode, int>();
		private static readonly Dictionary<DiaNode, int> pageTextHashCache = new Dictionary<DiaNode, int>();
		private static readonly Dictionary<DiaNode, List<string>> pageTextsCache = new Dictionary<DiaNode, List<string>>();
		private static readonly Dictionary<DiaNode, List<string>> pageRichTextsCache = new Dictionary<DiaNode, List<string>>();
		private static readonly TextGenerator s_textGen = new TextGenerator();

		public static void InvalidateNode(DiaNode node)
		{
			pageStartIndicesCache.Remove(node);
			pageCalcWidthCache.Remove(node);
			pageTextHashCache.Remove(node);
			pageTextsCache.Remove(node);
			pageRichTextsCache.Remove(node);
		}

		public static void EnsurePagesBuilt(DiaNode node, string rawText, GUIStyle style, float textWidth, int linesPerPage)
		{
			int textHash = rawText?.GetHashCode() ?? 0;
			bool needRebuild = false;
			if (!pageStartIndicesCache.ContainsKey(node)) needRebuild = true;
			if (!pageCalcWidthCache.TryGetValue(node, out var prevWidth) || !Mathf.Approximately(prevWidth, textWidth)) needRebuild = true;
			if (!pageLinesCache.TryGetValue(node, out var prevLines) || prevLines != linesPerPage) needRebuild = true;
			if (!pageTextHashCache.TryGetValue(node, out var prevHash) || prevHash != textHash) needRebuild = true;
			if (!needRebuild) return;

			string plainText = rawText.StripTags();
			var plainTextIndices = CalculatePageStartIndices(plainText, style, textWidth, linesPerPage);
			
			// Map plain text indices back to original rich text indices
			var indices = MapPlainIndicesToRich(rawText, plainText, plainTextIndices);

			var pages = new List<string>(indices.Count);
			var richPages = new List<string>(indices.Count);
			for (int i = 0; i < indices.Count; i++)
			{
				int start = indices[i];
				int end = (i + 1 < indices.Count) ? indices[i + 1] : rawText.Length;
				string pageRaw = rawText.Substring(start, end - start);
				pages.Add(pageRaw);
				var openTagsAtStart = GetOpenTagsAt(rawText, start);
				string prefix = string.Concat(openTagsAtStart.opening);
				string suffix = string.Concat(openTagsAtStart.closing);
				richPages.Add(prefix + pageRaw + suffix);
			}

			pageStartIndicesCache[node] = indices;
			pageCalcWidthCache[node] = textWidth;
			pageLinesCache[node] = linesPerPage;
			pageTextHashCache[node] = textHash;
			pageTextsCache[node] = pages;
			pageRichTextsCache[node] = richPages;
		}

		public static List<int> GetPageStarts(DiaNode node) => pageStartIndicesCache[node];
		public static List<string> GetPageTexts(DiaNode node) => pageTextsCache[node];
		public static List<string> GetPageRichTexts(DiaNode node) => pageRichTextsCache[node];

		private static List<int> MapPlainIndicesToRich(string richText, string plainText, List<int> plainTextIndices)
		{
			var richIndices = new List<int> { 0 };
			if (plainTextIndices.Count <= 1) return richIndices;

			int plainIndex = 0;
			int richIndex = 0;

			for (int i = 1; i < plainTextIndices.Count; i++)
			{
				int targetPlainIndex = plainTextIndices[i];
				while (plainIndex < targetPlainIndex && richIndex < richText.Length)
				{
					if (richText[richIndex] == '<')
					{
						int tagEnd = richText.IndexOf('>', richIndex);
						if (tagEnd != -1)
						{
							richIndex = tagEnd + 1;
							continue;
						}
					}
					
					if (plainIndex < plainText.Length && richIndex < richText.Length && richText[richIndex] == plainText[plainIndex])
					{
						plainIndex++;
					}
					richIndex++;
				}
				richIndices.Add(richIndex);
			}

			return richIndices;
		}

		private static List<int> CalculatePageStartIndices(string text, GUIStyle style, float width, int linesPerPage)
		{
			var indices = new List<int> { 0 };
			if (string.IsNullOrEmpty(text)) return indices;
			float fontSize = RPGDialogMod.settings != null ? RPGDialogMod.settings.dialogFontSize : style.fontSize;
			var settings = new TextGenerationSettings
			{
				font = style.font,
				fontSize = (int)fontSize,
				fontStyle = style.fontStyle,
				scaleFactor = 1f,
				generationExtents = new Vector2(width, float.MaxValue),
				horizontalOverflow = HorizontalWrapMode.Wrap,
				verticalOverflow = VerticalWrapMode.Overflow,
				richText = true,
			};
			s_textGen.Populate(text, settings);
			if (s_textGen.lineCount <= linesPerPage) return indices;
			for (int i = 0; i < s_textGen.lineCount; i++)
			{
				if ((i + 1) % linesPerPage == 0 && (i + 1 < s_textGen.lineCount))
				{
					indices.Add(s_textGen.lines[i + 1].startCharIdx);
				}
			}
			// Optimized: Use HashSet to avoid duplicates and skip Distinct() call.
			return new List<int>(new HashSet<int>(indices));
		}

		// Public helper to calculate page start indices for arbitrary text using the same logic
		public static List<int> CalculatePageStartIndicesForText(string text, GUIStyle style, float width, int linesPerPage)
		{
			return CalculatePageStartIndices(text, style, width, linesPerPage);
		}

		private static (System.Collections.Generic.List<string> opening, System.Collections.Generic.List<string> closing) GetOpenTagsAt(string input, int position)
		{
			var stack = new Stack<string>();
			for (int i = 0; i < position && i < input.Length; i++)
			{
				if (input[i] != '<') continue;
				int j = input.IndexOf('>', i);
				if (j == -1) break;
				string tag = input.Substring(i, j - i + 1);
				string inner = tag.Length >= 3 ? tag.Substring(1, j - i - 1).Trim() : string.Empty;
				string innerLower = inner.ToLowerInvariant();
				bool isClosing = innerLower.StartsWith("/");
				if (isClosing)
				{
					string name = innerLower.Substring(1).Split(' ')[0];
					if (stack.Count > 0 && stack.Peek().StartsWith(name + ":"))
					{
						stack.Pop();
					}
				}
				else
				{
					string name = innerLower.Split(' ')[0];
					if (name == "color" || name == "b" || name == "i" || name == "size")
					{
						stack.Push(name + ":" + tag);
					}
				}
				i = j;
			}
			
			// Optimized: Avoid LINQ Select/Reverse/ToList for performance.
			var openList = new List<string>(stack.Count);
			var closeList = new List<string>(stack.Count);
			var reversedStack = new List<string>(stack);
			reversedStack.Reverse();
			
			foreach (var item in reversedStack)
			{
				openList.Add(item.Substring(item.IndexOf(':') + 1));
			}
			foreach (var item in stack)
			{
				closeList.Add("</" + item.Substring(0, item.IndexOf(':')) + ">");
			}
			
			return (openList, closeList);
		}
	}
}


