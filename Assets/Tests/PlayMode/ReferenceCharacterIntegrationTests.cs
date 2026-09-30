using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Emberfield.Presentation;
using Emberfield.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Emberfield.Tests.PlayMode
{
    public sealed class ReferenceCharacterIntegrationTests
    {
        private GameObject instance;
        private Mesh sampled;
        private WorldView cosmeticView;
        private bool changedCosmetics, previousOnline;
        private CosmeticEquippedItem[] previousOwnEquipment, previousOpponentEquipment;

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            cosmeticView?.Dispose(); cosmeticView = null;
            if (sampled != null) Object.Destroy(sampled);
            if (instance != null) Object.Destroy(instance);
            if (changedCosmetics)
            {
                CosmeticLoadout.SetOnlineEquipment(previousOwnEquipment);
                CosmeticLoadout.SetOpponentEquipment(previousOpponentEquipment);
                CosmeticLoadout.IsOnlineSession = previousOnline;
                changedCosmetics = false;
            }
            sampled = null; instance = null;
            yield return null;
        }

        [Test]
        public void CollectionHas26DistinctLoadableRiggedPrefabsWithThreeDecreasingLodsAndPbrMaps()
        {
            var entries = ReferenceCharacterVisuals.Entries;
            Assert.That(entries, Has.Length.EqualTo(26), "Bake the complete collection before this acceptance suite.");
            CollectionAssert.AreEquivalent(Enumerable.Range(1, 26).Select(index => "figure_" + index.ToString("00")), entries.Select(entry => entry.id));
            Assert.That(entries.Select(entry => entry.prefabResourcePath).Distinct().Count(), Is.EqualTo(26));
            foreach (var entry in entries)
            {
                string context = entry.id + " / " + entry.name;
                Assert.That(ContentRealms.IsValidRealm(entry.realm), Is.True, context);
                Assert.That(entry.name, Is.Not.Null.And.Not.Empty, context);
                Assert.That(entry.role, Is.Not.Null.And.Not.Empty, context);
                Assert.That(entry.referenceImage, Is.Not.Null.And.Not.Empty, context);
                Assert.That(entry.heightMetres, Is.GreaterThan(.1f), context);
                var prefab = ReferenceCharacterVisuals.Prefab(entry.id);
                Assert.That(prefab, Is.Not.Null, context + " Resources prefab");
                Assert.That(prefab.GetComponent<CorsairAnimationDriver>(), Is.Not.Null, context + " animation driver");
                var animator = prefab.GetComponent<Animator>();
                Assert.That(animator, Is.Not.Null, context + " Animator");
                Assert.That(animator.avatar, Is.Not.Null, context + " avatar");
                Assert.That(animator.avatar.isValid, Is.True, context + " valid skeleton avatar");
                Assert.That(animator.applyRootMotion, Is.False, context + " simulation owns translation");
                Assert.That(animator.runtimeAnimatorController, Is.Not.Null, context + " controller");
                var clips = animator.runtimeAnimatorController.animationClips;
                var run = clips.FirstOrDefault(clip => clip != null && clip.name == "Corsair_Run");
                var death = clips.FirstOrDefault(clip => clip != null && clip.name == "Corsair_Death");
                Assert.That(run, Is.Not.Null, context + " Run clip");
                Assert.That(death, Is.Not.Null, context + " Death clip");
                Assert.That(run.length, Is.GreaterThan(0), context);
                Assert.That(death.length, Is.GreaterThan(0), context);
                Assert.That(run.isLooping, Is.True, context + " running loops");
                Assert.That(death.isLooping, Is.False, context + " falling is one-shot");

                var group = prefab.GetComponent<LODGroup>();
                Assert.That(group, Is.Not.Null, context + " LOD group");
                var lods = group.GetLODs(); Assert.That(lods, Has.Length.EqualTo(3), context);
                var membership = new HashSet<Renderer>();
                ulong previousTriangles = ulong.MaxValue;
                float previousTransition = 1;
                foreach (var lod in lods)
                {
                    Assert.That(lod.screenRelativeTransitionHeight, Is.GreaterThan(0).And.LessThan(previousTransition), context + " ordered LOD thresholds");
                    previousTransition = lod.screenRelativeTransitionHeight;
                    Assert.That(lod.renderers, Is.Not.Empty, context + " populated LOD");
                    ulong triangles = 0;
                    foreach (var renderer in lod.renderers)
                    {
                        Assert.That(renderer, Is.InstanceOf<SkinnedMeshRenderer>(), context + " skinned LOD");
                        Assert.That(membership.Add(renderer), Is.True, context + " no renderer duplicated between LODs");
                        var skin = (SkinnedMeshRenderer)renderer;
                        Assert.That(skin.sharedMesh, Is.Not.Null, context);
                        Assert.That(skin.sharedMesh.vertexCount, Is.GreaterThan(0), context);
                        Assert.That(skin.bones.Length, Is.GreaterThan(1), context + " skin has a skeleton");
                        Assert.That(skin.bones.All(bone => bone != null && bone.IsChildOf(prefab.transform)), Is.True, context + " bones belong to this prefab");
                        Assert.That(skin.sharedMesh.bindposes.Length, Is.EqualTo(skin.bones.Length), context + " bind pose matches bone palette");
                        for (int submesh = 0; submesh < skin.sharedMesh.subMeshCount; submesh++) triangles += skin.sharedMesh.GetIndexCount(submesh) / 3;
                    }
                    Assert.That(triangles, Is.GreaterThan(0).And.LessThan(previousTriangles), context + " lower LOD reduces geometry");
                    previousTriangles = triangles;
                }
                var materials = membership.SelectMany(renderer => renderer.sharedMaterials).Distinct().ToArray();
                Assert.That(materials, Is.Not.Empty, context + " material set");
                foreach (var material in materials)
                {
                    Assert.That(material, Is.Not.Null, context);
                    Assert.That(material.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"), context + " PBR shader");
                    Assert.That(material.GetTexture("_BaseMap"), Is.Not.Null, context + " " + material.name + " base map");
                    Assert.That(material.GetFloat("_Metallic"), Is.InRange(0f, 1f), context);
                    Assert.That(material.GetFloat("_Smoothness"), Is.InRange(0f, 1f), context);
                    if (material.GetTexture("_BumpMap") != null) Assert.That(material.IsKeywordEnabled("_NORMALMAP"), Is.True, context + " active normal map");
                    if (material.GetTexture("_MetallicGlossMap") != null) Assert.That(material.IsKeywordEnabled("_METALLICSPECGLOSSMAP"), Is.True, context + " active metallic/smoothness map");
                }
                // Dielectrics may use scalar metallic/smoothness; a metallic texture is not required on skin or cloth.
                Assert.That(materials.Any(material => material.GetTexture("_BumpMap") != null), Is.True, context + " surface normal detail");
            }
        }

        [Test]
        public void DefaultUnitMappingsKeepRealmAndRoleWhileFootRidersMayRemainGalleryOnly()
        {
            var rulesAsset = Resources.Load<TextAsset>("Definitions/greybox"); Assert.That(rulesAsset, Is.Not.Null);
            var rules = JsonUtility.FromJson<GameDefinition>(rulesAsset.text);
            var resolved = new HashSet<string>();
            var roles = new Dictionary<string, string[]> {
                { "tender", new[] { "worker" } }, { "reedguard", new[] { "warrior", "guard" } },
                { "stringwarden", new[] { "archer" } }, { "threadkeeper", new[] { "mage" } },
                { "frostguard", new[] { "pikeman", "hero" } }, { "ember_drake", new[] { "metallic", "glacial", "igneous" } }
            };
            foreach (var faction in rules.Factions)
            {
                string realm = ContentRealms.RealmForFaction(faction.Id);
                foreach (var definition in rules.Units)
                {
                    string id = ReferenceCharacterVisuals.Resolve(definition.Id, faction.Kind, realm);
                    if (id == null) continue;
                    var entry = ReferenceCharacterVisuals.Find(id);
                    Assert.That(entry, Is.Not.Null, faction.Id + " / " + definition.Id + " maps to catalog content");
                    Assert.That(entry.realm, Is.EqualTo(realm), id + " never crosses competitive realms");
                    Assert.That(roles.ContainsKey(definition.Id), Is.True, id + " has a supported gameplay role");
                    CollectionAssert.Contains(roles[definition.Id], entry.role, faction.Id + " / " + definition.Id);
                    if (definition.Id == "ember_drake") Assert.That(entry.family, Is.EqualTo("dragon"));
                    resolved.Add(id);
                }
            }
            Assert.That(resolved.Count, Is.GreaterThan(0), "Collection must integrate some playable roles.");
            Assert.That(ReferenceCharacterVisuals.Resolve("ember_drake", FactionKind.SolarKingdom, ContentRealms.Historical), Is.Null);
            Assert.That(ReferenceCharacterVisuals.Resolve("tender", FactionKind.AvenCompact, ContentRealms.Fantasy), Is.Null);
            Assert.That(ReferenceCharacterVisuals.Resolve("tender", FactionKind.AvenCompact, "unknown"), Is.Null);
            foreach (var entry in ReferenceCharacterVisuals.Entries.Where(entry => entry.role == "rider"))
                Assert.That(resolved.Contains(entry.id), Is.False, entry.id + " is a rider on foot, not a substitute for mounted cavalry");
            // Heroes and dismounted rider interpretations need not all become default gameplay units.
        }

        [Test]
        public void MountainAndPirateMetadataMatchTheirNewRealmsWithoutBorrowingUnavailableNavalOrOrcArt()
        {
            foreach (var entry in ReferenceCharacterVisuals.Entries)
            {
                if (entry.family == "mountain") Assert.That(entry.realm, Is.EqualTo(ContentRealms.Fantasy), entry.id);
                if (entry.family == "pirate") Assert.That(entry.realm, Is.EqualTo(ContentRealms.Naval), entry.id);
            }
            Assert.That(ReferenceCharacterVisuals.Resolve("tender", FactionKind.SkeldClans, ContentRealms.Historical), Is.Null);
            Assert.That(ReferenceCharacterVisuals.Resolve("tender", FactionKind.SkeldClans, ContentRealms.Fantasy), Is.EqualTo("figure_23"));
            foreach (var faction in new[] { FactionKind.AvenCompact, FactionKind.SerevinMarch, FactionKind.EnglishKingdom })
            {
                Assert.That(ReferenceCharacterVisuals.Resolve("reedguard", faction, ContentRealms.Historical), Is.EqualTo("figure_18"));
                Assert.That(ReferenceCharacterVisuals.Resolve("reedguard", faction, ContentRealms.Naval), Is.Null);
            }
            Assert.That(ReferenceCharacterVisuals.Resolve("reedguard", FactionKind.AshenDominion, ContentRealms.Fantasy), Is.Null, "No orc model is available in the reference collection.");
            Assert.That(ReferenceCharacterVisuals.Resolve("gunpowder_corsair", FactionKind.PirateBrotherhood, ContentRealms.Naval), Is.Null, "Existing imported pirate prefabs keep priority.");
        }

        [TestCase(false)] [TestCase(true)]
        public void NewHumanFactionsUseSharedHumanSilhouettesWithoutDwarvenEquipment(bool building)
        {
            instance = new GameObject("Human silhouette regression");
            Transform Create(FactionKind faction) => building
                ? AlphaWorldArt.Building("hearth", faction, instance.transform, 1, 6, 6)
                : AlphaWorldArt.Unit("stringwarden", faction, instance.transform, 1);
            var human = Create(FactionKind.AvenCompact).GetComponent<LODGroup>().GetLODs();
            var dwarf = Create(FactionKind.DrakeforgedClans).GetComponent<LODGroup>().GetLODs();
            foreach (var faction in new[] { FactionKind.EnglishKingdom, FactionKind.PirateBrotherhood })
            {
                var actual = Create(faction).GetComponent<LODGroup>().GetLODs();
                Assert.That(actual.Length, Is.EqualTo(human.Length));
                for (int lod = 0; lod < actual.Length; lod++)
                {
                    var humanMesh = human[lod].renderers[0].GetComponent<MeshFilter>().sharedMesh;
                    var dwarfMesh = dwarf[lod].renderers[0].GetComponent<MeshFilter>().sharedMesh;
                    var actualMesh = actual[lod].renderers[0].GetComponent<MeshFilter>().sharedMesh;
                    Assert.That(dwarfMesh.vertexCount, Is.GreaterThan(humanMesh.vertexCount), "The fixture includes real dwarven adornments.");
                    Assert.That(actualMesh.vertexCount, Is.EqualTo(humanMesh.vertexCount), faction + " LOD" + lod + " must retain the shared human silhouette.");
                    Assert.That(actualMesh.bounds, Is.EqualTo(humanMesh.bounds));
                }
            }
        }

        [UnityTest] public IEnumerator HistoricalWarriorRunsAndFallsThroughItsActualAnimator() => VerifyAnimator("figure_18");
        [UnityTest] public IEnumerator FantasyDwarfRunsAndFallsThroughItsActualAnimator() => VerifyAnimator("figure_12");
        [UnityTest] public IEnumerator DragonRunsAndFallsThroughItsActualAnimator() => VerifyAnimator("figure_06");

        [UnityTest]
        public IEnumerator EquippedDragonKeepsItsAuthoredCosmeticAndRebuildsOnlyWhenReferenceEligibilityChanges()
        {
            previousOnline = CosmeticLoadout.IsOnlineSession;
            CosmeticLoadout.IsOnlineSession = true;
            previousOwnEquipment = CurrentEquipment(1);
            previousOpponentEquipment = CurrentEquipment(2);
            changedCosmetics = true;
            CosmeticLoadout.SetOnlineEquipment(null); CosmeticLoadout.SetOpponentEquipment(null);
            var rules = JsonUtility.FromJson<GameDefinition>(Resources.Load<TextAsset>("Definitions/greybox").text);
            var world = new World(rules, new MapDefinition {
                Id = "faction_proving_ground", RealmId = ContentRealms.Fantasy, WidthCells = 32, HeightCells = 32,
                OfflineMatch = new OfflineMatchDefinition { Enabled = true, CentralBuildingId = "hearth" },
                PlayerFactions = new[] { new PlayerFactionDefinition { PlayerId = 1, FactionId = "drakeforged" },
                    new PlayerFactionDefinition { PlayerId = 2, FactionId = "drakeforged" } },
                UnitSpawns = new[] { new UnitSpawnDefinition { Id = 1, OwnerId = 1, DefinitionId = "ember_drake", Position = new SimPoint(10500, 10500) },
                    new UnitSpawnDefinition { Id = 2, OwnerId = 2, DefinitionId = "ember_drake", Position = new SimPoint(15500, 15500) } },
                BuildingSpawns = new[] {
                    new BuildingSpawnDefinition { Id = 100, OwnerId = 1, DefinitionId = "hearth", Position = new SimPoint(6000, 6000) },
                    new BuildingSpawnDefinition { Id = 101, OwnerId = 2, DefinitionId = "hearth", Position = new SimPoint(26000, 26000) } }
            });
            string before = JsonUtility.ToJson(NetworkObservation.Export(world, 1, 1));
            instance = new GameObject("Reference cosmetic acceptance");
            var cameraRoot = new GameObject("Acceptance camera"); cameraRoot.transform.SetParent(instance.transform, false);
            cosmeticView = new WorldView(world, instance.transform, cameraRoot.AddComponent<Camera>());
            var reference = cosmeticView.RootFor(1);
            Assert.That(reference.GetComponentInChildren<CorsairAnimationDriver>(), Is.Not.Null, "The default appearance uses the rigged reference dragon.");
            var ownMarker = reference.Find("Owner marker").GetComponent<LineRenderer>();
            var rivalMarker = cosmeticView.RootFor(2).Find("Owner marker").GetComponent<LineRenderer>();
            Assert.That(ownMarker.enabled && ownMarker.gameObject.activeSelf, Is.True, "Healthy unselected imported units still show their owner.");
            Assert.That(rivalMarker.sharedMaterial.color, Is.Not.EqualTo(ownMarker.sharedMaterial.color), "Mirror factions need distinct owner markers without altering PBR materials.");
            Assert.That(reference.Find("Health bar").position.y, Is.GreaterThan(reference.GetComponentsInChildren<SkinnedMeshRenderer>().Max(renderer => renderer.bounds.max.y)),
                "The health bar clears the imported silhouette rather than using an obsolete unit height.");
            EquipDragon("drake_frost");
            world.TryGetUnit(1, out var own); world.TryGetUnit(2, out var opponent);
            Assert.That(ReferenceCharacterVisuals.TryCreate(world, own, FactionKind.DrakeforgedClans, instance.transform), Is.Null,
                "A default reference model must not hide an equipped appearance.");
            cosmeticView.Sync(1, Array.Empty<int>());
            var authored = cosmeticView.RootFor(1);
            Assert.That(authored, Is.Not.SameAs(reference));
            Assert.That(reference.gameObject.activeSelf, Is.False, "The replaced visual disappears immediately, before deferred destruction.");
            Assert.That(authored.GetComponentInChildren<CorsairAnimationDriver>(), Is.Null);
            AssertCosmeticColor(authored, CosmeticLoadout.Style("sapphire_frost").Primary);
            Assert.That(CosmeticLoadout.Resolve(opponent.DefinitionId, ContentRealms.Fantasy, 2).IsDefault, Is.True,
                "Our equipped appearance cannot recolor the other player.");
            cosmeticView.Sync(1, Array.Empty<int>());
            Assert.That(cosmeticView.RootFor(1), Is.SameAs(authored), "An unchanged loadout does not recreate visual objects.");
            EquipDragon("drake_ember"); cosmeticView.Sync(1, Array.Empty<int>());
            Assert.That(cosmeticView.RootFor(1), Is.SameAs(authored), "Changing one authored cosmetic to another only updates materials.");
            AssertCosmeticColor(authored, CosmeticLoadout.Style("ember_crown").Primary);
            CosmeticLoadout.SetOnlineEquipment(null); cosmeticView.Sync(1, Array.Empty<int>());
            var restored = cosmeticView.RootFor(1);
            Assert.That(restored, Is.Not.SameAs(authored));
            Assert.That(restored.GetComponentInChildren<CorsairAnimationDriver>().HasValidRig, Is.True);
            Assert.That(JsonUtility.ToJson(NetworkObservation.Export(world, 1, 1)), Is.EqualTo(before),
                "Cosmetic transitions must leave authoritative observation and gameplay state unchanged.");
            yield return null;
        }

        private static CosmeticEquippedItem[] CurrentEquipment(int owner) => CosmeticLoadout.Catalog
            .Where(item => CosmeticLoadout.Resolve(item.targetId, item.realmId, owner).Id == item.styleId)
            .Select(item => new CosmeticEquippedItem { itemId = item.id, slot = item.slot, targetId = item.targetId }).ToArray();

        private static void EquipDragon(string id) => CosmeticLoadout.SetOnlineEquipment(new[] {
            new CosmeticEquippedItem { itemId = id, slot = "creature", targetId = "ember_drake" }
        });

        private static void AssertCosmeticColor(Transform root, Color expected)
        {
            var properties = new MaterialPropertyBlock(); bool found = false;
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
            {
                renderer.GetPropertyBlock(properties);
                if (properties.GetFloat("_CosmeticBlend") <= 0) continue;
                found = true;
                var actual = properties.GetColor("_CosmeticPrimary");
                Assert.That(actual.r, Is.EqualTo(expected.r).Within(.001f));
                Assert.That(actual.g, Is.EqualTo(expected.g).Within(.001f));
                Assert.That(actual.b, Is.EqualTo(expected.b).Within(.001f));
            }
            Assert.That(found, Is.True, "The authored mesh must receive the equipped material treatment.");
        }

        private IEnumerator VerifyAnimator(string id)
        {
            var prefab = ReferenceCharacterVisuals.Prefab(id); Assert.That(prefab, Is.Not.Null, id);
            instance = Object.Instantiate(prefab); instance.GetComponent<LODGroup>().ForceLOD(0);
            var driver = instance.GetComponent<CorsairAnimationDriver>();
            Assert.That(driver.HasValidRig, Is.True, id);
            var skin = instance.GetComponent<LODGroup>().GetLODs()[0].renderers.Cast<SkinnedMeshRenderer>()
                .OrderByDescending(renderer => renderer.sharedMesh.vertexCount).First();
            sampled = new Mesh(); var animator = driver.Animator;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; animator.Rebind(); animator.Update(0);
            yield return null;
            foreach (string state in new[] { "Run", "Death" })
            {
                Assert.That(driver.PlayPreview(state), Is.True, id + " preview " + state);
                animator.Play(state, 0, 0); animator.speed = 1; animator.Update(0);
                skin.BakeMesh(sampled); var before = sampled.vertices; float uprightHeight = WorldHeight(skin, sampled);
                animator.Update(driver.Duration(state) * (state == "Death" ? .97f : .39f));
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName(state), Is.True, id + " entered " + state);
                skin.BakeMesh(sampled); var after = sampled.vertices;
                Assert.That(MaxDisplacement(before, after), Is.GreaterThan(.00001f), id + " " + state + " deforms actual skinned vertices");
                if (state == "Death")
                {
                    if (id == "figure_18") Assert.That(WorldHeight(skin, sampled), Is.LessThan(uprightHeight * .78f),
                        "The historical warrior must actually fall, not merely rotate while remaining upright.");
                    animator.Update(driver.Duration(state)); skin.BakeMesh(sampled); var held = sampled.vertices;
                    animator.Update(driver.Duration(state)); skin.BakeMesh(sampled);
                    Assert.That(MaxDisplacement(held, sampled.vertices), Is.LessThan(.00001f), id + " holds the fallen pose without looping");
                }
                Assert.That(instance.transform.position.sqrMagnitude, Is.LessThan(.000001f), id + " animation must not apply root motion");
            }
            animator.speed = 0;
        }

        private static float MaxDisplacement(Vector3[] before, Vector3[] after)
        {
            Assert.That(after.Length, Is.EqualTo(before.Length)); float movement = 0;
            for (int index = 0; index < before.Length; index += Math.Max(1, before.Length / 2000))
                movement = Mathf.Max(movement, Vector3.Distance(before[index], after[index]));
            return movement;
        }
        private static float WorldHeight(SkinnedMeshRenderer skin, Mesh mesh)
        {
            float minimum = float.PositiveInfinity, maximum = float.NegativeInfinity;
            foreach (var vertex in mesh.vertices)
            { float y = skin.transform.TransformPoint(vertex).y; minimum = Mathf.Min(minimum, y); maximum = Mathf.Max(maximum, y); }
            return maximum - minimum;
        }
    }
}
