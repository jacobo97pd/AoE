using System;
using System.Collections.Generic;
using Emberfield.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Emberfield.Presentation
{
    public sealed partial class WorldView : IDisposable
    {
        private sealed class Visual
        {
            public Transform Root;
            public GameObject Selection;
            public float Radius;
            public Transform Model, HealthBar, HealthFill;
            public Transform ResearchBar, ResearchFill;
            public int LastHealth, OwnerId, LastAttackCooldown;
            public float HitUntil;
            public GameObject Hit;
            public string DefinitionId;
            public Transform Influence;
            public Renderer FactionMarker;
            public bool IsArt, WasComplete, IsUnit, IsShip;
            public int LastCargo;
            public Transform LeftWing, RightWing, GateLeaf;
            public HearthSmoke Smoke;
            public MeshRenderer[] SwayParts;
            public bool Worked;
            public FactionKind FactionKind;
            public int CosmeticRevision = -1;
            public int LastOilCooldown, BoardingWallId, BoardingDuration;
            public Vector3 ClimbStart;
            // A siege ladder's raise against an enemy wall, and a soldier's way up one (SiegeLadderVisuals).
            public SiegeLadderVisuals.Ladder Ladder;
            public SiegeLadderVisuals.Climb Climb;
            public CorsairAnimationDriver ImportedCharacter;
            public bool IsReferenceCharacter;
            // The character skin (MeshyUnitVisuals) the model was drawn with, null for a default model.
            public string SkinId;
            public double FacingTime = -1;
            // Readability rim (SetOnDarkGround): a Meshy character's own renderers, cached once, and whether the
            // lift is on, so a still figure standing on volcanic ash costs nothing past its first frame there.
            public Renderer[] ReadabilityParts;
            public bool OnDarkGround;
            // Looked up once rather than every frame: the owner's faction (redone if the owner ever changes) and the
            // unit's attack cooldown, which paces the lunge. A changed definition gets a new visual altogether.
            public FactionDefinition Faction;
            public int FactionOwner = -1;
            public int AttackCooldownTicks = -1;
        }

        private readonly World world;
        private readonly Transform root;
        private readonly Camera camera;
        private readonly Dictionary<int, Visual> visuals = new Dictionary<int, Visual>();
        private readonly Dictionary<int, Transform> projectiles = new Dictionary<int, Transform>();
        private readonly Dictionary<int, ProjectileAppearance> projectileAppearance = new Dictionary<int, ProjectileAppearance>();
        private struct ProjectileAppearance { public float SourceHeight, TargetHeight, Distance; public int Kind; public Vector3 Origin; }
        private readonly Stack<Transform> projectilePool = new Stack<Transform>();
        private Mesh fallbackArrowMesh;
        private readonly HashSet<int> liveIds = new HashSet<int>();
        private readonly HashSet<int> selectionIds = new HashSet<int>();
        private readonly List<int> removedIds = new List<int>();
        private readonly List<Material> materials = new List<Material>();
        private readonly Material blue, red, neutral, selection, dark, health, arrow;
        private readonly FogView fogView;
        private readonly AmberBiome biome;
        private readonly AlphaEnvironment environment;
        private readonly AlphaSiegeEffects siegeEffects;
        private readonly bool sliceEnabled, alphaEnabled;
        public SliceFeedback Feedback { get; }
        private readonly List<Transform> fogObstacles = new List<Transform>();
        // Where each obstacle stands, and the vision revision they were last shown for: explored ground only changes
        // with the revision, so the thousands of fillers on a map are walked when it moves, not every frame.
        private readonly List<SimPoint> fogObstacleCells = new List<SimPoint>();
        private readonly List<bool> fogObstacleShown = new List<bool>();
        private long fogObstacleRevision = -1;
        // Read once per Sync for every bar that faces the camera.
        private Quaternion cameraRotation;
        public int Count => visuals.Count;
        public int ProjectileCount => projectiles.Count;
        /// <summary>The fog drawn over the ground, or null on a map without vision.</summary>
        public FogView Fog => fogView;
        /// <summary>Visuals the last Sync had to build, for pacing reports: a new one is the costly kind of frame.</summary>
        public int CreatedLastSync { get; private set; }
        public Transform RootFor(int id) => visuals.TryGetValue(id, out var v) ? v.Root : null;
        public Transform ProjectileRootFor(int id) => projectiles.TryGetValue(id, out var p) ? p : null;

        public WorldView(World world, Transform parent, Camera camera)
        {
            this.world = world;
            this.camera = camera;
            // The sandbox is the first thing a player opens, so it draws with the same art as a match.
            sliceEnabled = world.Map.Id == "amber_crossing" || world.Map.Id == "sapphire_coast" || world.Map.Id == "sunscar_basin" || world.Map.Id == "legend_lands" || world.Map.Id == "faction_proving_ground" || world.Map.Id == "art_review" || world.Map.Id == "amber_reach";
            alphaEnabled = sliceEnabled && world.Map.Id != "art_review";
            root = new GameObject("World presentation").transform;
            root.SetParent(parent, false);
            blue = Material(alphaEnabled ? AlphaWorldArt.OwnerColor(1) : new Color(.15f, .51f, .76f));
            red = Material(alphaEnabled ? AlphaWorldArt.OwnerColor(2) : new Color(.78f, .28f, .23f));
            neutral = Material(new Color(.55f, .52f, .43f));
            selection = Material(new Color(1, .8f, .23f));
            dark = Material(new Color(.08f, .11f, .12f));
            health = Material(new Color(.4f, .88f, .46f));
            arrow = Material(new Color(1, .9f, .52f));
            var ground = Material(new Color(.26f, .32f, .28f));
            float width = world.Map.WidthCells * world.Map.CellSizeMillimetres * .001f;
            float depth = world.Map.HeightCells * world.Map.CellSizeMillimetres * .001f;
            if (sliceEnabled) { biome = new AmberBiome(world.Map, root, world.Lands); Feedback = new SliceFeedback(root, camera); }
            else Shape("Amber Reach", PrimitiveType.Cube, root, new Vector3(width / 2, -.18f, depth / 2), new Vector3(width, .3f, depth), ground);
            // One battlefield view at a time: the fillers and nodes whose level follows the zoom are this view's.
            SceneryDetail.Reset();
            if (alphaEnabled) { environment = new AlphaEnvironment(world, root); siegeEffects = new AlphaSiegeEffects(world, root); }
            // A land's monument or peak stands alone on its footing: no filler there, or only the peak's own rocks.
            var underLandmarks = alphaEnabled ? AlphaEnvironment.LandmarkFootprints(world) : null;
            foreach (var c in world.Map.BlockedCells)
            {
                if (alphaEnabled && AlphaEnvironment.IsWaterCell(world.Map, c.X, c.Z)) continue;
                string rocks = null;
                if (underLandmarks != null && underLandmarks.TryGetValue(c.Z * world.Map.WidthCells + c.X, out rocks) && rocks == null) continue;
                Transform rock = alphaEnabled ? AlphaEnvironment.CreateObstacle(root, c.X, c.Z, rocks ?? world.Lands.BiomeAt(c.X, c.Z), rocks != null) : sliceEnabled ? ArtKit.CreateResource(ResourceKind.Stone, root) : null;
                if (rock != null)
                {
                    // Alpha scenery already carries its own nudge off centre; the greybox stand-in does not.
                    var centre = new Vector3((c.X + .5f) * world.Map.CellSizeMillimetres * .001f, 0, (c.Z + .5f) * world.Map.CellSizeMillimetres * .001f);
                    rock.position = alphaEnabled ? centre + rock.localPosition : centre;
                    if (!alphaEnabled) rock.localScale = new Vector3(.7f, .9f, .7f);
                }
                else rock = Shape("Rock obstacle", PrimitiveType.Cube, root, new Vector3((c.X + .5f) * world.Map.CellSizeMillimetres * .001f, .5f, (c.Z + .5f) * world.Map.CellSizeMillimetres * .001f), new Vector3(.94f, 1, .94f), neutral).transform;
                fogObstacles.Add(rock); fogObstacleCells.Add(DefinitionLoader.ToSimulation(rock.position)); fogObstacleShown.Add(rock.gameObject.activeSelf);
            }
            // Subtle 8m grid makes ground motion and construction distance legible.
            var grid = Material(new Color(.31f, .37f, .32f));
            if (!sliceEnabled)
            {
                for (int x = 8; x < width; x += 8) Shape("Map guide", PrimitiveType.Cube, root, new Vector3(x, -.02f, depth / 2), new Vector3(.035f, .02f, depth), grid);
                for (int z = 8; z < depth; z += 8) Shape("Map guide", PrimitiveType.Cube, root, new Vector3(width / 2, -.02f, z), new Vector3(width, .02f, .035f), grid);
            }
            if (world.Vision != null) fogView = new FogView(world, root);
            CreateObjectives();
            Sync(1, Array.Empty<int>());
        }

        public void Sync(float alpha, IReadOnlyCollection<int> selected)
        {
            UnitMeshDetail.Refresh(camera);
            SceneryDetail.Refresh(camera);
            CreatedLastSync = 0;
            cameraRotation = camera.transform.rotation;
            selectionIds.Clear();
            // By index where the collection allows it: a foreach through the interface boxes an enumerator every frame.
            if (selected is IReadOnlyList<int> selectedList) for (int i = 0; i < selectedList.Count; i++) selectionIds.Add(selectedList[i]);
            else foreach (int id in selected) selectionIds.Add(id);
            liveIds.Clear();
            worked.Clear();
            PresentationMarkers.Units.Begin();
            var units = world.Units;
            for (int unitIndex = 0; unitIndex < units.Count; unitIndex++)
            {
                var u = units[unitIndex];
                liveIds.Add(u.Id);
                if (!Visible(u.Id)) { if (visuals.TryGetValue(u.Id, out var hidden)) hidden.Root.gameObject.SetActive(false); continue; }
                // Collected from visible workers only: a tree shaken by someone you cannot see would give away
                // where they are, and fog is the one thing presentation must never leak.
                if (u.WorkerTask == WorkerTask.Gathering && u.TargetResourceId != 0) worked.Add(u.TargetResourceId);
                DiscardTransformedVisual(u.Id, u.DefinitionId);
                if (visuals.TryGetValue(u.Id, out var existing) && existing.CosmeticRevision != CosmeticLoadout.Revision &&
                    ReferenceCharacterVisuals.Resolve(u.DefinitionId, existing.FactionKind, world.Map.RealmId) is string referenceId && ReferenceCharacterVisuals.Find(referenceId) != null)
                {
                    bool useReference = CosmeticLoadout.Resolve(u.DefinitionId, world.Map.RealmId, u.OwnerId).IsDefault;
                    if (useReference != existing.IsReferenceCharacter)
                    { existing.Root.gameObject.SetActive(false); UnityEngine.Object.Destroy(existing.Root.gameObject); visuals.Remove(u.Id); }
                }
                // Equipping or removing a character skin redraws the units it dresses, and only those.
                if (visuals.TryGetValue(u.Id, out var dressed) && dressed.CosmeticRevision != CosmeticLoadout.Revision && dressed.ImportedCharacter != null &&
                    MeshyUnitVisuals.Skin(u.DefinitionId, dressed.FactionKind, world.Map.RealmId, u.OwnerId)?.id != dressed.SkinId)
                { dressed.Root.gameObject.SetActive(false); UnityEngine.Object.Destroy(dressed.Root.gameObject); visuals.Remove(u.Id); }
                if (u.Domain == MovementDomain.Water && !visuals.ContainsKey(u.Id)) NewShipVisual(u);
                if (!visuals.TryGetValue(u.Id, out var v))
                {
                    v = NewVisual(u.Id, u.DefinitionId, alphaEnabled ? AlphaWorldArt.UnitSelectionRadius(u.DefinitionId) : ((u.Tags & CombatTags.Cavalry) != 0 ? .9f : .7f) * AlphaWorldArt.UnitScale, true);
                    v.IsUnit = true;
                    // Authored art, imported pirates and reference characters all hang under the model, so one
                    // scale here keeps every character the same fraction of the world it stands in. The ring
                    // stroke follows: at full width it read as a wheel around the smaller character.
                    v.Model.localScale = Vector3.one * AlphaWorldArt.UnitScale;
                    v.OwnerId = u.OwnerId;
                    var faction = FactionFor(u.OwnerId);
                    // A navy's soldiers are its kingdom's (AlphaWorldArt.Culture).
                    v.FactionKind = AlphaWorldArt.Culture(faction?.Kind ?? FactionKind.AvenCompact);
                    // Meshy models come first where the catalogue has one for this faction and unit; they fit the
                    // health bar to their own bounds like the reference figures they replace.
                    v.ImportedCharacter = MeshyUnitVisuals.TryCreate(world, u, v.FactionKind, v.Model, out v.SkinId);
                    v.IsReferenceCharacter = v.ImportedCharacter != null;
                    if (v.ImportedCharacter == null) v.ImportedCharacter = ImportedCharacterVisuals.TryCreate(world, u, v.Model);
                    if (v.ImportedCharacter == null)
                    { v.ImportedCharacter = ReferenceCharacterVisuals.TryCreate(world, u, v.FactionKind, v.Model); v.IsReferenceCharacter = v.ImportedCharacter != null; }
                    if (v.ImportedCharacter != null) AddOwnerMarker(v);
                    // Siege engines have a Meshy model for every faction (the ladder in its culture's style); AlphaWorldArt.Unit stays the procedural build its tests pin.
                    v.IsArt = v.ImportedCharacter != null || (alphaEnabled ? (MeshyPropVisuals.TrySiege(u.DefinitionId, u.OwnerId, v.FactionKind, v.Model) ?? AlphaWorldArt.Unit(u.DefinitionId, v.FactionKind, v.Model, u.OwnerId)) != null
                        : sliceEnabled && faction?.Kind == FactionKind.AvenCompact && ArtKit.CreateUnit(u.DefinitionId, v.Model, u.OwnerId) != null);
                    if (!v.IsArt) { BuildUnit(v, u); BuildFactionUnit(v, u); }
                    else if (alphaEnabled) BuildAlphaFactionUnit(v, u);
                    if (v.IsArt && u.DefinitionId == "ember_drake")
                    {
                        var art = v.Model.GetChild(0); v.LeftWing = art.Find("Left wing"); v.RightWing = art.Find("Right wing");
                    }
                    float healthHeight = v.ImportedCharacter != null ? AlphaWorldArt.UnitHeight(u.DefinitionId) + .25f : alphaEnabled ? AlphaWorldArt.UnitHeight(u.DefinitionId) + .22f : ((u.Tags & CombatTags.Cavalry) != 0 ? 2.45f : 1.95f) * AlphaWorldArt.UnitScale;
                    if (v.IsReferenceCharacter)
                    {
                        // Bounds include hats and weapons, keeping the bar clear of the rendered silhouette.
                        float top = 0;
                        foreach (var renderer in v.ImportedCharacter.GetComponentsInChildren<SkinnedMeshRenderer>())
                            top = Mathf.Max(top, renderer.bounds.max.y - v.Root.position.y);
                        if (top > .1f) healthHeight = top + .22f;
                    }
                    AddHealthBar(v, healthHeight, 1.1f * AlphaWorldArt.UnitScale);
                    v.LastHealth = u.Health;
                    v.LastCargo = u.CarriedAmount;
                    v.LastAttackCooldown = u.AttackCooldownTicks;
                }
                if (!v.Root.gameObject.activeSelf) { v.LastHealth = u.Health; v.LastCargo = u.CarriedAmount; v.LastAttackCooldown = u.AttackCooldownTicks; v.HitUntil = 0; v.FacingTime = -1; v.ImportedCharacter?.ResetObservation(u, world.TickIndex, alpha); }
                v.Root.gameObject.SetActive(true);
                var previous = DefinitionLoader.ToWorld(u.PreviousPosition);
                var current = DefinitionLoader.ToWorld(u.Position);
                v.Root.position = Vector3.Lerp(previous, current, alpha) + Vector3.up * (u.ElevationMillimetres * .001f);
                if (u.BoardingWallId != 0 && Visible(u.BoardingWallId) && world.TryGetBuilding(u.BoardingWallId, out var climbWall))
                {
                    if (v.BoardingWallId != u.BoardingWallId || u.BoardingRemainingTicks > v.BoardingDuration)
                    { v.BoardingWallId = u.BoardingWallId; v.BoardingDuration = Mathf.Max(1, u.BoardingRemainingTicks); v.ClimbStart = current; }
                    float height = 3;
                    var wallDefinition = BuildingDefinitionFor(climbWall.DefinitionId);
                    if (wallDefinition != null) height = wallDefinition.WallHeightMillimetres * .001f;
                    // The deck runs along the wall's long side, north-south on a turned wall.
                    var deck = DefinitionLoader.ToWorld(climbWall.Position); deck.y = height;
                    if (climbWall.DepthCells > climbWall.WidthCells)
                    { float half = climbWall.DepthCells * world.Map.CellSizeMillimetres * .0004f; deck.z = Mathf.Clamp(current.z, deck.z - half, deck.z + half); }
                    else { float half = climbWall.WidthCells * world.Map.CellSizeMillimetres * .0004f; deck.x = Mathf.Clamp(current.x, deck.x - half, deck.x + half); }
                    float climb = Mathf.Clamp01(1 - (u.BoardingRemainingTicks - alpha) / Mathf.Max(1, v.BoardingDuration));
                    // With its side's ladder raised against an enemy wall the soldier goes up the rungs instead
                    // (SiegeLadderVisuals); a tower or its own wall keeps this straight lift.
                    if (!ClimbLadder(v, u, climbWall, alpha)) v.Root.position = Vector3.Lerp(v.ClimbStart, deck, climb);
                }
                else v.BoardingWallId = v.BoardingDuration = 0;
                Quaternion? facing = null; bool engaging = false;
                if ((current - previous).sqrMagnitude > .00001f) facing = Quaternion.LookRotation(current - previous);
                if (u.AttackTargetId != 0 && Visible(u.AttackTargetId))
                {
                    Vector3 target = current;
                    if (world.TryGetUnit(u.AttackTargetId, out var enemy)) target = DefinitionLoader.ToWorld(enemy.Position);
                    else if (world.TryGetBuilding(u.AttackTargetId, out var structure)) target = DefinitionLoader.ToWorld(structure.Position);
                    if ((target - current).sqrMagnitude > .00001f) { facing = v.IsShip ? Broadside(v.Root.rotation, target - current) : Quaternion.LookRotation(target - current); engaging = true; }
                }
                v.Root.rotation = Turn(v, facing ?? v.Root.rotation, alpha, v.IsShip ? ShipTurnDegreesPerSecond : engaging ? EngageDegreesPerSecond : TurnDegreesPerSecond);
                // The lunge runs over the first four ticks of a cooldown, eased between them like the position.
                float pulse = 0;
                if (u.AttackCooldownTicks > 0)
                {
                    if (v.AttackCooldownTicks < 0)
                    {
                        v.AttackCooldownTicks = 0;
                        foreach (var definition in world.Definition.Units) if (definition.Id == u.DefinitionId) { v.AttackCooldownTicks = definition.Attack.CooldownTicks; break; }
                    }
                    pulse = Mathf.Clamp01(1 - (v.AttackCooldownTicks - u.AttackCooldownTicks + Mathf.Clamp01(alpha)) / 4f);
                }
                v.Model.localPosition = new Vector3(0, 0, pulse * .18f * AlphaWorldArt.UnitScale);
                if (v.ImportedCharacter != null)
                {
                    v.CosmeticRevision = CosmeticLoadout.Revision;
                    v.Model.localPosition = Vector3.zero;
                    v.Model.localRotation = Quaternion.identity;
                    v.ImportedCharacter.Sync(u, world.TickIndex, alpha);
                    SetOnDarkGround(v, world.Lands.BiomeAt(u.Position) == MapLands.Volcanic);
                }
                else if (v.IsShip) PoseShip(v, u, alpha, pulse);
                else if (v.IsArt)
                {
                    float phase = (world.TickIndex + alpha) * (float)Simulation.World.TickSeconds * 9 + u.Id * .37f;
                    bool moving = current != previous || u.BoardingWallId != 0;
                    bool working = u.WorkerTask == WorkerTask.Gathering || u.WorkerTask == WorkerTask.Constructing;
                    v.Model.localPosition += Vector3.up * (moving ? Mathf.Abs(Mathf.Sin(phase)) * .055f * AlphaWorldArt.UnitScale : 0);
                    v.Model.localRotation = Quaternion.Euler((working ? Mathf.Sin(phase) * 9 : u.CarriedAmount > 0 ? -4 : 0) - pulse * 10, 0, moving ? Mathf.Sin(phase) * 3 : 0);
                    if (u.DefinitionId == "ember_drake")
                    {
                        float flap = Mathf.Sin(phase * .40f) * (moving ? 26 : 10) + pulse * 18;
                        if (v.LeftWing != null) v.LeftWing.localRotation = Quaternion.Euler(0, 0, flap);
                        if (v.RightWing != null) v.RightWing.localRotation = Quaternion.Euler(0, 0, -flap);
                    }
                    else if (u.DefinitionId.StartsWith("siege_", StringComparison.Ordinal))
                        v.Model.localRotation = Quaternion.Euler(-pulse * 4, 0, moving ? Mathf.Sin(phase) * .55f : 0);
                    RefreshCosmetic(v);
                }
                if (u.DefinitionId == ImportedCharacterVisuals.GunpowderCorsairId && u.AttackCooldownTicks > v.LastAttackCooldown)
                    EmitGunshot(v, alpha);
                v.LastAttackCooldown = u.AttackCooldownTicks;
                // What the worker is carrying picks the sound, so an economy can be followed by ear.
                if (u.CarriedAmount > v.LastCargo)
                    Feedback?.Emit(u.CarriedKind == ResourceKind.Wood ? FeedbackCue.Chop
                        : u.CarriedKind == ResourceKind.Food ? FeedbackCue.Gather : FeedbackCue.Mine, current);
                v.LastCargo = u.CarriedAmount;
                UpdateHealth(v, u.Health, u.MaxHealth);
                // Over the stock pose above: a ladder leans on the wall it serves, and a climber takes its rungs,
                // facing the wall and playing Climb after the driver's own Sync (SiegeLadderVisuals).
                SyncLadder(v, u, alpha);
                PoseClimber(v);
                bool isSelected = selectionIds.Contains(u.Id);
                v.Selection.SetActive(isSelected);
                if (alphaEnabled) v.HealthBar.gameObject.SetActive(isSelected || u.Health < u.MaxHealth || u.AttackTargetId != 0);
                UpdateFactionUnit(v, u, isSelected);
            }
            PresentationMarkers.Units.End();
            PresentationMarkers.Buildings.Begin();
            var buildings = world.Buildings;
            for (int buildingIndex = 0; buildingIndex < buildings.Count; buildingIndex++)
            {
                var b = buildings[buildingIndex];
                liveIds.Add(b.Id);
                if (!Visible(b.Id)) { if (visuals.TryGetValue(b.Id, out var hidden)) hidden.Root.gameObject.SetActive(false); continue; }
                DiscardTransformedVisual(b.Id, b.DefinitionId);
                if (!visuals.TryGetValue(b.Id, out var v))
                {
                    // A turned wall or gate is its own model turned a quarter, not a model squeezed into the swapped
                    // footprint: its stonework, merlons and passage run along the model's X. So it is built along and
                    // across as the definition stands and the Model container turns, while the bars and the ring,
                    // which hang on the root, stay as they are.
                    float width = (b.IsTurned ? b.DepthCells : b.WidthCells) * world.Map.CellSizeMillimetres * .001f;
                    float depth = (b.IsTurned ? b.WidthCells : b.DepthCells) * world.Map.CellSizeMillimetres * .001f;
                    v = NewVisual(b.Id, b.DefinitionId, Mathf.Max(width, depth) * .65f);
                    v.OwnerId = b.OwnerId;
                    v.Root.position = DefinitionLoader.ToWorld(b.Position);
                    var faction = FactionFor(b.OwnerId);
                    // A navy builds its kingdom's town (AlphaWorldArt.Culture).
                    v.FactionKind = AlphaWorldArt.Culture(faction?.Kind ?? FactionKind.AvenCompact);
                    // A Meshy model takes precedence where the catalogue has one for this faction and building; the
                    // flag-gated test barracks is the older, local-player-only form of the same thing.
                    var imported = MeshyBuildingVisuals.TryCreate(b, v.FactionKind, v.Model, width, depth) ?? ImportedBuildingVisuals.TryCreate(b, v.Model, width, depth);
                    var artBuilding = imported != null ? imported
                        : alphaEnabled ? AlphaWorldArt.Building(b.DefinitionId, v.FactionKind, v.Model, b.OwnerId, width, depth)
                        : sliceEnabled && faction?.Kind == FactionKind.AvenCompact ? ArtKit.CreateBuilding(b.DefinitionId, v.Model, b.OwnerId, width, depth) : null;
                    // A dock is the coast's harbour quay, whatever culture built it.
                    bool dock = artBuilding == null && sliceEnabled && BuildingDefinitionFor(b.DefinitionId)?.RequiresShore == true;
                    if (dock) artBuilding = DockModel(b, v.Model, width, depth);
                    v.IsArt = artBuilding != null;
                    if (artBuilding != null && !dock) artBuilding.localRotation = Quaternion.Euler(0, 180, 0);
                    if (artBuilding != null && b.DefinitionId == "gate") v.GateLeaf = artBuilding.Find("Portcullis");
                    if (!v.IsArt)
                    {
                    Shape("Foundation", PrimitiveType.Cube, v.Model, new Vector3(0, .2f, 0), new Vector3(width * .96f, .4f, depth * .96f), neutral);
                    Shape("Hall", PrimitiveType.Cube, v.Model, new Vector3(0, 1.05f, 0), new Vector3(width * .78f, 1.5f, depth * .78f), b.OwnerId == 1 ? blue : red);
                    Shape("Roof", PrimitiveType.Cube, v.Model, new Vector3(0, 1.95f, 0), new Vector3(width * .88f, .3f, depth * .88f), neutral);
                    if (b.DefinitionId == "archive")
                    {
                        Shape("Archive shelves", PrimitiveType.Cube, v.Model, new Vector3(0, 1.13f, -depth * .4f), new Vector3(width * .55f, .85f, .12f), dark);
                        for (int shelf = 0; shelf < 3; shelf++) Shape("Archive shelf", PrimitiveType.Cube, v.Model, new Vector3(0, .85f + shelf * .3f, -depth * .43f), new Vector3(width * .57f, .07f, .18f), neutral);
                    }
                    BuildFactionBuilding(v, b, width, depth);
                    }
                    // The charter lantern and supply reach are play information, so they hang on any model.
                    if (v.IsArt && alphaEnabled) BuildAlphaFactionBuilding(v, b);
                    // The chimney anchor is measured on the procedural hall; on another model it would float in the air.
                    if (v.IsArt && alphaEnabled && imported == null && HearthSmoke.Smokes(b.DefinitionId))
                        v.Smoke = HearthSmoke.Attach(v.Root,
                            new Vector3(width * .17f, AlphaWorldArt.BuildingHeight(b.DefinitionId) * .88f, -depth * .12f),
                            Mathf.Clamp(Mathf.Min(width, depth) / 3.4f, .55f, 1.15f));
                    float barHeight = alphaEnabled ? AlphaWorldArt.BuildingHeight(b.DefinitionId) + .25f : 2.6f;
                    AddHealthBar(v, barHeight, Mathf.Min(2, width));
                    AddResearchBar(v, barHeight - .23f, Mathf.Min(2, width));
                    // Turned only now: the Meshy fit above measures the model in world space, along X.
                    if (b.IsTurned) v.Model.localRotation = Quaternion.Euler(0, 90, 0);
                    v.LastHealth = b.Health;
                    v.WasComplete = b.IsComplete;
                    v.LastOilCooldown = b.OilCooldownTicks;
                }
                v.Root.position = DefinitionLoader.ToWorld(b.Position);
                if (!v.Root.gameObject.activeSelf) { v.LastHealth = b.Health; v.WasComplete = b.IsComplete; v.LastOilCooldown = b.OilCooldownTicks; v.HitUntil = 0; }
                if (!v.WasComplete && b.IsComplete) Feedback?.Emit(FeedbackCue.Complete, v.Root.position + Vector3.up);
                v.WasComplete = b.IsComplete;
                bool isSelected = selectionIds.Contains(b.Id);
                v.Selection.SetActive(isSelected);
                v.Model.localScale = new Vector3(1, Mathf.Lerp(.12f, 1, b.ConstructionProgress), 1);
                // A building with no roof on it yet has no fire in it either.
                if (v.Smoke != null) v.Smoke.gameObject.SetActive(b.IsComplete);
                if (v.GateLeaf != null)
                {
                    v.GateLeaf.localPosition = Vector3.up * (b.GateOpen ? 2.45f : 0);
                    v.GateLeaf.localScale = new Vector3(1, b.GateOpen ? .075f : 1, 1);
                }
                if (b.OilCooldownTicks > v.LastOilCooldown) siegeEffects?.Oil(b);
                v.LastOilCooldown = b.OilCooldownTicks;
                if (v.IsArt) RefreshCosmetic(v);
                v.Root.gameObject.SetActive(true);
                v.Root.position = DefinitionLoader.ToWorld(b.Position);
                UpdateHealth(v, b.Health, b.MaxHealth);
                if (alphaEnabled) v.HealthBar.gameObject.SetActive(isSelected || b.Health < b.MaxHealth || !b.IsComplete);
                v.ResearchBar.gameObject.SetActive(b.ActiveResearch != null);
                if (b.ActiveResearch != null)
                {
                    v.ResearchBar.rotation = cameraRotation;
                    v.ResearchFill.localScale = new Vector3(Mathf.Clamp01(1 - b.ActiveResearch.RemainingTicks / (float)b.ActiveResearch.TotalTicks), 1, 1);
                }
                UpdateFactionBuilding(v, b, isSelected);
            }
            PresentationMarkers.Buildings.End();
            PresentationMarkers.Resources.Begin();
            var resources = world.Resources;
            for (int resourceIndex = 0; resourceIndex < resources.Count; resourceIndex++)
            {
                var r = resources[resourceIndex];
                liveIds.Add(r.Id);
                if (!Visible(r.Id)) { if (visuals.TryGetValue(r.Id, out var hidden)) hidden.Root.gameObject.SetActive(false); continue; }
                if (r.RemainingAmount <= 0) { if (visuals.TryGetValue(r.Id, out var depleted)) depleted.Root.gameObject.SetActive(false); continue; }
                if (visuals.TryGetValue(r.Id, out var existing))
                {
                    existing.Root.gameObject.SetActive(r.RemainingAmount > 0);
                    if (r.Kind == ResourceKind.Wood) SetWorked(existing, worked.Contains(r.Id));
                    continue;
                }
                var v = NewVisual(r.Id, r.DefinitionId, .75f);
                v.Root.position = DefinitionLoader.ToWorld(r.Position);
                // Meshy nodes first; AlphaWorldArt.Resource itself stays procedural, its tests pin the ArtKit build.
                // A node wears the land it stands in; on a map without lands that is the map's own biome.
                string land = world.Lands.BiomeAt(r.Position);
                if (alphaEnabled && MeshyPropVisuals.TryResource(r.Kind, land, v.Model, r.Id) != null) { v.IsArt = true; continue; }
                if (alphaEnabled && AlphaWorldArt.Resource(r.Kind, v.Model, land) != null) { v.IsArt = true; continue; }
                if (sliceEnabled && ArtKit.CreateResource(r.Kind, v.Model) != null) { v.IsArt = true; continue; }
                Color tint = r.Kind == ResourceKind.Food ? new Color(.83f, .51f, .24f) : r.Kind == ResourceKind.Wood ? new Color(.32f, .55f, .31f) : r.Kind == ResourceKind.Metal ? new Color(.43f, .62f, .66f) : new Color(.64f, .63f, .56f);
                Shape(r.Kind.ToString(), r.Kind == ResourceKind.Wood ? PrimitiveType.Capsule : PrimitiveType.Cube, v.Root, new Vector3(0, .65f, 0), new Vector3(.9f, r.Kind == ResourceKind.Wood ? 1.1f : 1.2f, .9f), Material(tint));
            }
            PresentationMarkers.Resources.End();
            PresentationMarkers.Removed.Begin();
            removedIds.Clear();
            foreach (var pair in visuals) if (!liveIds.Contains(pair.Key)) removedIds.Add(pair.Key);
            foreach (int id in removedIds)
            {
                var visual = visuals[id];
                // A soldier who climbed aboard a ship is carried, not dead: it simply leaves the shore.
                if (world.TryGetPassenger(id, out _))
                { visual.Root.gameObject.SetActive(false); UnityEngine.Object.Destroy(visual.Root.gameObject); visuals.Remove(id); continue; }
                // A removed enemy can have left sight and died between presentation updates.
                // Its old rendered position cannot establish visibility at the moment of death.
                if (visual.Root.gameObject.activeSelf && (world.Vision == null || visual.OwnerId == MatchController.LocalPlayer))
                    Feedback?.Emit(FeedbackCue.Defeat, visual.Root.position);
                // A hull seen going down sinks; a replica's enemy may only have sailed out of sight.
                if (visual.IsShip && visual.Root.gameObject.activeSelf && (!world.IsNetworkReplica || visual.OwnerId == MatchController.LocalPlayer)) Sink(visual);
                if (visual.ImportedCharacter != null && visual.Root.gameObject.activeSelf && visual.OwnerId == MatchController.LocalPlayer)
                {
                    // Detach only the local visual; no enemy disappearance is interpreted as
                    // a death. Its driver hides the corpse if the ground leaves current vision.
                    visual.ImportedCharacter.transform.SetParent(root, true);
                    visual.ImportedCharacter.BeginDeath(world);
                }
                visual.Root.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(visual.Root.gameObject); visuals.Remove(id);
            }
            PresentationMarkers.Removed.End();
            PresentationMarkers.Effects.Begin();
            SyncProjectiles(alpha);
            SyncGunshots(alpha);
            PresentationMarkers.Effects.End();
            PresentationMarkers.Fog.Begin();
            fogView?.Sync();
            PresentationMarkers.Fog.End();
            PresentationMarkers.Scenery.Begin();
            environment?.Sync();
            SyncObjectives();
            Feedback?.Update();
            siegeEffects?.Sync();
            if (world.Vision != null && world.Vision.Revision != fogObstacleRevision)
            {
                fogObstacleRevision = world.Vision.Revision;
                // Only a change reaches the engine: nothing else shows or hides a filler.
                for (int i = 0; i < fogObstacles.Count; i++)
                {
                    bool explored = world.Vision.IsExplored(1, fogObstacleCells[i]);
                    if (explored != fogObstacleShown[i]) { fogObstacleShown[i] = explored; fogObstacles[i].gameObject.SetActive(explored); }
                }
            }
            PresentationMarkers.Scenery.End();
        }

        private Dictionary<string, BuildingDefinition> buildingDefinitions;
        private BuildingDefinition BuildingDefinitionFor(string id)
        {
            if (buildingDefinitions == null)
            {
                buildingDefinitions = new Dictionary<string, BuildingDefinition>();
                foreach (var definition in world.Definition.Buildings) if (!buildingDefinitions.ContainsKey(definition.Id)) buildingDefinitions.Add(definition.Id, definition);
            }
            return id != null && buildingDefinitions.TryGetValue(id, out var found) ? found : null;
        }

        private static void RefreshCosmetic(Visual visual)
        {
            if (visual.CosmeticRevision == CosmeticLoadout.Revision) return;
            AlphaWorldArt.ApplyOwnedCosmetic(visual.Model, visual.DefinitionId, visual.FactionKind, visual.OwnerId);
            visual.CosmeticRevision = CosmeticLoadout.Revision;
        }

        private void BuildUnit(Visual visual, UnitState unit)
        {
            var model = visual.Model;
            var team = unit.OwnerId == 1 ? blue : red;
            if (unit.IsPackedOutpost) return;
            bool cavalry = (unit.Tags & CombatTags.Cavalry) != 0;
            Shape("Body", PrimitiveType.Capsule, model, new Vector3(0, cavalry ? 1.35f : .72f, 0), new Vector3(.62f, .65f, .62f), team);
            if (cavalry)
            {
                Shape("Mount body", PrimitiveType.Cube, model, new Vector3(0, .75f, 0), new Vector3(.75f, .6f, 1.2f), neutral);
                Shape("Mount head", PrimitiveType.Cube, model, new Vector3(0, 1.15f, .58f), new Vector3(.37f, .65f, .42f), neutral);
                for (int x = -1; x <= 1; x += 2) for (int z = -1; z <= 1; z += 2)
                    Shape("Leg", PrimitiveType.Cube, model, new Vector3(x * .24f, .3f, z * .4f), new Vector3(.15f, .6f, .15f), dark);
                Shape("Lance", PrimitiveType.Cube, model, new Vector3(.5f, 1.2f, .75f), new Vector3(.075f, .075f, 1.8f), neutral);
            }
            else if (unit.IsWorker)
                Shape("Pack", PrimitiveType.Cube, model, new Vector3(0, .72f, -.34f), new Vector3(.52f, .55f, .3f), neutral);
            else if ((unit.Tags & CombatTags.Ranged) != 0)
            {
                Shape("Bow", PrimitiveType.Cube, model, new Vector3(.45f, 1, .15f), new Vector3(.1f, 1.15f, .1f), neutral);
                foreach (int direction in new[] { -1, 1 })
                {
                    var tip = Shape("Bow tip", PrimitiveType.Cube, model, new Vector3(.45f, 1 + direction * .52f, .02f), new Vector3(.09f, .42f, .09f), neutral);
                    tip.transform.localRotation = Quaternion.Euler(direction * -35, 0, 0);
                }
                Shape("Quiver", PrimitiveType.Cube, model, new Vector3(0, 1, -.3f), new Vector3(.3f, .65f, .25f), dark);
            }
            else if (unit.AttackDamage > 0)
                Shape("Spear", PrimitiveType.Cube, model, new Vector3(.45f, 1, 0), new Vector3(.07f, 2, .07f), neutral);
        }

        private readonly HashSet<int> worked = new HashSet<int>();
        private static MaterialPropertyBlock swayBlock;
        private static MaterialPropertyBlock readabilityBlock;
        private static readonly int RimStrengthId = Shader.PropertyToID("_RimStrength");
        // Every Meshy character ships with this "Readability rim" baked into its material (MeshyUnitBaker); a
        // dark-clothed orc on the volcanic waste's own dark ash needs more of it than the shader's plain default.
        private const float GroundRim = .08f, VolcanicRim = .26f;

        /// <summary>
        /// Lifts a Meshy character's rim light while it stands on volcanic ash, the one land dark enough to lose a
        /// dark-clothed figure against its own ground; every other biome keeps the shader's plain default. Cheap and
        /// presentation only: a property block override per renderer, written only when the ground under the unit
        /// changes, so a unit holding still off the ash costs nothing past its first frame.
        /// </summary>
        private static void SetOnDarkGround(Visual visual, bool onDarkGround)
        {
            if (visual.OnDarkGround == onDarkGround) return;
            visual.OnDarkGround = onDarkGround;
            if (visual.ReadabilityParts == null) visual.ReadabilityParts = visual.ImportedCharacter.GetComponentsInChildren<Renderer>(true);
            readabilityBlock = readabilityBlock ?? new MaterialPropertyBlock();
            foreach (var renderer in visual.ReadabilityParts)
            {
                if (renderer == null || renderer.sharedMaterial == null || !renderer.sharedMaterial.HasProperty(RimStrengthId)) continue;
                readabilityBlock.Clear();
                renderer.GetPropertyBlock(readabilityBlock);
                readabilityBlock.SetFloat(RimStrengthId, onDarkGround ? VolcanicRim : GroundRim);
                renderer.SetPropertyBlock(readabilityBlock);
            }
        }

        /// <summary>How hard a tree shakes while it is being felled. The landscape itself no longer moves at all.</summary>
        private const float FellingSway = 2.2f;

        /// <summary>
        /// Drives the shader's sway on one tree while somebody is cutting it, and only then. Renderers are
        /// gathered once and the block is written only when the state changes, so a still forest costs nothing.
        /// </summary>
        private static void SetWorked(Visual visual, bool worked)
        {
            if (visual.Worked == worked) return;
            visual.Worked = worked;
            if (visual.SwayParts == null) visual.SwayParts = visual.Root.GetComponentsInChildren<MeshRenderer>(true);
            swayBlock = swayBlock ?? new MaterialPropertyBlock();
            foreach (var renderer in visual.SwayParts)
            {
                if (renderer == null) continue;
                swayBlock.Clear();
                renderer.GetPropertyBlock(swayBlock);
                swayBlock.SetFloat("_Sway", worked ? FellingSway : 0);
                renderer.SetPropertyBlock(swayBlock);
            }
        }

        private void AddHealthBar(Visual visual, float height, float width)
        {
            var bar = new GameObject("Health bar").transform; bar.SetParent(visual.Root, false); bar.localPosition = Vector3.up * height;
            Shape("Background", PrimitiveType.Cube, bar, Vector3.zero, new Vector3(width + .08f, .16f, .045f), dark);
            var fill = new GameObject("Fill").transform; fill.SetParent(bar, false); fill.localPosition = new Vector3(-width / 2, 0, -.035f);
            Shape("Health", PrimitiveType.Cube, fill, new Vector3(width / 2, 0, 0), new Vector3(width, .095f, .03f), health);
            visual.HealthBar = bar; visual.HealthFill = fill;
            visual.Hit = Shape("Impact", PrimitiveType.Cylinder, visual.Root, new Vector3(0, .07f, 0), new Vector3(visual.Radius * 2.3f, .025f, visual.Radius * 2.3f), arrow);
            visual.Hit.SetActive(false);
        }

        private void AddResearchBar(Visual visual, float height, float width)
        {
            var bar = new GameObject("Research bar").transform; bar.SetParent(visual.Root, false); bar.localPosition = Vector3.up * height;
            Shape("Research background", PrimitiveType.Cube, bar, Vector3.zero, new Vector3(width + .08f, .13f, .045f), dark);
            var fill = new GameObject("Research fill").transform; fill.SetParent(bar, false); fill.localPosition = new Vector3(-width / 2, 0, -.035f);
            Shape("Research progress", PrimitiveType.Cube, fill, new Vector3(width / 2, 0, 0), new Vector3(width, .075f, .03f), arrow);
            visual.ResearchBar = bar; visual.ResearchFill = fill;
            bar.gameObject.SetActive(false);
        }

        private void UpdateHealth(Visual visual, int current, int maximum)
        {
            visual.HealthBar.rotation = cameraRotation;
            visual.HealthFill.localScale = new Vector3(Mathf.Clamp01(current / (float)maximum), 1, 1);
            if (current < visual.LastHealth) { visual.HitUntil = Time.unscaledTime + .15f; Feedback?.Emit(FeedbackCue.Impact, visual.Root.position); }
            visual.LastHealth = current;
            visual.Hit.SetActive(Time.unscaledTime < visual.HitUntil);
        }

        // Every unit turns along its path at a bounded rate instead of snapping to each
        // new heading once per 20 Hz tick: its facing eases between ticks as its position
        // does. Called every frame, so a unit that stood still starts turning from where
        // it stands. Simulated time drives it, so a paused match holds the pose; facing an
        // attack target turns faster, so a strike never lands while looking away.
        private const float TurnDegreesPerSecond = 540, EngageDegreesPerSecond = 1440;
        private Quaternion Turn(Visual v, Quaternion heading, float alpha, float degreesPerSecond)
        {
            double now = (world.TickIndex + Mathf.Clamp01(alpha)) * (double)Simulation.World.TickSeconds;
            bool first = v.FacingTime < 0;
            float delta = first ? 0 : (float)Math.Max(0, Math.Min(.25, now - v.FacingTime));
            v.FacingTime = now;
            return first ? heading : Quaternion.RotateTowards(v.Root.rotation, heading, degreesPerSecond * delta);
        }

        private void SyncProjectiles(float alpha)
        {
            liveIds.Clear();
            var shots = world.Projectiles;
            for (int shotIndex = 0; shotIndex < shots.Count; shotIndex++)
            {
                var projectile = shots[shotIndex];
                liveIds.Add(projectile.Id);
                if (world.Vision != null && !world.Vision.IsVisible(1, projectile.Position))
                { if (projectiles.TryGetValue(projectile.Id, out var hidden)) hidden.gameObject.SetActive(false); continue; }
                if (!projectiles.TryGetValue(projectile.Id, out var visual))
                {
                    visual = projectilePool.Count > 0 ? projectilePool.Pop() : Shape("Arrow", PrimitiveType.Cube, root, Vector3.zero, new Vector3(.075f, .075f, .65f), arrow).transform;
                    if (fallbackArrowMesh == null) fallbackArrowMesh = visual.GetComponent<MeshFilter>().sharedMesh;
                    visual.gameObject.SetActive(true); projectiles.Add(projectile.Id, visual);
                    var appearance = new ProjectileAppearance { SourceHeight = 1.05f * AlphaWorldArt.UnitScale, TargetHeight = 1.05f * AlphaWorldArt.UnitScale, Origin = DefinitionLoader.ToWorld(projectile.PreviousPosition), Distance = 1 };
                    if (Visible(projectile.SourceEntityId))
                    {
                        if (world.TryGetBuilding(projectile.SourceEntityId, out var sourceBuilding))
                        {
                            appearance.SourceHeight = sourceBuilding.DefinitionId == "keep" ? 4.15f : 3.50f;
                            appearance.Kind = sourceBuilding.DefinitionId == "keep" ? 1 : 0; appearance.Origin = DefinitionLoader.ToWorld(sourceBuilding.Position);
                        }
                        else if (world.TryGetUnit(projectile.SourceEntityId, out var sourceUnit))
                        {
                            appearance.SourceHeight = sourceUnit.ElevationMillimetres * .001f + (sourceUnit.DefinitionId == "ember_drake" ? 1.76f : 1.12f) * AlphaWorldArt.UnitScale;
                            appearance.Kind = sourceUnit.DefinitionId == "ember_drake" ? 2 : 0; appearance.Origin = DefinitionLoader.ToWorld(sourceUnit.Position);
                            // A ship's guns lob round shot, the keep's stone.
                            if (sourceUnit.Domain == MovementDomain.Water) { appearance.Kind = 1; appearance.SourceHeight = .75f; }
                            if (sourceUnit.DefinitionId == ImportedCharacterVisuals.GunpowderCorsairId)
                            {
                                appearance.Kind = 3;
                                appearance.SourceHeight = sourceUnit.ElevationMillimetres * .001f + 1.45f * AlphaWorldArt.UnitScale;
                                if (visuals.TryGetValue(sourceUnit.Id, out var sourceVisual) && sourceVisual.ImportedCharacter != null)
                                    appearance.SourceHeight = sourceVisual.ImportedCharacter.MuzzleWorldPosition.y;
                            }
                        }
                    }
                    if (Visible(projectile.TargetEntityId))
                    {
                        if (world.TryGetUnit(projectile.TargetEntityId, out var targetUnit))
                        { appearance.TargetHeight = targetUnit.ElevationMillimetres * .001f + .8f * AlphaWorldArt.UnitScale; appearance.Distance = Vector3.Distance(appearance.Origin, DefinitionLoader.ToWorld(targetUnit.Position)); }
                        else if (world.TryGetBuilding(projectile.TargetEntityId, out var targetBuilding))
                        { appearance.TargetHeight = 1.7f; appearance.Distance = Vector3.Distance(appearance.Origin, DefinitionLoader.ToWorld(targetBuilding.Position)); }
                    }
                    projectileAppearance.Add(projectile.Id, appearance);
                    if (alphaEnabled || appearance.Kind == 3)
                    {
                        visual.name = appearance.Kind == 3 ? "Pistol shot" : appearance.Kind == 1 ? "Keep stone" : appearance.Kind == 2 ? "Drake ember" : "Defensive arrow";
                        visual.localScale = Vector3.one; visual.GetComponent<MeshFilter>().sharedMesh = AlphaSiegeVisuals.Projectile(appearance.Kind);
                        visual.GetComponent<MeshRenderer>().sharedMaterial = ArtKit.Catalog.Material;
                    }
                    else
                    {
                        visual.name = "Arrow"; visual.localScale = new Vector3(.075f, .075f, .65f);
                        visual.GetComponent<MeshFilter>().sharedMesh = fallbackArrowMesh;
                        visual.GetComponent<MeshRenderer>().sharedMaterial = arrow;
                    }
                }
                visual.gameObject.SetActive(true);
                Vector3 previous = DefinitionLoader.ToWorld(projectile.PreviousPosition), current = DefinitionLoader.ToWorld(projectile.Position);
                float elevation = 1.05f * AlphaWorldArt.UnitScale;
                if (projectileAppearance.TryGetValue(projectile.Id, out var arc) && (alphaEnabled || arc.Kind == 3))
                {
                    float travel = Mathf.Clamp01(Vector3.Distance(arc.Origin, Vector3.Lerp(previous, current, alpha)) / Mathf.Max(.1f, arc.Distance));
                    elevation = Mathf.Lerp(arc.SourceHeight, arc.TargetHeight, travel) + Mathf.Sin(travel * Mathf.PI) * (arc.Kind == 3 ? 0 : arc.Kind == 1 ? 1.5f : .30f);
                }
                visual.position = Vector3.Lerp(previous, current, alpha) + Vector3.up * elevation;
                if ((current - previous).sqrMagnitude > .00001f) visual.rotation = Quaternion.LookRotation(current - previous);
            }
            removedIds.Clear();
            foreach (var pair in projectiles) if (!liveIds.Contains(pair.Key)) removedIds.Add(pair.Key);
            foreach (int id in removedIds)
            {
                var visual = projectiles[id]; visual.gameObject.SetActive(false); projectiles.Remove(id); projectileAppearance.Remove(id);
                if (projectilePool.Count < 128) projectilePool.Push(visual);
                else UnityEngine.Object.Destroy(visual.gameObject);
            }
        }

        public int Pick(Camera camera, Vector2 screen, float minimumPixels)
        {
            int best = 0; float bestDistance = float.MaxValue;
            foreach (var pair in visuals)
            {
                var v = pair.Value;
                if (!Visible(pair.Key)) continue;
                if (!v.Root.gameObject.activeSelf) continue;
                var p = camera.WorldToScreenPoint(v.Root.position + Vector3.up * .6f);
                if (p.z <= 0) continue;
                var edge = camera.WorldToScreenPoint(v.Root.position + Vector3.up * .6f + camera.transform.right * v.Radius);
                float radius = Mathf.Max(minimumPixels, Vector2.Distance(p, edge));
                float distance;
                if (alphaEnabled && v.IsUnit)
                {
                    // Pick the canonical body capsule; wings, cosmetics and selection rings cannot inflate it.
                    Vector2 low = camera.WorldToScreenPoint(v.Root.position + Vector3.up * .18f);
                    Vector2 high = camera.WorldToScreenPoint(v.Root.position + Vector3.up * (AlphaWorldArt.UnitHeight(v.DefinitionId) -.18f));
                    Vector2 axis = high - low;
                    Vector2 nearest = low + axis * (axis.sqrMagnitude < .001f ? 0 : Mathf.Clamp01(Vector2.Dot(screen - low, axis) / axis.sqrMagnitude));
                    distance = Vector2.Distance(screen, nearest);
                }
                else distance = Vector2.Distance(screen, p);
                if (distance > radius || distance >= bestDistance) continue;
                best = pair.Key; bestDistance = distance;
            }
            return best;
        }

        public List<int> VisibleUnits(Camera camera, int ownerId, string definitionId = null, Rect? rectangle = null)
        {
            var ids = new List<int>();
            foreach (var u in world.Units)
            {
                if (u.OwnerId != ownerId || !Visible(u.Id) || !visuals.ContainsKey(u.Id) || (definitionId != null && u.DefinitionId != definitionId)) continue;
                var p = camera.WorldToScreenPoint(visuals[u.Id].Root.position);
                if (p.z > 0 && p.x >= 0 && p.x <= Screen.width && p.y >= 0 && p.y <= Screen.height && (!rectangle.HasValue || rectangle.Value.Contains(p))) ids.Add(u.Id);
            }
            return ids;
        }

        private Visual NewVisual(int id, string label, float radius, bool unit = false)
        {
            var entity = new GameObject(label + " #" + id).transform;
            entity.SetParent(root, false);
            var model = new GameObject("Model").transform; model.SetParent(entity, false);
            var marker = SelectionMarker(entity, radius, unit);
            marker.SetActive(false);
            var visual = new Visual { Root = entity, Model = model, Selection = marker, Radius = radius, DefinitionId = label };
            visuals.Add(id, visual); CreatedLastSync++;
            return visual;
        }

        private GameObject SelectionMarker(Transform entity, float radius, bool unit)
        {
            GameObject marker;
            // A building ring traces its footprint, but a unit's radius is the tap margin: drawn at full
            // width it circled the character like a wheel instead of marking the ground at its feet.
            float ringRadius = radius * (unit ? .72f : 1.1f), ringWidth = unit ? .065f * AlphaWorldArt.UnitScale : .065f;
            if (alphaEnabled)
            {
                marker = new GameObject("Selection"); marker.transform.SetParent(entity, false);
                var ring = marker.AddComponent<LineRenderer>(); ring.useWorldSpace = false; ring.loop = true; ring.positionCount = 48;
                ring.startWidth = ring.endWidth = ringWidth; ring.sharedMaterial = selection; ring.shadowCastingMode = ShadowCastingMode.Off; ring.receiveShadows = false;
                for (int i = 0; i < 48; i++) { float angle = i * Mathf.PI * 2 / 48; ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * ringRadius, .045f, Mathf.Sin(angle) * ringRadius)); }
            }
            else marker = Shape("Selection", PrimitiveType.Cylinder, entity, new Vector3(0, .025f, 0), new Vector3(ringRadius * 2, .015f, ringRadius * 2), selection);
            return marker;
        }

        private Material Material(Color color)
        {
            var template = Resources.Load<Material>("Materials/Greybox");
            if (template == null) throw new InvalidOperationException("Greybox material missing. Run Emberfield project verification.");
            var material = new Material(template) { color = color, enableInstancing = true };
            material.SetFloat("_Smoothness", .15f);
            materials.Add(material);
            return material;
        }

        private void AddOwnerMarker(Visual visual)
        {
            var marker = new GameObject("Owner marker"); marker.transform.SetParent(visual.Root, false);
            var ring = marker.AddComponent<LineRenderer>(); ring.useWorldSpace = false; ring.loop = true; ring.positionCount = 32;
            ring.startWidth = ring.endWidth = (visual.IsReferenceCharacter ? .075f : .045f) * AlphaWorldArt.UnitScale; ring.sharedMaterial = visual.OwnerId == MatchController.LocalPlayer ? blue : red;
            ring.shadowCastingMode = ShadowCastingMode.Off; ring.receiveShadows = false;
            for (int i = 0; i < 32; i++)
            {
                float angle = i * Mathf.PI * 2 / 32;
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * visual.Radius * .55f, .04f, Mathf.Sin(angle) * visual.Radius * .55f));
            }
        }

        private static GameObject Shape(string name, PrimitiveType primitive, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(primitive);
            go.name = name; go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = scale;
            UnityEngine.Object.Destroy(go.GetComponent<Collider>());
            var renderer = go.GetComponent<Renderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            return go;
        }

        private bool Visible(int id) => world.Vision == null || world.Vision.IsEntityVisible(1, id);
        public void Dispose() { fogView?.Dispose(); biome?.Dispose(); environment?.Dispose(); siegeEffects?.Dispose(); Feedback?.Dispose(); if (root != null) UnityEngine.Object.Destroy(root.gameObject); foreach (var m in materials) UnityEngine.Object.Destroy(m); }
    }
}
