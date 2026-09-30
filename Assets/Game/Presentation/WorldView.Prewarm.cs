using System;
using System.Collections.Generic;
using Emberfield.Simulation;
using UnityEngine;

namespace Emberfield.Presentation
{
    public sealed partial class WorldView
    {
        /// <summary>Development players and the editor skip the prewarm with this, to compare against it.</summary>
        public const string NoPrewarmFlag = "-emberfieldNoPrewarm";
        // How far past the ground the prewarm stands along the camera's line of sight: about 16 m under the terrain at
        // the camera's 55°, inside the far plane and the desktop's 110 m of shadows, and deeper than any model is tall.
        private const float PrewarmDepth = 20;
        private const int PrewarmedProjectiles = 32;
        private Transform prewarm;
        /// <summary>Models the prewarm read and drew, and what building them cost; zero when it did not run.</summary>
        public int PrewarmedModels { get; private set; }
        public double PrewarmMilliseconds { get; private set; }

        /// <summary>
        /// Pays while the match loads what it would otherwise pay mid-battle the first time it shows something. Every
        /// model its players can field is read from disk and recoloured for its owner, and drawn once with a ring, a
        /// health bar and each projectile, so the textures upload and the GPU builds its pipeline states behind the
        /// loading frame instead of on the frame a unit first walks out of a barracks or a new building is laid out.
        /// The pieces stand under the ground on the camera's own line of sight: inside the view, so they draw, and behind
        /// the terrain, so nothing shows. They go at the first Update after that frame (DrawnOnce); the models stay loaded
        /// in their catalogues' caches, as they would after first use. Presentation only; nothing here touches the World.
        /// </summary>
        public void Prewarm()
        {
            if (prewarm != null || !PrewarmEnabled()) return;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            prewarm = new GameObject("Prewarm (drawn once, under the ground)", typeof(DrawnOnce)).transform;
            prewarm.SetParent(root, false);
            var view = camera.transform;
            var ground = new Plane(Vector3.up, Vector3.zero);
            var sight = new Ray(view.position, view.forward);
            prewarm.position = (ground.Raycast(sight, out float distance) ? sight.GetPoint(distance) : view.position + view.forward * 55) + view.forward * PrewarmDepth;
            var seen = new HashSet<string>();
            string realm = world.Map.RealmId;
            foreach (var player in world.Players)
            {
                var faction = FactionFor(player.Id);
                if (faction == null) continue;
                foreach (var unit in world.Definition.Units)
                {
                    var entry = MeshyUnitVisuals.Enabled ? MeshyUnitVisuals.Resolve(unit.Id, faction.Kind) : null;
                    if (entry != null) { if (seen.Add(entry.id + "/" + player.Id) && MeshyUnitVisuals.Create(entry, player.Id, prewarm) != null) PrewarmedModels++; continue; }
                    string reference = ReferenceCharacterVisuals.Resolve(unit.Id, faction.Kind, realm);
                    var character = reference != null ? ReferenceCharacterVisuals.Find(reference) : null;
                    if (character != null && character.realm == realm)
                    {
                        var prefab = seen.Add(reference) ? ReferenceCharacterVisuals.Prefab(reference) : null;
                        if (prefab) { UnityEngine.Object.Instantiate(prefab, prewarm, false); PrewarmedModels++; }
                        continue;
                    }
                    if (alphaEnabled && MeshyPropVisuals.ResolveSiege(unit.Id, faction.Kind) is MeshyPropVisuals.Entry siege && seen.Add(siege.id + "/" + player.Id) &&
                        MeshyPropVisuals.TrySiege(unit.Id, player.Id, faction.Kind, prewarm) != null) PrewarmedModels++;
                    // A sea to sail: the hull this player can launch.
                    if (alphaEnabled && unit.Domain == MovementDomain.Water && world.HasDeepWater && world.ValidateUnitRecruitment(player.Id, unit.Id).Accepted &&
                        MeshyPropVisuals.ResolveShip(unit.Id, faction.Kind) is MeshyPropVisuals.Entry ship && seen.Add(ship.id + "/" + player.Id) &&
                        MeshyPropVisuals.TryShip(unit.Id, player.Id, faction.Kind, prewarm) != null) PrewarmedModels++;
                }
                foreach (var building in world.Definition.Buildings)
                {
                    var entry = MeshyBuildingVisuals.Resolve(building.Id, faction.Kind);
                    if (entry == null || !seen.Add(entry.id + "/" + player.Id)) continue;
                    float cell = world.Map.CellSizeMillimetres * .001f;
                    if (MeshyBuildingVisuals.TryCreate(building.Id, player.Id, faction.Kind, prewarm, building.WidthCells * cell, building.DepthCells * cell) != null) PrewarmedModels++;
                }
            }
            // What every match shows sooner or later: a selection ring, an owner ring, the health and research bars and a hit.
            var sample = new Visual { Root = prewarm, Model = prewarm, Radius = 1, OwnerId = MatchController.LocalPlayer };
            SelectionMarker(prewarm, 1, true);
            AddOwnerMarker(sample);
            AddHealthBar(sample, 1, 1); AddResearchBar(sample, .8f, 1);
            sample.ResearchBar.gameObject.SetActive(true); sample.Hit.SetActive(true);
            // The projectile pool starts full, and each projectile's mesh is drawn once.
            for (int i = projectilePool.Count; i < PrewarmedProjectiles; i++)
            {
                var spare = Shape("Arrow", PrimitiveType.Cube, root, Vector3.zero, new Vector3(.075f, .075f, .65f), arrow).transform;
                spare.gameObject.SetActive(false); projectilePool.Push(spare);
            }
            if (alphaEnabled)
                for (int kind = 0; kind <= 3; kind++)
                {
                    var shot = Shape("Projectile", PrimitiveType.Cube, prewarm, Vector3.zero, Vector3.one, arrow);
                    shot.GetComponent<MeshFilter>().sharedMesh = AlphaSiegeVisuals.Projectile(kind);
                    shot.GetComponent<MeshRenderer>().sharedMaterial = ArtKit.Catalog.Material;
                }
            PrewarmMilliseconds = watch.Elapsed.TotalMilliseconds;
            if (Debug.isDebugBuild) Debug.Log(FormattableString.Invariant($"EMBERFIELD_PREWARM models={PrewarmedModels} ms={PrewarmMilliseconds:0.0}"));
        }

        private static bool PrewarmEnabled()
        {
            if (!Debug.isDebugBuild && !Application.isEditor) return true;
            return Array.IndexOf(Environment.GetCommandLineArgs(), NoPrewarmFlag) < 0;
        }
    }
}
