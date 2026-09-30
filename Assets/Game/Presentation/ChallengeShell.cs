using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Emberfield.Presentation
{
    public sealed partial class ProductShell
    {
        /// <summary>
        /// The training ground: the rung the design always had between the guide and a real opponent.
        /// Each exercise is one page-width card carrying what it teaches and what it asks for, and the
        /// one you played last says how it went, so the page remembers your session without a save file.
        /// </summary>
        private void Challenges()
        {
            var title = Label("Challenges heading", content, "Short exercises", 46, AlphaTheme.Ink, true);
            Place(title.rectTransform, new Vector2(0, .85f), new Vector2(1, 1));
            var all = ChallengeCatalog.All;
            int passed = ChallengeRun.PassedCount(match);
            var subtitle = Label("Challenges subtitle", content,
                passed >= all.Length
                    ? "Every exercise passed. You have the whole vocabulary now; the rest is deciding when to use it."
                    : "One thing to do, one clock. Learn a skill on its own before an opponent makes you use it under pressure.",
                21, AlphaTheme.Muted);
            Place(subtitle.rectTransform, new Vector2(0, .75f), new Vector2(.72f, .85f));
            var tally = Label("Challenges tally", content, passed + " of " + all.Length + " passed", 19, AlphaTheme.Gold);
            tally.alignment = TextAnchor.MiddleRight;
            Place(tally.rectTransform, new Vector2(.74f, .75f), new Vector2(1, .85f));
            const float top = .70f, height = .125f, gap = .018f;
            for (int index = 0; index < all.Length; index++)
            {
                var challenge = all[index];
                float y = top - index * (height + gap);
                var card = Panel("Challenge " + challenge.Id, content);
                Place(card, new Vector2(0, y - height), new Vector2(.74f, y));
                // The card is roughly eighty pixels tall, so everything has to live inside that: the verdict
                // shares the title row on the right rather than being drawn past the card's own bottom edge.
                var name = Label("Challenge name " + challenge.Id, card, challenge.Name, 23, AlphaTheme.Ink, true);
                Box(name.rectTransform, 20, 8, -150, 36, true);
                var verdict = Label("Challenge verdict " + challenge.Id, card, Verdict(challenge.Id), 14, AlphaTheme.Gold);
                Box(verdict.rectTransform, -150, 10, -20, 34, true);
                verdict.alignment = TextAnchor.MiddleRight;
                var brief = Label("Challenge brief " + challenge.Id, card, challenge.Brief, 15, AlphaTheme.Muted);
                Box(brief.rectTransform, 20, 38, -20, 74, true);

                string identifier = challenge.Id;
                var play = Action(content, "Play " + identifier, "BEGIN    " + Minutes(challenge.Seconds), () => Begin(identifier));
                Place((RectTransform)play.transform, new Vector2(.76f, y - height + .012f), new Vector2(1, y - .012f));
            }

            // The whole point of a training ground is the door out of it.
            if (passed < all.Length) return;
            var graduate = Action(content, "Skirmish after training", "YOU ARE READY    ·    PLAY A SKIRMISH    ›", () => Navigate("skirmish"), true);
            Place((RectTransform)graduate.transform, new Vector2(0, -.115f), new Vector2(.74f, -.02f));
        }

        private static string Minutes(int seconds) => seconds / 60 + " min";

        /// <summary>What happened the last time this exercise was played in this session.</summary>
        private string Verdict(string id)
        {
            if (ChallengeRun.WasPassed(match, id)) return "Passed";
            return ChallengeRun.LastPlayedId == id && ChallengeRun.LastResult == ChallengeState.Lost
                ? "Not completed yet" : "Not attempted yet";
        }

        /// <summary>When an exercise ends, come back to the list by itself so retrying costs no navigation.</summary>
        private void ObserveChallenge()
        {
            var run = match.Challenge;
            if (run == null || !run.ShouldPresentResult || IsOpen) return;
            run.MarkPresented();
            Open(); Navigate("challenges");
        }

        private void Begin(string id)
        {
            ChallengeRun.Queue(id);
            Close();
            returnHome = false;
            // A challenge owns its own battlefield, so it always starts from a fresh scene.
            SceneManager.LoadScene("Greybox");
        }
    }
}
