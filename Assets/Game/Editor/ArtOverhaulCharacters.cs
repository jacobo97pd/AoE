using System;
using System.Collections.Generic;
using Emberfield.Presentation;
using UnityEngine;

namespace Emberfield.Editor
{
    public static partial class ArtOverhaulCharacters
    {
        private const int Root=0,Hips=1,Spine=2,Head=3,UpperArmL=4,LowerArmL=5,HandL=6,UpperArmR=7,LowerArmR=8,HandR=9,UpperLegL=10,LowerLegL=11,FootL=12,UpperLegR=13,LowerLegR=14,FootR=15;
        private static readonly Paint Navy=new Paint(.105f,.16f,.23f,tile:0), Wine=new Paint(.40f,.085f,.07f,tile:0);
        private static readonly Paint Moss=new Paint(.20f,.30f,.13f,tile:0), Leaf=new Paint(.34f,.43f,.15f,tile:10);
        private static readonly Paint Fur=new Paint(.62f,.56f,.43f,tile:0), DarkFur=new Paint(.29f,.26f,.22f,tile:0);
        private static readonly Paint Bronze=new Paint(.49f,.32f,.12f,.72f,.46f,tile:3), Crystal=new Paint(.28f,.68f,.77f,.36f,.7f,tile:15);

        public static ArtStylePrototypeGeometry.Prototype Build(ArtUnitKind kind,ArtBiome biome,int lod)
        {
            if(lod<0||lod>2)throw new ArgumentOutOfRangeException(nameof(lod));
            if(!Enum.IsDefined(typeof(ArtUnitKind),kind)||!Enum.IsDefined(typeof(ArtBiome),biome))throw new ArgumentOutOfRangeException();
            if(kind==ArtUnitKind.Dragon)return BuildDragon(biome,lod);
            var b=new Builder(lod);var bones=HumanPose(kind);var names=HumanNames();var parents=HumanParents();
            bool plated=kind==ArtUnitKind.Warrior||kind==ArtUnitKind.Cavalry||kind==ArtUnitKind.Hero||kind==ArtUnitKind.Dwarf;
            Body(b,bones,plated&&biome!=ArtBiome.Caribbean);
            Face(b,kind!=ArtUnitKind.Worker,kind==ArtUnitKind.Elf);
            if(kind==ArtUnitKind.Worker)
            {
                if(biome==ArtBiome.Kingdom){Worker(b,bones);WorkerDetails(b);}
                else TravellingWorker(b,bones,biome);
            }
            else if(kind==ArtUnitKind.Pirate||(kind==ArtUnitKind.Hero&&biome==ArtBiome.Caribbean))PirateOutfit(b,bones,kind==ArtUnitKind.Hero);
            else if(kind==ArtUnitKind.Dwarf)DwarfOutfit(b,bones);
            else if(kind==ArtUnitKind.Elf)ElfOutfit(b,bones);
            else if(kind==ArtUnitKind.MountainWarrior)MountainOutfit(b,bones);
            else if(biome==ArtBiome.Desert)DesertOutfit(b,bones,kind==ArtUnitKind.Hero);
            else
            {
                Warrior(b,bones,kind==ArtUnitKind.Hero);
                Cape(b,kind==ArtUnitKind.Hero?.56f:.35f,kind==ArtUnitKind.Hero?.19f:.68f,Team,kind==ArtUnitKind.Hero?Wine:Navy);
                LayeredArmor(b,bones,kind==ArtUnitKind.Hero);
                if(kind==ArtUnitKind.Hero)HeroDetails(b,biome);
                if(biome==ArtBiome.Fantasy)FantasyDetails(b);
                if(biome==ArtBiome.Caribbean)NavalDetails(b);
            }
            if(kind==ArtUnitKind.Cavalry)
            {
                var mounted=MountedPose(bones);b.RemapRider(bones,mounted);bones=mounted;
                ExtendMountRig(ref names,ref bones,ref parents);Horse(b,bones,biome);
            }
            var mesh=b.Finish("Overhaul "+biome+" "+kind,bones);
            if(kind==ArtUnitKind.Dwarf||kind==ArtUnitKind.Elf||kind==ArtUnitKind.MountainWarrior||kind==ArtUnitKind.Hero)RemapProportions(mesh,bones,kind);
            if(kind==ArtUnitKind.Worker||kind==ArtUnitKind.MountainWarrior)EnlargeHead(mesh,bones,kind==ArtUnitKind.Worker?1.14f:1.10f);
            return new ArtStylePrototypeGeometry.Prototype{Mesh=mesh,BoneNames=names,BonePositions=bones,BoneParents=parents};
        }

        private static string[] HumanNames()=>new[]{"Root","Hips","Spine","Head","UpperArmL","LowerArmL","HandL","UpperArmR","LowerArmR","HandR","UpperLegL","LowerLegL","FootL","UpperLegR","LowerLegR","FootR"};
        private static int[] HumanParents()=>new[]{-1,0,1,2,2,4,5,2,7,8,1,10,11,1,13,14};
        private static Vector3[] HumanPose(ArtUnitKind kind)
        {
            var pose=new[] {
            Vector3.zero,new Vector3(0,1.02f,0),new Vector3(0,1.35f,0),new Vector3(0,1.74f,.025f),
            new Vector3(-.33f,1.58f,0),new Vector3(-.46f,1.25f,.045f),new Vector3(-.43f,1.20f,.30f),
            new Vector3(.33f,1.58f,0),new Vector3(.47f,1.26f,.035f),new Vector3(.535f,1.20f,.205f),
            new Vector3(-.145f,.99f,0),new Vector3(-.17f,.59f,.025f),new Vector3(-.18f,.13f,.03f),
            new Vector3(.145f,.99f,-.025f),new Vector3(.17f,.59f,-.015f),new Vector3(.18f,.13f,-.055f)
            };
            if(kind==ArtUnitKind.Hero)
            {
                pose[LowerArmL]=new Vector3(-.51f,1.38f,.02f);pose[HandL]=new Vector3(-.66f,1.39f,.16f);
                pose[LowerArmR]=new Vector3(.49f,1.18f,.06f);pose[HandR]=new Vector3(.59f,1.08f,.19f);
            }
            return pose;
        }
        private static Vector3[] MountedPose(Vector3[] source)
        {
            var result=(Vector3[])source.Clone();for(int i=1;i<result.Length;i++)result[i]+=Vector3.up*1.02f;
            result[UpperLegL]=new Vector3(-.22f,1.98f,0);result[LowerLegL]=new Vector3(-.47f,1.57f,.21f);result[FootL]=new Vector3(-.49f,1.04f,.25f);
            result[UpperLegR]=new Vector3(.22f,1.98f,0);result[LowerLegR]=new Vector3(.47f,1.57f,.17f);result[FootR]=new Vector3(.49f,1.04f,.21f);return result;
        }
        private static void RemapProportions(Mesh mesh,Vector3[] bones,ArtUnitKind kind)
        {
            Vector3 Point(Vector3 p)
            {
                if(kind==ArtUnitKind.Dwarf)
                {
                    float y=p.y<1?p.y*.58f:p.y<1.65f?.58f+(p.y-1)*.82f:1.113f+(p.y-1.65f)*.92f;
                    float width=p.y<1.65f?1.30f:Mathf.Lerp(1.30f,1.07f,Mathf.Clamp01((p.y-1.65f)/.15f));
                    return new Vector3(p.x*width,y,p.z*1.16f);
                }
                return Vector3.Scale(p,kind==ArtUnitKind.Elf?new Vector3(.85f,1.06f,.90f):kind==ArtUnitKind.Hero?new Vector3(1.14f,1.065f,1.10f):new Vector3(1.17f,1.01f,1.11f));
            }
            var positions=mesh.vertices;var normals=mesh.normals;
            for(int i=0;i<positions.Length;i++)
            {
                var p=positions[i];var n=normals[i];var tangent=Vector3.Cross(n,Mathf.Abs(n.y)<.9f?Vector3.up:Vector3.right).normalized;
                var bitangent=Vector3.Cross(n,tangent);var a=Point(p+tangent*.001f)-Point(p-tangent*.001f);var c=Point(p+bitangent*.001f)-Point(p-bitangent*.001f);
                var transformed=Vector3.Cross(a,c);float squared=transformed.sqrMagnitude;
                normals[i]=squared>1e-24f?transformed/Mathf.Sqrt(squared):n;positions[i]=Point(p);
            }
            for(int i=0;i<bones.Length;i++)bones[i]=Point(bones[i]);var binds=new Matrix4x4[bones.Length];for(int i=0;i<binds.Length;i++)binds[i]=Matrix4x4.Translate(-bones[i]);
            mesh.vertices=positions;mesh.normals=normals;mesh.bindposes=binds;mesh.RecalculateBounds();
        }
        private static void EnlargeHead(Mesh mesh,Vector3[] bones,float scale)
        {
            var positions=mesh.vertices;var weights=mesh.boneWeights;var pivot=bones[Head];
            for(int i=0;i<positions.Length;i++)if(weights[i].boneIndex0==Head)
            {
                // Keep the neck seam fixed while giving the face, ears and hat a readable RTS proportion.
                float influence=Mathf.SmoothStep(0,1,Mathf.InverseLerp(pivot.y-.10f,pivot.y+.06f,positions[i].y));
                positions[i]=pivot+(positions[i]-pivot)*Mathf.Lerp(1,scale,influence);
            }
            mesh.vertices=positions;mesh.RecalculateBounds();
        }

        private static void Cape(Builder b,float width,float bottom,Paint outside,Paint lining)
        {
            Vector3 Shape(float u,float v)
            {
                float x=(u-.5f)*2*width*Mathf.Lerp(1.13f,.69f,v);float y=Mathf.Lerp(bottom,1.625f,v);
                float wave=Mathf.Sin(u*Mathf.PI*5+.4f)*.047f*(1-v*.65f);
                return new Vector3(x,y+Mathf.Sin(u*Mathf.PI*3)*.028f*(1-v),-.245f-(1-v)*.16f-wave);
            }
            int columns=b.Lod==0?12:b.Lod==1?8:4,rows=b.Lod==0?9:b.Lod==1?6:3;
            b.Sheet(Shape,columns,rows,outside,Spine,Vector3.back,false);
            b.Sheet((u,v)=>Shape(u,v)+Vector3.forward*.009f,columns,rows,lining,Spine,Vector3.forward,false);
            b.Sheet((u,v)=>Shape(u,v*.024f/(1.625f-bottom))+Vector3.back*.005f,columns,1,Gold,Spine,Vector3.back,false);
            for(int side=-1;side<=1;side+=2)
                b.Ribbon(new[]{new Vector3(side*.242f,1.60f,-.14f),new Vector3(side*.18f,1.606f,.09f),new Vector3(side*.12f,1.527f,.192f)},.045f,.009f,Gold,Spine);
        }
        private static void LayeredArmor(Builder b,Vector3[] bones,bool hero)
        {
            for(int side=-1;side<=1;side+=2)
            {
                int bone=side<0?UpperArmL:UpperArmR;var shoulder=bones[bone];
                for(int layer=0;layer<(b.Lod==2?1:3);layer++)
                {
                    float y=1.486f-layer*.047f;
                    b.Plate(new[]{new Vector2(-.107f,.049f),new Vector2(-.13f,-.035f),new Vector2(0,-.071f),new Vector2(.13f,-.035f),new Vector2(.107f,.049f)},
                        new Vector3(shoulder.x+side*.074f,y,.14f-layer*.008f),Quaternion.Euler(0,side*37,side*8),.023f,.010f,.021f,Steel,hero?Gold:SteelEdge,bone);
                }
                for(int plate=0;plate<(b.Lod==2?1:3);plate++)
                    b.Plate(new[]{new Vector2(-.067f,.034f),new Vector2(-.077f,-.033f),new Vector2(.074f,-.038f),new Vector2(.067f,.034f)},
                        new Vector3(side*.168f,.845f-plate*.063f,.116f),Quaternion.Euler(0,side*12,0),.016f,.008f,.012f,Steel,Gold,side<0?UpperLegL:UpperLegR);
            }
            if(b.Lod<2)
            {
                b.Ribbon(new[]{new Vector3(-.154f,1.58f,.123f),new Vector3(-.088f,1.528f,.225f),new Vector3(0,1.50f,.240f),new Vector3(.088f,1.528f,.225f),new Vector3(.154f,1.58f,.123f)},.014f,.005f,Gold,Spine);
            }
        }
        private static void WorkerDetails(Builder b)
        {
            // Gathered wheat is grouped into a few broad seed heads; no individual hair-like strands.
            int stalks=b.Lod==0?7:b.Lod==1?5:3;
            for(int i=0;i<stalks;i++)
            {
                float x=-.17f+i*.052f;float y=1.61f+(i%3)*.055f;
                b.Curve(new[]{new Vector3(x*.7f,1.10f,-.365f),new Vector3(x,1.44f,-.41f),new Vector3(x*1.2f,y,-.43f)},.007f,6,Straw,Spine);
                b.Limb(new Vector3(x*1.2f,y-.03f,-.43f),new Vector3(x*1.28f,y+.14f,-.46f),new[]{new Vector2(.026f,.023f),new Vector2(.035f,.03f),new Vector2(.002f,.003f)},8,StrawLight,Spine,.10f);
            }
            for(int side=-1;side<=1;side+=2)
            {
                b.Plate(new[]{new Vector2(-.025f,.08f),new Vector2(-.082f,.012f),new Vector2(.01f,-.066f),new Vector2(.047f,.056f)},new Vector3(side*.092f,1.584f,.125f),Quaternion.Euler(0,side*15,side*-18),.009f,.003f,.012f,Ivory,Linen,Spine);
                if(b.Lod<2)b.Ribbon(new[]{new Vector3(side*.083f,.525f,.100f),new Vector3(side*.154f,.56f,.141f),new Vector3(side*.239f,.533f,.103f)},.024f,.005f,LeatherLight,side<0?LowerLegL:LowerLegR);
            }
        }
        private static void WrappedHead(Builder b,bool desert,bool hero=false)
        {
            var fabric=desert?Ivory:Wine;
            b.Profile(Vector3.zero,Quaternion.identity,new[]{new Ring(1.97f,.17f,.15f),new Ring(2.04f,.19f,.163f),new Ring(2.10f,.164f,.147f),new Ring(2.154f,.084f,.09f)},20,fabric,Head,.025f);
            for(int i=0;i<(b.Lod==2?1:3);i++)
            {
                int band=b.Lod==2?1:i;float y=1.995f+band*.039f;
                float width=band==1?.203f:band==0?.195f:.194f,depth=band==1?.175f:band==0?.170f:.164f;
                b.Profile(new Vector3(0,y,0),Quaternion.Euler(0,0,band%2==0?2:-2),new[]{new Ring(0,width,depth),new Ring(.025f,width-.003f,depth-.002f)},20,band==1?Team:fabric,Head);
            }
            b.Sheet((u,v)=>new Vector3((u-.5f)*.22f,Mathf.Lerp(1.39f,1.99f,v),-.145f-.08f*(1-v)+Mathf.Sin(v*6+u*3)*.025f),b.Lod==2?2:4,b.Lod==0?6:3,fabric,Head,Vector3.back,true);
            if(hero)b.Oval(new Vector3(0,2.05f,.168f),new Vector3(.036f,.047f,.015f),10,Gold,Head);
        }
        private static void LongCoat(Builder b,Paint coat,Paint lining,bool hero)
        {
            ClothPanel(b,-.274f,-.045f,.57f,1.53f,coat,Spine,false,.034f,Gold);
            ClothPanel(b,.045f,.274f,.57f,1.53f,coat,Spine,false,.034f,Gold);
            Cape(b,.31f,hero?.29f:.54f,coat,lining);
            for(int side=-1;side<=1;side+=2)
            {
                b.Plate(new[]{new Vector2(-.033f,.16f),new Vector2(-.07f,-.03f),new Vector2(.011f,-.11f),new Vector2(.056f,.14f)},new Vector3(side*.112f,1.43f,.206f),Quaternion.Euler(0,side*17,side*17),.012f,.005f,.007f,lining,Gold,Spine);
                for(int button=0;button<(b.Lod==0?4:b.Lod==1?2:0);button++)b.Oval(new Vector3(side*.068f,1.31f-button*.105f,.238f),new Vector3(.011f,.012f,.008f),6,Gold,Spine);
            }
        }
        private static void Sash(Builder b,Paint paint)
        {
            b.Profile(Vector3.zero,Quaternion.identity,new[]{new Ring(1.00f,.263f,.207f),new Ring(1.046f,.278f,.223f),new Ring(1.119f,.254f,.205f)},18,paint,Hips,.025f);
            b.Sheet((u,v)=>new Vector3(.115f+u*.12f+.04f*(1-v),.60f+v*.45f,.202f+Mathf.Sin(v*7+u*2)*.018f),b.Lod==2?2:4,b.Lod==0?6:3,paint,Hips,Vector3.forward,true);
            Belt(b,false);
        }
        private static void Scimitar(Builder b,Vector3 hand,bool ornate,int bone=HandR)
        {
            var origin=hand+new Vector3(.03f,-.10f,.055f);
            b.Limb(origin,origin+Vector3.up*.18f,new[]{new Vector2(.029f,.03f),new Vector2(.030f,.03f),new Vector2(.025f,.026f)},10,Leather,bone);
            b.Blade(new[]{origin+new Vector3(0,.17f,0),origin+new Vector3(.04f,.35f,0),origin+new Vector3(.125f,.56f,-.014f),origin+new Vector3(.275f,.78f,-.03f),origin+new Vector3(.38f,.90f,-.025f)},new[]{.035f,.075f,.084f,.060f,.001f},.026f,SteelEdge,bone);
            b.Curve(new[]{origin+new Vector3(-.11f,.17f,0),origin+new Vector3(.01f,.14f,.025f),origin+new Vector3(.12f,.18f,.015f)},.021f,8,Gold,bone);
            if(ornate)b.Curve(new[]{origin+new Vector3(.11f,.18f,0),origin+new Vector3(.15f,.04f,.07f),origin+new Vector3(.04f,-.015f,.07f),origin},.013f,8,Gold,bone);
        }
        private static void RoundShield(Builder b,Vector3 centre,Paint front,int bone=HandL,bool rune=false)
        {
            var rotation=Quaternion.Euler(0,-12,-6);b.Plate(EllipseOutline(.325f,.367f,b.Lod==0?20:12),centre,rotation,.025f,.026f,.065f,front,Gold,bone);
            b.Oval(centre+rotation*new Vector3(0,0,.10f),new Vector3(.071f,.071f,.041f),12,Bronze,bone);
            if(rune)
            {
                foreach(int side in new[]{-1,1})b.Ribbon(new[]{centre+new Vector3(side*.05f,.16f,.11f),centre+new Vector3(side*.17f,.04f,.11f),centre+new Vector3(side*.05f,-.12f,.11f)},.030f,.006f,Gold,bone);
            }
        }
        private static void TravellingWorker(Builder b,Vector3[] bones,ArtBiome biome)
        {
            bool desert=biome==ArtBiome.Desert;WrappedHead(b,desert);
            ClothPanel(b,-.24f,-.025f,.73f,1.53f,desert?Ivory:biome==ArtBiome.Fantasy?Moss:Navy,Spine,false,.032f,Gold);
            ClothPanel(b,.025f,.24f,.73f,1.53f,Team,Spine,false,.032f,Ivory);Sash(b,desert?Wine:Team);
            Backpack(b,desert?Linen:LeatherLight,true);Pouch(b,new Vector3(-.28f,.94f,.10f),Quaternion.Euler(0,-20,0),Hips);
            var hand=bones[HandR]+new Vector3(.008f,0,.027f);
            b.Limb(hand+Vector3.down*.85f,hand+Vector3.up*.62f,new[]{new Vector2(.035f,.035f),new Vector2(.036f,.036f)},10,Wood,HandR);
            b.Blade(new[]{hand+new Vector3(-.28f,.57f,0),hand+new Vector3(-.11f,.66f,0),hand+new Vector3(.14f,.65f,0),hand+new Vector3(.32f,.51f,0)},new[]{.008f,.065f,.066f,.006f},.043f,Steel,HandR);
            if(biome==ArtBiome.Fantasy)LeafMantle(b,.30f);
        }
        private static void Backpack(Builder b,Paint material,bool rolled)
        {
            b.Profile(new Vector3(0,0,-.27f),Quaternion.identity,new[]{new Ring(.93f,.17f,.09f),new Ring(1.0f,.24f,.16f),new Ring(1.32f,.24f,.17f),new Ring(1.45f,.18f,.13f)},16,material,Spine,.035f);
            for(int side=-1;side<=1;side+=2)
            {
                b.Ribbon(new[]{new Vector3(side*.17f,1.47f,-.26f),new Vector3(side*.2f,1.59f,.02f),new Vector3(side*.16f,1.11f,.20f)},.042f,.014f,Leather,Spine);
                b.Ribbon(new[]{new Vector3(side*.16f,.96f,-.39f),new Vector3(side*.17f,1.25f,-.45f),new Vector3(side*.16f,1.43f,-.39f)},.03f,.013f,LeatherDark,Spine);
            }
            if(rolled)b.Limb(new Vector3(-.27f,1.48f,-.3f),new Vector3(.27f,1.48f,-.3f),new[]{new Vector2(.072f,.072f),new Vector2(.108f,.108f),new Vector2(.105f,.105f),new Vector2(.076f,.076f)},14,Team,Spine);
        }
        private static void Tricorn(Builder b,bool hero)
        {
            b.Profile(Vector3.zero,Quaternion.identity,new[]{new Ring(1.965f,.16f,.14f),new Ring(2.04f,.174f,.15f),new Ring(2.11f,.14f,.123f),new Ring(2.15f,.067f,.069f)},20,LeatherDark,Head);
            b.Sheet((u,v)=>
            {
                float angle=u*Mathf.PI*2;float r=Mathf.Lerp(.165f,.38f,v);float lift=Mathf.Pow((1+Mathf.Cos(angle*3+.55f))*.5f,3)*.15f*v;
                return new Vector3(Mathf.Sin(angle)*r,1.992f+lift,Mathf.Cos(angle)*r*.8f);
            },b.Lod==0?36:b.Lod==1?24:12,b.Lod==0?4:2,Navy,Head,Vector3.up,true);
            if(b.Lod<2)
            {
                b.Curve(new[]{new Vector3(-.13f,2.075f,-.08f),new Vector3(-.25f,2.29f,-.17f),new Vector3(-.44f,2.31f,-.23f),new Vector3(-.51f,2.26f,-.28f)},hero?.025f:.018f,8,Wine,Head);
                b.Blade(new[]{new Vector3(-.13f,2.075f,-.08f),new Vector3(-.29f,2.25f,-.17f),new Vector3(-.45f,2.30f,-.24f),new Vector3(-.53f,2.25f,-.29f)},new[]{.015f,.067f,.071f,.004f},.014f,Wine,Head);
            }
        }
        private static void PirateOutfit(Builder b,Vector3[] bones,bool hero)
        {
            LongCoat(b,hero?Wine:Navy,hero?Navy:Wine,hero);Sash(b,Team);Tricorn(b,hero);
            Scimitar(b,bones[HandR],true);Pouch(b,new Vector3(-.27f,.96f,.10f),Quaternion.Euler(0,-28,0),Hips);
            b.Limb(new Vector3(-.31f,.90f,.17f),new Vector3(-.35f,1.10f,.21f),new[]{new Vector2(.04f,.027f),new Vector2(.045f,.027f)},10,Wood,Hips);
            b.Limb(new Vector3(-.345f,1.07f,.20f),new Vector3(-.40f,1.37f,.19f),new[]{new Vector2(.025f,.025f),new Vector2(.027f,.027f)},10,SteelDark,Hips);
            if(b.Lod<2)for(int ring=0;ring<3;ring++)b.EllipseCurve(new Vector3(.29f,.83f-ring*.018f,-.08f),.12f,.06f,.012f,18,Straw,Hips);
        }
        private static void DesertOutfit(Builder b,Vector3[] bones,bool hero)
        {
            WrappedHead(b,true,hero);LongCoat(b,Ivory,Wine,hero);Sash(b,Wine);Scimitar(b,bones[HandR],hero);RoundShield(b,bones[HandL]+new Vector3(-.03f,0,.095f),Bronze);
            for(int side=-1;side<=1;side+=2)
            {
                int upper=side<0?UpperArmL:UpperArmR;
                b.Plate(new[]{new Vector2(-.13f,.10f),new Vector2(-.17f,-.055f),new Vector2(0,-.13f),new Vector2(.17f,-.055f),new Vector2(.13f,.10f)},new Vector3(side*.36f,1.51f,.098f),Quaternion.Euler(0,side*31,side*8),.03f,.016f,.035f,Bronze,Gold,upper);
                if(b.Lod<2)for(int row=0;row<3;row++)b.Plate(EllipseOutline(.063f,.043f,8),new Vector3(side*.19f,1.41f-row*.08f,.18f),Quaternion.Euler(0,side*16,0),.01f,.006f,.012f,Bronze,Gold,Spine);
            }
        }
        private static void FurCollar(Builder b,float width,Paint paint)
        {
            b.Profile(new Vector3(0,0,-.025f),Quaternion.identity,new[]{
                new Ring(1.515f,width*.92f,.185f),new Ring(1.561f,width*1.09f,.239f),
                new Ring(1.638f,width*1.03f,.225f),new Ring(1.697f,width*.68f,.155f),new Ring(1.708f,.12f,.10f)
            },24,paint,Spine,.027f);
            int count=b.Lod==0?14:b.Lod==1?9:5;
            for(int i=0;i<count;i++)
            {
                float a=i*Mathf.PI*2/count;var centre=new Vector3(Mathf.Sin(a)*width,1.55f+(i%3)*.014f,Mathf.Cos(a)*.204f-.025f);
                b.Oval(centre,new Vector3(.067f,.044f,.064f),8,paint,Spine);
            }
        }
        private static void DwarfOutfit(Builder b,Vector3[] bones)
        {
            LongCoat(b,Team,LeatherDark,false);LayeredArmor(b,bones,true);FurCollar(b,.32f,Fur);Belt(b,true);Helmet(b);
            for(int braid=-1;braid<=1;braid++)
            {
                var top=new Vector3(braid*.072f,1.78f,.16f);var end=new Vector3(braid*.065f,1.29f-Mathf.Abs(braid)*.05f,.245f);
                b.Limb(top,end,new[]{new Vector2(.057f,.054f),new Vector2(.064f,.053f),new Vector2(.053f,.045f),new Vector2(.026f,.023f)},12,HairLight,Head,.13f);
                b.Limb(Vector3.Lerp(top,end,.79f),Vector3.Lerp(top,end,.88f),new[]{new Vector2(.052f,.044f),new Vector2(.047f,.038f)},10,Gold,Head);
            }
            RoundShield(b,new Vector3(-.46f,1.17f,.42f),Team,HandL,true);
            var hand=bones[HandR];b.Limb(hand+Vector3.down*.60f,hand+Vector3.up*.45f,new[]{new Vector2(.041f,.037f),new Vector2(.042f,.038f)},12,Wood,HandR);
            b.Profile(hand+new Vector3(-.28f,.43f,0),Quaternion.Euler(0,0,-90),new[]{new Ring(0,.123f,.13f),new Ring(.052f,.154f,.15f),new Ring(.16f,.137f,.14f),new Ring(.47f,.137f,.14f),new Ring(.53f,.16f,.157f),new Ring(.58f,.124f,.133f)},10,SteelDark,HandR);
            b.Plate(new[]{new Vector2(0,.10f),new Vector2(-.083f,0),new Vector2(0,-.10f),new Vector2(.083f,0)},hand+new Vector3(.04f,.43f,.16f),Quaternion.identity,.012f,.01f,.012f,Crystal,Gold,HandR);
        }
        private static void LeafMantle(Builder b,float width)
        {
            int count=b.Lod==0?12:b.Lod==1?8:5;
            for(int i=0;i<count;i++)
            {
                float a=i*Mathf.PI*2/count;var centre=new Vector3(Mathf.Sin(a)*width,1.50f,Mathf.Cos(a)*.19f);
                b.Plate(new[]{new Vector2(0,.12f),new Vector2(-.065f,.025f),new Vector2(-.045f,-.072f),new Vector2(0,-.18f),new Vector2(.045f,-.072f),new Vector2(.065f,.025f)},centre,Quaternion.Euler(10,a*Mathf.Rad2Deg,0),.009f,.008f,.021f,i%2==0?Leaf:Moss,Gold,Spine);
            }
        }
        private static void ElfOutfit(Builder b,Vector3[] bones)
        {
            LongCoat(b,Moss,Team,false);LeafMantle(b,.27f);Belt(b,false);
            for(int side=-1;side<=1;side+=2)
            {
                b.Blade(new[]{new Vector3(side*.147f,1.86f,.019f),new Vector3(side*.23f,1.914f,-.015f),new Vector3(side*.288f,1.95f,-.043f)},new[]{.040f,.026f,.001f},.032f,Skin,Head);
                b.Curve(new[]{new Vector3(side*.128f,2.007f,.025f),new Vector3(side*.161f,1.82f,-.061f),new Vector3(side*.136f,1.55f,-.087f)},.046f,10,StrawLight,Head);
            }
            b.Profile(Vector3.zero,Quaternion.identity,new[]{new Ring(1.972f,.16f,.137f),new Ring(2.04f,.136f,.13f),new Ring(2.065f,.044f,.048f)},18,StrawLight,Head);
            b.Ribbon(new[]{new Vector3(-.135f,1.969f,.084f),new Vector3(0,1.968f,.166f),new Vector3(.135f,1.969f,.084f)},.019f,.006f,Gold,Head);
            var hand=bones[HandL]+new Vector3(0,0,.055f);var a=hand+new Vector3(-.015f,-.71f,-.17f);var c=hand;var d=hand+new Vector3(.008f,.78f,-.18f);
            b.Curve(new[]{a,Vector3.Lerp(a,c,.45f)+new Vector3(-.055f,0,.05f),c,Vector3.Lerp(c,d,.60f)+new Vector3(-.05f,0,.04f),d},.031f,12,Wood,HandL);
            b.Curve(new[]{a,hand+new Vector3(.025f,0,-.175f),d},.005f,6,Ivory,HandL);
            Backpack(b,Leather,false);
            if(b.Lod<2)for(int arrow=0;arrow<5;arrow++)
            {
                float x=.05f+arrow*.034f;b.Limb(new Vector3(x,1.30f,-.33f),new Vector3(x+.06f,1.78f,-.38f),new[]{new Vector2(.006f,.006f),new Vector2(.006f,.006f)},6,Wood,Spine);
                b.Blade(new[]{new Vector3(x+.055f,1.68f,-.38f),new Vector3(x+.06f,1.79f,-.38f)},new[]{.026f,.011f},.004f,Ivory,Spine);
            }
        }
        private static void MountainOutfit(Builder b,Vector3[] bones)
        {
            LongCoat(b,Navy,DarkFur,false);FurCollar(b,.37f,Fur);Backpack(b,Leather,true);Sash(b,Team);
            b.Profile(Vector3.zero,Quaternion.identity,new[]{new Ring(1.92f,.19f,.16f),new Ring(2.015f,.183f,.164f),new Ring(2.112f,.12f,.11f),new Ring(2.153f,.06f,.057f)},18,Navy,Head);
            if(b.Lod<2)for(int i=0;i<9;i++)
            {
                float a=(i/8f-.5f)*Mathf.PI;var from=new Vector3(Mathf.Sin(a)*.18f,1.90f+Mathf.Cos(a)*.10f,.12f);
                b.Oval(from+new Vector3(0,-.01f,.011f),new Vector3(.039f,.043f,.037f),8,Fur,Head);
            }
            var hand=bones[HandR];b.Limb(hand+Vector3.down*.73f,hand+Vector3.up*.46f,new[]{new Vector2(.035f,.035f),new Vector2(.035f,.034f)},10,Wood,HandR);
            b.Blade(new[]{hand+new Vector3(.02f,.37f,0),hand+new Vector3(.17f,.46f,0),hand+new Vector3(.33f,.43f,0),hand+new Vector3(.35f,.19f,0),hand+new Vector3(.15f,.25f,0)},new[]{.052f,.098f,.107f,.07f,.024f},.045f,Steel,HandR);
            for(int ring=0;ring<(b.Lod==2?1:3);ring++)b.EllipseCurve(new Vector3(-.31f,.89f-ring*.025f,-.12f),.145f,.055f,.014f,18,Straw,Hips);
        }
        private static void HeroDetails(Builder b,ArtBiome biome)
        {
            FurCollar(b,.36f,Fur);
            b.Profile(Vector3.zero,Quaternion.identity,new[]{new Ring(1.948f,.164f,.144f,.006f),new Ring(1.992f,.158f,.143f),new Ring(2.045f,.115f,.105f),new Ring(2.074f,.042f,.046f)},20,HairLight,Head);
            for(int side=-1;side<=1;side+=2)b.TaperCurve(new[]{new Vector3(side*.142f,1.968f,-.041f),new Vector3(side*.167f,1.85f,-.059f),new Vector3(side*.14f,1.736f,-.091f)},new[]{new Vector2(.035f,.048f),new Vector2(.043f,.052f),new Vector2(.015f,.022f)},10,Hair,Head);
            b.Profile(Vector3.zero,Quaternion.identity,new[]{new Ring(1.988f,.169f,.155f),new Ring(2.035f,.169f,.155f)},20,Gold,Head);
            for(int i=0;i<(b.Lod==2?3:5);i++)
            {
                float a=-1.2f+i*2.4f/(b.Lod==2?2:4);var basePoint=new Vector3(Mathf.Sin(a)*.170f,2.027f,Mathf.Cos(a)*.157f);
                b.Blade(new[]{basePoint,basePoint+new Vector3(Mathf.Sin(a)*.012f,.13f+(i%2)*.025f,Mathf.Cos(a)*.009f)},new[]{.028f,.001f},.016f,Gold,Head);
            }
            if(b.Lod<2)b.Oval(new Vector3(0,1.58f,.178f),new Vector3(.039f,.043f,.018f),12,Crystal,Spine);
        }
        private static void FantasyDetails(Builder b){LeafMantle(b,.30f);b.Oval(new Vector3(0,1.64f,.114f),new Vector3(.036f,.042f,.028f),10,Crystal,Spine);}
        private static void NavalDetails(Builder b){Sash(b,Wine);b.Ribbon(new[]{new Vector3(-.25f,1.57f,.13f),new Vector3(.23f,1.10f,.17f)},.055f,.012f,Leather,Spine);}
        private static void ExtendMountRig(ref string[] names,ref Vector3[] bones,ref int[] parents)
        {
            Array.Resize(ref names,28);Array.Resize(ref bones,28);Array.Resize(ref parents,28);
            var extraNames=new[]{"MountRoot","MountNeck","MountHead","MountFrontUpperL","MountFrontLowerL","MountFrontUpperR","MountFrontLowerR","MountRearUpperL","MountRearLowerL","MountRearUpperR","MountRearLowerR","MountTail"};
            var extraPositions=new[]{new Vector3(0,1.30f,0),new Vector3(0,1.63f,.61f),new Vector3(0,2.10f,1.06f),new Vector3(-.285f,1.31f,.59f),new Vector3(-.295f,.65f,.66f),new Vector3(.285f,1.31f,.59f),new Vector3(.295f,.65f,.66f),new Vector3(-.285f,1.29f,-.59f),new Vector3(-.30f,.68f,-.73f),new Vector3(.285f,1.29f,-.59f),new Vector3(.30f,.68f,-.73f),new Vector3(0,1.38f,-.91f)};
            var extraParents=new[]{0,16,17,16,19,16,21,16,23,16,25,16};
            for(int i=0;i<extraNames.Length;i++){names[i+16]=extraNames[i];bones[i+16]=extraPositions[i];parents[i+16]=extraParents[i];}
        }
        private static void Horse(Builder b,Vector3[] bones,ArtBiome biome)
        {
            var coat=biome==ArtBiome.Desert?new Paint(.58f,.40f,.21f,tile:1):biome==ArtBiome.Fantasy?new Paint(.63f,.66f,.64f,tile:1):new Paint(.24f,.105f,.048f,tile:1);
            var mane=biome==ArtBiome.Fantasy?Ivory:Hair;var hooves=new Paint(.085f,.075f,.064f,tile:1);
            b.Profile(new Vector3(0,1.30f,-.92f),Quaternion.Euler(90,0,0),new[]{new Ring(0,.17f,.24f),new Ring(.24f,.35f,.38f),new Ring(.66f,.393f,.413f),new Ring(1.10f,.37f,.42f),new Ring(1.48f,.313f,.38f),new Ring(1.70f,.214f,.29f)},24,coat,16);
            b.Profile(Vector3.zero,Quaternion.identity,new[]{new Ring(1.38f,.242f,.28f,.57f),new Ring(1.65f,.225f,.26f,.73f),new Ring(1.87f,.188f,.223f,.91f),new Ring(2.09f,.125f,.16f,1.06f),new Ring(2.18f,.10f,.12f,1.07f)},22,coat,17);
            b.Limb(new Vector3(0,2.13f,1.01f),new Vector3(0,1.949f,1.496f),new[]{new Vector2(.112f,.148f),new Vector2(.148f,.159f),new Vector2(.131f,.127f),new Vector2(.106f,.090f),new Vector2(.095f,.072f)},20,coat,18);
            b.Oval(new Vector3(0,1.96f,1.48f),new Vector3(.106f,.069f,.097f),16,LeatherDark,18);
            for(int side=-1;side<=1;side+=2)
            {
                b.TaperCurve(new[]{new Vector3(side*.084f,2.16f,1.035f),new Vector3(side*.11f,2.34f,1.01f),new Vector3(side*.091f,2.419f,.99f)},new[]{new Vector2(.044f,.049f),new Vector2(.032f,.043f),new Vector2(.006f,.008f)},12,coat,18);
                b.Plate(EllipseOutline(.027f,.022f,10),new Vector3(side*.126f,2.104f,1.225f),Quaternion.Euler(0,side*69,0),.005f,.004f,.005f,Iris,LeatherDark,18);
                b.Ribbon(new[]{new Vector3(side*.086f,2.163f,1.068f),new Vector3(side*.145f,2.089f,1.20f),new Vector3(side*.105f,1.98f,1.497f)},.032f,.009f,Leather,18);
            }
            b.Ribbon(new[]{new Vector3(-.107f,1.973f,1.476f),new Vector3(0,1.956f,1.56f),new Vector3(.107f,1.973f,1.476f)},.033f,.008f,Leather,18);
            b.TaperCurve(new[]{new Vector3(0,1.62f,.43f),new Vector3(0,1.83f,.69f),new Vector3(0,2.12f,.92f),new Vector3(0,2.20f,1.04f)},new[]{new Vector2(.045f,.14f),new Vector2(.038f,.11f),new Vector2(.041f,.087f),new Vector2(.025f,.047f)},16,mane,17);
            int[] upper={19,21,23,25};
            for(int i=0;i<upper.Length;i++)
            {
                int u=upper[i],l=u+1;bool front=i<2;var top=bones[u];var knee=bones[l];var foot=new Vector3(knee.x,.125f,front?.74f:-.58f);
                b.Limb(top,knee,new[]{new Vector2(.124f,.16f),new Vector2(.114f,.143f),new Vector2(.080f,.095f),new Vector2(.061f,.078f)},18,coat,u);
                b.Oval(knee,new Vector3(.077f,.086f,.084f),14,coat,l);
                b.Limb(knee,foot,new[]{new Vector2(.063f,.070f),new Vector2(.045f,.057f),new Vector2(.043f,.054f),new Vector2(.067f,.072f)},14,coat,l);
                b.Profile(new Vector3(foot.x,0,foot.z+.014f),Quaternion.identity,new[]{new Ring(.012f,.085f,.105f),new Ring(.033f,.097f,.116f),new Ring(.123f,.080f,.09f),new Ring(.174f,.064f,.068f)},14,hooves,l);
                if(b.Lod<2)b.Limb(foot+Vector3.up*.065f,foot+Vector3.up*.125f,new[]{new Vector2(.073f,.081f),new Vector2(.070f,.076f)},12,coat,l);
            }
            b.TaperCurve(new[]{new Vector3(0,1.38f,-.91f),new Vector3(.031f,1.10f,-1.087f),new Vector3(.08f,.77f,-1.139f),new Vector3(.09f,.49f,-1.12f)},new[]{new Vector2(.052f,.056f),new Vector2(.09f,.082f),new Vector2(.104f,.087f),new Vector2(.025f,.039f)},16,mane,27);
            for(int side=-1;side<=1;side+=2)
            {
                b.Sheet((u,v)=>new Vector3(side*(.345f+.065f*(1-v)),.88f+v*.76f+Mathf.Sin(u*9)*.025f*(1-v),Mathf.Lerp(-.55f,.57f,u)),b.Lod==0?10:b.Lod==1?6:3,b.Lod==0?6:3,Team,16,side<0?Vector3.left:Vector3.right,true);
                b.Sheet((u,v)=>new Vector3(side*.417f,.897f+v*.027f+Mathf.Sin(u*9)*.025f,Mathf.Lerp(-.55f,.57f,u)),b.Lod==0?10:b.Lod==1?6:3,1,Gold,16,side<0?Vector3.left:Vector3.right,false);
                Pouch(b,new Vector3(side*.407f,1.42f,-.38f),Quaternion.Euler(0,side*90,0),16);
            }
            b.Profile(new Vector3(0,0,-.02f),Quaternion.identity,new[]{new Ring(1.68f,.32f,.30f),new Ring(1.75f,.295f,.282f),new Ring(1.786f,.245f,.238f)},20,LeatherDark,16);
            b.Ribbon(new[]{new Vector3(-.26f,1.734f,-.24f),new Vector3(0,1.826f,-.255f),new Vector3(.26f,1.734f,-.24f)},.045f,.012f,LeatherLight,16);
            if(biome==ArtBiome.Kingdom||biome==ArtBiome.Fantasy)
            {
                b.Plate(new[]{new Vector2(-.20f,.20f),new Vector2(-.26f,-.10f),new Vector2(0,-.30f),new Vector2(.26f,-.10f),new Vector2(.20f,.20f)},new Vector3(0,1.38f,.80f),Quaternion.Euler(15,0,0),.025f,.022f,.046f,Steel,Gold,16);
                b.Plate(new[]{new Vector2(-.095f,.16f),new Vector2(-.107f,-.125f),new Vector2(0,-.20f),new Vector2(.107f,-.125f),new Vector2(.095f,.16f)},new Vector3(0,2.104f,1.315f),Quaternion.Euler(24,0,0),.018f,.013f,.025f,Steel,Gold,18);
            }
            b.Curve(new[]{new Vector3(-.10f,1.99f,1.48f),new Vector3(-.18f,1.85f,.89f),new Vector3(-.35f,2.18f,.40f)},.009f,8,Leather,16);
        }

        private static ArtStylePrototypeGeometry.Prototype BuildDragon(ArtBiome biome,int lod)
        {
            var names=HumanNames();var parents=HumanParents();var bones=new[]
            {
                Vector3.zero,new Vector3(0,1.20f,-.52f),new Vector3(0,1.55f,.20f),new Vector3(0,2.36f,1.30f),
                new Vector3(-.48f,1.48f,.54f),new Vector3(-.77f,.70f,.39f),new Vector3(-.77f,.12f,1.02f),
                new Vector3(.48f,1.48f,.54f),new Vector3(.77f,.70f,.39f),new Vector3(.77f,.12f,1.02f),
                new Vector3(-.47f,1.20f,-.73f),new Vector3(-.78f,.54f,-.62f),new Vector3(-.82f,.12f,-.69f),
                new Vector3(.47f,1.20f,-.73f),new Vector3(.78f,.54f,-.62f),new Vector3(.82f,.12f,-.69f)
            };
            Array.Resize(ref names,24);Array.Resize(ref bones,24);Array.Resize(ref parents,24);
            var extraNames=new[]{"WingL","WingTipL","WingR","WingTipR","Tail1","Tail2","Jaw","Neck"};
            var extraParents=new[]{2,16,2,18,1,20,3,2};
            var extraPositions=new[]{new Vector3(-.44f,1.70f,.12f),new Vector3(-1.48f,2.35f,-.10f),new Vector3(.44f,1.70f,.12f),new Vector3(1.48f,2.35f,-.10f),new Vector3(0,1.15f,-1.06f),new Vector3(.13f,.88f,-2.0f),new Vector3(0,2.18f,1.28f),new Vector3(0,1.78f,.82f)};
            for(int i=0;i<8;i++){names[16+i]=extraNames[i];parents[16+i]=extraParents[i];bones[16+i]=extraPositions[i];}
            var b=new Builder(lod);
            var scales=new Paint(.32f,.49f,.56f,.09f,.39f,tile:11);var lightScales=new Paint(.66f,.75f,.72f,.10f,.40f,tile:11);
            var membrane=new Paint(.42f,.57f,.66f,0,.32f,tile:1);var membraneUnder=new Paint(.59f,.69f,.74f,0,.29f,tile:1);
            var ridge=new Paint(.17f,.31f,.38f,.12f,.43f,tile:11);var scaleEdge=new Paint(.44f,.62f,.66f,.12f,.42f,tile:11);
            var horn=new Paint(.70f,.69f,.52f,.08f,.38f,tile:11);var mouth=new Paint(.14f,.085f,.10f,tile:1);var tongue=new Paint(.49f,.21f,.20f,tile:13);
            var teamScales=new Paint(.76f,.84f,.87f,.16f,.44f,1,11);
            b.TaperCurve(new[]{new Vector3(0,1.16f,-1.04f),new Vector3(0,1.23f,-.62f),new Vector3(0,1.40f,-.13f),new Vector3(0,1.53f,.30f),new Vector3(0,1.56f,.65f)},new[]{new Vector2(.31f,.32f),new Vector2(.49f,.45f),new Vector2(.56f,.49f),new Vector2(.51f,.49f),new Vector2(.32f,.36f)},28,scales,Spine);
            b.Profile(Vector3.zero,Quaternion.identity,DragonNeckRings(lod),28,scales,23);
            b.Profile(new Vector3(0,2.415f,1.13f),Quaternion.Euler(99,0,0),new[]{new Ring(0,.13f,.155f),new Ring(.13f,.216f,.189f),new Ring(.31f,.236f,.160f),new Ring(.48f,.205f,.126f),new Ring(.67f,.166f,.095f),new Ring(.81f,.112f,.065f)},28,scales,Head);
            b.Profile(new Vector3(0,2.18f,1.23f),Quaternion.Euler(92,0,0),new[]{new Ring(0,.138f,.063f),new Ring(.15f,.193f,.084f),new Ring(.35f,.189f,.071f),new Ring(.58f,.148f,.043f),new Ring(.68f,.099f,.025f)},24,lightScales,22);
            b.Profile(new Vector3(0,2.267f,1.36f),Quaternion.Euler(90,0,0),new[]{new Ring(0,.16f,.035f),new Ring(.22f,.18f,.047f),new Ring(.44f,.14f,.031f),new Ring(.53f,.080f,.022f)},16,mouth,Head);
            b.Oval(new Vector3(0,2.223f,1.69f),new Vector3(.078f,.023f,.18f),14,tongue,22);
            for(int side=-1;side<=1;side+=2)
            {
                var eye=new Vector3(side*.206f,2.464f,1.454f);var eyeRot=Quaternion.Euler(0,side*56,side*9);
                b.Plate(EllipseOutline(.063f,.030f,12),eye,eyeRot,.008f,.008f,.009f,new Paint(.89f,.63f,.25f,.05f,.52f,tile:15),ridge,Head);
                b.Plate(EllipseOutline(.008f,.024f,8),eye+eyeRot*Vector3.forward*.025f,eyeRot,.002f,.001f,.001f,LeatherDark,LeatherDark,Head);
                b.TaperCurve(new[]{new Vector3(side*.138f,2.531f,1.33f),new Vector3(side*.232f,2.516f,1.447f),new Vector3(side*.228f,2.465f,1.575f)},new[]{new Vector2(.058f,.054f),new Vector2(.060f,.038f),new Vector2(.016f,.018f)},14,ridge,Head);
                var hornPoints=new[]{new Vector3(side*.17f,2.534f,1.20f),new Vector3(side*.25f,2.69f,1.10f),new Vector3(side*.35f,2.89f,.95f),new Vector3(side*.40f,3.066f,.825f),new Vector3(side*.375f,3.151f,.74f)};
                b.TaperCurve(hornPoints,new[]{new Vector2(.096f,.100f),new Vector2(.081f,.080f),new Vector2(.058f,.059f),new Vector2(.025f,.030f),new Vector2(.002f,.003f)},18,horn,Head);
                if(lod<2)for(int rib=0;rib<3;rib++)
                {
                    float radius=.09f-rib*.02f;b.Limb(Vector3.Lerp(hornPoints[rib],hornPoints[rib+1],.40f),Vector3.Lerp(hornPoints[rib],hornPoints[rib+1],.51f),new[]{new Vector2(radius,radius),new Vector2(radius*.96f,radius*.96f)},12,lightScales,Head);
                }
                b.TaperCurve(new[]{new Vector3(side*.18f,2.25f,1.34f),new Vector3(side*.28f,2.28f,1.19f),new Vector3(side*.32f,2.37f,1.06f)},new[]{new Vector2(.068f,.075f),new Vector2(.045f,.045f),new Vector2(.003f,.004f)},14,horn,22);
                b.ScalePatch(new Vector3(side*.195f,2.30f,1.27f),Quaternion.Euler(0,side*78,0),new Vector2(.115f,.092f),.033f,scaleEdge,Head);
                b.ScalePatch(new Vector3(side*.185f,2.24f,1.49f),Quaternion.Euler(0,side*80,-side*12),new Vector2(.15f,.050f),.020f,lightScales,22);
                int teeth=lod==0?5:lod==1?3:2;
                for(int tooth=0;tooth<teeth;tooth++)
                {
                    float t=tooth/(float)(teeth-1),z=1.48f+t*.38f,x=Mathf.Lerp(.18f,.107f,t),y=Mathf.Lerp(2.321f,2.266f,t),length=tooth==0?.155f:.073f;
                    b.TaperCurve(new[]{new Vector3(side*x,y,z),new Vector3(side*(x-.01f),y-length*.6f,z+.012f),new Vector3(side*(x-.024f),y-length,z+.036f)},new[]{new Vector2(tooth==0?.033f:.020f,tooth==0?.033f:.020f),new Vector2(.018f,.018f),new Vector2(.001f,.002f)},10,Ivory,Head);
                }
                b.Plate(EllipseOutline(.025f,.016f,8),new Vector3(side*.092f,2.317f,1.915f),Quaternion.Euler(-23,side*32,0),.003f,.003f,.001f,LeatherDark,ridge,Head);
            }
            for(int leg=0;leg<4;leg++)
            {
                int upper=leg==0?4:leg==1?7:leg==2?10:13;int lower=upper+1,foot=upper+2,side=leg%2==0?-1:1;bool front=leg<2;
                var top=bones[upper];var joint=bones[lower];var paw=bones[foot];
                b.Oval(top+new Vector3(-side*.055f,.035f,front?-.035f:.045f),front?new Vector3(.266f,.319f,.287f):new Vector3(.29f,.331f,.345f),22,scales,upper);
                b.TaperCurve(new[]{top,Vector3.Lerp(top,joint,.35f)+new Vector3(side*.078f,.020f,front?-.06f:.15f),Vector3.Lerp(top,joint,.75f)+new Vector3(side*.028f,0,front?-.025f:.055f),joint},new[]{new Vector2(.235f,.257f),new Vector2(front?.251f:.285f,front?.215f:.268f),new Vector2(.17f,.182f),new Vector2(.123f,.147f)},22,scales,upper);
                b.Oval(joint,new Vector3(.155f,.155f,.16f),16,ridge,lower);
                var hock=front?Vector3.Lerp(joint,paw,.52f)+new Vector3(side*.027f,.02f,-.012f):new Vector3(side*.865f,.30f,-.94f);
                b.TaperCurve(new[]{joint,hock,paw+new Vector3(0,.048f,.035f)},new[]{new Vector2(.138f,.156f),new Vector2(.106f,.119f),new Vector2(.163f,.149f)},18,scales,lower);
                b.Oval(paw+new Vector3(0,.03f,.065f),new Vector3(.212f,.132f,.271f),20,scales,foot);
                b.ScalePatch(top+new Vector3(side*.23f,.01f,.10f),Quaternion.Euler(0,side*66,side*14),new Vector2(.17f,.21f),.035f,scaleEdge,upper);
                b.ScalePatch(joint+new Vector3(0,.065f,.135f),Quaternion.Euler(-21,side*15,0),new Vector2(.117f,.103f),.031f,scaleEdge,lower);
                for(int toe=-1;toe<=1;toe++)
                {
                    var start=paw+new Vector3(toe*.133f,.050f,.15f);
                    b.TaperCurve(new[]{start,start+new Vector3(toe*.018f,-.015f,.17f),start+new Vector3(toe*.025f,-.048f,.285f)},new[]{new Vector2(.069f,.060f),new Vector2(.050f,.045f),new Vector2(.006f,.007f)},12,scales,foot);
                    var nail=start+new Vector3(toe*.02f,-.022f,.20f);
                    b.TaperCurve(new[]{nail,nail+new Vector3(toe*.008f,-.026f,.09f),nail+new Vector3(toe*.014f,-.053f,.17f)},new[]{new Vector2(.043f,.039f),new Vector2(.027f,.023f),new Vector2(.001f,.002f)},10,horn,foot);
                }
            }
            b.TaperCurve(new[]{new Vector3(0,1.17f,-1.01f),new Vector3(.06f,1.06f,-1.46f),new Vector3(.13f,.88f,-2.02f)},new[]{new Vector2(.29f,.27f),new Vector2(.225f,.21f),new Vector2(.143f,.137f)},24,scales,20);
            b.TaperCurve(new[]{new Vector3(.13f,.88f,-2.02f),new Vector3(.22f,.63f,-2.53f),new Vector3(.40f,.48f,-2.91f),new Vector3(.70f,.56f,-3.09f)},new[]{new Vector2(.143f,.137f),new Vector2(.096f,.088f),new Vector2(.058f,.052f),new Vector2(.006f,.005f)},20,scales,21);
            for(int side=-1;side<=1;side+=2)
            {
                int wing=side<0?16:18,tipBone=wing+1;var root=bones[wing];var elbow=bones[tipBone];
                int firstWingVertex=b.VertexCount;
                var tips=new[]{new Vector3(side*3.10f,2.89f,.18f),new Vector3(side*2.94f,2.04f,-.90f),new Vector3(side*2.14f,1.65f,-1.57f),new Vector3(side*1.28f,1.41f,-1.59f),new Vector3(side*.45f,1.26f,-.77f)};
                b.TaperCurve(new[]{root,elbow,tips[0]},new[]{new Vector2(.124f,.134f),new Vector2(.084f,.092f),new Vector2(.012f,.019f)},20,scales,wing);
                for(int panel=0;panel<tips.Length-1;panel++)
                {
                    var a=tips[panel];var c=tips[panel+1];var fan=panel<3?elbow:root;int bone=panel<2?tipBone:wing;
                    Vector3 WingSample(float u,float v)
                    {
                        var edge=Vector3.Lerp(a,c,u)+Vector3.up*(Mathf.Sin(u*Mathf.PI)*.14f);
                        var p=Vector3.Lerp(fan,edge,v);p.y-=Mathf.Sin(v*Mathf.PI)*Mathf.Sin(u*Mathf.PI)*.15f;return p;
                    }
                    int columns=lod==0?10:lod==1?7:4,rows=lod==0?8:lod==1?5:3;
                    b.Sheet(WingSample,columns,rows,panel==3?teamScales:membrane,bone,Vector3.up,false);
                    b.Sheet((u,v)=>WingSample(u,v)+Vector3.down*.007f,columns,rows,panel==3?teamScales:membraneUnder,bone,Vector3.down,false);
                    b.TaperCurve(new[]{elbow,Vector3.Lerp(elbow,c,.54f)+Vector3.up*.055f,c},new[]{new Vector2(.044f,.049f),new Vector2(.025f,.031f),new Vector2(.009f,.014f)},12,ridge,bone);
                    if(lod<2)
                    {
                        var vein=Vector3.Lerp(a,c,.52f);b.Curve(new[]{fan,Vector3.Lerp(fan,vein,.52f)+Vector3.down*.035f,vein},.009f,6,scaleEdge,bone);
                    }
                }
                b.Sheet((u,v)=>Vector3.Lerp(root,Vector3.Lerp(elbow,tips[3],u),v)+Vector3.down*(Mathf.Sin(u*Mathf.PI)*Mathf.Sin(v*Mathf.PI)*.065f),lod==0?8:lod==1?5:3,lod==0?6:lod==1?4:2,teamScales,wing,Vector3.up,true);
                b.TaperCurve(new[]{elbow,elbow+new Vector3(side*.047f,.18f,.068f),elbow+new Vector3(side*.084f,.21f,.131f)},new[]{new Vector2(.046f,.038f),new Vector2(.022f,.023f),new Vector2(.002f,.003f)},12,lightScales,wing);
                // Every membrane and strut uses the same spatial blend, including duplicated edge vertices.
                b.BlendRange(firstWingVertex,wing,tipBone,root,elbow);
            }
            int plates=lod==0?9:lod==1?6:3;
            for(int i=0;i<plates;i++)
            {
                float bottom=1.435f+i*.91f/plates,height=.99f/plates;
                b.Sheet((u,v)=>DragonNeckSurface(bottom+v*height,(u-.5f)*2.32f,.016f+.012f*Mathf.Sin(v*Mathf.PI),lod),lod==0?12:lod==1?8:5,lod==0?3:2,i%3==0?scaleEdge:lightScales,23,Vector3.forward,false);
            }
            DragonScaleRows(b,scales,scaleEdge,ridge);
            for(int i=0;i<(lod==2?4:8);i++)
            {
                float t=i/(float)(lod==2?3:7);var start=new Vector3(0,Mathf.Lerp(1.49f,1.93f,t),Mathf.Lerp(-.99f,.51f,t));
                b.Limb(start,start+new Vector3(0,.18f,-.073f),new[]{new Vector2(.087f,.103f),new Vector2(.027f,.045f),new Vector2(.002f,.003f)},10,i%2==0?teamScales:ridge,Spine);
            }
            return new ArtStylePrototypeGeometry.Prototype{Mesh=b.Finish("Overhaul "+biome+" Original Dragon",bones),BoneNames=names,BonePositions=bones,BoneParents=parents};
        }
        private static Ring[] DragonNeckRings(int lod)
        {
            var source=new[]{new Ring(1.40f,.305f,.315f,.54f),new Ring(1.62f,.318f,.295f,.625f),new Ring(1.88f,.265f,.259f,.755f),new Ring(2.14f,.206f,.215f,.975f),new Ring(2.365f,.205f,.193f,1.17f),new Ring(2.49f,.179f,.165f,1.285f)};
            return lod==0?source:lod==1?new[]{source[0],source[1],source[3],source[5]}:new[]{source[0],source[1],source[5]};
        }
        private static Vector3 DragonNeckSurface(float y,float angle,float clearance,int lod)
        {
            var rings=DragonNeckRings(lod);int index=0;while(index<rings.Length-2&&y>rings[index+1].Y)index++;
            var a=rings[index];var c=rings[index+1];float t=Mathf.InverseLerp(a.Y,c.Y,y);
            return new Vector3(Mathf.Sin(angle)*(Mathf.Lerp(a.X,c.X,t)+clearance),y,Mathf.Lerp(a.OffsetZ,c.OffsetZ,t)+Mathf.Cos(angle)*(Mathf.Lerp(a.Z,c.Z,t)+clearance));
        }
        private static void DragonScaleRows(Builder b,Paint scales,Paint edge,Paint ridge)
        {
            int rows=b.Lod==0?6:b.Lod==1?4:2,columns=b.Lod==0?4:b.Lod==1?3:2;
            for(int side=-1;side<=1;side+=2)for(int row=0;row<rows;row++)for(int column=0;column<columns;column++)
            {
                float z=Mathf.Lerp(-.84f,.45f,(row+.35f*(column%2))/(rows-.65f));
                float angle=Mathf.Lerp(.27f,1.38f,column/(float)(columns-1));
                float width=.55f-Mathf.Abs(z+.03f)*.17f,height=.485f-Mathf.Abs(z+.03f)*.15f,baseY=1.39f+z*.24f;
                var normal=new Vector3(side*Mathf.Sin(angle),Mathf.Cos(angle),-.04f);
                var centre=new Vector3(side*Mathf.Sin(angle)*width,baseY+Mathf.Cos(angle)*height,z)+normal*.011f;
                b.ScalePatch(centre,Quaternion.LookRotation(normal,Vector3.forward),new Vector2(b.Lod==2?.16f:.115f,b.Lod==2?.24f:.165f),.024f,(row+column)%3==0?edge:ridge,Spine);
            }
            int neckRows=b.Lod==0?5:b.Lod==1?3:2;
            for(int side=-1;side<=1;side+=2)for(int row=0;row<neckRows;row++)
            {
                float y=Mathf.Lerp(1.57f,2.33f,row/(float)(neckRows-1));float angle=side*1.31f;
                var normal=new Vector3(Mathf.Sin(angle),.12f,Mathf.Cos(angle));
                b.ScalePatch(DragonNeckSurface(y,angle,.014f,b.Lod),Quaternion.LookRotation(normal,Vector3.up),new Vector2(.115f,.124f),.022f,row%2==0?edge:ridge,23);
            }
            int tailRows=b.Lod==0?8:b.Lod==1?5:3;
            for(int i=0;i<tailRows;i++)
            {
                float t=i/(float)(tailRows-1);var p=new Vector3(.03f+t*.28f,1.415f-t*.69f,-1.11f-t*1.70f);
                b.ScalePatch(p,Quaternion.Euler(-90,0,0),new Vector2(.18f*(1-t*.7f),.20f*(1-t*.4f)),.020f,i%2==0?edge:scales,t<.5f?20:21);
            }
        }
        private static void Body(Builder b, Vector3[] bones, bool warrior)
        {
            b.Profile(Vector3.zero, Quaternion.identity, new[]
            {
                new Ring(.90f,.205f,.135f), new Ring(1.02f,.235f,.153f), new Ring(1.13f,.223f,.158f),
                new Ring(1.30f,.264f,.177f), new Ring(1.47f,.295f,.181f), new Ring(1.57f,.276f,.157f),
                new Ring(1.64f,.125f,.102f)
            }, 16, warrior ? SteelDark : Linen, Spine, .018f);
            b.BlendWaist();
            b.Profile(Vector3.zero, Quaternion.identity, new[] { new Ring(1.60f,.085f,.081f,.012f), new Ring(1.69f,.084f,.080f,.018f), new Ring(1.76f,.106f,.087f,.022f) }, 12, Skin, Head);

            for (int side = 0; side < 2; side++)
            {
                int upperLeg = side == 0 ? UpperLegL : UpperLegR;
                int lowerLeg = side == 0 ? LowerLegL : LowerLegR;
                int foot = side == 0 ? FootL : FootR;
                int upperArm = side == 0 ? UpperArmL : UpperArmR;
                int lowerArm = side == 0 ? LowerArmL : LowerArmR;
                int hand = side == 0 ? HandL : HandR;
                var hip = bones[upperLeg]; var knee = bones[lowerLeg]; var ankle = bones[foot];
                b.Limb(knee - Vector3.up * .025f, hip + Vector3.up * .065f,
                    new[] { new Vector2(.089f,.105f), new Vector2(.131f,.133f), new Vector2(.146f,.145f), new Vector2(.132f,.134f), new Vector2(.101f,.111f) },
                    12, Trousers, upperLeg, .052f);
                b.Limb(ankle + Vector3.up * .015f, knee + Vector3.up * .013f,
                    new[] { new Vector2(.074f,.083f), new Vector2(.082f,.085f), new Vector2(.097f,.103f), new Vector2(.107f,.10f), new Vector2(.099f,.089f) },
                    12, warrior ? LeatherDark : Leather, lowerLeg, .016f);

                // Heel, instep and toe are one continuous swept shoe profile, not a box foot.
                b.Profile(new Vector3(ankle.x, .103f, ankle.z - .11f), Quaternion.Euler(90,0,0), new[]
                {
                    new Ring(0,.066f,.060f), new Ring(.045f,.092f,.088f), new Ring(.125f,.108f,.101f),
                    new Ring(.235f,.112f,.085f), new Ring(.320f,.080f,.048f), new Ring(.362f,.026f,.020f)
                }, 12, warrior ? Steel : LeatherDark, foot);
                if (b.Lod < 2)
                {
                    b.Profile(new Vector3(ankle.x,.036f,ankle.z-.104f), Quaternion.Euler(90,0,0), new[]
                    { new Ring(0,.070f,.023f), new Ring(.055f,.102f,.029f), new Ring(.25f,.120f,.028f), new Ring(.344f,.066f,.023f) }, 10, LeatherDark, foot);
                    b.Limb(ankle + Vector3.up * .32f, ankle + Vector3.up * .375f,
                        new[] { new Vector2(.102f,.106f), new Vector2(.111f,.113f), new Vector2(.105f,.105f) }, 12,
                        warrior ? Gold : LeatherLight, lowerLeg);
                }

                var shoulder = bones[upperArm]; var elbow = bones[lowerArm]; var wrist = bones[hand];
                b.Limb(shoulder, elbow + Vector3.up * .025f,
                    new[] { new Vector2(.117f,.126f), new Vector2(.134f,.135f), new Vector2(.12f,.127f), new Vector2(.096f,.104f), new Vector2(.088f,.096f) },
                    12, warrior ? SteelDark : Linen, upperArm, warrior ? .01f : .045f);
                b.Limb(elbow, wrist,
                    new[] { new Vector2(.079f,.079f), new Vector2(.091f,.085f), new Vector2(.083f,.082f), new Vector2(.065f,.065f), new Vector2(.057f,.060f) },
                    12, warrior ? LeatherDark : Skin, lowerArm);
                if (!warrior)
                {
                    var sleeveEnd = Vector3.Lerp(shoulder, elbow, .85f);
                    b.Limb(sleeveEnd + (shoulder - elbow).normalized * .017f, sleeveEnd + (elbow - shoulder).normalized * .074f,
                        new[] { new Vector2(.102f,.110f), new Vector2(.119f,.117f), new Vector2(.117f,.118f), new Vector2(.10f,.102f) }, 12, Ivory, upperArm);
                }
                Hand(b, wrist, hand, side == 0 ? -1 : 1, warrior || side == 0, side == 1);
            }
        }

        private static void Hand(Builder b, Vector3 wrist, int bone, int side, bool glove, bool grips)
        {
            Paint paint = glove ? Leather : SkinLight;
            var centre = wrist + new Vector3(0, -.063f, .027f);
            b.Oval(centre, new Vector3(.074f,.103f,.061f), 10, paint, bone);
            var thumb = centre + new Vector3(-side * .066f,.015f,.044f);
            if (b.Lod < 2)
                b.Limb(thumb + Vector3.up * .034f, thumb + new Vector3(side*.009f,-.05f,.013f),
                    new[] { new Vector2(.027f,.026f), new Vector2(.033f,.030f), new Vector2(.020f,.021f) }, 8, paint, bone);
            if (b.Lod == 0)
            {
                for (int finger = 0; finger < 4; finger++)
                {
                    float x = (finger - 1.5f) * .030f;
                    var a = centre + new Vector3(x,-.039f,.03f);
                    var end = a + new Vector3(0,-.065f,grips ? -.013f : .016f);
                    b.Limb(a, end, new[] { new Vector2(.017f,.020f), new Vector2(.018f,.021f), new Vector2(.013f,.014f) }, 6, paint, bone);
                }
            }
        }

        private static void Face(Builder b, bool warrior, bool clean=false)
        {
            b.Profile(Vector3.zero, Quaternion.identity, new[]
            {
                new Ring(1.697f,.079f,.081f,.046f), new Ring(1.735f,.122f,.107f,.037f),
                new Ring(1.80f,.153f,.126f,.027f), new Ring(1.858f,.16f,.136f,.023f),
                new Ring(1.915f,.15f,.130f,.020f), new Ring(1.981f,.128f,.116f,.014f),
                new Ring(2.025f,.073f,.071f,.009f), new Ring(2.040f,.021f,.023f,.007f)
            }, 18, Skin, Head);
            b.Profile(Vector3.zero, Quaternion.identity, new[]
            {
                new Ring(1.800f,.025f,.019f,.190f), new Ring(1.822f,.041f,.030f,.217f),
                new Ring(1.854f,.029f,.027f,.204f), new Ring(1.896f,.020f,.023f,.177f),
                new Ring(1.922f,.009f,.010f,.155f)
            }, 10, SkinLight, Head);
            for (int side = -1; side <= 1; side += 2)
            {
                if (b.Lod < 2)
                {
                    b.Oval(new Vector3(side*.161f,1.842f,.024f), new Vector3(.029f,.053f,.027f), 10, SkinLight, Head);
                    if (b.Lod == 0) b.Oval(new Vector3(side*.172f,1.843f,.044f), new Vector3(.012f,.032f,.008f), 8, SkinShade, Head);
                    var eye = new Vector3(side*.064f,1.884f,.150f);
                    var eyeRotation = Quaternion.Euler(0,side*22,0);
                    b.Plate(EllipseOutline(.020f,.008f,8), eye, eyeRotation, .0025f, .002f, .0008f, Eye, SkinShade, Head);
                    b.Plate(EllipseOutline(.006f,.006f,8), eye + eyeRotation*new Vector3(-side*.001f,0,.004f), eyeRotation, .001f, .001f, 0, Iris, Iris, Head);
                    b.Curve(new[] { new Vector3(side*.033f,1.905f,.162f), new Vector3(side*.07f,1.914f,.157f), new Vector3(side*.107f,1.905f,.133f) }, .012f, 6, Hair, Head);
                    if (b.Lod == 0)
                    {
                        b.Curve(new[] { new Vector3(side*.033f,1.893f,.163f), new Vector3(side*.065f,1.895f,.165f), new Vector3(side*.09f,1.889f,.153f) }, .004f, 5, SkinShade, Head);
                    }
                }
                if (b.Lod < 2 && !clean)
                    b.Curve(new[] { new Vector3(side*.012f,1.806f,.209f), new Vector3(side*.043f,1.796f,.197f), new Vector3(side*.080f,1.782f,.168f) }, warrior ? .016f : .022f, 8, Hair, Head);
            }
            // A shaped chin and cheek mass leaves the upper face and nose exposed.
            if(!clean)b.Profile(Vector3.zero, Quaternion.identity, new[]
            {
                new Ring(warrior ? 1.693f : 1.651f,.025f,.041f,.102f), new Ring(1.710f,.081f,.079f,.059f),
                new Ring(1.753f,.125f,.098f,.046f), new Ring(1.787f,.146f,.097f,.032f),
                new Ring(1.817f,.147f,.069f,.003f)
            }, 14, Hair, Head, .075f);
            if (b.Lod < 2)
            {
                b.Curve(new[] { new Vector3(-.041f,1.770f,.163f), new Vector3(0,1.766f,.168f), new Vector3(.041f,1.770f,.163f) }, .007f, 6, SkinShade, Head);
                if (!warrior && !clean)
                {
                    for (int lockIndex = -1; lockIndex <= 1; lockIndex++)
                        b.Curve(new[] { new Vector3(lockIndex*.043f,1.742f,.143f), new Vector3(lockIndex*.031f,1.69f,.139f), new Vector3(lockIndex*.018f,1.665f,.121f) }, .012f, 6, HairLight, Head);
                }
            }
            if (!warrior)
            {
                b.Profile(Vector3.zero, Quaternion.identity, new[] { new Ring(1.956f,.161f,.139f,.007f), new Ring(1.991f,.157f,.137f,.002f), new Ring(2.032f,.092f,.089f), new Ring(2.045f,.014f,.014f) }, 16, Hair, Head);
                if (b.Lod < 2)
                    for (int side = -1; side <= 1; side += 2)
                        b.Curve(new[] { new Vector3(side*.143f,1.973f,-.041f), new Vector3(side*.159f,1.891f,-.036f), new Vector3(side*.157f,1.810f,-.046f) }, .033f, 8, HairLight, Head);
            }
        }

        private static void Worker(Builder b, Vector3[] bones)
        {
            // Broad, slightly raised brim and a pinched, rounded crown.
            var hatPivotCorrection = new Vector3(-.1047f,.0027f,0);
            b.Profile(hatPivotCorrection, Quaternion.Euler(0,0,-3), new[]
            {
                new Ring(1.965f,.188f,.151f), new Ring(1.967f,.356f,.292f), new Ring(1.989f,.405f,.325f),
                new Ring(2.006f,.402f,.323f), new Ring(2.003f,.306f,.249f), new Ring(2.011f,.192f,.156f),
                new Ring(2.091f,.175f,.141f), new Ring(2.137f,.123f,.106f), new Ring(2.153f,.061f,.055f)
            }, 28, Straw, Head);
            // Same angular tessellation as the crown and a real radial clearance prevent saw-tooth intersections.
            b.Profile(hatPivotCorrection, Quaternion.Euler(0,0,-3), new[] { new Ring(2.016f,.201f,.165f), new Ring(2.056f,.192f,.157f) }, 28, Team, Head);

            ClothPanel(b, -.225f, -.019f, 1.075f, 1.573f, Team, Spine, false, .014f);
            ClothPanel(b, .019f, .225f, 1.075f, 1.573f, Team, Spine, false, .014f);
            ClothPanel(b, -.255f, -.019f, .746f, 1.092f, Team, Hips, false, .017f, Ivory);
            ClothPanel(b, .019f, .255f, .746f, 1.092f, Team, Hips, false, .017f, Ivory);
            ClothPanel(b, -.23f, .23f, .80f, 1.54f, Team, Spine, true, .015f);
            b.Ribbon(new[] { new Vector3(-.247f,1.557f,.128f), new Vector3(-.172f,1.432f,.172f), new Vector3(-.059f,1.263f,.197f), new Vector3(.185f,1.103f,.144f) }, .054f, .010f, Leather, Spine);
            b.Ribbon(new[] { new Vector3(.247f,1.557f,.131f), new Vector3(.172f,1.432f,.178f), new Vector3(.059f,1.263f,.206f), new Vector3(-.185f,1.103f,.146f) }, .046f, .009f, LeatherLight, Spine);
            Belt(b, false);

            // Rounded canvas pack with shaped flap, two leather straps, and a rolled blanket.
            b.Profile(new Vector3(0,0,-.244f), Quaternion.identity, new[]
            {
                new Ring(.99f,.141f,.095f), new Ring(1.045f,.214f,.139f), new Ring(1.31f,.224f,.155f),
                new Ring(1.43f,.193f,.141f), new Ring(1.476f,.12f,.095f)
            }, 14, LeatherLight, Spine, .018f);
            b.Plate(new[] { new Vector2(-.185f,.08f),new Vector2(-.178f,-.08f),new Vector2(0,-.116f),new Vector2(.178f,-.08f),new Vector2(.185f,.08f) },
                new Vector3(0,1.32f,-.396f), Quaternion.Euler(0,180,0), .019f, .016f, .018f, Leather, LeatherLight, Spine);
            for (int side = -1; side <= 1; side += 2)
            {
                b.Ribbon(new[] { new Vector3(side*.137f,1.47f,-.339f), new Vector3(side*.16f,1.32f,-.418f), new Vector3(side*.145f,1.046f,-.369f) }, .031f, .012f, LeatherDark, Spine);
                b.Ribbon(new[] { new Vector3(side*.185f,1.52f,-.168f), new Vector3(side*.226f,1.578f,0), new Vector3(side*.193f,1.354f,.184f) }, .038f, .014f, Leather, Spine);
            }
            b.Limb(new Vector3(-.251f,1.469f,-.285f),new Vector3(.251f,1.469f,-.285f),
                new[] { new Vector2(.063f,.063f),new Vector2(.091f,.09f),new Vector2(.084f,.091f),new Vector2(.066f,.065f) }, 12, Linen, Spine);
            b.Ribbon(new[] { new Vector3(-.215f,1.192f,-.382f),new Vector3(0,1.179f,-.414f),new Vector3(.215f,1.192f,-.382f) }, .089f, .008f, Team, Spine);
            if (b.Lod < 2)
            {
                Pouch(b,new Vector3(-.28f,.924f,.094f),Quaternion.Euler(0,-25,0),Hips);
                Pouch(b,new Vector3(.26f,.925f,.076f),Quaternion.Euler(0,35,0),Hips);
                Horizon(b,new Vector3(-.109f,.867f,.210f),Quaternion.identity,.24f,Hips);
            }

            var shaftBottom = new Vector3(.541f,.12f,.233f);
            var shaftTop = new Vector3(.541f,1.776f,.233f);
            b.Limb(shaftBottom, shaftTop, new[] { new Vector2(.027f,.027f),new Vector2(.034f,.034f),new Vector2(.028f,.028f) }, 10, Wood, HandR);
            b.Curve(new[] { new Vector3(.395f,1.779f,.231f),new Vector3(.46f,1.75f,.237f),new Vector3(.541f,1.744f,.241f),new Vector3(.62f,1.75f,.237f),new Vector3(.687f,1.779f,.231f) }, .025f, 8, SteelDark, HandR);
            for (int prong = -1; prong <= 1; prong++)
            {
                float x = .541f + prong*.145f;
                b.Limb(new Vector3(x,1.766f,.23f),new Vector3(x+prong*.024f,2.111f,.189f),
                    new[] { new Vector2(.024f,.022f),new Vector2(.023f,.021f),new Vector2(.017f,.017f),new Vector2(.003f,.005f) }, 8, Steel, HandR);
            }
            b.Limb(new Vector3(.541f,1.64f,.233f),new Vector3(.541f,1.767f,.233f), new[] { new Vector2(.036f,.036f),new Vector2(.040f,.04f),new Vector2(.03f,.03f) }, 10, SteelDark, HandR);
        }

        private static void Warrior(Builder b, Vector3[] bones, bool hero=false)
        {
            // Fitted cuirass with a broad breast ridge and narrowing waist.
            b.Profile(Vector3.zero, Quaternion.identity, new[]
            {
                new Ring(1.04f,.241f,.168f,.004f), new Ring(1.11f,.25f,.179f,.001f), new Ring(1.27f,.282f,.211f,.006f),
                new Ring(1.43f,.307f,.219f,.007f), new Ring(1.553f,.274f,.188f), new Ring(1.602f,.163f,.133f)
            }, 18, Steel, Spine);
            b.Profile(Vector3.zero, Quaternion.identity, new[] { new Ring(1.592f,.133f,.117f),new Ring(1.629f,.13f,.114f),new Ring(1.65f,.093f,.095f) }, 14, Gold, Spine);
            ClothPanel(b,-.191f,-.009f,1.075f,1.513f,Team,Spine,false,.060f);
            ClothPanel(b,.009f,.191f,1.075f,1.513f,Ivory,Spine,false,.060f);
            ClothPanel(b,-.253f,-.012f,.684f,1.095f,Team,Hips,false,.032f,Gold);
            ClothPanel(b,.012f,.253f,.684f,1.095f,Ivory,Hips,false,.032f,Gold);
            ClothPanel(b,-.292f,.292f,.674f,1.568f,Team,Spine,true,.037f);
            for (int side = -1; side <= 1; side += 2)
            {
                int upper = side < 0 ? UpperArmL : UpperArmR;
                int lower = side < 0 ? LowerArmL : LowerArmR;
                int leg = side < 0 ? LowerLegL : LowerLegR;
                var shoulder = bones[upper];
                float shoulderWidth=hero?1.14f:1;
                b.Profile(shoulder + new Vector3(side*.025f,-.115f,0), Quaternion.identity, new[]
                {
                    new Ring(0,.165f*shoulderWidth,.144f),new Ring(.028f,.215f*shoulderWidth,.167f),new Ring(.074f,.20f*shoulderWidth,.155f),
                    new Ring(.11f,.12f*shoulderWidth,.11f),new Ring(.132f,.045f,.052f)
                }, 16, Steel, upper);
                b.Profile(shoulder + new Vector3(side*.025f,-.115f,0),Quaternion.identity,
                    new[] { new Ring(.009f,.193f*shoulderWidth,.161f),new Ring(.027f,.216f*shoulderWidth,.17f) },16,Gold,upper);
                var elbow = bones[lower]; var wrist = bones[side < 0 ? HandL : HandR];
                b.Limb(Vector3.Lerp(elbow,wrist,.18f),Vector3.Lerp(elbow,wrist,.94f),
                    new[] { new Vector2(.100f,.100f),new Vector2(.113f,.106f),new Vector2(.092f,.091f),new Vector2(.076f,.076f) },14,Steel,lower);
                if (b.Lod < 2)
                {
                    b.Oval(elbow + new Vector3(0,-.005f,-.015f),new Vector3(.102f,.097f,.091f),12,SteelEdge,lower);
                    b.Limb(Vector3.Lerp(elbow,wrist,.83f),Vector3.Lerp(elbow,wrist,.96f),
                        new[] { new Vector2(.088f,.086f),new Vector2(.089f,.087f) },12,Gold,lower);
                    var knee = bones[leg];
                    b.Oval(knee + new Vector3(0,.009f,.083f),new Vector3(.112f,.079f,.061f),10,SteelEdge,leg);
                    var ankle=bones[side<0?FootL:FootR];
                    b.Profile(Vector3.zero,Quaternion.identity,new[]
                    {
                        new Ring(.145f,.078f,.094f,ankle.z,ankle.x),
                        new Ring(.237f,.087f,.110f,Mathf.Lerp(ankle.z,knee.z,.25f),Mathf.Lerp(ankle.x,knee.x,.25f)),
                        new Ring(.392f,.108f,.122f,Mathf.Lerp(ankle.z,knee.z,.61f),Mathf.Lerp(ankle.x,knee.x,.61f)),
                        new Ring(.515f,.107f,.113f,knee.z,knee.x),new Ring(.555f,.088f,.095f,knee.z,knee.x)
                    },12,Steel,leg,0,-1.22f,2.44f);
                    var hip = bones[side < 0 ? UpperLegL : UpperLegR];
                    b.Plate(new[] { new Vector2(-.107f,.086f),new Vector2(-.13f,-.088f),new Vector2(.111f,-.103f),new Vector2(.112f,.085f) },
                        new Vector3(side*.244f,.971f,.073f),Quaternion.Euler(0,side*47,side*5),.023f,.013f,.01f,Steel,Gold,Hips);
                }
            }
            Belt(b,true);
            if(!hero)Helmet(b);
            if (b.Lod < 2)
            {
                Horizon(b,new Vector3(-.095f,1.30f,.284f),Quaternion.identity,.28f,Spine);
                Pouch(b,new Vector3(.292f,1.006f,-.026f),Quaternion.Euler(0,72,0),Hips);
                // Belt sword is secondary decoration; the reedguard's tactical weapon remains its lance.
                var swordStart = new Vector3(.302f,.978f,-.045f); var swordEnd = new Vector3(.42f,.48f,-.10f);
                b.Limb(swordStart,swordEnd,new[] { new Vector2(.038f,.025f),new Vector2(.041f,.027f),new Vector2(.026f,.019f),new Vector2(.014f,.014f) },8,LeatherDark,Hips);
                b.Limb(swordStart,new Vector3(.268f,1.119f,-.034f),new[] { new Vector2(.022f,.024f),new Vector2(.021f,.021f) },8,Leather,Hips);
                b.Curve(new[] { new Vector3(.229f,1.007f,-.028f),new Vector3(.292f,1.007f,-.029f),new Vector3(.36f,1.024f,-.034f) },.013f,6,Gold,Hips);
            }

            if(hero)
            {
                HeroSword(b,bones[HandR]);
                b.Plate(new[]{new Vector2(-.078f,.064f),new Vector2(-.089f,-.051f),new Vector2(0,-.084f),new Vector2(.086f,-.047f),new Vector2(.075f,.067f)},bones[HandL]+new Vector3(0,.005f,.065f),Quaternion.Euler(0,-17,0),.016f,.011f,.009f,Steel,Gold,HandL);
                return;
            }
            var shieldCentre = new Vector3(-.454f,1.175f,.375f);
            var shieldRotation = Quaternion.Euler(3,-12,-6);
            var outline = new[] { new Vector2(-.219f,.384f),new Vector2(-.291f,.28f),new Vector2(-.26f,-.145f),new Vector2(0,-.487f),new Vector2(.26f,-.145f),new Vector2(.291f,.28f),new Vector2(.219f,.384f) };
            b.Plate(outline,shieldCentre,shieldRotation,.040f,.035f,.036f,Team,Gold,HandL);
            Horizon(b,shieldCentre + shieldRotation * new Vector3(0,.01f,.083f),shieldRotation,.77f,HandL);
            if (b.Lod == 0)
            {
                foreach (var point in outline)
                {
                    var rivet = shieldCentre + shieldRotation * new Vector3(point.x*.945f,point.y*.945f,.019f);
                    b.Oval(rivet,new Vector3(.012f,.012f,.009f),6,SteelEdge,HandL);
                }
            }
            var shaftBottom = new Vector3(.541f,.15f,.247f);
            var shaftTop = new Vector3(.541f,2.623f,.247f);
            b.Limb(shaftBottom,shaftTop,new[] { new Vector2(.027f,.027f),new Vector2(.036f,.036f),new Vector2(.030f,.030f) },12,Wood,HandR);
            b.Limb(new Vector3(.541f,2.511f,.247f),new Vector3(.541f,2.653f,.247f),new[] { new Vector2(.041f,.041f),new Vector2(.035f,.035f),new Vector2(.027f,.027f) },12,Gold,HandR);
            b.Plate(new[] { new Vector2(-.035f,-.115f),new Vector2(-.105f,-.01f),new Vector2(0,.260f),new Vector2(.105f,-.01f),new Vector2(.035f,-.115f) },
                new Vector3(.541f,2.664f,.247f),Quaternion.identity,.011f,.012f,.025f,SteelEdge,Steel,HandR);
            if (b.Lod < 2)
            {
                for (int wrap = 0; wrap < 4; wrap++)
                    b.Limb(new Vector3(.541f,1.013f+wrap*.051f,.247f),new Vector3(.541f,1.042f+wrap*.051f,.247f),new[] { new Vector2(.040f,.040f),new Vector2(.039f,.039f) },10,Leather,HandR);
            }
        }

        private static void HeroSword(Builder b,Vector3 hand)
        {
            var direction=new Vector3(.24f,-.95f,.18f).normalized;
            var guard=hand+direction*.10f+Vector3.forward*.025f;
            b.Limb(hand-direction*.13f,guard,new[]{new Vector2(.031f,.033f),new Vector2(.029f,.032f),new Vector2(.031f,.032f)},12,LeatherDark,HandR);
            var across=Vector3.Cross(direction,Vector3.forward).normalized;
            b.Blade(new[]{guard+direction*.026f,guard+direction*.18f,guard+direction*.69f,guard+direction*.90f},new[]{.056f,.082f,.060f,.001f},.026f,SteelEdge,HandR);
            b.Curve(new[]{guard-across*.19f-direction*.047f,guard-across*.10f,guard+across*.10f,guard+across*.19f-direction*.047f},.024f,10,Gold,HandR);
            b.Oval(hand-direction*.16f,new Vector3(.045f,.048f,.039f),12,Gold,HandR);
            b.Plate(EllipseOutline(.033f,.037f,10),guard+Vector3.forward*.036f,Quaternion.identity,.01f,.006f,.008f,Crystal,Gold,HandR);
        }

        private static void Helmet(Builder b)
        {
            b.Profile(Vector3.zero,Quaternion.identity,new[]
            {
                new Ring(1.977f,.18f,.158f,.004f),new Ring(2.016f,.178f,.157f),new Ring(2.078f,.154f,.145f,-.004f),
                new Ring(2.147f,.10f,.106f,-.009f),new Ring(2.189f,.029f,.038f,-.013f)
            },20,Steel,Head);
            b.EllipseCurve(new Vector3(0,1.978f,.004f),.183f,.163f,.011f,24,Gold,Head);
            b.Profile(Vector3.zero,Quaternion.identity,new[] { new Ring(1.797f,.153f,.129f,-.003f),new Ring(1.873f,.175f,.15f,-.003f),new Ring(1.984f,.179f,.158f) },18,SteelDark,Head,0,.94f,Mathf.PI*2-1.88f);
            for (int side = -1; side <= 1; side += 2)
            {
                b.Plate(new[] { new Vector2(-.032f,.105f),new Vector2(-.049f,-.072f),new Vector2(.014f,-.095f),new Vector2(.045f,.094f) },
                    new Vector3(side*.147f,1.875f,.116f),Quaternion.Euler(0,side*39,side*-8),.013f,.01f,.01f,Steel,Gold,Head);
            }
            if (b.Lod < 2)
            {
                b.Ribbon(new[] { new Vector3(0,1.982f,.174f),new Vector3(0,2.083f,.129f),new Vector3(0,2.202f,-.013f),new Vector3(0,2.055f,-.149f) },.026f,.008f,SteelEdge,Head);
                b.Plate(new[] { new Vector2(-.022f,.018f),new Vector2(0,-.026f),new Vector2(.022f,.018f) },new Vector3(0,1.984f,.177f),Quaternion.identity,.008f,.002f,.004f,Gold,Gold,Head);
            }
        }

        private static void Belt(Builder b, bool warrior)
        {
            float depth=warrior?.228f:.210f;
            b.Profile(Vector3.zero,Quaternion.identity,new[] { new Ring(1.027f,.252f,depth-.013f),new Ring(1.037f,.263f,depth),new Ring(1.102f,.261f,depth),new Ring(1.109f,.25f,depth-.013f) },16,Leather,Hips);
            var buckle = new Vector3(.018f,1.068f,depth+.012f);
            b.Plate(new[] { new Vector2(-.053f,.042f),new Vector2(-.053f,-.042f),new Vector2(.053f,-.042f),new Vector2(.053f,.042f) },buckle,Quaternion.identity,.013f,.009f,.002f,LeatherDark,Gold,Hips);
            b.Ribbon(new[] { buckle+new Vector3(0,-.027f,.018f),buckle+new Vector3(0,.027f,.018f) },.009f,.003f,Gold,Hips);
            if (b.Lod == 0)
                for (int rivet = 0; rivet < 3; rivet++)
                    b.Oval(new Vector3(-.084f-rivet*.042f,1.069f,.184f-rivet*.013f),new Vector3(.005f,.005f,.006f),6,Gold,Hips);
        }

        private static void Pouch(Builder b, Vector3 centre, Quaternion rotation, int bone)
        {
            b.Profile(centre,rotation,new[] { new Ring(-.117f,.049f,.035f),new Ring(-.098f,.079f,.052f),new Ring(.082f,.084f,.052f),new Ring(.112f,.06f,.038f) },10,Leather,bone);
            b.Plate(new[] { new Vector2(-.074f,.052f),new Vector2(-.069f,-.035f),new Vector2(0,-.071f),new Vector2(.069f,-.035f),new Vector2(.074f,.052f) },
                centre+rotation*new Vector3(0,.03f,.053f),rotation,.008f,.008f,.005f,LeatherLight,LeatherDark,bone);
            b.Oval(centre+rotation*new Vector3(0,-.02f,.070f),new Vector3(.010f,.01f,.006f),6,Gold,bone);
        }

        // An original forked horizon mark: a stem and two rising paths above one broad horizon.
        private static void Horizon(Builder b, Vector3 centre, Quaternion rotation, float scale, int bone)
        {
            var branches = new[]
            {
                new[] { new Vector3(0,-.20f,0),new Vector3(0,.105f,0),new Vector3(-.14f,.235f,0) },
                new[] { new Vector3(0,.105f,0),new Vector3(.14f,.235f,0) },
                new[] { new Vector3(-.19f,-.045f,0),new Vector3(.19f,-.045f,0) }
            };
            foreach (var branch in branches)
            {
                var points = new Vector3[branch.Length];
                for (int i=0;i<points.Length;i++) points[i]=centre+rotation*(branch[i]*scale);
                b.Ribbon(points,.047f*scale,.006f,Gold,bone,rotation*Vector3.forward);
            }
        }

        private static void ClothPanel(Builder b, float left, float right, float low, float high, Paint paint, int bone, bool back, float clearance, Paint? trim=null)
        {
            Func<float,float,Vector3> sample=(u,v) =>
            {
                float y=Mathf.Lerp(low,high,v); float x=Mathf.Lerp(left,right,u);
                float waist=1-Mathf.Clamp01(Mathf.Abs(y-1.08f)/.48f);
                float spread=1-.17f*waist;
                float bodyWidth=y>1.08f?Mathf.Lerp(.238f,.307f,Mathf.Clamp01((y-1.08f)/.4f)):Mathf.Lerp(.282f,.238f,Mathf.Clamp01((y-.70f)/.38f));
                float bodyDepth=y>1.10f?Mathf.Lerp(.161f,.187f,Mathf.Clamp01((y-1.10f)/.33f)):Mathf.Lerp(.213f,.161f,Mathf.Clamp01((y-.75f)/.35f));
                float relativeX=x*spread/bodyWidth;
                float z=bodyDepth*Mathf.Sqrt(Mathf.Max(.10f,1-relativeX*relativeX))+clearance;
                z+=Mathf.Sin(u*Mathf.PI*3+.3f)*.012f*(1-v*.40f);
                return new Vector3(x*spread,y+(1-v)*.012f*Mathf.Cos(u*Mathf.PI*2),back?-z:z);
            };
            var front=back?Vector3.back:Vector3.forward;
            int columns=b.Lod==0?4:b.Lod==1?3:2;
            b.Sheet(sample,columns,b.Lod==0?5:b.Lod==1?3:2,paint,bone,front,true);
            if(trim.HasValue)
                b.Sheet((u,v)=>sample(u,v*.018f/(high-low))+front*.003f,columns,1,trim.Value,bone,front,false);
        }

        private static Vector2[] EllipseOutline(float width, float height, int sides)
        {
            var points=new Vector2[sides];
            for(int i=0;i<sides;i++){float a=i*Mathf.PI*2/sides;points[i]=new Vector2(Mathf.Cos(a)*width,Mathf.Sin(a)*height);}
            return points;
        }

    }
}
