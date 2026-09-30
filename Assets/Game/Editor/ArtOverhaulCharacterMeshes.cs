using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Emberfield.Editor
{
    public static partial class ArtOverhaulCharacters
    {
        private readonly struct Paint
        {
            public readonly Color Color;
            public readonly Vector2 Surface; public readonly int Tile;
            public Paint(float r, float g, float b, float metallic = 0, float smoothness = .32f, float team = 0, int tile = 0)
            { Color = new Color(r, g, b, team); Surface = new Vector2(metallic, smoothness); Tile = tile; }
        }

        private static readonly Paint Linen = new Paint(.83f, .79f, .65f);
        private static readonly Paint Ivory = new Paint(.91f, .89f, .80f);
        private static readonly Paint Team = new Paint(.88f, .93f, 1f, 0, .32f, 1);
        private static readonly Paint Leather = new Paint(.26f, .12f, .055f, 0, .30f, tile: 1);
        private static readonly Paint LeatherLight = new Paint(.43f, .24f, .095f, 0, .35f, tile: 1);
        private static readonly Paint LeatherDark = new Paint(.095f, .061f, .038f, 0, .26f, tile: 1);
        private static readonly Paint Trousers = new Paint(.24f, .24f, .19f, 0, .24f, tile: 0);
        private static readonly Paint Steel = new Paint(.48f, .60f, .67f, .82f, .62f, tile: 3);
        private static readonly Paint SteelEdge = new Paint(.72f, .79f, .80f, .88f, .69f, tile: 3);
        private static readonly Paint SteelDark = new Paint(.18f, .25f, .29f, .65f, .42f, tile: 3);
        private static readonly Paint Gold = new Paint(.82f, .51f, .16f, .76f, .52f, tile: 3);
        private static readonly Paint Skin = new Paint(.68f, .39f, .22f, 0, .39f, tile: 13);
        private static readonly Paint SkinLight = new Paint(.79f, .49f, .30f, 0, .40f, tile: 13);
        private static readonly Paint SkinShade = new Paint(.48f, .235f, .135f, 0, .33f, tile: 13);
        private static readonly Paint Hair = new Paint(.105f, .061f, .035f, 0, .27f, tile: 1);
        private static readonly Paint HairLight = new Paint(.19f, .105f, .046f, 0, .29f, tile: 1);
        private static readonly Paint Eye = new Paint(.53f, .52f, .44f, 0, .35f, tile: 13);
        private static readonly Paint Iris = new Paint(.075f, .095f, .072f, 0, .45f, tile: 13);
        private static readonly Paint Straw = new Paint(.69f, .45f, .16f, 0, .30f, tile: 12);
        private static readonly Paint StrawLight = new Paint(.85f, .63f, .27f, 0, .33f, tile: 12);
        private static readonly Paint Wood = new Paint(.34f, .19f, .078f, 0, .31f, tile: 2);

        private readonly struct Ring
        {
            public readonly float Y, X, Z, OffsetX, OffsetZ;
            public Ring(float y, float x, float z, float offsetZ = 0, float offsetX = 0)
            { Y = y; X = x; Z = z; OffsetZ = offsetZ; OffsetX = offsetX; }
        }

        private sealed class Builder
        {
            public readonly int Lod;
            private readonly List<Vector3> vertices=new List<Vector3>();
            private readonly List<Vector3> normals=new List<Vector3>();
            private readonly List<Color> colors=new List<Color>();
            private readonly List<Vector2> uv=new List<Vector2>();
            private readonly List<Vector2> surface=new List<Vector2>(); private readonly List<Vector2> atlas=new List<Vector2>();
            private readonly List<BoneWeight> weights=new List<BoneWeight>();
            private readonly List<int> triangles=new List<int>();
            public Builder(int lod){Lod=lod;}
            public int VertexCount=>vertices.Count;
            public void ScalePatch(Vector3 centre,Quaternion rotation,Vector2 size,float bulge,Paint paint,int bone)
            {
                int sides=Lod==0?10:Lod==1?8:6,rings=Lod==0?2:1;
                int first=vertices.Count;Add(centre+rotation*new Vector3(0,0,bulge),rotation*Vector3.forward,new Vector2(.5f,.5f),paint,bone);
                for(int ring=1;ring<=rings;ring++)
                {
                    float radius=(float)ring/rings;
                    for(int i=0;i<sides;i++)
                    {
                        float angle=i*Mathf.PI*2/sides;float x=Mathf.Cos(angle)*radius,y=Mathf.Sin(angle)*radius;
                        float taper=1-.28f*Mathf.Max(0,-y);
                        var local=new Vector3(x*size.x*taper,y*size.y,bulge*(1-radius*radius));
                        var normal=UnitNormal(new Vector3(2*x*bulge/size.x,2*y*bulge/size.y,1));
                        Add(centre+rotation*local,rotation*normal,new Vector2(.5f+x*.45f,.5f+y*.45f),paint,bone);
                    }
                }
                for(int i=0;i<sides;i++)Tri(first,first+1+i,first+1+(i+1)%sides);
                for(int ring=1;ring<rings;ring++)for(int i=0;i<sides;i++)
                {
                    int a=first+1+(ring-1)*sides+i,c=first+1+ring*sides+i,n=(i+1)%sides;
                    Tri(a,c,first+1+ring*sides+n);Tri(a,first+1+ring*sides+n,first+1+(ring-1)*sides+n);
                }
            }
            public void BlendRange(int first,int rootBone,int tipBone,Vector3 root,Vector3 hinge)
            {
                var axis=hinge-root;float lengthSquared=axis.sqrMagnitude;
                for(int i=first;i<vertices.Count;i++)
                {
                    float projection=Vector3.Dot(vertices[i]-root,axis)/lengthSquared;
                    float tip=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.35f,1.15f,projection));
                    weights[i]=tip>=.5f
                        ?new BoneWeight{boneIndex0=tipBone,weight0=tip,boneIndex1=rootBone,weight1=1-tip}
                        :new BoneWeight{boneIndex0=rootBone,weight0=1-tip,boneIndex1=tipBone,weight1=tip};
                }
            }
            public void RemapRider(Vector3[] original,Vector3[] mounted)
            {
                for(int i=0;i<vertices.Count;i++)
                {
                    int bone=weights[i].weight1>weights[i].weight0?weights[i].boneIndex1:weights[i].boneIndex0;
                    if(bone==Root||bone>=16)continue;
                    Quaternion rotation=Quaternion.identity;
                    if(bone==UpperLegL||bone==UpperLegR)rotation=Quaternion.FromToRotation(original[bone+1]-original[bone],mounted[bone+1]-mounted[bone]);
                    else if(bone==LowerLegL||bone==LowerLegR)rotation=Quaternion.FromToRotation(original[bone+1]-original[bone],mounted[bone+1]-mounted[bone]);
                    vertices[i]=mounted[bone]+rotation*(vertices[i]-original[bone]);normals[i]=rotation*normals[i];
                }
            }
            public void Blade(Vector3[] points,float[] widths,float thickness,Paint paint,int bone)
            {
                for(int i=0;i<points.Length-1;i++)
                {
                    var direction=(points[i+1]-points[i]).normalized;var across=Vector3.Cross(direction,Vector3.forward).normalized;
                    if(across.sqrMagnitude<.1f)across=Vector3.right;
                    var a=points[i]-across*widths[i];var c=points[i]+across*widths[i];
                    var d=points[i+1]+across*widths[i+1];var e=points[i+1]-across*widths[i+1];
                    foreach(int side in new[]{-1,1})
                    {
                        var midA=points[i]+Vector3.forward*thickness*side;var midB=points[i+1]+Vector3.forward*thickness*side;
                        if(side>0){Flat(a,midA,midB,paint,bone);Flat(a,midB,e,paint,bone);Flat(midA,c,d,paint,bone);Flat(midA,d,midB,paint,bone);}
                        else{Flat(a,midB,midA,paint,bone);Flat(a,e,midB,paint,bone);Flat(midA,d,c,paint,bone);Flat(midA,midB,d,paint,bone);}
                    }
                }
            }
            public void TaperCurve(Vector3[] points,Vector2[] radii,int sides,Paint paint,int bone)
            {
                int count=Sides(sides);int first=vertices.Count;
                for(int i=0;i<points.Length;i++)
                {
                    var direction=(points[Mathf.Min(i+1,points.Length-1)]-points[Mathf.Max(0,i-1)]).normalized;
                    var axis=Vector3.Cross(direction,Mathf.Abs(direction.z)<.9f?Vector3.forward:Vector3.up).normalized;var other=Vector3.Cross(direction,axis).normalized;
                    for(int j=0;j<=count;j++)
                    {
                        float a=j*Mathf.PI*2/count;var normal=axis*Mathf.Cos(a)+other*Mathf.Sin(a);
                        var p=points[i]+axis*Mathf.Cos(a)*radii[i].x+other*Mathf.Sin(a)*radii[i].y;
                        Add(p,normal,new Vector2((float)j/count,(float)i/(points.Length-1)),paint,bone);
                    }
                }
                for(int i=0;i<points.Length-1;i++)for(int j=0;j<count;j++){int a=first+i*(count+1)+j,c=a+count+1;Tri(a,a+1,c+1);Tri(a,c+1,c);}
                for(int j=0;j<count;j++){Flat(points[0],vertices[first+j+1],vertices[first+j],paint,bone);int last=first+(points.Length-1)*(count+1);Flat(points[points.Length-1],vertices[last+j],vertices[last+j+1],paint,bone);}
            }
            // Called just after the base torso: preserve the pelvis seam during a bend,
            // without deforming rigid gear, the backpack or the weapon attachments.
            public void BlendWaist()
            {
                for(int i=0;i<vertices.Count;i++)
                {
                    float spine=Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.05f,1.30f,vertices[i].y));
                    weights[i]=spine>=.5f
                        ?new BoneWeight{boneIndex0=Spine,weight0=spine,boneIndex1=Hips,weight1=1-spine}
                        :new BoneWeight{boneIndex0=Hips,weight0=1-spine,boneIndex1=Spine,weight1=spine};
                }
            }
            private int Sides(int high){return Lod==0?high:Lod==1?Mathf.Max(6,Mathf.RoundToInt(high*.64f)):Mathf.Max(5,high/3);}
            // Unity's Vector3.normalized discards vectors shorter than 1e-5. Finite-difference
            // surface derivatives legitimately produce smaller cross-products on facial detail.
            private static Vector3 UnitNormal(Vector3 value)
            {
                float squared=value.sqrMagnitude;
                return squared>1e-24f&&!float.IsNaN(squared)&&!float.IsInfinity(squared)?value/Mathf.Sqrt(squared):Vector3.zero;
            }
            private int Add(Vector3 position,Vector3 normal,Vector2 tex,Paint paint,int bone)
            {
                int index=vertices.Count;vertices.Add(position);normals.Add(UnitNormal(normal));colors.Add(paint.Color);
                uv.Add(paint.Tile==13?new Vector2(.46f+tex.x*.08f,.46f+tex.y*.08f):tex);surface.Add(paint.Surface);atlas.Add(new Vector2(paint.Tile,0));weights.Add(new BoneWeight{boneIndex0=bone,weight0=1});return index;
            }
            private void Tri(int a,int b,int c)
            {
                if(Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]).sqrMagnitude<1e-14f)return;
                triangles.Add(a);triangles.Add(b);triangles.Add(c);
            }
            private void Flat(Vector3 a,Vector3 b,Vector3 c,Paint paint,int bone)
            {
                var normal=UnitNormal(Vector3.Cross(b-a,c-a));if(normal.sqrMagnitude<.01f)return;
                int first=Add(a,normal,Vector2.zero,paint,bone);Add(b,normal,Vector2.right,paint,bone);Add(c,normal,Vector2.up,paint,bone);Tri(first,first+1,first+2);
            }
            private Ring[] Reduce(Ring[] source)
            {
                if(Lod==0||source.Length<=3)return source;
                if(Lod==2)
                {
                    int widest=1;
                    for(int i=2;i<source.Length-1;i++)if(source[i].X*source[i].Z>source[widest].X*source[widest].Z)widest=i;
                    return new[]{source[0],source[widest],source[source.Length-1]};
                }
                if(source.Length<=5)return source;
                var result=new List<Ring>{source[0]};
                for(int i=1;i<source.Length-1;i+=2)result.Add(source[i]);
                result.Add(source[source.Length-1]);return result.ToArray();
            }
            private static Vector3 RingPoint(Ring ring,float angle,float fold)
            {
                float ripple=1+Mathf.Cos(angle*6)*fold;
                return new Vector3(ring.OffsetX+Mathf.Sin(angle)*ring.X*ripple,ring.Y,ring.OffsetZ+Mathf.Cos(angle)*ring.Z*ripple);
            }
            public void Profile(Vector3 origin,Quaternion rotation,Ring[] source,int sides,Paint paint,int bone,float fold=0,float start=0,float arc=Mathf.PI*2)
            {
                var rings=Reduce(source);int count=Sides(sides);int first=vertices.Count;
                for(int r=0;r<rings.Length;r++)
                {
                    var before=rings[Mathf.Max(0,r-1)];var after=rings[Mathf.Min(rings.Length-1,r+1)];
                    for(int j=0;j<=count;j++)
                    {
                        float angle=start+arc*j/count;var p=RingPoint(rings[r],angle,fold);
                        var tangent=RingPoint(rings[r],angle+.002f,fold)-RingPoint(rings[r],angle-.002f,fold);
                        var vertical=RingPoint(after,angle,fold)-RingPoint(before,angle,fold);
                        var n=UnitNormal(Vector3.Cross(tangent,vertical));
                        Add(origin+rotation*p,rotation*n,new Vector2((float)j/count,(float)r/(rings.Length-1)),paint,bone);
                    }
                }
                for(int r=0;r<rings.Length-1;r++)for(int j=0;j<count;j++)
                {int a=first+r*(count+1)+j;int c=a+count+1;Tri(a,a+1,c+1);Tri(a,c+1,c);}
                for(int j=0;j<count;j++)
                {
                    var bottom=rings[0];var top=rings[rings.Length-1];
                    var lowCentre=origin+rotation*new Vector3(bottom.OffsetX,bottom.Y,bottom.OffsetZ);
                    var highCentre=origin+rotation*new Vector3(top.OffsetX,top.Y,top.OffsetZ);
                    Flat(lowCentre,origin+rotation*RingPoint(bottom,start+arc*(j+1)/count,fold),origin+rotation*RingPoint(bottom,start+arc*j/count,fold),paint,bone);
                    Flat(highCentre,origin+rotation*RingPoint(top,start+arc*j/count,fold),origin+rotation*RingPoint(top,start+arc*(j+1)/count,fold),paint,bone);
                }
                if(arc<Mathf.PI*2-.001f)
                {
                    for(int r=0;r<rings.Length-1;r++)foreach(float angle in new[]{start,start+arc})
                    {
                        var a=origin+rotation*RingPoint(rings[r],angle,fold);var d=origin+rotation*RingPoint(rings[r+1],angle,fold);
                        var c=origin+rotation*new Vector3(rings[r+1].OffsetX,rings[r+1].Y,rings[r+1].OffsetZ);
                        var e=origin+rotation*new Vector3(rings[r].OffsetX,rings[r].Y,rings[r].OffsetZ);
                        if(angle==start){Flat(a,d,c,paint,bone);Flat(a,c,e,paint,bone);}else{Flat(a,c,d,paint,bone);Flat(a,e,c,paint,bone);}
                    }
                }
            }
            public void Limb(Vector3 from,Vector3 to,Vector2[] radii,int sides,Paint paint,int bone,float fold=0)
            {
                var axis=(to-from).normalized;var forward=Vector3.ProjectOnPlane(Vector3.forward,axis);
                if(forward.sqrMagnitude<.01f)forward=Vector3.ProjectOnPlane(Vector3.up,axis);
                var q=Quaternion.LookRotation(forward.normalized,axis);float length=(to-from).magnitude;
                var rings=new Ring[radii.Length];
                for(int i=0;i<rings.Length;i++)rings[i]=new Ring(length*i/(rings.Length-1),radii[i].x,radii[i].y);
                Profile(from,q,rings,sides,paint,bone,fold);
            }
            public void Oval(Vector3 centre,Vector3 radius,int sides,Paint paint,int bone)
            {
                float extent=Mathf.Max(radius.x,Mathf.Max(radius.y,radius.z));
                int count=extent<.025f?3:Lod==0?5:Lod==1?4:3;var rings=new Ring[count];
                for(int i=0;i<count;i++)
                {float t=Mathf.Lerp(-.975f,.975f,(float)i/(count-1));float w=Mathf.Sqrt(1-t*t);rings[i]=new Ring(t*radius.y,w*radius.x,w*radius.z);}
                Profile(centre,Quaternion.identity,rings,sides,paint,bone);
            }
            public void Curve(Vector3[] points,float radius,int sides,Paint paint,int bone)
            {
                if(points.Length<2)return;int count=Sides(sides);int first=vertices.Count;
                for(int i=0;i<points.Length;i++)
                {
                    var direction=(points[Mathf.Min(i+1,points.Length-1)]-points[Mathf.Max(0,i-1)]).normalized;
                    var axis=Vector3.Cross(direction,Mathf.Abs(direction.z)<.8f?Vector3.forward:Vector3.up).normalized;
                    var other=Vector3.Cross(direction,axis).normalized;
                    for(int j=0;j<=count;j++)
                    {float a=j*Mathf.PI*2/count;var normal=axis*Mathf.Cos(a)+other*Mathf.Sin(a);Add(points[i]+normal*radius,normal,new Vector2((float)j/count,(float)i/(points.Length-1)),paint,bone);}
                }
                for(int i=0;i<points.Length-1;i++)for(int j=0;j<count;j++)
                {int a=first+i*(count+1)+j;int c=a+count+1;Tri(a,a+1,c+1);Tri(a,c+1,c);}
                for(int j=0;j<count;j++)
                {
                    Flat(points[0],vertices[first+j+1],vertices[first+j],paint,bone);
                    int last=first+(points.Length-1)*(count+1);Flat(points[points.Length-1],vertices[last+j],vertices[last+j+1],paint,bone);
                }
            }
            public void EllipseCurve(Vector3 centre,float width,float depth,float radius,int samples,Paint paint,int bone)
            {
                int count=Sides(samples);var points=new Vector3[count+1];
                for(int i=0;i<=count;i++){float a=i*Mathf.PI*2/count;points[i]=centre+new Vector3(Mathf.Sin(a)*width,0,Mathf.Cos(a)*depth);}
                Curve(points,radius,6,paint,bone);
            }
            public void Plate(Vector2[] outline,Vector3 centre,Quaternion rotation,float depth,float bevel,float bulge,Paint face,Paint edge,int bone)
            {
                float extent=0;foreach(var point in outline)extent=Mathf.Max(extent,Mathf.Max(Mathf.Abs(point.x),Mathf.Abs(point.y)));
                float inset=1-Mathf.Clamp(bevel/Mathf.Max(.001f,extent),0,.45f);
                // Accept either winding. Front is always local +Z.
                float area=0;for(int i=0;i<outline.Length;i++){var a=outline[i];var c=outline[(i+1)%outline.Length];area+=a.x*c.y-c.x*a.y;}
                var top=centre+rotation*new Vector3(0,0,depth+bulge);var back=centre-rotation*Vector3.forward*.012f;
                for(int i=0;i<outline.Length;i++)
                {
                    int j=(i+1)%outline.Length;var a=outline[area>0?i:j];var c=outline[area>0?j:i];
                    var ao=centre+rotation*new Vector3(a.x,a.y,0);var co=centre+rotation*new Vector3(c.x,c.y,0);
                    var ai=centre+rotation*new Vector3(a.x*inset,a.y*inset,depth);var ci=centre+rotation*new Vector3(c.x*inset,c.y*inset,depth);
                    Flat(ai,ci,top,face,bone);Flat(ao,co,ci,edge,bone);Flat(ao,ci,ai,edge,bone);Flat(co,ao,back,edge,bone);
                }
            }
            public void Ribbon(Vector3[] points,float width,float depth,Paint paint,int bone,Vector3? normalOverride=null)
            {
                var normal=normalOverride??Vector3.forward;
                for(int i=0;i<points.Length-1;i++)
                {
                    var direction=(points[i+1]-points[i]).normalized;var across=Vector3.Cross(direction,normal).normalized*width*.5f;
                    var a=points[i]-across;var c=points[i]+across;var d=points[i+1]+across;var e=points[i+1]-across;
                    var raised=normal*depth;
                    if(Vector3.Dot(Vector3.Cross(c-a,d-a),normal)<0){var swap=a;a=c;c=swap;swap=d;d=e;e=swap;}
                    Flat(a+raised,c+raised,d+raised,paint,bone);Flat(a+raised,d+raised,e+raised,paint,bone);
                    if(Lod<2)
                    {
                        Flat(a,e,e+raised,paint,bone);Flat(a,e+raised,a+raised,paint,bone);
                        Flat(c+raised,d+raised,d,paint,bone);Flat(c+raised,d,c,paint,bone);
                    }
                }
            }
            public void Sheet(Func<float,float,Vector3> sample,int columns,int rows,Paint paint,int bone,Vector3 forward,bool twoSided)
            {
                int first=vertices.Count;
                for(int y=0;y<=rows;y++)for(int x=0;x<=columns;x++)
                {
                    float u=(float)x/columns,v=(float)y/rows;var p=sample(u,v);
                    var du=sample(Mathf.Min(1,u+.001f),v)-sample(Mathf.Max(0,u-.001f),v);
                    var dv=sample(u,Mathf.Min(1,v+.001f))-sample(u,Mathf.Max(0,v-.001f));
                    var n=UnitNormal(Vector3.Cross(du,dv));if(Vector3.Dot(n,forward)<0)n=-n;
                    Add(p,n,new Vector2(u,v),paint,bone);
                }
                for(int y=0;y<rows;y++)for(int x=0;x<columns;x++)
                {
                    int a=first+y*(columns+1)+x,c=a+columns+1;
                    bool reverse=Vector3.Dot(Vector3.Cross(vertices[a+1]-vertices[a],vertices[c+1]-vertices[a]),forward)<0;
                    if(reverse){Tri(a,c+1,a+1);Tri(a,c,c+1);}else{Tri(a,a+1,c+1);Tri(a,c+1,c);}
                }
                if(!twoSided)return;
                int backFirst=vertices.Count;int count=(columns+1)*(rows+1);
                for(int i=0;i<count;i++)Add(vertices[first+i]-normals[first+i]*.003f,-normals[first+i],uv[first+i],paint,bone);
                for(int y=0;y<rows;y++)for(int x=0;x<columns;x++)
                {
                    int a=backFirst+y*(columns+1)+x,c=a+columns+1;
                    bool reverse=Vector3.Dot(Vector3.Cross(vertices[a+1]-vertices[a],vertices[c+1]-vertices[a]),-forward)<0;
                    if(reverse){Tri(a,c+1,a+1);Tri(a,c,c+1);}else{Tri(a,a+1,c+1);Tri(a,c+1,c);}
                }
            }
            private void RepairNormalsAndCompact()
            {
                var used=new bool[vertices.Count];var weighted=new Vector3[vertices.Count];var largest=new Vector3[vertices.Count];
                for(int i=0;i<triangles.Count;i+=3)
                {
                    int a=triangles[i],c=triangles[i+1],d=triangles[i+2];
                    var face=Vector3.Cross(vertices[c]-vertices[a],vertices[d]-vertices[a]);
                    foreach(int index in new[]{a,c,d})
                    {
                        used[index]=true;weighted[index]+=face;
                        if(face.sqrMagnitude>largest[index].sqrMagnitude)largest[index]=face;
                    }
                }
                var remap=new int[vertices.Count];int write=0;
                for(int read=0;read<vertices.Count;read++)
                {
                    if(!used[read]){remap[read]=-1;continue;}
                    var normal=normals[read];
                    if(float.IsNaN(normal.sqrMagnitude)||float.IsInfinity(normal.sqrMagnitude)||normal.sqrMagnitude<.25f)
                    {
                        normal=UnitNormal(weighted[read]);
                        if(normal.sqrMagnitude<.25f)normal=UnitNormal(largest[read]);
                        if(normal.sqrMagnitude<.25f)throw new InvalidOperationException("Referenced mesh vertex has no finite incident surface normal.");
                    }
                    remap[read]=write;vertices[write]=vertices[read];normals[write]=normal;colors[write]=colors[read];
                    uv[write]=uv[read];surface[write]=surface[read];atlas[write]=atlas[read];weights[write]=weights[read];write++;
                }
                for(int i=0;i<triangles.Count;i++)triangles[i]=remap[triangles[i]];
                int removed=vertices.Count-write;
                vertices.RemoveRange(write,removed);normals.RemoveRange(write,removed);colors.RemoveRange(write,removed);
                uv.RemoveRange(write,removed);surface.RemoveRange(write,removed);atlas.RemoveRange(write,removed);weights.RemoveRange(write,removed);
            }
            public Mesh Finish(string name,Vector3[] restPositions)
            {
                RepairNormalsAndCompact();
                var mesh=new Mesh{name=name+" LOD"+Lod,indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
                mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetColors(colors);mesh.SetUVs(0,uv);mesh.SetUVs(1,surface);mesh.SetUVs(2,atlas);mesh.SetTriangles(triangles,0);
                mesh.boneWeights=weights.ToArray();var bindposes=new Matrix4x4[restPositions.Length];
                for(int i=0;i<bindposes.Length;i++)bindposes[i]=Matrix4x4.Translate(-restPositions[i]);
                mesh.bindposes=bindposes;mesh.RecalculateBounds();return mesh;
            }
        }
    }
}
