using System;
using System.Collections.Generic;
using System.Linq;
using Emberfield.Simulation;
using Emberfield.Voice;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Emberfield.Presentation
{
    public enum VoiceListenMode { Off, PushToTalk, AlwaysOn }
    public enum VoiceLanguageChoice { Automatic, Spanish, English }

    // Voice is one more input layer. A phrase becomes a VoiceIntent and runs through the same selection,
    // command and camera handlers as the HUD, so World validates a spoken order exactly like a tapped one.
    public sealed class VoiceControls : IDisposable
    {
        public const float PushToTalkTailSeconds = .8f, ListenOnceSeconds = 6, HeardDisplaySeconds = 8;
        // How far from the pointed ground "attack here" looks for a visible enemy.
        private const double AttackReachMillimetres = 6000;
        private static bool? platformSupported;
        private readonly MatchController match;
        private readonly Queue<string> pending = new Queue<string>();
        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        private IVoiceRecognizer recognizer;
        private IReadOnlyList<VoicePhrase> phrases = Array.Empty<VoicePhrase>();
        private string[] phraseTexts = Array.Empty<string>();
        private float listenUntil = -1, retryAt, heardAt = -100;
        private bool listenOnce, injected, configured, focused = true;

        public VoiceListenMode Mode { get; private set; }
        public VoiceLanguage Language { get; private set; }
        public VoiceCommandParser Parser { get; private set; }
        public IReadOnlyList<VoicePhrase> Phrases => phrases;
        public bool IsListening => recognizer != null && recognizer.IsListening;
        public bool IsAvailable => recognizer?.IsAvailable ?? PlatformSupported;
        public string Problem => recognizer?.Problem;
        public bool HelpVisible { get; private set; }
        public string LastHeard { get; private set; } = "";
        public VoiceIntent LastIntent { get; private set; } = VoiceIntent.None;
        public bool ShowsRecentPhrase => LastHeard.Length > 0 && Time.unscaledTime - heardAt < HeardDisplaySeconds;
        /// <summary>A fixed screen point for "here". Otherwise the mouse, or the screen centre while the mouse is over the HUD.</summary>
        public Vector2? PointerOverride { get; set; }
        public static bool PlatformSupported => platformSupported ?? (platformSupported = WindowsVoiceRecognizer.IsSupported).Value;

        public VoiceControls(MatchController match) { this.match = match; ApplySettings(); }

        public void ApplySettings()
        {
            var settings = match.Alpha?.Settings.Value;
            Configure(settings == null ? VoiceListenMode.Off : (VoiceListenMode)settings.VoiceMode,
                ResolveLanguage(settings == null ? VoiceLanguageChoice.Automatic : (VoiceLanguageChoice)settings.VoiceLanguage));
        }

        /// <summary>Sets mode and language for this session. The Settings screens persist them through AlphaControls.</summary>
        public void Configure(VoiceListenMode mode, VoiceLanguage language)
        {
            if (configured && mode == Mode && language == Language) return;
            if (!configured || language != Language)
            {
                match.World.TryGetPlayer(MatchController.LocalPlayer, out var player);
                Parser = new VoiceCommandParser(VoiceVocabulary.Build(match.World.Definition, player?.FactionId, match.World.Map.RealmId, language));
                phrases = Parser.Vocabulary.Phrases();
                phraseTexts = phrases.Select(phrase => phrase.Text).ToArray();
            }
            configured = true; Mode = mode; Language = language;
            StopListening();
            // The microphone stays untouched until the player turns voice on.
            if (mode == VoiceListenMode.Off) ReleaseRecognizer();
            else
            {
                if (recognizer == null) { recognizer = new WindowsVoiceRecognizer(); recognizer.Recognized += OnRecognized; }
                recognizer.Load(phraseTexts);
            }
            retryAt = 0; ResumeAlwaysOn();
            match.Hud?.Invalidate();
        }

        /// <summary>Replaces the Windows recognizer, for tests and the packaged smoke.</summary>
        public void UseRecognizer(IVoiceRecognizer replacement)
        {
            StopListening();
            if (recognizer != null) { recognizer.Recognized -= OnRecognized; recognizer.Dispose(); }
            recognizer = replacement; injected = replacement != null;
            if (recognizer == null) return;
            recognizer.Recognized += OnRecognized; recognizer.Load(phraseTexts);
            ResumeAlwaysOn();
        }

        public void Update()
        {
            float now = Time.unscaledTime;
            if (match.Shell?.IsOpen == true || !focused) { if (IsListening) StopListening(); }
            else if (Mode == VoiceListenMode.PushToTalk) UpdatePushToTalk(now);
            else if (Mode == VoiceListenMode.AlwaysOn && !IsListening && recognizer != null && recognizer.IsAvailable && now >= retryAt)
            {
                // Windows can stop after an audio error; listen again without spinning.
                retryAt = now + (recognizer.Problem != null ? 10 : 2);
                StartListening(float.PositiveInfinity, false);
            }
            if (IsListening && now > listenUntil) StopListening();
            while (pending.Count > 0) Hear(pending.Dequeue());
        }

        private void UpdatePushToTalk(float now)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || TypingInTextField()) return;
            if (keyboard.vKey.wasPressedThisFrame) StartListening(float.PositiveInfinity, false, true);
            else if (keyboard.vKey.wasReleasedThisFrame && !listenOnce && IsListening) listenUntil = now + PushToTalkTailSeconds;
        }

        /// <summary>Listens for one command: the HUD chip, or a touch screen without a keyboard.</summary>
        public void ListenOnce()
        {
            if (Mode == VoiceListenMode.Off || match.Shell?.IsOpen == true) return;
            StartListening(Time.unscaledTime + ListenOnceSeconds, true, true);
        }

        /// <summary>The HUD chip: turns voice on the first time, then listens for one command (or toggles help while always listening).</summary>
        public void PressHudButton()
        {
            if (Mode == VoiceListenMode.Off)
            {
                if (match.Alpha != null) match.Alpha.SetVoiceMode(VoiceListenMode.PushToTalk);
                else Configure(VoiceListenMode.PushToTalk, Language);
                match.SetFeedback(L("Voz activada. Mantén V (o toca VOZ) y di una orden; di «ayuda» para ver ejemplos.",
                    "Voice on. Hold V (or tap VOICE) and say an order; say “help” for examples."));
            }
            if (Mode == VoiceListenMode.AlwaysOn) { HelpVisible = !HelpVisible; match.Hud?.Invalidate(); }
            else if (IsListening) StopListening();
            else ListenOnce();
        }

        public void SetFocused(bool value)
        {
            focused = value;
            if (!value) StopListening(); else { retryAt = 0; ResumeAlwaysOn(); }
        }

        /// <summary>Interprets one phrase, spoken or typed, and carries it out.</summary>
        public VoiceIntent Hear(string text)
        {
            LastHeard = (text ?? "").Trim(); heardAt = Time.unscaledTime;
            LastIntent = Parser.Parse(LastHeard);
            if (LastIntent.IsNone)
                match.SetFeedback(L("No he entendido «", "I did not understand “") + LastHeard + L("». Di «ayuda» para ver ejemplos.", "”. Say “help” for examples."));
            else Execute(LastIntent);
            if (listenOnce) StopListening();
            match.Hud?.Invalidate();
            return LastIntent;
        }

        public void Execute(VoiceIntent intent)
        {
            if (intent == null || intent.IsNone || match.Shell?.IsOpen == true) return;
            if (MenuOpen && !WorksInMenus(intent.Action))
            {
                match.SetFeedback(L("Hay un menú abierto. Di «reanuda» o «cancela» para volver a la partida.", "A menu is open. Say “resume” or “cancel” to return to the game."));
                return;
            }
            switch (intent.Action)
            {
                case VoiceAction.Help:
                    HelpVisible = true;
                    match.SetFeedback(L("Algunos comandos de voz. Di «cierra la ayuda» para ocultarlos.", "Some voice commands. Say “close help” to hide them."));
                    break;
                case VoiceAction.CloseHelp: HelpVisible = false; match.SetFeedback(L("Ayuda oculta.", "Help hidden.")); break;
                case VoiceAction.SelectArmy:
                    SelectMatching(IsArmy, L("Ejército seleccionado", "Army selected"), L("No tienes unidades de combate.", "You have no combat units."));
                    break;
                case VoiceAction.SelectWorkers:
                    SelectMatching(unit => unit.IsWorker, L("Trabajadores seleccionados", "Workers selected"), L("No tienes trabajadores.", "You have no workers."));
                    break;
                case VoiceAction.SelectIdleWorkers:
                    SelectMatching(IsIdleWorker, L("Trabajadores ociosos seleccionados", "Idle workers selected"), L("No hay trabajadores ociosos.", "No workers are idle."));
                    break;
                case VoiceAction.SelectUnits:
                    SelectMatching(unit => unit.DefinitionId == intent.TargetId, Capital(Name(intent.TargetId, 2)) + " " + Selected(intent.TargetId, 2),
                        L("No tienes ", "You have no ") + Name(intent.TargetId, 2) + ".");
                    break;
                case VoiceAction.SelectBuilding: SelectBuilding(intent.TargetId); break;
                case VoiceAction.ClearSelection: match.ClearSelection(); break;
                case VoiceAction.Attack: AttackPointed(); break;
                case VoiceAction.Move: MovePointed(); break;
                case VoiceAction.Stop:
                    if (match.SelectedUnits().Length == 0) match.SetFeedback(SelectFirst); else match.StopSelected();
                    break;
                case VoiceAction.Retreat: Retreat(); break;
                case VoiceAction.Gather: GatherResource(intent.Resource); break;
                case VoiceAction.ReturnCargo: ReturnCargo(); break;
                case VoiceAction.CycleFormation: match.CycleFormation(); break;
                case VoiceAction.Train: TrainUnits(intent.TargetId, intent.Count); break;
                case VoiceAction.Build: PlaceBuilding(intent.TargetId); break;
                case VoiceAction.Confirm: Confirm(); break;
                case VoiceAction.Cancel: Cancel(); break;
                case VoiceAction.Research: ResearchFamily(intent.TargetId); break;
                case VoiceAction.AdvanceEra: AdvanceEra(); break;
                case VoiceAction.CameraHome:
                {
                    var home = HomeBuilding();
                    if (home != null) match.Rig.Focus(DefinitionLoader.ToWorld(home.Position)); else match.Rig.Home();
                    match.SetFeedback(L("Cámara en tu base.", "Camera on your base."));
                    break;
                }
                case VoiceAction.CameraSelection: FocusSelection(); break;
                case VoiceAction.CameraRotate: match.RotateView(); break;
                case VoiceAction.ZoomIn: match.Rig.Zoom(.75f); break;
                case VoiceAction.ZoomOut: match.Rig.Zoom(1.33f); break;
                case VoiceAction.Pause:
                    // Opening the menu clears the selection and its feedback; say what actually happened.
                    if (!match.OfflineControls.IsOpen) match.OfflineControls.Open();
                    match.SetFeedback(match.OfflineControls.IsPaused ? L("Partida en pausa. Di «reanuda» para continuar.", "Game paused. Say “resume” to continue.")
                        : L("Menú abierto. Di «reanuda» para volver.", "Menu open. Say “resume” to return."));
                    break;
                case VoiceAction.Resume: Resume(); break;
            }
        }

        private bool MenuOpen => match.Research.IsOpen || match.Factions.IsOpen || match.OfflineControls.BlocksWorldInput ||
            match.Online?.BlocksWorldInput == true || match.Alpha?.IsOpen == true;
        private static bool WorksInMenus(VoiceAction action) => action == VoiceAction.Pause || action == VoiceAction.Resume ||
            action == VoiceAction.Help || action == VoiceAction.CloseHelp || action == VoiceAction.Cancel;

        private void SelectMatching(Func<UnitState, bool> filter, string selected, string none)
        {
            var ids = OwnedUnits(filter);
            if (ids.Count == 0) { match.SetFeedback(none); return; }
            match.Select(ids);
            match.SetFeedback(selected + ": " + ids.Count + ".");
        }

        private void SelectBuilding(string definitionId)
        {
            var owned = match.World.Buildings.Where(building => building.OwnerId == MatchController.LocalPlayer && building.DefinitionId == definitionId).ToList();
            if (owned.Count == 0) { match.SetFeedback(L("No tienes ", "You have no ") + Name(definitionId, 2) + "."); return; }
            // Saying the same building again steps to the next one.
            var current = match.Economy.SelectedBuilding();
            int index = current != null && current.DefinitionId == definitionId ? (owned.IndexOf(current) + 1) % owned.Count : 0;
            match.Select(new[] { owned[index].Id });
            Reveal(owned[index].Position);
            match.SetFeedback(Capital(Name(definitionId)) + " " + Selected(definitionId, 1) + (owned.Count > 1 ? " (" + (index + 1) + "/" + owned.Count + ")." : "."));
        }

        // Brings a spoken target into view only when it is off screen.
        private void Reveal(SimPoint position)
        {
            var point = DefinitionLoader.ToWorld(position);
            var viewport = match.Rig.Camera.WorldToViewportPoint(point);
            if (viewport.z < 0 || viewport.x < .05f || viewport.x > .95f || viewport.y < .3f || viewport.y > .95f) match.Rig.Focus(point);
        }

        private void AttackPointed()
        {
            var ids = match.SelectedCombatUnits();
            if (ids.Length == 0) { SelectQuietly(IsArmy); ids = match.SelectedCombatUnits(); }
            if (ids.Length == 0) { match.SetFeedback(L("No tienes unidades de combate.", "You have no combat units.")); return; }
            if (!TryPointer(out var point)) { match.SetFeedback(PointAtGround); return; }
            int target = NearestEnemy(point);
            if (target != 0) match.Issue(new AttackCommand(MatchController.LocalPlayer, ids, target), L("¡Al ataque!", "Attacking!"));
            else match.Issue(new MoveCommand(MatchController.LocalPlayer, ids, point, match.Formation), L("No se ve ningún enemigo ahí; avanzando.", "No enemy in sight there; advancing."));
        }

        private void MovePointed()
        {
            var ids = match.SelectedUnits();
            if (ids.Length == 0) { match.SetFeedback(SelectFirst); return; }
            if (!TryPointer(out var point)) { match.SetFeedback(PointAtGround); return; }
            match.Issue(new MoveCommand(MatchController.LocalPlayer, ids, point, match.Formation), L("En marcha.", "Moving."));
        }

        private void Retreat()
        {
            var ids = match.SelectedUnits();
            if (ids.Length == 0) { SelectQuietly(IsArmy); ids = match.SelectedUnits(); }
            if (ids.Length == 0) { match.SetFeedback(L("No tienes unidades de combate.", "You have no combat units.")); return; }
            var home = HomeBuilding();
            if (home == null) { match.SetFeedback(L("No tienes una base a la que volver.", "You have no base to return to.")); return; }
            match.Issue(new MoveCommand(MatchController.LocalPlayer, ids, BesideHome(home), match.Formation), L("¡Retirada a la base!", "Falling back to base!"));
        }

        private void GatherResource(ResourceKind kind)
        {
            var workers = match.Economy.SelectedWorkers();
            if (workers.Length == 0)
            {
                var idle = OwnedUnits(IsIdleWorker);
                if (idle.Count == 0) { match.SetFeedback(L("No hay trabajadores ociosos. Selecciona trabajadores primero.", "No workers are idle. Select workers first.")); return; }
                match.Select(idle); workers = match.Economy.SelectedWorkers();
            }
            var centre = UnitCentre(workers);
            ResourceNodeState best = null; double bestDistance = double.MaxValue;
            foreach (var node in match.World.Resources)
            {
                if (node.Kind != kind || node.RemainingAmount <= 0) continue;
                // The rules only accept sources the player can see.
                if (match.World.Vision != null && !match.World.Vision.IsEntityVisible(MatchController.LocalPlayer, node.Id)) continue;
                double distance = Distance(centre, node.Position);
                if (distance < bestDistance) { best = node; bestDistance = distance; }
            }
            string resource = Parser.Vocabulary.ResourceName(kind);
            if (best == null) { match.SetFeedback(L("No se ve " + resource + " cerca. Explora para encontrar más.", "No " + resource + " in sight. Scout to find more.")); return; }
            string who = workers.Length == 1 ? L("Un trabajador", "One worker") : L(workers.Length + " trabajadores", workers.Length + " workers");
            match.Issue(new GatherCommand(MatchController.LocalPlayer, workers, best.Id), who + L(" a por " + resource + ".", " gathering " + resource + "."));
        }

        private void ReturnCargo()
        {
            if (!match.Economy.HasSelectedCargo())
            {
                var carrying = OwnedUnits(unit => unit.IsWorker && unit.CarriedAmount > 0);
                if (carrying.Count == 0) { match.SetFeedback(L("Ningún trabajador lleva recursos.", "No worker is carrying resources.")); return; }
                match.Select(carrying);
            }
            match.Economy.ReturnCargo();
        }

        // Queues at every finished producer, shortest queue first, like tapping each building's button.
        private void TrainUnits(string unitId, int count)
        {
            if (match.Economy.UnitDefinition(unitId) == null) return;
            var allowed = match.World.ValidateUnitRecruitment(MatchController.LocalPlayer, unitId);
            if (!allowed.Accepted) { match.SetFeedback(allowed.Message); return; }
            var queues = new Dictionary<BuildingState, int>();
            foreach (var building in match.World.Buildings)
            {
                if (building.OwnerId != MatchController.LocalPlayer || !building.IsOperational || building.ActiveResearch != null) continue;
                var site = match.Economy.BuildingDefinition(building.DefinitionId);
                if (site != null && Array.IndexOf(site.TrainableUnitIds ?? Array.Empty<string>(), unitId) >= 0) queues[building] = building.ProductionQueue.Count;
            }
            if (queues.Count == 0)
            {
                match.SetFeedback(L("Necesitas ", "You need ") + ProducerPhrase(unitId) + L(" para entrenar ", " to train ") + Name(unitId, 2) + ".");
                return;
            }
            int queued = 0; string refusal = null;
            for (int i = 0; i < count && refusal == null; i++)
            {
                CommandResult last = default;
                foreach (var site in queues.OrderBy(pair => pair.Value).ThenBy(pair => pair.Key.Id).Select(pair => pair.Key).ToList())
                {
                    last = match.SubmitPlayerCommand(new TrainCommand(MatchController.LocalPlayer, site.Id, unitId));
                    if (last.Accepted) { queues[site]++; queued++; break; }
                }
                if (!last.Accepted) refusal = last.Message;
            }
            if (queued == 0) { match.SetFeedback(refusal); return; }
            match.SetFeedback(match.World.IsNetworkReplica ? L("Orden de entrenamiento enviada.", "Training order sent.")
                : queued + " " + Name(unitId, queued) + L(" en cola", " queued") + (refusal != null ? ". " + refusal : "."));
        }

        private void PlaceBuilding(string buildingId)
        {
            var economy = match.Economy;
            if (economy.SelectedWorkers().Length == 0)
            {
                var worker = NearestWorker(TryPointer(out var near) ? near : HomePoint());
                if (worker == null) { match.SetFeedback(L("No tienes trabajadores para construir.", "You have no workers to build with.")); return; }
                match.Select(new[] { worker.Id });
            }
            economy.BeginBuild(buildingId);
            if (economy.PendingBuildingId != buildingId) return; // BeginBuild already said which requirement is missing.
            if (TryPointer(out var point)) economy.PreviewAt(point);
            match.SetFeedback(economy.HasPreview && economy.PlacementResult.Accepted
                ? L("Buen sitio para " + Article(buildingId) + ". Di «confirma», o toca otro lugar.", "Good spot for the " + Name(buildingId) + ". Say “confirm”, or tap another place.")
                : (economy.HasPreview ? economy.PlacementResult.Message + " " : "") + L("Apunta a otro sitio y di «confirma», o di «cancela».", "Point somewhere else and say “confirm”, or say “cancel”."));
        }

        private void Confirm()
        {
            if (match.Factions.PendingDeployUnitId != 0) { match.Factions.ConfirmDeploy(); return; }
            var economy = match.Economy;
            if (economy.PendingBuildingId == null) { match.SetFeedback(L("No hay nada que confirmar.", "Nothing to confirm.")); return; }
            // Keep a valid spot the player tapped; otherwise use where they point now.
            if ((!economy.HasPreview || !economy.PlacementResult.Accepted) && TryPointer(out var point)) economy.PreviewAt(point);
            var result = economy.ConfirmBuild();
            if (result.Accepted && !match.World.IsNetworkReplica) match.SetFeedback(L("Cimientos colocados; los trabajadores ya construyen.", "Foundation placed. Workers are constructing."));
        }

        private void Cancel()
        {
            var economy = match.Economy;
            if (match.ChoosingWall || match.ChoosingDescent) { match.CancelSiegeTarget(); match.SetFeedback(L("Orden de muralla cancelada.", "Wall order cancelled.")); }
            else if (match.Factions.PendingDeployUnitId != 0) { match.Factions.CancelDeploy(); match.SetFeedback(L("Despliegue cancelado.", "Deployment cancelled.")); }
            else if (economy.PendingBuildingId != null || economy.IsChoosingRally) { economy.CancelBuild(); match.SetFeedback(L("Colocación cancelada.", "Placement cancelled.")); }
            else if (MenuOpen) Resume();
            else if (HelpVisible) { HelpVisible = false; match.SetFeedback(L("Ayuda oculta.", "Help hidden.")); }
            else match.SetFeedback(L("No hay nada que cancelar.", "Nothing to cancel."));
        }

        private void Resume()
        {
            bool open = MenuOpen;
            match.Research.Close(); match.Factions.Close(); match.Alpha?.Close();
            if (match.Online?.IsOpen == true) match.Online.Close();
            if (match.OfflineControls.IsOpen) match.OfflineControls.Close();
            if (open) match.SetFeedback(L("De vuelta a la partida.", "Back to the game."));
        }

        private void ResearchFamily(string family)
        {
            if (!match.World.TryGetPlayer(MatchController.LocalPlayer, out var player)) return;
            foreach (string id in Parser.Vocabulary.TechnologiesIn(family))
                if (!player.HasTechnology(id)) { StartResearch(id); return; }
            match.SetFeedback(L("Ya has completado esas mejoras.", "Those upgrades are already complete."));
        }

        private void AdvanceEra()
        {
            if (!match.World.TryGetPlayer(MatchController.LocalPlayer, out var player)) return;
            foreach (var technology in match.World.Definition.Technologies ?? Array.Empty<TechnologyDefinition>())
                if (!string.IsNullOrEmpty(technology.AdvancesToEraId) && EraTier(technology.AdvancesToEraId) == player.EraTier + 1 && match.Factions.Allows(technology.RequiredFactionId))
                { StartResearch(technology.Id); return; }
            match.SetFeedback(L("Ya estás en la última era.", "You are already in the final era."));
        }

        private void StartResearch(string technologyId)
        {
            string name = match.Research.TechnologyName(technologyId);
            if (match.Research.ActiveSite(technologyId) != null) { match.SetFeedback(name + L(" ya se está investigando.", " is already being researched.")); return; }
            var check = match.Research.Validate(technologyId);
            if (!check.Accepted) { match.SetFeedback(name + ": " + check.Message); return; }
            match.Research.Start(technologyId);
        }

        private int EraTier(string eraId)
        {
            foreach (var era in match.World.Definition.Eras ?? Array.Empty<EraDefinition>()) if (era.Id == eraId) return era.Tier;
            return -1;
        }

        private void FocusSelection()
        {
            var points = new List<SimPoint>();
            foreach (int id in match.Selection)
            {
                if (match.World.TryGetUnit(id, out var unit)) points.Add(unit.Position);
                else if (match.World.TryGetBuilding(id, out var building)) points.Add(building.Position);
            }
            if (points.Count == 0)
                foreach (var unit in match.World.Units) if (unit.OwnerId == MatchController.LocalPlayer && IsArmy(unit)) points.Add(unit.Position);
            if (points.Count == 0) { match.SetFeedback(L("No hay nada que enfocar.", "Nothing to focus on.")); return; }
            match.Rig.Focus(DefinitionLoader.ToWorld(Centre(points)));
            match.SetFeedback(L("Cámara centrada.", "Camera centred."));
        }

        private List<int> OwnedUnits(Func<UnitState, bool> filter)
        {
            var ids = new List<int>();
            foreach (var unit in match.World.Units) if (unit.OwnerId == MatchController.LocalPlayer && filter(unit)) ids.Add(unit.Id);
            return ids;
        }

        private void SelectQuietly(Func<UnitState, bool> filter) { var ids = OwnedUnits(filter); if (ids.Count > 0) match.Select(ids); }
        private static bool IsArmy(UnitState unit) => !unit.IsWorker && !unit.IsPackedOutpost && (unit.AttackDamage > 0 || (unit.Tags & CombatTags.Siege) != 0);
        private static bool IsIdleWorker(UnitState unit) => unit.IsWorker && unit.WorkerTask == WorkerTask.None && unit.Order == UnitOrder.Idle;

        private BuildingState HomeBuilding()
        {
            BuildingState fallback = null;
            foreach (var building in match.World.Buildings)
            {
                if (building.OwnerId != MatchController.LocalPlayer) continue;
                var definition = match.Economy.BuildingDefinition(building.DefinitionId);
                // The seat of the settlement trains workers and takes deliveries.
                if (definition != null && definition.CanDropOff &&
                    (definition.TrainableUnitIds ?? Array.Empty<string>()).Any(id => match.Economy.UnitDefinition(id)?.IsWorker == true)) return building;
                if (fallback == null && building.IsComplete) fallback = building;
            }
            return fallback;
        }

        private SimPoint HomePoint()
        {
            var home = HomeBuilding();
            var map = match.World.Map;
            return home != null ? home.Position : new SimPoint(map.WidthCells * map.CellSizeMillimetres / 2, map.HeightCells * map.CellSizeMillimetres / 2);
        }

        // A point on the map-centre side of the base, clear of its footprint.
        private SimPoint BesideHome(BuildingState home)
        {
            var map = match.World.Map; int cell = map.CellSizeMillimetres;
            double dx = map.WidthCells * cell / 2.0 - home.Position.X, dz = map.HeightCells * cell / 2.0 - home.Position.Z;
            double length = Math.Sqrt(dx * dx + dz * dz);
            if (length < 1) { dx = 1; dz = 0; length = 1; }
            double offset = (Math.Max(home.WidthCells, home.DepthCells) / 2.0 + 2) * cell;
            return new SimPoint((int)(home.Position.X + dx / length * offset), (int)(home.Position.Z + dz / length * offset));
        }

        // Idle workers first, then the nearest busy one.
        private UnitState NearestWorker(SimPoint point)
        {
            UnitState best = null; double bestScore = double.MaxValue;
            foreach (var unit in match.World.Units)
            {
                if (unit.OwnerId != MatchController.LocalPlayer || !unit.IsWorker) continue;
                double score = Distance(point, unit.Position) + (IsIdleWorker(unit) ? 0 : 1e9);
                if (score < bestScore) { best = unit; bestScore = score; }
            }
            return best;
        }

        // Units before buildings; a building counts from its footprint edge.
        private int NearestEnemy(SimPoint point)
        {
            int best = 0; double bestDistance = AttackReachMillimetres;
            foreach (var unit in match.World.Units)
                if (IsVisibleEnemy(unit.OwnerId, unit.Id) && Distance(point, unit.Position) < bestDistance) { best = unit.Id; bestDistance = Distance(point, unit.Position); }
            if (best != 0) return best;
            int cell = match.World.Map.CellSizeMillimetres;
            foreach (var building in match.World.Buildings)
            {
                if (!IsVisibleEnemy(building.OwnerId, building.Id)) continue;
                double distance = Distance(point, building.Position) - Math.Max(building.WidthCells, building.DepthCells) * cell * .5;
                if (distance < bestDistance) { best = building.Id; bestDistance = distance; }
            }
            return best;
        }

        private bool IsVisibleEnemy(int owner, int id) => owner != MatchController.LocalPlayer && owner != 0 &&
            (match.World.Vision == null || match.World.Vision.IsEntityVisible(MatchController.LocalPlayer, id));

        private bool TryPointer(out SimPoint point)
        {
            if (match.Rig.GroundPoint(PointerOverride ?? PointerScreen(), out var ground)) { point = DefinitionLoader.ToSimulation(ground); return true; }
            point = default; return false;
        }

        private Vector2 PointerScreen()
        {
            var centre = new Vector2(Screen.width * .5f, Screen.height * .5f);
            var mouse = Mouse.current;
            if (mouse == null) return centre;
            var position = mouse.position.ReadValue();
            bool inside = position.x >= 0 && position.y >= 0 && position.x <= Screen.width && position.y <= Screen.height;
            return inside && !OverUi(position) ? position : centre;
        }

        private bool OverUi(Vector2 point)
        {
            if (EventSystem.current == null) return false;
            uiHits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, uiHits);
            return uiHits.Count > 0;
        }

        private static bool TypingInTextField()
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            var field = selected != null ? selected.GetComponent<InputField>() : null;
            return field != null && field.isFocused;
        }

        private SimPoint UnitCentre(IEnumerable<int> ids)
        {
            var points = new List<SimPoint>();
            foreach (int id in ids) if (match.World.TryGetUnit(id, out var unit)) points.Add(unit.Position);
            return Centre(points);
        }

        private static SimPoint Centre(List<SimPoint> points)
        {
            if (points.Count == 0) return default;
            long x = 0, z = 0;
            foreach (var point in points) { x += point.X; z += point.Z; }
            return new SimPoint((int)(x / points.Count), (int)(z / points.Count));
        }

        private static double Distance(SimPoint a, SimPoint b) { double dx = a.X - b.X, dz = a.Z - b.Z; return Math.Sqrt(dx * dx + dz * dz); }

        private void StartListening(float until, bool once, bool explain = false)
        {
            if (recognizer == null || !recognizer.IsAvailable)
            {
                if (explain) match.SetFeedback(Problem ?? L("El reconocimiento de voz de Windows no está disponible en este equipo.", "Windows speech recognition is not available on this device."));
                match.Hud?.Invalidate();
                return;
            }
            listenUntil = until; listenOnce = once;
            if (!recognizer.IsListening) recognizer.Start();
            if (explain && !recognizer.IsListening && recognizer.Problem != null) match.SetFeedback(recognizer.Problem);
            match.Hud?.Invalidate();
        }

        private void StopListening()
        {
            listenUntil = -1; listenOnce = false;
            if (recognizer != null && recognizer.IsListening) recognizer.Stop();
            match.Hud?.Invalidate();
        }

        private void ResumeAlwaysOn()
        {
            if (Mode == VoiceListenMode.AlwaysOn && focused && match.Shell?.IsOpen != true && !IsListening) StartListening(float.PositiveInfinity, false);
        }

        private void ReleaseRecognizer()
        {
            if (recognizer == null || injected) return;
            recognizer.Recognized -= OnRecognized; recognizer.Dispose(); recognizer = null;
        }

        private void OnRecognized(string text) => pending.Enqueue(text);

        // Feedback names what the player says: "lanceros", "cuartel", "spearmen", "barracks".
        private string Name(string id, int count = 1)
        {
            var noun = Parser.Vocabulary.Find(id)?.Primary;
            if (noun != null) return count == 1 ? noun.Singular : noun.Plural;
            return match.Economy.UnitDefinition(id)?.DisplayName ?? match.Economy.BuildingDefinition(id)?.DisplayName ?? id;
        }
        private bool Feminine(string id) => Parser.Vocabulary.Find(id)?.Primary.Feminine == true;
        private string Selected(string id, int count) =>
            L(Feminine(id) ? count == 1 ? "seleccionada" : "seleccionadas" : count == 1 ? "seleccionado" : "seleccionados", "selected");
        private string Article(string id) => (Feminine(id) ? "la " : "el ") + Name(id);
        private string ProducerPhrase(string unitId)
        {
            foreach (var building in match.World.Definition.Buildings ?? Array.Empty<BuildingDefinition>())
                if (Array.IndexOf(building.TrainableUnitIds ?? Array.Empty<string>(), unitId) >= 0 && match.Factions.Allows(building.RequiredFactionId))
                    return L(Feminine(building.Id) ? "una " + Name(building.Id) + " terminada" : "un " + Name(building.Id) + " terminado", "a finished " + Name(building.Id));
            return L("un edificio que lo entrene", "a building that trains it");
        }
        private static string Capital(string text) => string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);
        private string L(string spanish, string english) => Language == VoiceLanguage.Spanish ? spanish : english;
        private string SelectFirst => L("Primero selecciona unidades, por ejemplo con «selecciona el ejército».", "Select units first, for example with “select army”.");
        private string PointAtGround => L("Apunta al terreno con el ratón.", "Point at the ground with the mouse.");

        public string StatusText
        {
            get
            {
                if (Mode == VoiceListenMode.Off) return L("VOZ · apagada · toca para activarla", "VOICE · off · tap to turn on");
                if (!IsAvailable || Problem != null && !IsListening) return L("VOZ · no disponible", "VOICE · unavailable");
                if (IsListening) return Mode == VoiceListenMode.AlwaysOn ? L("VOZ · escuchando siempre", "VOICE · always listening") : L("VOZ · escuchando…", "VOICE · listening…");
                return Keyboard.current != null ? L("VOZ · mantén V o toca para hablar", "VOICE · hold V or tap to talk") : L("VOZ · toca para hablar", "VOICE · tap to talk");
            }
        }

        public string DetailText
        {
            get
            {
                if (Mode != VoiceListenMode.Off && (!IsAvailable || Problem != null && !IsListening))
                    return Problem ?? L("El reconocimiento de voz de Windows no está disponible.", "Windows speech recognition is not available.");
                if (ShowsRecentPhrase) return "«" + LastHeard + "»" + (LastIntent.IsNone ? L(" · no entendido", " · not understood") : "");
                return L("Di «ayuda» para ver ejemplos.", "Say “help” for examples.");
            }
        }

        public static VoiceLanguage ResolveLanguage(VoiceLanguageChoice choice) =>
            choice == VoiceLanguageChoice.Spanish ? VoiceLanguage.Spanish : choice == VoiceLanguageChoice.English ? VoiceLanguage.English
            : Application.systemLanguage == SystemLanguage.Spanish ? VoiceLanguage.Spanish : VoiceLanguage.English;
        public static string ModeName(VoiceListenMode mode) =>
            mode == VoiceListenMode.PushToTalk ? "Push-to-talk (V)" : mode == VoiceListenMode.AlwaysOn ? "Always listening" : "Off";
        public static string LanguageName(VoiceLanguageChoice choice) =>
            choice == VoiceLanguageChoice.Spanish ? "Español" : choice == VoiceLanguageChoice.English ? "English"
            : "Auto · " + (ResolveLanguage(choice) == VoiceLanguage.Spanish ? "Español" : "English");

        public void Dispose()
        {
            pending.Clear(); StopListening();
            if (recognizer != null) { recognizer.Recognized -= OnRecognized; recognizer.Dispose(); recognizer = null; }
        }
    }

    public sealed partial class MatchController
    {
        public VoiceControls Voice { get; private set; }
        private void InitializeVoice() { if (Alpha != null) Voice = new VoiceControls(this); }
    }
}
