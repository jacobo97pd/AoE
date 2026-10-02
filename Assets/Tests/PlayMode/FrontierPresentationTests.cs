using System;
using Emberfield.Presentation;
using Emberfield.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace Emberfield.Tests.PlayMode
{
    public sealed class FrontierPresentationTests
    {
        private string previousPreviews;
        private bool previousOnline;
        [SetUp] public void Setup()
        { previousPreviews = NativeSmokeStorage.GetPreference("Emberfield.Alpha03.CosmeticPreviews"); previousOnline = CosmeticLoadout.IsOnlineSession; CosmeticLoadout.ClearPreviews(); CosmeticLoadout.SetOnlineEquipment(null); CosmeticLoadout.SetOpponentEquipment(null); }
        [TearDown] public void Cleanup()
        {
            CosmeticLoadout.ClearPreviews(); foreach (string id in previousPreviews.Split('|')) CosmeticLoadout.EquipPreview(id);
            CosmeticLoadout.SetOnlineEquipment(null); CosmeticLoadout.SetOpponentEquipment(null); CosmeticLoadout.IsOnlineSession = previousOnline;
        }
        [TestCase("historical")]
        [TestCase("fantasy")]
        [TestCase("naval")]
        public void EveryFactionLoadsItsAllowedBiomesWithAnOpponentFromItsOwnRealm(string realm)
        {
            foreach (string faction in ContentRealms.FactionsForRealm(realm))
                foreach (string mapId in FrontierCodex.MapsForRealm(realm))
                {
                    var world = DefinitionLoader.CreateOfflineWorld(faction,VictoryMode.Conquest,mapId);
                    Assert.AreEqual(mapId,world.Map.Id); Assert.AreEqual(realm,world.Map.RealmId);
                    foreach (var seat in world.Map.PlayerFactions) Assert.AreEqual(realm,ContentRealms.RealmForFaction(seat.FactionId));
                    for (int i = 0; i < 5; i++) world.Tick();
                    Assert.AreEqual(5,world.TickIndex);
                }
        }
        [Test] public void CosmeticPreviewDoesNotChangeCombatObservationOrCompetitiveDefinition()
        {
            var world = DefinitionLoader.CreateOfflineWorld("drakeforged",VictoryMode.Conquest);
            string rules = JsonUtility.ToJson(world.Definition), before = JsonUtility.ToJson(NetworkObservation.Export(world,1,1));
            foreach (var item in CosmeticLoadout.Catalog) Assert.IsTrue(CosmeticLoadout.EquipPreview(item.id));
            Assert.AreEqual(rules,JsonUtility.ToJson(world.Definition));
            Assert.AreEqual(before,JsonUtility.ToJson(NetworkObservation.Export(world,1,1)));
            var comparison = DefinitionLoader.CreateOfflineWorld("drakeforged",VictoryMode.Conquest);
            world.Submit(new TrainCommand(1,100,"tender")); comparison.Submit(new TrainCommand(1,100,"tender"));
            for (int i = 0; i < 200; i++) { world.Tick(); comparison.Tick(); }
            Assert.AreEqual(JsonUtility.ToJson(NetworkObservation.Export(comparison,1,2)),JsonUtility.ToJson(NetworkObservation.Export(world,1,2)));
        }
        [Test] public void LocalPreviewsCannotImpersonateOwnedOnlineEquipmentOrRecolorAnOfflineOpponent()
        {
            Assert.IsTrue(CosmeticLoadout.EquipPreview("drake_frost")); CosmeticLoadout.IsOnlineSession = false;
            Assert.AreEqual("sapphire_frost",CosmeticLoadout.Resolve("ember_drake","fantasy",1).Id);
            Assert.IsTrue(CosmeticLoadout.Resolve("ember_drake","fantasy",2).IsDefault);
            CosmeticLoadout.IsOnlineSession = true;
            Assert.IsTrue(CosmeticLoadout.Resolve("ember_drake","fantasy",1).IsDefault);
            CosmeticLoadout.SetOnlineEquipment(new[] { new CosmeticEquippedItem { itemId = "drake_ember",slot = "creature",targetId = "ember_drake" } });
            CosmeticLoadout.SetOpponentEquipment(new[] { new CosmeticEquippedItem { itemId = "drake_void",slot = "creature",targetId = "ember_drake" } });
            Assert.AreEqual("ember_crown",CosmeticLoadout.Resolve("ember_drake","fantasy",1).Id);
            Assert.AreEqual("void_scale",CosmeticLoadout.Resolve("ember_drake","fantasy",2).Id);
            Assert.IsTrue(CosmeticLoadout.Resolve("ember_drake","historical",1).IsDefault);
        }
        [Test] public void InvalidTargetsCannotSmuggleAnOwnedCosmeticOntoAnotherUnit()
        {
            CosmeticLoadout.IsOnlineSession = true;
            CosmeticLoadout.SetOnlineEquipment(new[] { new CosmeticEquippedItem { itemId = "drake_frost",slot = "creature",targetId = "dune_elephant" } });
            Assert.IsTrue(CosmeticLoadout.Resolve("dune_elephant","historical").IsDefault);
            Assert.IsTrue(CosmeticLoadout.Resolve("ember_drake","fantasy").IsDefault);
            Assert.IsFalse(CosmeticLoadout.EquipPreview("unknown_product"));
        }
        [Test] public void CatalogOnlyContainsKnownVisualStylesAndUniqueProducts()
        {
            var seen = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            Assert.GreaterOrEqual(CosmeticLoadout.Catalog.Count,12);
            foreach (var item in CosmeticLoadout.Catalog)
            {
                Assert.IsTrue(seen.Add(item.id)); Assert.IsFalse(CosmeticLoadout.Style(item.styleId).IsDefault,item.id);
                CollectionAssert.Contains(new[] { "shared","historical","fantasy","naval" },item.realmId);
                CollectionAssert.Contains(new[] { "architecture","creature","banner","character" },item.slot);
            }
        }

        [Test] public void NewFactionKindsDoNotResolveFantasyCosmeticsForHistoricalOrNavalArmies()
        {
            CosmeticCatalogItem appearance = null;
            foreach (var item in CosmeticLoadout.Catalog) if (item.realmId == "fantasy" && item.slot == "architecture") { appearance = item; break; }
            Assert.IsNotNull(appearance);
            CosmeticLoadout.IsOnlineSession = true;
            CosmeticLoadout.SetOnlineEquipment(new[] { new CosmeticEquippedItem { itemId = appearance.id, slot = appearance.slot, targetId = appearance.targetId } });
            var model = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var renderer = model.GetComponent<MeshRenderer>(); var block = new MaterialPropertyBlock();
                string definition = appearance.targetId == "*" ? "keep" : appearance.targetId;
                AlphaWorldArt.ApplyOwnedCosmetic(model.transform, definition, FactionKind.DrakeforgedClans, 1);
                renderer.GetPropertyBlock(block); Assert.AreEqual(1, block.GetFloat("_CosmeticBlend"));
                foreach (var faction in new[] { FactionKind.EnglishKingdom, FactionKind.PirateBrotherhood })
                {
                    AlphaWorldArt.ApplyOwnedCosmetic(model.transform, definition, faction, 1);
                    renderer.GetPropertyBlock(block); Assert.AreEqual(0, block.GetFloat("_CosmeticBlend"), faction.ToString());
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(model); }
        }
    }
}
