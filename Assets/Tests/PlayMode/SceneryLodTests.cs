using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Emberfield.Presentation;
using Emberfield.Quality;
using Emberfield.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Emberfield.Tests.PlayMode
{
    /// <summary>
    /// Phones draw lighter trees, rocks, palms, buildings and units once zoomed out; a desktop draws what it always did.
    /// The battlefield camera is orthographic, so a model fills its size over twice the orthographic size of the screen.
    /// Fillers and gatherable nodes keep one renderer and swap a shared mesh when the zoom crosses their switch
    /// (SceneryDetail); buildings keep a LOD group, which compares that share multiplied by the LOD bias.
    /// </summary>
    public sealed class SceneryLodTests
    {
        private const float ClosestZoom = 5, PlayZoom = 8, WidestZoom = 22;
        private static readonly Vector3 Focus = new Vector3(20, 0, 20);
        private GameObject root;
        private MatchController match;
        private RenderPipelineAsset pipeline;
        private float lodBias, meshLodThreshold;
        private int maximumLod, frameRate, vSync;
        private SkinWeights skinWeights;
        private AnisotropicFiltering anisotropic;

        [SetUp]
        public void Remember()
        {
            pipeline = QualitySettings.renderPipeline; lodBias = QualitySettings.lodBias; meshLodThreshold = QualitySettings.meshLodThreshold;
            maximumLod = QualitySettings.maximumLODLevel; frameRate = Application.targetFrameRate; vSync = QualitySettings.vSyncCount;
            skinWeights = QualitySettings.skinWeights; anisotropic = QualitySettings.anisotropicFiltering;
        }

        [UnityTearDown]
        public IEnumerator Restore()
        {
            if (root != null) Object.Destroy(root);
            MobileQuality.ForcedMode = null;
            yield return null;
            // Back to a desktop, so the next fixture's views do not build a phone's scenery.
            MobileQuality.Apply(null, null, false);
            QualitySettings.renderPipeline = pipeline; QualitySettings.lodBias = lodBias; QualitySettings.meshLodThreshold = meshLodThreshold;
            QualitySettings.maximumLODLevel = maximumLod; Application.targetFrameRate = frameRate; QualitySettings.vSyncCount = vSync;
            QualitySettings.skinWeights = skinWeights; QualitySettings.anisotropicFiltering = anisotropic;
        }

        // Amber Crossing, the forest map, as the offline Aven match opens it.
        private IEnumerator StartMatch(string tier)
        {
            if (root != null) { Object.Destroy(root); yield return null; }
            MobileQuality.ForcedMode = tier;
            root = new GameObject("Scenery LOD " + (tier ?? "desktop")); root.SetActive(false);
            match = root.AddComponent<MatchController>();
            match.InitialOfflineFactionId = "aven"; match.InitialOfflineMode = VictoryMode.Conquest;
            root.SetActive(true); match.enabled = false;
            yield return null;
            match.SyncPresentation(1);
            // A node first drawn by that sync gives its prefab's group up at the end of the frame.
            yield return null;
        }

        private void Zoom(float zoom) { match.Rig.SetHome(Focus, zoom); match.SyncPresentation(1); }

        // The obstacle models of the catalogue: near mesh to far mesh (null where the model has one level).
        private static Dictionary<Mesh, Mesh> ObstacleModels()
        {
            var models = new Dictionary<Mesh, Mesh>();
            foreach (var entry in MeshyPropVisuals.Entries)
            {
                if (entry.role != "obstacle") continue;
                var prefab = Resources.Load<GameObject>(entry.prefab);
                var level = prefab.transform.Find("LOD1");
                models[prefab.GetComponent<MeshFilter>().sharedMesh] = level != null ? level.GetComponent<MeshFilter>().sharedMesh : null;
            }
            if (models.Values.All(far => far == null)) Assert.Ignore("No Meshy fillers with a far level baked yet.");
            return models;
        }

        // Every Meshy filler on the map with the model it is a copy of, whichever level it draws now.
        private List<(MeshFilter Filter, Mesh Near, Mesh Far)> Fillers(Dictionary<Mesh, Mesh> models)
        {
            var fillers = new List<(MeshFilter, Mesh, Mesh)>();
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!filter.name.StartsWith("Landscape ", StringComparison.Ordinal)) continue;
                foreach (var model in models)
                    if (model.Value != null && (filter.sharedMesh == model.Key || filter.sharedMesh == model.Value)) { fillers.Add((filter, model.Key, model.Value)); break; }
            }
            return fillers;
        }

        private static float Size(Mesh near) { var size = near.bounds.size; return Mathf.Max(size.x, size.y, size.z); }

        private static NUnit.Framework.Constraints.IResolveConstraint AllocatesNothing() => UnityEngine.TestTools.Constraints.ConstraintExtensions.AllocatingGCMemory(Is.Not);

        private static int Triangles(Mesh mesh) => (int)mesh.GetIndexCount(0) / 3;
        private static int Triangles(Renderer renderer) => Triangles(renderer.GetComponent<MeshFilter>().sharedMesh);

        [UnityTest]
        public IEnumerator ADesktopDrawsEveryFillerWholeAndShadowedAtEveryZoom()
        {
            yield return StartMatch(null);
            // No zoom a desktop allows reaches a filler's switch, so a desktop keeps the one renderer and the one mesh a
            // filler always had, and nothing follows the zoom.
            var models = ObstacleModels();
            Assert.That(SceneryDetail.Count, Is.EqualTo(0), "a desktop registers nothing");
            foreach (float zoom in new[] { WidestZoom, PlayZoom })
            {
                Zoom(zoom);
                int fillers = 0;
                foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    Assert.That(filter.sharedMesh != null && models.Values.Contains(filter.sharedMesh), Is.False, filter.name + " draws no far level on a desktop");
                    if (!models.ContainsKey(filter.sharedMesh)) continue;
                    fillers++;
                    Assert.IsNull(filter.GetComponent<LODGroup>(), filter.name + " has no LOD group on a desktop");
                    Assert.That(filter.GetComponentsInChildren<Renderer>(true).Length, Is.EqualTo(1), filter.name + " is one renderer");
                    Assert.That(filter.GetComponent<MeshRenderer>().shadowCastingMode, Is.EqualTo(ShadowCastingMode.On), filter.name);
                }
                Assert.That(fillers, Is.GreaterThan(500), "Amber Crossing is a forest map");
            }
        }

        [UnityTest]
        public IEnumerator TheLowTierKeepsDetailedTreesAtThePlayZoomAndLightensThemWellBeyondIt()
        {
            yield return StartMatch("low");
            var tier = MobileTierSettings.For(MobileTier.Low);
            var models = ObstacleModels();
            var fillers = Fillers(models);
            Assert.That(fillers.Count, Is.GreaterThan(500), "Amber Crossing is a forest map");
            Assert.That(SceneryDetail.Count, Is.GreaterThanOrEqualTo(fillers.Count), "every filler with a far level follows the zoom");
            foreach (var (filter, near, far) in fillers)
            {
                Assert.IsNull(filter.GetComponent<LODGroup>(), filter.name + " has no LOD group");
                Assert.That(filter.GetComponentsInChildren<Renderer>(true).Length, Is.EqualTo(1), filter.name + " is one renderer");
                Assert.That(filter.GetComponent<MeshRenderer>().shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off), filter.name + " casts no shadow on the low tier");
                Assert.That(Triangles(far), Is.LessThanOrEqualTo(Triangles(near) * .6f), filter.name + " far level");
            }
            var trees = fillers.Where(filler => filler.Filter.name == "Landscape thicket").ToList();
            Assert.That(trees.Count, Is.GreaterThan(300));

            // Every tree is detailed at the play zoom and zoomed right in, and light at the widest zoom.
            foreach (float zoom in new[] { PlayZoom, ClosestZoom })
            {
                Zoom(zoom);
                foreach (var (filter, near, _) in trees) Assert.That(filter.sharedMesh, Is.SameAs(near), filter.name + " is detailed at zoom " + zoom);
            }
            Zoom(WidestZoom);
            foreach (var (filter, _, far) in trees) Assert.That(filter.sharedMesh, Is.SameAs(far), filter.name + " is light at the widest zoom");

            // Each model switches at its own size's share, every copy at once, with room to spare past the play zoom; it
            // comes back only once the zoom is well inside the switch again.
            foreach (var model in trees.GroupBy(tree => tree.Near))
            {
                float switchZoom = MobileTiers.SceneryDetailZoom(tier.SceneryDetailHeight, Size(model.Key));
                Assert.That(switchZoom / MobileTiers.SceneryHysteresis, Is.GreaterThan(PlayZoom * 1.3f), "a tree stays detailed well past the play zoom");
                var copies = model.ToList();
                void Expect(float zoom, bool light, string why)
                {
                    Zoom(zoom);
                    foreach (var (filter, near, far) in copies) Assert.That(filter.sharedMesh, Is.SameAs(light ? far : near), filter.name + " " + why + " at zoom " + zoom);
                }
                Expect(PlayZoom, false, "detailed");
                Expect(switchZoom * .98f, false, "detailed just inside the switch");
                Expect(switchZoom * 1.02f, true, "light just past it");
                Expect(switchZoom * .97f, true, "still light a little inside it");
                Expect(switchZoom * .9f, false, "detailed again well inside it");
                Expect(switchZoom * .98f, false, "still detailed");
            }
        }

        [UnityTest]
        public IEnumerator TheZoomSwapCostsNothingBetweenZoomChangesAndAllocatesNothing()
        {
            yield return StartMatch("mid");
            var models = ObstacleModels();
            var camera = match.Rig.Camera;
            Zoom(PlayZoom);
            var fillers = Fillers(models);
            var drawn = fillers.Select(filler => filler.Filter.sharedMesh).ToArray();
            // Many syncs at one zoom change nothing.
            for (int i = 0; i < 5; i++) match.SyncPresentation(1);
            for (int i = 0; i < fillers.Count; i++) Assert.That(fillers[i].Filter.sharedMesh, Is.SameAs(drawn[i]));
            Assert.That(() => SceneryDetail.Refresh(camera), AllocatesNothing(), "a sync at an unchanged zoom");
            // Crossing every tree's switch and back swaps every copy without allocating.
            camera.orthographicSize = WidestZoom;
            Assert.That(() => SceneryDetail.Refresh(camera), AllocatesNothing(), "zooming out");
            Assert.That(fillers.Count(filler => filler.Filter.sharedMesh == filler.Far), Is.GreaterThan(300));
            camera.orthographicSize = PlayZoom;
            Assert.That(() => SceneryDetail.Refresh(camera), AllocatesNothing(), "zooming back in");
            for (int i = 0; i < fillers.Count; i++) Assert.That(fillers[i].Filter.sharedMesh, Is.SameAs(drawn[i]));
            // The stills can hold the far level whatever the zoom, and hand the level back to the zoom after.
            SceneryDetail.HoldLighterLevel();
            match.SyncPresentation(1);
            Assert.That(fillers.All(filler => filler.Filter.sharedMesh == filler.Far), Is.True, "held at the far level");
            SceneryDetail.Release();
            match.SyncPresentation(1);
            for (int i = 0; i < fillers.Count; i++) Assert.That(fillers[i].Filter.sharedMesh, Is.SameAs(drawn[i]), "released");
        }

        // The first node of the kind drawn with its Meshy model (whose levels are children named LOD0 and LOD1).
        private int Node(ResourceKind kind)
        {
            foreach (var resource in match.World.Resources)
            {
                var visual = resource.Kind == kind ? match.View.RootFor(resource.Id) : null;
                if (visual != null && visual.GetComponentsInChildren<MeshFilter>(true).Any(filter => filter.name == "LOD0")) return resource.Id;
            }
            return 0;
        }

        [UnityTest]
        public IEnumerator GatherableNodesSwitchWithTheTreesAndADesktopKeepsTheirPrefab()
        {
            yield return StartMatch(null);
            var group = match.View.RootFor(Node(ResourceKind.Wood))?.GetComponentInChildren<LODGroup>();
            if (group == null) Assert.Ignore("No Meshy wood node baked yet.");
            Assert.That(group.GetComponentsInChildren<MeshRenderer>(true).Length, Is.EqualTo(2), "a desktop keeps the prefab's two levels");
            var nearMesh = group.GetLODs()[0].renderers[0].GetComponent<MeshFilter>().sharedMesh;
            var farMesh = group.GetLODs()[1].renderers[0].GetComponent<MeshFilter>().sharedMesh;

            foreach (string name in new[] { "low", "mid", "high" })
            {
                yield return StartMatch(name);
                var tier = MobileQuality.Active;
                var node = match.View.RootFor(Node(ResourceKind.Wood));
                Assert.IsNull(node.GetComponentInChildren<LODGroup>(), name + ": the node's group gives way to the zoom");
                var renderers = node.GetComponentsInChildren<MeshRenderer>(true).Where(renderer => renderer.name == "LOD0" || renderer.name == "LOD1").ToArray();
                Assert.That(renderers.Length, Is.EqualTo(1), name + ": one renderer");
                var filter = renderers[0].GetComponent<MeshFilter>();
                // A node leaves its detailed level at the zoom the tier's 3 m trees do, whatever its own size.
                float treeZoom = MobileTiers.SceneryDetailZoom(tier.SceneryDetailHeight, MobileTiers.SceneryTreeHeight);
                Assert.That(MobileTiers.NodeDetailZoom(tier.SceneryDetailHeight), Is.EqualTo(treeZoom).Within(1e-4f));
                Zoom(PlayZoom);
                Assert.That(filter.sharedMesh, Is.SameAs(nearMesh), name + ": detailed at the play zoom");
                if (treeZoom * 1.02f <= WidestZoom)
                {
                    Zoom(treeZoom * .98f);
                    Assert.That(filter.sharedMesh, Is.SameAs(nearMesh), name + ": detailed just inside the trees' switch");
                    Zoom(treeZoom * 1.02f);
                    Assert.That(filter.sharedMesh, Is.SameAs(farMesh), name + ": light just beyond it");
                }

                // A berry bush is a third of a tree's size; it keeps its berries wherever the trees keep their detail,
                // the play zoom included.
                Zoom(PlayZoom);
                var bush = match.View.RootFor(Node(ResourceKind.Food));
                Assert.IsNotNull(bush, name + ": a Meshy berry bush is drawn");
                var bushFilter = bush.GetComponentsInChildren<MeshFilter>(true).Single(candidate => candidate.name == "LOD0");
                var entry = MeshyPropVisuals.Entries.First(candidate => candidate.role == "resource" && candidate.kind == "Food" && candidate.biome == "forest");
                Assert.That(bushFilter.sharedMesh, Is.SameAs(Resources.Load<GameObject>(entry.prefab).transform.Find("LOD0").GetComponent<MeshFilter>().sharedMesh),
                    name + ": the berry bush keeps its berries at the play zoom");
            }
        }

        [UnityTest]
        public IEnumerator ATreeBeingFelledSwaysAtItsFarLevelToo()
        {
            yield return StartMatch("low");
            int tree = Node(ResourceKind.Wood), worker = 0;
            if (tree == 0) Assert.Ignore("No Meshy wood node baked yet.");
            foreach (var unit in match.World.Units) if (unit.OwnerId == MatchController.LocalPlayer && unit.IsWorker) { worker = unit.Id; break; }
            var filter = match.View.RootFor(tree).GetComponentsInChildren<MeshFilter>(true).Single(candidate => candidate.name == "LOD0");
            var near = filter.sharedMesh;
            Assert.IsTrue(match.World.Submit(new GatherCommand(MatchController.LocalPlayer, new[] { worker }, tree)).Accepted);
            bool cutting = false;
            for (int tick = 0; tick < World.TickRate * 120 && !cutting; tick++)
            {
                match.World.Tick();
                cutting = match.World.TryGetUnit(worker, out var state) && state.WorkerTask == WorkerTask.Gathering && state.TargetResourceId == tree;
            }
            Assert.IsTrue(cutting, "The worker must reach the tree and start cutting.");
            var block = new MaterialPropertyBlock();
            // The node's one renderer keeps its sway whichever mesh it draws, and through a swap.
            foreach (float zoom in new[] { WidestZoom, PlayZoom, WidestZoom })
            {
                Zoom(zoom);
                Assert.That(filter.sharedMesh == near, Is.EqualTo(zoom < WidestZoom), "zoom " + zoom);
                block.Clear(); filter.GetComponent<MeshRenderer>().GetPropertyBlock(block);
                Assert.That(block.GetFloat("_Sway"), Is.GreaterThan(0), "the tree bends with the axe at zoom " + zoom);
            }
        }

        [UnityTest]
        public IEnumerator PhoneUnitsDrawLighterMeshLevelsZoomedOutAndADesktopNever()
        {
            List<SkinnedMeshRenderer> Units() => root.GetComponentsInChildren<CorsairAnimationDriver>(true)
                .SelectMany(driver => driver.GetComponentsInChildren<SkinnedMeshRenderer>(true)).Where(skin => skin.sharedMesh != null && skin.sharedMesh.lodCount > 1).ToList();
            yield return StartMatch(null);
            var desktop = Units();
            if (desktop.Count == 0) Assert.Ignore("No Meshy unit with mesh levels yet.");
            match.Rig.SetHome(new Vector3(20, 0, 20), WidestZoom); match.SyncPresentation(1);
            foreach (var skin in desktop) Assert.That(skin.forceMeshLod, Is.EqualTo(-1), "a desktop leaves the level to Unity, which keeps the whole mesh under this camera");
            yield return StartMatch("low");
            // Every unit is under 2.3 m, so under the low tier's 0.2 of the screen at the play zoom and wider.
            foreach (float zoom in new[] { WidestZoom, PlayZoom })
            {
                match.Rig.SetHome(new Vector3(20, 0, 20), zoom); match.SyncPresentation(1);
                foreach (var skin in Units())
                {
                    var unit = skin.GetComponentInParent<CorsairAnimationDriver>();
                    Assert.That(unit.HasValidRig, Is.True, unit.name + " keeps its rig");
                    Assert.That(skin.forceMeshLod, Is.GreaterThan(0), unit.name + " draws a lighter level at zoom " + zoom);
                    Assert.That(skin.sharedMesh.GetIndexCount(0, skin.forceMeshLod), Is.LessThan(skin.sharedMesh.GetIndexCount(0, 0)));
                }
            }
            var anyUnit = Units()[0];
            match.Rig.SetHome(new Vector3(20, 0, 20), WidestZoom); match.SyncPresentation(1);
            int widest = anyUnit.forceMeshLod;
            match.Rig.SetHome(new Vector3(20, 0, 20), 5); match.SyncPresentation(1);
            Assert.That(anyUnit.forceMeshLod, Is.LessThan(widest), "zooming in brings the detail back");
            Assert.That(UnitMeshDetail.Count, Is.GreaterThanOrEqualTo(Units().Count));
        }

        [Test]
        public void EveryCultureBuildingHasAFarLevelThatStandsWhereItsNearOneDoes()
        {
            int checkedBuildings = 0;
            foreach (var entry in MeshyBuildingVisuals.Entries)
            {
                var prefab = Resources.Load<GameObject>(entry.prefab);
                Assert.IsNotNull(prefab, entry.id);
                bool fortification = entry.building == "wall" || entry.building == "gate" || entry.building == MeshyBuildingVisuals.GateLeaf;
                var group = prefab.GetComponent<LODGroup>();
                if (fortification) { Assert.IsNull(group, entry.id + " is light enough for one level"); continue; }
                Assert.IsNotNull(group, entry.id + " has a far level");
                var levels = group.GetLODs();
                Assert.That(levels.Length, Is.EqualTo(2), entry.id);
                Assert.That(levels[0].screenRelativeTransitionHeight, Is.EqualTo(MeshyBuildingVisuals.FarLevelHeight).Within(1e-5f), entry.id);
                Assert.That(levels[1].screenRelativeTransitionHeight, Is.EqualTo(0), entry.id + " is never culled");
                var near = levels[0].renderers.Single(); var far = levels[1].renderers.Single();
                int nearTriangles = Triangles(near), farTriangles = Triangles(far);
                Assert.That(farTriangles, Is.InRange(nearTriangles * .3f, nearTriangles * .45f), entry.id + " far triangles");
                Assert.That(entry.farTriangles, Is.EqualTo(farTriangles), entry.id + " catalogue");
                Assert.That(far.sharedMaterial, Is.SameAs(near.sharedMaterial), entry.id);
                Assert.That(far.shadowCastingMode, Is.EqualTo(ShadowCastingMode.On), entry.id);
                var instance = Object.Instantiate(prefab);
                try
                {
                    Bounds nearBox = default, farBox = default;
                    foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>())
                        if (renderer.name == "LOD1") farBox = renderer.bounds; else nearBox = renderer.bounds;
                    Assert.That((farBox.size - nearBox.size).magnitude, Is.LessThan(nearBox.size.magnitude * .05f), entry.id + " far level keeps the silhouette's box");
                    Assert.That(farBox.min.y, Is.GreaterThanOrEqualTo(nearBox.min.y - .001f), entry.id + " far level does not sink");
                }
                finally { Object.DestroyImmediate(instance); }
                // A desktop's LOD bias is 2: even the smallest building at the widest zoom stays on its near level.
                Assert.That(group.size / (2 * WidestZoom) * 2, Is.GreaterThanOrEqualTo(MeshyBuildingVisuals.FarLevelHeight), entry.id + " on a desktop");
                checkedBuildings++;
            }
            if (checkedBuildings == 0) Assert.Ignore("No Meshy culture buildings baked yet.");
            Assert.That(checkedBuildings, Is.GreaterThanOrEqualTo(46));
        }
    }
}
