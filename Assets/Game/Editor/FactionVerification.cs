using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Emberfield.Presentation;
using Emberfield.Simulation;
using UnityEditor;
using UnityEngine;

namespace Emberfield.Editor
{
    public static class FactionVerification
    {
        [MenuItem("Emberfield/Verify shipped faction counters")]
        public static void Run()
        {
            var definitions = DefinitionLoader.CreateWorld().Definition;
            var report = new StringBuilder("# Shipped faction counter checks\n\n");
            report.AppendLine("Unity: " + Application.unityVersion);
            using (var hash = SHA256.Create())
                report.AppendLine("\nRules SHA256: `" + BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(
                    Path.Combine(Application.dataPath, "Game/Resources/Definitions/greybox.json")))).Replace("-", "") + "`");
            report.AppendLine("\nUnmodified shipped unit definitions and explicit Aven/Serevin player assignments. Faction passives apply; no research, charter, relay or Reposition is activated. Authored spawns bypass resource payment so the displayed costs describe nominal investment, not a simulated build order. The Ashrunner costs more than either common unit in these checks; this is a counter-behavior regression, not proof of competitive faction balance.");
            report.AppendLine("\nOpen 64 by 48 cell terrain; 1 m cells; front positions (28.5, 23.5) m and (35.5, 23.5) m, 7 m apart. A second unit starts 1 m along the Z axis. Both sides receive explicit attack orders against the opposing front unit before tick 0. Each case swaps player IDs, factions and spawn sides. The favored type should win one-on-one; two disadvantaged units should overcome one favored unit. The deadline is 90 simulation seconds, including any remaining projectile flight after a side is eliminated. A result with projectiles still in flight fails. Ticks advance directly; no performance measurement.\n");
            report.AppendLine("| Unit | Faction | Food | Wood | Metal | Stone | Nominal total | Population | Train ticks |\n| --- | --- | --- | --- | --- | --- | --- | --- | --- |");
            foreach (string id in new[] { "reedguard", "stringwarden", "ashrunner" })
            {
                var unit = FindUnit(definitions, id);
                var cost = unit.Cost;
                long total = (long)cost.Food + cost.Wood + cost.Metal + cost.Stone;
                report.AppendLine($"| {id} | {FactionFor(id)} | {cost.Food} | {cost.Wood} | {cost.Metal} | {cost.Stone} | {total} | {unit.PopulationCost} | {unit.TrainTicks} |");
            }
            report.AppendLine("\n| Case | Player 1 / faction | Player 2 / faction | Expected winner | Actual winner | Orders accepted (P1 / P2) | Seconds | Survivors / health (P1; P2) | Projectiles left | Pass |\n| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |");
            bool passed = true;
            int cases = 0;
            var rejections = new StringBuilder();
            var favored = new[] { "reedguard", "ashrunner" };
            var opposed = new[] { "ashrunner", "stringwarden" };
            for (int pair = 0; pair < favored.Length; pair++)
            for (int numbers = 1; numbers <= 2; numbers++)
            for (int swap = 0; swap < 2; swap++)
            {
                string left = swap == 0 ? favored[pair] : opposed[pair];
                string right = swap == 0 ? opposed[pair] : favored[pair];
                int leftCount = swap == 0 ? 1 : numbers;
                int rightCount = swap == 0 ? numbers : 1;
                int expected = numbers == 1 ? (swap == 0 ? 1 : 2) : (swap == 0 ? 2 : 1);
                var spawns = new List<UnitSpawnDefinition>();
                var leftIds = new int[leftCount];
                var rightIds = new int[rightCount];
                for (int index = 0; index < leftCount; index++)
                {
                    leftIds[index] = index + 1;
                    spawns.Add(Spawn(leftIds[index], 1, left, 28500, 23500 + index * 1000));
                }
                for (int index = 0; index < rightCount; index++)
                {
                    rightIds[index] = index + 10;
                    spawns.Add(Spawn(rightIds[index], 2, right, 35500, 23500 + index * 1000));
                }
                var map = new MapDefinition
                {
                    Id = "faction_counter_probe", WidthCells = 64, HeightCells = 48, CellSizeMillimetres = 1000,
                    UnitSpawns = spawns.ToArray(),
                    PlayerFactions = new[]
                    {
                        new PlayerFactionDefinition { PlayerId = 1, FactionId = FactionFor(left) },
                        new PlayerFactionDefinition { PlayerId = 2, FactionId = FactionFor(right) }
                    }
                };
                var world = new World(definitions, map);
                var leftOrder = world.Submit(new AttackCommand(1, leftIds, rightIds[0]));
                var rightOrder = world.Submit(new AttackCommand(2, rightIds, leftIds[0]));
                int blue = leftCount, red = rightCount;
                while (world.TickIndex < World.TickRate * 90 && ((blue > 0 && red > 0) || world.Projectiles.Count > 0))
                {
                    world.Tick(); blue = red = 0;
                    foreach (var unit in world.Units) { if (unit.OwnerId == 1) blue++; else if (unit.OwnerId == 2) red++; }
                }
                int winner = blue > 0 && red == 0 ? 1 : red > 0 && blue == 0 ? 2 : 0;
                int blueHealth = 0, redHealth = 0;
                foreach (var unit in world.Units) { if (unit.OwnerId == 1) blueHealth += unit.Health; else if (unit.OwnerId == 2) redHealth += unit.Health; }
                bool result = leftOrder.Accepted && rightOrder.Accepted && winner == expected && world.Projectiles.Count == 0;
                passed &= result;
                cases++;
                string seconds = (world.TickIndex / (double)World.TickRate).ToString("0.00", CultureInfo.InvariantCulture);
                report.AppendLine($"| {cases} | {leftCount} {left} / {FactionFor(left)} | {rightCount} {right} / {FactionFor(right)} | {expected} | {winner} | {leftOrder.Accepted} / {rightOrder.Accepted} | {seconds} | {blue} / {blueHealth}; {red} / {redHealth} | {world.Projectiles.Count} | {result} |");
                if (!leftOrder.Accepted || !rightOrder.Accepted)
                    rejections.AppendLine($"Case {cases} command rejection: P1 `{leftOrder.Reason}`; P2 `{rightOrder.Reason}`.\n");
            }
            if (rejections.Length > 0) report.AppendLine("\n" + rejections);
            report.AppendLine("\nWinner 0 means neither side has an exclusive surviving force (mutual elimination or deadline reached with both sides alive).");
            report.AppendLine("\nCases: " + cases);
            report.AppendLine("Passed: " + passed);
            Directory.CreateDirectory("TestResults");
            File.WriteAllText("TestResults/faction-counters.md", report.ToString());
            if (!passed) throw new InvalidOperationException("A shipped faction counter scenario failed. Inspect TestResults/faction-counters.md; do not infer balance from the expected outcome.");
            Debug.Log("EMBERFIELD_FACTION_COUNTERS_OK 8 scenarios");
        }

        private static string FactionFor(string unitId) => unitId == "ashrunner" ? "serevin" : "aven";

        private static UnitDefinition FindUnit(GameDefinition definitions, string id)
        {
            foreach (var unit in definitions.Units) if (unit.Id == id) return unit;
            throw new InvalidOperationException("Shipped faction probe unit is missing: " + id);
        }

        private static UnitSpawnDefinition Spawn(int id, int owner, string definition, int x, int z) =>
            new UnitSpawnDefinition { Id = id, OwnerId = owner, DefinitionId = definition, Position = new SimPoint(x, z) };
    }
}
