using System;
using Emberfield.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace Emberfield.Presentation
{
    public sealed partial class MatchHud
    {
        private readonly Text[] stockValues = new Text[5], stockRates = new Text[5];
        private Text matchSubtitle;
        private RectTransform selectionFrame;
        private Image selectionHealth;
        private RectTransform healthTrack;
        private Text performanceOverlay;

        private void CreateResourcePills(RectTransform top)
        {
            matchSubtitle = Label("Match subtitle", top, 12); matchSubtitle.color = AlphaTheme.Muted;
            Anchor(matchSubtitle.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(70, -66), new Vector2(261, -43));
            var band = Rect("Resource ledger", top);
            Anchor(band, new Vector2(0, 1), Vector2.one, new Vector2(277, -73), new Vector2(match.Research.Available ? -202 : -18, -11));
            var layout = band.gameObject.AddComponent<HorizontalLayoutGroup>(); layout.spacing = 8;
            layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = layout.childForceExpandHeight = true;
            var names = new[] { "FOOD", "WOOD", "METAL", "STONE", "PEOPLE" };
            var symbols = new[] { AlphaSymbol.Food, AlphaSymbol.Wood, AlphaSymbol.Metal, AlphaSymbol.Stone, AlphaSymbol.Population };
            var colors = new[] { new Color(.88f,.72f,.42f), new Color(.49f,.69f,.53f), new Color(.63f,.77f,.77f), new Color(.76f,.75f,.7f), AlphaTheme.Gold };
            for (int i = 0; i < names.Length; i++)
            {
                var tile = Rect(names[i], band); AlphaTheme.StylePanel(tile, true);
                tile.gameObject.AddComponent<LayoutElement>().preferredWidth = 120;
                var icon = AlphaIcon.Create(tile, symbols[i], colors[i]);
                Anchor(icon.rectTransform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(11, -13), new Vector2(36, 16));
                var name = Label("Resource", tile, 10); name.text = names[i]; name.font = AlphaTheme.Strong; name.color = AlphaTheme.Muted;
                Anchor(name.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(44, -18), new Vector2(-8, -4));
                stockValues[i] = Label("Amount", tile, 23); stockValues[i].font = AlphaTheme.Strong;
                Anchor(stockValues[i].rectTransform, Vector2.zero, Vector2.one, new Vector2(44, 16), new Vector2(-7, -18));
                stockValues[i].resizeTextForBestFit = true; stockValues[i].resizeTextMinSize = 16; stockValues[i].resizeTextMaxSize = 23;
                stockRates[i] = Label("Rate", tile, 10); stockRates[i].color = AlphaTheme.Positive;
                Anchor(stockRates[i].rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(44, 2), new Vector2(-5, 16));
                stockRates[i].resizeTextForBestFit = true; stockRates[i].resizeTextMinSize = 9; stockRates[i].resizeTextMaxSize = 10;
            }
            band.gameObject.SetActive(match.Stress == null); matchSubtitle.gameObject.SetActive(match.Stress == null);
            performanceOverlay = Label("Performance overlay", safeRoot, 13); performanceOverlay.color = AlphaTheme.Ink;
            Anchor(performanceOverlay.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -175), new Vector2(465, -138));
            performanceOverlay.gameObject.SetActive(false);
        }
        // What each pill last showed. A pill's text is composed again only when its number changes, so a settlement
        // whose stock holds still allocates nothing between deliveries.
        private readonly long[] shownStock = new long[5], shownRates = new long[5];
        private long shownClock = -1;
        private void RefreshResourcePills(PlayerState player)
        {
            var stock = player.Resources;
            for (int i = 0; i < 4; i++)
            {
                int amount = stock.Get((ResourceKind)i);
                if (!rewrite && shownStock[i] == amount) continue;
                shownStock[i] = amount; Show(stockValues[i], amount.ToString());
            }
            long population = (long)player.PopulationUsed << 32 | (uint)player.PopulationCapacity;
            if (rewrite || shownStock[4] != population)
            { shownStock[4] = population; Show(stockValues[4], player.PopulationUsed + " / " + player.PopulationCapacity); }
            if (match.World.Match == null) Show(matchSubtitle, match.IsCombatSandbox ? "COMBAT PRACTICE" : match.IsFactionDrill ? "FACTION PRACTICE" : "SETTLEMENT PRACTICE");
            else
            {
                // The clock reads whole seconds: once a second is when this line can change.
                long clock = match.World.Match.ElapsedTicks / World.TickRate * 16 + (int)match.World.Match.Mode * 2 + (match.World.IsNetworkReplica ? 1 : 0);
                if (rewrite || clock != shownClock)
                {
                    shownClock = clock;
                    Show(matchSubtitle, (match.World.IsNetworkReplica ? "ONLINE · " : "") + match.World.Match.Mode.ToString().ToUpperInvariant() + " · " + OfflinePanel.Clock(match.World.Match.ElapsedTicks));
                }
            }
            if (match.Metrics == null) return;
            performanceOverlay.gameObject.SetActive(match.Alpha?.Settings.Value.ShowPerformanceMetrics == true);
            if (performanceOverlay.gameObject.activeSelf)
                Show(performanceOverlay, $"{match.Metrics.FramesPerSecond:0} FPS  ·  FRAME p95 {match.Metrics.FrameP95Milliseconds:0.0} ms" +
                    (match.Metrics.HasLocalTickTiming ? $"  ·  RULES p95 {match.Metrics.TickP95Milliseconds:0.00} ms" : "  ·  ONLINE"));
            for (int i = 0; i < 4; i++)
            {
                var kind = (ResourceKind)i;
                double rate = match.Metrics.HasExactIncome ? match.Metrics.IncomePerMinute(kind) : match.Metrics.NetPerMinute(kind);
                double rounded = Math.Round(rate);
                long key = (long)rounded * 2 + (match.Metrics.HasExactIncome ? 1 : 0);
                if (rewrite || shownRates[i] != key)
                {
                    shownRates[i] = key;
                    Show(stockRates[i], (match.Metrics.HasExactIncome ? "+" : "NET ") + rounded.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/MIN");
                }
                stockRates[i].color = rate < 0 ? AlphaTheme.Danger : AlphaTheme.Positive;
            }
            long waiting = player.PopulationReserved > 0 ? player.PopulationReserved : -1L - match.Metrics.IdleWorkers;
            if (rewrite || shownRates[4] != waiting)
            {
                shownRates[4] = waiting;
                Show(stockRates[4], player.PopulationReserved > 0 ? player.PopulationReserved + " IN TRAINING" : match.Metrics.IdleWorkers + " IDLE WORKERS");
            }
            stockRates[4].color = player.PopulationUsed + player.PopulationReserved >= player.PopulationCapacity ? AlphaTheme.Gold : AlphaTheme.Muted;
        }
        private void CreateSelectionFrame(RectTransform bottom)
        {
            selectionFrame = Rect("Selected entity frame", bottom); AlphaTheme.StylePanel(selectionFrame, true);
            healthTrack = Rect("Health track", selectionFrame);
            Anchor(healthTrack, Vector2.zero, new Vector2(1, 0), new Vector2(14, 9), new Vector2(-14, 13));
            healthTrack.gameObject.AddComponent<Image>().color = AlphaTheme.Border;
            var fill = Rect("Health", healthTrack); Stretch(fill);
            selectionHealth = fill.gameObject.AddComponent<Image>(); selectionHealth.color = AlphaTheme.Positive; selectionHealth.raycastTarget = false;
        }
        private void LayoutSelectionFrame(MobileHudLayout.Metrics metrics)
        {
            selectionFrame.anchorMin = selectionFrame.anchorMax = selectionFrame.pivot = Vector2.zero;
            selectionFrame.anchoredPosition = metrics.Details.position; selectionFrame.sizeDelta = metrics.Details.size;
            details.rectTransform.anchoredPosition = metrics.Details.position + new Vector2(14, 20);
            details.rectTransform.sizeDelta = metrics.Details.size - new Vector2(28, 26);
        }
        private void RefreshSelectionHealth()
        {
            long current = 0, maximum = 0;
            foreach (int id in match.Selection)
            {
                if (match.World.TryGetUnit(id, out var unit)) { current += unit.Health; maximum += unit.MaxHealth; }
                else if (match.World.TryGetBuilding(id, out var building)) { current += building.Health; maximum += building.MaxHealth; }
            }
            healthTrack.gameObject.SetActive(maximum > 0);
            float fraction = maximum > 0 ? (float)(current / (double)maximum) : 0;
            selectionHealth.rectTransform.anchorMax = new Vector2(fraction, 1);
            selectionHealth.color = fraction < .3f ? AlphaTheme.Danger : fraction < .65f ? AlphaTheme.Gold : AlphaTheme.Positive;
        }
        private static void AddCommandIcon(RectTransform rect, Text label, string text)
        {
            AlphaSymbol symbol;
            if (text == "Workers") symbol = AlphaSymbol.Worker;
            else if (text == "Army") symbol = AlphaSymbol.Army;
            else if (text == "Stop") symbol = AlphaSymbol.Stop;
            else if (text.StartsWith("Select:", StringComparison.Ordinal)) symbol = AlphaSymbol.Select;
            else if (text.StartsWith("Formation", StringComparison.Ordinal)) symbol = AlphaSymbol.Formation;
            else if (text == "Clear" || text == "Cancel") symbol = AlphaSymbol.Clear;
            else if (text == "Home") symbol = AlphaSymbol.Home;
            else if (text == "Menu") symbol = AlphaSymbol.Menu;
            else if (text == "Settings") symbol = AlphaSymbol.Settings;
            else if (text.IndexOf("Research", StringComparison.OrdinalIgnoreCase) >= 0) symbol = AlphaSymbol.Research;
            // A build or train tile carries a name over its cost. The one generic icon they all shared told
            // them apart from nothing and took a third of the tile, which truncated the longer costs.
            else return;
            var icon = AlphaIcon.Create(rect, symbol, text == "Workers" || text == "Army" ? AlphaTheme.Background : text == "Stop" ? AlphaTheme.Muted : AlphaTheme.Gold);
            Anchor(icon.rectTransform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(10, -11), new Vector2(31, 11));
            label.rectTransform.offsetMin = new Vector2(38, 3); label.rectTransform.offsetMax = new Vector2(-7, -3);
        }
    }
}
