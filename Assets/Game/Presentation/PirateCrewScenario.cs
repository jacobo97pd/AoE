using System;
using System.Collections.Generic;
using Emberfield.Simulation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Emberfield.Presentation
{
    /// <summary>Opt-in coastal review using real unit definitions, commands and economy.</summary>
    public static class PirateCrewScenario
    {
        private static bool requested, commandLineConsumed;
        private static World pendingPresentation;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterReview() => CrimsonCorsairReview.StartCrewMatch = StartReviewMatch;

        public static void StartReviewMatch()
        {
            requested = true;
            SceneManager.LoadScene("Greybox");
        }

        public static bool TryCreateWorld(out World world)
        {
            bool commandLine = !commandLineConsumed && Array.IndexOf(Environment.GetCommandLineArgs(), "-emberfieldPirateCrew") >= 0;
            commandLineConsumed = true;
            world = null;
            if (!requested && !commandLine) return false;
            requested = false;
            world = CreateReviewWorld();
            pendingPresentation = world;
            return true;
        }

        public static World CreateReviewWorld()
        {
            var baseline = CorsairHeroScenario.CreateReviewWorld();
            // Replace the three review workers with this crew, including its worker.
            // Captain (3) + three crew (1 each) stays at the Hearth's normal 6/6.
            // Both loaders create fresh definitions; ordinary map starts are untouched.
            var crew = new[] { ImportedCharacterVisuals.BoardingRaiderId, ImportedCharacterVisuals.GunpowderCorsairId, ImportedCharacterVisuals.TreasureSeekerId };
            var spawns = new List<UnitSpawnDefinition>();
            int replaced = 0;
            foreach (var spawn in baseline.Map.UnitSpawns)
            {
                string definitionId = spawn.DefinitionId;
                if (replaced < crew.Length && spawn.OwnerId == MatchController.LocalPlayer && baseline.TryGetUnit(spawn.Id, out var unit) && unit.IsWorker)
                    definitionId = crew[replaced++];
                spawns.Add(new UnitSpawnDefinition { Id = spawn.Id, OwnerId = spawn.OwnerId, DefinitionId = definitionId, Position = spawn.Position });
            }
            if (replaced != crew.Length) throw new InvalidOperationException("Coastal crew review requires three available worker spawns.");
            PirateCrewGameSmoke.AddFixtureSpawns(baseline, spawns);
            baseline.Map.UnitSpawns = spawns.ToArray();
            return new World(baseline.Definition, baseline.Map);
        }

        public static void PreparePresentation(MatchController match)
        {
            if (match == null || !ReferenceEquals(pendingPresentation, match.World)) return;
            pendingPresentation = null;
            match.Shell?.Close();
            var ids = new List<int>(); Vector3 center = Vector3.zero;
            foreach (var unit in match.World.Units)
                if (unit.OwnerId == MatchController.LocalPlayer && ImportedCharacterVisuals.CanUse(match.World, unit))
                { ids.Add(unit.Id); center += DefinitionLoader.ToWorld(unit.Position); }
            match.Select(ids);
            if (ids.Count > 0) match.Rig.SetHome(center / ids.Count, 7);
            match.SetFeedback("Tripulación pirata: sable, pistola y exploración. La buscadora recolecta y construye; recluta más en el Hearth y el Muster Hall de los Piratas, en Navales.");
        }
    }
}
