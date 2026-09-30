using System;
using System.Collections;
using System.Collections.Generic;
using Emberfield.Presentation;
using Emberfield.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Emberfield.Tests.PlayMode
{
    public sealed class NavalPresentationTests
    {
        private GameObject root;
        private MatchController match;
        private const int Worker = 1, Raider = 2, Ship = 10, EnemyShip = 30;

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (root != null) UnityEngine.Object.Destroy(root);
            yield return null;
        }

        // Keep the normal controller, HUD and input services, but replace its practice world with a small
        // deterministic shore. No scene asset, production cost, live setting or user save is changed.
        private void Create()
        {
            root = new GameObject("Naval presentation test"); root.SetActive(false);
            match = root.AddComponent<MatchController>(); match.enabled = false;
            root.SetActive(true); match.Shell?.Close();
            var rules = JsonUtility.FromJson<GameDefinition>(JsonUtility.ToJson(match.World.Definition));
            rules.StartingResources = new ResourceAmount(5000, 5000, 5000, 5000);
            rules.BasePopulationCapacity = 100;
            var blocked = new List<GridCell>(); var water = new List<GridCell>();
            for (int z = 9; z <= 14; z++) for (int x = 0; x < 40; x++)
            {
                water.Add(new GridCell(x, z));
                if (x != 30 || z != 9) blocked.Add(new GridCell(x, z)); // An open ford still belongs to feet.
            }
            blocked.Add(new GridCell(30, 18)); // Dry rock must not be painted as sea.
            var map = new MapDefinition
            {
                Id = "sapphire_coast", RealmId = ContentRealms.Naval, BiomeId = "caribbean",
                WidthCells = 40, HeightCells = 24, CellSizeMillimetres = 1000,
                BlockedCells = blocked.ToArray(), WaterCells = water.ToArray(),
                PlayerFactions = new[] { new PlayerFactionDefinition { PlayerId = 1, FactionId = "pirates" }, new PlayerFactionDefinition { PlayerId = 2, FactionId = "pirates" } },
                UnitSpawns = new[]
                {
                    Spawn(Worker, 1, "treasure_seeker", 15500, 8500),
                    Spawn(Raider, 1, "boarding_raider", 11500, 6500),
                    Spawn(Ship, 1, "pirate_sloop", 15500, 9500),
                    Spawn(EnemyShip, 2, "pirate_sloop", 34500, 13500)
                }
            };
            foreach (var canvas in root.GetComponentsInChildren<Canvas>()) canvas.gameObject.SetActive(false);
            match.Hud.Dispose(); match.View.Dispose();
            typeof(MatchController).GetProperty(nameof(MatchController.World)).SetValue(match, new World(rules, map));
            typeof(MatchController).GetProperty(nameof(MatchController.View)).SetValue(match, new WorldView(match.World, root.transform, match.Rig.Camera));
            typeof(MatchController).GetProperty(nameof(MatchController.Hud)).SetValue(match, new MatchHud(match, root.transform));
            match.Rig.SetHome(new Vector3(15.5f, 0, 11), 8);
            Refresh();
        }

        private static UnitSpawnDefinition Spawn(int id, int owner, string definition, int x, int z) =>
            new UnitSpawnDefinition { Id = id, OwnerId = owner, DefinitionId = definition, Position = new SimPoint(x, z) };

        private UnitState Unit(int id)
        {
            Assert.IsTrue(match.World.TryGetUnit(id, out var unit), "Expected a live unit " + id);
            return unit;
        }

        private void Refresh() { match.Hud.Invalidate(); match.SyncPresentation(1); Canvas.ForceUpdateCanvases(); }

        private Button Button(string prefix)
        {
            foreach (var button in root.GetComponentsInChildren<Button>())
            {
                var label = button.GetComponentInChildren<Text>();
                if (label != null && label.text.StartsWith(prefix, StringComparison.Ordinal)) return button;
            }
            return null;
        }

        private void Until(Func<bool> condition)
        {
            for (int i = 0; i < 400 && !condition(); i++) match.World.Tick();
            Assert.IsTrue(condition(), "The bounded naval order must complete.");
            Refresh();
        }

        [UnityTest]
        public IEnumerator WorkersHaveEmbarkActionAndPassengersLeaveTheShoreUntilTheyLand()
        {
            Create(); yield return null;
            match.Select(new[] { Worker }); Refresh();
            var embark = Button("Board ship\n");
            Assert.IsNotNull(embark, "Workers need the same embark control as soldiers, alongside their build controls.");
            embark.onClick.Invoke(); Assert.IsTrue(match.ChoosingShip);
            var screen = match.Rig.Camera.WorldToScreenPoint(match.View.RootFor(Ship).position + Vector3.up * 1.2f);
            match.Tap(screen, false);
            Assert.IsFalse(match.ChoosingShip);
            Assert.AreEqual(Ship, Unit(Worker).EmbarkShipId);
            Until(() => Unit(Ship).CargoCount == 1);
            Assert.IsTrue(match.World.TryGetPassenger(Worker, out _));
            Assert.IsNull(match.View.RootFor(Worker), "A passenger is removed from picking and rendering, not killed on shore.");
            Assert.IsFalse(match.Selection.Contains(Worker));

            match.Select(new[] { Ship }); Refresh();
            var details = Array.Find(root.GetComponentsInChildren<Text>(), label => label.name == "Selection");
            Assert.IsNotNull(details);
            StringAssert.Contains("1/" + Unit(Ship).CargoCapacity + " aboard", details.text.Split('\n')[0],
                "Cargo must appear before the combat details that can fall below a compact HUD.");
            var land = Button("Land troops\n"); Assert.IsNotNull(land); land.onClick.Invoke();
            Assert.IsTrue(match.ChoosingLanding);
            match.OrderGround(new SimPoint(17500, 11500));
            Assert.IsTrue(match.ChoosingLanding, "Invalid open-water landing keeps the targeting control active.");
            match.OrderGround(new SimPoint(15500, 16500));
            Assert.IsFalse(match.ChoosingLanding);
            Until(() => Unit(Ship).CargoCount == 0);
            Assert.IsTrue(match.World.IsWalkable(Unit(Worker).Position));
            Assert.IsNotNull(match.View.RootFor(Worker));
        }

        [UnityTest]
        public IEnumerator MinimapShowsDeepWaterAndOrdersLoadedShipsToLandThroughTheSameHudMode()
        {
            Create(); yield return null;
            match.Select(new[] { Worker }); Assert.IsTrue(match.EmbarkSelected(Ship));
            Until(() => Unit(Ship).CargoCount == 1);
            match.Select(new[] { Ship }); match.ChooseLanding(); Refresh();
            yield return new WaitForSecondsRealtime(.25f);
            Refresh();
            var minimap = match.Hud.Minimap;
            var sea = new Color32(46, 104, 150, 255);
            int seaPixels = 0;
            foreach (var pixel in minimap.Texture.GetPixels32()) if (pixel.Equals(sea)) seaPixels++;
            Assert.Greater(seaPixels, 200, "Navigable sea must read differently from dry obstacles.");
            var ground = new SimPoint(15500, 16500);
            var rect = minimap.Image.rectTransform;
            var local = rect.rect.min + Vector2.Scale(minimap.MapUv(ground), rect.rect.size);
            var pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Right,
                position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(local))
            };
            Assert.IsTrue(ExecuteEvents.Execute(minimap.Image.gameObject, pointer, ExecuteEvents.pointerClickHandler));
            Assert.IsTrue(Unit(Ship).IsUnloading, "The minimap must not replace a landing with an impossible land MoveCommand.");
            Assert.IsFalse(match.ChoosingLanding);
            Until(() => Unit(Ship).CargoCount == 0);
        }

        [UnityTest]
        public IEnumerator ShipModelsRemainSelectableAndMixedArmyCanFilterToShips()
        {
            Create(); yield return null;
            var ship = match.View.RootFor(Ship); Assert.IsNotNull(ship);
            Assert.IsNotNull(ship.Find("Owner marker"));
            Assert.Greater(ship.GetComponentsInChildren<MeshFilter>().Length, 0);
            var screen = match.Rig.Camera.WorldToScreenPoint(ship.position + Vector3.up * 1.2f);
            match.ClearSelection(); match.Tap(screen, false);
            CollectionAssert.AreEqual(new[] { Ship }, match.Selection);
            match.Select(new[] { Ship, Raider }); Refresh();
            var filter = Button("Ships x1\n"); Assert.IsNotNull(filter); filter.onClick.Invoke();
            CollectionAssert.AreEqual(new[] { Ship }, match.Selection);
            Assert.AreEqual("Selection filtered to 1 ships.", match.Feedback);
            var water = new SimPoint(23500, 11500); match.OrderGround(water);
            Assert.AreEqual(UnitOrder.Moving, Unit(Ship).Order);
            Assert.IsFalse(match.World.IsWalkable(water));
            match.Select(new[] { Worker }); match.ChooseShip(); Assert.IsTrue(match.ChoosingShip);
            match.ToggleSelected(Worker); Assert.IsFalse(match.ChoosingShip);
        }
    }
}
