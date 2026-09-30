using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Emberfield.Voice
{
    /// <summary>Spoken or typed text reduced to comparable words: lower case, without accents or punctuation.</summary>
    public static class VoiceText
    {
        public const int MaximumCount = 10;

        private static readonly Dictionary<string, int> Numbers = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { "un", 1 }, { "uno", 1 }, { "una", 1 }, { "dos", 2 }, { "tres", 3 }, { "cuatro", 4 }, { "cinco", 5 },
            { "seis", 6 }, { "siete", 7 }, { "ocho", 8 }, { "nueve", 9 }, { "diez", 10 },
            { "a", 1 }, { "an", 1 }, { "one", 1 }, { "two", 2 }, { "three", 3 }, { "four", 4 }, { "five", 5 },
            { "six", 6 }, { "seven", 7 }, { "eight", 8 }, { "nine", 9 }, { "ten", 10 },
        };

        public static string Normalize(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            string decomposed = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(decomposed.Length);
            bool space = true;
            foreach (char c in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
                if (char.IsLetterOrDigit(c)) { builder.Append(c); space = false; }
                else if (!space) { builder.Append(' '); space = true; }
            }
            return builder.ToString().Trim();
        }

        public static string[] Words(string text) => Normalize(text).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        /// <summary>A spoken or written count from one to ten; "un", "una", "a" and "an" count as one.</summary>
        public static bool TryCount(string word, out int count)
        {
            if (Numbers.TryGetValue(word, out count)) return true;
            if (int.TryParse(word, NumberStyles.None, CultureInfo.InvariantCulture, out count) && count >= 1)
            {
                count = Math.Min(count, MaximumCount); return true;
            }
            count = 0; return false;
        }

        /// <summary>The word and its likely singular forms, for plurals the vocabulary does not list.</summary>
        internal static IEnumerable<string> Forms(string word)
        {
            yield return word;
            if (word.Length > 4 && word.EndsWith("ies", StringComparison.Ordinal)) yield return word.Substring(0, word.Length - 3) + "y";
            if (word.Length > 3 && word.EndsWith("es", StringComparison.Ordinal)) yield return word.Substring(0, word.Length - 2);
            if (word.Length > 2 && word.EndsWith("s", StringComparison.Ordinal)) yield return word.Substring(0, word.Length - 1);
        }
    }
}
