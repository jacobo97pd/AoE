using System;
using System.Collections.Generic;
using System.Text;
using Emberfield.Diagnostics;
using Emberfield.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace Emberfield.Presentation
{
    public sealed partial class MatchHud : IDisposable
    {
        private readonly MatchController match;
        private readonly GameObject canvasObject;
        private readonly RectTransform safeRoot, box;
        private readonly Text title, resources, details, feedback;
        private readonly Text formationControl;
        private readonly Text researchControl;
        public ResearchPanel ResearchPanel { get; }
        public FactionPanel FactionPanel { get; }
        public OfflinePanel OfflinePanel { get; }
        public OnlinePanel OnlinePanel { get; }
        public AlphaPanel AlphaPanel { get; }
        public VoicePanel VoicePanel { get; }
        public AlphaTutorialView TutorialView { get; }
        public MinimapView Minimap { get; }
        private readonly Text touchSelectionControl, objectiveStatus;
        private readonly RectTransform contextRow;
        private readonly RectTransform commandsRoot, actionRow;
        private readonly GridLayoutGroup actionLayout, contextLayout;
        private readonly CanvasScaler scaler;
        private int layoutWidth, layoutHeight, layoutActionCount = -1, layoutContextCount = -1;
        private Rect layoutSafeArea;
        private readonly Font font;
        private readonly Canvas canvas;
        private readonly Text debug;
        private float nextRefresh;
        private string contextKey;
        private readonly Color ink = AlphaTheme.Ink;

        public MatchHud(MatchController match, Transform parent)
        {
            this.match = match;
            font = AlphaTheme.Body;
            canvasObject = new GameObject("Tablet HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(parent, false);
            canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.matchWidthOrHeight = 1;
            safeRoot = Rect("Safe area", canvasObject.transform); Stretch(safeRoot);
            var top = Panel("Top bar", safeRoot); Anchor(top, new Vector2(0, 1), Vector2.one, new Vector2(0, -84), Vector2.zero);
            var crest = AlphaIcon.Create(top, AlphaSymbol.Shield, AlphaTheme.Gold);
            Anchor(crest.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(22, -60), new Vector2(55, -21));
            title = Label("Title", top, 21); title.font = AlphaTheme.Display;
            Anchor(title.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(70, -43), new Vector2(261, -14));
            resources = Label("Resources", top, 19); Anchor(resources.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(22, 4), new Vector2(-22, 34));
            resources.resizeTextForBestFit = true; resources.resizeTextMinSize = 17; resources.resizeTextMaxSize = 23;
            resources.gameObject.SetActive(match.Stress != null);
            CreateResourcePills(top);
            if (match.Research.Available)
            {
                researchControl = Button(top, "Settlement\nResearch", match.Research.Open);
                AlphaTheme.StyleButton(researchControl.GetComponentInParent<Button>(), true);
                Anchor((RectTransform)researchControl.transform.parent, Vector2.one, Vector2.one, new Vector2(-184, -70), new Vector2(-18, -13));
            }
            var bottom = commandsRoot = Panel("Commands", safeRoot); Anchor(bottom, Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 250));
            CreateSelectionFrame(bottom);
            details = Label("Selection", bottom, 19); Anchor(details.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(22, -60), new Vector2(-22, -6));
            details.resizeTextForBestFit = true; details.resizeTextMinSize = 15; details.resizeTextMaxSize = 19;
            contextRow = Rect("Context actions", bottom); Anchor(contextRow, Vector2.zero, new Vector2(1, 0), new Vector2(18, 110), new Vector2(-18, 170));
            contextLayout = contextRow.gameObject.AddComponent<GridLayoutGroup>();
            var row = actionRow = Rect("Actions", bottom); Anchor(row, new Vector2(0, 0), new Vector2(1, 0), new Vector2(18, 56), new Vector2(-18, 108));
            actionLayout = row.gameObject.AddComponent<GridLayoutGroup>();
            if (match.Stress != null)
            {
                Button(row, "Run orders", match.StartStress);
                Button(row, "Next scenario", () => match.ReloadStress(match.PerformanceUnitCount, (MovementScenarioKind)(((int)match.PerformanceScenario + 1) % 6)));
                Button(row, "Follow / Pan", () => { match.Stress.FollowCamera = !match.Stress.FollowCamera; match.SetFeedback(match.Stress.FollowCamera ? "Camera follows the moving group." : "Camera is free to pan."); });
                Button(row, "Frame army", () => match.Stress.FrameArmy(match.Rig, true));
                Button(row, "Reset", match.Restart);
                Button(row, "Economy", () => UnityEngine.SceneManagement.SceneManager.LoadScene("Greybox"));
            }
            else
            {
                Button(row, "Workers", match.SelectWorkers);
                Button(row, "Army", match.SelectArmy);
                Button(row, "Stop", match.StopSelected);
                if (match.World.Match != null) touchSelectionControl = Button(row, "Select: off", match.ToggleTouchSelection);
                formationControl = Button(row, "Formation\nLoose", match.CycleFormation);
                formationControl.resizeTextForBestFit = true;
                formationControl.resizeTextMinSize = 12;
                formationControl.resizeTextMaxSize = 18;
                Button(row, "Clear", match.ClearSelection);
                Button(row, "Home", match.Rig.Home);
                Button(row, "Rotate view", match.RotateView);
                // Only orders live on this row. Everything that changes or leaves the session — play, online,
                // the other sandbox, factions and restart — waits inside Menu, so the row stays one line.
                Button(row, "Menu", match.OfflineControls.Open);
                if (match.Alpha != null) Button(row, "Settings", match.Alpha.Open);
            }
            feedback = Label("Feedback", bottom, 17); feedback.color = AlphaTheme.Muted;
            Anchor(feedback.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(22, 5), new Vector2(-22, 33));
            feedback.resizeTextForBestFit = true; feedback.resizeTextMinSize = 14; feedback.resizeTextMaxSize = 17;
            box = Rect("Selection rectangle", canvasObject.transform);
            var image = box.gameObject.AddComponent<Image>(); image.color = new Color(.9f, .75f, .2f, .2f); image.raycastTarget = false;
            box.gameObject.SetActive(false);
            CreateWallRunTag();
            if (match.Stress != null)
            {
                debug = Label("Developer metrics", safeRoot, 17);
                Anchor(debug.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(22, -128), new Vector2(-22, -94));
                debug.color = new Color(.85f, .87f, .85f);
            }
            // Practice/sandbox worlds have no Match or Vision, but they still have a Map to draw; only the
            // movement-stress fixture (its own debug overlay, no orders) goes without a minimap.
            if (match.Stress == null) Minimap = new MinimapView(match, safeRoot, font);
            if (match.Research.Available) ResearchPanel = new ResearchPanel(match, safeRoot, font);
            if (match.Factions.Available) FactionPanel = new FactionPanel(match, safeRoot, font);
            if (match.World.Match != null || match.IsChallenge)
            {
                var strip = Panel("Match objective clock", safeRoot);
                Anchor(strip, new Vector2(0, 1), Vector2.one, new Vector2(18, -125), new Vector2(-18, -92));
                objectiveStatus = Label("Objectives", strip, 15); Stretch(objectiveStatus.rectTransform, 14, 2);
                objectiveStatus.color = AlphaTheme.Gold;
                objectiveStatus.resizeTextForBestFit = true; objectiveStatus.resizeTextMinSize = 13; objectiveStatus.resizeTextMaxSize = 15;
            }
            if (match.Stress == null) OfflinePanel = new OfflinePanel(match, safeRoot, font);
            if (match.Stress == null) OnlinePanel = new OnlinePanel(match, safeRoot, font);
            if (match.Alpha != null)
            {
                TutorialView = new AlphaTutorialView(match, safeRoot, font);
                AlphaPanel = new AlphaPanel(match, safeRoot, font);
                if (match.Voice != null) VoicePanel = new VoicePanel(match, commandsRoot);
                canvasObject.AddComponent<AlphaTextContrast>().Initialize(match);
            }
            Refresh();
        }

        public void Refresh()
        {
            using var marker = PresentationMarkers.Hud.Auto();
            RefreshLayout();
            Minimap?.Refresh();
            RefreshWallRun();
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + .1f;
            using var text = PresentationMarkers.HudText.Auto();
            if (match.Stress != null) { RefreshStress(); RefreshLayout(); return; }
            ResearchPanel?.Refresh();
            FactionPanel?.Refresh();
            OfflinePanel?.Refresh();
            TutorialView?.Refresh();
            OnlinePanel?.Refresh();
            AlphaPanel?.Refresh();
            VoicePanel?.Refresh();
            if (touchSelectionControl != null) Show(touchSelectionControl, match.TouchSelectionMode ? "Select: ON" : "Select: off");
            if (objectiveStatus != null) { if (match.Challenge != null) RefreshChallengeObjective(); else RefreshOfflineObjective(); }
            if (formationControl != null && (rewrite || shownFormation != match.Formation))
            { shownFormation = match.Formation; Show(formationControl, "Formation\n" + match.Formation); }
            if (researchControl != null && match.World.TryGetPlayer(MatchController.LocalPlayer, out var researchPlayer) &&
                (rewrite || shownEraTier != researchPlayer.EraTier || shownEraId != researchPlayer.EraId))
            {
                shownEraTier = researchPlayer.EraTier; shownEraId = researchPlayer.EraId;
                Show(researchControl, researchPlayer.EraTier + " · " + match.Research.EraName(researchPlayer.EraId) + "\nResearch");
            }
            match.Economy.RefreshPreview();
            match.Factions.RefreshPreview();
            Show(title, match.Challenge != null ? match.Challenge.Definition.Name
                : match.World.Match != null || match.IsFactionDrill ? match.Factions.LocalDefinition.DisplayName : match.IsCombatSandbox ? "Battle practice" : "Amber Reach");
            title.resizeTextForBestFit = true; title.resizeTextMinSize = 16; title.resizeTextMaxSize = 21;
            if (match.World.TryGetPlayer(MatchController.LocalPlayer, out var player))
            {
                var stock = player.Resources;
                // Only Stress shows the one-line stock; the pills below are what a match reads.
                if (resources.gameObject.activeSelf)
                    Show(resources, $"Food {stock.Food}     Wood {stock.Wood}     Metal {stock.Metal}     Stone {stock.Stone}     People {player.PopulationUsed}+{player.PopulationReserved}/{player.PopulationCapacity}");
                RefreshResourcePills(player);
            }
            detailText = match.Selection.Count == 0 ? (match.IsCombatSandbox ? "Your army awaits. Select a unit to give an order." : "Your settlement awaits. Select a Tender to gather and build.") : SelectedCountText();
            if (match.IsFactionDrill && match.Selection.Count == 0) detailText = FrontierCodex.Description(match.Factions.LocalDefinition.Id);
            if (match.World.Match != null && match.Selection.Count == 0) detailText = "Command your settlement · gather resources, scout the crossing and protect your Hearth.";
            if (match.World.IsNetworkReplica && match.Selection.Count == 0) detailText = "Command your settlement · the online match continues while menus are open.";
            if (match.Selection.Count == 0 && match.Metrics != null) detailText = MetricsText(match.Metrics);
            if (match.Selection.Count == 1)
            {
                int id = match.Selection[0];
                if (match.World.TryGetUnit(id, out var u))
                {
                    var definition = match.Economy.UnitDefinition(u.DefinitionId);
                    string task = u.AttackTargetId != 0 ? "Attacking #" + u.AttackTargetId : !u.AutoAttackEnabled && !u.IsWorker ? "Holding fire" : u.IsWorker && u.WorkerTask != WorkerTask.None ? WorkerTaskText(u.WorkerTask) : u.Order.ToString();
                    string role = PirateRole(u.DefinitionId) ?? RoleName(u.Tags);
                    detailText = definition.DisplayName + " / " + role + NavalUnitDetails(u) + "  ·  " + u.Health + "/" + u.MaxHealth + " health  ·  " + task;
                    // A worker now carries both: what it gathers, and the modest attack it defends itself with.
                    if (u.IsWorker) detailText += "  ·  gathers " + u.GatherAmount + " per work cycle · carry " + u.CarryCapacity;
                    if (u.AttackDamage > 0) detailText += $"\nAttack {u.AttackDamage}  ·  Armor {u.Armor}  ·  Range {definition.Attack.RangeMillimetres * .001f:0.0}m  ·  " +
                        CounterHint(definition);
                    if (u.CarriedAmount > 0) detailText += "  ·  carrying " + u.CarriedAmount + " " + u.CarriedKind;
                    AppendFactionUnitDetails(u);
                    if (u.WallId != 0) detailText += " · On wall deck";
                    else if (u.BoardingRemainingTicks > 0) detailText += " · Climbing " + (u.BoardingRemainingTicks / (float)World.TickRate).ToString("0.0") + "s";
                }
                else if (match.World.TryGetBuilding(id, out var b))
                {
                    detailText = match.Economy.BuildingDefinition(b.DefinitionId).DisplayName + (b.IsComplete ? "  ·  Ready" : $"  ·  Constructing {b.ConstructionProgress * 100:0}%") + $"  ·  {b.Health}/{b.MaxHealth} health";
                    if (b.ProductionQueue.Count > 0)
                    {
                        var first = b.ProductionQueue[0];
                        detailText += $"  ·  {match.Economy.UnitDefinition(first.UnitDefinitionId).DisplayName} {first.RemainingTicks * 1000f / (World.TickRate * b.ProductionWorkPermille):0.0}s at current rate  ·  {b.ProductionQueue.Count} queued";
                    }
                    if (b.ActiveResearch != null)
                        detailText += $"\nResearch: {match.Research.TechnologyName(b.ActiveResearch.TechnologyId)} · {b.ActiveResearch.RemainingTicks / (float)World.TickRate:0.0}s · training occupied";
                    AppendFactionBuildingDetails(b);
                }
            }
            Show(details, detailText);
            RefreshContext();
            RefreshLayout();
            RefreshFactionButtons();
            RefreshSelectionHealth();
            Show(feedback, match.Feedback);
            if (debug != null)
            {
                debug.gameObject.SetActive(match.World.Match == null);
                if (match.World.Match == null) debug.text = $"{1f / Mathf.Max(.001f, Time.smoothDeltaTime):0} FPS  ·  tick {match.World.TickIndex}  ·  simulation {match.LastTickMilliseconds:F3} ms  ·  {match.View.Count} entities";
            }
            rewrite = false;
        }

        public void Invalidate() { nextRefresh = 0; contextKey = null; rewrite = true; }

        // The source text each label was last given; UiLocalization shows its translation. Writing the same source again
        // would put the untranslated text back until the canvases draw, so the label and its canvas would rebuild twice
        // every refresh for nothing. Invalidate writes every label again.
        private readonly Dictionary<Text, string> shownSources = new Dictionary<Text, string>();
        private bool rewrite = true;
        private string detailText;
        private MovementFormation shownFormation;
        private int shownEraTier, shownSelected, shownWorkers, shownIdle, shownArmy, shownBusy, shownProducers;
        private string shownEraId, selectedText, metricsText;
        private double shownOrders;
        private void Show(Text label, string source)
        {
            if (!rewrite && shownSources.TryGetValue(label, out var last) && last == source) return;
            shownSources[label] = source; label.text = source;
        }
        private readonly StringBuilder contextBuilder = new StringBuilder(256);
        // StringBuilder.Append(int) makes a string of the number first; this writes the digits straight in.
        private static StringBuilder AppendNumber(StringBuilder text, long value)
        {
            if (value < 0) { text.Append('-'); value = -value; }
            long scale = 1;
            while (scale <= value / 10) scale *= 10;
            for (; scale > 0; scale /= 10) text.Append((char)('0' + value / scale % 10));
            return text;
        }
        private static bool Same(StringBuilder text, string other)
        {
            if (text.Length != other.Length) return false;
            for (int i = 0; i < other.Length; i++) if (text[i] != other[i]) return false;
            return true;
        }
        private string SelectedCountText()
        {
            if (selectedText == null || shownSelected != match.Selection.Count)
            { shownSelected = match.Selection.Count; selectedText = shownSelected + " selected · tap an enemy to attack, or open ground to move"; }
            return selectedText;
        }
        // Composed again only when a number in it changes, so an idle settlement's summary allocates nothing.
        private string MetricsText(AlphaMatchMetrics metrics)
        {
            if (metricsText == null || shownWorkers != metrics.Workers || shownIdle != metrics.IdleWorkers || shownArmy != metrics.Army ||
                shownBusy != metrics.BusyProducers || shownProducers != metrics.Producers || shownOrders != metrics.OrdersPerMinute)
            {
                shownWorkers = metrics.Workers; shownIdle = metrics.IdleWorkers; shownArmy = metrics.Army;
                shownBusy = metrics.BusyProducers; shownProducers = metrics.Producers; shownOrders = metrics.OrdersPerMinute;
                metricsText = $"WORKERS  {shownWorkers}  ·  {shownIdle} idle       ARMY  {shownArmy}       PRODUCING  {shownBusy}/{shownProducers}       ORDERS/MIN  {shownOrders:0}";
            }
            return metricsText;
        }

        private void RefreshLayout(bool force = false)
        {
            int actions = ActiveChildren(actionRow), context = ActiveChildren(contextRow);
            var safe = Screen.safeArea;
            bool viewportChanged = layoutWidth != Screen.width || layoutHeight != Screen.height || layoutSafeArea != safe;
            if (!force && !viewportChanged && actions == layoutActionCount && context == layoutContextCount) return;
            layoutWidth = Screen.width; layoutHeight = Screen.height; layoutSafeArea = safe;
            layoutActionCount = actions; layoutContextCount = context;
            var metrics = MobileHudLayout.Resolve(layoutWidth, layoutHeight, safe, actions, context);
            scaler.referenceResolution = metrics.ReferenceResolution;
            canvas.scaleFactor = metrics.Scale;
            MobileHudLayout.Apply(metrics, safeRoot, commandsRoot, actionRow, actionLayout, contextRow, contextLayout,
                details.rectTransform, feedback.rectTransform);
            LayoutSelectionFrame(metrics);
            if (viewportChanged) { nextRefresh = 0; box.gameObject.SetActive(false); }
            Canvas.ForceUpdateCanvases();
        }

        private static int ActiveChildren(Transform row)
        {
            int count = 0;
            for (int index = 0; index < row.childCount; index++) if (row.GetChild(index).gameObject.activeSelf) count++;
            return count;
        }

        private void RefreshStress()
        {
            var stress = match.Stress;
            var observation = stress.Observer.Current;
            var navigation = match.World.NavigationMetrics.Snapshot;
            double frame = stress.SmoothedFrameMilliseconds;
            title.text = $"MOVEMENT STRESS    /    {match.PerformanceScenario}    /    {match.PerformanceUnitCount} movers";
            title.resizeTextForBestFit = true; title.resizeTextMinSize = 14; title.resizeTextMaxSize = 23;
            resources.text = $"{(frame > 0 ? 1000 / frame : 0):0} FPS   ·   frame {frame:0.00} ms   ·   simulation {match.LastTickMilliseconds:0.000} ms   ·   route searches {stress.LastNavigationMilliseconds:0.000} ms";
            details.text = $"Arrived {observation.Arrived}/{stress.Scenario.UnitIds.Count}  ·  stalled {observation.Stalled}  ·  overlaps {observation.OverlapPairs} (peak {observation.MaxOverlapPairs})  ·  invalid {observation.MaxInvalidPositions}\nOrder {stress.CommandMilliseconds:0.00} ms  ·  {navigation.TotalQueryCount} searches  ·  elapsed {stress.Scenario.ElapsedTicks / (float)World.TickRate:0.0}s  ·  {stress.FrameSamples} frame samples";
            if (contextKey != "stress")
            {
                contextKey = "stress";
                for (int i = contextRow.childCount - 1; i >= 0; i--) { var child = contextRow.GetChild(i).gameObject; child.SetActive(false); UnityEngine.Object.Destroy(child); }
                foreach (int count in MovementStressSession.Counts)
                    Button(contextRow, count + " units", () => match.ReloadStress(count, match.PerformanceScenario));
            }
            feedback.text = stress.Status + " Manual orders change the fixture.";
            if (debug != null) debug.text = "Unarmed navigation fixture · uncapped rendering · normal 20 Hz rules · desktop measurements";
        }

        private void RefreshContext()
        {
            // The buttons' lambdas below reach the economy through match: one that captured this local would make every
            // refresh allocate a closure, not only the ones that rebuild the row.
            var economy = match.Economy;
            var building = economy.SelectedBuilding();
            bool workers = match.Selection.Count > 0 && economy.SelectedWorkers().Length > 0;
            bool cargo = economy.HasSelectedCargo();
            bool army = match.SelectedCombatUnits().Length > 0;
            int spears = 0, archers = 0, cavalry = 0, creatures = 0, siege = 0, heroes = 0, ships = 0;
            foreach (int id in match.Selection)
                if (match.World.TryGetUnit(id, out var selected) && selected.OwnerId == MatchController.LocalPlayer)
                {
                    if (selected.DefinitionId == CorsairHeroScenario.HeroId) { heroes++; continue; }
                    switch (ArmyRole(selected))
                    {
                        case CombatTags.Infantry: spears++; break;
                        case CombatTags.Ranged: archers++; break;
                        case CombatTags.Cavalry: cavalry++; break;
                        case CombatTags.Creature: creatures++; break;
                        case CombatTags.Siege: siege++; break;
                        case CombatTags.Naval: ships++; break;
                    }
                }
            army |= siege > 0;
            // The key names everything the row's buttons depend on. It is written into one reused builder and only turned
            // into a string when it differs from the last one, which is when the row is built again.
            var key = contextBuilder.Clear();
            if (economy.PendingBuildingId != null)
            {
                key.Append(economy.PendingBuildingId);
                // The wall run's Confirm and Undo buttons are enabled once there is something to order or take back.
                if (economy.WallRun.Active) key.Append(":run:").Append(economy.WallRun.AcceptedCount > 0).Append(':').Append(economy.WallRun.CornerCount > 0);
            }
            else if (economy.IsChoosingRally) key.Append("rally");
            else if (building != null) AppendNumber(key, building.Id).Append(':').Append(building.IsComplete);
            else if (workers) key.Append("workers:").Append(cargo);
            else key.Append(army ? "army" : "none");
            if (army && !workers)
            {
                key.Append(":roles:"); AppendNumber(key, spears).Append(':'); AppendNumber(key, archers).Append(':'); AppendNumber(key, cavalry).Append(':');
                AppendNumber(key, creatures).Append(':'); AppendNumber(key, siege).Append(':'); AppendNumber(key, heroes).Append(':'); AppendNumber(key, ships);
            }
            AppendNavalContextKey(key);
            if (match.World.TryGetPlayer(MatchController.LocalPlayer, out var owner))
            { key.Append(":research:"); AppendNumber(key, owner.CompletedTechnologyIds.Count).Append(':').Append(building?.ActiveResearch?.TechnologyId); }
            if (building != null)
                foreach (string trainable in economy.BuildingDefinition(building.DefinitionId).TrainableUnitIds)
                    key.Append(":recruit:").Append(trainable).Append(':').Append(match.World.ValidateUnitRecruitment(MatchController.LocalPlayer, trainable).Accepted);
            AppendFactionContextKey(key);
            key.Append(":siege:").Append(match.ChoosingWall).Append(':').Append(match.ChoosingDescent).Append(':');
            if (building != null) key.Append(building.GateOpen);
            key.Append(':'); AppendNumber(key, match.WallInfantry(true).Length).Append(":build:"); AppendNumber(key, constructionPage);
            if (contextKey != null && Same(key, contextKey)) return;
            contextKey = key.ToString();
            factionButtons.Clear();
            for (int i = contextRow.childCount - 1; i >= 0; i--)
            {
                var child = contextRow.GetChild(i).gameObject; child.SetActive(false); UnityEngine.Object.Destroy(child);
            }
            if (match.ChoosingShip || match.ChoosingLanding) Button(contextRow, "Cancel ship order", match.CancelNavalTarget);
            else if (match.ChoosingWall || match.ChoosingDescent)
            {
                Button(contextRow,"Cancel wall order",match.CancelSiegeTarget);
            }
            else if (match.Factions.PendingDeployUnitId != 0)
            {
                Button(contextRow, "Confirm deploy", () => match.Factions.ConfirmDeploy());
                Button(contextRow, "Cancel deploy", match.Factions.CancelDeploy);
            }
            else if (economy.WallRun.Active) AddWallRunActions();
            else if (economy.PendingBuildingId != null)
            {
                Button(contextRow, "Confirm placement", () => match.Economy.ConfirmBuild());
                // A gate can stand in a north-south wall. It turns by itself beside one; this turns it by hand (or R).
                if (BuildingFootprints.CanTurn(economy.BuildingDefinition(economy.PendingBuildingId))) Button(contextRow, "Rotate gate", economy.RotatePending);
                Button(contextRow, "Cancel placement", economy.CancelBuild);
            }
            else if (economy.IsChoosingRally)
            {
                Button(contextRow, "Cancel rally", economy.CancelBuild);
            }
            else if (building != null && building.IsComplete)
            {
                foreach (string id in economy.BuildingDefinition(building.DefinitionId).TrainableUnitIds)
                {
                    var unit = economy.UnitDefinition(id);
                    if (!match.Factions.Allows(unit.RequiredFactionId)) continue;
                    if (!string.IsNullOrEmpty(unit.RequiredRealmId) && unit.RequiredRealmId != match.World.Map.RealmId) continue;
                    var recruitment = match.World.ValidateUnitRecruitment(MatchController.LocalPlayer, id);
                    string cost = !recruitment.Accepted && unit.MaxAlivePerPlayer > 0 ? "Límite de héroe alcanzado" : EconomyControls.CostText(unit.Cost);
                    var label = Button(contextRow, "Train " + unit.DisplayName + "\n" + cost, () => match.Economy.Train(id));
                    label.GetComponentInParent<Button>().interactable = recruitment.Accepted && building.ActiveResearch == null && match.World.ValidateRequirements(MatchController.LocalPlayer, unit.RequiredEraId, unit.RequiredTechnologyIds).Accepted;
                }
                if (economy.BuildingDefinition(building.DefinitionId).TrainableUnitIds.Length > 0) Button(contextRow, "Set rally", economy.SelectRally);
                if (match.Research.Available)
                    foreach (var technology in match.World.Definition.Technologies)
                        if (technology.ResearchBuildingId == building.DefinitionId && match.Factions.Allows(technology.RequiredFactionId)) { Button(contextRow, "Research tree", match.Research.Open); break; }
                AddFactionBuildingActions(building);
                AddSiegeBuildingActions(building);
            }
            else if (workers)
            {
                if (cargo) Button(contextRow, "Deliver cargo", () => match.Economy.ReturnCargo());
                int eligibleIndex = 0;
                foreach (var definition in match.World.Definition.Buildings)
                {
                    if (!match.Factions.Allows(definition.RequiredFactionId)) continue;
                    if (eligibleIndex++ / BuildPageSize != constructionPage) continue;
                    var allowed = match.World.ValidateRequirements(MatchController.LocalPlayer, definition.RequiredEraId, definition.RequiredTechnologyIds);
                    string requirement = allowed.Accepted ? EconomyControls.CostText(definition.Cost) : "Requires " + match.Research.EraName(definition.RequiredEraId);
                    Button(contextRow, definition.DisplayName + "\n" + requirement, () => match.Economy.BeginBuild(definition.Id));
                }
                if (eligibleIndex > BuildPageSize) Button(contextRow,constructionPage == 0 ? "More buildings\nFortifications & beasts" : "More buildings\nEconomy & army",() => { constructionPage = (constructionPage+1) % ((eligibleIndex+BuildPageSize-1)/BuildPageSize); Invalidate(); });
                AddNavalUnitActions();
            }
            else
            {
                AddFactionUnitActions();
                AddSiegeUnitActions();
                AddNavalUnitActions();
                if (ships > 0 && ships < match.Selection.Count) Button(contextRow, "Ships x" + ships + "\nSail and shell the shore", () => SelectRole(CombatTags.Naval));
                if (spears > 0) Button(contextRow, "Infantry x" + spears + "\nClose combat", () => SelectRole(CombatTags.Infantry));
                if (heroes > 0) Button(contextRow, "Héroe pirata\nCombate con sable", SelectCorsair);
                if (archers > 0) Button(contextRow, "Ranged x" + archers + "\nKeep your distance", () => SelectRole(CombatTags.Ranged));
                if (cavalry > 0) Button(contextRow, "Cavalry x" + cavalry + "\nStrong against archers", () => SelectRole(CombatTags.Cavalry));
                if (creatures > 0) Button(contextRow,"Creatures x" + creatures + "\nSupport with an escort",() => SelectRole(CombatTags.Creature));
                if (siege > 0) Button(contextRow,"Siege x" + siege + "\nBreak or scale defenses",() => SelectRole(CombatTags.Siege));
            }
        }

        private void SelectCorsair()
        {
            var ids = new List<int>();
            foreach (int id in match.Selection)
                if (match.World.TryGetUnit(id, out var unit) && unit.OwnerId == MatchController.LocalPlayer && unit.DefinitionId == CorsairHeroScenario.HeroId)
                    ids.Add(id);
            match.Select(ids);
            match.SetFeedback("Corsario Carmesí seleccionado.");
        }

        private void SelectRole(CombatTags role)
        {
            var ids = new List<int>();
            foreach (int id in match.Selection)
                if (match.World.TryGetUnit(id, out var unit) && unit.OwnerId == MatchController.LocalPlayer &&
                    unit.DefinitionId != CorsairHeroScenario.HeroId && ArmyRole(unit) == role)
                    ids.Add(id);
            match.Select(ids);
            match.SetFeedback("Selection filtered to " + ids.Count + " " + (role == CombatTags.Naval ? "ships" : role == CombatTags.Creature ? "creatures" : role == CombatTags.Siege ? "siege engines" : role == CombatTags.Cavalry ? "cavalry" : role == CombatTags.Ranged ? "ranged units" : "infantry") + ".");
        }

        private static CombatTags ArmyRole(UnitState unit) => unit.IsWorker ? CombatTags.None
            : (unit.Tags & CombatTags.Naval) != 0 ? CombatTags.Naval
            : (unit.Tags & CombatTags.Siege) != 0 ? CombatTags.Siege
            : unit.AttackDamage <= 0 ? CombatTags.None
            : (unit.Tags & CombatTags.Creature) != 0 ? CombatTags.Creature
            // A mounted archer (the camel archer) fights as an archer: keep your distance, not charge the archers.
            : (unit.Tags & CombatTags.Ranged) != 0 ? CombatTags.Ranged
            : (unit.Tags & CombatTags.Cavalry) != 0 ? CombatTags.Cavalry
            : (unit.Tags & CombatTags.Infantry) != 0 ? CombatTags.Infantry : CombatTags.None;

        private static string RoleName(CombatTags tags) => (tags & CombatTags.Naval) != 0 ? "Ship" : (tags & CombatTags.Worker) != 0 ? "Worker" : (tags & CombatTags.Siege) != 0 ? "Siege" : (tags & CombatTags.Creature) != 0 ? "Creature" : (tags & CombatTags.Cavalry) != 0 ? "Cavalry" : (tags & CombatTags.Ranged) != 0 ? "Ranged" : "Infantry";
        private static string PirateRole(string id) => id == CorsairHeroScenario.HeroId ? "Héroe pirata" :
            id == ImportedCharacterVisuals.BoardingRaiderId ? "Infantería de abordaje" :
            id == ImportedCharacterVisuals.GunpowderCorsairId ? "Pistolero" :
            id == ImportedCharacterVisuals.TreasureSeekerId ? "Exploradora y recolectora" : null;
        private static string CounterHint(UnitDefinition definition)
        {
            if (definition.Id == CorsairHeroScenario.HeroId || definition.Id == ImportedCharacterVisuals.BoardingRaiderId) return "Combate con sable";
            if (definition.Id == ImportedCharacterVisuals.GunpowderCorsairId) return "Pistola · recarga entre disparos";
            foreach (var bonus in definition.Attack.Bonuses ?? System.Array.Empty<DamageBonus>())
                if (bonus.MultiplierPermille > 1000) return "Bonus against " + bonus.TargetTags;
            return (definition.Tags & CombatTags.Siege) != 0 ? "Support with an escort" : "No attack bonus";
        }

        private static string WorkerTaskText(WorkerTask task)
        {
            switch (task)
            {
                case WorkerTask.MovingToResource: return "To resource";
                case WorkerTask.Gathering: return "Gathering";
                case WorkerTask.ReturningResources: return "Delivering";
                case WorkerTask.MovingToConstruction: return "To construction";
                case WorkerTask.Constructing: return "Constructing";
                default: return "Idle";
            }
        }

        public void SetSelectionBox(Rect? screenRect)
        {
            box.gameObject.SetActive(screenRect.HasValue);
            if (!screenRect.HasValue) return;
            var eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (!MobileHudLayout.PlaceSelection((RectTransform)canvas.transform, box, screenRect.Value, eventCamera))
                box.gameObject.SetActive(false);
        }

        public void PrepareOffscreenCapture(Camera camera)
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            RefreshLayout(true);
            Canvas.ForceUpdateCanvases();
        }

        private RectTransform Panel(string name, Transform parent)
        {
            var rect = Rect(name, parent); AlphaTheme.StylePanel(rect); return rect;
        }

        private Text Label(string name, Transform parent, int size)
        {
            var rect = Rect(name, parent); var label = rect.gameObject.AddComponent<Text>(); label.font = font; label.fontSize = size;
            label.color = ink; label.alignment = TextAnchor.MiddleLeft; label.raycastTarget = false; return label;
        }

        private Text Button(Transform parent, string text, UnityEngine.Events.UnityAction action)
        {
            var rect = Rect(text, parent); var image = rect.gameObject.AddComponent<Image>(); image.color = AlphaTheme.SurfaceRaised;
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(action);
            bool primary = text == "Confirm" || text == "Play" || text == "Workers" || text == "Army";
            button.onClick.AddListener(() => UiSound.Play(primary ? UiCue.Confirm : UiCue.Click));
            AlphaTheme.StyleButton(button, primary);
            var element = rect.gameObject.AddComponent<LayoutElement>(); element.minWidth = 90; element.minHeight = MobileHudLayout.MinimumTarget; element.preferredHeight = 56;
            var label = Label("Label", rect, text.Contains("\n") ? 16 : 17); label.font = AlphaTheme.Strong; label.text = text; label.alignment = TextAnchor.MiddleCenter;
            AlphaTheme.StyleButton(button, primary);
            Stretch(label.rectTransform, 8, 3);
            AddCommandIcon(rect, label, text);
            label.resizeTextForBestFit = true; label.resizeTextMinSize = 13; label.resizeTextMaxSize = text.Contains("\n") ? 16 : 17;
            return label;
        }

        private static RectTransform Rect(string name, Transform parent) { var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false); return rect; }
        private static void Stretch(RectTransform rect, float horizontal = 0, float vertical = 0) => Anchor(rect, Vector2.zero, Vector2.one, new Vector2(horizontal, vertical), new Vector2(-horizontal, -vertical));
        private static void Anchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax) { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = offsetMin; rect.offsetMax = offsetMax; }
        public void Dispose() { Minimap?.Dispose(); if (canvasObject != null) UnityEngine.Object.Destroy(canvasObject); }
    }
}
