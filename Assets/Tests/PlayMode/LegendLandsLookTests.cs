using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Emberfield.Presentation;
using Emberfield.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Emberfield.Tests.PlayMode
{
    /// <summary>
    /// How the Lands of Legend look: every land draws its own ground, stands, nodes, tufts and water for every fantasy
    /// pairing, lava runs only in a volcanic land, the grade follows the camera between lands, and the embers and smoke
    /// stay capped and drop away on the low phone tier. The rules never see any of it (LegendLandsTests covers the map).
    /// </summary>
    public sealed class LegendLandsLookTests
    {
        private const string MapId = "legend_lands";
        private GameObject holder;
        private WorldView view;
        private RenderPipelineAsset pipeline;
        private Color fog, sky; private float fogStart, fogEnd;
        private float lodBias, meshLodThreshold; private int maximumLod, frameRate, vSync;
        private SkinWeights skinWeights; private AnisotropicFiltering anisotropic;

        [SetUp]
        public void Remember()
        {
            pipeline = QualitySettings.renderPipeline; lodBias = QualitySettings.lodBias; meshLodThreshold = QualitySettings.meshLodThreshold;
            maximumLod = QualitySettings.maximumLODLevel; frameRate = Application.targetFrameRate; vSync = QualitySettings.vSyncCount;
            skinWeights = QualitySettings.skinWeights; anisotropic = QualitySettings.anisotropicFiltering;
            fog = RenderSettings.fogColor; fogStart = RenderSettings.fogStartDistance; fogEnd = RenderSettings.fogEndDistance; sky = RenderSettings.ambientSkyColor;
        }

        [UnityTearDown]
        public IEnumerator Restore()
        {
            view?.Dispose(); view = null;
            if (holder != null) Object.Destroy(holder);
            MobileQuality.ForcedMode = null;
            yield return null;
            MobileQuality.Apply(null, null, false);
            QualitySettings.renderPipeline = pipeline; QualitySettings.lodBias = lodBias; QualitySettings.meshLodThreshold = meshLodThreshold;
            QualitySettings.maximumLODLevel = maximumLod; Application.targetFrameRate = frameRate; QualitySettings.vSyncCount = vSync;
            QualitySettings.skinWeights = skinWeights; QualitySettings.anisotropicFiltering = anisotropic;
            RenderSettings.fogColor = fog; RenderSettings.fogStartDistance = fogStart; RenderSettings.fogEndDistance = fogEnd; RenderSettings.ambientSkyColor = sky;
        }

        private static World Match(string first, string second, string mapId = MapId)
        {
            var rules = JsonUtility.FromJson<GameDefinition>(Resources.Load<TextAsset>("Definitions/greybox").text);
            var map = JsonUtility.FromJson<MapDefinition>(Resources.Load<TextAsset>("Maps/" + mapId).text);
            map.RealmId = ContentRealms.RealmForFaction(first);
            map.PlayerFactions = new[] { new PlayerFactionDefinition { PlayerId = 1, FactionId = first }, new PlayerFactionDefinition { PlayerId = 2, FactionId = second } };
            ContentRealms.PrepareStartingUnits(map);
            return new World(rules, map);
        }

        private Camera Camera(Vector3 focus, float zoom = 8)
        {
            var camera = new GameObject("Lands camera", typeof(Camera)).GetComponent<Camera>(); camera.transform.SetParent(holder.transform);
            Aim(camera, focus, zoom);
            return camera;
        }

        private static void Aim(Camera camera, Vector3 focus, float zoom)
        {
            camera.orthographic = true; camera.orthographicSize = zoom;
            camera.transform.rotation = Quaternion.Euler(55, 35, 0); camera.transform.position = focus - camera.transform.forward * 55;
        }

        private World Draw(string first, string second, string mapId = MapId)
        {
            var world = Match(first, second, mapId);
            holder = new GameObject("Lands look " + first + " " + second);
            view = new WorldView(world, holder.transform, Camera(new Vector3(72, 0, 56), 30));
            return world;
        }

        private static string BiomeAt(World world, Vector3 position) => world.Lands.BiomeAt(DefinitionLoader.ToSimulation(position));

        public static IEnumerable<object[]> Pairings => new[]
        {
            new object[] { "verdant", "ashen" }, new object[] { "ashen", "verdant" }, new object[] { "verdant", "drakeforged" },
            new object[] { "skeld", "ashen" }, new object[] { "skeld", "drakeforged" }, new object[] { "verdant", "verdant" }
        };

        [TestCaseSource(nameof(Pairings))]
        public void EveryLandDrawsItsOwnScenery(string first, string second)
        {
            var world = Draw(first, second);
            var biomes = world.Lands.Biomes;
            // The ground carries every biome shown, the map's own first, and a single-biome pairing draws as one biome.
            var ground = holder.GetComponentsInChildren<MeshRenderer>(true).First(r => r.name.EndsWith("original biome", StringComparison.Ordinal)).sharedMaterial;
            Assert.AreEqual(biomes.Count > 1, ground.IsKeywordEnabled("_LANDS"), ground.name);
            if (biomes.Count > 1)
                for (int slot = 0; slot < biomes.Count; slot++)
                    Assert.AreEqual(biomes[slot] + "_layers", ground.GetTexture(slot == 0 ? "_Layers" : "_Layers" + slot).name);
            // Every filler, node and tuft is drawn from the land it stands in.
            int checkedFillers = 0;
            foreach (var renderer in holder.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!renderer.name.StartsWith("Landscape ", StringComparison.Ordinal)) continue;
                string kind = renderer.name.Substring(10), land = BiomeAt(world, renderer.transform.position);
                if (kind == "flowers") Assert.AreEqual(MapLands.Elven, land, "Flowers grow only in the elven forest.");
                else if (kind == "ash_tuft") Assert.AreEqual(MapLands.Volcanic, land, "Ash tufts grow only in the volcanic waste.");
                else if (kind == "heather") Assert.AreEqual(MapLands.Highland, land, "Heather grows only on the highland.");
                else if ((kind == "thicket" || kind == "obstacle") && renderer.sharedMaterial.name.Split('_')[0] is string art && art != land)
                    Assert.AreEqual("obstacle", kind, "Only a peak's rocks stand in another land's style: " + renderer.sharedMaterial.name + " in " + land);
                else if (kind == "thicket" || kind == "obstacle") checkedFillers++;
            }
            Assert.Greater(checkedFillers, 1500);
            var nodes = holder.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Meshy ", StringComparison.Ordinal) && t.name.Contains("_node")).ToList();
            // Only the nodes in sight are drawn yet: each starting town's.
            Assert.Greater(nodes.Count, 8);
            foreach (var node in nodes) StringAssert.StartsWith(BiomeAt(world, node.position) + "_", node.name.Substring(6));
            // No filler grows on a monument's pad.
            foreach (var landmark in world.Map.Landmarks.Where(l => l.Kind == MapLands.MonumentSlot))
            {
                var at = DefinitionLoader.ToWorld(landmark.Position);
                Assert.IsFalse(holder.GetComponentsInChildren<MeshRenderer>(true).Any(r => r.name.StartsWith("Landscape ", StringComparison.Ordinal) &&
                    Mathf.Abs(r.transform.position.x - at.x) < 2.4f && Mathf.Abs(r.transform.position.z - at.z) < 2.4f), "A filler grows on the monument at " + at);
            }
        }

        [TestCaseSource(nameof(Pairings))]
        public void LavaRunsOnlyInAVolcanicLand(string first, string second)
        {
            var world = Draw(first, second);
            bool volcanic = world.Lands.Biomes.Contains(MapLands.Volcanic);
            var sheets = holder.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name.StartsWith("Public ", StringComparison.Ordinal) && (r.name.EndsWith(" water", StringComparison.Ordinal) || r.name.EndsWith(" lava", StringComparison.Ordinal))).ToList();
            var lava = sheets.Where(r => r.sharedMaterial.shader.name == "Emberfield/Meshy Lava").ToList();
            Assert.AreEqual(volcanic ? 1 : 0, lava.Count);
            Assert.IsTrue(sheets.All(r => (r.sharedMaterial.shader.name == "Emberfield/Meshy Lava") == r.name.EndsWith(" lava", StringComparison.Ordinal)));
            foreach (var sheet in sheets)
            {
                // Every quad of a sheet lies in cells of one land: lava in the volcanic land and never elsewhere.
                var mesh = sheet.GetComponent<MeshFilter>().sharedMesh; var vertices = mesh.vertices; var triangles = mesh.triangles;
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    var middle = (vertices[triangles[i]] + vertices[triangles[i + 1]] + vertices[triangles[i + 2]]) / 3;
                    string land = BiomeAt(world, middle);
                    if (lava.Contains(sheet)) Assert.AreEqual(MapLands.Volcanic, land);
                    else Assert.AreNotEqual(MapLands.Volcanic, land);
                }
            }
            // A ford reads as a crossing: crust on lava (vertex green), stepping stones in water.
            if (volcanic) Assert.IsTrue(lava[0].GetComponent<MeshFilter>().sharedMesh.colors.Any(c => c.g > .99f), "The lava ford has no crust.");
            bool water = world.Lands.Biomes.Any(b => b != MapLands.Volcanic && world.Map.WaterCells.Any(c => world.Lands.BiomeAt(c.X, c.Z) == b && world.Lands.ZoneAt(c.X, c.Z) != MapLands.Neutral));
            Assert.AreEqual(water, holder.GetComponentsInChildren<MeshRenderer>(true).Any(r => r.name.EndsWith(" ford", StringComparison.Ordinal) && !r.name.Contains(MapLands.Volcanic)));
        }

        [Test]
        public void AnotherMapKeepsItsOneBiome()
        {
            var world = Draw("aven", "serevin", "amber_crossing");
            Assert.IsFalse(world.Lands.HasLands);
            var ground = holder.GetComponentsInChildren<MeshRenderer>(true).First(r => r.name.EndsWith("original biome", StringComparison.Ordinal)).sharedMaterial;
            Assert.IsFalse(ground.IsKeywordEnabled("_LANDS"));
            Assert.IsFalse(holder.GetComponentsInChildren<MeshRenderer>(true).Any(r => r.name.StartsWith("Land ", StringComparison.Ordinal) || r.sharedMaterial != null && r.sharedMaterial.shader.name == "Emberfield/Meshy Lava"));
            Assert.IsFalse(holder.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Landscape flowers" || t.name == "Landscape heather" || t.name == "Landscape ash_tuft"));
            var sun = new GameObject("Sun", typeof(Light)).GetComponent<Light>(); sun.transform.SetParent(holder.transform);
            AlphaLighting.Configure(holder.GetComponentInChildren<Camera>(), sun, holder.transform, world.Map, world.Lands);
            Assert.IsNull(holder.GetComponentInChildren<LandAtmosphere>(), "Only a map with lands grades by land.");
        }

        [Test]
        public void TheGradeFollowsTheCamera()
        {
            var world = Match("verdant", "ashen");
            holder = new GameObject("Lands grade");
            var elven = new Vector3(26, 0, 30); var volcanic = new Vector3(118, 0, 82);
            var camera = Camera(elven);
            var sun = new GameObject("Sun", typeof(Light)).GetComponent<Light>(); sun.transform.SetParent(holder.transform);
            AlphaLighting.Configure(camera, sun, holder.transform, world.Map, world.Lands);
            var atmosphere = holder.GetComponentInChildren<LandAtmosphere>();
            Assert.IsNotNull(atmosphere);
            Assert.AreEqual(1, atmosphere.Weight(MapLands.Elven), .01f); Assert.AreEqual(0, atmosphere.Weight(MapLands.Volcanic), .01f);
            var golden = RenderSettings.fogColor;
            // Panning onto the volcanic land fades its grade in over about a second rather than cutting to it.
            Aim(camera, volcanic, 8);
            atmosphere.Step(.2f);
            Assert.That(atmosphere.Weight(MapLands.Volcanic), Is.InRange(.2f, .8f));
            Assert.That(atmosphere.Weight(MapLands.Elven), Is.InRange(.2f, .8f));
            for (int i = 0; i < 30; i++) atmosphere.Step(.1f);
            Assert.AreEqual(1, atmosphere.Weight(MapLands.Volcanic), .02f); Assert.AreEqual(0, atmosphere.Weight(MapLands.Elven), .02f);
            Assert.Greater(RenderSettings.fogColor.r, RenderSettings.fogColor.b, "The volcanic haze is red.");
            Assert.AreNotEqual(golden, RenderSettings.fogColor);
            // The neutral centre wears neither grade.
            Aim(camera, new Vector3(72, 0, 56), 5);
            atmosphere.Settle();
            Assert.AreEqual(0, atmosphere.Weight(MapLands.Volcanic), .01f); Assert.AreEqual(0, atmosphere.Weight(MapLands.Elven), .01f);
            // A pairing without an elven or a volcanic land has nothing to grade.
            Object.Destroy(holder); holder = new GameObject("Highland grade");
            var highland = Match("skeld", "drakeforged");
            var other = new GameObject("Sun", typeof(Light)).GetComponent<Light>(); other.transform.SetParent(holder.transform);
            AlphaLighting.Configure(Camera(elven), other, holder.transform, highland.Map, highland.Lands);
            Assert.IsNull(holder.GetComponentInChildren<LandAtmosphere>());
        }

        // The strongest "_RimStrength" any Meshy-shaded renderer under a unit's root actually renders with, or -1
        // if none carries the property (a unit drawn by another path, which this readability lift never touches).
        // A renderer with no override reads its material's own value: an unset property block still renders it.
        private static float RimOf(Transform root)
        {
            float strongest = -1;
            var block = new MaterialPropertyBlock();
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.sharedMaterial == null || !renderer.sharedMaterial.HasProperty("_RimStrength")) continue;
                block.Clear(); renderer.GetPropertyBlock(block);
                float rim = block.HasFloat("_RimStrength") ? block.GetFloat("_RimStrength") : renderer.sharedMaterial.GetFloat("_RimStrength");
                strongest = Mathf.Max(strongest, rim);
            }
            return strongest;
        }

        // Fog would hide one side's start from the other; this readability lift is about the ground under a unit,
        // not about scouting, so both starts see the whole map (the same trick SceneryStills uses for its tour).
        private World DrawSeeingBothSides(string first, string second)
        {
            var rules = JsonUtility.FromJson<GameDefinition>(Resources.Load<TextAsset>("Definitions/greybox").text);
            var map = JsonUtility.FromJson<MapDefinition>(Resources.Load<TextAsset>("Maps/" + MapId).text);
            map.RealmId = ContentRealms.RealmForFaction(first);
            map.PlayerFactions = new[] { new PlayerFactionDefinition { PlayerId = 1, FactionId = first }, new PlayerFactionDefinition { PlayerId = 2, FactionId = second } };
            ContentRealms.PrepareStartingUnits(map);
            map.OfflineMatch.VisionUnitCells = 128;
            foreach (var definition in rules.Units) definition.VisionCells = 0;
            var world = new World(rules, map);
            holder = new GameObject("Lands look " + first + " " + second + " (full vision)");
            view = new WorldView(world, holder.transform, Camera(new Vector3(72, 0, 56), 30));
            return world;
        }

        [Test]
        public void MeshyCharactersLiftTheirRimOnlyOnVolcanicGround()
        {
            var world = DrawSeeingBothSides("ashen", "verdant");
            UnitState onAsh = null, elsewhere = null; float onAshRim = -1, elsewhereRim = -1;
            foreach (var unit in world.Units)
            {
                var root = view.RootFor(unit.Id); if (root == null) continue;
                float rim = RimOf(root); if (rim < 0) continue; // not a Meshy character; this lift never touches it
                bool ash = world.Lands.BiomeAt(unit.Position) == MapLands.Volcanic;
                if (ash && onAsh == null) { onAsh = unit; onAshRim = rim; }
                else if (!ash && elsewhere == null) { elsewhere = unit; elsewhereRim = rim; }
            }
            Assert.IsNotNull(onAsh, "The volcanic land must field a Meshy character to test.");
            Assert.IsNotNull(elsewhere, "Another land must field one too.");
            Assert.AreEqual(.08f, elsewhereRim, .001f, "Off the ash the shader's plain default holds.");
            Assert.AreEqual(.26f, onAshRim, .001f, "The volcanic waste's own dark ground lifts the rim.");
            Assert.That(onAshRim, Is.GreaterThan(elsewhereRim), "A figure on the ash must read better than one off it.");
            // A pairing with no volcanic land has no ash to stand on, so nothing ever lifts.
            Object.Destroy(holder); holder = null;
            var highland = DrawSeeingBothSides("skeld", "drakeforged");
            int checkedUnits = 0;
            foreach (var unit in highland.Units)
            {
                var root = view.RootFor(unit.Id); if (root == null) continue;
                float rim = RimOf(root); if (rim < 0) continue;
                checkedUnits++;
                Assert.AreEqual(.08f, rim, .001f, "Highland units never lift.");
            }
            Assert.Greater(checkedUnits, 0, "The highland pairing must field a Meshy character to test.");
        }

        // Quads of each kind of mote in the view: every set is one mesh of four vertices a mote.
        private Dictionary<string, int> Motes()
        {
            var counts = new Dictionary<string, int>();
            foreach (var renderer in holder.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name.StartsWith("Land ", StringComparison.Ordinal)))
            {
                counts.TryGetValue(renderer.name, out int count);
                counts[renderer.name] = count + renderer.GetComponent<MeshFilter>().sharedMesh.vertexCount / 4;
            }
            return counts;
        }

        [UnityTest]
        public IEnumerator TheMotesAreCappedAndTheLowTierDropsThem()
        {
            Draw("verdant", "ashen");
            var desktop = Motes();
            Assert.Greater(desktop.GetValueOrDefault("Land LavaEmbers"), 0); Assert.Greater(desktop.GetValueOrDefault("Land VolcanoEmbers"), 0);
            Assert.Greater(desktop.GetValueOrDefault("Land Glimmers"), 0); Assert.Greater(desktop.GetValueOrDefault("Land VolcanoSmoke"), 0);
            Assert.LessOrEqual(desktop.Values.Sum(), 900, "The whole map's motes stay a few hundred quads.");
            foreach (string tier in new[] { "low", "high" })
            {
                view.Dispose(); view = null; Object.Destroy(holder); yield return null;
                MobileQuality.ForcedMode = tier;
                var owner = new GameObject("Tier " + tier);
                MobileQuality.Apply(null, owner, true);
                Draw("verdant", "ashen");
                owner.transform.SetParent(holder.transform);
                var drawn = Motes();
                if (tier == "high") { Assert.That(drawn, Is.EquivalentTo(desktop)); continue; }
                Assert.AreEqual(0, drawn.GetValueOrDefault("Land LavaEmbers") + drawn.GetValueOrDefault("Land VolcanoEmbers") + drawn.GetValueOrDefault("Land Glimmers"), "Low draws no embers or glimmers.");
                int smoke = desktop.GetValueOrDefault("Land VolcanoSmoke") + desktop.GetValueOrDefault("Land LavaSmoke");
                int lowSmoke = drawn.GetValueOrDefault("Land VolcanoSmoke") + drawn.GetValueOrDefault("Land LavaSmoke");
                Assert.That(lowSmoke, Is.InRange(1, smoke / 2 + 4), "Low keeps about half the smoke.");
            }
        }
    }
}
