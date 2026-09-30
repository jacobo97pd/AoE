"""Blender-native tailored surfaces for the Kingdom character study.

Coordinates are metres, Z up, facing -Y. No Unity geometry is reused.
"""
import bpy, bmesh, math, random
from mathutils import Vector, Matrix
from math import sin, cos, pi

MATERIALS = {}
PARTS = []

def material(name, color, metallic=0.0, roughness=.65):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1)
    mat.use_nodes = True
    bs = mat.node_tree.nodes.get('Principled BSDF')
    bs.inputs['Base Color'].default_value = (*color, 1)
    bs.inputs['Metallic'].default_value = metallic
    bs.inputs['Roughness'].default_value = roughness
    if name.startswith('Skin'):
        bs.inputs['Subsurface Weight'].default_value = .065
        bs.inputs['Subsurface Radius'].default_value = (.8, .36, .18)
    MATERIALS[name] = {'name': name, 'baseColor': list(color)+[1], 'metallic': metallic,
                       'smoothness': 1-roughness, 'surface': 'Skin' if name.startswith('Skin') else name}
    return mat

def finish(obj, name, mat, smooth=True, part=True):
    obj.name = name
    if mat: obj.data.materials.append(mat if not isinstance(mat,str) else bpy.data.materials[mat])
    if obj.type == 'MESH':
        for p in obj.data.polygons: p.use_smooth = smooth
    if part: PARTS.append(obj)
    return obj

def mesh(name, verts, faces, mat, uv=None):
    data=bpy.data.meshes.new(name);data.from_pydata(verts, [], faces);data.update()
    obj=bpy.data.objects.new(name,data);bpy.context.collection.objects.link(obj)
    layer=data.uv_layers.new(name='UVMap')
    for poly in data.polygons:
        for li in poly.loop_indices:
            vi=data.loops[li].vertex_index
            co=Vector(verts[vi])
            layer.data[li].uv=uv[vi] if uv else ((co.x+.65)/1.3,co.z/1.9)
    bm=bmesh.new();bm.from_mesh(data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(data);bm.free();data.update()
    return finish(obj,name,mat)

def apply(obj):
    bpy.context.view_layer.objects.active=obj
    for mod in list(obj.modifiers):
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return obj

def subd(obj, levels=1, thickness=0, bevel=0):
    if levels:
        m=obj.modifiers.new('Tailored smooth surface','SUBSURF');m.levels=levels;m.render_levels=levels
    if thickness:
        m=obj.modifiers.new('Real fabric / metal thickness','SOLIDIFY');m.thickness=thickness;m.offset=0
    if bevel:
        m=obj.modifiers.new('Rounded fabrication edges','BEVEL');m.width=bevel;m.segments=2
    return obj

def sphere(name,center,scale,mat,segments=20,rings=12):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments,ring_count=rings,location=center)
    obj=bpy.context.object;obj.scale=scale
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    return finish(obj,name,mat)

def curve(name, points, radius, mat, resolution=3, cyclic=False, radii=None):
    data=bpy.data.curves.new(name,'CURVE');data.dimensions='3D';data.resolution_u=resolution;data.bevel_depth=radius;data.bevel_resolution=2
    s=data.splines.new('BEZIER');s.bezier_points.add(len(points)-1)
    for i,(p,co) in enumerate(zip(s.bezier_points,points)):
        p.co=co;p.handle_left_type='AUTO';p.handle_right_type='AUTO'
        if radii:p.radius=radii[i]
    s.use_cyclic_u=cyclic
    obj=bpy.data.objects.new(name,data);bpy.context.collection.objects.link(obj)
    finish(obj,name,mat)
    bpy.ops.object.select_all(action='DESELECT')
    bpy.context.view_layer.objects.active=obj;obj.select_set(True)
    bpy.ops.object.convert(target='MESH');obj.select_set(False)
    for p in obj.data.polygons:p.use_smooth=True
    return obj

def tube(name, a, b, r, mat, end_radius=None, vertices=20):
    a,b=Vector(a),Vector(b);d=b-a
    bpy.ops.mesh.primitive_cone_add(vertices=vertices,radius1=r,radius2=end_radius if end_radius is not None else r,depth=d.length,location=(a+b)/2)
    obj=bpy.context.object;obj.rotation_euler=d.to_track_quat('Z','Y').to_euler()
    bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
    return subd(finish(obj,name,mat),0,bevel=min(r*.25,.003))

def ring(name,center,rx,ry,width,mat,segments=64,wave=0):
    verts=[];uv=[]
    for row in range(3):
        for j in range(segments):
            a=2*pi*j/segments
            verts.append((center[0]+rx*cos(a),center[1]+ry*sin(a),center[2]+(row/2-.5)*width+wave*sin(3*a)))
            uv.append((j/segments,row/2))
    faces=[(r*segments+j,r*segments+(j+1)%segments,(r+1)*segments+(j+1)%segments,(r+1)*segments+j) for r in range(2) for j in range(segments)]
    return subd(mesh(name,verts,faces,mat,uv),1,.005)

def ribbon(name,points,width,mat,thickness=.003,subdivision=2):
    verts=[];uv=[]
    for i,p in enumerate(points):
        p=Vector(p)
        tangent=Vector(points[min(i+1,len(points)-1)])-Vector(points[max(0,i-1)])
        tangent.normalize()
        across=Vector((1,0,0))-tangent*tangent.x
        if across.length<.1:across=tangent.cross(Vector((0,-1,0)))
        side=across.normalized()*width/2
        for s in [-1,1]:verts.append(tuple(p+s*side));uv.append(((s+1)/2,i/max(1,len(points)-1)))
    faces=[(2*i,2*i+1,2*i+3,2*i+2) for i in range(len(points)-1)]
    return subd(mesh(name,verts,faces,mat,uv),subdivision,thickness)

def panel(name, rows, mat, cols=28, folds=.012, thickness=.004):
    """Draped front/back garment; rows = (z, halfWidth, centralY, edgeY)."""
    verts=[];uv=[]
    for i,(z,w,y,ey) in enumerate(rows):
        t=i/(len(rows)-1)
        for j in range(cols+1):
            u=j/cols;x=(u*2-1)*w
            ripple=folds*sin(u*pi*8+.45*sin(t*pi))*sin(pi*t*.8)
            yy=y+(ey-y)*abs(2*u-1)**1.65+ripple
            zz=z+(.007*sin(u*pi*5+.7) if i==len(rows)-1 else 0)
            verts.append((x,yy,zz));uv.append((u,t))
    faces=[(i*(cols+1)+j,i*(cols+1)+j+1,(i+1)*(cols+1)+j+1,(i+1)*(cols+1)+j) for i in range(len(rows)-1) for j in range(cols)]
    return subd(mesh(name,verts,faces,mat,uv),1,thickness)

def lathe(name,rings,mat,segments=64,fold=0):
    """Rings: z, rx, ry, cy. Organic ellipse sweep, not stacked primitives."""
    verts=[];uv=[]
    for i,(z,rx,ry,cy) in enumerate(rings):
        for j in range(segments):
            a=2*pi*j/segments;f=fold*(sin(a*11+i*.7)+.35*sin(a*7-i))
            verts.append(((rx+f)*cos(a),cy+(ry+f)*sin(a),z));uv.append((j/segments,i/(len(rings)-1)))
    faces=[(i*segments+j,i*segments+(j+1)%segments,(i+1)*segments+(j+1)%segments,(i+1)*segments+j) for i in range(len(rings)-1) for j in range(segments)]
    return mesh(name,verts,faces,mat,uv)

def shell(source,name,predicate,mat,offset=.006,subdivision=0,thickness=.003,folds=0):
    verts=[];uv=[];faces=[];mapping={};src=source.data
    for poly in src.polygons:
        c=poly.center
        if not predicate(c):continue
        face=[]
        for li in poly.loop_indices:
            vi=src.loops[li].vertex_index
            if vi not in mapping:
                v=src.vertices[vi];co=v.co+v.normal*offset
                if folds:
                    co+=v.normal*(folds*sin(co.z*65+co.x*13)*(.3+.7*abs(sin(co.x*23))))
                mapping[vi]=len(verts);verts.append(tuple(co))
                val=src.uv_layers.active.data[li].uv if src.uv_layers.active else (co.x,co.z)
                uv.append((val[0]/9,val[1]/4))
            face.append(mapping[vi])
        faces.append(face)
    if not faces:raise ValueError('Empty shell: '+name)
    obj=mesh(name,verts,faces,mat,uv)
    # Polygon-centre selection makes a stair-step boundary on a sculpt mesh.
    # Relax only that boundary before adding thickness, preserving face detail.
    bm=bmesh.new();bm.from_mesh(obj.data)
    boundary=[v for v in bm.verts if v.is_boundary]
    for iteration in range(5):
        nxt={v:sum((e.other_vert(v).co for e in v.link_edges if e.is_boundary),Vector())/max(1,sum(1 for e in v.link_edges if e.is_boundary)) for v in boundary}
        for v,co in nxt.items():v.co=v.co.lerp(co,.45)
    bm.to_mesh(obj.data);bm.free();obj.data.update()
    return subd(obj,subdivision,thickness)

def fleur(name,center,size,mat,normal_y=-1):
    """Modeled lily heraldry from curved lobes; public-domain traditional form."""
    x,y,z=center
    outlines=[[(0,-.50),(-.11,-.14),(-.10,.13),(0,.5),(.10,.13),(.11,-.14)],
              [(-.035,-.1),(-.27,.02),(-.33,.21),(-.24,.29),(-.13,.20),(-.1,.03)],
              [( .035,-.1),(.27,.02),(.33,.21),(.24,.29),(.13,.20),(.1,.03)],
              [(-.23,-.13),(-.23,-.2),(.23,-.2),(.23,-.13)]]
    for n,outline in enumerate(outlines):
        vv=[(x+a*size,y,z+b*size) for a,b in outline]
        obj=mesh(name+str(n),vv,[tuple(range(len(vv)))],mat)
        subd(obj,0,.003,.0015)

def buckle(name,center,width,height,mat):
    x,y,z=center
    curve(name,[(x-width/2,y,z-height/2),(x+width/2,y,z-height/2),(x+width/2,y,z+height/2),(x-width/2,y,z+height/2)],.003,mat,2,True)
    tube(name+' tongue',(x,y-.002,z-height*.3),(x,y-.002,z+height*.42),.0015,mat,vertices=8)

def leaf_plate(name,center,width,height,mat,bulge=.018):
    x,y,z=center;verts=[];uv=[]
    rows=10;cols=10
    for i in range(rows+1):
        v=i/rows;w=width*(.78+.22*sin(v*pi))
        for j in range(cols+1):
            u=j/cols;xx=(u-.5)*w
            verts.append((x+xx,y-bulge*(1-(2*u-1)**2)*sin(v*pi),z+(v-.5)*height))
            uv.append((u,v))
    faces=[(i*(cols+1)+j,i*(cols+1)+j+1,(i+1)*(cols+1)+j+1,(i+1)*(cols+1)+j) for i in range(rows) for j in range(cols)]
    return subd(mesh(name,verts,faces,mat,uv),1,.006,.001)
