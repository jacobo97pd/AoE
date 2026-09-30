using System;
using System.Collections.Generic;
using Emberfield.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace Emberfield.Presentation
{
    // Scrollable technology tree, composed inside the existing safe-area HUD canvas.
    public sealed class ResearchPanel
    {
        private sealed class Card
        {
            public TechnologyDefinition Definition;
            public Text Status, Action;
            public Button Button;
            public RectTransform Fill;
        }
        private readonly MatchController match;
        private readonly RectTransform root, content, viewport;
        private readonly Text era, stock, notice;
        private readonly Font font;
        private readonly List<Card> cards = new List<Card>();
        private readonly List<Text> eraSteps = new List<Text>();
        private readonly GridLayoutGroup grid;
        private readonly ScrollRect scroll;
        private bool wasOpen;
        public bool IsVisible => root.gameObject.activeSelf;
        public RectTransform Root => root;

        public ResearchPanel(MatchController match, Transform parent, Font font)
        {
            this.match = match; this.font = AlphaTheme.Body;
            root = Rect("Research tree", parent);
            Anchor(root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AlphaPanelDecor.Apply(root);

            era = Label("Research heading", root, 26, TextAnchor.MiddleLeft);
            era.font = AlphaTheme.Display; era.color = AlphaTheme.Gold;
            Anchor(era.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(24, -56), new Vector2(-180, -12));
            var close = MakeButton("Close research", root, "Return to map", match.Research.Close);
            Anchor(close.GetComponent<RectTransform>(), Vector2.one, Vector2.one, new Vector2(-174, -62), new Vector2(-20, -14));
            stock = Label("Research resources", root, 19, TextAnchor.MiddleLeft);
            Anchor(stock.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(24, -99), new Vector2(-24, -61));

            var steps = Rect("Era progression", root);
            Anchor(steps, new Vector2(0, 1), Vector2.one, new Vector2(24, -154), new Vector2(-24, -108));
            var row = steps.gameObject.AddComponent<HorizontalLayoutGroup>(); row.spacing = 10;
            row.childForceExpandWidth = row.childForceExpandHeight = true;
            foreach (var definition in match.World.Definition.Eras ?? Array.Empty<EraDefinition>())
            {
                var tile = Rect(definition.Id, steps); AlphaTheme.StylePanel(tile, true);
                tile.gameObject.AddComponent<LayoutElement>().preferredWidth = 200;
                var label = Label("Era", tile, 19, TextAnchor.MiddleCenter); Stretch(label.rectTransform, 5);
                label.text = definition.Tier + "  " + definition.DisplayName;
                eraSteps.Add(label);
            }

            viewport = Rect("Research viewport", root);
            Anchor(viewport, Vector2.zero, Vector2.one, new Vector2(24, 102), new Vector2(-24, -169));
            viewport.gameObject.AddComponent<Image>().color = AlphaTheme.Background;
            viewport.gameObject.AddComponent<RectMask2D>();
            content = Rect("Technology cards", viewport);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1); content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.spacing = new Vector2(14, 14); grid.padding = new RectOffset(0, 0, 0, 14);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>(); fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = content;
            scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 35;

            foreach (var definition in match.World.Definition.Technologies ?? Array.Empty<TechnologyDefinition>())
                if (match.Factions.Allows(definition.RequiredFactionId)) AddCard(definition);
            notice = Label("Research notice", root, 18, TextAnchor.MiddleLeft);
            Anchor(notice.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(24, 14), new Vector2(-24, 89));
            notice.resizeTextForBestFit = true; notice.resizeTextMinSize = 16; notice.resizeTextMaxSize = 18;
            root.gameObject.SetActive(false);
        }

        private void AddCard(TechnologyDefinition definition)
        {
            var cardRoot = Rect("Technology " + definition.Id, content);
            AlphaTheme.StylePanel(cardRoot);
            var heading = Label("Name", cardRoot, 21, TextAnchor.MiddleLeft);
            Anchor(heading.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(14, -38), new Vector2(-14, -5));
            heading.text = definition.DisplayName;
            heading.font = AlphaTheme.Strong; heading.color = AlphaTheme.Ink;
            var effect = Label("Effect", cardRoot, 17, TextAnchor.UpperLeft);
            Anchor(effect.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(14, -100), new Vector2(-14, -40));
            effect.text = definition.Description;
            effect.resizeTextForBestFit = true; effect.resizeTextMinSize = 15; effect.resizeTextMaxSize = 17;
            var requirements = Label("Requirements", cardRoot, 16, TextAnchor.UpperLeft);
            Anchor(requirements.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(14, -126), new Vector2(-14, -100));
            requirements.resizeTextForBestFit = true; requirements.resizeTextMinSize = 13; requirements.resizeTextMaxSize = 16;
            requirements.color = AlphaTheme.Muted;
            requirements.text = match.Research.Requirements(definition);
            var cost = Label("Cost and time", cardRoot, 17, TextAnchor.MiddleLeft);
            Anchor(cost.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(14, 98), new Vector2(-14, 125));
            cost.text = EconomyControls.CostText(definition.Cost) + " · " + definition.ResearchTicks / (float)World.TickRate + " s";
            var status = Label("Status", cardRoot, 16, TextAnchor.MiddleLeft);
            Anchor(status.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(14, 56), new Vector2(-14, 94));
            var button = MakeButton("Research " + definition.Id, cardRoot, "Research", () => match.Research.Start(definition.Id));
            Anchor(button.GetComponent<RectTransform>(), Vector2.zero, new Vector2(1, 0), new Vector2(14, 10), new Vector2(-14, 54));
            var fill = Rect("Research progress", cardRoot); var fillImage = fill.gameObject.AddComponent<Image>(); fillImage.color = AlphaTheme.Gold; fillImage.raycastTarget = false;
            Anchor(fill, Vector2.zero, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 4));
            cards.Add(new Card { Definition = definition, Status = status, Button = button, Action = button.GetComponentInChildren<Text>(), Fill = fill });
        }

        public void Refresh()
        {
            bool open = match.Research.IsOpen;
            root.gameObject.SetActive(open);
            if (!open) { wasOpen = false; return; }
            root.SetAsLastSibling();
            int columns = Screen.width / (float)Screen.height > 1.7f ? 2 : 3;
            grid.constraintCount = columns;
            grid.cellSize = new Vector2(Mathf.Max(280, (viewport.rect.width - grid.spacing.x * (columns - 1)) / columns), 252);
            if (!wasOpen) { scroll.verticalNormalizedPosition = 1; wasOpen = true; }
            era.text = "RESEARCH  /  " + match.Research.EraLabel;
            match.World.TryGetPlayer(MatchController.LocalPlayer, out var player);
            if (player != null)
            {
                var resources = player.Resources;
                stock.text = $"Food {resources.Food}     Wood {resources.Wood}     Metal {resources.Metal}     Stone {resources.Stone}     ·     Shared with army and construction";
                for (int i = 0; i < eraSteps.Count; i++) eraSteps[i].color = i + 1 == player.EraTier ? AlphaTheme.Gold : i + 1 < player.EraTier ? AlphaTheme.Positive : AlphaTheme.Muted;
            }
            foreach (var card in cards)
            {
                bool complete = player != null && player.HasTechnology(card.Definition.Id);
                var active = match.Research.ActiveSite(card.Definition.Id);
                var result = match.Research.Validate(card.Definition.Id);
                card.Status.text = match.Research.Status(card.Definition);
                card.Status.color = complete ? AlphaTheme.Positive : result.Accepted ? AlphaTheme.Ink : AlphaTheme.Gold;
                card.Button.interactable = !complete && active == null && result.Accepted;
                card.Action.text = complete ? "Completed" : active != null ? "Researching" : result.Accepted ? string.IsNullOrEmpty(card.Definition.AdvancesToEraId) ? "Research" : "Advance Era" : "Locked";
                float progress = complete ? 1 : active == null ? 0 : 1 - active.ActiveResearch.RemainingTicks / (float)active.ActiveResearch.TotalTicks;
                card.Fill.anchorMax = new Vector2(progress, 0);
            }
            notice.text = match.Research.Notice + "\nScroll to see the full tree. Match continues while browsing; destroying a research site loses its unfinished investment.";
        }

        public Button ButtonFor(string id)
        {
            foreach (var card in cards) if (card.Definition.Id == id) return card.Button;
            return null;
        }
        public string StatusFor(string id)
        {
            foreach (var card in cards) if (card.Definition.Id == id) return card.Status.text;
            return "";
        }
        public void ScrollToBottom() => scroll.verticalNormalizedPosition = 0;
        public void Reveal(string id)
        {
            Canvas.ForceUpdateCanvases();
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i].Definition.Id != id) continue;
                float hiddenHeight = Mathf.Max(0, content.rect.height - viewport.rect.height);
                float cardTop = (i / grid.constraintCount) * (grid.cellSize.y + grid.spacing.y);
                scroll.verticalNormalizedPosition = hiddenHeight > 0 ? 1 - Mathf.Clamp01(cardTop / hiddenHeight) : 1;
                Canvas.ForceUpdateCanvases();
                return;
            }
        }

        private Button MakeButton(string name, Transform parent, string value, UnityEngine.Events.UnityAction callback)
        {
            var rect = Rect(name, parent);
            var image = rect.gameObject.AddComponent<Image>(); image.color = new Color(.24f, .35f, .39f);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(callback);
            var colors = button.colors; colors.disabledColor = new Color(.52f, .55f, .55f, 1); button.colors = colors;
            var text = Label("Label", rect, 18, TextAnchor.MiddleCenter); text.text = value; Stretch(text.rectTransform, 4);
            AlphaTheme.StyleButton(button, name.StartsWith("Research ", StringComparison.Ordinal));
            return button;
        }
        private Text Label(string name, Transform parent, int size, TextAnchor alignment)
        {
            var rect = Rect(name, parent); var text = rect.gameObject.AddComponent<Text>(); text.font = font;
            text.fontSize = size; text.color = AlphaTheme.Ink; text.alignment = alignment; text.raycastTarget = false;
            return text;
        }
        private static RectTransform Rect(string name, Transform parent)
        { var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false); return rect; }
        private static void Stretch(RectTransform rect, int padding) => Anchor(rect, Vector2.zero, Vector2.one, Vector2.one * padding, Vector2.one * -padding);
        private static void Anchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 from, Vector2 to)
        { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = from; rect.offsetMax = to; }
    }
}
