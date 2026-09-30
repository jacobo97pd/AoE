using System.Collections.Generic;
using Emberfield.Presentation;
using Emberfield.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace Emberfield.Tests.PlayMode
{
    public sealed class FrontierBiomeArtTests
    {
        [TestCase("amber_crossing", "forest")]
        [TestCase("sapphire_coast", "caribbean")]
        [TestCase("sunscar_basin", "desert")]
        public void BiomesHaveFairMirroredStartsAndConnectedObjectivesAndResourceRoutes(string mapId, string biomeId)
        {
            var world = DefinitionLoader.CreateOfflineWorld("aven", VictoryMode.Dominion, mapId); var map = world.Map;
            Assert.AreEqual(biomeId, map.BiomeId); Assert.AreEqual(ContentRealms.Historical, map.RealmId);
            int width = map.WidthCells, height = map.HeightCells; var blocked = new HashSet<int>();
            foreach (var cell in map.BlockedCells) blocked.Add(cell.Z * width + cell.X);
            foreach (var cell in map.BlockedCells) Assert.IsTrue(blocked.Contains((height - 1 - cell.Z) * width + width - 1 - cell.X), "Terrain must retain 180 degree symmetry.");
            var reached = new HashSet<int>(); var queue = new Queue<int>();
            var start = map.UnitSpawns[0].Position; int first = start.Z / 1000 * width + start.X / 1000; reached.Add(first); queue.Enqueue(first);
            while (queue.Count > 0)
            {
                int current = queue.Dequeue(), x = current % width, z = current / width;
                foreach (var step in new[] { new Vector2Int(-1, 0), new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(0, 1) })
                {
                    int nx = x + step.x, nz = z + step.y, index = nz * width + nx;
                    if (nx < 0 || nx >= width || nz < 0 || nz >= height || blocked.Contains(index) || !reached.Add(index)) continue;
                    queue.Enqueue(index);
                }
            }
            int own = 0, rival = 0;
            foreach (var unit in map.UnitSpawns) { if (unit.OwnerId == 1) own++; else if (unit.OwnerId == 2) rival++; Assert.IsTrue(reached.Contains(unit.Position.Z / 1000 * width + unit.Position.X / 1000)); }
            Assert.AreEqual(4, own); Assert.AreEqual(own, rival);
            foreach (var node in map.ResourceSpawns)
            {
                Assert.IsTrue(reached.Contains(node.Position.Z / 1000 * width + node.Position.X / 1000), "Resource approach terrain: " + node.Id);
                bool mirror = false;
                foreach (var candidate in map.ResourceSpawns)
                    if (candidate.DefinitionId == node.DefinitionId && candidate.Position.X + node.Position.X == width * 1000 && candidate.Position.Z + node.Position.Z == height * 1000) mirror = true;
                Assert.IsTrue(mirror, "Resources must have identical mirrored counterparts: " + node.Id);
            }
            foreach (var objective in map.OfflineMatch.Objectives) Assert.IsTrue(reached.Contains(objective.Position.Z / 1000 * width + objective.Position.X / 1000), objective.Id);
            Assert.AreEqual(0, world.TickIndex, "Map/presentation inspection must not advance the simulation.");
        }

        [Test]
        public void CosmeticApplicationChangesOnlyVisualPropertiesAndKeepsTeamIdentificationAndMeshOwnership()
        {
            var holder = new GameObject("Cosmetic isolation");
            try
            {
                var creature = AlphaWorldArt.Unit("ember_drake", FactionKind.DrakeforgedClans, holder.transform, 2);
                Assert.IsNotNull(creature.Find("Left wing")); Assert.IsNotNull(creature.Find("Right wing"));
                var renderer = creature.GetComponentInChildren<MeshRenderer>(); var filter = creature.GetComponentInChildren<MeshFilter>();
                var mesh = filter.sharedMesh; var scale = creature.localScale; var before = new MaterialPropertyBlock(); renderer.GetPropertyBlock(before);
                AlphaWorldArt.ApplyCosmetic(creature, new CosmeticVisualStyle("test_sapphire", Color.blue, Color.cyan, .12f));
                var after = new MaterialPropertyBlock(); renderer.GetPropertyBlock(after);
                Assert.AreEqual(before.GetColor("_TeamColor"), after.GetColor("_TeamColor"));
                Assert.AreEqual(Color.blue, after.GetColor("_CosmeticPrimary")); Assert.AreEqual(.12f, after.GetFloat("_CosmeticEmission"));
                Assert.AreSame(mesh, filter.sharedMesh); Assert.AreEqual(scale, creature.localScale); Assert.IsEmpty(creature.GetComponentsInChildren<Collider>());
            }
            finally { Object.DestroyImmediate(holder); }
        }

        [Test]
        public void EachBiomeOwnsItsResourceSilhouettesAndBothNewMapsHaveDistinctTopology()
        {
            var holder = new GameObject("Biome resources");
            try
            {
                var forest = AlphaWorldArt.Resource(ResourceKind.Wood, holder.transform, "forest").GetComponentInChildren<MeshFilter>().sharedMesh;
                var palm = AlphaWorldArt.Resource(ResourceKind.Wood, holder.transform, "caribbean").GetComponentInChildren<MeshFilter>().sharedMesh;
                var date = AlphaWorldArt.Resource(ResourceKind.Wood, holder.transform, "desert").GetComponentInChildren<MeshFilter>().sharedMesh;
                CollectionAssert.AreNotEqual(forest.vertices, palm.vertices); CollectionAssert.AreNotEqual(palm.vertices, date.vertices);
                var coast = DefinitionLoader.CreateOfflineWorld("pirates", VictoryMode.Conquest, "sapphire_coast");
                var desert = DefinitionLoader.CreateOfflineWorld("skeld", VictoryMode.Conquest, "sunscar_basin");
                CollectionAssert.AreNotEqual(coast.Map.BlockedCells, desert.Map.BlockedCells);
                CollectionAssert.AreNotEqual(System.Array.ConvertAll(coast.Map.ResourceSpawns, node => node.Position), System.Array.ConvertAll(desert.Map.ResourceSpawns, node => node.Position));
            }
            finally { Object.DestroyImmediate(holder); }
        }

        [Test]
        public void UpperBodiesOfCreaturesAndSiegeCanBePickedWithoutCosmeticsChangingTheTarget()
        {
            var holder = new GameObject("Tall body picking"); WorldView view = null;
            try
            {
                var source = DefinitionLoader.CreateOfflineWorld("drakeforged", VictoryMode.Conquest);
                var map = new MapDefinition { Id = "faction_proving_ground", RealmId = ContentRealms.Fantasy, BiomeId = "forest", WidthCells = 64, HeightCells = 48,
                    PlayerFactions = new[] { new PlayerFactionDefinition { PlayerId = 1, FactionId = "drakeforged" }, new PlayerFactionDefinition { PlayerId = 2, FactionId = "ashen" } },
                    UnitSpawns = new[] { new UnitSpawnDefinition { Id = 501, OwnerId = 1, DefinitionId = "ember_drake", Position = new SimPoint(18000, 24000) }, new UnitSpawnDefinition { Id = 502, OwnerId = 1, DefinitionId = "siege_tower", Position = new SimPoint(34000, 24000) } } };
                var world = new World(source.Definition, map);
                var camera = new GameObject("Pick camera", typeof(Camera)).GetComponent<Camera>(); camera.transform.SetParent(holder.transform);
                camera.orthographic = true; camera.orthographicSize = 24; camera.transform.position = new Vector3(26, 28, -8); camera.transform.LookAt(new Vector3(26, 0, 24));
                view = new WorldView(world, holder.transform, camera);
                foreach (var unit in world.Units)
                {
                    var body = view.RootFor(unit.Id); var upper = camera.WorldToScreenPoint(body.position + Vector3.up * (AlphaWorldArt.UnitHeight(unit.DefinitionId) -.35f));
                    Assert.AreEqual(unit.Id, view.Pick(camera, upper, 1), "The upper body must accept a normal tap: " + unit.DefinitionId);
                    AlphaWorldArt.ApplyCosmetic(body.Find("Model"), new CosmeticVisualStyle("test", Color.blue, Color.cyan));
                    Assert.AreEqual(unit.Id, view.Pick(camera, upper, 1), "A cosmetic cannot change the selection target.");
                }
            }
            finally { view?.Dispose(); Object.DestroyImmediate(holder); }
        }
    }
}
