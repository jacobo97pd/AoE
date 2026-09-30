using System;
using System.Collections.Generic;
using Emberfield.Simulation;
using UnityEngine;

namespace Emberfield.Presentation
{
    /// <summary>
    /// An authored fixture for the siege showcase, opted into with -emberfieldSiegeShowcase. Ordinary play never
    /// touches it. It is set on amber_crossing because the world art is gated on a map-id whitelist, and played by
    /// Ashen against Drakeforged because the war troll asks for the Ashen faction and the fantasy realm.
    ///
    /// The economy is granted rather than gathered: a fortress costs about 960 food, 850 wood, 300 metal and 840
    /// stone, and filming twenty minutes of Tenders walking is not the subject. Every wall, gate, tower and keep in
    /// the recording is still placed and raised live by ordinary build commands through the ordinary construction
    /// system. Nothing here says anything about economic pacing, and the report says so.
    /// </summary>
    public static class SiegeShowcaseScenario
    {
        public const string Flag = "-emberfieldSiegeShowcase";
        public const string FixtureVersion = "amber-crossing-ashen-conquest-siege-showcase-v1";
        public static bool IsActive { get; private set; }

        // Authored identifiers. The shipped map's highest id is 272, so 300 and up are free.
        public const int Hearth = 100, MusterHall = 300, Archive = 301, BeastLodge = 302, Storeyard = 303;
        public static readonly int[] Crew = { 1, 2, 3, 4, 5, 6, 7, 8 };
        public static readonly int[] Guards = { 410, 411, 412, 413 };
        public static readonly int[] Archers = { 414, 415 };
        public static readonly int[] Trolls = { 420, 421, 422 };
        public static readonly int[] Raiders = { 500, 501, 502, 503, 504, 505, 506, 507 };
        public static readonly int[] Ladders = { 510, 511, 512 };
        public static readonly int[] Rams = { 520, 521 };
        public const int Drake = 530;

        // Row z=30 is clear of terrain and resource nodes from x=15 to x=38; x=39 is a metal seam and x=14 is cliff.
        // A wall is 3x1 and a gate 2x1, so their centres sit on different half-cells. The forest at x28-35, z32-35
        // closes the eastern approach by itself, which funnels the assault onto the gate without a sealed ring.
        public static readonly (string Id, int X, int Z)[] KingdomWorks =
        {
            ("wall", 17500, 30500),
            ("wall", 20500, 30500),
            ("wall", 23500, 30500),
            ("wall", 28500, 30500),
            ("wall", 31500, 30500),
            ("wall", 34500, 30500),
            ("wall", 37500, 30500),
            ("watchtower", 19000, 29000),
            ("watchtower", 38000, 29000),
        };
        public static readonly (string Id, int X, int Z) KeepSite = ("keep", 33000, 27000);
        public static readonly (string Id, int X, int Z) GateSite = ("gate", 26000, 30500);
        public const int WestWallX = 20500, MiddleWallX = 23500, EastWallX = 28500;

        public static bool TryCreateWorld(out World world)
        {
            world = null;
            if (!Debug.isDebugBuild && !Application.isEditor) return false;
            if (Array.IndexOf(Environment.GetCommandLineArgs(), Flag) < 0) return false;
            world = Create();
            IsActive = true;
            return true;
        }

        public static World Create()
        {
            // The realm, the faction assignment and the allowed-map check all come from the shipped loader.
            var baseline = DefinitionLoader.CreateOfflineWorld("ashen", VictoryMode.Conquest, "amber_crossing");
            var rules = baseline.Definition;
            var map = baseline.Map;

            rules.StartingResources = new ResourceAmount(2000, 1500, 600, 1400);
            rules.BasePopulationCapacity = 40;

            var buildings = new List<BuildingSpawnDefinition>(map.BuildingSpawns)
            {
                Building(MusterHall, 1, "muster_hall", 21500, 23500),
                Building(Archive, 1, "archive", 17500, 26500),
                Building(BeastLodge, 1, "beast_lodge", 18000, 22500),
                Building(Storeyard, 1, "storeyard", 23000, 27000),
                Building(304, 1, "shelter", 21000, 20000),
                Building(305, 1, "shelter", 24000, 20000),
                Building(306, 1, "shelter", 27000, 20000),
                Building(307, 1, "shelter", 33000, 20000),
            };
            map.BuildingSpawns = buildings.ToArray();

            var units = new List<UnitSpawnDefinition>();
            for (int i = 0; i < Crew.Length; i++) units.Add(Unit(Crew[i], 1, "tender", 22500 + i * 1000, 28500));
            // The garrison starts beside the segments it will climb, at the inside face. Walking them there at
            // recording time failed: the lane behind the wall is narrow and whoever stands in it blocks the rest.
            units.Add(Unit(Guards[0], 1, "reedguard", 27600, 29300));
            units.Add(Unit(Guards[1], 1, "reedguard", 29400, 29300));
            units.Add(Unit(Guards[2], 1, "reedguard", 26600, 29300));
            units.Add(Unit(Guards[3], 1, "reedguard", 30400, 29300));
            units.Add(Unit(Archers[0], 1, "stringwarden", 23500, 29500));
            units.Add(Unit(Archers[1], 1, "stringwarden", 24500, 29500));
            units.Add(Unit(Trolls[0], 1, "war_troll", 25500, 26500));
            units.Add(Unit(Trolls[1], 1, "war_troll", 27000, 26500));
            units.Add(Unit(Trolls[2], 1, "war_troll", 28500, 26500));
            // The rival keeps a household at its own hearth so Conquest has something to be about.
            for (int i = 0; i < 4; i++) units.Add(Unit(11 + i, 2, "tender", 119500 - i * 2000, 83500));
            for (int i = 0; i < Raiders.Length; i++) units.Add(Unit(Raiders[i], 2, "reedguard", 16500 + i * 1000, 42500));
            units.Add(Unit(Ladders[0], 2, "siege_ladder", 18000, 40500));
            units.Add(Unit(Ladders[1], 2, "siege_ladder", 21000, 40500));
            units.Add(Unit(Ladders[2], 2, "siege_ladder", 24000, 40500));
            units.Add(Unit(Rams[0], 2, "siege_ram", 27000, 40500));
            units.Add(Unit(Rams[1], 2, "siege_ram", 29500, 40500));
            units.Add(Unit(Drake, 2, "ember_drake", 32000, 40500));
            map.UnitSpawns = units.ToArray();

            return new World(rules, map);
        }

        private static BuildingSpawnDefinition Building(int id, int owner, string definitionId, int x, int z)
            => new BuildingSpawnDefinition { Id = id, OwnerId = owner, DefinitionId = definitionId, Position = new SimPoint(x, z) };
        private static UnitSpawnDefinition Unit(int id, int owner, string definitionId, int x, int z)
            => new UnitSpawnDefinition { Id = id, OwnerId = owner, DefinitionId = definitionId, Position = new SimPoint(x, z) };
    }
}
