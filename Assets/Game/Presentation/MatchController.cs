using System;
using System.Collections.Generic;
using System.Diagnostics;
using Emberfield.Diagnostics;
using Emberfield.Simulation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace Emberfield.Presentation
{
    public sealed partial class MatchController : MonoBehaviour
    {
        public const int LocalPlayer = 1;
        public string MapResourcePath = "Maps/amber_reach";
        public string InitialFactionId;
        public bool PerformanceStress;
        public int PerformanceUnitCount = 100;
        public MovementScenarioKind PerformanceScenario = MovementScenarioKind.OpenField;
        public MovementStressSession Stress { get; private set; }
        public MovementFormation Formation { get; private set; } = MovementFormation.Loose;
        private static int? nextPerformanceCount;
        private static MovementScenarioKind nextPerformanceScenario;
        private static string nextFactionId;
        public bool IsCombatSandbox => World != null && World.Map.Id == "combat_sandbox";
        public bool IsFactionDrill => World != null && World.Map.Id == "faction_proving_ground";
        public World World { get; private set; }
        public WorldView View { get; private set; }
        public RtsCamera Rig { get; private set; }
        public MatchHud Hud { get; private set; }
        public EconomyControls Economy { get; private set; }
        public ResearchControls Research { get; private set; }
        public FactionControls Factions { get; private set; }
        public OfflineControls OfflineControls { get; private set; }
        public ProductShell Shell { get; private set; }
        public AlphaMatchMetrics Metrics { get; private set; }
        public bool TouchSelectionMode { get; set; }
        public List<int> Selection { get; } = new List<int>();
        public string Feedback { get; private set; } = "Select a Tender and tap a resource source to gather.";
        public double LastTickMilliseconds { get; private set; }
        // What the last Update spent, for pacing reports: how many ticks it caught up, their cost with the AI's, and the view's.
        public int LastFrameTicks { get; private set; }
        public double LastFrameSimulationMilliseconds { get; private set; }
        public double LastPresentationMilliseconds { get; private set; }
        public double LastViewMilliseconds { get; private set; }
        private RtsInput input;
        private double accumulator;
        private int loadFrame;
        private double loadStarted, reportedSinceLoad;
        private bool loading = true;
        /// <summary>True while this match's frames still carry its load (see StillLoading); none of them is sampled.</summary>
        public bool IsLoading => loading;
        private readonly Stopwatch tickWatch = new Stopwatch();
        private readonly Stopwatch frameWatch = new Stopwatch();
        private Predicate<int> selectionGone;

        private void Awake()
        {
            loadFrame = Time.frameCount; loadStarted = Time.realtimeSinceStartupAsDouble;
            FramePacing.Begin(PerformanceStress);
            if (TryInitializeOnlineWorld()) { }
            else if (PerformanceStress)
            {
                if (nextPerformanceCount.HasValue)
                {
                    PerformanceUnitCount = nextPerformanceCount.Value; PerformanceScenario = nextPerformanceScenario;
                    nextPerformanceCount = null;
                }
                Stress = new MovementStressSession(PerformanceScenario, PerformanceUnitCount);
                World = Stress.Scenario.World;
                Feedback = Stress.Status;
            }
            else if (SiegeShowcaseScenario.TryCreateWorld(out var siegeWorld)) World = siegeWorld;
            else if (MeshyUnitReview.TryCreateWorld(out var reviewWorld)) World = reviewWorld;
            else if (ArmyFilm.TryCreateWorld(out var filmWorld)) World = filmWorld;
            else if (LegendLandsFilm.TryCreateWorld(out var legendFilmWorld)) World = legendFilmWorld;
            else if (SiegeFilm.TryCreateWorld(out var siegeFilmWorld)) World = siegeFilmWorld;
            else if (NavalBattleFilm.TryCreateWorld(out var navalFilmWorld)) World = navalFilmWorld;
            else if (SceneryStills.TryCreateWorld(out var stillsWorld)) World = stillsWorld;
            else if (PirateCrewScenario.TryCreateWorld(out var crewWorld)) World = crewWorld;
            else if (CorsairHeroScenario.TryCreateWorld(out var corsairWorld)) World = corsairWorld;
            else if (ChallengeRun.TryCreateWorld(out var challengeWorld)) World = challengeWorld;
            else
            {
                string chosen = nextFactionId ?? InitialFactionId;
                nextFactionId = null;
                if (UnityEngine.Debug.isDebugBuild || Application.isEditor)
                {
                    var args = Environment.GetCommandLineArgs();
                    int choice = Array.IndexOf(args, "-emberfieldFaction");
                    if (string.IsNullOrEmpty(chosen) && choice >= 0 && choice + 1 < args.Length) chosen = args[choice + 1];
                }
                string offlineChoice = nextOfflineFaction ?? InitialOfflineFactionId;
                var offlineMode = nextOfflineFaction != null ? nextOfflineMode : InitialOfflineMode;
                if (string.IsNullOrEmpty(offlineChoice) && (UnityEngine.Debug.isDebugBuild || Application.isEditor))
                {
                    var args = Environment.GetCommandLineArgs();
                    int option = Array.IndexOf(args, "-emberfieldOffline");
                    if (option >= 0 && option + 1 < args.Length && Enum.TryParse(args[option + 1], true, out VictoryMode requested))
                    { offlineChoice = string.IsNullOrEmpty(chosen) ? "aven" : chosen; offlineMode = requested; }
                }
                if (!string.IsNullOrEmpty(offlineChoice))
                {
                    World = DefinitionLoader.CreateOfflineWorld(offlineChoice, offlineMode, nextOfflineMap ?? ContentRealms.DefaultMapForRealm(ContentRealms.RealmForFaction(offlineChoice)));
                    nextOfflineFaction = null;
                    nextOfflineMap = null;
                }
                else World = string.IsNullOrEmpty(chosen) ? DefinitionLoader.CreateWorld(MapResourcePath) : DefinitionLoader.CreateFactionWorld(chosen);
                if ((UnityEngine.Debug.isDebugBuild || Application.isEditor) && Array.IndexOf(Environment.GetCommandLineArgs(), "-emberfieldArtReview") >= 0)
                    World = DefinitionLoader.CreateWorld("Maps/art_review");
            }
            CosmeticLoadout.IsOnlineSession = World.IsNetworkReplica;
            var camObject = new GameObject("RTS Camera"); camObject.transform.SetParent(transform);
            var cam = camObject.AddComponent<Camera>(); cam.tag = "MainCamera";
            cam.backgroundColor = new Color(.12f, .16f, .17f); cam.clearFlags = CameraClearFlags.SolidColor;
            Rig = new RtsCamera(cam, new Vector2(World.Map.WidthCells, World.Map.HeightCells) * World.Map.CellSizeMillimetres * .001f,
                World.Map.RealmId == ContentRealms.Naval);
            if (Stress != null)
            {
                cam.transform.rotation = Quaternion.Euler(65, 0, 0);
                Rig.MaximumZoom = 90;
                Stress.FrameArmy(Rig, true);
            }
            if (IsCombatSandbox)
            {
                Rig.SetHome(new Vector3(30.5f, 0, 27), cam.aspect > 1.7f ? 12 : 11);
                Feedback = "Select your blue army and tap a red unit to attack. Spears counter cavalry; cavalry counters archers; archers counter spears.";
            }
            var lightObject = new GameObject("Sun"); lightObject.transform.SetParent(transform);
            var light = lightObject.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.2f;
            light.transform.rotation = Quaternion.Euler(50, -25, 0); light.shadows = LightShadows.None;
            RenderSettings.ambientLight = new Color(.7f, .74f, .78f);
            AlphaLighting.Configure(cam, light, transform, World.Map, World.Lands);
            MobileQuality.Apply(light, gameObject, PerformanceStress);
            var events = new GameObject("Input events"); events.transform.SetParent(transform);
            events.AddComponent<EventSystem>(); events.AddComponent<InputSystemUIInputModule>();
            View = new WorldView(World, transform, cam);
            Economy = new EconomyControls(this, transform);
            Research = new ResearchControls(this);
            Factions = new FactionControls(this, transform);
            ConsumeOfflineDifficulty();
            OfflineControls = new OfflineControls(this);
            InitializeOfflineMatch();
            ConsumePendingRestore();
            Online = new OnlineControls(this);
            InitializeAlpha();
            InitializeVoice();
            // The sandbox is a place people spend time in, so it gets a soundscape like any match does.
            if (!PerformanceStress && World.Map.Id != "art_review") gameObject.AddComponent<FrontierAmbience>().Initialize(this);
            Metrics = new AlphaMatchMetrics(World, LocalPlayer);
            BeginChallenge();
            if (IsFactionDrill) Feedback = Factions.LocalDefinition.DisplayName + " drill. Train your unique unit at the Muster Hall; select infrastructure for faction actions.";
            Hud = new MatchHud(this, transform);
            input = new RtsInput(this);
            if (Stress == null) Shell = new ProductShell(this, transform);
            // A match about to be played reads and draws what it can field now, so its first uses do not stall a frame.
            if (Stress == null && (World.Match != null || World.IsNetworkReplica)) View.Prewarm();
        }

        private void Start() { PirateCrewScenario.PreparePresentation(this); CorsairHeroScenario.PreparePresentation(this); PirateCrewGameSmoke.TryStart(this); CorsairGameSmoke.TryStart(this); PlayerSmoke.TryStart(this); OnlinePlayerSmoke.TryStart(this); ProductShellSmoke.TryStart(this); ExpansionPlayerSmoke.TryStart(this); FullMatchRecording.TryStart(this); SiegeShowcase.TryStart(this); ImportedBuildingVisuals.TryStartCapture(this); MeshyUnitReview.TryStartCapture(this); ArmyFilm.TryStart(this); LegendLandsFilm.TryStart(this); SiegeFilm.TryStart(this); NavalBattleFilm.TryStart(this); SceneryStills.TryStart(this); VoiceSmoke.TryStart(this); }

        // The frame this match was made in loads the scene and draws it for the first time. That time is the loading
        // screen's, not a frame anyone played through, and it reaches unscaledDeltaTime in one long interval: at the next
        // frame without vertical sync, two to four frames on with it, where frames are timed by when they are shown.
        // So a frame counts as loading until the intervals reported since the load have covered the wall-clock time it
        // has taken, within a few refreshes of ordinary latency; the first frame's own interval is the menu's.
        private const double LoadSlackSeconds = .1;
        private const int MostLoadingFrames = 30;
        private bool StillLoading()
        {
            if (!loading) return false;
            if (Time.frameCount <= loadFrame) return true;
            double owed = Time.realtimeSinceStartupAsDouble - loadStarted - reportedSinceLoad;
            reportedSinceLoad += Time.unscaledDeltaTime;
            if (owed > LoadSlackSeconds && Time.frameCount <= loadFrame + MostLoadingFrames) return true;
            return loading = false;
        }

        private void Update()
        {
            LastFrameTicks = 0; LastFrameSimulationMilliseconds = 0;
            bool stillLoading = StillLoading();
            Shell?.Update();
            Metrics?.ObserveFrame(Time.unscaledDeltaTime, !stillLoading && Shell?.BlocksLocalSimulation != true && Alpha?.IsOpen != true &&
                !OfflineControls.IsOpen && !OfflineControls.IsPaused && !Research.IsOpen && !Factions.IsOpen && Online?.IsOpen != true &&
                (World.Match == null || !World.Match.IsFinished));
            input.Update();
            Rig.Ease(Time.unscaledDeltaTime);
            Voice?.Update();
            Online?.Update();
            if (World.IsNetworkReplica) { accumulator = 0; SyncFrame(Online.Interpolation); return; }
            // A restore owns the simulation until it lands: the opponent must not think while the recorded
            // orders are still being replayed into the world.
            if (IsRestoring) { accumulator = 0; AdvanceRestore(); return; }
            if (Shell?.BlocksLocalSimulation == true || Alpha?.IsOpen == true || OfflineControls.IsPaused || MeshyUnitReview.HoldsSimulation || Challenge?.IsFinished == true || World.Match != null && World.Match.IsFinished)
            {
                accumulator = 0;
                SyncFrame(1);
                return;
            }
            Stress?.OnFrame(Time.unscaledDeltaTime);
            // The loading frame takes the match's first tick, the one that compiles the rules and lets the AI make its
            // opening plan, behind the loading screen. The load itself is not time the match owes: catching it up would
            // crowd five ticks into the first frame after it.
            if (Time.frameCount == loadFrame && Stress == null) accumulator = Simulation.World.TickSeconds;
            else if (!stillLoading) accumulator += Math.Min(Time.unscaledDeltaTime, .25f);
            int steps = 0;
            frameWatch.Restart();
            while (accumulator >= Simulation.World.TickSeconds && steps < 5)
            {
                AdvanceSimulationTick();
                accumulator -= Simulation.World.TickSeconds;
                steps++;
            }
            LastFrameTicks = steps; LastFrameSimulationMilliseconds = frameWatch.Elapsed.TotalMilliseconds;
            SyncFrame((float)(accumulator / Simulation.World.TickSeconds));
            Stress?.ObservePresentation(LastPresentationMilliseconds, LastViewMilliseconds);
        }

        /// <summary>
        /// The match's own frame, drawn only after Unity's animator update has run: the animated units synced here are
        /// evaluated there, all together, instead of one by one inside the sync (CorsairAnimationDriver.BeginEngineFrame).
        /// </summary>
        private void SyncFrame(float alpha)
        {
            CorsairAnimationDriver.BeginEngineFrame(Rig.Camera);
            try { SyncPresentation(alpha); }
            finally { CorsairAnimationDriver.EndEngineFrame(); }
        }

        public void AdvanceSimulationTick()
        {
            OfflineControls.Ai?.Tick();
            Stress?.BeforeTick();
            Metrics?.BeginTick(World);
            tickWatch.Restart(); World.Tick(); tickWatch.Stop();
            LastTickMilliseconds = tickWatch.Elapsed.TotalMilliseconds;
            Metrics?.ObserveTick(World, LastTickMilliseconds);
            ObserveAlpha();
            Stress?.AfterTick(LastTickMilliseconds);
            Challenge?.Observe();
        }

        public void SyncPresentation(float alpha)
        {
            frameWatch.Restart();
            // One predicate for the match: a lambda here would allocate a delegate every frame.
            selectionGone ??= id => !World.TryGetUnit(id, out _) && !World.TryGetBuilding(id, out _);
            int removed = Selection.RemoveAll(selectionGone);
            if (removed > 0)
            {
                if ((ChoosingShip && BoardingTroops().Length == 0) || (ChoosingLanding && LoadedShips().Length == 0)) CancelNavalTarget();
                if (Economy.PendingBuildingId != null && Economy.SelectedWorkers().Length == 0) Economy.CancelBuild();
                if (Economy.IsChoosingRally && Economy.SelectedBuilding() == null) Economy.CancelBuild();
                Hud.Invalidate();
            }
            if (Factions.PendingDeployUnitId != 0 && (!World.TryGetUnit(Factions.PendingDeployUnitId, out var deploying) || !deploying.IsPackedOutpost)) Factions.CancelDeploy();
            double beforeView = frameWatch.Elapsed.TotalMilliseconds;
            View.Sync(alpha, Selection);
            LastViewMilliseconds = frameWatch.Elapsed.TotalMilliseconds - beforeView;
            Research.ObserveCompletions();
            Stress?.FrameArmy(Rig);
            Rig.Constrain(); OfflineControls.Observe(); ObserveAlpha(); Hud.Refresh();
            LastPresentationMilliseconds = frameWatch.Elapsed.TotalMilliseconds;
        }

        public void Select(IEnumerable<int> ids, bool add = false)
        {
            CancelSiegeTarget(); CancelNavalTarget();
            if (!add) Selection.Clear();
            foreach (int id in ids)
            {
                bool owned = World.TryGetUnit(id, out var u) && u.OwnerId == LocalPlayer;
                owned |= World.TryGetBuilding(id, out var b) && b.OwnerId == LocalPlayer;
                if (owned && !Selection.Contains(id)) Selection.Add(id);
            }
        }

        // Shift-clicking one entity adds it to the group, or drops it if it is already in.
        public void ToggleSelected(int id)
        {
            CancelNavalTarget();
            if (Selection.Remove(id)) return;
            Select(new[] { id }, true);
        }

        public int[] SelectedUnits()
        {
            // The HUD asks every refresh; with nothing selected there is nothing to build a list for.
            if (Selection.Count == 0) return Array.Empty<int>();
            var ids = new List<int>();
            foreach (int id in Selection) if (World.TryGetUnit(id, out var u) && u.OwnerId == LocalPlayer) ids.Add(id);
            return ids.ToArray();
        }

        public void Tap(Vector2 point, bool doubleTap)
        {
            if (Shell?.IsOpen == true || Research.IsOpen || Factions.IsOpen || OfflineControls.BlocksWorldInput || Online?.BlocksWorldInput == true || Alpha?.IsOpen == true) return;
            if (HandleSiegeTap(point)) return;
            if (HandleNavalTap(point)) return;
            if (Factions.PendingDeployUnitId != 0 && Rig.GroundPoint(point, out var deployPoint))
            {
                Factions.PreviewAt(DefinitionLoader.ToSimulation(deployPoint));
                return;
            }
            if ((Economy.PendingBuildingId != null || Economy.IsChoosingRally) && Rig.GroundPoint(point, out var placementPoint))
            {
                Economy.HandleTerrain(placementPoint);
                return;
            }
            int id = View.Pick(Rig.Camera, point, Mathf.Max(18, Screen.height / 38f));
            if (World.TryGetUnit(id, out var unit))
            {
                if (unit.OwnerId == LocalPlayer)
                {
                    if (!doubleTap && TryBoard(unit)) return;
                    Select(doubleTap ? View.VisibleUnits(Rig.Camera, LocalPlayer, unit.DefinitionId) : new List<int> { id });
                    SetFeedback(doubleTap ? "Similar visible units selected." : unit.IsWorker ? "Tap a resource to gather, or open ground to move." : "Tap an enemy to attack, or open ground to move.");
                }
                else AttackTarget(id);
                return;
            }
            if (World.TryGetBuilding(id, out var building))
            {
                if (building.OwnerId == LocalPlayer)
                {
                    var workers = Economy.SelectedWorkers();
                    if (!building.IsComplete && workers.Length > 0) Issue(new ConstructCommand(LocalPlayer, workers, id), "Workers assigned to construction.");
                    else if (building.IsComplete && Economy.BuildingDefinition(building.DefinitionId).CanDropOff && Economy.HasSelectedCargo()) Economy.ReturnCargo(id);
                    else { Select(new[] { id }); SetFeedback(Economy.BuildingDefinition(building.DefinitionId).DisplayName + " selected."); }
                }
                else AttackTarget(id);
                return;
            }
            if (World.TryGetResource(id, out var resource))
            {
                var workers = Economy.SelectedWorkers();
                if (workers.Length > 0) Issue(new GatherCommand(LocalPlayer, workers, id), "Gathering " + resource.Kind.ToString().ToLowerInvariant() + ". Resources count when delivered.");
                else SetFeedback(resource.Kind + " source: " + resource.RemainingAmount + ". Select a Tender to gather.");
                return;
            }
            if (SelectedUnits().Length > 0 && Rig.GroundPoint(point, out var destination))
            {
                OrderGround(DefinitionLoader.ToSimulation(destination));
            }
        }

        public void Issue(IGameCommand command, string success)
        {
            var result = SubmitPlayerCommand(command);
            SetFeedback(result.Accepted ? World.IsNetworkReplica ? "Order sent. Waiting for the server." : success : result.Message);
            if (result.Accepted && Selection.Count > 0)
            {
                var selectedRoot = View.RootFor(Selection[0]);
                if (selectedRoot != null) View.Feedback?.Emit(FeedbackCue.Order, selectedRoot.position);
            }
        }

        public CommandResult SubmitPlayerCommand(IGameCommand command)
        {
            var result = World.Submit(command);
            if (!World.IsNetworkReplica) Metrics?.ObserveCommand(command, result);
            ObserveAlphaCommand(command, result);
            return result;
        }

        public int[] SelectedCombatUnits()
        {
            if (Selection.Count == 0) return Array.Empty<int>();
            var ids = new List<int>();
            foreach (int id in Selection)
                if (World.TryGetUnit(id, out var unit) && unit.OwnerId == LocalPlayer && unit.AttackDamage > 0) ids.Add(id);
            return ids.ToArray();
        }

        private void AttackTarget(int targetId)
        {
            var ids = SelectedCombatUnits();
            if (ids.Length == 0) { SetFeedback("Select an armed unit to attack."); return; }
            Issue(new AttackCommand(LocalPlayer, ids, targetId), "Attacking. Tap open ground to disengage, or Stop to hold fire.");
        }

        public void SetFeedback(string text) { Feedback = text; Hud?.Invalidate(); }

        public void StopSelected() { var ids = SelectedUnits(); if (ids.Length > 0) Issue(new StopCommand(LocalPlayer, ids), "Stopped. Military units hold fire until a new order."); }
        public void CycleFormation()
        {
            Formation = (MovementFormation)(((int)Formation + 1) % 3);
            SetFeedback(Formation + " formation selected. Tap open ground to place it.");
        }
        public void SelectWorkers()
        {
            var ids = new List<int>();
            foreach (int id in View.VisibleUnits(Rig.Camera, LocalPlayer))
                if (World.TryGetUnit(id, out var unit) && unit.IsWorker) ids.Add(id);
            Select(ids);
        }
        public void SelectArmy()
        {
            var ids = new List<int>();
            foreach (int id in View.VisibleUnits(Rig.Camera, LocalPlayer))
                // Workers defend themselves but are never part of the army: Army must not pull them off their work.
                if (World.TryGetUnit(id, out var unit) && !unit.IsPackedOutpost && !unit.IsWorker &&
                    (unit.AttackDamage > 0 || (unit.Tags & CombatTags.Siege) != 0)) ids.Add(id);
            Select(ids); SetFeedback("Visible army selected. Tap an enemy to attack.");
        }
        public void SwitchSandbox() => SceneManager.LoadScene(IsCombatSandbox ? "Greybox" : "CombatSandbox");
        public void OpenStress() => SceneManager.LoadScene("RTSPerformanceStress");
        public void StartFactionDrill(string factionId)
        {
            if (!ContentRealms.IsPlayableFactionInRealm(factionId, ContentRealms.RealmForFaction(factionId)))
                throw new ArgumentException("Choose an available faction.", nameof(factionId));
            foreach (var faction in World.Definition.Factions ?? Array.Empty<FactionDefinition>())
                if (faction.Id == factionId) { nextFactionId = factionId; SceneManager.LoadScene("Greybox"); return; }
            throw new ArgumentException("Choose an available faction.", nameof(factionId));
        }
        public void ReloadStress(int count, MovementScenarioKind kind)
        {
            nextPerformanceCount = count; nextPerformanceScenario = kind;
            SceneManager.LoadScene("RTSPerformanceStress");
        }
        public void StartStress() { Stress?.Start(); if (Stress != null) SetFeedback(Stress.Status); }
        public void ClearSelection() { CancelSiegeTarget(); CancelNavalTarget(); Economy.CancelBuild(); Factions.CancelDeploy(); Selection.Clear(); SetFeedback("Selection cleared."); }
        public void RotateView()
        {
            Rig.Rotate(90);
            SetFeedback("Camera rotated. Drag the terrain to pan; pinch or scroll to zoom.");
        }

        public void Restart()
        {
            if (World.IsNetworkReplica) { Online.Open(); return; }
            if (QueueChallengeRestart()) { SceneManager.LoadScene("Greybox"); return; }
            if (World.Match != null) StartOfflineMatch(Factions.LocalDefinition.Id, World.Match.Mode, World.Map.Id, OfflineDifficulty);
            else if (Stress != null) ReloadStress(PerformanceUnitCount, PerformanceScenario);
            else if (IsFactionDrill) StartFactionDrill(Factions.LocalDefinition.Id);
            else SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
        private void OnApplicationFocus(bool focused) { Voice?.SetFocused(focused); if (!focused && !OfflineRenderProbe.IsRunning && !SiegeShowcase.IsRunning && !ArmyFilm.IsRunning && !LegendLandsFilm.IsRunning && !SiegeFilm.IsRunning && !NavalBattleFilm.IsRunning && !SceneryStills.IsRunning) { input?.Cancel(); accumulator = 0; OfflineControls?.PauseForFocus(); } }
        private void OnApplicationPause(bool paused) { if (paused && !OfflineRenderProbe.IsRunning) { input?.Cancel(); accumulator = 0; OfflineControls?.PauseForFocus(); } }
        private void OnDestroy() { Shell?.Dispose(); Online?.Dispose(); input?.Dispose(); OfflineControls?.Dispose(); Economy?.Dispose(); Factions?.Dispose(); View?.Dispose(); Voice?.Dispose(); Hud?.Dispose(); }
    }
}
