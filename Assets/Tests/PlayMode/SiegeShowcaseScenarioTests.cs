using System.Collections.Generic;
using Emberfield.Presentation;
using Emberfield.Simulation;
using NUnit.Framework;

namespace Emberfield.Tests.PlayMode
{
    /// <summary>
    /// The showcase is filmed once and watched many times, so its fixture is checked here rather than discovered
    /// halfway through a recording. Every authored site must be somewhere a building can actually be raised.
    /// </summary>
    public sealed class SiegeShowcaseScenarioTests
    {
        [Test]
        public void TheFixtureBuildsWithEveryAuthoredPieceOnTheBoard()
        {
            var world = SiegeShowcaseScenario.Create();
            Assert.IsNotNull(world.Match, "Conquest gives the recording a real result to end on.");
            Assert.AreEqual("amber_crossing", world.Map.Id, "The world art is gated on a map-id whitelist.");
            Assert.AreEqual("fantasy", world.Map.RealmId, "The war troll asks for the Ashen faction and its realm.");

            foreach (int id in SiegeShowcaseScenario.Crew)
                Assert.IsTrue(world.TryGetUnit(id, out var crew) && crew.IsWorker, "Builder " + id);
            foreach (int id in SiegeShowcaseScenario.Trolls)
                Assert.IsTrue(world.TryGetUnit(id, out var troll) && troll.DefinitionId == "war_troll", "Beast " + id);
            foreach (int id in SiegeShowcaseScenario.Ladders)
                Assert.IsTrue(world.TryGetUnit(id, out var ladder) && ladder.DefinitionId == "siege_ladder", "Ladder " + id);
            foreach (int id in SiegeShowcaseScenario.Rams)
                Assert.IsTrue(world.TryGetUnit(id, out var ram) && ram.DefinitionId == "siege_ram", "Ram " + id);
            Assert.IsTrue(world.TryGetUnit(SiegeShowcaseScenario.Drake, out var drake) && drake.DefinitionId == "ember_drake");

            foreach (int id in new[] { SiegeShowcaseScenario.Hearth, SiegeShowcaseScenario.MusterHall,
                SiegeShowcaseScenario.Archive, SiegeShowcaseScenario.BeastLodge, SiegeShowcaseScenario.Storeyard })
                Assert.IsTrue(world.TryGetBuilding(id, out _), "Building " + id);

            Assert.IsTrue(world.TryGetPlayer(1, out var defender));
            Assert.GreaterOrEqual(defender.Resources.Stone, 1000, "The fortress is granted its stone, not filmed gathering it.");
        }

        [Test]
        public void EveryAuthoredFortificationSiteIsAFootprintTheRulesAccept()
        {
            var world = SiegeShowcaseScenario.Create();
            var crew = SiegeShowcaseScenario.Crew;
            var sites = new List<(string Id, int X, int Z)>(SiegeShowcaseScenario.KingdomWorks)
            {
                SiegeShowcaseScenario.KeepSite,
                SiegeShowcaseScenario.GateSite,
            };
            foreach (var site in sites)
            {
                // Fog and worker reach are the director's problem at recording time. What must hold here is the
                // part no amount of walking fixes: footprint alignment, terrain, resources and other buildings.
                var placement = world.ValidatePlacement(1, crew, site.Id, new SimPoint(site.X, site.Z));
                string where = site.Id + " at " + site.X + "/" + site.Z + ": " + placement.Message;
                Assert.AreNotEqual(CommandRejection.InvalidPlacement, placement.Reason, where);
                Assert.AreNotEqual(CommandRejection.UnknownDefinition, placement.Reason, where);
                Assert.AreNotEqual(CommandRejection.InsufficientResources, placement.Reason, where);
            }
        }

        [Test]
        public void NoAuthoredBuildingSitsOnTopOfAnother()
        {
            var world = SiegeShowcaseScenario.Create();
            int cell = world.Map.CellSizeMillimetres;
            var seen = new List<BuildingState>();
            foreach (var building in world.Buildings)
            {
                foreach (var other in seen)
                {
                    bool apart = System.Math.Abs(building.Position.X - other.Position.X) >= (building.WidthCells + other.WidthCells) * cell / 2
                        || System.Math.Abs(building.Position.Z - other.Position.Z) >= (building.DepthCells + other.DepthCells) * cell / 2;
                    Assert.IsTrue(apart, "Authored buildings overlap near " + building.Position.X + "/" + building.Position.Z);
                }
                seen.Add(building);
            }
        }

        [Test]
        public void TheFixtureIsOptInAndNeverTouchesAnOrdinaryStart()
        {
            // No command-line flag in a test run, so nothing must claim the world.
            Assert.IsFalse(SiegeShowcaseScenario.TryCreateWorld(out var world));
            Assert.IsNull(world);
        }
    }
}
