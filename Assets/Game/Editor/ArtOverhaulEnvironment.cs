using System;
using System.Collections.Generic;
using Emberfield.Presentation;
using UnityEngine;
using UnityEngine.Rendering;
using MeshBuilder = Emberfield.Editor.ArtOverhaulEnvironmentMeshes;
using Paint = Emberfield.Editor.ArtOverhaulEnvironmentMeshes.Paint;

namespace Emberfield.Editor
{
    /// <summary>Original heighted 3D environment recipes for four independent visual studies.</summary>
    public static class ArtOverhaulEnvironment
    {
        public sealed class Layout
        {
            public Vector3 UnitFocus, RTFocus;
            public Vector3[] UnitPositions;
            public Quaternion[] UnitRotations;
        }
        private static readonly Dictionary<string, Mesh> Shared = new Dictionary<string, Mesh>();
        private sealed class Route { internal Vector2[] Points; internal float Width; }
        private static readonly List<Route> Roads = new List<Route>();
        private static readonly List<Rect> Clearings = new List<Rect>();
        private static void Reserve(float x,float z,float width,float depth) => Clearings.Add(new Rect(x-width*.5f,z-depth*.5f,width,depth));
        private static bool Reserved(float x,float z,float margin=0)
        {
            foreach(var r in Clearings)if(x>=r.xMin-margin&&x<=r.xMax+margin&&z>=r.yMin-margin&&z<=r.yMax+margin)return true;
            return false;
        }
        private static readonly Paint Timber = P(.29f,.145f,.060f,2), TimberLight = P(.46f,.27f,.11f,2);
        private static readonly Paint Stone = P(.66f,.61f,.48f,4), StoneLight = P(.83f,.78f,.63f,4);
        private static readonly Paint Plaster = P(.86f,.80f,.64f,13), Slate = P(.16f,.29f,.36f,9);
        private static readonly Paint Dark = P(.065f,.073f,.064f,2), Gold = P(.83f,.55f,.19f,3,.65f,.57f);
        private static readonly Paint Cloth = P(.18f,.38f,.63f,0), Metal = P(.35f,.40f,.42f,3,.65f,.48f);
        private static Paint P(float r,float g,float b,int tile,float metallic=0,float smooth=.24f) => new Paint(new Color(r,g,b),tile,metallic,smooth);

        public static Layout Build(ArtBiome biome, Transform parent, Material surfaceMaterial, Material waterMaterial, Material foliageMaterial, Material crystalMaterial)
        {
            if (parent == null || surfaceMaterial == null || waterMaterial == null || foliageMaterial == null || crystalMaterial == null)
                throw new ArgumentException("Environment requires a parent and four shared materials.");
            var root = new GameObject(biome + " • inhabited frontier").transform; root.SetParent(parent,false);
            Roads.Clear();Clearings.Clear();Reserve(2,-7.5f,20,11);
            switch (biome)
            {
                case ArtBiome.Kingdom: Kingdom(root,surfaceMaterial,waterMaterial,foliageMaterial); break;
                case ArtBiome.Caribbean: Caribbean(root,surfaceMaterial,waterMaterial,foliageMaterial); break;
                case ArtBiome.Desert: Desert(root,surfaceMaterial,waterMaterial,foliageMaterial); break;
                case ArtBiome.Fantasy: Fantasy(root,surfaceMaterial,waterMaterial,foliageMaterial,crystalMaterial); break;
                default: throw new ArgumentOutOfRangeException(nameof(biome));
            }
            // Routes are integrated into the terrain, so junctions share a single visible surface.
            Terrain(biome,root,surfaceMaterial);Roads.Clear();
            var positions = new[] { new Vector2(-4.6f,-6.5f),new Vector2(-1.2f,-5.5f),new Vector2(2.8f,-6.0f),new Vector2(6.7f,-4.9f),new Vector2(10.8f,-2.6f) };
            var result = new Layout { UnitFocus = At(biome,2.4f,-5.4f)+Vector3.up*.9f, RTFocus = new Vector3(0,1.5f,5), UnitPositions=new Vector3[positions.Length],UnitRotations=new Quaternion[positions.Length] };
            for (int i=0;i<positions.Length;i++) { result.UnitPositions[i]=At(biome,positions[i].x,positions[i].y);result.UnitRotations[i]=Quaternion.Euler(0,165+i%2*12,0); }
            return result;
        }
        private static Vector3 At(ArtBiome biome,float x,float z) => new Vector3(x,Height(biome,x,z),z);
        private static float Smooth(float a,float b,float x) => Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,x));
        private static float RiverX(float z) => -13.5f+Mathf.Sin(z*.12f)*2.0f;
        private static float RiverY(float z) => Smooth(19,21,z)*4.2f+.03f;
        private static float Height(ArtBiome biome,float x,float z)
        {
            float noise=Mathf.PerlinNoise(x*.047f+17,z*.047f+9);
            float h=.25f+noise*1.15f+Mathf.Sin(x*.11f+z*.15f)*.24f;
            float flat=1-Smooth(8,17,Vector2.Distance(new Vector2(x,z),new Vector2(2,-5)));
            h=Mathf.Lerp(h,.22f,flat);
            if (biome==ArtBiome.Kingdom)
            {
                h+=Smooth(16,23,z)*(3.4f+noise*2.7f);
                h=Mathf.Lerp(h,RiverY(z)-.72f,1-Smooth(1.9f,3.6f,Mathf.Abs(x-RiverX(z))));
            }
            else if (biome==ArtBiome.Caribbean)
            {
                float shore=-12+Mathf.Sin(z*.13f)*3;
                h=Mathf.Lerp(-1.4f,h,Smooth(shore-2,shore+7,x));
                h+=Smooth(13,29,z)*noise*5;
            }
            else if (biome==ArtBiome.Desert)
            {
                h+=(1-flat)*(Mathf.Sin(x*.15f+z*.035f)*Mathf.Sin(x*.15f+z*.035f)*1.8f+noise*1.4f);
                float pond=Vector2.Distance(new Vector2(x,z),new Vector2(-12,2));
                h=Mathf.Lerp(h,-.45f,1-Smooth(4.3f,6.4f,pond));
                h+=Smooth(20,38,z)*3;
            }
            else
            {
                h+=Smooth(15,29,z)*noise*5;
                float river=Mathf.Abs(x-(-12+Mathf.Sin(z*.095f)*3));
                h=Mathf.Lerp(h,-.5f,1-Smooth(2.0f,3.7f,river));
            }
            return h;
        }
        private static void Terrain(ArtBiome biome,Transform parent,Material material)
        {
            var b=new MeshBuilder(); const int cells=160; const float step=.9f, half=cells*step*.5f;
            for(int z=0;z<=cells;z++)for(int x=0;x<=cells;x++)
            {
                float px=x*step-half,pz=z*step-half,h=Height(biome,px,pz),noise=Mathf.PerlinNoise(px*.10f+5,pz*.10f+12);
                Color color=biome==ArtBiome.Desert?Color.Lerp(new Color(.73f,.51f,.25f),new Color(.94f,.77f,.43f),noise)
                    : biome==ArtBiome.Caribbean?Color.Lerp(new Color(.22f,.34f,.22f),new Color(.39f,.49f,.29f),noise)
                    : biome==ArtBiome.Fantasy?Color.Lerp(new Color(.13f,.25f,.24f),new Color(.27f,.41f,.32f),noise)
                    : Color.Lerp(new Color(.22f,.32f,.19f),new Color(.39f,.47f,.28f),noise);
                if(biome==ArtBiome.Caribbean)color=Color.Lerp(new Color(.92f,.82f,.54f),color,Smooth(-7,2,px));
                int tile=biome==ArtBiome.Desert||biome==ArtBiome.Caribbean&&px<-6?7:6;
                float margin=RouteMargin(new Vector2(px,pz))+(Mathf.PerlinNoise(px*.8f,pz*.8f)-.5f)*.24f;
                float road=1-Smooth(-.15f,.52f,margin);
                var routeColor=biome==ArtBiome.Desert?new Color(.68f,.52f,.32f):biome==ArtBiome.Fantasy?new Color(.43f,.51f,.43f):biome==ArtBiome.Caribbean?new Color(.69f,.60f,.42f):new Color(.55f,.52f,.43f);
                color=Color.Lerp(color,routeColor,road);
                if(road>.54f)tile=biome==ArtBiome.Kingdom||biome==ArtBiome.Fantasy?5:7;
                float dx=Height(biome,px+.1f,pz)-Height(biome,px-.1f,pz),dz=Height(biome,px,pz+.1f)-Height(biome,px,pz-.1f);
                b.Vertex(new Vector3(px,h,pz),new Vector3(-dx,.2f,-dz).normalized,new Vector2(px*.70f,pz*.70f),new Paint(color,tile));
            }
            for(int z=0;z<cells;z++)for(int x=0;x<cells;x++){int i=z*(cells+1)+x;b.Face(i,i+cells+1,i+1);b.Face(i+1,i+cells+1,i+cells+2);}
            var terrain=Emit(parent,"Continuous sculpted terrain • 144m",b.Finish(biome+" terrain"),material);
            terrain.gameObject.AddComponent<MeshCollider>().sharedMesh=terrain.GetComponent<MeshFilter>().sharedMesh;
        }
        private static Transform Emit(Transform parent,string name,Mesh mesh,Material material,Vector3? position=null,Vector3? scale=null,float yaw=0)
        {
            var t=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer)).transform;t.SetParent(parent,false);
            t.localPosition=position??Vector3.zero;t.localRotation=Quaternion.Euler(0,yaw,0);t.localScale=scale??Vector3.one;
            t.GetComponent<MeshFilter>().sharedMesh=mesh;var r=t.GetComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.On;r.receiveShadows=true;return t;
        }
        private static Mesh Cache(string key,Func<Mesh> make){if(!Shared.TryGetValue(key,out var mesh)||mesh==null){mesh=make();Shared[key]=mesh;}return mesh;}
        private static void Lod(Transform parent,string name,Vector3 position,Vector3 scale,float yaw,Material material,Func<int,Mesh> mesh)
        {
            var root=new GameObject(name).transform;root.SetParent(parent,false);root.localPosition=position;root.localScale=scale;root.localRotation=Quaternion.Euler(0,yaw,0);
            var near=Emit(root,"LOD0",mesh(0),material);var far=Emit(root,"LOD1",mesh(1),material);
            var group=root.gameObject.AddComponent<LODGroup>();group.SetLODs(new[]{new LOD(.065f,new[]{near.GetComponent<Renderer>()}),new LOD(.007f,new[]{far.GetComponent<Renderer>()})});group.fadeMode=LODFadeMode.None;group.RecalculateBounds();
        }
        private static void Path(ArtBiome biome,Transform root,Material material,Vector2[] points,float width,bool stone=false)
        {
            Roads.Add(new Route { Points=points,Width=width });
        }
        private static float RouteMargin(Vector2 p)
        {
            float margin=1000;
            foreach(var route in Roads)for(int section=0;section<route.Points.Length-1;section++)
            {
                var a=route.Points[section];var d=route.Points[section+1]-a;float t=Mathf.Clamp01(Vector2.Dot(p-a,d)/d.sqrMagnitude);
                margin=Mathf.Min(margin,Vector2.Distance(p,a+d*t)-route.Width*.5f);
            }
            return margin;
        }
        private static void River(ArtBiome biome,Transform root,Material water)
        {
            var b=new MeshBuilder();var paint=P(.18f,.55f,.62f,0,0,.85f);
            for(float z=-72;z<72;z+=.5f)
            {
                float x=biome==ArtBiome.Kingdom?RiverX(z):-12+Mathf.Sin(z*.095f)*3;
                float nz=z+.5f,nx=biome==ArtBiome.Kingdom?RiverX(nz):-12+Mathf.Sin(nz*.095f)*3;
                float y=biome==ArtBiome.Kingdom?RiverY(z):.04f,ny=biome==ArtBiome.Kingdom?RiverY(nz):.04f;
                b.Quad(new Vector3(x-2.25f,y,z),new Vector3(nx-2.25f,ny,nz),new Vector3(nx+2.25f,ny,nz),new Vector3(x+2.25f,y,z),paint);
            }
            var river=Emit(root,"Flowing river and falls",b.Finish("Continuous river water"),water);river.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
        }

        private static void Kingdom(Transform root,Material surface,Material water,Material foliage)
        {
            const ArtBiome biome=ArtBiome.Kingdom;River(biome,root,water);
            Path(biome,root,surface,new[]{new Vector2(-4,-33),new Vector2(0,-13),new Vector2(3,-5),new Vector2(9,4),new Vector2(9,12),new Vector2(2,17),new Vector2(-4,19),new Vector2(-4,29)},3.8f,true);
            Path(biome,root,surface,new[]{new Vector2(-24,-1),new Vector2(-10,-1),new Vector2(2,-2),new Vector2(19,4),new Vector2(29,13)},2.7f);
            House(biome,root,surface,10,5,0,1.02f);House(biome,root,surface,18,10,1,1.08f);House(biome,root,surface,21,2,0,.88f);House(biome,root,surface,13,15,1,.84f);
            House(biome,root,surface,-5,15,2,.91f);
            Castle(root,surface,At(biome,-4,24));
            Windmill(root,surface,At(biome,-3,10.8f));
            Bridge(root,surface,new Vector3(RiverX(-1),0,-1),false);
            Farms(root,surface,foliage,biome,-2,3);
            var cliffs=new MeshBuilder();
            for(int i=0;i<9;i++)
            {
                float x=-24+i*2.1f,z=21+(i%3)*1.2f;
                if(Mathf.Abs(x-RiverX(z))<3.1f)continue;
                cliffs.Ellipsoid(new Vector3(x,2.5f,z),new Vector3(4.1f,6.2f,4.0f),P(.47f,.49f,.39f,14),14,10,.16f);
                cliffs.Ellipsoid(new Vector3(x,5.1f,z),new Vector3(3.7f,.55f,3.3f),P(.32f,.43f,.16f,6),12,6,.12f);
            }
            Emit(root,"Stratified waterfall escarpment",cliffs.Finish("Weathered cliff faces"),surface);
            var foam=new MeshBuilder();for(int i=0;i<9;i++)foam.Ellipsoid(new Vector3(RiverX(19)+Mathf.Sin(i*3.1f)*1.5f,.12f,18.5f+i%3*.35f),new Vector3(.8f,.07f,.5f),new Paint(new Color(.80f,.94f,.91f),0,0,.5f,1),10,4);
            for(int ribbon=0;ribbon<5;ribbon++)
            {
                float x=RiverX(20)+(ribbon-2)*.65f;var white=new Paint(new Color(.75f,.90f,.88f),0,0,.6f,.63f);
                for(int step=0;step<14;step++)
                {
                    float z=19+step/7f,nz=19+(step+1)/7f,y=RiverY(z)+.025f,ny=RiverY(nz)+.025f,w=.14f+Mathf.Sin(step*.8f+ribbon)*.035f;
                    foam.Quad(new Vector3(x-w,y,z),new Vector3(x-w,ny,nz),new Vector3(x+w,ny,nz),new Vector3(x+w,y,z),white);
                }
            }
            Emit(root,"Waterfall foaming plunge",foam.Finish("Waterfall foam"),water);
            Reserve(RiverX(20),20,9.5f,10);
            Forest(biome,root,foliage,surface,false);GroundCover(biome,root,foliage,surface);
            RiverBanks(biome,root,surface,foliage);
            VillageProps(biome,root,surface,9,2);VillageProps(biome,root,surface,17,7);
        }
        private static void House(ArtBiome biome,Transform root,Material material,float x,float z,int variant,float scale)
        {
            Reserve(x,z,(variant==2?7.3f:8.8f)*scale,7.9f*scale);
            string style=biome==ArtBiome.Caribbean?"port":biome==ArtBiome.Desert?"sand":biome==ArtBiome.Fantasy?"grove":"kingdom";
            Lod(root,style+" inhabited house",At(biome,x,z),Vector3.one*scale,variant==2?18:-12+variant*25,material,lod=>Cache("house-"+style+variant+"-"+lod,()=>HouseMesh(style,variant,lod)));
        }
        private static Mesh HouseMesh(string style,int variant,int lod)
        {
            if(style=="kingdom"&&variant==2)return BarnMesh(lod);
            var b=new MeshBuilder();float width=variant==2?5.3f:variant==0?3.8f:4.6f,depth=variant==1?4.7f:3.8f,height=variant==1?4.3f:2.85f;
            var walls=style=="sand"?P(.85f,.61f,.32f,4):style=="grove"?P(.73f,.83f,.69f,13):variant==1?P(.79f,.75f,.64f,13):P(.86f,.76f,.60f,13);
            var roof=style=="port"?P(.61f,.23f,.12f,8):style=="sand"?P(.33f,.63f,.62f,8):style=="grove"?P(.19f,.45f,.33f,10):variant==0?P(.50f,.24f,.16f,8):Slate;
            b.Box(new Vector3(0,.2f,0),new Vector3(width+.45f,.4f,depth+.45f),Stone,.1f);
            b.Box(new Vector3(0,height*.5f+.25f,0),new Vector3(width,height,depth),walls,.14f);
            for(int side=-1;side<=1;side+=2)
            {
                b.Beam(new Vector3(side*width*.48f,.4f,-depth*.51f),new Vector3(side*width*.48f,height+.25f,-depth*.51f),.17f,.16f,Timber);
                b.Beam(new Vector3(side*width*.48f,.4f,depth*.51f),new Vector3(side*width*.48f,height+.25f,depth*.51f),.17f,.16f,Timber);
                b.Beam(new Vector3(-width*.5f,1.6f,side*depth*.51f),new Vector3(width*.5f,1.6f,side*depth*.51f),.13f,.14f,Timber);
                b.Triangle(new Vector3(-width*.5f,height+.2f,side*depth*.5f),new Vector3(0,height+1.9f,side*depth*.5f),new Vector3(width*.5f,height+.2f,side*depth*.5f),walls);
                b.Triangle(new Vector3(width*.5f,height+.2f,side*depth*.5f),new Vector3(0,height+1.9f,side*depth*.5f),new Vector3(-width*.5f,height+.2f,side*depth*.5f),walls);
            }
            b.Roof(new Vector3(0,height+.16f,0),width+.85f,depth+.7f,1.85f,roof,lod==0?8:4);
            b.Box(new Vector3(0,1.05f,-depth*.5f-.045f),new Vector3(1.08f,1.75f,.08f),Dark,.02f);
            b.Arch(new Vector3(0,.25f,-depth*.5f-.14f),1.10f,1.86f,.23f,StoneLight,lod==0?12:7);
            for(int n=0;n<5;n++)b.Box(new Vector3(-.4f+n*.2f,.91f,-depth*.5f-.10f),new Vector3(.17f,1.31f,.055f),TimberLight,.012f);
            b.Box(new Vector3(0,.18f,-depth*.5f-.63f),new Vector3(1.8f,.22f,1.1f),StoneLight,.08f);
            for(int side=-1;side<=1;side+=2)
            {
                float x=side*width*.32f;
                b.Box(new Vector3(x,2.2f,-depth*.5f-.07f),new Vector3(.74f,.89f,.10f),Dark,.07f);
                b.Arch(new Vector3(x,1.72f,-depth*.5f-.15f),.66f,1.05f,.18f,TimberLight,lod==0?9:5);
                b.Beam(new Vector3(x,1.75f,-depth*.5f-.19f),new Vector3(x,2.63f,-depth*.5f-.19f),.06f,.045f,TimberLight);
                b.Beam(new Vector3(x-.3f,2.16f,-depth*.5f-.19f),new Vector3(x+.3f,2.16f,-depth*.5f-.19f),.07f,.045f,TimberLight);
                if(lod==0)
                {
                    b.Box(new Vector3(x,1.63f,-depth*.5f-.25f),new Vector3(.96f,.22f,.33f),Timber,.035f);
                    for(int n=0;n<4;n++)b.Ellipsoid(new Vector3(x-.3f+n*.2f,1.84f,-depth*.5f-.28f),new Vector3(.26f,.29f,.28f),P(.36f,.54f,.19f,10),8,5);
                    for(int row=0;row<5;row++)b.Box(new Vector3(side*width*.49f,.65f+row*.48f,-depth*.5f-.045f),new Vector3(.37f,.3f,.19f),StoneLight,.05f);
                }
            }
            b.Box(new Vector3(width*.26f,height+1.5f,depth*.21f),new Vector3(.61f,2.0f,.7f),Stone,.08f);
            b.Box(new Vector3(width*.26f,height+2.50f,depth*.21f),new Vector3(.83f,.17f,.89f),StoneLight,.05f);
            if(style=="kingdom")
            {
                // Framed upper storey and an attached lower wing break the repeated barn silhouette.
                for(int side=-1;side<=1;side+=2)
                {
                    b.Beam(new Vector3(-width*.5f,height+.17f,side*depth*.51f),new Vector3(width*.5f,height+.17f,side*depth*.51f),.17f,.17f,Timber);
                    for(int post=-1;post<=1;post++)b.Beam(new Vector3(post*width*.34f,1.68f,side*depth*.51f),new Vector3(post*width*.34f,height+.23f,side*depth*.51f),.13f,.14f,Timber);
                    for(int panel=-1;panel<=0;panel++)b.Beam(new Vector3(panel*width*.47f,height-.02f,side*depth*.515f),new Vector3((panel+1)*width*.47f,1.76f,side*depth*.515f),.11f,.12f,Timber);
                    b.Beam(new Vector3(side*width*.45f,height+.32f,-depth*.51f),new Vector3(0,height+1.7f,-depth*.51f),.14f,.14f,Timber);
                }
                float wingX=width*.5f+.68f,wingH=variant==1?2.8f:2.12f;
                b.Box(new Vector3(wingX,wingH*.5f,depth*.12f),new Vector3(1.65f,wingH,2.7f),walls,.12f);
                b.Box(new Vector3(wingX,.16f,depth*.12f),new Vector3(1.9f,.32f,2.95f),Stone,.07f);
                b.Roof(new Vector3(wingX,wingH,depth*.12f),2.1f,3.08f,1.02f,variant==1?roof:P(.56f,.28f,.17f,8),lod==0?5:3);
                b.Box(new Vector3(wingX,1.28f,-1.01f),new Vector3(.53f,.72f,.08f),Dark,.035f);
                b.Arch(new Vector3(wingX,.95f,-1.09f),.53f,.86f,.15f,TimberLight,8);
                if(variant==1)
                {
                    b.Box(new Vector3(-.86f,height+1.15f,-1.13f),new Vector3(1.22f,1.16f,1.45f),walls,.08f);
                    b.Roof(new Vector3(-.86f,height+1.70f,-1.13f),1.54f,1.79f,.77f,roof,lod==0?4:3);
                    b.Box(new Vector3(-.86f,height+1.22f,-1.88f),new Vector3(.56f,.59f,.06f),Dark,.04f);
                    b.Arch(new Vector3(-.86f,height+.92f,-1.93f),.53f,.65f,.12f,StoneLight,8);
                    for(int side=-1;side<=1;side+=2)b.Box(new Vector3(side*1.47f,3.40f,-depth*.5f-.06f),new Vector3(.66f,.85f,.09f),Dark,.045f);
                }
            }
            if(variant==1)
            {
                for(int side=-1;side<=1;side+=2)b.Frustum(new Vector3(side*1.6f,0,-depth*.5f-1.55f),new Vector3(side*1.6f,2.1f,-depth*.5f-1.55f),.075f,.065f,Timber,8);
                b.Quad(new Vector3(-1.85f,2.65f,-depth*.5f),new Vector3(1.85f,2.65f,-depth*.5f),new Vector3(1.85f,2.1f,-depth*.5f-1.9f),new Vector3(-1.85f,2.1f,-depth*.5f-1.9f),Cloth,2);
            }
            return b.Finish(style+" layered timber plaster residence LOD"+lod);
        }
        private static Mesh BarnMesh(int lod)
        {
            var b=new MeshBuilder();var straw=P(.62f,.49f,.27f,12);b.Box(new Vector3(0,.18f,0),new Vector3(5.5f,.36f,4.75f),Stone,.08f);
            b.Box(new Vector3(0,1.56f,0),new Vector3(5.15f,2.95f,4.4f),P(.68f,.57f,.39f,13),.13f);
            b.Roof(new Vector3(0,2.97f,0),5.8f,5.0f,1.8f,straw,lod==0?10:5);
            for(int side=-1;side<=1;side+=2)
            {
                b.Triangle(new Vector3(-2.575f,2.97f,side*2.2f),new Vector3(0,4.6f,side*2.2f),new Vector3(2.575f,2.97f,side*2.2f),P(.49f,.34f,.18f,2));
                b.Triangle(new Vector3(2.575f,2.97f,side*2.2f),new Vector3(0,4.6f,side*2.2f),new Vector3(-2.575f,2.97f,side*2.2f),P(.49f,.34f,.18f,2));
                for(int post=-2;post<=2;post++)b.Beam(new Vector3(post*1.22f,.2f,side*2.23f),new Vector3(post*1.22f,3.01f,side*2.23f),.16f,.16f,Timber);
                b.Beam(new Vector3(-2.62f,2.95f,side*2.25f),new Vector3(2.62f,2.95f,side*2.25f),.16f,.16f,Timber);
            }
            b.Box(new Vector3(0,1.38f,-2.265f),new Vector3(2.40f,2.35f,.1f),TimberLight,.045f);
            for(int side=-1;side<=1;side+=2)
            {
                b.Beam(new Vector3(side*.08f,.30f,-2.36f),new Vector3(side*1.1f,2.44f,-2.36f),.13f,.08f,Timber);
                b.Beam(new Vector3(side*1.1f,.30f,-2.36f),new Vector3(side*.08f,2.44f,-2.36f),.13f,.08f,Timber);
            }
            b.Ellipsoid(new Vector3(3.15f,.69f,.8f),new Vector3(1.32f,1.42f,1.7f),straw,14,8,.16f);
            b.Ellipsoid(new Vector3(3.10f,.45f,-.65f),new Vector3(1.25f,.91f,1.25f),straw,14,8,.14f);
            return b.Finish("Thatched agricultural barn with braced double doors LOD"+lod);
        }
        private static void Castle(Transform root,Material material,Vector3 position)
        {
            Reserve(position.x,position.z,20,15);
            Lod(root,"Stone hill castle • towers and gatehouse",position,Vector3.one,0,material,lod=>Cache("kingdom-castle-"+lod,()=>{
                var b=new MeshBuilder();
                b.Box(new Vector3(0,-1.25f,0),new Vector3(16,2.7f,11.5f),Stone,.13f);
                b.Box(new Vector3(0,.25f,0),new Vector3(16,.5f,11.5f),Stone,.2f);
                for(int s=-1;s<=1;s+=2)
                {
                    b.Box(new Vector3(s*6,2.35f,0),new Vector3(1.0f,4.7f,9.0f),Stone,.12f);
                    b.Box(new Vector3(0,2.2f,s*4.5f),new Vector3(12.4f,4.4f,.9f),Stone,.13f);
                    for(int n=0;n<12;n++)b.Box(new Vector3(-5.7f+n*1.04f,4.85f,s*4.5f),new Vector3(.48f,.8f,1.12f),StoneLight,.06f);
                    for(int n=0;n<8;n++)b.Box(new Vector3(s*6,5.05f,-3.7f+n*1.04f),new Vector3(1.15f,.78f,.47f),StoneLight,.06f);
                    for(int t=-1;t<=1;t+=2)Tower(b,new Vector3(s*6,0,t*4.5f),1.85f,6.6f,lod,false);
                }
                b.Box(new Vector3(0,4.4f,1),new Vector3(6.0f,8.8f,5.2f),Plaster,.15f);
                b.Roof(new Vector3(0,8.75f,1),6.8f,6.0f,3.0f,Slate,lod==0?10:5);
                Tower(b,new Vector3(2.9f,0,2.4f),1.28f,11.5f,lod,true);
                b.Box(new Vector3(0,1.95f,-5.02f),new Vector3(2.15f,3.7f,.07f),Dark,.03f);
                b.Arch(new Vector3(0,.15f,-5.14f),2.2f,3.8f,.45f,StoneLight,lod==0?16:9);
                for(int n=0;n<9;n++)b.Beam(new Vector3(-.94f+n*.235f,.3f,-5.19f),new Vector3(-.94f+n*.235f,3.2f,-5.19f),.055f,.055f,Metal);
                for(int y=0;y<2;y++)for(int x=-1;x<=1;x++)
                {
                    b.Box(new Vector3(x*1.7f,4.0f+y*2.4f,-1.66f),new Vector3(.56f,1.3f,.12f),Dark,.12f);
                    b.Arch(new Vector3(x*1.7f,3.36f+y*2.4f,-1.74f),.6f,1.48f,.21f,StoneLight,9);
                }
                return b.Finish("Hand assembled stone castle LOD"+lod);
            }));
        }
        private static void Tower(MeshBuilder b,Vector3 p,float radius,float height,int lod,bool roof)
        {
            b.Frustum(p,p+Vector3.up*height,radius*1.08f,radius,Stone,lod==0?20:12);
            for(int level=0;level<3;level++)b.Frustum(p+Vector3.up*(height*.25f+level*height*.29f),p+Vector3.up*(height*.25f+level*height*.29f+.15f),radius*1.04f,radius*1.04f,StoneLight,lod==0?20:12);
            b.Frustum(p+Vector3.up*height,p+Vector3.up*(height+.2f),radius*1.18f,radius*1.18f,StoneLight,lod==0?20:12);
            if(roof)b.Frustum(p+Vector3.up*(height+.2f),p+Vector3.up*(height+3.1f),radius*1.3f,.05f,Slate,lod==0?20:12);
            else for(int k=0;k<10;k++){float a=k*Mathf.PI*.2f;b.Box(p+new Vector3(Mathf.Cos(a)*radius,height+.55f,Mathf.Sin(a)*radius),new Vector3(.55f,.75f,.52f),StoneLight,.06f,Quaternion.Euler(0,-a*Mathf.Rad2Deg,0));}
            for(int k=0;k<3;k++)b.Box(p+new Vector3((k-1)*radius*.6f,height*.64f,-radius*.91f),new Vector3(.20f,1.0f,.10f),Dark,.035f);
            b.Frustum(p+Vector3.up*(height+.3f),p+Vector3.up*(height+(roof?4.7f:2.3f)),.045f,.03f,Timber,8);
            float flag=height+(roof?4.25f:1.9f);
            b.Quad(p+new Vector3(0,flag+.4f,0),p+new Vector3(1.35f,flag+.2f,.1f),p+new Vector3(1.22f,flag-.4f,.1f),p+new Vector3(0,flag-.42f,0),Cloth);
            b.Quad(p+new Vector3(0,flag-.42f,0),p+new Vector3(1.22f,flag-.4f,.1f),p+new Vector3(1.35f,flag+.2f,.1f),p+new Vector3(0,flag+.4f,0),Cloth);
        }
        private static void Windmill(Transform root,Material material,Vector3 p)
        {
            Reserve(p.x,p.z,6.8f,6.0f);
            var b=new MeshBuilder();b.Frustum(Vector3.zero,Vector3.up*5.7f,1.45f,.96f,Plaster,20);
            b.Frustum(Vector3.up*5.6f,Vector3.up*7.1f,1.5f,.1f,Slate,20);
            b.Arch(new Vector3(0,0,-1.41f),.9f,1.8f,.15f,StoneLight);b.Box(new Vector3(0,.8f,-1.44f),new Vector3(.88f,1.5f,.10f),Timber,.04f);
            for(int arm=0;arm<4;arm++)
            {
                var q=Quaternion.Euler(0,0,45+arm*90);var centre=new Vector3(0,4.65f,-1.45f);
                b.Beam(centre+q*new Vector3(.2f,0,0),centre+q*new Vector3(3.9f,0,0),.14f,.19f,Timber);
                for(int n=0;n<7;n++)b.Box(centre+q*new Vector3(1.65f+n*.32f,.34f,0),new Vector3(.24f,.83f,.10f),P(.87f,.79f,.52f,12),.02f,q);
            }
            b.Ellipsoid(new Vector3(0,4.65f,-1.6f),new Vector3(.48f,.48f,.35f),TimberLight,14,8);
            Emit(root,"Miller's tower and four lattice sails",b.Finish("Original windmill"),material,p);
        }
        private static void Bridge(Transform root,Material material,Vector3 p,bool organic)
        {
            var b=new MeshBuilder();var paint=organic?Timber:Stone;
            for(int i=0;i<16;i++)
            {
                float x=-4+i*.5f,y=.5f+Mathf.Sin((i+.5f)/16*Mathf.PI)*.65f;
                b.Box(new Vector3(x+.25f,y,0),new Vector3(.49f,.22f,3.3f),organic?TimberLight:StoneLight,.055f);
                for(int side=-1;side<=1;side+=2)
                {
                    if(i%2==0)b.Box(new Vector3(x+.25f,y+.53f,side*1.68f),new Vector3(.22f,1.1f,.3f),paint,.07f);
                    if(i<15)b.Beam(new Vector3(x+.25f,y+.88f,side*1.68f),new Vector3(x+.75f,.5f+Mathf.Sin((i+1.5f)/16*Mathf.PI)*.65f+.88f,side*1.68f),.17f,.18f,paint);
                }
            }
            if(!organic)for(int side=-1;side<=1;side+=2)for(int i=-1;i<=1;i++)b.Arch(new Vector3(i*2.45f,-.6f,side*1.3f),2.1f,1.6f,.7f,Stone,10);
            else for(int side=-1;side<=1;side+=2)b.Frustum(new Vector3(-4,.4f,side*1.2f),new Vector3(4,.4f,side*1.2f),.31f,.25f,Timber,12);
            Emit(root,organic?"Grown timber bough bridge":"Three-arch stone village bridge",b.Finish("Hand built river bridge"),material,p);
        }
        private static void Farms(Transform root,Material surface,Material foliage,ArtBiome biome,float x,float z)
        {
            var soil=new MeshBuilder();var crops=new MeshBuilder();
            for(int field=0;field<2;field++)for(int row=0;row<6;row++)
            {
                float px=x-3+row*.65f,pz=z+field*3.9f;
                soil.Box(At(biome,px,pz)+Vector3.up*.015f,new Vector3(.44f,.07f,3.3f),P(.28f,.19f,.085f,7),.025f);
                for(int n=0;n<11;n++)
                {
                    var p=At(biome,px,pz-1.4f+n*.27f);float h=.62f+(n+row)%3*.09f;
                    crops.Frustum(p,p+Vector3.up*h,.021f,.01f,P(.71f,.57f,.17f,12),5);
                    crops.Ellipsoid(p+Vector3.up*h,new Vector3(.13f,.3f,.1f),P(.88f,.70f,.25f,12),6,5);
                    crops.Beam(p+Vector3.up*.22f,p+new Vector3(.18f,.44f,.03f),.025f,.08f,P(.39f,.51f,.16f,6));
                }
            }
            Emit(root,"Cultivated furrows",soil.Finish("Farm soil beds"),surface);Emit(root,"Golden wheat at harvest",crops.Finish("Modeled grain stalks"),foliage);
            var fence=new MeshBuilder();for(int n=0;n<10;n++){var p=At(biome,x-4+n*.75f,z-2.2f);fence.Box(p+Vector3.up*.45f,new Vector3(.1f,.9f,.13f),TimberLight,.02f);if(n<9)for(int level=0;level<2;level++)fence.Beam(p+Vector3.up*(.35f+level*.35f),At(biome,x-4+(n+1)*.75f,z-2.2f)+Vector3.up*(.35f+level*.35f),.065f,.07f,Timber);}
            Emit(root,"Low split rail field fence",fence.Finish("Farm fence"),surface);
        }
        private static void Forest(ArtBiome biome,Transform root,Material foliage,Material surface,bool palms)
        {
            var points=new List<Vector2>();
            for(int i=0;i<22;i++)points.Add(new Vector2(-27+(i%6)*10.2f,19+(i/6)*7.6f));
            points.AddRange(new[]{new Vector2(-25,-17),new Vector2(25,-15),new Vector2(23,-5),new Vector2(-24,2),new Vector2(26,6),new Vector2(-29,10)});
            for(int i=0;i<points.Count;i++)
            {
                var p=points[i];if(Reserved(p.x,p.y,1.4f))continue;
                if(Height(biome,p.x,p.y)<-.1f)continue;
                Tree(biome,root,foliage,p.x,p.y,((i%5)*.11f+.83f)*(palms?.85f:1),i*137.5f,palms);
            }
            for(int i=0;i<16;i++)
            {
                float x=-28+(i*11%57),z=-20+(i*17%56);if(Reserved(x,z,.9f))continue;
                Rock(biome,root,surface,x,z,.75f+(i%4)*.35f);
            }
        }
        private static void Tree(ArtBiome biome,Transform root,Material material,float x,float z,float scale,float yaw,bool palm=false)
        {
            if(Reserved(x,z,palm?1.9f:.9f))return;
            int variant=Mathf.FloorToInt(Mathf.Abs(x)*.19f+Mathf.Abs(z)*.11f)%3;
            string type=palm?"palm":(biome==ArtBiome.Fantasy?"enchanted":variant==2?"fir":variant==1?"silver birch":"oak")+"-"+variant;
            Lod(root,type+" branching tree",At(biome,x,z),Vector3.one*scale,yaw,material,lod=>Cache(type+"-tree-"+lod,()=>{
                var b=new MeshBuilder();var leaf=biome==ArtBiome.Fantasy?P(.18f,.37f,.31f,10):variant==2?P(.18f,.30f,.23f,10):variant==1?P(.32f,.42f,.27f,10):P(.23f,.35f,.21f,10);
                if(palm)
                {
                    for(int n=0;n<15;n++)b.Frustum(new Vector3(Mathf.Sin(n*.11f)*.45f,n*.40f,0),new Vector3(Mathf.Sin((n+1)*.11f)*.45f,(n+1)*.40f,0),.16f-n*.0048f,.153f-n*.0048f,n%2==0?Timber:TimberLight,lod==0?14:9);
                    var crown=new Vector3(.45f,5.95f,0);
                    for(int frond=0;frond<9;frond++)
                    {
                        float a=frond*Mathf.PI*2/9;var dir=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var side=Vector3.Cross(Vector3.up,dir);float length=2.65f+frond%3*.13f;
                        Vector3 Spine(float t)=>crown+dir*(t*length)+Vector3.up*(Mathf.Sin(t*Mathf.PI)*.55f-t*t*1.15f);
                        int pieces=lod==0?14:8;
                        for(int piece=0;piece<pieces;piece++)
                        {
                            float t=piece/(float)pieces,nt=(piece+1)/(float)pieces;var from=Spine(t);var to=Spine(nt);
                            b.Frustum(from,to,.019f*(1-t)+.004f,.019f*(1-nt)+.004f,leaf,5);
                            if(piece<1)continue;
                            float width=Mathf.Pow(Mathf.Sin((t*.86f+.06f)*Mathf.PI),.70f)*.55f*(1-t*.45f);
                            for(int leafSide=-1;leafSide<=1;leafSide+=2)
                            {
                                var tip=from+side*leafSide*width+dir*.29f-Vector3.up*(.12f+t*.11f);var middle=Vector3.Lerp(from,tip,.55f)+Vector3.up*.035f;var spread=dir*(lod==0?.065f:.10f);
                                b.Quad(from,middle-spread,tip,middle+spread,leaf);b.Quad(middle+spread,tip,middle-spread,from,leaf);
                            }
                        }
                    }
                    for(int nut=0;nut<4;nut++)b.Ellipsoid(crown+new Vector3(Mathf.Sin(nut*2.4f)*.17f,-.13f,Mathf.Cos(nut*2.4f)*.17f),new Vector3(.20f,.24f,.20f),Timber,10,6);
                }
                else
                {
                    var bark=variant==1?P(.67f,.66f,.53f,14):P(.28f,.24f,.17f,2);
                    Curve(b,new[]{Vector3.zero,new Vector3(.15f,2.4f,.05f),new Vector3(-.09f,4.4f,.14f),new Vector3(.12f,variant==2?7.1f:6.0f,.05f)},variant==1?.29f:.40f,bark,lod==0?16:10);
                    if(variant==2)
                    {
                        for(int level=0;level<8;level++)for(int arm=0;arm<5;arm++)
                        {
                            float a=arm*Mathf.PI*.4f+level*1.61f,h=1.65f+level*.65f,r=2.35f-level*.23f;var dir=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var tip=dir*r+Vector3.up*(h-.18f);
                            b.Frustum(Vector3.up*h,tip,.09f,.025f,bark,6);
                            b.Ellipsoid(dir*(r*.73f)+Vector3.up*h,new Vector3(r*1.1f,.68f,r*.52f),new Paint(leaf.Color*(.88f+level%3*.07f),10),lod==0?12:8,lod==0?7:5,.25f,Quaternion.Euler(0,-a*Mathf.Rad2Deg,13));
                        }
                        b.Ellipsoid(new Vector3(.10f,6.75f,0),new Vector3(.65f,1.20f,.61f),leaf,12,7,.24f);
                    }
                    else for(int arm=0;arm<7;arm++)
                    {
                        float a=arm*2.399963f,r=variant==1?1.35f:2.10f;var dir=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));float h=3.4f+arm%4*.63f;
                        var tip=dir*r+Vector3.up*h;Curve(b,new[]{Vector3.up*(2.3f+arm%3*.6f),dir*(r*.55f)+Vector3.up*(h-.55f),tip,tip+dir*.35f+Vector3.up*.7f},.17f,bark,lod==0?10:6);
                        int sprays=lod==0?6:3;
                        for(int tuft=0;tuft<sprays;tuft++)
                        {
                            float ta=tuft*2.39996f+a;var p=tip+new Vector3(Mathf.Sin(ta)*.71f,.33f+tuft%3*.37f,Mathf.Cos(ta)*.70f);
                            b.Ellipsoid(p,new Vector3(variant==1?1.20f:1.65f,.88f+tuft%2*.21f,1.20f),new Paint(leaf.Color*(.85f+tuft%4*.075f),10),lod==0?14:9,lod==0?8:5,.28f,Quaternion.Euler(tuft*11,ta*Mathf.Rad2Deg,tuft*7));
                        }
                    }
                    if(variant==1)for(int mark=0;mark<7;mark++)b.Box(new Vector3(.01f,.5f+mark*.58f,-.28f+mark*.015f),new Vector3(.24f,.075f,.035f),P(.26f,.27f,.24f,14),.01f);
                    for(int k=0;k<5;k++){float a=k*Mathf.PI*.4f;Curve(b,new[]{new Vector3(Mathf.Cos(a)*1.3f,-.035f,Mathf.Sin(a)*1.3f),new Vector3(Mathf.Cos(a)*.65f,.15f,Mathf.Sin(a)*.65f),new Vector3(0,.7f,0)},.15f,bark,8);}
                }
                return b.Finish(type+" multi-lobed foliage LOD"+lod);
            }));
        }
        private static void Rock(ArtBiome biome,Transform root,Material material,float x,float z,float scale)
        {
            var paint=biome==ArtBiome.Desert?P(.73f,.52f,.29f,14):P(.50f,.52f,.40f,14);
            Emit(root,"Weathered layered rock",Cache("rock-"+biome,()=>{var b=new MeshBuilder();b.Ellipsoid(new Vector3(0,.63f,0),new Vector3(2.2f,1.55f,1.8f),paint,16,10,.18f);b.Ellipsoid(new Vector3(.7f,.26f,-.3f),new Vector3(1.1f,.67f,.96f),paint,12,7,.17f);return b.Finish("Eroded stone cluster");}),material,At(biome,x,z),Vector3.one*scale,x*29+z*17);
        }
        private static void RiverBanks(ArtBiome biome,Transform root,Material surface,Material foliage)
        {
            var stones=new MeshBuilder();var plants=new MeshBuilder();var rock=P(.42f,.46f,.38f,14);var leaves=P(.24f,.36f,.24f,10);
            for(int i=0;i<34;i++)for(int side=-1;side<=1;side+=2)
            {
                float z=-26+i*1.85f;if(Mathf.Abs(z+1)<3.0f)continue;
                float centre=biome==ArtBiome.Kingdom?RiverX(z):-12+Mathf.Sin(z*.095f)*3;
                float x=centre+side*(3.6f+Mathf.Sin(i*2.4f)*.45f);var p=At(biome,x,z);
                float s=.55f+i%3*.20f;
                stones.Ellipsoid(p+Vector3.up*(s*.16f),new Vector3(s*1.45f,s*.63f,s*1.13f),rock,12,7,.25f,Quaternion.Euler(0,i*53,0));
                if(i%2==0)for(int spray=0;spray<3;spray++)
                {
                    var at=p+new Vector3(side*.65f+Mathf.Sin(spray*2.4f)*.30f,.19f,Mathf.Cos(spray*2.4f)*.37f);
                    plants.Ellipsoid(at,new Vector3(.67f,.43f,.52f),new Paint(leaves.Color*(.92f+spray*.07f),10),10,6,.24f);
                }
                if(i%3==0)for(int reed=0;reed<6;reed++)
                {
                    var start=p+new Vector3(-side*.3f,0,reed*.08f);var top=start+Vector3.up*(.45f+reed%3*.09f)+Vector3.right*.09f;
                    plants.Frustum(start,top,.013f,.007f,leaves,5);plants.Ellipsoid(top,new Vector3(.045f,.16f,.045f),P(.43f,.34f,.21f,12),5,4);
                }
            }
            Emit(root,"Irregular riverbank stones and eroded margins",stones.Finish("Natural riverbank stone batch"),surface);
            Emit(root,"Riverside shrubs and reed pockets",plants.Finish("Natural riverbank vegetation batch"),foliage);
        }
        private static void GroundCover(ArtBiome biome,Transform root,Material foliage,Material surface)
        {
            var grass=new MeshBuilder();var flowers=new MeshBuilder();var random=new System.Random(871+(int)biome*61);
            for(int i=0;i<760;i++)
            {
                float x=(float)random.NextDouble()*66-33,z=(float)random.NextDouble()*61-23;
                if(Height(biome,x,z)<.05f)continue;
                if(Vector2.Distance(new Vector2(x,z),new Vector2(2,-5))<4.0f)continue;
                if(RouteMargin(new Vector2(x,z))<.2f)continue;
                if(biome==ArtBiome.Desert&&Vector2.Distance(new Vector2(x,z),new Vector2(-12,2))>9&&i%7!=0)continue;
                var p=At(biome,x,z);float hue=(float)random.NextDouble();var color=biome==ArtBiome.Desert?P(.49f,.43f,.25f,6):P(.24f+hue*.09f,.35f+hue*.10f,.19f+hue*.06f,6);
                for(int blade=0;blade<7;blade++)
                {
                    float a=blade*2.39996f+i*.9f,h=.18f+(blade%3)*.12f;var dir=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var start=p+dir*.13f;var end=start+Vector3.up*h+dir*.19f;var side=Vector3.Cross(Vector3.up,dir)*.045f;
                    grass.Triangle(start-side,end,start+side,color);grass.Triangle(start+side,end,start-side,color);
                }
                if(i%8==0&&biome!=ArtBiome.Desert)
                {
                    for(int blossom=0;blossom<4;blossom++)flowers.Ellipsoid(p+new Vector3(Mathf.Sin(blossom*2)*.25f,.33f+blossom*.03f,Mathf.Cos(blossom*2)*.25f),new Vector3(.13f,.065f,.13f),i%3==0?P(.83f,.68f,.24f,0):P(.63f,.44f,.78f,0),7,4);
                }
            }
            Emit(root,"Modeled grass and bankside cover",grass.Finish("Ground cover batch"),foliage);if(flowers.VertexCount>0)Emit(root,"Scattered meadow flowers",flowers.Finish("Flower color accents"),surface);
        }
        private static void VillageProps(ArtBiome biome,Transform root,Material material,float x,float z)
        {
            var b=new MeshBuilder();
            for(int k=0;k<3;k++)
            {
                var p=At(biome,x+k*.8f,z+k%2*.6f);b.Frustum(p,p+Vector3.up*.83f,.31f,.30f,TimberLight,14);
                for(int band=0;band<2;band++)b.Frustum(p+Vector3.up*(.17f+band*.48f),p+Vector3.up*(.24f+band*.48f),.327f,.327f,Metal,14);
            }
            var crate=At(biome,x-1,z+.35f);b.Box(crate+Vector3.up*.43f,new Vector3(.85f,.85f,.85f),TimberLight,.07f);b.Beam(crate+new Vector3(-.36f,.08f,-.45f),crate+new Vector3(.36f,.78f,-.45f),.1f,.07f,Timber);
            var wagon=At(biome,x+2.5f,z-.8f);b.Box(wagon+Vector3.up*.65f,new Vector3(1.3f,.2f,2.1f),Timber,.06f);
            for(int side=-1;side<=1;side+=2)
            {
                for(int row=0;row<3;row++)b.Box(wagon+new Vector3(side*.64f,.8f+row*.17f,0),new Vector3(.09f,.12f,2.1f),TimberLight,.02f);
                for(int axle=-1;axle<=1;axle+=2)
                {
                    var hub=wagon+new Vector3(side*.78f,.43f,axle*.69f);b.Frustum(hub-Vector3.right*.07f,hub+Vector3.right*.07f,.39f,.39f,Timber,14);
                    for(int spoke=0;spoke<6;spoke++){float a=spoke*Mathf.PI/3;b.Beam(hub,hub+new Vector3(0,Mathf.Cos(a)*.34f,Mathf.Sin(a)*.34f),.045f,.05f,TimberLight);}
                }
                b.Beam(wagon+new Vector3(side*.35f,.56f,-.8f),wagon+new Vector3(side*.35f,.3f,-2.8f),.09f,.09f,Timber);
            }
            Emit(root,"Village provisions barrels cart and crates",b.Finish("Grouped settlement props"),material);
        }

        private static void Caribbean(Transform root,Material surface,Material water,Material foliage)
        {
            const ArtBiome biome=ArtBiome.Caribbean;
            var sea=new MeshBuilder();var foam=new MeshBuilder();
            var wet=P(.16f,.53f,.77f,0,0,.9f);var wash=new Paint(new Color(.79f,.94f,.86f),0,0,.65f,.72f);
            for(float z=-72;z<72;z+=1)
            {
                float sx=-8.6f+Mathf.Sin(z*.13f)*3,nx=-8.6f+Mathf.Sin((z+1)*.13f)*3;
                sea.Quad(new Vector3(-72,.04f,z),new Vector3(-72,.04f,z+1),new Vector3(nx,.04f,z+1),new Vector3(sx,.04f,z),wet,12);
                foam.Quad(new Vector3(sx-.40f,.055f,z),new Vector3(nx-.40f,.055f,z+1),new Vector3(nx,.055f,z+1),new Vector3(sx,.055f,z),wash,.8f);
            }
            var ocean=Emit(root,"Turquoise cove continuous ocean",sea.Finish("Coastal water surface"),water);ocean.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
            Emit(root,"Shallow shoreline wash",foam.Finish("Broken shoreline foam"),water).GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
            Path(biome,root,surface,new[]{new Vector2(1,-30),new Vector2(1,-10),new Vector2(4,-4),new Vector2(8,4),new Vector2(9,15),new Vector2(15,29)},3.1f);
            Path(biome,root,surface,new[]{new Vector2(-6,-1),new Vector2(4,-1),new Vector2(18,7),new Vector2(28,11)},2.4f);
            House(biome,root,surface,9,5,0,1.02f);House(biome,root,surface,16,10,1,1.07f);House(biome,root,surface,22,4,0,.9f);House(biome,root,surface,7,17,2,.95f);
            Dock(root,surface);Ship(root,surface,new Vector3(-23,.06f,7),-8,1);
            PortTower(root,surface,At(biome,-1,12),1.0f);PortTower(root,surface,At(biome,22,23),1.25f);
            Ruins(biome,root,surface,At(biome,2,28),false);
            for(int i=0;i<12;i++)
            {
                float z=-19+i*4.8f,x=-3.8f+Mathf.Sin(z*.13f)*2.4f;
                if(z>-3&&z<7)continue;
                Tree(biome,root,foliage,x,z,(.80f+i%3*.13f)*.85f,i*92,true);
            }
            Forest(biome,root,foliage,surface,true);
            for(int i=0;i<9;i++)Tree(biome,root,foliage,12+(i%3)*9,27+(i/3)*7,1.1f+i%2*.20f,i*137,false);
            GroundCover(biome,root,foliage,surface);VillageProps(biome,root,surface,7,2);VillageProps(biome,root,surface,15,7);
            Market(root,surface,At(biome,20,14),P(.59f,.24f,.16f,0),false);
        }
        private static void Dock(Transform root,Material material)
        {
            var b=new MeshBuilder();
            void Deck(Vector3 centre,int count,bool alongX)
            {
                for(int n=0;n<count;n++)
                {
                    float d=(n-(count-1)*.5f)*.34f;var p=centre+(alongX?Vector3.right:Vector3.forward)*d;
                    b.Box(p,new Vector3(alongX?.31f:2.5f,.16f,alongX?2.5f:.31f),n%4==0?Timber:TimberLight,.025f);
                }
                int poles=Mathf.CeilToInt(count*.34f/2.8f);
                for(int n=0;n<=poles;n++)for(int side=-1;side<=1;side+=2)
                {
                    float d=(n/(float)poles-.5f)*count*.34f;var p=centre+(alongX?Vector3.right:Vector3.forward)*d+(alongX?Vector3.forward:Vector3.right)*side*1.21f;
                    b.Frustum(p-Vector3.up*2.6f,p+Vector3.up*.86f,.12f,.105f,Timber,10);
                    b.Frustum(p+Vector3.up*.28f,p+Vector3.up*.39f,.14f,.14f,P(.66f,.55f,.32f,12),10);
                    if(n<poles)
                    {
                        var end=p+(alongX?Vector3.right:Vector3.forward)*(count*.34f/poles);
                        Curve(b,new[]{p+Vector3.up*.64f,(p+end)*.5f+Vector3.up*.40f,end+Vector3.up*.64f},.035f,P(.58f,.43f,.23f,12),6);
                    }
                }
                b.Beam(centre+(alongX?Vector3.right:Vector3.forward)*(-count*.17f)-Vector3.up*.19f,centre+(alongX?Vector3.right:Vector3.forward)*(count*.17f)-Vector3.up*.19f,.22f,.24f,Timber);
            }
            Deck(new Vector3(-13.5f,1.05f,-1),49,true);Deck(new Vector3(-20,1.05f,5.5f),46,false);
            for(int i=0;i<4;i++)
            {
                var p=new Vector3(-18+i*.9f,1.16f,-.8f);b.Box(p+Vector3.up*.43f,new Vector3(.8f,.8f,.8f),TimberLight,.05f);
                b.Beam(p+new Vector3(-.4f,.04f,-.43f),p+new Vector3(.4f,.79f,-.43f),.09f,.07f,Timber);
            }
            Emit(root,"Working harbor plank piers ropes and cargo",b.Finish("Grouped harbor pier"),material);
        }
        private static void Ship(Transform root,Material material,Vector3 position,float yaw,float scale)
        {
            Lod(root,"Moored trading vessel carved hull rigging and sails",position,Vector3.one*scale,yaw,material,lod=>Cache("caribbean-trader-"+lod,()=>{
                var b=new MeshBuilder();int segments=lod==0?22:12;
                float Width(float z)=>Mathf.Pow(Mathf.Max(0,Mathf.Sin((z+5.9f)/11.8f*Mathf.PI)),.62f)*2.03f+.025f;
                for(int side=-1;side<=1;side+=2)for(int k=0;k<segments;k++)
                {
                    float z=-5.9f+k*11.8f/segments,nz=-5.9f+(k+1)*11.8f/segments,w=Width(z),nw=Width(nz);
                    for(int band=0;band<5;band++)
                    {
                        float y=-.68f+band*.45f,ny=y+.44f,spread=Mathf.Sin((band+.3f)/5.2f*Mathf.PI*.5f),nspread=Mathf.Sin((band+1.3f)/5.2f*Mathf.PI*.5f);
                        var a=new Vector3(side*w*spread,y,z);var c=new Vector3(side*nw*nspread,ny,nz);var d=new Vector3(side*w*nspread,ny,z);var e=new Vector3(side*nw*spread,y,nz);
                        if(side>0)b.Quad(a,d,c,e,band%2==0?Timber:TimberLight);else b.Quad(e,c,d,a,band%2==0?Timber:TimberLight);
                    }
                    b.Beam(new Vector3(side*w,1.67f,z),new Vector3(side*nw,1.67f,nz),.13f,.13f,Timber);
                    if(k%2==0)b.Frustum(new Vector3(side*w,1.47f,z),new Vector3(side*w,2.10f,z),.045f,.045f,Timber,6);
                    b.Beam(new Vector3(side*w,2.10f,z),new Vector3(side*nw,2.10f,nz),.045f,.055f,TimberLight);
                    if(side>0)b.Quad(new Vector3(-w,1.47f,z),new Vector3(-nw,1.47f,nz),new Vector3(nw,1.47f,nz),new Vector3(w,1.47f,z),TimberLight);
                }
                b.Beam(new Vector3(0,.1f,5.5f),new Vector3(0,2.5f,7.5f),.19f,.20f,Timber);
                b.Box(new Vector3(0,1.95f,-3.8f),new Vector3(2.9f,.80f,2.0f),P(.31f,.18f,.09f,2),.08f);
                b.Box(new Vector3(0,2.42f,-3.8f),new Vector3(3.15f,.19f,2.2f),TimberLight,.07f);
                for(int i=-1;i<=1;i++)b.Box(new Vector3(i*.71f,2.03f,-4.84f),new Vector3(.40f,.36f,.06f),P(.22f,.48f,.57f,3,.2f,.68f),.03f);
                var sail=P(.94f,.83f,.58f,0);
                for(int mast=0;mast<2;mast++)
                {
                    float z=mast==0?.6f:-3.2f,height=mast==0?9.2f:6.9f,w=mast==0?2.55f:1.75f;
                    b.Frustum(new Vector3(0,1.45f,z),new Vector3(0,height,z),.13f,.07f,Timber,12);
                    for(int level=0;level<2;level++)
                    {
                        float top=height-.9f-level*2.45f,bot=top-2.20f,sw=w-level*.16f;
                        b.Beam(new Vector3(-sw-.15f,top,z),new Vector3(sw+.15f,top,z),.11f,.13f,Timber);
                        int steps=lod==0?10:5;
                        for(int x=0;x<steps;x++)for(int y=0;y<4;y++)
                        {
                            Vector3 V(float u,float v)=>new Vector3((u-.5f)*sw*2,Mathf.Lerp(bot,top,v)+Mathf.Sin(u*Mathf.PI)*.17f,z-.18f-Mathf.Sin(v*Mathf.PI)*Mathf.Sin(u*Mathf.PI)*.75f);
                            var a=V(x/(float)steps,y/4f);var c=V((x+1)/(float)steps,(y+1)/4f);var d=V(x/(float)steps,(y+1)/4f);var e=V((x+1)/(float)steps,y/4f);
                            b.Quad(a,e,c,d,sail);b.Quad(d,c,e,a,sail);
                        }
                        for(int side=-1;side<=1;side+=2)b.Beam(new Vector3(side*sw,bot,z),new Vector3(side*1.5f,1.7f,z+1.2f),.028f,.028f,Timber);
                    }
                    for(int side=-1;side<=1;side+=2)
                    {
                        b.Beam(new Vector3(0,height-.45f,z),new Vector3(side*1.8f,1.7f,z-1.2f),.032f,.032f,Timber);
                        b.Beam(new Vector3(0,height-.45f,z),new Vector3(side*1.8f,1.7f,z+1.2f),.032f,.032f,Timber);
                    }
                }
                b.Frustum(new Vector3(0,9.2f,.6f),new Vector3(0,9.7f,.6f),.04f,.035f,Timber,6);
                b.Triangle(new Vector3(0,9.75f,.6f),new Vector3(1.9f,9.5f,.65f),new Vector3(0,9.13f,.6f),Cloth);
                b.Triangle(new Vector3(0,9.13f,.6f),new Vector3(1.9f,9.5f,.65f),new Vector3(0,9.75f,.6f),Cloth);
                return b.Finish("Curved planked trading ship LOD"+lod);
            }));
        }
        private static void PortTower(Transform root,Material material,Vector3 position,float scale)
        {
            Reserve(position.x,position.z,7*scale,7*scale);
            Lod(root,"Coastal lookout with braced timber balcony",position,Vector3.one*scale,-10,material,lod=>Cache("port-lookout-"+lod,()=>{
                var b=new MeshBuilder();b.Frustum(Vector3.zero,Vector3.up*3.2f,1.72f,1.40f,Stone,lod==0?20:12);
                b.Frustum(Vector3.up*3.0f,Vector3.up*3.25f,1.91f,1.91f,Timber,16);
                for(int s=-1;s<=1;s+=2)for(int t=-1;t<=1;t+=2)
                {
                    b.Beam(new Vector3(s*1.25f,2.9f,t*1.25f),new Vector3(s*1.25f,6.2f,t*1.25f),.16f,.16f,Timber);
                    b.Beam(new Vector3(s*1.23f,3.25f,t*1.23f),new Vector3(-s*1.23f,4.1f,t*1.23f),.09f,.09f,TimberLight);
                }
                b.Box(new Vector3(0,4.2f,0),new Vector3(3.1f,.19f,3.1f),TimberLight,.04f);
                for(int side=-1;side<=1;side+=2)
                {
                    b.Beam(new Vector3(-1.45f,5.05f,side*1.45f),new Vector3(1.45f,5.05f,side*1.45f),.10f,.11f,Timber);
                    b.Beam(new Vector3(side*1.45f,5.05f,-1.45f),new Vector3(side*1.45f,5.05f,1.45f),.10f,.11f,Timber);
                }
                b.Roof(new Vector3(0,6.15f,0),3.7f,3.7f,1.6f,P(.67f,.28f,.14f,8),lod==0?7:4);
                b.Arch(new Vector3(0,0,-1.57f),.80f,1.7f,.18f,StoneLight,10);b.Box(new Vector3(0,.79f,-1.60f),new Vector3(.77f,1.5f,.1f),Timber,.03f);
                for(int i=0;i<12;i++)b.Box(new Vector3(2.0f,.2f+i*.34f,-1.7f+i*.27f),new Vector3(.85f,.18f,.42f),TimberLight,.035f);
                return b.Finish("Masonry and timber harbor lookout LOD"+lod);
            }));
        }
        private static void Curve(MeshBuilder b,Vector3[] points,float radius,Paint paint,int sides=10)
        {
            for(int i=0;i<points.Length-1;i++)b.Frustum(points[i],points[i+1],radius*(1-i/(float)points.Length*.45f),radius*(1-(i+1)/(float)points.Length*.45f),paint,sides);
        }
        private static void Ruins(ArtBiome biome,Transform root,Material material,Vector3 position,bool enchanted)
        {
            Lod(root,enchanted?"Mossy runestone sanctuary remains":"Jungle reclaimed stone arch ruins",position,Vector3.one,13,material,lod=>Cache("ruins-"+enchanted+"-"+lod,()=>{
                var b=new MeshBuilder();var stone=enchanted?P(.48f,.65f,.54f,15):P(.67f,.65f,.46f,4);
                for(int k=0;k<3;k++)
                {
                    float x=(k-1)*3.6f;b.Arch(new Vector3(x,0,k%2*.4f),2.8f,4.3f,.8f,stone,lod==0?16:9);
                    b.Box(new Vector3(x,4.52f,k%2*.4f),new Vector3(3.4f,.36f,1.02f),StoneLight,.08f);
                }
                for(int i=0;i<10;i++)
                {
                    float x=-6+i*1.3f,z=2.8f+Mathf.Sin(i*2.1f)*1.8f;
                    b.Box(new Vector3(x,.33f,z),new Vector3(.9f,.64f,1.13f),stone,.12f,Quaternion.Euler(i*3,i*49,i%2*12));
                }
                for(int side=-1;side<=1;side+=2)b.Frustum(new Vector3(side*6,0,3),new Vector3(side*6,3.2f,3),.43f,.34f,stone,lod==0?16:10);
                return b.Finish("Layered ancient archway ruins LOD"+lod);
            }));
        }
        private static void Market(Transform root,Material material,Vector3 position,Paint cloth,bool desert)
        {
            var b=new MeshBuilder();var poles=desert?TimberLight:Timber;
            for(int side=-1;side<=1;side+=2)for(int rear=0;rear<2;rear++)
                b.Frustum(new Vector3(side*1.9f,0,rear*2.3f),new Vector3(side*1.9f,2.6f+rear*.5f,rear*2.3f),.075f,.055f,poles,8);
            for(int strip=0;strip<8;strip++)for(int length=0;length<5;length++)
            {
                Vector3 V(float u,float v)=>new Vector3((u-.5f)*4.1f,2.65f+v*.5f-Mathf.Sin(u*Mathf.PI)*.16f-Mathf.Sin(v*Mathf.PI)*.26f,v*2.6f-.13f);
                float a=strip/8f,c=(strip+1)/8f,d=length/5f,e=(length+1)/5f;var stripe=strip%3==0?P(.94f,.83f,.56f,0):cloth;
                b.Quad(V(a,d),V(a,e),V(c,e),V(c,d),stripe);
                b.Quad(V(c,d),V(c,e),V(a,e),V(a,d),stripe);
            }
            for(int k=0;k<8;k++)
            {
                var a=new Vector3(-2.05f+k*.5125f,2.62f,-.13f);b.Quad(a,a+Vector3.right*.51f,a+new Vector3(.51f,-.27f,0),a-Vector3.up*.27f,k%3==0?Plaster:cloth);
            }
            b.Box(new Vector3(0,.86f,.85f),new Vector3(3.5f,.18f,.85f),TimberLight,.04f);
            for(int side=-1;side<=1;side+=2)b.Box(new Vector3(side*1.5f,.43f,.85f),new Vector3(.12f,.86f,.65f),poles,.02f);
            for(int basket=0;basket<5;basket++)
            {
                var p=new Vector3(-1.35f+basket*.67f,.99f,.83f);b.Frustum(p,p+Vector3.up*.21f,.23f,.29f,TimberLight,12);
                for(int fruit=0;fruit<5;fruit++)b.Ellipsoid(p+new Vector3(Mathf.Sin(fruit*2.4f)*.16f,.22f,Mathf.Cos(fruit*2.4f)*.16f),Vector3.one*.17f,basket%2==0?P(.88f,.42f,.11f,0):P(.52f,.60f,.17f,0),8,5);
            }
            for(int jar=0;jar<3;jar++)
            {
                var p=new Vector3(-2.4f,0,.35f+jar*.65f);b.Ellipsoid(p+Vector3.up*.36f,new Vector3(.55f,.74f,.55f),P(.69f,.31f,.16f,8),12,8);
                b.Frustum(p+Vector3.up*.65f,p+Vector3.up*.81f,.13f,.16f,P(.76f,.40f,.22f,8),12);
                b.Frustum(p+Vector3.up*.812f,p+Vector3.up*.815f,.12f,.12f,Dark,12);
            }
            Emit(root,"Provision market striped awning fruit baskets and pottery",b.Finish("Grouped market stall"),material,position);
        }
        private static void Desert(Transform root,Material surface,Material water,Material foliage)
        {
            const ArtBiome biome=ArtBiome.Desert;
            var pond=new MeshBuilder();var p=P(.14f,.63f,.49f,0,0,.86f);var centre=new Vector3(-12,.035f,2);
            for(int k=0;k<80;k++)
            {
                float a=k*Mathf.PI/40,b=(k+1)*Mathf.PI/40,ra=4.5f+Mathf.Sin(a*3)*.11f,rb=4.5f+Mathf.Sin(b*3)*.11f;
                pond.Triangle(centre,centre+new Vector3(Mathf.Cos(b)*rb,0,Mathf.Sin(b)*rb),centre+new Vector3(Mathf.Cos(a)*ra,0,Mathf.Sin(a)*ra),p);
            }
            Emit(root,"Clear spring fed oasis pool",pond.Finish("Oasis water"),water).GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
            Path(biome,root,surface,new[]{new Vector2(0,-32),new Vector2(1,-12),new Vector2(3,-5),new Vector2(6,4),new Vector2(7,12),new Vector2(2,17),new Vector2(-3,19),new Vector2(-3,30)},3.8f);
            Path(biome,root,surface,new[]{new Vector2(-9,-5),new Vector2(-3,-1),new Vector2(10,2),new Vector2(20,8),new Vector2(31,12)},2.8f);
            SandHouse(root,surface,At(biome,11,5),0,1.02f,-6);
            SandHouse(root,surface,At(biome,19,10),1,1.04f,8);
            SandHouse(root,surface,At(biome,23,1),2,.92f,-12);
            SandHouse(root,surface,At(biome,1,13),0,.92f,14);
            DesertFort(root,surface,At(biome,-3,25));
            Market(root,surface,At(biome,16,2),P(.71f,.25f,.17f,0),true);
            Market(root,surface,At(biome,18,15),P(.19f,.46f,.51f,0),true);
            Obelisk(root,surface,At(biome,-10,17),1.0f);Obelisk(root,surface,At(biome,4,17),1.0f);
            for(int i=0;i<8;i++)
            {
                float a=i*Mathf.PI*.25f;float x=-12+Mathf.Cos(a)*6.9f,z=2+Mathf.Sin(a)*7.1f;
                if(z<0&&x>-9)continue;
                Tree(biome,root,foliage,x,z,(.83f+i%3*.1f)*.85f,i*117,true);
            }
            for(int i=0;i<4;i++)Tree(biome,root,foliage,22+i%2*4,18+i/2*10,.8f+i*.05f,i*121,true);
            var cliffs=new MeshBuilder();var rock=P(.63f,.44f,.27f,14);
            var outcrops=new[]{new Vector3(-31,4.7f,19),new Vector3(-28.3f,5.9f,22),new Vector3(-23.8f,4.8f,25.3f),new Vector3(-29,5.4f,29),new Vector3(-21.6f,4.1f,31.7f),new Vector3(-25.5f,6.7f,35.5f)};
            for(int i=0;i<outcrops.Length;i++)
            {
                float x=outcrops[i].x,z=outcrops[i].z,h=Height(biome,x,z),height=outcrops[i].y;
                cliffs.Ellipsoid(new Vector3(x,h+height*.21f,z),new Vector3(7.5f+i%2,height,5.7f+i%3*.45f),new Paint(rock.Color*(.92f+i%3*.045f),14),24,16,.14f,Quaternion.Euler(i%2*7,i*39,-7+i%3*6));
                cliffs.Ellipsoid(new Vector3(x+1.8f,h+.65f,z-1.8f),new Vector3(5.2f,2.8f,3.9f),rock,20,12,.17f,Quaternion.Euler(4,i*27,12));
            }
            Emit(root,"Wind carved sandstone escarpments",cliffs.Finish("Layered desert outcrops"),surface);
            for(int i=0;i<16;i++)
            {
                float x=-28+i*13%61,z=-21+i*17%54;
                if(Reserved(x,z,1.1f))continue;
                Rock(biome,root,surface,x,z,.55f+i%4*.30f);
            }
            GroundCover(biome,root,foliage,surface);OasisPlants(root,foliage);
            VillageProps(biome,root,surface,8,1);VillageProps(biome,root,surface,23,12);
        }
        private static void SandHouse(Transform root,Material material,Vector3 position,int variant,float scale,float yaw)
        {
            Reserve(position.x,position.z,9*scale,8*scale);
            Lod(root,"Sandstone courtyard residence with ceramic dome",position,Vector3.one*scale,yaw,material,lod=>Cache("sand-residence-"+variant+"-"+lod,()=>{
                var b=new MeshBuilder();var wall=P(.88f,.68f,.40f,13);var trim=P(.94f,.79f,.52f,4);var tile=P(.16f,.44f,.48f,8,0,.48f);
                float width=variant==1?5.6f:4.8f,depth=4.7f,h=variant==1?4.2f:3.5f;
                b.Box(new Vector3(0,.24f,0),new Vector3(width+.6f,.48f,depth+.6f),P(.73f,.50f,.28f,4),.15f);
                b.Box(new Vector3(0,h*.5f+.22f,0),new Vector3(width,h,depth),wall,.19f);
                b.Box(new Vector3(0,h+.25f,0),new Vector3(width+.38f,.3f,depth+.38f),trim,.085f);
                for(int side=-1;side<=1;side+=2)
                {
                    b.Box(new Vector3(side*width*.5f,h+.62f,0),new Vector3(.25f,.62f,depth),wall,.055f);
                    b.Box(new Vector3(0,h+.62f,side*depth*.5f),new Vector3(width,.62f,.25f),wall,.055f);
                    for(int i=0;i<6;i++)b.Box(new Vector3(-width*.47f+i*width*.188f,h+1.04f,side*depth*.5f),new Vector3(.36f,.43f,.32f),trim,.06f);
                    for(int i=0;i<5;i++)b.Box(new Vector3(side*width*.5f,h+1.04f,-depth*.45f+i*depth*.225f),new Vector3(.32f,.43f,.36f),trim,.06f);
                    b.Box(new Vector3(side*width*.47f,h*.5f+.24f,-depth*.51f),new Vector3(.24f,h,.25f),trim,.06f);
                }
                if(variant!=2)
                {
                    b.Frustum(new Vector3(.35f,h+.3f,.32f),new Vector3(.35f,h+.85f,.32f),1.60f,1.48f,trim,lod==0?24:14);
                    b.Ellipsoid(new Vector3(.35f,h+.86f,.32f),new Vector3(3.10f,2.50f,3.10f),tile,lod==0?28:16,lod==0?14:8);
                    b.Frustum(new Vector3(.35f,h+1.99f,.32f),new Vector3(.35f,h+2.57f,.32f),.12f,.03f,Gold,10);
                    if(lod==0)for(int ring=0;ring<3;ring++)b.Frustum(new Vector3(.35f,h+.85f+ring*.12f,.32f),new Vector3(.35f,h+.87f+ring*.12f,.32f),1.55f-ring*.01f,1.55f-ring*.01f,P(.29f,.62f,.61f,8,0,.55f),28);
                }
                else
                {
                    for(int i=0;i<5;i++)b.Beam(new Vector3(-2.2f,h+1.25f,-1.4f+i*.6f),new Vector3(2.2f,h+1.25f,-1.4f+i*.6f),.12f,.12f,Timber);
                    b.Quad(new Vector3(-1.9f,h+1.26f,-1.3f),new Vector3(-1.9f,h+1.26f,1.1f),new Vector3(1.9f,h+1.26f,1.1f),new Vector3(1.9f,h+1.26f,-1.3f),P(.79f,.38f,.24f,0));
                }
                b.Box(new Vector3(0,1.17f,-depth*.5f-.035f),new Vector3(1.34f,2.05f,.08f),Dark,.03f);
                b.Arch(new Vector3(0,.19f,-depth*.5f-.19f),1.32f,2.25f,.30f,trim,lod==0?15:8);
                for(int side=-1;side<=1;side+=2)
                {
                    float x=side*width*.32f;
                    b.Box(new Vector3(x,2.10f,-depth*.5f-.045f),new Vector3(.65f,1.08f,.08f),Dark,.04f);
                    b.Arch(new Vector3(x,1.62f,-depth*.5f-.12f),.63f,1.17f,.18f,tile,lod==0?10:6);
                    for(int bar=0;bar<3;bar++)b.Beam(new Vector3(x-.2f+bar*.2f,1.64f,-depth*.5f-.23f),new Vector3(x-.2f+bar*.2f,2.48f,-depth*.5f-.23f),.035f,.035f,Gold);
                    b.Box(new Vector3(side*(width*.5f+.03f),2.12f,.4f),new Vector3(.06f,1.17f,.68f),Dark,.02f);
                    b.Arch(new Vector3(side*(width*.5f+.12f),1.55f,.4f),.63f,1.29f,.18f,trim,lod==0?10:6,Quaternion.Euler(0,90,0));
                }
                for(int step=0;step<3;step++)b.Box(new Vector3(0,.08f+step*.095f,-depth*.5f-.85f+step*.26f),new Vector3(1.8f,.16f,1.05f-step*.2f),trim,.05f);
                if(variant==1)Minaret(b,new Vector3(width*.5f+.15f,0,depth*.31f),8.3f,lod,wall,trim,tile);
                return b.Finish("Layered courtyard sandstone and glazed dome LOD"+lod);
            }));
        }
        private static void Minaret(MeshBuilder b,Vector3 p,float h,int lod,Paint wall,Paint trim,Paint tile)
        {
            b.Frustum(p,p+Vector3.up*h,.74f,.55f,wall,lod==0?16:10);
            for(int k=0;k<3;k++)b.Frustum(p+Vector3.up*(h*.3f+k*h*.28f),p+Vector3.up*(h*.3f+k*h*.28f+.17f),.86f,.86f,trim,lod==0?16:10);
            b.Frustum(p+Vector3.up*(h-.1f),p+Vector3.up*(h+.16f),1.02f,1.02f,trim,16);
            for(int k=0;k<8;k++)
            {
                float a=k*Mathf.PI*.25f;var q=new Vector3(Mathf.Cos(a)*.75f,0,Mathf.Sin(a)*.75f);
                b.Frustum(p+q+Vector3.up*h,p+q+Vector3.up*(h+.65f),.045f,.045f,Gold,6);
            }
            b.Frustum(p+Vector3.up*(h+.66f),p+Vector3.up*(h+.74f),.85f,.85f,Gold,16);
            b.Ellipsoid(p+Vector3.up*(h+.62f),new Vector3(1.38f,1.9f,1.38f),tile,lod==0?20:12,lod==0?12:7);
            b.Frustum(p+Vector3.up*(h+1.5f),p+Vector3.up*(h+2.15f),.10f,.014f,Gold,8);
            for(int k=0;k<2;k++)b.Box(p+new Vector3(0,h*.4f+k*h*.27f,-.67f),new Vector3(.21f,.84f,.07f),Dark,.05f);
        }
        private static void DesertFort(Transform root,Material material,Vector3 position)
        {
            Reserve(position.x,position.z,22,16);
            Lod(root,"Oasis citadel of warm sandstone and turquoise ceramics",position,Vector3.one,0,material,lod=>Cache("desert-citadel-"+lod,()=>{
                var b=new MeshBuilder();var wall=P(.80f,.57f,.32f,4);var trim=P(.93f,.76f,.47f,4);var glaze=P(.16f,.44f,.47f,8,0,.48f);
                b.Box(new Vector3(0,-1.0f,0),new Vector3(18.4f,2.2f,12.4f),wall,.15f);
                b.Box(new Vector3(0,.25f,0),new Vector3(18.4f,.5f,12.4f),wall,.2f);
                for(int side=-1;side<=1;side+=2)
                {
                    b.Box(new Vector3(0,2.65f,side*5.1f),new Vector3(16.1f,5.3f,1.0f),wall,.17f);
                    b.Box(new Vector3(side*7.5f,2.65f,0),new Vector3(1.0f,5.3f,10.3f),wall,.17f);
                    b.Box(new Vector3(0,5.13f,side*5.1f),new Vector3(16.6f,.23f,1.18f),trim,.055f);
                    for(int n=0;n<15;n++)b.Box(new Vector3(-7.35f+n*1.05f,5.63f,side*5.1f),new Vector3(.53f,.9f,1.16f),trim,.075f);
                    for(int n=0;n<10;n++)b.Box(new Vector3(side*7.5f,5.63f,-4.72f+n*1.05f),new Vector3(1.16f,.9f,.53f),trim,.075f);
                    for(int rear=-1;rear<=1;rear+=2)
                    {
                        var p=new Vector3(side*7.5f,0,rear*5.1f);b.Box(p+Vector3.up*3.25f,new Vector3(3.05f,6.5f,3.05f),wall,.18f);
                        b.Box(p+Vector3.up*6.48f,new Vector3(3.46f,.35f,3.46f),trim,.09f);
                        for(int edge=-1;edge<=1;edge+=2)for(int n=-1;n<=1;n++)
                        {
                            b.Box(p+new Vector3(n*1.22f,7.05f,edge*1.45f),new Vector3(.6f,.9f,.6f),trim,.06f);
                            b.Box(p+new Vector3(edge*1.45f,7.05f,n*1.22f),new Vector3(.6f,.9f,.6f),trim,.06f);
                        }
                        b.Box(p+new Vector3(0,4.8f,-1.55f),new Vector3(.35f,1.25f,.08f),Dark,.035f);
                    }
                }
                b.Box(new Vector3(0,3.9f,.7f),new Vector3(7,7.8f,6.1f),P(.91f,.72f,.46f,13),.18f);
                b.Frustum(new Vector3(0,7.73f,.7f),new Vector3(0,8.15f,.7f),3.25f,3.05f,trim,lod==0?28:16);
                b.Ellipsoid(new Vector3(0,8.14f,.7f),new Vector3(6.1f,4.3f,6.1f),glaze,lod==0?30:18,lod==0?16:9);
                b.Frustum(new Vector3(0,10.20f,.7f),new Vector3(0,11.30f,.7f),.2f,.026f,Gold,12);
                Minaret(b,new Vector3(4.6f,0,2.9f),11,lod,wall,trim,glaze);
                b.Box(new Vector3(0,2.35f,-5.66f),new Vector3(2.6f,4.42f,.10f),Dark,.06f);
                b.Arch(new Vector3(0,.15f,-5.82f),2.70f,4.70f,.45f,trim,lod==0?18:10);
                b.Arch(new Vector3(0,.14f,-6.08f),3.45f,5.18f,.24f,P(.28f,.51f,.49f,8),lod==0?18:10);
                for(int n=0;n<7;n++)b.Box(new Vector3(-1.08f+n*.36f,1.75f,-5.75f),new Vector3(.31f,3.2f,.055f),TimberLight,.015f);
                for(int y=0;y<2;y++)for(int x=-1;x<=1;x++)
                {
                    var p=new Vector3(x*1.95f,3.5f+y*2.2f,-2.41f);b.Box(p+Vector3.up*.62f,new Vector3(.73f,1.23f,.08f),Dark,.05f);b.Arch(p,.72f,1.45f,.22f,trim,lod==0?12:7);
                }
                return b.Finish("Original sandstone fortified palace LOD"+lod);
            }));
        }
        private static void Obelisk(Transform root,Material material,Vector3 position,float scale)
        {
            var mesh=Cache("sandstone-obelisk",()=>{
                var b=new MeshBuilder();var stone=P(.80f,.62f,.38f,4);b.Box(new Vector3(0,.16f,0),new Vector3(1.65f,.32f,1.65f),StoneLight,.08f);b.Box(new Vector3(0,.46f,0),new Vector3(1.21f,.28f,1.21f),stone,.06f);
                for(int face=0;face<4;face++)
                {
                    var q=Quaternion.Euler(0,face*90,0);Vector3 V(float x,float y,float z)=>q*new Vector3(x,y,z);
                    b.Quad(V(-.42f,.58f,-.42f),V(-.28f,5.3f,-.28f),V(.28f,5.3f,-.28f),V(.42f,.58f,-.42f),stone,3);
                    b.Triangle(V(-.28f,5.3f,-.28f),new Vector3(0,6.15f,0),V(.28f,5.3f,-.28f),Gold);
                    for(int mark=0;mark<5;mark++)
                    {
                        float y=1.2f+mark*.70f,z=Mathf.Lerp(-.42f,-.28f,(y-.58f)/4.72f)-.015f;
                        b.Beam(V(-.11f,y,z),V(.11f,y+.13f,z),.036f,.029f,P(.29f,.43f,.39f,15));
                        b.Beam(V(.11f,y+.13f,z),V(-.08f,y+.28f,z),.036f,.029f,P(.29f,.43f,.39f,15));
                    }
                }
                return b.Finish("Tapered engraved gate obelisk");
            });Emit(root,"Engraved sandstone obelisk",mesh,material,position,Vector3.one*scale);
        }
        private static void OasisPlants(Transform root,Material material)
        {
            var b=new MeshBuilder();var leaf=P(.25f,.46f,.17f,10);
            for(int cluster=0;cluster<48;cluster++)
            {
                float a=cluster*2.39996f,r=4.5f+cluster%4*.37f;var p=At(ArtBiome.Desert,-12+Mathf.Cos(a)*r,2+Mathf.Sin(a)*r);
                for(int reed=0;reed<5;reed++)
                {
                    var tip=p+new Vector3(Mathf.Sin(reed*2.4f)*.16f,.55f+reed*.09f,Mathf.Cos(reed*2.4f)*.16f);
                    b.Frustum(p,tip,.021f,.014f,leaf,5);b.Ellipsoid(tip,new Vector3(.065f,.20f,.065f),P(.43f,.31f,.13f,12),6,4);
                    b.Triangle(p+new Vector3(-.08f,.05f,0),tip+new Vector3(.15f,-.15f,.03f),p+new Vector3(.03f,.28f,0),leaf);
                }
            }
            Emit(root,"Oasis reed beds and moist bank vegetation",b.Finish("Oasis reeds"),material);
        }
        private static void Fantasy(Transform root,Material surface,Material water,Material foliage,Material crystal)
        {
            const ArtBiome biome=ArtBiome.Fantasy;River(biome,root,water);
            Path(biome,root,surface,new[]{new Vector2(1,-32),new Vector2(1,-12),new Vector2(3,-5),new Vector2(7,4),new Vector2(9,16),new Vector2(4,29)},3.6f,true);
            Path(biome,root,surface,new[]{new Vector2(-25,2),new Vector2(-13,2),new Vector2(-3,0),new Vector2(12,3),new Vector2(26,11)},2.7f,true);
            Sanctum(root,surface,At(biome,12,6),0,1.0f,-8);Sanctum(root,surface,At(biome,21,12),1,1.1f,8);Sanctum(root,surface,At(biome,22,1),0,.84f,-12);
            Sanctum(root,surface,At(biome,4,23),2,1.45f,0);
            AncientTree(root,foliage,At(biome,-5,16));
            RootBridge(root,surface,new Vector3(-12+Mathf.Sin(2*.095f)*3,.1f,2));
            Ruins(biome,root,surface,At(biome,-22,16),true);
            var spots=new[]{new Vector2(-6,8),new Vector2(17,3),new Vector2(-20,-6),new Vector2(1,26),new Vector2(25,19),new Vector2(-7,-18),new Vector2(20,-12)};
            for(int i=0;i<spots.Length;i++)CrystalCluster(root,crystal,surface,At(biome,spots[i].x,spots[i].y),.70f+i%3*.24f,i*53);
            Forest(biome,root,foliage,surface,false);GroundCover(biome,root,foliage,surface);
            var bank=new MeshBuilder();var moss=P(.18f,.36f,.26f,10);
            for(int i=0;i<32;i++)
            {
                float z=-22+i*1.8f,x=-12+Mathf.Sin(z*.095f)*3+(i%2==0?-3.5f:3.5f);var p=At(biome,x,z);
                bank.Ellipsoid(p+Vector3.up*.14f,new Vector3(.80f,.32f,.66f),moss,12,7,.14f);
                if(i%3==0)for(int shroom=0;shroom<3;shroom++)
                {
                    var c=p+new Vector3(.18f+shroom*.19f,.32f+shroom*.045f,.13f);bank.Frustum(c-Vector3.up*.21f,c,.03f,.023f,StoneLight,7);
                    bank.Ellipsoid(c,new Vector3(.27f,.095f,.27f),P(.34f,.59f,.59f,11),10,6);
                }
            }
            Emit(root,"Mossy fern banks and silvercap mushrooms",bank.Finish("Enchanted riverside ground details"),foliage);
        }
        private static void Sanctum(Transform root,Material material,Vector3 position,int variant,float scale,float yaw)
        {
            Reserve(position.x,position.z,9*scale,9*scale);
            Lod(root,"Elven sanctuary swept ribs carved arches and slender spires",position,Vector3.one*scale,yaw,material,lod=>Cache("grove-sanctum-"+variant+"-"+lod,()=>{
                var b=new MeshBuilder();var wall=P(.66f,.76f,.64f,13);var ribs=P(.42f,.54f,.43f,15);var roof=P(.17f,.37f,.34f,9,0,.35f);
                float h=variant==2?6.2f:4.3f,r=variant==1?2.5f:2.2f;
                b.Frustum(Vector3.zero,Vector3.up*.26f,r+1.0f,r+.9f,ribs,lod==0?24:14);
                b.Frustum(Vector3.up*.26f,Vector3.up*.52f,r+.65f,r+.5f,wall,lod==0?24:14);
                b.Frustum(Vector3.up*.50f,Vector3.up*h,r,r*.80f,wall,lod==0?24:14);
                for(int k=0;k<8;k++)
                {
                    float a=k*Mathf.PI*.25f;var outward=new Vector3(Mathf.Sin(a),0,Mathf.Cos(a));
                    var points=new[]{outward*(r+.43f)+Vector3.up*.35f,outward*(r+.36f)+Vector3.up*1.45f,outward*(r*.87f)+Vector3.up*(h-.20f),outward*(r*.56f)+Vector3.up*(h+1.65f),Vector3.up*(h+3.7f)};
                    Curve(b,points,lod==0?.14f:.12f,ribs,lod==0?12:8);
                    if(k%2==1)
                    {
                        var q=Quaternion.Euler(0,a*Mathf.Rad2Deg,0);var p=outward*(r*.94f)+Vector3.up*1.6f;
                        b.Box(p+Vector3.up*.78f,new Vector3(.66f,1.5f,.075f),P(.065f,.16f,.15f,15),.07f,q);
                        b.Arch(p,.69f,1.68f,.20f,StoneLight,lod==0?14:8,q);
                    }
                }
                // Concave roof profile, assembled in rings; every face has measured texture coordinates.
                int sides=lod==0?28:16;
                for(int ring=0;ring<7;ring++)for(int k=0;k<sides;k++)
                {
                    float t=ring/7f,nt=(ring+1)/7f,rad=r*1.32f*Mathf.Pow(1-t,1.45f)+.04f,nrad=r*1.32f*Mathf.Pow(1-nt,1.45f)+.04f;
                    float y=h-.06f+t*3.45f,ny=h-.06f+nt*3.45f,a=k*Mathf.PI*2/sides,na=(k+1)*Mathf.PI*2/sides;
                    Vector3 V(float rr,float yy,float aa)=>new Vector3(Mathf.Sin(aa)*rr,yy,Mathf.Cos(aa)*rr);
                    b.Quad(V(rad,y,a),V(rad,y,na),V(nrad,ny,na),V(nrad,ny,a),roof);
                }
                b.Frustum(Vector3.up*(h+3.32f),Vector3.up*(h+4.10f),.09f,.012f,Gold,10);
                b.Box(new Vector3(0,1.63f,-r-.035f),new Vector3(1.04f,2.10f,.09f),P(.045f,.12f,.10f,2),.05f);
                b.Arch(new Vector3(0,.54f,-r-.19f),1.05f,2.36f,.24f,StoneLight,lod==0?16:9);
                for(int side=-1;side<=1;side+=2)
                {
                    var points=new[]{new Vector3(side*1.45f,.2f,-r-.1f),new Vector3(side*1.5f,1.7f,-r-.17f),new Vector3(side*.94f,3.08f,-r-.20f),new Vector3(0,3.56f,-r-.20f)};
                    Curve(b,points,.10f,Gold,8);
                }
                for(int step=0;step<4;step++)b.Box(new Vector3(0,.075f+step*.11f,-r-1.14f+step*.24f),new Vector3(2.15f,.15f,1.1f-step*.20f),wall,.07f);
                if(variant>0)
                {
                    var p=new Vector3(r+.85f,0,r*.3f);b.Frustum(p,p+Vector3.up*(h+2.8f),.53f,.25f,wall,16);
                    b.Frustum(p+Vector3.up*(h+2.5f),p+Vector3.up*(h+4.2f),.68f,.035f,roof,16);
                    b.Frustum(p+Vector3.up*(h+4.1f),p+Vector3.up*(h+4.7f),.045f,.008f,Gold,8);
                    b.Arch(new Vector3(r*.78f,.4f,.1f),1.90f,3.4f,.34f,ribs,14,Quaternion.Euler(0,90,0));
                }
                return b.Finish("Organic arched fantasy sanctuary LOD"+lod);
            }));
        }
        private static void AncientTree(Transform root,Material foliage,Vector3 position)
        {
            Reserve(position.x,position.z,12,12);
            Lod(root,"Ancient guardian tree woven boughs and buttress roots",position,Vector3.one,7,foliage,lod=>Cache("ancient-tree-"+lod,()=>{
                var b=new MeshBuilder();var bark=P(.25f,.25f,.18f,2);var leaf=P(.18f,.38f,.31f,10);
                Curve(b,new[]{Vector3.zero,new Vector3(.45f,2,0),new Vector3(-.18f,4.5f,.25f),new Vector3(.55f,7,.1f),new Vector3(.1f,9.7f,.3f)},1.35f,bark,lod==0?24:14);
                b.Frustum(new Vector3(.1f,9.65f,.3f),new Vector3(.4f,11.1f,.3f),.83f,.035f,bark,lod==0?20:12);
                for(int spray=0;spray<7;spray++)
                {
                    float a=spray*2.39996f;var p=new Vector3(Mathf.Cos(a)*1.35f,10.6f+spray%3*.38f,Mathf.Sin(a)*1.3f);
                    b.Ellipsoid(p,new Vector3(2.2f,1.0f,1.7f),new Paint(leaf.Color*(.95f+spray%3*.065f),10),lod==0?14:9,lod==0?8:5,.22f,Quaternion.Euler(8,spray*47,14));
                }
                for(int arm=0;arm<10;arm++)
                {
                    float a=arm*2.39996f;var dir=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));float h=4.5f+arm%4*1.17f;
                    Curve(b,new[]{new Vector3(.15f,h,0),dir*2.5f+Vector3.up*(h+.1f),dir*4.4f+Vector3.up*(h+1.0f),dir*5.5f+Vector3.up*(h+2.8f)},.43f,bark,lod==0?14:9);
                    for(int spray=0;spray<(lod==0?6:3);spray++)
                    {
                        float k=spray*2.39996f;var c=dir*(4.8f+spray%2*.6f)+new Vector3(Mathf.Sin(k+a)*1.2f,h+2.4f+spray%3*.4f,Mathf.Cos(k+a)*1.25f);
                        b.Ellipsoid(c,new Vector3(2.25f,1.25f,1.8f),new Paint(leaf.Color*(.85f+spray%3*.11f),10),lod==0?14:9,lod==0?8:5,.23f,Quaternion.Euler(spray*7,a*Mathf.Rad2Deg,spray*9));
                    }
                    Curve(b,new[]{Vector3.up*2.0f,dir*1.75f+Vector3.up*.80f,dir*3.3f+Vector3.up*.12f,dir*5.0f-Vector3.up*.24f},.44f,bark,lod==0?12:8);
                }
                return b.Finish("Ancient tree asymmetric spreading boughs LOD"+lod);
            }));
        }
        private static void RootBridge(Transform root,Material material,Vector3 position)
        {
            var b=new MeshBuilder();var bark=P(.32f,.31f,.23f,2);var wood=P(.53f,.52f,.37f,2);
            for(int side=-1;side<=1;side+=2)
            {
                var points=new[]{new Vector3(-5,-.15f,side*1.5f),new Vector3(-3.5f,.8f,side*1.45f),new Vector3(-1.3f,1.47f,side*1.38f),new Vector3(1.1f,1.47f,side*1.38f),new Vector3(3.6f,.65f,side*1.45f),new Vector3(5.1f,-.15f,side*1.65f)};
                Curve(b,points,.34f,bark,14);
                var rail=new Vector3[points.Length];for(int i=0;i<points.Length;i++)rail[i]=points[i]+Vector3.up*.92f;Curve(b,rail,.12f,bark,10);
                for(int i=1;i<points.Length-1;i++)Curve(b,new[]{points[i],points[i]+Vector3.up*.4f+Vector3.right*.24f,rail[i]},.06f,bark,8);
            }
            for(int step=0;step<24;step++)
            {
                float x=-4.6f+step*.4f,y=.22f+Mathf.Sin((step+.5f)/24*Mathf.PI)*.92f;
                b.Box(new Vector3(x,y,0),new Vector3(.37f,.18f,2.9f),wood,.035f,Quaternion.Euler(0,Mathf.Sin(step*2.4f)*1.8f,0));
            }
            Emit(root,"Living root and carved bough river bridge",b.Finish("Woven root bridge"),material,position);
        }
        private static void CrystalCluster(Transform root,Material crystal,Material surface,Vector3 position,float scale,float yaw)
        {
            var stone=Cache("crystal-natural-plinth",()=>{var b=new MeshBuilder();b.Ellipsoid(Vector3.up*.19f,new Vector3(2.5f,.64f,2.0f),P(.35f,.41f,.36f,14),16,8,.24f);return b.Finish("Weathered crystal outcrop base");});
            Emit(root,"Natural crystal bearing rock",stone,surface,position,Vector3.one*scale,yaw);
            var mesh=Cache("grove-crystal-cluster",()=>{
                var b=new MeshBuilder();
                for(int gem=0;gem<7;gem++)
                {
                    float a=gem*2.39996f,r=gem==0?0:.54f;var p=new Vector3(Mathf.Cos(a)*r,.25f,Mathf.Sin(a)*r);float h=gem==0?2.8f:1.0f+(gem%3)*.40f,w=gem==0?.34f:.19f;
                    var axis=new Vector3(Mathf.Cos(a)*(gem==0?0:.21f),1,Mathf.Sin(a)*(gem==0?0:.21f));var q=Quaternion.FromToRotation(Vector3.up,axis);
                    var c=gem%2==0?P(.16f,.65f,.61f,15,.1f,.54f):P(.32f,.50f,.77f,15,.1f,.54f);
                    for(int face=0;face<6;face++)
                    {
                        float u=face*Mathf.PI/3,v=(face+1)*Mathf.PI/3;
                        Vector3 V(float angle,float height,float radius)=>p+q*new Vector3(Mathf.Cos(angle)*radius,height,Mathf.Sin(angle)*radius);
                        b.Quad(V(u,0,w*.85f),V(u,h*.75f,w),V(v,h*.75f,w),V(v,0,w*.85f),c);
                        b.Triangle(V(u,h*.75f,w),p+q*Vector3.up*h,V(v,h*.75f,w),c);
                    }
                }
                return b.Finish("Seven naturally inclined mineral crystals");
            });Emit(root,"Luminous mineral crystal cluster",mesh,crystal,position,Vector3.one*scale,yaw);
        }
    }
}
