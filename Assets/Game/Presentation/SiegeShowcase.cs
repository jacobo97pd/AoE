using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Emberfield.Simulation;
using UnityEngine;

namespace Emberfield.Presentation
{
    /// <summary>
    /// Films the authored siege fixture: a kingdom is fortified on camera, beasts walk out of its gate, and an
    /// assault climbs the wall with ladders while rams break the gate. Opt-in with -emberfieldSiegeShowcase FOLDER.
    ///
    /// The director owns the clock. It calls World.Tick directly rather than MatchController.AdvanceSimulationTick,
    /// because that would also tick the offline AI, which would pull the rams and climbers away mid-shot. Both
    /// seats are driven from here through ordinary commands and the ordinary validation.
    ///
    /// Frames are written with the same names, the same frames.csv columns and the same mode vocabulary as the
    /// full-match recorder, because the Blender encoder reads exactly that contract.
    /// </summary>
    public static class SiegeShowcase
    {
        public static bool IsRunning { get; private set; }
        private static bool started;

        public static void TryStart(MatchController match)
        {
            if (started || !Debug.isDebugBuild && !Application.isEditor) return;
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, SiegeShowcaseScenario.Flag);
            if (index < 0 || index + 1 >= args.Length) return;
            if (!SiegeShowcaseScenario.IsActive)
            { Debug.LogError("EMBERFIELD_SIEGE_SHOWCASE the fixture world was not created."); SafeQuit.Request(1); return; }
            started = true; IsRunning = true;
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            match.StartCoroutine(new Director(match, Path.GetFullPath(args[index + 1])).Run());
        }

        // ================================================================================ director

        private sealed class Director
        {
            private const int Me = MatchController.LocalPlayer, Foe = 2;
            private readonly MatchController match;
            private readonly World world;
            private readonly string folder;
            private readonly System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
            private readonly Dictionary<int, int> wallByX = new Dictionary<int, int>();
            private readonly List<string> log = new List<string>();
            private readonly List<double> tickCost = new List<double>();
            private Film film;
            private Shot shot;
            private int gateId, keepId;

            public Director(MatchController match, string folder)
            { this.match = match; this.folder = folder; world = match.World; }

            public IEnumerator Run()
            {
                yield return null;
                match.enabled = false;
                match.Shell?.Close(); match.OfflineControls.Close();
                yield return null;
                Directory.CreateDirectory(folder);
                string frames = Path.Combine(folder, "frames");
                Directory.CreateDirectory(frames);
                film = new Film(frames);
                match.Hud.PrepareOffscreenCapture(match.Rig.Camera);
                match.Rig.MinimumZoom = 3.5f; match.Rig.MaximumZoom = 40f;
                shot = new Shot(match);

                yield return ActSettle();
                yield return ActFortify();
                yield return ActBeasts();
                yield return ActAssault();
                yield return ActBreach();
                yield return ActResult();

                film.Close();
                var report = new Report
                {
                    fixture = SiegeShowcaseScenario.FixtureVersion,
                    ticks = world.TickIndex,
                    simulatedSeconds = world.TickIndex / (double)World.TickRate,
                    frames = film.Count, battleFrames = film.Battle,
                    wallsRaised = wallByX.Count, deaths = world.DeathCount,
                    peakUnits = peakUnits, peakBuildings = peakBuildings,
                    tickMedianMs = Quantile(.5), tickP95Ms = Quantile(.95), tickMaxMs = Quantile(1),
                    note = "Economy granted, not gathered. Every fortification was placed and raised live by "
                        + "ordinary build commands. Frame counts are not a frame-rate measurement.",
                    timeline = log,
                };
                File.WriteAllText(Path.Combine(folder, "showcase.json"), JsonUtility.ToJson(report, true), new UTF8Encoding(false));
                Debug.Log("EMBERFIELD_SIEGE_SHOWCASE frames=" + film.Count + " battle=" + film.Battle + " ticks=" + world.TickIndex);
                IsRunning = false;
                SafeQuit.Request(0);
            }

            [Serializable]
            private sealed class Report
            {
                public string fixture, note;
                public long ticks;
                public double simulatedSeconds, tickMedianMs, tickP95Ms, tickMaxMs;
                public int frames, battleFrames, wallsRaised, deaths, peakUnits, peakBuildings;
                public List<string> timeline;
            }

            private int peakUnits, peakBuildings;

            /// <summary>Simulation cost under this load. Not a frame rate, and never to be quoted as one.</summary>
            private double Quantile(double share)
            {
                if (tickCost.Count == 0) return 0;
                var sorted = new List<double>(tickCost);
                sorted.Sort();
                int index = Mathf.Clamp(Mathf.CeilToInt((float)(share * sorted.Count)) - 1, 0, sorted.Count - 1);
                return System.Math.Round(sorted[index], 4);
            }

            // ---------------------------------------------------------------- the clock

            private void Step()
            {
                match.Metrics?.BeginTick(world);
                watch.Restart(); world.Tick(); watch.Stop();
                // Timed around the simulation only. Frame capture happens afterwards and reads the GPU back
                // synchronously, so it dominates wall time and says nothing about either of these numbers.
                tickCost.Add(watch.Elapsed.TotalMilliseconds);
                if (world.Units.Count > peakUnits) peakUnits = world.Units.Count;
                if (world.Buildings.Count > peakBuildings) peakBuildings = world.Buildings.Count;
                match.Metrics?.ObserveTick(world, watch.Elapsed.TotalMilliseconds);
            }

            /// <summary>
            /// One simulated tick, presented and written. Battle beats write two frames at alpha .5 and 1 so the
            /// interpolated motion, the climb height and the rigged animation all move smoothly; an overview beat
            /// writes every fourth tick for a time-lapse.
            /// </summary>
            private void Frame(bool battle)
            {
                Step();
                if (battle)
                {
                    match.SyncPresentation(.5f); shot.Advance(World.TickSeconds * .5f); film.Write(match, world.TickIndex, .5f, "battle");
                    match.SyncPresentation(1f); shot.Advance(World.TickSeconds * .5f); film.Write(match, world.TickIndex, 1f, "battle");
                }
                else
                {
                    match.SyncPresentation(1f); shot.Advance(World.TickSeconds);
                    if (world.TickIndex % 4 == 0) film.Write(match, world.TickIndex, 1f, "overview");
                }
            }

            private IEnumerator Roll(int ticks, bool battle)
            {
                for (int i = 0; i < ticks; i++) { Frame(battle); if (world.TickIndex % 5 == 0) yield return null; }
            }

            private IEnumerator RollUntil(Func<bool> done, int limit, bool battle)
            {
                for (int i = 0; i < limit && !done(); i++) { Frame(battle); if (world.TickIndex % 5 == 0) yield return null; }
            }

            /// <summary>
            /// Waits for an order to finish, after giving it time to start. A unit that was told to walk this very
            /// tick is still Idle, so waiting on Idle alone returns at once and every later step acts on units
            /// that never moved. That one mistake cost a keep, a wall segment and a wall garrison in take one.
            /// </summary>
            private IEnumerator Settle(Func<bool> done, int limit, bool battle)
            {
                yield return Roll(16, battle);
                yield return RollUntil(done, limit, battle);
            }

            private void Say(string text)
            {
                match.SetFeedback(text);
                log.Add((world.TickIndex / World.TickRate) + "s " + text);
            }

            private CommandResult Mine(IGameCommand command) => match.SubmitPlayerCommand(command);
            private CommandResult Foes(IGameCommand command) => world.Submit(command);
            private static Vector3 At(int x, int z) => DefinitionLoader.ToWorld(new SimPoint(x, z));

            private bool CrewIdle()
            {
                foreach (int id in SiegeShowcaseScenario.Crew)
                    if (world.TryGetUnit(id, out var unit) && unit.Order == UnitOrder.Moving) return false;
                return true;
            }

            private bool HasTechnology(string id)
            {
                if (!world.TryGetPlayer(Me, out var player)) return false;
                foreach (string completed in player.CompletedTechnologyIds) if (completed == id) return true;
                return false;
            }

            private List<int> Living(IEnumerable<int> ids)
            {
                var alive = new List<int>();
                foreach (int id in ids) if (world.TryGetUnit(id, out _)) alive.Add(id);
                return alive;
            }

            // ---------------------------------------------------------------- construction

            /// <summary>
            /// Places one building and films it rising. Placement is refused for three reasons that a walk fixes —
            /// a builder standing on the footprint, no path to it, and ground nobody has seen yet — so each is met
            /// by staging the crew and trying again rather than by giving up.
            /// </summary>
            private IEnumerator Raise(string definitionId, int x, int z, SimPoint stage, Action<int> record)
            {
                var site = new SimPoint(x, z);
                // Walk clear first, every time. Segments are adjacent, so the crew that has just finished one is
                // standing inside the next one's clearance; validating before moving fails on the tightest and
                // most visible part of the wall.
                Mine(new MoveCommand(Me, SiegeShowcaseScenario.Crew, stage, MovementFormation.Loose));
                yield return Settle(CrewIdle, 200, false);
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    var check = world.ValidatePlacement(Me, SiegeShowcaseScenario.Crew, definitionId, site);
                    if (check.Accepted) break;
                    if (check.Reason != CommandRejection.UnitObstruction && check.Reason != CommandRejection.NoPath &&
                        check.Reason != CommandRejection.TargetNotVisible)
                    { Say("Rechazado " + definitionId + " en " + x + "/" + z + ": " + check.Message); yield break; }
                    Mine(new MoveCommand(Me, SiegeShowcaseScenario.Crew, stage, MovementFormation.Loose));
                    yield return Settle(CrewIdle, 200, false);
                }
                var placed = Mine(new BuildCommand(Me, SiegeShowcaseScenario.Crew, definitionId, site));
                if (!placed.Accepted) { Say("Rechazado " + definitionId + " en " + x + "/" + z + ": " + placed.Message); yield break; }
                int id = placed.EntityId;
                record(id);
                yield return RollUntil(() => world.TryGetBuilding(id, out var raised) && raised.IsComplete, 900, false);
            }

            // ---------------------------------------------------------------- the acts

            private IEnumerator ActSettle()
            {
                shot.Cut(At(26000, 26000), 0, 52, 13);
                Say("Asentamiento de Ceniza. Avanzando a la Era del Reino.");
                // The attackers hold their staging ground until they are called, so nothing drifts on camera.
                Foes(new StopCommand(Foe, SiegeShowcaseScenario.Raiders));
                Foes(new StopCommand(Foe, SiegeShowcaseScenario.Ladders));
                Foes(new StopCommand(Foe, SiegeShowcaseScenario.Rams));
                Foes(new StopCommand(Foe, new[] { SiegeShowcaseScenario.Drake }));
                Mine(new ResearchCommand(Me, SiegeShowcaseScenario.Hearth, "advance_kingdom"));
                yield return Roll(140, false);
                shot.OrbitPerSecond = 1.4f; shot.Pitch = 50; shot.Zoom = 12;
                yield return RollUntil(() => HasTechnology("advance_kingdom"), 760, false);
                shot.OrbitPerSecond = 0;
                film.Still(match, Path.Combine(folder, "still-kingdom.png"));
            }

            private IEnumerator ActFortify()
            {
                Say("Era del Reino. Levantad la muralla.");
                Mine(new ResearchCommand(Me, SiegeShowcaseScenario.Hearth, "advance_dominion"));
                Mine(new TrainCommand(Me, SiegeShowcaseScenario.BeastLodge, "war_troll"));
                shot.Cut(At(20000, 29000), 0, 44, 10);
                foreach (var site in SiegeShowcaseScenario.KingdomWorks)
                {
                    shot.Target = At(site.X, site.Z - 2000);
                    int raised = 0;
                    // Three cells inside the line: far enough that eight Tenders do not occupy the footprint
                    // they are about to be asked to validate, near enough to walk back and raise it.
                    yield return Raise(site.Id, site.X, site.Z, new SimPoint(Mathf.Clamp(site.X, 18000, 36000), 25500), id => raised = id);
                    if (site.Id == "wall" && raised != 0) wallByX[site.X] = raised;
                }
                Say("Muralla en pie. Esperando la Era del Dominio.");
                film.Still(match, Path.Combine(folder, "still-wall.png"));
                yield return RollUntil(() => HasTechnology("advance_dominion"), 1200, false);

                shot.Cut(At(33000, 27000), 0, 38, 9);
                Say("Era del Dominio. Alzad el torreon.");
                // The keep is four cells square and sits across the lane the crew uses for the wall, so it
                // stages west of itself instead.
                yield return Raise(SiegeShowcaseScenario.KeepSite.Id, SiegeShowcaseScenario.KeepSite.X,
                    SiegeShowcaseScenario.KeepSite.Z, new SimPoint(24000, 22000), id => keepId = id);

                // The gate goes up last: a finished gate is shut, and a shut gate would fence the builders out.
                shot.Cut(At(26000, 30500), 180, 32, 7);
                Say("Cerrad la puerta.");
                yield return Raise(SiegeShowcaseScenario.GateSite.Id, SiegeShowcaseScenario.GateSite.X,
                    SiegeShowcaseScenario.GateSite.Z, new SimPoint(26000, 25500), id => gateId = id);
                film.Still(match, Path.Combine(folder, "still-fortress.png"));
            }

            private IEnumerator ActBeasts()
            {
                var trolls = Living(SiegeShowcaseScenario.Trolls);
                foreach (var unit in world.Units)
                    if (unit.OwnerId == Me && unit.DefinitionId == "war_troll" && !trolls.Contains(unit.Id)) trolls.Add(unit.Id);
                if (trolls.Count == 0) yield break;

                shot.Cut(At(23000, 32000), 180, 26, 6);
                shot.SubjectId = trolls[0]; shot.Framing = .36f; shot.Follow = 1.2f;
                Say("Abrid la puerta. Que salgan las bestias.");
                if (gateId != 0) Mine(new SetGateCommand(Me, gateId, true));
                yield return Roll(30, true);

                int[] marks = { 17500, 19500, 21500, 23500 };
                for (int i = 0; i < trolls.Count && i < marks.Length; i++)
                    Mine(new MoveCommand(Me, new[] { trolls[i] }, new SimPoint(marks[i], 33500), MovementFormation.Loose));
                yield return Roll(200, true);
                film.Still(match, Path.Combine(folder, "still-beasts.png"));
                shot.SubjectId = 0;
            }

            // The wall row occupies z 30000..31000. Boarding asks for 1800 mm from that footprint and a ladder
            // for 1300 mm, so the defenders form up just inside it and the assault just outside.
            private const int InsideLane = 29300, OutsideLane = 31600;

            private IEnumerator ActAssault()
            {
                Say("Enemigo a la vista. Cerrad la puerta.");
                if (gateId != 0) Mine(new SetGateCommand(Me, gateId, false));

                // Deck capacity is four and is shared between both sides, so two defenders per climbed segment
                // is the most that can be posted without refusing a climber at the top of the ladder.
                // One contested segment. Deck capacity is four and is shared, so two defenders leave exactly two
                // slots for climbers -- and one focal point films better than two half-fought ones.
                shot.Cut(At(SiegeShowcaseScenario.EastWallX, 30000), 0, 32, 9);
                yield return Garrison(SiegeShowcaseScenario.EastWallX, 0, 1);
                film.Still(match, Path.Combine(folder, "still-garrison.png"));

                Say("Escaleras contra el muro.");
                shot.Cut(At(25000, 31500), 0, 36, 14);
                // The rams go in with the ladders even though they end up in front of the east ladder's foot: they draw
                // the trolls waiting outside the gate. Held back, they left the trolls the ladders, which fell before
                // anyone could climb them.
                foreach (int id in Living(SiegeShowcaseScenario.Rams)) Foes(new MoveCommand(Foe, new[] { id }, new SimPoint(26000, 32600), MovementFormation.Loose));
                Foes(new MoveCommand(Foe, new[] { SiegeShowcaseScenario.Drake }, new SimPoint(30500, 32600), MovementFormation.Loose));
                var ladders = Living(SiegeShowcaseScenario.Ladders);
                for (int i = 0; i < ladders.Count; i++)
                    Foes(new MoveCommand(Foe, new[] { ladders[i] }, new SimPoint(LadderLane(ladders[i]), OutsideLane), MovementFormation.Loose));
                // The climbers march with the ladders. The defenders shoot at the equipment, and a ladder left alone at
                // the wall while its crew walked up from the staging ground was felled before anyone could climb it.
                var crew = ClimbCrew();
                for (int i = 0; i < crew.Count; i++)
                    Foes(new MoveCommand(Foe, new[] { crew[i] }, ClimbMark(LadderLane(SiegeShowcaseScenario.Ladders[2]), i), MovementFormation.Loose));
                yield return Roll(30, true);
                // Then close on the east ladder from outside, so it is seen to pull up and swing against the face.
                int east = SiegeShowcaseScenario.Ladders[2];
                if (world.TryGetUnit(east, out var rolling))
                {
                    shot.Cut(DefinitionLoader.ToWorld(rolling.Position) + Vector3.up, ClimbYaw, ClimbPitch, 5);
                    shot.SubjectId = east; shot.Framing = .45f; shot.Follow = 1.4f;
                }
                yield return Settle(() => Idle(Living(SiegeShowcaseScenario.Ladders)), 400, true);
                shot.SubjectId = 0;
                foreach (int id in Living(SiegeShowcaseScenario.Ladders)) Say("Escalera " + id + Where(id));
                // A ladder that is still walking is not equipment, and a ladder that walks during the climb
                // cancels it, so they are stopped before anyone is sent up.
                foreach (int id in Living(SiegeShowcaseScenario.Ladders)) Foes(new StopCommand(Foe, new[] { id }));

                yield return Climb(SiegeShowcaseScenario.EastWallX, 2);
                // Then wide from outside: whichever ladders the defenders have left standing, and the deck they led to.
                shot.Cut(At(24500, 33500), 165, 32, 10.5f);
                yield return Roll(120, true);
                film.Still(match, Path.Combine(folder, "still-climb.png"));
            }

            /// <summary>Walks two guards to the inside face of a segment, then sends them up it.</summary>
            private IEnumerator Garrison(int wallX, int first, int second)
            {
                if (!wallByX.TryGetValue(wallX, out int wall)) yield break;
                var guards = new List<int>();
                foreach (int index in new[] { first, second })
                    if (index < SiegeShowcaseScenario.Guards.Length && world.TryGetUnit(SiegeShowcaseScenario.Guards[index], out _))
                        guards.Add(SiegeShowcaseScenario.Guards[index]);
                if (guards.Count == 0) yield break;
                for (int i = 0; i < guards.Count; i++)
                    Mine(new MoveCommand(Me, new[] { guards[i] }, new SimPoint(wallX - 800 + i * 1600, InsideLane), MovementFormation.Loose));
                // Keep asking. Local avoidance can leave a soldier short of the mark, and one that stopped short
                // would otherwise refuse the order for the whole group and take the garrison with it.
                for (int attempt = 0; attempt < 4; attempt++)
                {
                    yield return Settle(() => Idle(guards), 160, true);
                    var waiting = new List<int>();
                    foreach (int id in guards)
                    {
                        if (!world.TryGetUnit(id, out var guard)) continue;
                        if (guard.WallId != 0 || guard.BoardingWallId != 0) continue;
                        if (Mine(new BoardWallCommand(Me, new[] { id }, wall)).Accepted) continue;
                        waiting.Add(id);
                    }
                    if (waiting.Count == 0) break;
                    for (int i = 0; i < waiting.Count; i++)
                        Mine(new MoveCommand(Me, new[] { waiting[i] }, new SimPoint(wallX - 500 + i * 1000, InsideLane), MovementFormation.Loose));
                    if (attempt == 3) foreach (int id in waiting) Say("La guardia " + id + Where(id) + " no llego al muro.");
                }
                yield return Roll(40, true);
            }

            /// <summary>Walks two raiders to the outside face beside their ladder, then sends them up it.</summary>
            private IEnumerator Climb(int wallX, int ladderIndex)
            {
                if (!wallByX.TryGetValue(wallX, out int wall)) yield break;
                var ladders = SiegeShowcaseScenario.Ladders;
                if (ladderIndex >= ladders.Length || !world.TryGetUnit(ladders[ladderIndex], out _)) yield break;
                var climbers = ClimbCrew();
                if (climbers.Count == 0) yield break;
                if (world.TryGetUnit(ladders[ladderIndex], out var parked))
                    shot.Cut(DefinitionLoader.ToWorld(parked.Position) + Vector3.up * 1.3f, ClimbYaw, ClimbPitch, 4.2f);
                else shot.Cut(At(wallX, 30200), 0, 30, 9);
                for (int i = 0; i < climbers.Count; i++)
                    Foes(new MoveCommand(Foe, new[] { climbers[i] }, ClimbMark(LadderLane(ladders[ladderIndex]), i), MovementFormation.Loose));
                var attached = new List<int>();
                for (int attempt = 0; attempt < 4 && attached.Count == 0; attempt++)
                {
                    yield return Settle(() => Idle(climbers), 140, true);
                    // Whichever ladder is still standing closest: the defenders shoot at the equipment too, and a
                    // destroyed ladder reports the same refusal as one that is simply too far away.
                    int ladder = NearestLadder(wall);
                    if (ladder == 0) { Say("Sin escaleras en pie."); yield break; }
                    foreach (int id in climbers)
                    {
                        if (!world.TryGetUnit(id, out var raider) || raider.WallId != 0 || raider.BoardingWallId != 0) continue;
                        if (Foes(new BoardWallCommand(Foe, new[] { id }, wall, ladder)).Accepted) attached.Add(id);
                    }
                    if (attached.Count > 0) break;
                    if (world.TryGetUnit(ladder, out var equipment))
                        foreach (int id in climbers)
                            if (world.TryGetUnit(id, out _))
                                Foes(new MoveCommand(Foe, new[] { id }, new SimPoint(equipment.Position.X + 600, OutsideLane), MovementFormation.Loose));
                }
                if (attached.Count == 0) { Say("Nadie pudo subir."); yield break; }
                climbers = attached;
                foreach (int id in climbers) boarded.Add(id);
                Say("Suben por la escalera.");
                shot.SubjectId = climbers[0]; shot.Framing = .42f; shot.Follow = 1.6f; shot.Zoom = 3.4f;
                yield return Roll(130, true);
                shot.SubjectId = 0;
            }

            // The ladder shots look south-east at the outside of the wall from the north-west, steeply enough that the
            // line of sight clears the forest's south-west corner and passes over most of the rams. The ladder's lean
            // then runs across the frame and its climbers are seen from behind. The old cut looked at the inside face
            // from 23 m back, and the wall hid everything but the last step onto the deck. Looking along the wall lined
            // the other two ladders and the rams up in front of it; from the north or east the forest hides it.
            private const float ClimbYaw = 128, ClimbPitch = 44;

            /// <summary>The two raiders who go up the east ladder: alive, on the ground and not yet sent up.</summary>
            private List<int> ClimbCrew()
            {
                var crew = new List<int>();
                foreach (int id in Living(SiegeShowcaseScenario.Raiders))
                {
                    if (crew.Count == 2) break;
                    if (world.TryGetUnit(id, out var raider) && raider.WallId == 0 && !boarded.Contains(id)) crew.Add(id);
                }
                return crew;
            }

            /// <summary>Where a climber waits: a metre to either side of the ladder, clear of its base.</summary>
            private static SimPoint ClimbMark(int ladderX, int index) => new SimPoint(ladderX - 1000 + index * 2000, OutsideLane);

            private readonly HashSet<int> boarded = new HashSet<int>();

            /// <summary>
            /// Each ladder has its own segment, so three lanes never queue behind one another. The east one stands half
            /// a metre west of its segment's middle, still well within it, to keep the forest out of the climb shot.
            /// </summary>
            private static int LadderLane(int ladderId)
            {
                if (ladderId == SiegeShowcaseScenario.Ladders[0]) return SiegeShowcaseScenario.WestWallX;
                if (ladderId == SiegeShowcaseScenario.Ladders[1]) return SiegeShowcaseScenario.MiddleWallX;
                return SiegeShowcaseScenario.EastWallX - 500;
            }

            /// <summary>Where a unit actually ended up, so a refusal reports a measurement and not a guess.</summary>
            private string Where(int id)
                => world.TryGetUnit(id, out var unit) ? " en " + unit.Position.X + "/" + unit.Position.Z : " (muerto)";

            /// <summary>The surviving, stationary ladder nearest a segment, or zero if none is left.</summary>
            private int NearestLadder(int wallId)
            {
                if (!world.TryGetBuilding(wallId, out var wall)) return 0;
                int best = 0; long nearest = long.MaxValue;
                foreach (int id in SiegeShowcaseScenario.Ladders)
                {
                    if (!world.TryGetUnit(id, out var ladder) || ladder.Order != UnitOrder.Idle) continue;
                    long dx = ladder.Position.X - wall.Position.X, dz = ladder.Position.Z - wall.Position.Z;
                    long distance = dx * dx + dz * dz;
                    if (distance < nearest) { nearest = distance; best = id; }
                }
                return best;
            }

            private bool Idle(List<int> ids)
            {
                foreach (int id in ids)
                    if (world.TryGetUnit(id, out var unit) && unit.Order == UnitOrder.Moving) return false;
                return true;
            }

            private IEnumerator ActBreach()
            {
                Say("El ariete busca la puerta.");
                shot.Cut(At(26000, 30800), 0, 30, 8);
                var rams = Living(SiegeShowcaseScenario.Rams);
                if (gateId != 0)
                    foreach (int id in rams) Foes(new AttackCommand(Foe, new[] { id }, gateId));
                shot.OrbitPerSecond = 2.4f;
                yield return Roll(320, true);
                shot.OrbitPerSecond = 0;
                film.Still(match, Path.Combine(folder, "still-breach.png"));

                Say("Las bestias cargan.");
                var trolls = new List<int>();
                foreach (var unit in world.Units)
                    if (unit.OwnerId == Me && unit.DefinitionId == "war_troll") trolls.Add(unit.Id);
                var target = 0;
                foreach (int id in Living(SiegeShowcaseScenario.Rams)) { target = id; break; }
                if (target == 0) foreach (int id in Living(SiegeShowcaseScenario.Raiders)) { target = id; break; }
                if (trolls.Count > 0 && target != 0)
                {
                    shot.SubjectId = trolls[0]; shot.Framing = .40f; shot.Pitch = 22; shot.Zoom = 6;
                    Mine(new AttackCommand(Me, trolls.ToArray(), target));
                }
                yield return Roll(300, true);
                shot.SubjectId = 0;
            }

            private IEnumerator ActResult()
            {
                Say("Resistencia de Ceniza.");
                yield return Roll(200, true);
                shot.Cut(At(27000, 28500), 0, 44, 19);
                match.Hud.PrepareOffscreenCapture(match.Rig.Camera);
                for (int i = 0; i < 90; i++)
                {
                    match.SyncPresentation(1f); shot.Advance(World.TickSeconds);
                    film.Write(match, world.TickIndex, 1f, "result");
                }
                film.Still(match, Path.Combine(folder, "still-result.png"));
            }
        }

        // ================================================================================ camera

        /// <summary>
        /// A cinematic rig over the shipped camera. Rotation is written straight onto the transform because
        /// RtsCamera.Rotate rewrites the pitch to a fixed 55 degrees, which flattens every shot.
        /// </summary>
        private sealed class Shot
        {
            private readonly MatchController match;
            private Vector3 focus;
            public float Yaw, Pitch = 50, Zoom = 12, OrbitPerSecond, Follow = 2.2f;
            /// <summary>Where the followed subject sits vertically in frame, 0.5 being the middle.</summary>
            public float Framing = .5f;
            public int SubjectId;
            public Vector3 Target { get; set; }

            public Shot(MatchController match) { this.match = match; Target = focus = Vector3.zero; }

            public void Cut(Vector3 at, float yaw, float pitch, float zoom)
            { focus = Target = at; Yaw = yaw; Pitch = pitch; Zoom = zoom; SubjectId = 0; Apply(); }

            public void Advance(double seconds)
            {
                float step = (float)seconds;
                Yaw += OrbitPerSecond * step;
                if (SubjectId != 0)
                {
                    var view = match.View.RootFor(SubjectId);
                    if (view != null) Target = view.position;
                    else SubjectId = 0;
                }
                focus = Vector3.Lerp(focus, Target, 1 - Mathf.Exp(-Follow * step));
                Apply();
            }

            private void Apply()
            {
                var camera = match.Rig.Camera;
                var rotation = Quaternion.Euler(Pitch, Yaw, 0);
                camera.transform.rotation = rotation;
                // Framing below the middle leaves the ground the subject stands on in shot rather than the sky.
                float lift = (Framing - .5f) * Zoom * 2;
                camera.orthographicSize = Zoom;
                camera.transform.position = focus + rotation * new Vector3(0, lift, -Zoom * 2.6f);
            }
        }

        // ================================================================================ recorder

        /// <summary>
        /// Writes the frames and the index the Blender encoder reads. Deliberately the same contract as the
        /// full-match recorder: frame-%06d.jpg plus a frames.csv of index, tick, alpha and mode.
        /// </summary>
        private sealed class Film
        {
            private readonly string folder;
            private readonly StreamWriter index;
            private RenderTexture target;
            private Texture2D pixels;
            public int Count { get; private set; }
            public int Battle { get; private set; }

            public Film(string folder)
            {
                this.folder = folder;
                index = new StreamWriter(Path.Combine(folder, "frames.csv"), false, new UTF8Encoding(false));
                index.WriteLine("index,tick,alpha,mode");
            }

            private void Ensure()
            {
                if (target != null) return;
                int width = Mathf.Max(640, Screen.width), height = Mathf.Max(360, Screen.height);
                target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { name = "Showcase" };
                pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            }

            public void Write(MatchController match, long tick, float alpha, string mode)
            {
                Ensure();
                Capture(match);
                File.WriteAllBytes(Path.Combine(folder, "frame-" + Count.ToString("D6", CultureInfo.InvariantCulture) + ".jpg"), pixels.EncodeToJPG(88));
                index.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3}", Count, tick, alpha, mode));
                Count++;
                if (mode == "battle") Battle++;
            }

            public void Still(MatchController match, string path)
            {
                Ensure();
                Capture(match);
                File.WriteAllBytes(path, pixels.EncodeToPNG());
            }

            private void Capture(MatchController match)
            {
                var camera = match.Rig.Camera;
                var previous = camera.targetTexture;
                camera.targetTexture = target;
                camera.Render();
                var active = RenderTexture.active;
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                pixels.Apply(false);
                RenderTexture.active = active;
                camera.targetTexture = previous;
            }

            public void Close()
            {
                index.Flush(); index.Dispose();
                if (target != null) { target.Release(); UnityEngine.Object.Destroy(target); }
                if (pixels != null) UnityEngine.Object.Destroy(pixels);
            }
        }
    }
}
