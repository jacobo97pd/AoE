using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Emberfield.Presentation;
using Emberfield.Simulation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Emberfield.Editor
{
    /// <summary>
    /// Inspection images of the shipping map definitions and procedural presentation assets.
    /// This editor-only view has no HUD or fog; it never changes a saved scene or match.
    /// </summary>
    public static class VisualMapAtlas
    {
        private const int Layer = 28, Width = 1800, Height = 1200;
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        [Serializable] private sealed class Entry
        {
            public string id, name, category = "maps", biome, description, image, mapId;
            public string view = "inspección sin niebla", realms = "histórico y fantástico", faction, mode = "Conquest";
            public int width = Width, height = Height, tick = 0;
        }
        [Serializable] private sealed class Document { public Entry[] items; }
        private sealed class Detail
        {
            public string Id, Name, Description;
            public Vector3 Target;
            public float Size, Yaw;
            public Detail(string id, string name, string description, Vector3 target, float size, float yaw = 35)
            { Id = id; Name = name; Description = description; Target = target; Size = size; Yaw = yaw; }
        }

        /// <summary>Renders only the map sheets, so a terrain change can be reviewed on its own.</summary>
        public static void Run()
        {
            string directory = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "docs/art/visual-atlas");
            var arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index < arguments.Length - 1; index++)
                if (arguments[index] == "-atlasOutput") directory = arguments[index + 1];
            Export(directory);
            Debug.Log("EMBERFIELD_MAP_ATLAS_OK");
        }

        public static void Export(string directory)
        {
            Directory.CreateDirectory(directory);
            var entries = new List<Entry>();
            ExportMap(directory, "amber_crossing", "Amber Crossing", "forest", "aven", entries, new[] {
                new Detail("settlement", "Poblado inicial", "Hearth de Aven, trabajadores y recursos en sus posiciones de inicio.", new Vector3(26, .8f, 26), 11),
                new Detail("bridge", "Puente del norte", "Puente de madera sobre el río; el agua y el cruce transitable salen del propio mapa.", new Vector3(72, .4f, 20), 11, 20),
                new Detail("ford", "Vado central", "El vado del centro es también el objetivo en disputa: agua transitable sin puente.", new Vector3(72, .4f, 56), 12, 20)
            });
            ExportMap(directory, "sapphire_coast", "Sapphire Coast", "caribbean", "aven", entries, new[] {
                new Detail("harbor", "Puerto y barco", "Muelle, velero, palmeras y agua costera. El barco y el puerto son decoración; no hay combate naval.", new Vector3(9, 1, 20), 10, -35),
                new Detail("bay", "Bahía e islote", "Ruinas sobre el islote rocoso, muelle y palmeras dentro de la bahía.", new Vector3(20, 1, 38), 12, 30),
                new Detail("settlement", "Base en la costa", "Asentamiento inicial, recursos y senderos del bioma caribeño.", new Vector3(32, .7f, 24), 11, 35)
            });
            ExportMap(directory, "sunscar_basin", "Sunscar Basin", "desert", "serevin", entries, new[] {
                new Detail("oasis", "Oasis", "Agua y palmeras datileras del oasis, al final del cauce que baja de las dunas.", new Vector3(59, .8f, 44), 11, 30),
                new Detail("pyramid", "Pirámide", "Monumento dentro del campo de juego, sobre terreno intransitable propio.", new Vector3(36.5f, 1.2f, 52.5f), 10, 35),
                new Detail("stone_bridge", "Puente de piedra", "El cruce de piedra sobre el cauce, único paso en ese tramo.", new Vector3(63, .8f, 25), 10, 20)
            });
            // Elves against orcs, so the atlas shows the forest, the volcanic waste and the neutral highland at once.
            ExportMap(directory, "legend_lands", "Tierras de Leyenda", "highland", "verdant", entries, new[] {
                new Detail("settlement", "Poblado en el bosque élfico", "Hearth élfico, trabajadores y recursos en la tierra de su cultura.", new Vector3(26, .8f, 26), 11),
                new Detail("lava_ford", "Vado del río de lava", "El camino trasero de los orcos vadea el río de lava que baja del volcán.", new Vector3(82.5f, .5f, 86), 12, 20),
                new Detail("crown", "Centro neutral", "La franja neutral de montaña con la baliza central y los menhires.", new Vector3(72, .5f, 56), 14, 35)
            });
            File.WriteAllText(Path.Combine(directory, "maps.json"), JsonUtility.ToJson(new Document { items = entries.ToArray() }, true));
            Debug.Log("Visual map atlas exported: " + entries.Count + " actual-map inspection images to " + Path.GetFullPath(directory));
        }

        private static void ExportMap(string directory, string id, string name, string biome, string faction, List<Entry> entries, Detail[] details)
        {
            // The parent atlas runner supplies an empty, isolated editor scene.
            // Reuse it: Unity cannot additively create another scene while that
            // first untitled scene is unsaved. All objects remain under stage.
            var owned = new List<Object>();
            var stage = new GameObject("Map atlas inspection: " + id);
            var previousSun = RenderSettings.sun;
            var previousAmbientMode = RenderSettings.ambientMode;
            var previousSky = RenderSettings.ambientSkyColor;
            var previousEquator = RenderSettings.ambientEquatorColor;
            var previousGround = RenderSettings.ambientGroundColor;
            float previousAmbientIntensity = RenderSettings.ambientIntensity;
            try
            {
                var world = DefinitionLoader.CreateOfflineWorld(faction, VictoryMode.Conquest, id);
                RevealInspection(world);
                var cameraObject = new GameObject("Inspection camera"); cameraObject.transform.SetParent(stage.transform, false);
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false; camera.orthographic = true; camera.aspect = Width / (float)Height;
                camera.nearClipPlane = .1f; camera.farClipPlane = 600;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.cullingMask = 1 << Layer;
                var sunObject = new GameObject("Actual biome sunlight"); sunObject.transform.SetParent(stage.transform, false);
                var sun = sunObject.AddComponent<Light>(); sun.type = LightType.Directional; sun.cullingMask = 1 << Layer; sun.renderMode = LightRenderMode.ForcePixel;
                AlphaLighting.Configure(camera, sun, stage.transform, world.Map); RenderSettings.sun = sun;
                var cameraData = camera.GetUniversalAdditionalCameraData(); cameraData.volumeLayerMask = 1 << Layer;
                var terrain = new AmberBiome(world.Map, stage.transform, world.Lands);
                OwnFields(terrain, owned, "mesh", "material");
                var environment = new AlphaEnvironment(world, stage.transform);
                OwnFields(environment, owned, "water", "ownedMeshes", "ownedMaterials");
                BuildStartModels(world, stage.transform);
                var underLandmarks = AlphaEnvironment.LandmarkFootprints(world);
                foreach (var cell in world.Map.BlockedCells)
                {
                    if (AlphaEnvironment.IsWaterCell(world.Map, cell.X, cell.Z)) continue;
                    if (underLandmarks.TryGetValue(cell.Z * world.Map.WidthCells + cell.X, out string rocks) && rocks == null) continue;
                    var rock = AlphaEnvironment.CreateObstacle(stage.transform, cell.X, cell.Z, rocks ?? world.Lands.BiomeAt(cell.X, cell.Z), rocks != null);
                    rock.position = new Vector3((cell.X + .5f) * world.Map.CellSizeMillimetres * .001f, 0, (cell.Z + .5f) * world.Map.CellSizeMillimetres * .001f);
                }
                foreach (var transform in stage.GetComponentsInChildren<Transform>(true)) transform.gameObject.layer = Layer;
                foreach (var lod in stage.GetComponentsInChildren<LODGroup>(true)) lod.ForceLOD(0);
                // Read the current default asset colors, independent of the developer's local wardrobe.
                AlphaWorldArt.ApplyCosmetic(stage.transform, default);
                float mapWidth = world.Map.WidthCells * world.Map.CellSizeMillimetres * .001f;
                float mapDepth = world.Map.HeightCells * world.Map.CellSizeMillimetres * .001f;
                Aim(camera, new Vector3(mapWidth * .5f, 0, mapDepth * .5f), 50, 0, 62);
                FitOverview(camera, stage.transform);
                Capture(camera, Path.Combine(directory, "map-" + id + ".png"));
                bool fantasyOnly = !ContentRealms.IsMapAllowedInRealm(id, ContentRealms.Historical);
                entries.Add(new Entry { id = id, name = name + " · mapa completo", biome = biome, mapId = id, faction = faction, image = "map-" + id + ".png",
                    realms = fantasyOnly ? "fantástico" : "histórico y fantástico",
                    description = "Vista de inspección del escenario real, sin niebla ni HUD, al inicio de Conquest. " + world.Map.WidthCells + " × " + world.Map.HeightCells + " celdas. " +
                        (fantasyOnly ? "Este terreno es solo del universo de fantasía y cada tierra toma la cultura de quien empieza en ella" : "Este terreno está disponible en los dos universos PvP separados") +
                        "; aquí se muestra un único emparejamiento del mismo universo." });
                foreach (var detail in details)
                {
                    Aim(camera, detail.Target, detail.Size, detail.Yaw, 52);
                    string image = "map-" + id + "-" + detail.Id + ".png";
                    Capture(camera, Path.Combine(directory, image));
                    entries.Add(new Entry { id = id + "-" + detail.Id, name = name + " · " + detail.Name, biome = biome, mapId = id, faction = faction,
                        image = image, description = detail.Description + " Vista de inspección sin niebla, con iluminación y geometría reales del juego." });
                }
            }
            finally
            {
                // Runtime Dispose methods use delayed Destroy, so the isolated editor exporter owns immediate cleanup.
                foreach (var lighting in stage.GetComponentsInChildren<AlphaLighting>(true))
                {
                    var field = typeof(AlphaLighting).GetField("profile", Fields);
                    var profile = field?.GetValue(lighting) as VolumeProfile;
                    if (profile != null)
                    {
                        foreach (var component in profile.components) if (component != null) owned.Add(component);
                        owned.Add(profile); field.SetValue(lighting, null);
                    }
                }
                Object.DestroyImmediate(stage);
                foreach (var asset in owned) if (asset != null && !EditorUtility.IsPersistent(asset)) Object.DestroyImmediate(asset);
                RenderSettings.sun = previousSun; RenderSettings.ambientMode = previousAmbientMode;
                RenderSettings.ambientSkyColor = previousSky; RenderSettings.ambientEquatorColor = previousEquator;
                RenderSettings.ambientGroundColor = previousGround; RenderSettings.ambientIntensity = previousAmbientIntensity;
            }
        }

        private static void BuildStartModels(World world, Transform parent)
        {
            FactionKind Faction(int owner)
            {
                if (world.TryGetPlayer(owner, out var player))
                    foreach (var definition in world.Definition.Factions) if (definition.Id == player.FactionId) return definition.Kind;
                throw new InvalidOperationException("Missing map start faction.");
            }
            foreach (var unit in world.Units)
            {
                var model = AlphaWorldArt.Unit(unit.DefinitionId, Faction(unit.OwnerId), parent, unit.OwnerId);
                if (model == null) throw new InvalidOperationException("Missing unit artwork: " + unit.DefinitionId);
                model.position = DefinitionLoader.ToWorld(unit.Position) + Vector3.up * (unit.ElevationMillimetres * .001f);
            }
            foreach (var building in world.Buildings)
            {
                var model = AlphaWorldArt.Building(building.DefinitionId, Faction(building.OwnerId), parent, building.OwnerId,
                    building.WidthCells * world.Map.CellSizeMillimetres * .001f, building.DepthCells * world.Map.CellSizeMillimetres * .001f);
                if (model == null) throw new InvalidOperationException("Missing building artwork: " + building.DefinitionId);
                model.position = DefinitionLoader.ToWorld(building.Position); model.rotation = Quaternion.Euler(0, 180, 0);
            }
            foreach (var resource in world.Resources)
            {
                if (resource.RemainingAmount <= 0) continue;
                var model = AlphaWorldArt.Resource(resource.Kind, parent, world.Lands.BiomeAt(resource.Position));
                if (model == null) throw new InvalidOperationException("Missing resource artwork: " + resource.Kind);
                model.position = DefinitionLoader.ToWorld(resource.Position);
            }
        }

        private static void RevealInspection(World world)
        {
            // Only this detached inspection world's cell masks are touched. No rules or map assets are edited.
            var players = typeof(FogOfWarSystem).GetField("players", Fields)?.GetValue(world.Vision) as IDictionary;
            if (players == null) throw new InvalidOperationException("Inspection visibility accessor no longer matches FogOfWarSystem.");
            foreach (DictionaryEntry player in players)
                foreach (string name in new[] { "Visible", "Explored" })
                {
                    var cells = player.Value.GetType().GetField(name, Fields)?.GetValue(player.Value) as bool[];
                    if (cells == null) throw new InvalidOperationException("Inspection visibility mask missing: " + name);
                    for (int i = 0; i < cells.Length; i++) cells[i] = true;
                }
        }

        private static void OwnFields(object instance, List<Object> owned, params string[] names)
        {
            foreach (string name in names)
            {
                object value = instance.GetType().GetField(name, Fields)?.GetValue(instance);
                if (value is Object asset) owned.Add(asset);
                else if (value is IEnumerable values) foreach (object item in values) if (item is Object child) owned.Add(child);
            }
        }

        private static void Aim(Camera camera, Vector3 target, float size, float yaw, float pitch)
        {
            camera.transform.rotation = Quaternion.Euler(pitch, yaw, 0);
            camera.transform.position = target - camera.transform.forward * 55;
            camera.orthographicSize = size;
        }

        private static void FitOverview(Camera camera, Transform stage)
        {
            float minX = float.PositiveInfinity, maxX = float.NegativeInfinity, minY = float.PositiveInfinity, maxY = float.NegativeInfinity;
            foreach (var renderer in stage.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled) continue;
                var bounds = renderer.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var point = camera.worldToCameraMatrix.MultiplyPoint3x4(corner);
                    minX = Mathf.Min(minX, point.x); maxX = Mathf.Max(maxX, point.x); minY = Mathf.Min(minY, point.y); maxY = Mathf.Max(maxY, point.y);
                }
            }
            if (float.IsInfinity(minX)) throw new InvalidOperationException("No visible map geometry.");
            camera.transform.position += camera.transform.right * ((minX + maxX) * .5f) + camera.transform.up * ((minY + maxY) * .5f);
            camera.orthographicSize = Mathf.Max((maxY - minY) * .5f, (maxX - minX) * .5f / camera.aspect) * 1.06f;
        }

        private static void Capture(Camera camera, string path)
        {
            var previous = RenderTexture.active;
            var texture = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { name = "Map atlas output", antiAliasing = 1 };
            var pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            texture.Create(); camera.targetTexture = texture;
            try
            {
                var request = new RenderPipeline.StandardRequest { destination = texture };
                // The parent exporter initializes URP before this synchronous capture step.
                if (!RenderPipeline.SupportsRenderRequest(camera, request)) throw new InvalidOperationException("The active render pipeline does not support the map atlas capture request.");
                for (int warmup = 0; warmup < 3; warmup++) RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = texture; pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0); pixels.Apply();
                File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = previous;
                texture.Release(); Object.DestroyImmediate(texture); Object.DestroyImmediate(pixels);
            }
        }
    }
}
