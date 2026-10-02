using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Emberfield.Presentation;
using Emberfield.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Emberfield.Tests.PlayMode
{
    /// <summary>
    /// The store's character skins swap one faction's unit model for a model of their own: only for the owner who wears
    /// it, only for that faction and unit, and never as a faction's default. Everything else about the unit (colour,
    /// states, rings, rules) stays as the faction's own model has it.
    /// </summary>
    public sealed class CharacterSkinTests
    {
        private const string DwarfSkin = "char_mountain_guard", ElfSkin = "char_forest_archer", RangerSkin = "char_wandering_ranger";
        private readonly List<GameObject> created = new List<GameObject>();
        private string previousPreviews;
        private bool previousOnline;
        private WorldView view;

        [SetUp]
        public void Setup()
        {
            previousPreviews = NativeSmokeStorage.GetPreference("Emberfield.Alpha03.CosmeticPreviews");
            previousOnline = CosmeticLoadout.IsOnlineSession;
            CosmeticLoadout.ClearPreviews(); CosmeticLoadout.SetOnlineEquipment(null); CosmeticLoadout.SetOpponentEquipment(null);
            CosmeticLoadout.IsOnlineSession = false;
        }

        [TearDown]
        public void Cleanup()
        {
            view?.Dispose(); view = null;
            foreach (var go in created) if (go) Object.Destroy(go);
            created.Clear();
            CosmeticLoadout.ClearPreviews(); foreach (string id in previousPreviews.Split('|')) CosmeticLoadout.EquipPreview(id);
            CosmeticLoadout.SetOnlineEquipment(null); CosmeticLoadout.SetOpponentEquipment(null);
            CosmeticLoadout.IsOnlineSession = previousOnline;
        }

        private static CosmeticEquippedItem Worn(string itemId)
        {
            var item = CosmeticLoadout.Find(itemId);
            return new CosmeticEquippedItem { itemId = item.id, slot = item.slot, targetId = CosmeticLoadout.EquipmentTarget(item) };
        }

        private Transform Parent() { var go = new GameObject("Skin test parent"); created.Add(go); return go.transform; }

        private static UnitState Unit(World world, int owner, string definition)
        {
            foreach (var unit in world.Units) if (unit.OwnerId == owner && unit.DefinitionId == definition && unit.Id >= 600) return unit;
            return null;
        }

        private static string Skinned(CorsairAnimationDriver driver) => driver.GetComponentInChildren<SkinnedMeshRenderer>().sharedMaterial.name;

        [Test]
        public void TheCatalogSellsThreeCharacterSkinsEachForOneFactionsRealUnit()
        {
            var skins = CosmeticLoadout.Catalog.Where(item => item.slot == CosmeticLoadout.CharacterSlot).ToArray();
            CollectionAssert.AreEquivalent(new[] { DwarfSkin, ElfSkin, RangerSkin }, skins.Select(item => item.id).ToArray());
            var rules = JsonUtility.FromJson<GameDefinition>(Resources.Load<TextAsset>("Definitions/greybox").text);
            var models = new HashSet<string>();
            foreach (var item in skins)
            {
                Assert.IsTrue(ContentRealms.IsFactionInRealm(item.factionId, item.realmId), item.id + ": the faction belongs to the item's realm");
                Assert.IsTrue(rules.Units.Any(unit => unit.Id == item.targetId), item.id + ": the target is a unit of the rules");
                Assert.IsFalse(CosmeticLoadout.Style(item.styleId).IsDefault, item.id + " names its store accent");
                Assert.AreEqual(499, item.priceMinor); Assert.AreEqual("EUR", item.currency);
                var entry = MeshyUnitVisuals.ResolveSkin(item.modelId, item.targetId, item.factionId);
                Assert.IsNotNull(entry, item.id + " names a baked skin of its own faction and unit");
                Assert.IsTrue(entry.cosmetic, entry.id + " is flagged as a cosmetic model");
                Assert.IsTrue(models.Add(entry.id), entry.id + " is sold once");
                Assert.IsNotNull(Resources.Load<GameObject>(entry.prefab), entry.id + " prefab");
                var replaced = MeshyUnitVisuals.Resolve(item.targetId, KindOf(item.factionId));
                Assert.IsNotNull(replaced, item.id + " replaces a model the faction really has");
                Assert.AreNotEqual(replaced.id, entry.id);
                Assert.IsFalse(replaced.cosmetic);
            }
            // Every skin in the catalogue is sold, and none is anything else's default.
            foreach (var entry in MeshyUnitVisuals.Entries.Where(e => e.cosmetic)) Assert.IsTrue(models.Contains(entry.id), entry.id + " is for sale");
            foreach (FactionKind kind in Enum.GetValues(typeof(FactionKind)))
                foreach (var unit in rules.Units)
                    Assert.IsTrue(MeshyUnitVisuals.Resolve(unit.Id, kind)?.cosmetic != true, kind + " " + unit.Id + " must never default to a skin");
            // Their names and prices, as the store shows them (Spanish by default).
            Assert.AreEqual("Mountain Guard", CosmeticLoadout.Find(DwarfSkin).displayName);
            Assert.AreEqual("Forest Archer", CosmeticLoadout.Find(ElfSkin).displayName);
            Assert.AreEqual("Wandering Ranger", CosmeticLoadout.Find(RangerSkin).displayName);
        }

        private static FactionKind KindOf(string id) => id switch
        {
            "drakeforged" => FactionKind.DrakeforgedClans, "verdant" => FactionKind.VerdantCovenant, "skeld" => FactionKind.SkeldClans,
            "ashen" => FactionKind.AshenDominion, _ => throw new ArgumentException(id),
        };

        [Test]
        public void EquippingASkinSwapsOnlyThatFactionsUnitAndUnequippingRestoresIt()
        {
            var world = MeshyUnitReview.CreateWorld("drakeforged");
            var guard = Unit(world, 1, "reedguard"); var crossbow = Unit(world, 1, "stringwarden"); var rival = Unit(world, 2, "reedguard");
            Assert.IsNotNull(guard); Assert.IsNotNull(crossbow); Assert.IsNotNull(rival);
            var dwarf = FactionKind.DrakeforgedClans; var clans = FactionKind.SkeldClans;
            string defaultGuard = MeshyUnitVisuals.Resolve("reedguard", dwarf).name;

            var before = MeshyUnitVisuals.TryCreate(world, guard, dwarf, Parent(), out string noSkin);
            Assert.IsNotNull(before); Assert.IsNull(noSkin); Assert.AreEqual(defaultGuard, before.name);

            Assert.IsTrue(CosmeticLoadout.EquipPreview(DwarfSkin));
            var worn = MeshyUnitVisuals.TryCreate(world, guard, dwarf, Parent(), out string skin);
            Assert.IsNotNull(worn); Assert.AreEqual("skin_mountain_guard", skin); Assert.AreEqual("Guerrero de la Montaña", worn.name);
            StringAssert.StartsWith("skin_mountain_guard", Skinned(worn));
            // Nothing else changes: the same faction's other unit, the rival's unit of the same definition, and the default's catalogue entry.
            Assert.AreEqual(MeshyUnitVisuals.Resolve("stringwarden", dwarf).name, MeshyUnitVisuals.TryCreate(world, crossbow, dwarf, Parent()).name);
            Assert.AreEqual(MeshyUnitVisuals.Resolve("reedguard", clans).name, MeshyUnitVisuals.TryCreate(world, rival, clans, Parent()).name,
                "A skin never reaches the other player's unit: previews are the local player's.");
            Assert.AreEqual(defaultGuard, MeshyUnitVisuals.Resolve("reedguard", dwarf).name);
            // The clans' ranger is for the clans: wearing it as the dwarves changes nothing.
            Assert.IsTrue(CosmeticLoadout.EquipPreview(RangerSkin));
            Assert.AreEqual("Guerrero de la Montaña", MeshyUnitVisuals.TryCreate(world, guard, dwarf, Parent()).name, "Both skins are worn together, one per faction.");

            CosmeticLoadout.ClearPreviews();
            Assert.AreEqual(defaultGuard, MeshyUnitVisuals.TryCreate(world, guard, dwarf, Parent(), out string cleared).name);
            Assert.IsNull(cleared);
        }

        [Test]
        public void ASkinKeepsTheOwnersColourItsRigAndEveryStateOfTheUnitItDresses()
        {
            var cases = new[] { ("drakeforged", "reedguard", DwarfSkin, false), ("verdant", "stringwarden", ElfSkin, true), ("skeld", "reedguard", RangerSkin, false) };
            foreach (var (faction, unitId, itemId, aims) in cases)
            {
                var world = MeshyUnitReview.CreateWorld(faction);
                var unit = Unit(world, 1, unitId); Assert.IsNotNull(unit, faction + " " + unitId);
                Assert.IsTrue(CosmeticLoadout.EquipPreview(itemId));
                var driver = MeshyUnitVisuals.TryCreate(world, unit, KindOf(faction), Parent());
                Assert.IsNotNull(driver, itemId); Assert.IsTrue(driver.HasValidRig, itemId + " rig");
                foreach (string state in new[] { "Idle", "Walk", "Run", "Attack", "Hit", "Death", "Climb" })
                    Assert.IsTrue(driver.HasState(state), itemId + " plays " + state + " like the unit it dresses (reedguard and stringwarden board walls)");
                Assert.AreEqual(aims, driver.HasState("Aim"), itemId + " aims where the default archer does");
                var skinned = driver.GetComponentInChildren<SkinnedMeshRenderer>();
                Assert.AreEqual("Emberfield/Meshy Unit", skinned.sharedMaterial.shader.name);
                Color team = skinned.sharedMaterial.GetColor("_TeamColor"), expected = AlphaWorldArt.OwnerColor(1);
                Assert.That(Vector4.Distance(team, expected), Is.LessThan(.001f), itemId + " takes the owner's colour");
                var baseMap = skinned.sharedMaterial.GetTexture("_BaseMap");
                Assert.IsTrue(baseMap != null && baseMap.width <= 1024, itemId + " base map at most 1024");
                // A walking pace of its own, planted at the switch like every other model (AnimationDriverTests walks the whole catalogue).
                Assert.Greater(driver.WalkMetresPerSecond, .3f); Assert.GreaterOrEqual(driver.RunMetresPerSecond, driver.WalkMetresPerSecond * 1.5f);
                CosmeticLoadout.ClearPreviews();
            }
        }

        [Test]
        public void OnlineSkinsDressBothOwnersFromTheirOwnWardrobeAndOnlyForTheirFaction()
        {
            var world = MeshyUnitReview.CreateWorld("drakeforged");   // player 1 the Drakeforged, player 2 the Skeld clans
            var own = Unit(world, 1, "reedguard"); var rival = Unit(world, 2, "reedguard");
            var dwarf = FactionKind.DrakeforgedClans; var clans = FactionKind.SkeldClans;
            string dwarfDefault = MeshyUnitVisuals.Resolve("reedguard", dwarf).name, clansDefault = MeshyUnitVisuals.Resolve("reedguard", clans).name;
            CosmeticLoadout.IsOnlineSession = true;
            // A preview is only for offline play; it never dresses an online match.
            Assert.IsTrue(CosmeticLoadout.EquipPreview(DwarfSkin));
            Assert.AreEqual(dwarfDefault, MeshyUnitVisuals.TryCreate(world, own, dwarf, Parent()).name);
            // The account's own wardrobe dresses owner 1, the opponent's dresses owner 2; each by the server's own equipment target.
            CosmeticLoadout.SetOnlineEquipment(new[] { Worn(DwarfSkin) });
            CosmeticLoadout.SetOpponentEquipment(new[] { Worn(RangerSkin) });
            Assert.AreEqual("drakeforged:reedguard", Worn(DwarfSkin).targetId);
            Assert.AreEqual("Guerrero de la Montaña", MeshyUnitVisuals.TryCreate(world, own, dwarf, Parent()).name);
            Assert.AreEqual("Explorador Errante", MeshyUnitVisuals.TryCreate(world, rival, clans, Parent()).name);
            // Swapped between the owners, neither skin fits the other faction.
            CosmeticLoadout.SetOnlineEquipment(new[] { Worn(RangerSkin) });
            CosmeticLoadout.SetOpponentEquipment(new[] { Worn(DwarfSkin) });
            Assert.AreEqual(dwarfDefault, MeshyUnitVisuals.TryCreate(world, own, dwarf, Parent()).name);
            Assert.AreEqual(clansDefault, MeshyUnitVisuals.TryCreate(world, rival, clans, Parent()).name);
            // Equipment that names the wrong target, or an unknown item, is not worn.
            CosmeticLoadout.SetOnlineEquipment(new[] { new CosmeticEquippedItem { itemId = DwarfSkin, slot = "character", targetId = "reedguard" },
                new CosmeticEquippedItem { itemId = DwarfSkin, slot = "creature", targetId = "drakeforged:reedguard" }, new CosmeticEquippedItem { itemId = "nothing", slot = "character", targetId = "x" } });
            Assert.AreEqual(dwarfDefault, MeshyUnitVisuals.TryCreate(world, own, dwarf, Parent()).name);
            // Both skins of one account are worn together (the server keeps one per faction and unit).
            CosmeticLoadout.SetOnlineEquipment(new[] { Worn(DwarfSkin), Worn(RangerSkin), Worn(ElfSkin) });
            Assert.AreEqual("Guerrero de la Montaña", MeshyUnitVisuals.TryCreate(world, own, dwarf, Parent()).name);
            Assert.IsNull(CosmeticLoadout.ResolveCharacter("reedguard", "verdant", "fantasy", 1), "No skin was made for the Verdant reedguard.");
            Assert.IsNull(CosmeticLoadout.ResolveCharacter("reedguard", "drakeforged", "historical", 1), "A skin does not cross realms.");
            Assert.IsNull(CosmeticLoadout.ResolveCharacter("reedguard", "drakeforged", "fantasy", 3), "Only the two seats of a match are dressed.");
            // A colour style for the unit does not make the skin disappear, and a skin does not count as a colour style.
            Assert.IsTrue(CosmeticLoadout.Resolve("reedguard", "fantasy", 1).IsDefault);
        }

        [UnityTest]
        public IEnumerator TheViewRedrawsOnlyTheUnitsASkinDressesWhenTheLoadoutChanges()
        {
            var rules = JsonUtility.FromJson<GameDefinition>(Resources.Load<TextAsset>("Definitions/greybox").text);
            var world = new World(rules, new MapDefinition
            {
                Id = "faction_proving_ground", RealmId = ContentRealms.Fantasy, WidthCells = 32, HeightCells = 32,
                OfflineMatch = new OfflineMatchDefinition { Enabled = true, CentralBuildingId = "hearth" },
                PlayerFactions = new[] { new PlayerFactionDefinition { PlayerId = 1, FactionId = "drakeforged" }, new PlayerFactionDefinition { PlayerId = 2, FactionId = "skeld" } },
                UnitSpawns = new[] {
                    new UnitSpawnDefinition { Id = 1, OwnerId = 1, DefinitionId = "reedguard", Position = new SimPoint(10500, 10500) },
                    new UnitSpawnDefinition { Id = 2, OwnerId = 1, DefinitionId = "stringwarden", Position = new SimPoint(11500, 10500) },
                    new UnitSpawnDefinition { Id = 3, OwnerId = 2, DefinitionId = "reedguard", Position = new SimPoint(12500, 10500) } },
                BuildingSpawns = new[] {
                    new BuildingSpawnDefinition { Id = 100, OwnerId = 1, DefinitionId = "hearth", Position = new SimPoint(6000, 6000) },
                    new BuildingSpawnDefinition { Id = 101, OwnerId = 2, DefinitionId = "hearth", Position = new SimPoint(26000, 26000) } }
            });
            string rulesBefore = JsonUtility.ToJson(world.Definition), stateBefore = JsonUtility.ToJson(NetworkObservation.Export(world, 1, 1));
            var holder = new GameObject("Skin view test"); created.Add(holder);
            var cameraRoot = new GameObject("Skin view camera"); cameraRoot.transform.SetParent(holder.transform, false);
            view = new WorldView(world, holder.transform, cameraRoot.AddComponent<Camera>());
            var none = Array.Empty<int>();
            var guard = view.RootFor(1); var archer = view.RootFor(2); var rival = view.RootFor(3);
            Assert.IsNotNull(guard); Assert.IsNotNull(archer); Assert.IsNotNull(rival);
            string defaultGuard = MeshyUnitVisuals.Resolve("reedguard", FactionKind.DrakeforgedClans).name;
            Assert.AreEqual(defaultGuard, guard.GetComponentInChildren<CorsairAnimationDriver>().name);

            CosmeticLoadout.EquipPreview(DwarfSkin); view.Sync(1, none);
            var dressed = view.RootFor(1);
            Assert.AreNotSame(guard, dressed, "The dressed unit is drawn again with its skin.");
            Assert.IsFalse(guard.gameObject.activeSelf, "The model it replaces disappears at once.");
            Assert.AreEqual("Guerrero de la Montaña", dressed.GetComponentInChildren<CorsairAnimationDriver>().name);
            Assert.AreSame(archer, view.RootFor(2), "The same faction's archer is left alone."); Assert.AreSame(rival, view.RootFor(3), "So is the rival's reedguard.");
            Assert.IsNotNull(dressed.Find("Owner marker"), "The owner's ring still shows under the skin."); Assert.IsNotNull(dressed.Find("Health bar"));
            var driver = dressed.GetComponentInChildren<CorsairAnimationDriver>();
            Assert.IsTrue(driver.HasValidRig && driver.HasState("Walk") && driver.HasState("Attack"), "The animation driver's states work for the skin.");
            // The unit moves and fights as before: the simulation never saw the skin.
            Assert.AreEqual(rulesBefore, JsonUtility.ToJson(world.Definition)); Assert.AreEqual(stateBefore, JsonUtility.ToJson(NetworkObservation.Export(world, 1, 1)));
            view.Sync(1, none); Assert.AreSame(dressed, view.RootFor(1), "An unchanged loadout does not draw it again.");

            CosmeticLoadout.ClearPreviews(); view.Sync(1, none);
            var restored = view.RootFor(1);
            Assert.AreNotSame(dressed, restored); Assert.AreEqual(defaultGuard, restored.GetComponentInChildren<CorsairAnimationDriver>().name);
            Assert.AreSame(archer, view.RootFor(2)); Assert.AreSame(rival, view.RootFor(3));
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheStorePreviewShowsTheSkinModelAndPlaysItsIdle()
        {
            foreach (string id in new[] { DwarfSkin, ElfSkin, RangerSkin })
            {
                var item = CosmeticLoadout.Find(id);
                var canvas = new GameObject("Preview canvas", typeof(Canvas)); created.Add(canvas);
                var rect = new GameObject("Live cosmetic model", typeof(RectTransform)).GetComponent<RectTransform>();
                rect.SetParent(canvas.transform, false); rect.sizeDelta = new Vector2(380, 420);
                var picture = rect.gameObject.AddComponent<RawImage>();
                var preview = rect.gameObject.AddComponent<CosmeticModelPreview>(); preview.Initialize(picture, item);
                Assert.IsNotNull(preview.Character, id + " shows its own Meshy model, not the procedural unit");
                Assert.AreEqual(item.displayName == "Mountain Guard" ? "Guerrero de la Montaña" : item.displayName == "Forest Archer" ? "Arquero del Bosque" : "Explorador Errante", preview.Character.name);
                yield return null;
                preview.RenderPreview();
                Assert.IsTrue(preview.HasRendered, id + " renders");
                var animator = preview.Character.Animator;
                Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), id + " stands in its idle");
                float start = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                yield return new WaitForSecondsRealtime(.35f);
                preview.RenderPreview();
                Assert.AreNotEqual(start, animator.GetCurrentAnimatorStateInfo(0).normalizedTime, id + " idle keeps playing, on unscaled time");
                // Something other than the studio background is on the picture, and it is not the whole picture.
                var target = (RenderTexture)picture.texture; var previous = RenderTexture.active; var read = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
                RenderTexture.active = target; read.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); read.Apply(); RenderTexture.active = previous;
                var background = new Color(.14f, .21f, .24f); int figure = 0, edge = 0; var pixels = read.GetPixels();
                for (int i = 0; i < pixels.Length; i++)
                {
                    var pixel = pixels[i];
                    if (Mathf.Abs(pixel.r - background.r) + Mathf.Abs(pixel.g - background.g) + Mathf.Abs(pixel.b - background.b) <= .12f) continue;
                    figure++;
                    int x = i % target.width, y = i / target.width;
                    if (x < 2 || y < 2 || x >= target.width - 2 || y >= target.height - 2) edge++;
                }
                // Kept with the run's other evidence, for a look at how the store frames each skin.
                try { System.IO.File.WriteAllBytes(System.IO.Path.Combine(Application.dataPath, "../TestResults/skin-preview-" + id + ".png"), read.EncodeToPNG()); } catch (System.IO.IOException) { }
                Object.Destroy(read);
                float share = figure / (float)pixels.Length;
                Assert.That(share, Is.InRange(.05f, .75f), id + " fills a sensible part of the picture, share " + share.ToString("0.00"));
                Assert.AreEqual(0, edge, id + " is framed whole: nothing of it touches the picture's edge");
                Object.Destroy(canvas); created.Remove(canvas);
            }
        }
    }
}
