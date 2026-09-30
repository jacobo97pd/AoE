using System;
using Emberfield.Simulation;

namespace Emberfield.Presentation
{
    public enum ChallengeGoal { Stock, Train, Build, Era, Defeat }
    public enum ChallengeState { Running, Won, Lost }

    /// <summary>
    /// One short exercise: a map, a single thing to achieve and a clock. The design has always read
    /// tutorial, then exercises, then the AI, then other people; the exercises were the missing rung.
    /// Each one is set on a battlefield the game already ships, so a challenge costs content, not maps.
    /// </summary>
    public sealed class ChallengeDefinition
    {
        public string Id, MapPath, Name, Brief;
        public ChallengeGoal Goal;
        public ResourceKind Resource;
        public string DefinitionId;
        public int Target, Seconds;
    }

    public static class ChallengeCatalog
    {
        // Ordered as a course: gather, then the seam most players never find, then build, muster and fight.
        public static readonly ChallengeDefinition[] All =
        {
            new ChallengeDefinition
            {
                Id = "first_harvest", MapPath = "Maps/amber_reach", Goal = ChallengeGoal.Stock, Resource = ResourceKind.Food,
                Target = 400, Seconds = 180, Name = "First harvest",
                Brief = "Hold 400 food at once. Resources only count once a Tender carries them back to a drop-off."
            },
            new ChallengeDefinition
            {
                Id = "the_metal_seam", MapPath = "Maps/amber_reach", Goal = ChallengeGoal.Stock, Resource = ResourceKind.Metal,
                Target = 150, Seconds = 300, Name = "The metal seam",
                Brief = "Metal lies further out than food and wood. Scout for a seam, then keep Tenders on it and a Storeyard near it."
            },
            new ChallengeDefinition
            {
                Id = "raise_a_hall", MapPath = "Maps/amber_reach", Goal = ChallengeGoal.Build, DefinitionId = "muster_hall",
                Target = 1, Seconds = 300, Name = "Raise a hall",
                Brief = "Gather the wood for a Muster Hall and finish it. More Tenders on the frame raise it faster."
            },
            new ChallengeDefinition
            {
                Id = "muster_six", MapPath = "Maps/amber_reach", Goal = ChallengeGoal.Train, Target = 6, Seconds = 420,
                Name = "Muster six",
                Brief = "Field six military units at once. Houses raise the population limit; the Muster Hall trains the army."
            },
            new ChallengeDefinition
            {
                Id = "the_counter_triangle", MapPath = "Maps/combat_sandbox", Goal = ChallengeGoal.Defeat, Target = 1, Seconds = 240,
                Name = "The counter triangle",
                Brief = "Defeat the red army. Spears beat cavalry, cavalry beats archers, archers beat spears. Send the right unit."
            },
        };

        public static ChallengeDefinition Find(string id)
        {
            if (id == null) return null;
            foreach (var item in All) if (item.Id == id) return item;
            return null;
        }
    }

    /// <summary>Watches a running challenge. It reads the world and never changes it.</summary>
    public sealed class ChallengeRun
    {
        private static ChallengeDefinition pending;
        private readonly MatchController match;
        private readonly long startTick;
        private int frozenSeconds = -1;
        private int enemiesLeft;
        private float finishedAt = -1;
        private bool presented;
        public ChallengeDefinition Definition { get; }
        public ChallengeState State { get; private set; } = ChallengeState.Running;
        public int Progress { get; private set; }
        public bool IsFinished => State != ChallengeState.Running;

        /// <summary>Chosen from the menu, consumed by the scene that builds the world to play it in.</summary>
        public static void Queue(string id) { pending = ChallengeCatalog.Find(id); }
        /// <summary>Queues an exercise that is not in the shipped course.</summary>
        public static void Queue(ChallengeDefinition definition) { pending = definition; }
        public static bool HasPending => pending != null;
        /// <summary>The identifier and result of the last challenge played, for the menu to report.</summary>
        public static string LastPlayedId { get; private set; }
        public static ChallengeState LastResult { get; private set; }

        public static bool TryCreateWorld(out World world)
        {
            world = null;
            if (pending == null) return false;
            world = DefinitionLoader.CreateWorld(pending.MapPath);
            return true;
        }

        public static ChallengeRun TryBegin(MatchController match)
        {
            var definition = pending;
            if (definition == null) return null;
            pending = null;
            LastPlayedId = definition.Id; LastResult = ChallengeState.Running;
            return new ChallengeRun(match, definition);
        }

        private ChallengeRun(MatchController match, ChallengeDefinition definition)
        { this.match = match; Definition = definition; startTick = match.World.TickIndex; }

        public int RemainingSeconds
        {
            get
            {
                // A finished exercise keeps the clock it finished on; watching it run down afterwards
                // tells the player nothing and makes a win look like it is still slipping away.
                if (frozenSeconds >= 0) return frozenSeconds;
                long spent = match.World.TickIndex - startTick;
                return Math.Max(0, Definition.Seconds - (int)(spent / World.TickRate));
            }
        }

        public void Observe()
        {
            if (IsFinished) return;
            Progress = Measure();
            if (Progress >= Definition.Target) { Finish(ChallengeState.Won); return; }
            if (RemainingSeconds <= 0) { Finish(ChallengeState.Lost); return; }
            // Losing everything you could still act with ends it now rather than making you wait out the clock.
            if (!AnythingLeft()) Finish(ChallengeState.Lost);
        }

        /// <summary>True once the result has been on screen long enough to read, and only once.</summary>
        public bool ShouldPresentResult => IsFinished && !presented && finishedAt >= 0 && UnityEngine.Time.unscaledTime - finishedAt > 2.5f;
        public void MarkPresented() { presented = true; }

        private void Finish(ChallengeState state)
        {
            frozenSeconds = RemainingSeconds;
            State = state; LastResult = state; finishedAt = UnityEngine.Time.unscaledTime;
            if (state == ChallengeState.Won) Record(match, Definition.Id);
            match.SetFeedback(state == ChallengeState.Won ? "Challenge complete." : "Challenge failed. Try it again.");
            match.Hud?.Invalidate();
        }

        /// <summary>A passed exercise is kept in the settings file beside the guide's own progress.</summary>
        private static void Record(MatchController match, string id)
        {
            var settings = match.Alpha?.Settings;
            if (settings == null || settings.Value.ChallengesPassed.Contains(id)) return;
            settings.Value.ChallengesPassed.Add(id);
            settings.Save();
        }

        /// <summary>Has this exercise been passed before, in this session or an earlier one?</summary>
        public static bool WasPassed(MatchController match, string id)
            => match?.Alpha != null && match.Alpha.Settings.Value.ChallengesPassed.Contains(id);

        public static int PassedCount(MatchController match)
        {
            if (match?.Alpha == null) return 0;
            int passed = 0;
            foreach (var challenge in ChallengeCatalog.All) if (match.Alpha.Settings.Value.ChallengesPassed.Contains(challenge.Id)) passed++;
            return passed;
        }

        private bool AnythingLeft()
        {
            foreach (var unit in match.World.Units) if (unit.OwnerId == MatchController.LocalPlayer) return true;
            foreach (var building in match.World.Buildings) if (building.OwnerId == MatchController.LocalPlayer) return true;
            return false;
        }

        private int Measure()
        {
            var world = match.World;
            switch (Definition.Goal)
            {
                case ChallengeGoal.Stock:
                    return world.TryGetPlayer(MatchController.LocalPlayer, out var stocked) ? stocked.Resources.Get(Definition.Resource) : 0;
                case ChallengeGoal.Era:
                    return world.TryGetPlayer(MatchController.LocalPlayer, out var advanced) ? advanced.EraTier : 1;
                case ChallengeGoal.Train:
                {
                    int army = 0;
                    foreach (var unit in world.Units)
                        if (unit.OwnerId == MatchController.LocalPlayer && !unit.IsWorker && unit.AttackDamage > 0) army++;
                    return army;
                }
                case ChallengeGoal.Build:
                {
                    int built = 0;
                    foreach (var building in world.Buildings)
                        if (building.OwnerId == MatchController.LocalPlayer && building.IsComplete && building.DefinitionId == Definition.DefinitionId) built++;
                    return built;
                }
                case ChallengeGoal.Defeat:
                {
                    // The army, not the household. The sandbox parks an enemy Tender well behind the line, and
                    // asking the player to hunt a lone villager with no minimap is not the lesson this teaches.
                    enemiesLeft = 0;
                    foreach (var unit in world.Units)
                        if (unit.OwnerId != MatchController.LocalPlayer && !unit.IsWorker && unit.AttackDamage > 0) enemiesLeft++;
                    return enemiesLeft == 0 ? 1 : 0;
                }
                default: return 0;
            }
        }

        public string Clock()
        {
            int seconds = RemainingSeconds;
            return seconds / 60 + ":" + (seconds % 60).ToString("00");
        }

        /// <summary>How far along the one thing being asked for is.</summary>
        public string Counter()
        {
            switch (Definition.Goal)
            {
                case ChallengeGoal.Stock: return Math.Min(Progress, Definition.Target) + " / " + Definition.Target;
                case ChallengeGoal.Train: return Math.Min(Progress, Definition.Target) + " / " + Definition.Target;
                case ChallengeGoal.Build: return Progress > 0 ? "Built" : "Not built yet";
                case ChallengeGoal.Defeat: return Progress > 0 ? "Field cleared" : enemiesLeft + " enemies left";
                default: return Progress + " / " + Definition.Target;
            }
        }
    }

    public sealed partial class MatchController
    {
        public ChallengeRun Challenge { get; private set; }
        public bool IsChallenge => Challenge != null;
        internal void BeginChallenge() { Challenge = ChallengeRun.TryBegin(this); }

        /// <summary>
        /// Queues the running exercise to be played again, and reports whether there was one. Restart used to
        /// fall through to a bare scene reload, which abandoned the exercise silently and, for one set on
        /// another map, changed the battlefield underneath the player.
        /// </summary>
        public bool QueueChallengeRestart()
        {
            if (Challenge == null) return false;
            ChallengeRun.Queue(Challenge.Definition);
            return true;
        }
    }
}
