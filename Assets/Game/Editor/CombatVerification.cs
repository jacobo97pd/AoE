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
    public static class CombatVerification
    {
        [MenuItem("Emberfield/Verify shipped combat balance")]
        public static void Run()
        {
            var definitions = DefinitionLoader.CreateWorld().Definition;
            var report = new StringBuilder("# Shipped combat counter checks\n\n");
            report.AppendLine("Unity: " + Application.unityVersion);
            using (var hash = SHA256.Create()) report.AppendLine("\nRules SHA256: `" + BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes("Assets/Game/Resources/Definitions/greybox.json"))).Replace("-", "") + "`");
            report.AppendLine("\nOpen terrain; 7 m between front units; both sides receive attack orders at tick 0. Each of the three common military roles used here costs 80 total resources, uses one population and trains in 160 ticks. Resource types retain different economic value, so this is a nominal cost comparison, not proof of competitive balance. Every scenario swaps player/spawn sides. Two units of the disadvantaged type must overcome one favored unit. Ticks advance directly; no performance measurement. Projectiles finish before the result is recorded.\n");
            report.AppendLine("| Blue | Red | Expected winner | Actual winner | Seconds | Survivors / health | Pass |\n| --- | --- | --- | --- | --- | --- | --- |");
            bool passed = true;
            var favored = new[] { "reedguard", "stringwarden", "strider" };
            var opposed = new[] { "strider", "reedguard", "stringwarden" };
            for (int pair = 0; pair < 3; pair++)
            for (int numbers = 1; numbers <= 2; numbers++)
            for (int swap = 0; swap < 2; swap++)
            {
                string left = swap == 0 ? favored[pair] : opposed[pair];
                string right = swap == 0 ? opposed[pair] : favored[pair];
                int leftCount = swap == 0 ? 1 : numbers, rightCount = swap == 0 ? numbers : 1;
                int expected = numbers == 1 ? (swap == 0 ? 1 : 2) : (swap == 0 ? 2 : 1);
                var units = new List<UnitSpawnDefinition>();
                var leftIds = new int[leftCount]; var rightIds = new int[rightCount];
                for (int i = 0; i < leftCount; i++) { leftIds[i] = i + 1; units.Add(Spawn(i + 1, 1, left, 28500, 23500 + i * 1000)); }
                for (int i = 0; i < rightCount; i++) { rightIds[i] = i + 10; units.Add(Spawn(i + 10, 2, right, 35500, 23500 + i * 1000)); }
                var map = new MapDefinition { Id = "counter_probe", UnitSpawns = units.ToArray() };
                var world = new World(definitions, map);
                bool accepted = world.Submit(new AttackCommand(1, leftIds, rightIds[0])).Accepted && world.Submit(new AttackCommand(2, rightIds, leftIds[0])).Accepted;
                int blue = leftCount, red = rightCount;
                while (world.TickIndex < World.TickRate * 90 && ((blue > 0 && red > 0) || world.Projectiles.Count > 0))
                {
                    world.Tick(); blue = red = 0;
                    foreach (var unit in world.Units) { if (unit.OwnerId == 1) blue++; else red++; }
                }
                int winner = blue > 0 && red == 0 ? 1 : red > 0 && blue == 0 ? 2 : 0;
                int remainingHealth = 0;
                foreach (var unit in world.Units) remainingHealth += unit.Health;
                bool result = accepted && winner == expected;
                passed &= result;
                report.AppendLine($"| {leftCount} {left} | {rightCount} {right} | {expected} | {winner} | {(world.TickIndex / (float)World.TickRate).ToString("0.00", CultureInfo.InvariantCulture)} | {world.Units.Count} / {remainingHealth} | {result} |");
            }
            report.AppendLine("\nPassed: " + passed);
            Directory.CreateDirectory("TestResults");
            File.WriteAllText("TestResults/combat-balance.md", report.ToString());
            if (!passed) throw new InvalidOperationException("A shipped combat counter scenario failed. Inspect TestResults/combat-balance.md.");
            Debug.Log("EMBERFIELD_COMBAT_BALANCE_OK 12 scenarios");
        }

        private static UnitSpawnDefinition Spawn(int id, int owner, string definition, int x, int z) =>
            new UnitSpawnDefinition { Id = id, OwnerId = owner, DefinitionId = definition, Position = new SimPoint(x, z) };
    }
}
