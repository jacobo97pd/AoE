using System;
using Emberfield.Presentation;
using Emberfield.Simulation;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Emberfield.Tests.PlayMode
{
    /// <summary>
    /// Meshy scenery is drawn by the thousand and scaled per cell. Every model must stand on the ground at its baked
    /// size, never block picking with a collider, and stay inside what the RTS camera can afford.
    /// </summary>
    public sealed class MeshyPropVisualsTests
    {
        [Test]
        public void EveryPropStandsOnTheGroundWithoutCollidersAndWithinBudget()
        {
            if (MeshyPropVisuals.Entries.Length == 0) Assert.Ignore("No Meshy props baked yet.");
            foreach (var entry in MeshyPropVisuals.Entries)
            {
                var prefab = Resources.Load<GameObject>(entry.prefab);
                Assert.IsNotNull(prefab, entry.id + " prefab");
                Assert.IsEmpty(prefab.GetComponentsInChildren<Collider>(true), entry.id + " must not take clicks from units");
                var filters = prefab.GetComponentsInChildren<MeshFilter>(true);
                Assert.IsNotEmpty(filters, entry.id + " mesh");
                foreach (var filter in filters)
                {
                    // Baked in metres from the ground up: the per-cell scale and the felling sway both depend on it.
                    Assert.AreEqual(0, filter.sharedMesh.bounds.min.y, .02f, entry.id + " " + filter.name + " stands on y = 0");
                    Assert.Greater(filter.sharedMesh.bounds.size.y, .2f, entry.id + " has real height in metres");
                }
                // Owned hulls use the unit shader's team mask; decorative moored ships remain scenery props.
                string shader = entry.role == "siege" || entry.role == "ship" ? "Emberfield/Meshy Unit" : "Emberfield/Meshy Prop";
                foreach (var renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
                {
                    Assert.AreEqual(shader, renderer.sharedMaterial.shader.name, entry.id + " shader");
                    if (entry.role != "ship") continue;
                    var material = renderer.sharedMaterial;
                    Assert.IsTrue(material.HasProperty("_TeamColor"), entry.id + " supports the owner's colour");
                    Assert.Greater(material.GetFloat("_TeamStrength"), 0, entry.id + " enables its painted team mask");
                    foreach (string texture in new[] { "_BaseMap", "_BumpMap", "_MaskMap" })
                        Assert.IsNotNull(material.GetTexture(texture), entry.id + " retains " + texture);
                    foreach (string pass in new[] { "Forward", "ShadowCaster", "DepthOnly" })
                        Assert.GreaterOrEqual(material.FindPass(pass), 0, entry.id + " renders " + pass);
                }
                Assert.LessOrEqual(entry.triangles, entry.role == "obstacle" ? 700 : 4500, entry.id + " triangles");
                if (entry.role == "obstacle") Assert.IsNotNull(prefab.GetComponent<MeshRenderer>(), entry.id + " is one renderer the loader can share");
                Assert.IsNotNull(prefab.GetComponent<LODGroup>(), entry.id + " culls or simplifies with distance");
            }
        }

        [Test]
        public void PirateHullPreservesItsTexturesAndUsesDistinctOwnerMaterialsAtEveryLod()
        {
            var entry = MeshyPropVisuals.ResolveShip("pirate_sloop", FactionKind.PirateBrotherhood);
            Assert.IsNotNull(entry, "The playable pirate ship needs its authored hull.");
            Assert.AreEqual("pirate_sloop", entry.id);
            var parent = new GameObject("Ship owner material contract");
            try
            {
                var first = MeshyPropVisuals.TryShip("pirate_sloop", 1, FactionKind.PirateBrotherhood, parent.transform);
                var second = MeshyPropVisuals.TryShip("pirate_sloop", 2, FactionKind.PirateBrotherhood, parent.transform);
                Assert.IsNotNull(first); Assert.IsNotNull(second);
                var source = Resources.Load<GameObject>(entry.prefab).GetComponentInChildren<MeshRenderer>(true).sharedMaterial;
                var firstMaterial = first.GetComponentInChildren<MeshRenderer>(true).sharedMaterial;
                var secondMaterial = second.GetComponentInChildren<MeshRenderer>(true).sharedMaterial;
                Assert.AreNotSame(firstMaterial, secondMaterial, "Opposing ships must not recolour each other's hull.");
                Assert.AreNotSame(source, firstMaterial, "Owner colours must not mutate the prefab material.");
                Assert.AreNotEqual(firstMaterial.GetColor("_TeamColor"), secondMaterial.GetColor("_TeamColor"));
                foreach (var hull in new[] { first, second })
                    foreach (var renderer in hull.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        Assert.AreSame(hull == first ? firstMaterial : secondMaterial, renderer.sharedMaterial, "All LODs retain the same owner.");
                        foreach (string texture in new[] { "_BaseMap", "_BumpMap", "_MaskMap" })
                            Assert.AreSame(source.GetTexture(texture), renderer.sharedMaterial.GetTexture(texture), "Recolouring preserves " + texture);
                    }
            }
            finally { Object.DestroyImmediate(parent); }
        }

        [Test]
        public void EveryFillerHasAFarLevelAndStandsUpright()
        {
            int fillers = 0;
            foreach (var entry in MeshyPropVisuals.Entries)
            {
                if (entry.role != "obstacle") continue;
                fillers++;
                var prefab = Resources.Load<GameObject>(entry.prefab);
                var near = prefab.GetComponent<MeshFilter>().sharedMesh;
                var farLevel = prefab.transform.Find("LOD1");
                Assert.IsNotNull(farLevel, entry.id + " has a far level for the phone tiers");
                var far = farLevel.GetComponent<MeshFilter>().sharedMesh;
                int nearTriangles = near.triangles.Length / 3, farTriangles = far.triangles.Length / 3;
                Assert.That(farTriangles, Is.InRange(nearTriangles * .35f, nearTriangles * .55f), entry.id + " far triangles");
                Assert.AreEqual(farTriangles, entry.farTriangles, entry.id + " catalogue");
                Assert.AreSame(prefab.GetComponent<MeshRenderer>().sharedMaterial, farLevel.GetComponent<MeshRenderer>().sharedMaterial, entry.id);
                // Decimation may shave a crown's tips and the far level drops the smallest chunks, but not the silhouette's box.
                Assert.That((far.bounds.size - near.bounds.size).magnitude, Is.LessThan(near.bounds.size.magnitude * .12f), entry.id + " far level keeps the box");
                // The one-level fillers were once baked lying on their sides: a tree or palm is taller than it is wide.
                if (entry.kind == "thicket" || entry.kind == "palm")
                    Assert.That(near.bounds.size.y, Is.GreaterThan(Mathf.Max(near.bounds.size.x, near.bounds.size.z) * 1.5f), entry.id + " stands upright");
                else Assert.That(near.bounds.size.y, Is.LessThan(1.1f), entry.id + " is a low rock");
                Assert.AreEqual(near.bounds.size.y, entry.height, .01f, entry.id + " height in the catalogue");
            }
            if (fillers == 0) Assert.Ignore("No Meshy fillers baked yet.");
        }

        [Test]
        public void ObstaclesOfOneBiomeShareMeshesAndOtherBiomesFallBack()
        {
            bool any = false;
            foreach (var entry in MeshyPropVisuals.Entries) any |= entry.role == "obstacle" && entry.biome == "forest";
            if (!any) Assert.Ignore("No forest obstacles baked yet.");
            Assert.IsTrue(MeshyPropVisuals.TryObstacle("thicket", "forest", 12345u, out var first, out var material));
            Assert.IsTrue(MeshyPropVisuals.TryObstacle("thicket", "forest", 12345u, out var again, out var sameMaterial));
            Assert.AreSame(first, again, "one seed, one variant: the forest must not reshuffle between loads");
            Assert.AreSame(material, sameMaterial, "copies share a material, which is what lets them instance");
            Assert.IsTrue(material.enableInstancing);
            Assert.IsFalse(MeshyPropVisuals.TryObstacle("thicket", "moon", 1u, out _, out _), "an unknown biome keeps the procedural filler");
            Assert.IsTrue(MeshyPropVisuals.TryObstacle("thicket", "forest", 12345u, out var near, out var far, out var shared));
            Assert.AreSame(first, near); Assert.AreSame(material, shared);
            Assert.IsNotNull(far, "the loader hands out the far level with the near one");
            Assert.Less(far.triangles.Length, near.triangles.Length);
        }

        [Test]
        public void EachCultureClimbsItsOwnLadderAndTheRestBorrowTheKingdoms()
        {
            var kingdom = MeshyPropVisuals.ResolveSiege("siege_ladder", FactionKind.AvenCompact);
            if (kingdom == null) Assert.Ignore("No Meshy siege ladder baked yet.");
            Assert.AreEqual("siege_ladder", kingdom.id);
            Assert.AreEqual("siege_ladder", MeshyPropVisuals.ResolveSiege("siege_ladder", FactionKind.MirajSultanate)?.id,
                "a faction with no ladder of its own borrows the kingdom's rather than dropping to the procedural one");
            var cultures = new[] { (FactionKind.SkeldClans, "mountain_siege_ladder"), (FactionKind.DrakeforgedClans, "dwarf_siege_ladder"),
                (FactionKind.VerdantCovenant, "elf_siege_ladder"), (FactionKind.AshenDominion, "orc_siege_ladder"),
                (FactionKind.DesertSultanate, "sultanate_siege_ladder"), (FactionKind.SahelConfederation, "sahel_siege_ladder") };
            foreach (var (faction, id) in cultures)
            {
                if (!Array.Exists(MeshyPropVisuals.Entries, entry => entry.id == id)) continue;
                Assert.AreEqual(id, MeshyPropVisuals.ResolveSiege("siege_ladder", faction)?.id, faction + " raises its own culture's ladder");
                var root = new GameObject("ladder " + id).transform;
                try { Assert.AreEqual("Meshy " + id, MeshyPropVisuals.TrySiege("siege_ladder", 2, faction, root)?.name); }
                finally { Object.DestroyImmediate(root.gameObject); }
            }
        }
    }
}
