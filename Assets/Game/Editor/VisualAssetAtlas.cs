using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Emberfield.Presentation;
using Emberfield.Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Emberfield.Editor
{
    // Inspection export only. Uses current game mesh factories; never edits rules, scenes or the player.
    public static class VisualAssetAtlas
    {
        [Serializable] private sealed class Entry
        {
            public string key,id,name,category,faction="",factionName="",realm="shared",biome="shared",image,description="",cost="",era="";
            public int baseHealth,population;
            public bool canonical;
        }
        [Serializable] private sealed class Sheet { public string id,name,image; }
        [Serializable] private sealed class Manifest
        {
            public string generatedUtc,unityVersion,sourceCommit;
            public string method="Current Unity mesh factories, highest native LOD, unchanged silhouettes and team masks. Isolated studio lighting and fitted camera; models are fitted individually, not shown at a common scale. Maps are inspection views without fog. No generated illustration or gameplay state modification.";
            public List<Entry> items=new List<Entry>();
            public List<Sheet> sheets=new List<Sheet>();
        }
        private static readonly string[] PropIds={"tree","grass","reeds","windmill","village","palm","ship","harbor","ruins","date_palm","pyramid","caravan","dune","dry_grass","obstacle","coastal_rock","desert_rock"};
        private static readonly string[] PropNames={"Árbol de borde","Hierba","Juncos","Molino","Aldea decorativa","Palmera costera","Barco decorativo","Muelle decorativo","Ruinas","Palmera datilera","Pirámide","Caravana de camellos","Duna","Hierba seca","Roca de bosque","Roca costera","Roca del desierto"};
        private static string output;
        private static Camera camera;
        private static Manifest manifest;
        private static GameDefinition rules;
        private static GameObject stage;
        private static readonly List<Texture2D> retained=new List<Texture2D>();

        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Run this isolated inspection export with tools/Export-VisualAtlas.ps1.");
            int originalAA=QualitySettings.antiAliasing;
            try
            {
                var args=Environment.GetCommandLineArgs(); int option=Array.IndexOf(args,"-atlasOutput");
                output=Path.GetFullPath(option>=0 && option+1<args.Length ? args[option+1] : "docs/art/visual-atlas");
                Directory.CreateDirectory(output); Directory.CreateDirectory(Path.Combine(output,"images"));
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                rules=JsonUtility.FromJson<GameDefinition>(Resources.Load<TextAsset>("Definitions/greybox").text);
                if (ArtKit.Catalog?.Material==null) throw new InvalidOperationException("The game's native art catalog is missing.");
                manifest=new Manifest { generatedUtc=DateTime.UtcNow.ToString("o"),unityVersion=Application.unityVersion,sourceCommit=ReadRevision() };
                stage=new GameObject("Visual atlas inspection stage");
                camera=new GameObject("Visual atlas studio camera",typeof(Camera)).GetComponent<Camera>();
                camera.orthographic=true; camera.cullingMask=1<<28; camera.clearFlags=CameraClearFlags.SolidColor;
                camera.backgroundColor=new Color(.075f,.125f,.15f); camera.allowHDR=false; camera.allowMSAA=true;
                camera.nearClipPlane=.05f; camera.farClipPlane=250; camera.enabled=false;
                var data=camera.GetUniversalAdditionalCameraData(); data.renderPostProcessing=false; data.renderShadows=false;
                foreach (var faction in rules.Factions)
                {
                    foreach (var unit in rules.Units)
                    {
                        if (!Eligible(unit.RequiredFactionId,faction.Id)) continue;
                        bool canonical=!string.IsNullOrEmpty(unit.RequiredFactionId) || faction.Id=="aven";
                        var entry=new Entry { key="unit-"+faction.Id+"-"+unit.Id,id=unit.Id,name=UnitName(unit.Id),category="units",faction=faction.Id,factionName=faction.DisplayName,realm=faction.RealmId,canonical=canonical,
                            baseHealth=unit.MaxHealth,population=unit.PopulationCost,cost=Cost(unit.Cost),era=unit.RequiredEraId,
                            description=unit.DisplayName+" · "+UnitDescription(unit) };
                        Export(entry,()=>AlphaWorldArt.Unit(unit.Id,faction.Kind,stage.transform,1));
                    }
                    foreach (var building in rules.Buildings)
                    {
                        if (!Eligible(building.RequiredFactionId,faction.Id)) continue;
                        var entry=new Entry { key="building-"+faction.Id+"-"+building.Id,id=building.Id,name=BuildingName(building.Id),category="buildings",faction=faction.Id,factionName=faction.DisplayName,realm=faction.RealmId,
                            canonical=!string.IsNullOrEmpty(building.RequiredFactionId) || faction.Id=="aven",baseHealth=building.MaxHealth,cost=Cost(building.Cost),era=building.RequiredEraId,
                            description=building.DisplayName+" · huella "+building.WidthCells+"×"+building.DepthCells+" celdas." };
                        if (building.Id=="beast_lodge" && (faction.Id=="aven" || faction.Id=="serevin" || faction.Id=="skeld")) entry.description+=" Esta facción puede construirla, pero no tiene criaturas compatibles para entrenar.";
                        Export(entry,()=>AlphaWorldArt.Building(building.Id,faction.Kind,stage.transform,1,building.WidthCells,building.DepthCells));
                    }
                }
                foreach (string biome in new[]{"forest","caribbean","desert"})
                    foreach (ResourceKind kind in new[]{ResourceKind.Food,ResourceKind.Wood})
                        Export(new Entry { key="resource-"+biome+"-"+kind,id=biome+"-"+kind,name=ResourceName(kind,biome),category="resources",biome=biome,canonical=true,description="Recurso recolectable. Modelo nativo de "+biome+"." },()=>AlphaWorldArt.Resource(kind,stage.transform,biome));
                foreach (ResourceKind kind in new[]{ResourceKind.Metal,ResourceKind.Stone})
                    Export(new Entry { key="resource-"+kind,id=kind.ToString(),name=ResourceName(kind,"shared"),category="resources",canonical=true,description="La misma geometría aparece en los tres biomas." },()=>AlphaWorldArt.Resource(kind,stage.transform));
                var propFactory=typeof(AlphaEnvironment).GetMethod("Create",BindingFlags.Static|BindingFlags.NonPublic);
                for(int i=0;i<PropIds.Length;i++)
                {
                    string id=PropIds[i];
                    Export(new Entry { key="prop-"+id,id=id,name=PropNames[i],category="scenery",canonical=true,biome=PropBiome(id),description=id=="ship" || id=="harbor" || id=="caravan" ? "Decoración del escenario. No es una unidad entrenable ni un sistema de combate naval." : i>=14 ? "Aspecto de una celda de terreno bloqueada; no se construye." : "Objeto decorativo del escenario; no entra en el ejército." },()=> (Transform)propFactory.Invoke(null,new object[]{id,stage.transform}));
                }
                Export(new Entry { key="objective-beacon",id="beacon",name="Baliza de Dominion",category="objectives",canonical=true,description="Objetivo público capturable del mapa." },()=>AlphaWorldArt.Beacon(stage.transform));
                var projectileFactory=typeof(AlphaWorldArt).Assembly.GetType("Emberfield.Presentation.AlphaSiegeVisuals").GetMethod("Projectile",BindingFlags.Static|BindingFlags.NonPublic);
                string[] projectileNames={"Flecha","Proyectil de fortaleza","Fuego de dragón / aceite en caída"};
                for (int i=0;i<3;i++)
                {
                    int kind=i;
                    Export(new Entry { key="projectile-"+i,id="projectile-"+i,name=projectileNames[i],category="effects",canonical=true,description="Malla del proyectil real, ampliada para inspección. No es una unidad." },()=>MeshObject((Mesh)projectileFactory.Invoke(null,new object[]{kind})));
                }
                foreach(var item in CosmeticLoadout.Catalog)
                {
                    // A character skin is a Meshy model, not a style on the procedural art this atlas draws.
                    if(item.slot==CosmeticLoadout.CharacterSlot) continue;
                    bool architecture=item.targetId=="*"; string target=architecture?"keep":item.targetId;
                    var faction=FactionForUnit(target,item.realmId);
                    Export(new Entry { key="cosmetic-"+item.id,id=item.id,name=item.displayName,category="cosmetics",realm=item.realmId,canonical=true,
                        description=item.description+(architecture?" Ejemplo del estilo aplicado a una fortaleza; también puede vestir otros edificios elegibles.":" Apariencia del ejército; no cambia reglas ni estadísticas." ) },
                        ()=>architecture?AlphaWorldArt.Building(target,faction.Kind,stage.transform,1,4,4):AlphaWorldArt.Unit(target,faction.Kind,stage.transform,1),CosmeticLoadout.Style(item.styleId));
                }
                Export(new Entry{key="state-gate-open",id="gate-open",name="Puerta abierta",category="states",canonical=true,description="El rastrillo cambia de altura y deja pasar a los ejércitos."},()=>{
                    var gate=AlphaWorldArt.Building("gate",FactionKind.AvenCompact,stage.transform,1,2,1); var leaf=gate.Find("Portcullis"); leaf.localPosition=Vector3.up*2.45f; leaf.localScale=new Vector3(1,.075f,1); return gate;
                });
                Export(new Entry{key="state-wall-garrison",id="wall-garrison",name="Infantería sobre muralla",category="states",canonical=true,description="Posición visual de guarnición a 3 m. La mecánica usa plazas fijas en el adarve."},()=>{
                    var root=new GameObject("Wall garrison pose").transform; root.SetParent(stage.transform,false); AlphaWorldArt.Building("wall",FactionKind.SkeldClans,root,1,3,1); var unit=AlphaWorldArt.Unit("reedguard",FactionKind.SkeldClans,root,1); unit.localPosition=Vector3.up*3; return root;
                });
                Export(new Entry{key="state-construction",id="construction",name="Edificio en construcción",category="states",canonical=true,description="El modelo emerge durante la construcción. Ejemplo de la escala visual intermedia."},()=>{
                    var model=AlphaWorldArt.Building("hearth",FactionKind.AvenCompact,stage.transform,1,4,4); model.localScale=new Vector3(4,.5f,4); return model;
                });
                Export(new Entry{key="state-team-colors",id="team-colors",name="Colores de los equipos",category="states",canonical=true,description="Mismo lancero: turquesa propio y rojo rival. Las skins conservan estas marcas."},()=>{
                    var root=new GameObject("Ownership comparison").transform; root.SetParent(stage.transform,false); var own=AlphaWorldArt.Unit("reedguard",FactionKind.AvenCompact,root,1); own.localPosition=Vector3.left*.8f; var rival=AlphaWorldArt.Unit("reedguard",FactionKind.AvenCompact,root,2); rival.localPosition=Vector3.right*.8f; return root;
                });
                MakeSheet("units","Personajes, criaturas y asedio",Select(e=>e.category=="units" && e.canonical),4);
                MakeSheet("buildings","Edificios y fortificaciones",Select(e=>e.category=="buildings" && e.canonical),4);
                MakeSheet("resources","Recursos de los tres biomas",Select(e=>e.category=="resources"),4);
                MakeSheet("scenery","Objetos del escenario",Select(e=>e.category=="scenery"),4);
                MakeSheet("cosmetics","Las 12 apariencias cosméticas",Select(e=>e.category=="cosmetics"),4);
                MakeSheet("factions","Las ocho unidades exclusivas",Select(e=>e.category=="units" && IsSignature(e.id)),4);
                MakeSheet("states","Objetivos, proyectiles y estados",Select(e=>e.category=="objectives" || e.category=="effects" || e.category=="states"),4);
                File.WriteAllText(Path.Combine(output,"manifest.json"),JsonUtility.ToJson(manifest,true));
                Object.DestroyImmediate(stage); Object.DestroyImmediate(camera.gameObject);
                foreach(var texture in retained) Object.DestroyImmediate(texture); retained.Clear();
                VisualMapAtlas.Export(output);
                Debug.Log("EMBERFIELD_VISUAL_ATLAS_OK models="+manifest.items.Count+" sheets="+manifest.sheets.Count+" folder="+output);
            }
            catch(Exception error) { QualitySettings.antiAliasing=originalAA; Debug.LogException(error); EditorApplication.Exit(1); }
            finally { QualitySettings.antiAliasing=originalAA; }
        }

        private static void Export(Entry entry,Func<Transform> factory,CosmeticVisualStyle style=default)
        {
            var model=factory(); if(model==null) throw new InvalidOperationException("Missing actual mesh: "+entry.key);
            try
            {
                AlphaWorldArt.ApplyCosmetic(model,style);
                foreach(var lod in model.GetComponentsInChildren<LODGroup>()) { lod.ForceLOD(0); lod.enabled=false; }
                foreach(var child in model.GetComponentsInChildren<Transform>(true)) { child.gameObject.layer=28; if(child.name=="LOD1") child.gameObject.SetActive(false); }
                var block=new MaterialPropertyBlock();
                foreach(var renderer in model.GetComponentsInChildren<MeshRenderer>()) { block.Clear(); renderer.GetPropertyBlock(block); block.SetFloat("_PreviewLighting",1); renderer.SetPropertyBlock(block); renderer.shadowCastingMode=ShadowCastingMode.Off; }
                var bounds=BoundsFor(model); Vector3 center=bounds.center;
                // Characters and doors face +Z; the scenery's one-sided foliage faces -Z.
                float viewZ=entry.category=="scenery"?-9:9;
                camera.transform.position=center+new Vector3(7,5,viewZ).normalized*35; camera.transform.LookAt(center); camera.aspect=1;
                float minX=float.PositiveInfinity,maxX=float.NegativeInfinity,minY=float.PositiveInfinity,maxY=float.NegativeInfinity;
                foreach(var filter in model.GetComponentsInChildren<MeshFilter>())
                {
                    var local=filter.sharedMesh.bounds; var matrix=camera.worldToCameraMatrix*filter.transform.localToWorldMatrix;
                    for(int i=0;i<8;i++) { var p=matrix.MultiplyPoint3x4(local.center+Vector3.Scale(local.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))); minX=Mathf.Min(minX,p.x);maxX=Mathf.Max(maxX,p.x);minY=Mathf.Min(minY,p.y);maxY=Mathf.Max(maxY,p.y); }
                }
                camera.orthographicSize=Mathf.Max(.16f,Mathf.Max(maxX-minX,maxY-minY)*.57f);
                camera.transform.position+=camera.transform.right*((minX+maxX)*.5f)+camera.transform.up*((minY+maxY)*.5f);
                entry.image="images/"+entry.key+".png";
                Save(camera,512,512,Path.Combine(output,entry.image)); manifest.items.Add(entry);
            }
            finally { Object.DestroyImmediate(model.gameObject); }
        }
        private static Bounds BoundsFor(Transform model)
        { var all=model.GetComponentsInChildren<MeshRenderer>(); if(all.Length==0) throw new InvalidOperationException("No mesh renderer in "+model.name);var result=all[0].bounds;foreach(var r in all)result.Encapsulate(r.bounds);return result; }
        private static Transform MeshObject(Mesh mesh)
        { var root=new GameObject("Native projectile",typeof(MeshFilter),typeof(MeshRenderer)).transform;root.SetParent(stage.transform,false);root.GetComponent<MeshFilter>().sharedMesh=mesh;root.GetComponent<MeshRenderer>().sharedMaterial=ArtKit.Catalog.Material;return root; }
        private static void Save(Camera target,int width,int height,string file)
        {
            var rt=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32){antiAliasing=4};rt.Create();
            var pixels=new Texture2D(width,height,TextureFormat.RGB24,false);var previous=RenderTexture.active;var old=target.targetTexture;
            try
            {
                target.targetTexture=rt;
                Canvas.ForceUpdateCanvases();
                var request=new RenderPipeline.StandardRequest{destination=rt};
                if(!RenderPipeline.SupportsRenderRequest(target,request)) target.Render();
                if(!RenderPipeline.SupportsRenderRequest(target,request)) throw new InvalidOperationException("URP render requests unavailable for native atlas.");
                RenderPipeline.SubmitRenderRequest(target,request); RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,width,height),0,0);pixels.Apply();
                int changed=0; var data=pixels.GetPixels32(); var first=data[0];
                for(int i=0;i<data.Length;i+=7) if(Math.Abs(data[i].r-first.r)+Math.Abs(data[i].g-first.g)+Math.Abs(data[i].b-first.b)>12) changed++;
                if(changed<80) throw new InvalidOperationException("Empty or uniform atlas render: "+file);
                File.WriteAllBytes(file,pixels.EncodeToPNG());
            }
            finally{target.targetTexture=old;RenderTexture.active=previous;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(pixels);}
        }
        private static void MakeSheet(string id,string title,List<Entry> entries,int columns)
        {
            int rows=(entries.Count+columns-1)/columns,width=1600,height=108+rows*360;
            var holder=new GameObject("Native atlas sheet UI",typeof(RectTransform),typeof(Canvas)); var canvas=holder.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
            camera.cullingMask=1<<27; camera.aspect=width/(float)height;
            var rect=(RectTransform)holder.transform;rect.sizeDelta=new Vector2(width,height);
            try
            {
                TextLabel(holder.transform,title,32,new Rect(26,16,width-52,42),true);
                TextLabel(holder.transform,"EMBERFIELD · MODELOS REALES DE UNITY · ESCALA AJUSTADA POR TARJETA",16,new Rect(28,61,width-56,28),false);
                for(int i=0;i<entries.Count;i++)
                {
                    var item=entries[i];float x=(i%columns)*400,y=108+(i/columns)*360;
                    var texture=new Texture2D(2,2); texture.LoadImage(File.ReadAllBytes(Path.Combine(output,item.image)));retained.Add(texture);
                    var picture=new GameObject(item.key,typeof(RectTransform),typeof(RawImage));picture.transform.SetParent(holder.transform,false);picture.GetComponent<RawImage>().texture=texture;
                    Place((RectTransform)picture.transform,new Rect(x+57,y,286,286));
                    TextLabel(holder.transform,item.name,22,new Rect(x+12,y+291,376,31),true);
                    TextLabel(holder.transform,SheetCaption(item),16,new Rect(x+12,y+324,376,26),false);
                }
                foreach(var child in holder.GetComponentsInChildren<Transform>(true)) child.gameObject.layer=27;
                Canvas.ForceUpdateCanvases();
                // The screenshot is an actual Unity Canvas of the native rendered models.
                string image="sheets-"+id+".png";Save(camera,width,height,Path.Combine(output,image));
                manifest.sheets.Add(new Sheet{id=id,name=title,image=image});
            }
            finally{Object.DestroyImmediate(holder);camera.cullingMask=1<<28;}
        }
        private static void TextLabel(Transform parent,string text,int size,Rect rect,bool strong)
        {
            var label=new GameObject(text,typeof(RectTransform),typeof(Text)).GetComponent<Text>();label.transform.SetParent(parent,false);label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.text=text;label.fontSize=size;label.fontStyle=strong?FontStyle.Bold:FontStyle.Normal;label.color=strong?new Color(.93f,.85f,.63f):new Color(.64f,.77f,.80f);label.alignment=TextAnchor.MiddleCenter;label.resizeTextForBestFit=true;label.resizeTextMinSize=12;label.resizeTextMaxSize=size;Place(label.rectTransform,rect);
        }
        private static void Place(RectTransform rect,Rect box){rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(box.x,-box.y);rect.sizeDelta=box.size;}
        private static string SheetCaption(Entry item)
        {
            if(!string.IsNullOrEmpty(item.factionName)) return item.factionName;
            switch(item.biome){case "forest":return "Bosque";case "caribbean":return "Costa caribeña";case "desert":return "Desierto";}
            switch(item.category){case "resources":return "Recurso compartido";case "scenery":return "Decoración";case "cosmetics":return "Apariencia cosmética";case "objectives":return "Objetivo del mapa";case "effects":return "Proyectil";case "states":return "Estado visual";default:return "";}
        }
        private static List<Entry> Select(Predicate<Entry> predicate)=>manifest.items.FindAll(predicate);
        private static bool Eligible(string required,string faction)=>string.IsNullOrEmpty(required)||required==faction;
        private static bool IsSignature(string id){foreach(var f in rules.Factions)if(f.UniqueUnitId==id)return true;return false;}
        private static FactionDefinition FactionForUnit(string id,string realm)
        {foreach(var u in rules.Units)if(u.Id==id&&!string.IsNullOrEmpty(u.RequiredFactionId))foreach(var f in rules.Factions)if(f.Id==u.RequiredFactionId)return f;return rules.Factions[realm=="fantasy"?7:0];}
        private static string Cost(ResourceAmount value)=>value.Food+" comida · "+value.Wood+" madera · "+value.Metal+" metal · "+value.Stone+" piedra";
        private static string UnitDescription(UnitDefinition u)=>u.Id=="supply_cart"?"Forma plegada del puesto Serevin; no se entrena como una unidad independiente.":u.SiegeEquipment!=SiegeEquipmentKind.None?"Equipo de asedio entrenable. Las facciones comparten este modelo.":(u.Tags&CombatTags.Creature)!=0?"Criatura de ejército entrenable; tiene costes y contramedidas.":u.IsWorker?"Recolecta recursos y construye.":"Unidad del ejército. Aspecto nativo de esta facción.";
        private static string UnitName(string id)
        {switch(id){case "tender":return "Recolector";case "reedguard":return "Lancero";case "stringwarden":return "Arquero";case "strider":return "Jinete";case "threadkeeper":return "Tejedor logístico";case "ashrunner":return "Jinete Ashrunner";case "supply_cart":return "Puesto plegado";case "sun_lion":return "León solar";case "grove_guardian":return "Guardián del bosque";case "war_troll":return "Trol de guerra";case "ember_drake":return "Dragón de brasas";case "dune_elephant":return "Elefante de guerra";case "frostguard":return "Guardia de escarcha";case "siege_ram":return "Ariete";case "siege_ladder":return "Escalera de asedio";case "siege_tower":return "Torre de asedio";default:return id;}}
        private static string BuildingName(string id)
        {switch(id){case "hearth":return "Centro urbano";case "shelter":return "Vivienda";case "muster_hall":return "Cuartel";case "storeyard":return "Almacén";case "archive":return "Archivo";case "supply_outpost":return "Puesto móvil";case "wall":return "Muralla";case "gate":return "Puerta";case "watchtower":return "Torre vigía";case "keep":return "Fortaleza";case "beast_lodge":return "Guarida de criaturas";case "siege_workshop":return "Taller de asedio";default:return id;}}
        private static string ResourceName(ResourceKind kind,string biome)=>kind==ResourceKind.Metal?"Yacimiento de metal":kind==ResourceKind.Stone?"Cantera":kind==ResourceKind.Wood?(biome=="forest"?"Árbol recolectable":biome=="caribbean"?"Palmera recolectable":"Palmera datilera recolectable"):(biome=="forest"?"Cultivo de trigo":biome=="caribbean"?"Frutos tropicales":"Frutos del oasis");
        private static string PropBiome(string id)=>id=="palm"||id=="ship"||id=="harbor"||id=="ruins"||id=="coastal_rock"?"caribbean":id=="date_palm"||id=="pyramid"||id=="caravan"||id=="dune"||id=="dry_grass"||id=="desert_rock"?"desert":id=="grass"?"shared":"forest";
        private static string ReadRevision(){try{var head=File.ReadAllText(".git/HEAD").Trim();return head.StartsWith("ref: ")?File.ReadAllText(Path.Combine(".git",head.Substring(5))).Trim():head;}catch{return "working-tree";}}
    }
}
