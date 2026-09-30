using System;
using System.Collections.Generic;
using Emberfield.Simulation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Emberfield.Presentation
{
    /// <summary>An explicitly requested playable review. Normal skirmish starts remain unchanged.</summary>
    public static class CorsairHeroScenario
    {
        private static bool requested, commandLineConsumed;
        private static World pendingPresentation;
        public const string HeroId = "crimson_corsair";

        public static void StartReviewMatch()
        {
            requested = true;
            SceneManager.LoadScene("Greybox");
        }

        public static bool TryCreateWorld(out World world)
        {
            bool commandLine = !commandLineConsumed && Array.IndexOf(Environment.GetCommandLineArgs(), "-emberfieldCorsairHero") >= 0;
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
            // Each loader call returns fresh JSON definitions. The shipped coast asset and
            // ordinary starting roster are not modified by this one-hero review setup.
            var baseline = DefinitionLoader.CreateOfflineWorld("pirates", VictoryMode.Conquest, "sapphire_coast");
            var map = baseline.Map;
            var spawns = new List<UnitSpawnDefinition>(map.UnitSpawns);
            // The review hero costs three population. Keep the first three local
            // workers so the demonstration starts at 6/6, preserving its origin and
            // friendly vision sources. This edits only the fresh review spawn list;
            // the smoke enemy is added afterwards against the remaining witnesses.
            int localWorkers = 0;
            spawns.RemoveAll(spawn => spawn.OwnerId == MatchController.LocalPlayer &&
                baseline.TryGetUnit(spawn.Id, out var unit) && unit.IsWorker && ++localWorkers > 3);
            int nextId = 1;
            foreach (var unit in map.UnitSpawns) nextId = Math.Max(nextId, unit.Id + 1);
            foreach (var building in map.BuildingSpawns) nextId = Math.Max(nextId, building.Id + 1);
            foreach (var resource in map.ResourceSpawns) nextId = Math.Max(nextId, resource.Id + 1);
            SimPoint origin = default;
            foreach (var unit in baseline.Units)
                if (unit.OwnerId == MatchController.LocalPlayer) { origin = unit.Position; break; }
            for (int radius = 2; radius <= 12; radius++)
            {
                for (int z = -radius; z <= radius; z++)
                for (int x = -radius; x <= radius; x++)
                {
                    if (Math.Abs(x) != radius && Math.Abs(z) != radius) continue;
                    var point = new SimPoint(origin.X + x * 1000, origin.Z + z * 1000);
                    if (!Free(baseline, point)) continue;
                    var heroSpawn = new UnitSpawnDefinition { Id = nextId, OwnerId = MatchController.LocalPlayer, DefinitionId = HeroId, Position = point };
                    spawns.Add(heroSpawn);
                    CorsairGameSmoke.AddFixtureSpawns(baseline, spawns, heroSpawn);
                    map.UnitSpawns = spawns.ToArray();
                    return new World(baseline.Definition, map);
                }
            }
            throw new InvalidOperationException("No free coastal spawn was available for the pirate review.");
        }

        private static bool Free(World world, SimPoint point)
        {
            foreach (int x in new[] { -350, 0, 350 }) foreach (int z in new[] { -350, 0, 350 })
                if (!world.IsWalkable(new SimPoint(point.X + x, point.Z + z))) return false;
            foreach (var unit in world.Units)
            {
                long dx = point.X - unit.Position.X, dz = point.Z - unit.Position.Z;
                long radius = unit.RadiusMillimetres + 400;
                if (dx * dx + dz * dz < radius * radius) return false;
            }
            return true;
        }

        public static void PreparePresentation(MatchController match)
        {
            if (match == null || !ReferenceEquals(pendingPresentation, match.World)) return;
            pendingPresentation = null;
            match.Shell?.Close();
            foreach (var unit in match.World.Units)
            {
                if (unit.OwnerId != MatchController.LocalPlayer || unit.DefinitionId != HeroId) continue;
                match.Select(new[] { unit.Id });
                match.Rig.SetHome(DefinitionLoader.ToWorld(unit.Position), 6);
                match.SetFeedback("Corsario Carmesí: selecciona terreno para moverte o un enemigo para atacar. Héroe de los Piratas; reclutable en su Hearth en Navales.");
                break;
            }
        }
    }
}
