using System;
using Emberfield.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace Emberfield.Presentation
{
    public sealed class FactionPanel
    {
        private readonly MatchController match;
        private readonly RectTransform root;
        private readonly Font font;
        private readonly RectTransform cards;
        private readonly Text introduction;
        private string realm = "historical";
        private int cardPage;
        public RectTransform Root => root;
        public bool IsVisible => root.gameObject.activeSelf;

        public FactionPanel(MatchController match, Transform parent, Font font)
        {
            this.match = match; this.font = AlphaTheme.Body;
            root = Rect("Choose faction", parent); Anchor(root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AlphaPanelDecor.Apply(root);
            var heading = Label(root, "CHOOSE YOUR FACTION", 28); Anchor(heading.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(24, -60), new Vector2(-190, -14));
            heading.font = AlphaTheme.Display; heading.color = AlphaTheme.Gold;
            var close = Button(root, "Return to map", match.Factions.Close); Anchor(close.GetComponent<RectTransform>(), Vector2.one, Vector2.one, new Vector2(-178, -62), new Vector2(-20, -14));
            introduction = Label(root, "", 20);
            Anchor(introduction.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(24, -126), new Vector2(-24, -70));
            cards = Rect("Faction choices", root); Anchor(cards, Vector2.zero, Vector2.one, new Vector2(24, 100), new Vector2(-24, -142));
            var row = cards.gameObject.AddComponent<HorizontalLayoutGroup>(); row.spacing = 20; row.childForceExpandWidth = row.childForceExpandHeight = true;
            RebuildCards();
            for (int i = 0; i < ContentRealms.All.Length; i++)
            {
                string id = ContentRealms.All[i];
                var button = Button(root, FrontierCodex.RealmName(id), () => { realm = id; cardPage = 0; RebuildCards(); });
                button.name = "Faction realm " + id;
                Anchor(button.GetComponent<RectTransform>(), new Vector2(i*.24f,0), new Vector2((i+1)*.24f,0),new Vector2(i == 0 ? 24 : 8,24),new Vector2(-8,78));
            }
            var next = Button(root,"Next faction pair ›",() => { cardPage = (cardPage + 1) % Math.Max(1, (FrontierCodex.VisibleFactions(realm).Length + 1) / 2); RebuildCards(); });
            Anchor(next.GetComponent<RectTransform>(),new Vector2(.72f,0),new Vector2(1,0),new Vector2(8,24),new Vector2(-24,78));
            root.gameObject.SetActive(false);
        }

        private void RebuildCards()
        {
            for (int i = cards.childCount-1; i >= 0; i--) { var old = cards.GetChild(i).gameObject; old.SetActive(false); UnityEngine.Object.Destroy(old); }
            introduction.text = realm == "naval" ? FrontierCodex.Availability("pirates") : "Práctica con recursos iniciales iguales. Cada ámbito tiene sus propios oponentes.";
            var ids = FrontierCodex.VisibleFactions(realm);
            cardPage = Math.Min(cardPage, Math.Max(0, (ids.Length - 1) / 2));
            for (int index = cardPage * 2; index < Math.Min(ids.Length, cardPage * 2 + 2); index++)
            {
                string id = ids[index];
                if (!ContentRealms.IsPlayableFactionInRealm(id, realm)) { AddPlannedCard(cards, id); continue; }
                foreach (var definition in match.World.Definition.Factions ?? Array.Empty<FactionDefinition>())
                    if (definition.Id == id) { AddCard(cards, definition); break; }
            }
        }

        private void AddPlannedCard(Transform parent, string id)
        {
            var card = Rect("Faction " + id, parent); AlphaTheme.StylePanel(card);
            card.gameObject.AddComponent<LayoutElement>().preferredWidth = 600;
            var title = Label(card, FrontierCodex.Name(id), 28); title.font = AlphaTheme.Display;
            Anchor(title.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(20, -90), new Vector2(-20, -16));
            var text = Label(card, FrontierCodex.Availability(id), 22); text.alignment = TextAnchor.UpperLeft;
            Anchor(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(20, 96), new Vector2(-20, -110));
            var button = Button(card, "NO DISPONIBLE", () => { }); button.name = "Choose " + id; button.interactable = false;
            Anchor(button.GetComponent<RectTransform>(), Vector2.zero, new Vector2(1, 0), new Vector2(20, 20), new Vector2(-20, 70));
        }

        private void AddCard(Transform parent, FactionDefinition faction)
        {
            bool aven = faction.Kind == FactionKind.AvenCompact;
            var card = Rect("Faction " + faction.Id, parent); AlphaTheme.StylePanel(card);
            card.gameObject.AddComponent<LayoutElement>().preferredWidth = 600;
            var stripe = Rect("Faction color", card); stripe.gameObject.AddComponent<Image>().color = aven ? new Color(.46f, .77f, .7f) : new Color(.95f, .63f, .33f);
            Anchor(stripe, new Vector2(0, 1), Vector2.one, new Vector2(0, -6), Vector2.zero);
            var title = Label(card, faction.DisplayName, 28); Anchor(title.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(20, -61), new Vector2(-20, -16));
            title.font = AlphaTheme.Display;
            var identity = Label(card, aven ? "BALANCED / INFRASTRUCTURE" : "MOBILE / FRONTIER PRESSURE", 18);
            identity.text = FrontierCodex.Identity(faction.Id);
            Anchor(identity.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(20, -96), new Vector2(-20, -61));
            identity.color = aven ? new Color(.6f, .85f, .77f) : new Color(1, .73f, .46f);
            string body = aven
                ? $"Tenders carry {faction.WorkerCarryBonus} extra {(faction.WorkerCarryBonus == 1 ? "resource" : "resources")}.\n\nSTOREYARD CHARTERS\nPay {EconomyControls.CostText(faction.CharterCost)} and wait {faction.CharterTicks / (float)World.TickRate:0.#}s. Logistics adds {faction.LogisticsCarryBonus} nearby carry; Muster speeds nearby training by {faction.MusterWorkBonusPermille / 10f:0.#}%. Switching pauses the benefit.\n\nTHREADKEEPER\nTrain a support unit and deploy a temporary, stationary relay beside a chartered Storeyard. Protect it from raids.\n\nRECIPROCAL STORES\nKingdom research extends charter reach."
                : $"Cavalry moves {faction.CavalrySpeedBonusPermille / 10f:0.#}% faster.{(faction.WorkerCarryBonus > 0 ? $" Tenders carry {faction.WorkerCarryBonus} extra {(faction.WorkerCarryBonus == 1 ? "resource" : "resources")}." : "")}\n\nMARCH RELOCATION\nBuild a Supply Outpost. Pack, move its vulnerable transport, then deploy beside clear terrain. Drop-off stops during relocation; damage carries over.\n\nASHRUNNER\nTrain a cavalry specialist. Reposition grants a brief speed burst with no attacking, followed by cooldown. Prepared spears still counter it.\n\nPREPARED ENCAMPMENTS\nKingdom research shortens packing and deployment.";
            if (faction.Kind != FactionKind.AvenCompact && faction.Kind != FactionKind.SerevinMarch)
            {
                body = FrontierCodex.RealmName(faction.RealmId) + " REALM\n\n" + faction.Description + "\n\nSIGNATURE ARMY\n";
                foreach (var unit in match.World.Definition.Units) if (unit.Id == faction.UniqueUnitId) body += unit.DisplayName + " · " + EconomyControls.CostText(unit.Cost) + " · " + unit.PopulationCost + " population.\n";
                foreach (var tech in match.World.Definition.Technologies) if (tech.Id == faction.UniqueTechnologyId) body += "\nUNIQUE RESEARCH\n" + tech.DisplayName + "\n" + tech.Description;
                body += "\n\nHistóricas, fantasía y navales nunca se enfrentan entre sí.";
            }
            var text = Label(card, body, 20); text.alignment = TextAnchor.UpperLeft;
            text.resizeTextForBestFit = true; text.resizeTextMinSize = 16; text.resizeTextMaxSize = 20;
            Anchor(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(20, 87), new Vector2(-20, -111));
            var play = Button(card, "Play " + faction.DisplayName, () => match.Factions.Choose(faction.Id)); play.name = "Choose " + faction.Id;
            Anchor(play.GetComponent<RectTransform>(), Vector2.zero, new Vector2(1, 0), new Vector2(20, 20), new Vector2(-20, 70));
        }
        public void Refresh() { root.gameObject.SetActive(match.Factions.IsOpen); if (IsVisible) root.SetAsLastSibling(); }
        private Text Label(Transform parent, string text, int size)
        {
            var rect = Rect("Label", parent); var label = rect.gameObject.AddComponent<Text>(); label.font = font; label.text = text; label.fontSize = size;
            label.color = AlphaTheme.Ink; label.alignment = TextAnchor.MiddleLeft; label.raycastTarget = false; return label;
        }
        private Button Button(Transform parent, string text, UnityEngine.Events.UnityAction action)
        {
            var rect = Rect(text, parent); var image = rect.gameObject.AddComponent<Image>(); image.color = new Color(.23f, .34f, .37f);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(action);
            bool primary = text.StartsWith("Play ", StringComparison.Ordinal);
            button.onClick.AddListener(() => UiSound.Play(primary ? UiCue.Confirm : UiCue.Click));
            var label = Label(rect, text, 20); label.alignment = TextAnchor.MiddleCenter; Anchor(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(5, 2), new Vector2(-5, -2));
            AlphaTheme.StyleButton(button, primary); return button;
        }
        private static RectTransform Rect(string name, Transform parent) { var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false); return rect; }
        private static void Anchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 a, Vector2 b) { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = a; rect.offsetMax = b; }
    }
}
