using System;
using Emberfield.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Emberfield.Presentation
{
    public sealed partial class WorldView
    {
        private FactionDefinition FactionFor(int owner)
        {
            if (!world.TryGetPlayer(owner, out var player)) return null;
            foreach (var faction in world.Definition.Factions ?? Array.Empty<FactionDefinition>())
                if (faction.Id == player.FactionId) return faction;
            return null;
        }
        // The per-frame form: a player's faction never changes during a match, so each visual keeps its owner's once found.
        private FactionDefinition FactionFor(Visual visual, int owner)
        {
            if (visual.Faction == null || visual.FactionOwner != owner) { visual.Faction = FactionFor(owner); visual.FactionOwner = owner; }
            return visual.Faction;
        }
        private void DiscardTransformedVisual(int id, string definitionId)
        {
            if (!visuals.TryGetValue(id, out var visual) || visual.DefinitionId == definitionId) return;
            visual.Root.gameObject.SetActive(false); UnityEngine.Object.Destroy(visual.Root.gameObject); visuals.Remove(id);
        }
        private void BuildFactionBuilding(Visual visual, BuildingState building, float width, float depth)
        {
            var faction = FactionFor(building.OwnerId);
            if (faction == null) return;
            var team = building.OwnerId == 1 ? blue : red;
            if (faction.Kind == FactionKind.AvenCompact)
            {
                Shape("Aven upper terrace", PrimitiveType.Cube, visual.Model, new Vector3(-width * .12f, 2.16f, depth * .08f), new Vector3(width * .57f, .22f, depth * .58f), neutral);
                Shape("Aven square cloth tab", PrimitiveType.Cube, visual.Model, new Vector3(width * .29f, 1.6f, -depth * .42f), new Vector3(.46f, .56f, .055f), team);
                if (building.DefinitionId == "storeyard")
                {
                    var marker = Shape("Visible charter marker", PrimitiveType.Cube, visual.Model, new Vector3(0, 2.5f, 0), new Vector3(.65f, .3f, .45f), dark);
                    visual.FactionMarker = marker.GetComponent<Renderer>();
                    visual.Influence = InfluenceRing(visual.Root);
                }
            }
            else
            {
                var roof = visual.Model.Find("Roof"); if (roof != null) roof.gameObject.SetActive(false);
                for (int side = -1; side <= 1; side += 2)
                {
                    var slope = Shape("Serevin wedge roof", PrimitiveType.Cube, visual.Model, new Vector3(side * width * .22f, 2.02f, 0), new Vector3(width * .5f, .15f, depth * .92f), neutral);
                    slope.transform.localRotation = Quaternion.Euler(0, 0, -side * 20);
                    Shape("Serevin runner", PrimitiveType.Cube, visual.Model, new Vector3(side * width * .4f, .23f, 0), new Vector3(.2f, .2f, depth * 1.03f), dark);
                }
                Shape("Long faction pennant", PrimitiveType.Cube, visual.Model, new Vector3(width * .36f, 1.8f, -depth * .43f), new Vector3(.2f, .85f, .06f), team);
            }
        }
        private void BuildFactionUnit(Visual visual, UnitState unit)
        {
            var faction = FactionFor(unit.OwnerId);
            if (faction == null) return;
            var team = unit.OwnerId == 1 ? blue : red;
            if (unit.IsPackedOutpost)
            {
                Shape("Packed supply bundles", PrimitiveType.Cube, visual.Model, new Vector3(0, .85f, 0), new Vector3(.9f, .75f, 1.3f), team);
                Shape("Folded roof", PrimitiveType.Cube, visual.Model, new Vector3(0, 1.28f, 0), new Vector3(1.0f, .13f, 1.4f), neutral);
                for (int side = -1; side <= 1; side += 2)
                {
                    Shape("Transport runner", PrimitiveType.Cube, visual.Model, new Vector3(side * .47f, .2f, 0), new Vector3(.16f, .16f, 1.7f), dark);
                    Shape("Runner brace", PrimitiveType.Cube, visual.Model, new Vector3(side * .38f, .4f, 0), new Vector3(.12f, .4f, .12f), neutral);
                }
                AddResearchBar(visual, 1.73f, 1.1f);
                return;
            }
            UnitDefinition definition = null;
            foreach (var item in world.Definition.Units) if (item.Id == unit.DefinitionId) { definition = item; break; }
            if (definition.IsThreadkeeper)
            {
                Shape("Threadkeeper supply frame", PrimitiveType.Cube, visual.Model, new Vector3(0, .85f, -.36f), new Vector3(.95f, 1.1f, .18f), neutral);
                Shape("Threadkeeper cross brace", PrimitiveType.Cube, visual.Model, new Vector3(0, 1.38f, -.43f), new Vector3(1.15f, .12f, .18f), team);
                visual.FactionMarker = Shape("Relay signal", PrimitiveType.Cube, visual.Model, new Vector3(.65f, 1.15f, 0), new Vector3(.35f, .7f, .12f), dark).GetComponent<Renderer>();
                visual.Influence = InfluenceRing(visual.Root);
                AddResearchBar(visual, 1.72f, 1.1f);
            }
            else if (definition.CanReposition)
            {
                Shape("Ashrunner pennant pole", PrimitiveType.Cube, visual.Model, new Vector3(-.35f, 1.63f, -.48f), new Vector3(.05f, 1.3f, .05f), neutral);
                visual.FactionMarker = Shape("Ashrunner split pennant", PrimitiveType.Cube, visual.Model, new Vector3(-.35f, 2.1f, -.78f), new Vector3(.06f, .26f, .65f), team).GetComponent<Renderer>();
                AddResearchBar(visual, 2.2f, 1.1f);
            }
            else
            {
                var tab = Shape(faction.Kind == FactionKind.AvenCompact ? "Aven square tab" : "Serevin diagonal tab", PrimitiveType.Cube, visual.Model, new Vector3(-.34f, .95f, 0), new Vector3(.1f, .34f, .32f), neutral);
                if (faction.Kind == FactionKind.SerevinMarch) tab.transform.localRotation = Quaternion.Euler(0, 0, 30);
            }
        }
        private void BuildAlphaFactionBuilding(Visual visual, BuildingState building)
        {
            if (building.DefinitionId != "storeyard" || FactionFor(building.OwnerId)?.Kind != FactionKind.AvenCompact) return;
            visual.FactionMarker = Shape("Visible charter lantern", PrimitiveType.Cube, visual.Model, new Vector3(.35f, 2.35f, -.28f), new Vector3(.21f, .29f, .21f), dark).GetComponent<Renderer>();
            visual.Influence = InfluenceRing(visual.Root);
        }
        private void BuildAlphaFactionUnit(Visual visual, UnitState unit)
        {
            if (unit.IsPackedOutpost) { AddResearchBar(visual, AlphaWorldArt.UnitHeight(unit.DefinitionId) + .01f, 1.1f * AlphaWorldArt.UnitScale); return; }
            UnitDefinition definition = null;
            foreach (var item in world.Definition.Units) if (item.Id == unit.DefinitionId) { definition = item; break; }
            if (definition == null || (!definition.IsThreadkeeper && !definition.CanReposition)) return;
            Vector3 marker = definition.IsThreadkeeper ? new Vector3(-.45f, 2.15f, -.18f) : new Vector3(-.34f, 2.40f, -.45f);
            visual.FactionMarker = Shape("Active faction signal", PrimitiveType.Cube, visual.Model, marker, new Vector3(.18f, .24f, .09f), unit.OwnerId == 1 ? blue : red).GetComponent<Renderer>();
            if (definition.IsThreadkeeper) visual.Influence = InfluenceRing(visual.Root);
            AddResearchBar(visual, AlphaWorldArt.UnitHeight(unit.DefinitionId) - .01f, 1.1f * AlphaWorldArt.UnitScale);
        }
        private Transform InfluenceRing(Transform parent)
        {
            var circle = new GameObject("Selected supply reach").transform; circle.SetParent(parent, false);
            var line = circle.gameObject.AddComponent<LineRenderer>(); line.useWorldSpace = false; line.loop = true; line.positionCount = 48;
            line.startWidth = line.endWidth = .045f; line.sharedMaterial = arrow; line.shadowCastingMode = ShadowCastingMode.Off;
            for (int index = 0; index < 48; index++) { float angle = index * Mathf.PI * 2 / 48; line.SetPosition(index, new Vector3(Mathf.Cos(angle), .065f, Mathf.Sin(angle))); }
            circle.gameObject.SetActive(false); return circle;
        }
        private void UpdateFactionBuilding(Visual visual, BuildingState building, bool selected)
        {
            var faction = FactionFor(visual, building.OwnerId);
            if (faction == null) return;
            if (visual.FactionMarker != null)
                visual.FactionMarker.sharedMaterial = building.CharterRemainingTicks > 0 || building.ActiveCharter == StoreyardCharter.None ? dark : building.ActiveCharter == StoreyardCharter.Logistics ? arrow : health;
            if (visual.Influence != null)
            {
                int radius = faction.CharterRadiusMillimetres;
                if (world.TryGetPlayer(building.OwnerId, out var player) && player.HasTechnology(faction.ReciprocalStoresTechnologyId)) radius += faction.ReciprocalRadiusBonusMillimetres;
                visual.Influence.localScale = new Vector3(radius * .001f, 1, radius * .001f);
                visual.Influence.gameObject.SetActive(selected && building.CharterRemainingTicks == 0 && building.ActiveCharter != StoreyardCharter.None);
            }
            if (building.CharterRemainingTicks > 0) SetFactionProgress(visual, 1 - building.CharterRemainingTicks / (float)faction.CharterTicks);
            if (building.RelocationStage == RelocationStage.Packing) SetFactionProgress(visual, 1 - building.RelocationRemainingTicks / (float)building.RelocationTotalTicks);
        }
        private void UpdateFactionUnit(Visual visual, UnitState unit, bool selected)
        {
            var faction = FactionFor(visual, unit.OwnerId);
            if (faction == null) return;
            if (visual.ResearchBar != null) visual.ResearchBar.gameObject.SetActive(false);
            if (visual.Influence != null)
            {
                bool linked = unit.ThreadkeeperRemainingTicks > 0 && world.TryGetBuilding(unit.LinkedStoreyardId, out var yard) && yard.CharterRemainingTicks == 0 && yard.ActiveCharter != StoreyardCharter.None;
                visual.Influence.localScale = new Vector3(faction.RelayRadiusMillimetres * .001f, 1, faction.RelayRadiusMillimetres * .001f);
                visual.Influence.gameObject.SetActive(selected && linked);
            }
            if (unit.ThreadkeeperRemainingTicks > 0)
            {
                if (visual.FactionMarker != null) visual.FactionMarker.sharedMaterial = arrow;
                SetFactionProgress(visual, unit.ThreadkeeperRemainingTicks / (float)faction.ThreadkeeperDeployTicks);
            }
            else if (unit.RepositionRemainingTicks > 0)
            {
                if (visual.FactionMarker != null) visual.FactionMarker.sharedMaterial = arrow;
                SetFactionProgress(visual, unit.RepositionRemainingTicks / (float)faction.RepositionTicks);
            }
            else if (visual.FactionMarker != null) visual.FactionMarker.sharedMaterial = unit.OwnerId == 1 ? blue : red;
            if (unit.RelocationStage == RelocationStage.Deploying) SetFactionProgress(visual, 1 - unit.RelocationRemainingTicks / (float)unit.RelocationTotalTicks);
        }
        private void SetFactionProgress(Visual visual, float progress)
        {
            if (visual.ResearchBar == null) return;
            visual.ResearchBar.gameObject.SetActive(true); visual.ResearchBar.rotation = cameraRotation;
            visual.ResearchFill.localScale = new Vector3(Mathf.Clamp01(progress), 1, 1);
        }
    }
}
