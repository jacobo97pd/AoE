using System;
using System.Collections.Generic;
using System.Linq;

namespace Emberfield.Voice
{
    /// <summary>
    /// Turns what the player said into a <see cref="VoiceIntent"/>. It understands the recognizer's exact
    /// phrases and the looser forms of typed text: accents, case, articles, digits and regular plurals.
    /// </summary>
    public sealed class VoiceCommandParser
    {
        public VoiceVocabulary Vocabulary { get; }

        public VoiceCommandParser(VoiceVocabulary vocabulary) { Vocabulary = vocabulary ?? throw new ArgumentNullException(nameof(vocabulary)); }

        public VoiceIntent Parse(string heard)
        {
            var words = VoiceText.Words(heard).ToList();
            // "por favor" is a courtesy, but a lone "por" belongs to "a por madera".
            for (int i = words.Count - 2; i >= 0; i--)
                if (words[i] == "por" && words[i + 1] == "favor") words.RemoveRange(i, 2);
            if (words.Count == 0) return VoiceIntent.None;
            // Fixed commands as said first, so "muévete aquí" keeps the "aquí" that fillers would drop.
            if (Vocabulary.TryFixed(words, out var action)) return new VoiceIntent(action);
            words.RemoveAll(Vocabulary.IsFiller);
            if (words.Count == 0) return VoiceIntent.None;
            if (Vocabulary.TryFixed(words, out action)) return new VoiceIntent(action);
            foreach (var verb in Vocabulary.Verbs)
            {
                if (words.Count <= verb.Words.Length || !words.Take(verb.Words.Length).SequenceEqual(verb.Words)) continue;
                var intent = Complete(verb.Kind, words.GetRange(verb.Words.Length, words.Count - verb.Words.Length));
                if (!intent.IsNone) return intent;
            }
            return VoiceIntent.None;
        }

        private VoiceIntent Complete(VoiceVerbKind kind, List<string> rest)
        {
            // Articles go first, so "entrena a dos lanceros" and "train a worker" both find their count.
            rest.RemoveAll(Vocabulary.IsArticle);
            int count = 1;
            if (rest.Count > 1 && VoiceText.TryCount(rest[0], out int spoken)) { count = spoken; rest.RemoveAt(0); }
            if (rest.Count == 0) return VoiceIntent.None;
            switch (kind)
            {
                case VoiceVerbKind.Train:
                    return TryTerm(Vocabulary.Units, rest, out var unit) ? new VoiceIntent(VoiceAction.Train, unit.Id, count) : VoiceIntent.None;
                case VoiceVerbKind.Build:
                    return TryTerm(Vocabulary.Buildings, rest, out var building) ? new VoiceIntent(VoiceAction.Build, building.Id) : VoiceIntent.None;
                case VoiceVerbKind.Select:
                    if (TryTerm(Vocabulary.Units, rest, out var units)) return new VoiceIntent(VoiceAction.SelectUnits, units.Id);
                    return TryTerm(Vocabulary.Buildings, rest, out var site) ? new VoiceIntent(VoiceAction.SelectBuilding, site.Id) : VoiceIntent.None;
                case VoiceVerbKind.Research:
                    return TryTerm(Vocabulary.ResearchFamilies, rest, out var family) ? new VoiceIntent(VoiceAction.Research, family.Id) : VoiceIntent.None;
                case VoiceVerbKind.Gather:
                    return Vocabulary.TryResource(rest, out var resource) ? new VoiceIntent(VoiceAction.Gather, resource: resource) : VoiceIntent.None;
                default:
                    return VoiceIntent.None;
            }
        }

        // The longest name that covers every remaining word; the first term wins a tie.
        private static bool TryTerm(IReadOnlyList<VoiceTerm> terms, List<string> words, out VoiceTerm match)
        {
            match = null; int best = 0;
            foreach (var term in terms)
                foreach (var noun in term.Nouns)
                    if ((Covers(words, noun.SingularWords) || Covers(words, noun.PluralWords)) && noun.SingularWords.Length > best)
                    { match = term; best = noun.SingularWords.Length; }
            return match != null;
        }

        private static bool Covers(List<string> words, string[] name)
        {
            if (name.Length == 0 || words.Count != name.Length) return false;
            for (int i = 0; i < name.Length; i++)
                if (!VoiceText.Forms(words[i]).Contains(name[i])) return false;
            return true;
        }
    }
}
