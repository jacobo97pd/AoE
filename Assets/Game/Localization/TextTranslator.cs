using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Emberfield.Localization
{
    /// <summary>The game's display languages. Spanish (Spain) is the default; English is the second language.</summary>
    public enum GameLanguage { Spanish = 0, English = 1 }

    /// <summary>
    /// Translates text exactly as a screen shows it. Screens keep composing text in their source language;
    /// the translator tries the whole text, then each line, then templates whose placeholders are translated
    /// in turn, then each part between " · " or " / " separators. Unknown text comes back unchanged.
    /// </summary>
    public sealed class TextTranslator
    {
        public const int CacheLimit = 4096;
        private const int MaximumDepth = 8;
        private static readonly Regex Separator = new Regex(@"(\s+[·/]\s+)", RegexOptions.CultureInvariant);
        private static readonly Regex Placeholder = new Regex(@"\{(?<name>[A-Za-z]\w*)(?::(?<kind>[a-z]+))?\}", RegexOptions.CultureInvariant);
        private readonly Dictionary<string, string> exact = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> folded = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<TranslationTemplate> templates = new List<TranslationTemplate>();
        private readonly Dictionary<string, string> cache = new Dictionary<string, string>(StringComparer.Ordinal);

        public GameLanguage Language { get; }
        public int EntryCount => exact.Count;
        public int TemplateCount => templates.Count;

        public TextTranslator(GameLanguage language) { Language = language; }

        /// <summary>A fixed text and its translation. Upper- and lower-case forms of the source are translated too.</summary>
        public TextTranslator Add(string source, string translation)
        {
            if (string.IsNullOrEmpty(source) || translation == null) throw new ArgumentException("A translation needs a source text and a translation.");
            if (exact.ContainsKey(source)) throw new ArgumentException("Duplicate translation source: " + source);
            exact.Add(source, translation);
            if (!folded.ContainsKey(source)) folded.Add(source, translation);
            cache.Clear();
            return this;
        }

        /// <summary>
        /// A composed text such as "Gathering {resource}. Resources count when delivered.". A source placeholder
        /// matches any text; {n:int} matches a whole number, {x:num} a decimal and {w:word} one word. Replacement
        /// placeholders are translated before insertion; :lower, :upper and :cap adjust case, :list translates a
        /// comma-separated list item by item and :raw inserts the captured text untouched.
        /// </summary>
        public TextTranslator Template(string source, string translation)
        {
            var pattern = new StringBuilder("^");
            string hint = "";
            int last = 0;
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match placeholder in Placeholder.Matches(source))
            {
                string literal = source.Substring(last, placeholder.Index - last);
                if (literal.Length > hint.Length) hint = literal;
                pattern.Append(Regex.Escape(literal));
                string name = placeholder.Groups["name"].Value, kind = placeholder.Groups["kind"].Value;
                if (!names.Add(name)) throw new ArgumentException("Repeated placeholder in: " + source);
                pattern.Append("(?<").Append(name).Append('>')
                    .Append(kind == "int" ? @"-?\d+" : kind == "num" ? @"-?\d+(?:[.,]\d+)?" : kind == "word" ? @"\S+" : ".+?").Append(')');
                last = placeholder.Index + placeholder.Length;
            }
            string tail = source.Substring(last);
            if (tail.Length > hint.Length) hint = tail;
            pattern.Append(Regex.Escape(tail)).Append('$');
            if (hint.Trim().Length == 0) throw new ArgumentException("A template needs literal text: " + source);
            foreach (Match placeholder in Placeholder.Matches(translation))
                if (!names.Contains(placeholder.Groups["name"].Value)) throw new ArgumentException("Unknown placeholder in the translation of: " + source);
            templates.Add(new TranslationTemplate(new Regex(pattern.ToString(), RegexOptions.CultureInvariant), hint, translation, null));
            cache.Clear();
            return this;
        }

        /// <summary>
        /// A text rewritten by code, for shapes a template cannot express, such as cost lists. The second
        /// argument translates a fragment of the match.
        /// </summary>
        public TextTranslator Rule(string pattern, Func<Match, Func<string, string>, string> rewrite)
        {
            templates.Add(new TranslationTemplate(new Regex(pattern, RegexOptions.CultureInvariant), null, null, rewrite ?? throw new ArgumentNullException(nameof(rewrite))));
            cache.Clear();
            return this;
        }

        public string Translate(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            if (cache.TryGetValue(text, out var known)) return known;
            string result = Block(text);
            if (cache.Count >= CacheLimit) cache.Clear();
            cache[text] = result;
            return result;
        }

        private string Block(string text)
        {
            if (exact.TryGetValue(text, out var whole)) return whole;
            if (text.IndexOf('\n') < 0) return Line(text, 0);
            var lines = text.Split('\n');
            bool changed = false;
            for (int i = 0; i < lines.Length; i++)
            {
                string translated = Line(lines[i], 0);
                if (translated != lines[i]) { lines[i] = translated; changed = true; }
            }
            return changed ? string.Join("\n", lines) : text;
        }

        private string Line(string line, int depth)
        {
            if (line.Length == 0 || depth > MaximumDepth) return line;
            int start = 0, end = line.Length;
            while (start < end && char.IsWhiteSpace(line[start])) start++;
            while (end > start && char.IsWhiteSpace(line[end - 1])) end--;
            if (start == end) return line;
            if (start > 0 || end < line.Length)
            {
                string core = line.Substring(start, end - start), inner = Line(core, depth);
                return inner == core ? line : line.Substring(0, start) + inner + line.Substring(end);
            }
            if (exact.TryGetValue(line, out var fixedText)) return fixedText;
            if (folded.TryGetValue(line, out var anyCase))
            {
                if (IsUpper(line)) return anyCase.ToUpperInvariant();
                if (IsLower(line)) return anyCase.ToLowerInvariant();
            }
            foreach (var template in templates)
            {
                if (template.Hint != null && line.IndexOf(template.Hint, StringComparison.Ordinal) < 0) continue;
                var match = template.Pattern.Match(line);
                if (!match.Success) continue;
                return template.Rewrite != null ? template.Rewrite(match, fragment => Line(fragment, depth + 1)) : Fill(template.Translation, match, line, depth);
            }
            var parts = Separator.Split(line);
            if (parts.Length > 1)
            {
                bool changed = false;
                for (int i = 0; i < parts.Length; i += 2)
                {
                    string translated = Line(parts[i], depth + 1);
                    if (translated != parts[i]) { parts[i] = translated; changed = true; }
                }
                if (changed) return string.Concat(parts);
            }
            return line;
        }

        private string Fill(string translation, Match match, string whole, int depth) => Placeholder.Replace(translation, placeholder =>
        {
            string value = match.Groups[placeholder.Groups["name"].Value].Value, kind = placeholder.Groups["kind"].Value;
            if (kind == "raw" || value == whole) return value;
            if (kind == "list")
            {
                var items = value.Split(new[] { ", " }, StringSplitOptions.None);
                for (int i = 0; i < items.Length; i++) items[i] = Line(items[i], depth + 1);
                return string.Join(", ", items);
            }
            string translated = Line(value, depth + 1);
            if (kind == "lower") return translated.ToLowerInvariant();
            if (kind == "upper") return translated.ToUpperInvariant();
            if (kind == "cap" && translated.Length > 0) return char.ToUpperInvariant(translated[0]) + translated.Substring(1);
            return translated;
        });

        private static bool IsUpper(string text)
        {
            bool letter = false;
            foreach (char c in text) { if (char.IsLower(c)) return false; if (char.IsLetter(c)) letter = true; }
            return letter;
        }

        private static bool IsLower(string text)
        {
            bool letter = false;
            foreach (char c in text) { if (char.IsUpper(c)) return false; if (char.IsLetter(c)) letter = true; }
            return letter;
        }

        private sealed class TranslationTemplate
        {
            internal readonly Regex Pattern;
            internal readonly string Hint, Translation;
            internal readonly Func<Match, Func<string, string>, string> Rewrite;
            internal TranslationTemplate(Regex pattern, string hint, string translation, Func<Match, Func<string, string>, string> rewrite)
            { Pattern = pattern; Hint = hint; Translation = translation; Rewrite = rewrite; }
        }
    }
}
