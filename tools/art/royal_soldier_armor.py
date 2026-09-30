"""A new, deliberately designed Kingdom infantry harness.

Blender 4.5; metres, +Z up, facing -Y. Every plate is authored from its
fabrication profile, not extracted from an anatomical mesh. Standalone:
  blender -b -t 6 -P tools/art/royal_soldier_armor.py
Output is D:/CodexTooling/royal-soldier/armor.blend. Materials remain separate.
"""
import bpy, bmesh, math, os, sys, json
from math import sin, cos, pi, sqrt
from mathutils import Vector, Matrix

OUT = 'D:/CodexTooling/royal-soldier'
PARTS = []
MATS = {}


def materials():
    """Use the shared surface library; fallback supports isolated shape work."""
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    try:
        import royal_soldier_materials as shared
        MATS.update(shared.register_materials())
    except (ImportError, AttributeError):
        palette = {
            'RS_Steel': ((.33,.38,.43),.84,.34),
            'RS_SteelDark': ((.115,.15,.185),.8,.42),
            'RS_Gold': ((.52,.32,.105),.77,.35),
            'RS_ThreadGold': ((.47,.29,.085),.3,.55),
            'RS_ShieldBlue': ((.014,.049,.14),.0,.46),
            'RS_Leather': ((.075,.029,.012),0,.71),
            'RS_LeatherLight': ((.16,.069,.025),0,.66),
            'RS_Wood': ((.19,.095,.036),0,.72),
            'RS_BlueCloth': ((.018,.06,.16),0,.8),
        }
        for name,(rgb,metal,rough) in palette.items():
            mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
            mat.use_nodes=True; mat.diffuse_color=(*rgb,1)
            bs=mat.node_tree.nodes.get('Principled BSDF')
            bs.inputs['Base Color'].default_value=(*rgb,1)
            bs.inputs['Metallic'].default_value=metal
            bs.inputs['Roughness'].default_value=rough
            MATS[name]=mat


def finish(obj, name, mat, smooth=True):
    obj.name=name
    if mat: obj.data.materials.append(MATS[mat] if isinstance(mat,str) else mat)
    if obj.type=='MESH':
        for face in obj.data.polygons: face.use_smooth=smooth
    PARTS.append(obj)
    return obj


def mesh(name, verts, faces, mat, uv=None, smooth=True):
    data=bpy.data.meshes.new(name); data.from_pydata(verts,[],faces); data.update()
    obj=bpy.data.objects.new(name,data); bpy.context.collection.objects.link(obj)
    layer=data.uv_layers.new(name='UVMap')
    for p in data.polygons:
        for li in p.loop_indices:
            vi=data.loops[li].vertex_index; co=Vector(verts[vi])
            layer.data[li].uv=uv[vi] if uv else (co.x*2,co.z*2)
    bm=bmesh.new();bm.from_mesh(data)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(data);bm.free()
    return finish(obj,name,mat,smooth)


def edge(obj, thickness=.003, bevel=.001, segments=3):
    if thickness:
        mod=obj.modifiers.new('Fabricated wall thickness','SOLIDIFY')
        mod.thickness=thickness;mod.offset=0
        mod.use_even_offset=True
    if bevel:
        mod=obj.modifiers.new('Fine rolled and softened cut edge','BEVEL')
        mod.width=bevel;mod.segments=segments
        mod.limit_method='ANGLE';mod.angle_limit=.34
    return obj


def curve(name, points, radius, mat, cyclic=False, resolution=3, poly=False):
    data=bpy.data.curves.new(name,'CURVE');data.dimensions='3D'
    data.resolution_u=resolution; data.bevel_depth=radius;data.bevel_resolution=3
    if poly:
        spline=data.splines.new('POLY');spline.points.add(len(points)-1)
        for p,co in zip(spline.points,points):p.co=(*co,1)
    else:
        spline=data.splines.new('BEZIER');spline.bezier_points.add(len(points)-1)
        for p,co in zip(spline.bezier_points,points):
            p.co=co;p.handle_left_type='AUTO';p.handle_right_type='AUTO'
    spline.use_cyclic_u=cyclic
    obj=bpy.data.objects.new(name,data);bpy.context.collection.objects.link(obj)
    finish(obj,name,mat)
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True)
    bpy.context.view_layer.objects.active=obj;bpy.ops.object.convert(target='MESH')
    obj.select_set(False)
    # Parametric UVs for the bevel surface produced by Blender.
    if not obj.data.uv_layers:
        uv=obj.data.uv_layers.new(name='UVMap')
        for loop in obj.data.loops:
            co=obj.data.vertices[loop.vertex_index].co
            uv.data[loop.index].uv=(co.x*5,co.z*5)
    return obj


def stud(name, point, normal=(0,-1,0), radius=.0038, mat='RS_Gold'):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=12,ring_count=8,location=point)
    obj=bpy.context.object;obj.scale=(radius,radius,radius*.35)
    obj.rotation_euler=Vector(normal).to_track_quat('Z','Y').to_euler()
    bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
    return finish(obj,name,mat)


def loop_edge(name, points, mat='RS_Gold', radius=.0018):
    return curve(name,points,radius,mat,True,poly=True)


def round_box(name,center,scale,mat,bevel=.003):
    bpy.ops.mesh.primitive_cube_add(size=1,location=center)
    obj=bpy.context.object;obj.scale=scale
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    finish(obj,name,mat,False);edge(obj,0,bevel,3)
    mod=obj.modifiers.new('Controlled planar normals','WEIGHTED_NORMAL')
    mod.keep_sharp=True;mod.weight=50
    return obj


def profile_plate(name, rows, mat='RS_Steel', front=True, ridge=.006, cols=48):
    """z, half width, edge y, bulge depth. Crown rows get a neck cutout."""
    verts=[];uv=[]
    for r,(z,width,ey,depth) in enumerate(rows):
        for j in range(cols+1):
            t=j/cols;u=t*2-1
            y=ey+(-1 if front else 1)*depth*sqrt(max(0,1-u*u*.92))
            y+=(-1 if front else 1)*ridge*(1-abs(u))**6
            zz=z
            if r==len(rows)-1:
                zz+=.07*abs(u)**1.7
            verts.append((u*width,y,zz));uv.append((t,r/(len(rows)-1)))
    faces=[(r*(cols+1)+j,r*(cols+1)+j+1,(r+1)*(cols+1)+j+1,(r+1)*(cols+1)+j)
           for r in range(len(rows)-1) for j in range(cols)]
    obj=edge(mesh(name,verts,faces,mat,uv),.004,.0014)
    # Borders are authored from the same surface, so no intersections or floats.
    borders=[verts[:cols+1],verts[-cols-1:],
             [verts[r*(cols+1)] for r in range(len(rows))],
             [verts[r*(cols+1)+cols] for r in range(len(rows))]]
    for n,path in enumerate(borders):curve(name+' rolled edge '+str(n),path,.0024,'RS_SteelDark',poly=True)
    if front:
        for n,path in enumerate(borders[:2]):
            curve(name+' narrow gilt reveal '+str(n),[(x,y-.001,z+.002) for x,y,z in path],.0013,'RS_Gold',poly=True)
    return obj,verts


def breastplate():
    rows=[(1.205,.172,.012,.116),(1.225,.177,.01,.124),(1.255,.183,.009,.136),
          (1.30,.198,.012,.155),(1.36,.219,.018,.179),(1.42,.237,.024,.192),
          (1.47,.246,.028,.185),(1.515,.236,.03,.170),(1.545,.216,.027,.147)]
    obj,verts=profile_plate('Forged breastplate with central keel',rows)
    back=[(z,w*.98,.015,d*.75) for z,w,ey,d in rows]
    profile_plate('Fitted backplate',back,front=False,ridge=.002)
    # Raised central rib: a shallow five-sided swage instead of a fat rod.
    ridge=[];faces=[]
    for i,(z,w,ey,d) in enumerate(rows[:-1]):
        for x in [-.006,0,.006]:
            y=ey-d-.006-(.003 if x==0 else 0)
            ridge.append((x,y,z))
    for r in range(len(rows)-2):
        for j in range(2):faces.append((r*3+j,r*3+j+1,(r+1)*3+j+1,(r+1)*3+j))
    edge(mesh('Pectoral forged centre ridge',ridge,faces,'RS_Steel'),.001,.0006)
    # Delicate pair of engraved outer swages, with inlaid gilt line below.
    for side in [-1,1]:
        path=[(side*.186,-.079,1.535),(side*.166,-.109,1.501),
              (side*.135,-.138,1.471),(side*.112,-.157,1.450)]
        curve('Breastplate etched arc '+str(side),path,.001,'RS_SteelDark')
        curve('Breastplate gilt arc '+str(side),[(x,y-.001,z-.006) for x,y,z in path],.0007,'RS_Gold')
        for z in [1.278,1.365,1.458]:
            row=min(rows,key=lambda r:abs(r[0]-z));x=side*row[1]*.94
            y=row[2]-row[3]*sqrt(1-.94*.94*.92)-.005
            stud('Breastplate side fastening', (x,y,z),radius=.003)
        # Leather side strap bridges front to back, visible under the arm.
        for z in [1.29,1.39]:
            curve('Breastplate leather side lacing',[(side*.204,-.065,z),(side*.219,.0,z),(side*.206,.10,z)],.009,'RS_Leather')
    # Overlapping fauld plates follow the waist in two short, tailored bands.
    for i in range(2):
        z=1.210-i*.044; rx=.18+i*.018; ry=.124+i*.009
        band_sweep('Waist fauld lame '+str(i),[(z+.016,rx*.98,ry*.98),(z,rx,ry),(z-.026,rx*1.03,ry*1.03)],'RS_SteelDark')
    # Small articulated tassets sit laterally, leaving blue cloth broad and clean.
    for side in [-1,1]:
        for i in range(3):
            z=1.144-i*.043
            x0=side*(.135+i*.004); x1=side*(.232+i*.009)
            points=[(x0,-.118,z+.012),(x1,-.067,z+.018),(x1+side*.003,-.085,z-.036),(x0,-.145,z-.038)]
            obj=edge(mesh('Hip tasset '+str(side)+' lame '+str(i),points,[(0,1,2,3)],'RS_Steel'),.0035,.002)
            curve('Tasset rolled lip',points[2:]+[points[0]],.0015,'RS_Gold',poly=True)
            for p in [points[0],points[1]]:stud('Tasset rivet',(p[0],p[1]-.002,p[2]-.008),radius=.0025)
    # Buckle of belt furnished by the tailoring assembly.
    buckle_outline=[(-.029,-.164,1.151),(.029,-.164,1.151),(.029,-.164,1.198),(-.029,-.164,1.198)]
    curve('Waist belt forged buckle',buckle_outline,.0034,'RS_Gold',True)
    curve('Waist belt buckle tongue',[(0,-.168,1.175),(.028,-.168,1.175)],.0015,'RS_Steel',poly=True)


def band_sweep(name,rows,mat='RS_Steel',arc=(-pi,pi),centre=(0,0,0),segs=64,crest=0):
    verts=[];uv=[]; cx,cy,cz=centre
    for r,(z,rx,ry) in enumerate(rows):
        for j in range(segs+1):
            a=arc[0]+(arc[1]-arc[0])*j/segs
            x=cx+rx*sin(a);y=cy-ry*cos(a)-crest*max(0,cos(a))**8
            verts.append((x,y,z+cz));uv.append((j/segs,r/(len(rows)-1)))
    faces=[(r*(segs+1)+j,r*(segs+1)+j+1,(r+1)*(segs+1)+j+1,(r+1)*(segs+1)+j)
           for r in range(len(rows)-1) for j in range(segs)]
    obj=edge(mesh(name,verts,faces,mat,uv),.0032,.0014)
    for row in [0,len(rows)-1]:
        curve(name+' rolled edge '+str(row),verts[row*(segs+1):(row+1)*(segs+1)],.0017,'RS_SteelDark',poly=True)
    return obj


def helmet():
    segs=96;rows=20;verts=[];uv=[]
    for r in range(rows+1):
        t=(r+.10)/(rows+.1); theta=t*pi/2
        for j in range(segs+1):
            a=2*pi*j/segs
            basez=1.890+.029*cos(a)
            z=basez+(2.052-basez)*cos(theta)**1.18
            x=.123*sin(theta)*sin(a)
            y=-.173*sin(theta)*cos(a)+.008
            verts.append((x,y,z));uv.append((j/segs,t))
    faces=[(r*(segs+1)+j,r*(segs+1)+j+1,(r+1)*(segs+1)+j+1,(r+1)*(segs+1)+j)
           for r in range(rows) for j in range(segs)]
    faces.append(tuple(range(segs-1,-1,-1)))
    edge(mesh('Open sallet forged crown',verts,faces,'RS_Steel',uv),.0037,.001)
    # Kettle brim, shallow and asymmetric; front edge frames the brow.
    brim=[];buv=[]
    for k in range(5):
        t=k/4
        for j in range(segs+1):
            a=2*pi*j/segs
            w=.028+.008*abs(sin(a))+.010*max(0,-cos(a))
            rx=.123+t*w;ry=.173+t*w
            z=1.890+.029*cos(a)-.017*t+.006*sin(t*pi)
            brim.append((rx*sin(a),-ry*cos(a)+.008,z));buv.append((j/segs,t))
    faces=[(r*(segs+1)+j,r*(segs+1)+j+1,(r+1)*(segs+1)+j+1,(r+1)*(segs+1)+j)
           for r in range(4) for j in range(segs)]
    edge(mesh('Sallet sweeping rolled brim',brim,faces,'RS_SteelDark',buv),.004,.001)
    curve('Sallet narrow golden brim reveal',brim[-segs-1:],.0015,'RS_Gold',poly=True)
    # Centre reinforcing crest conforms to crown; slim spine, no glued-on cone.
    crest=[]
    for k in range(49):
        a=-pi/2+pi*k/48
        x=0; y=.008+.173*sin(a)
        theta=abs(a)
        base=1.890-.029*sin(a)
        z=base+(2.052-base)*cos(theta)**1.18+.002
        crest.append((x,y,z))
    curve('Sallet crown central rolled ridge',crest,.0021,'RS_SteelDark',poly=True)
    # Decorative band is a millimetre inlay, visually restrained.
    band=[]
    for j in range(segs+1):
        a=2*pi*j/segs
        band.append((.122*sin(a),-.172*cos(a)+.008,1.911+.026*cos(a)))
    curve('Sallet narrow inlaid brow band',band,.0013,'RS_Gold',poly=True)
    for j in range(16):
        a=2*pi*(j+.5)/16
        p=(.125*sin(a),-.175*cos(a)+.008,1.906+.027*cos(a))
        stud('Sallet lining rivet',p,(sin(a),-cos(a),.2),.0025)
    # Neck lames restricted to rear hemisphere, with the face entirely open.
    for i in range(2):
        verts=[];uv=[]; n=40
        for r in range(3):
            t=r/2
            for j in range(n+1):
                a=pi*.53+pi*.94*j/n
                rx=.123+.008*t;ry=.156+.012*t
                z=1.869-i*.04-.034*t
                verts.append((rx*sin(a),-ry*cos(a)+.009,z));uv.append((j/n,t))
        faces=[(r*(n+1)+j,r*(n+1)+j+1,(r+1)*(n+1)+j+1,(r+1)*(n+1)+j) for r in range(2) for j in range(n)]
        edge(mesh('Sallet nape protection '+str(i),verts,faces,'RS_SteelDark',uv),.003,.0012)
        curve('Nape rolled edge '+str(i),verts[-n-1:],.0017,'RS_Steel',poly=True)


def dome_plate(name, center, scale, mat, theta_max=pi*.56, theta_start=0, segs=48, rows=12):
    verts=[];uv=[]
    for r in range(rows+1):
        t=(r+.03)/(rows+.03);theta=theta_start+(theta_max-theta_start)*t
        for j in range(segs+1):
            a=2*pi*j/segs
            verts.append((center[0]+scale[0]*sin(theta)*cos(a),
                          center[1]+scale[1]*sin(theta)*sin(a),
                          center[2]+scale[2]*cos(theta)))
            uv.append((j/segs,t))
    faces=[(r*(segs+1)+j,r*(segs+1)+j+1,(r+1)*(segs+1)+j+1,(r+1)*(segs+1)+j)
           for r in range(rows) for j in range(segs)]
    obj=edge(mesh(name,verts,faces,mat,uv),.0035,.0013)
    curve(name+' rolled edge',verts[-segs-1:],.002,'RS_SteelDark',poly=True)
    return obj,verts[-segs-1:]


def bone_plate(name,a,b,profiles,mat='RS_Steel',arc=(-pi*.85,pi*.85),crest=.003,segs=48):
    a,b=Vector(a),Vector(b);axis=(b-a).normalized()
    # -Y is outward-facing plate direction, projected perpendicular to bone.
    front=Vector((0,-1,0));front=(front-axis*front.dot(axis)).normalized()
    side=axis.cross(front).normalized()
    verts=[];uv=[]
    for r,(t,rx,ry) in enumerate(profiles):
        c=a.lerp(b,t)
        for j in range(segs+1):
            angle=arc[0]+(arc[1]-arc[0])*j/segs
            p=c+side*(rx*sin(angle))+front*(ry*cos(angle)+crest*max(0,cos(angle))**8)
            verts.append(tuple(p));uv.append((j/segs,t))
    faces=[(r*(segs+1)+j,r*(segs+1)+j+1,(r+1)*(segs+1)+j+1,(r+1)*(segs+1)+j)
           for r in range(len(profiles)-1) for j in range(segs)]
    obj=edge(mesh(name,verts,faces,mat,uv),.0033,.0014)
    for idx in [0,len(profiles)-1]:
        path=verts[idx*(segs+1):(idx+1)*(segs+1)]
        curve(name+' dark rolled perimeter '+str(idx),path,.0016,'RS_SteelDark',poly=True)
        if idx==0:curve(name+' restrained gilt piping',path,.0009,'RS_Gold',poly=True)
    return obj,verts,side,front


def smooth_outline(poly,steps=6):
    result=[]
    for i,p in enumerate(poly):
        p0=Vector(poly[(i-1)%len(poly)]);p1=Vector(p)
        p2=Vector(poly[(i+1)%len(poly)]);p3=Vector(poly[(i+2)%len(poly)])
        result.extend(bezier_segment(p1,p1+(p2-p0)/6,p2-(p3-p1)/6,p2,steps))
    return result


def pauldron(side):
    # Flattened, asymmetric shoulder plate with a cut neck edge. Its plan
    # shape is distinct from a sphere and the lower lames are thin overlaps.
    outline=smooth_outline([(-.096,-.060),(-.068,-.097),(.024,-.109),(.098,-.085),
                            (.132,-.028),(.121,.057),(.054,.097),(-.049,.089),(-.098,.042)],8)
    n=len(outline);rings=18;verts=[];uv=[]
    def point(x,y,t=1,zoff=0):
        xx=x*t; yy=y*t
        z=1.574+.070*sqrt(max(0,1-t*t))-.12*xx+zoff
        return (side*(.291+xx),yy,z)
    # Tiny centre ring avoids an exactly degenerate pole while preserving the
    # smooth highlight across the crown.
    for r in range(rings+1):
        t=(r+.01)/(rings+.01)
        for j,(x,y) in enumerate(outline):
            verts.append(point(x,y,t));uv.append((.5+x*t*4,.5+y*t*4))
    faces=[(r*n+j,r*n+(j+1)%n,(r+1)*n+(j+1)%n,(r+1)*n+j) for r in range(rings) for j in range(n)]
    edge(mesh('Royal asymmetric pauldron '+str(side),verts,faces,'RS_Steel',uv),.004,.0014)
    border=verts[-n:]
    curve('Pauldron turned dark edge '+str(side),border,.0021,'RS_SteelDark',True,poly=True)
    curve('Pauldron fine gilt reveal '+str(side),[(x,y,z+.003) for x,y,z in border],.001,'RS_Gold',True,poly=True)
    # A shallow incised arc follows the outer plate, not a gold outline around
    # every surface. Satin steel remains the main readable shape.
    path=[point(x,y,.81,.001) for x,y in outline[8:57]]
    curve('Pauldron chased inset arc '+str(side),path,.0008,'RS_SteelDark',poly=True)
    for i in range(2):
        vv=[];uv=[]
        for row in range(4):
            t=row/3
            for j,(x,y) in enumerate(outline):
                # Inner neck side tucks beneath the previous plate.
                xx=x*(.93-i*.04)+.006*i
                yy=y*(.99-i*.025)
                z=1.575-.031*i-.034*t-.12*xx
                vv.append((side*(.295+xx),yy,z));uv.append((j/n,t))
        ff=[(r*n+j,r*n+(j+1)%n,(r+1)*n+(j+1)%n,(r+1)*n+j) for r in range(3) for j in range(n)]
        edge(mesh('Pauldron articulated skirt '+str(side)+' '+str(i),vv,ff,'RS_SteelDark' if i==0 else 'RS_Steel',uv),.003,.0013)
        curve('Pauldron overlapping lame lip '+str(side)+' '+str(i),vv[-n:],.0015,'RS_SteelDark',True,poly=True)
    for x,y in [(-.061,-.089),(.089,-.077)]:
        p=point(x,y,.96,.007)
        stud('Pauldron lining fastening',p,(0,-.5,.8),.0028)


def knee_plate(side,knee):
    outline=smooth_outline([(-.055,.050),(0,.066),(.055,.050),(.073,.010),
                            (.050,-.042),(0,-.060),(-.050,-.042),(-.073,.010)],8)
    n=len(outline);rings=14;verts=[];uv=[]
    for r in range(rings+1):
        t=(r+.01)/(rings+.01)
        for x,z in outline:
            xx=x*t;zz=z*t
            y=knee.y-.060-.032*(1-t*t)**.65-.003*(1-abs(xx)/.077)**4
            verts.append((knee.x+xx,y,knee.z+zz));uv.append((.5+xx*6,.5+zz*6))
    faces=[(r*n+j,r*n+(j+1)%n,(r+1)*n+(j+1)%n,(r+1)*n+j) for r in range(rings) for j in range(n)]
    edge(mesh('Formed knee poleyn '+str(side),verts,faces,'RS_Steel',uv),.0035,.0014)
    curve('Knee fine rolled edge '+str(side),verts[-n:],.0018,'RS_SteelDark',True,poly=True)
    curve('Knee narrow gilt inset '+str(side),[(x,y-.001,z) for x,y,z in verts[-n:]],.0008,'RS_Gold',True,poly=True)


def limbs():
    for s in [-1,1]:
        shoulder=Vector((s*.27,0,1.60))
        elbow=Vector((-.36,-.015,1.30) if s<0 else (.37,0,1.34))
        wrist=Vector((-.38,-.15,1.10) if s<0 else (.28,-.28,1.32))
        pauldron(s)
        bone_plate('Upper arm rerebrace '+str(s),shoulder,elbow,
                   [(.31,.078,.075),(.40,.079,.077),(.69,.067,.068),(.87,.063,.064)],arc=(-2.45,2.45),crest=.001)
        # Compact fluted elbow fan; broad sides and a strong central fold.
        arm_axis=(wrist-elbow).normalized()
        elbow_front=Vector((0,-1,0));elbow_front=(elbow_front-arm_axis*elbow_front.dot(arm_axis)).normalized()
        ec=elbow+elbow_front*.023
        dome,lip=dome_plate('Elbow poleyn '+str(s),tuple(ec),(.077,.074,.072),'RS_Steel')
        # A vented side fin instead of an unscaled circular ball cap.
        points=[(s*(abs(ec.x)+.044),ec.y-.02,ec.z+.048),
                (s*(abs(ec.x)+.094),ec.y-.017,ec.z+.02),
                (s*(abs(ec.x)+.099),ec.y-.02,ec.z-.027),
                (s*(abs(ec.x)+.062),ec.y-.037,ec.z-.054),
                (s*(abs(ec.x)+.044),ec.y-.047,ec.z-.03)]
        edge(mesh('Couter side wing '+str(s),points,[tuple(range(5))],'RS_SteelDark'),.003,.002)
        curve('Couter wing rim '+str(s),points,.0012,'RS_Gold',True)
        bone_plate('Forearm vambrace '+str(s),elbow,wrist,
                   [(.14,.064,.070),(.24,.063,.068),(.56,.054,.058),(.80,.047,.052),(.93,.045,.049)],arc=(-2.60,2.60),crest=.003)
        # Two narrow cuff plates establish real layering at the glove.
        bone_plate('Vambrace wrist rolled cuff '+str(s),elbow,wrist,
                   [(.84,.049,.054),(.88,.050,.055),(.94,.048,.052)],'RS_SteelDark',arc=(-pi,pi),crest=0)
        knee=Vector((s*.15,(-.07 if s<0 else .045),.59))
        ankle=Vector((s*.16,(-.10 if s<0 else .06),.15))
        bone_plate('Sculpted shin greave '+str(s),knee,ankle,
                   [(.13,.069,.075),(.26,.073,.082),(.46,.065,.077),(.65,.055,.064),(.84,.046,.055),(.94,.043,.051)],
                   arc=(-2.45,2.45),crest=.008)
        # Knee is an embossed shield-like plate, not a spherical joint.
        knee_plate(s,knee)
        for x in [-.051,.051]:stud('Poleyn articulation pin',(knee.x+x,knee.y-.060,knee.z+.015),radius=.0035)
        # Rear leather straps make the open greave construction plausible.
        for t in [.25,.75]:
            c=knee.lerp(ankle,t);r=.065 if t<.5 else .047
            path=[(c.x-r,c.y,c.z),(c.x-r*.7,c.y+r,c.z),(c.x+r*.7,c.y+r,c.z),(c.x+r,c.y,c.z)]
            curve('Greave rear leather strap',path,.009,'RS_Leather')
        sabaton(s,ankle)


def sabaton(side,ankle):
    x=ankle.x; y=ankle.y
    # The boot is furnished by the clothing artist; these are independently
    # fabricated overlapping transverse lames following its dorsal envelope.
    rows=[(-.199,.024,.078),(-.178,.050,.101),(-.145,.069,.113),
          (-.105,.074,.129),(-.065,.073,.145),(-.027,.068,.171),(.018,.065,.205),(.052,.060,.202)]
    for i in range(len(rows)-1):
        y0,w0,z0=rows[i];y1,w1,z1=rows[i+1]
        verts=[];uv=[];n=28
        for r in range(4):
            t=r/3;yy=y0+(y1-y0)*t+.006*t
            w=w0+(w1-w0)*t;h=z0+(z1-z0)*t
            for j in range(n+1):
                a=-pi*.54+pi*1.08*j/n
                verts.append((x+w*sin(a),y+yy,.041+(h-.041)*cos(a)))
                uv.append((j/n,t))
        faces=[(r*(n+1)+j,r*(n+1)+j+1,(r+1)*(n+1)+j+1,(r+1)*(n+1)+j) for r in range(3) for j in range(n)]
        edge(mesh('Articulated sabaton '+str(side)+' lame '+str(i),verts,faces,'RS_Steel',uv),.003,.0013)
        curve('Sabaton overlap shadow '+str(side)+' '+str(i),verts[-n-1:],.0015,'RS_SteelDark',poly=True)
        if i in [1,4]:
            for p in [verts[-n-1+4],verts[-5]]:stud('Sabaton tiny rivet',(p[0],p[1],p[2]+.002),(0,0,1),.0025)
    # Closed domed toe cap completes the first lame. The leather toe is inside
    # the harness; only the dark sole remains visible below its edge.
    outline=smooth_outline([(-.024,.038),(-.020,.060),(0,.079),(.020,.060),
                            (.024,.038),(.019,.027),(0,.025),(-.019,.027)],6)
    n=len(outline);rings=10;verts=[];uv=[]
    for r in range(rings+1):
        t=(r+.01)/(rings+.01)
        for xx,zz in outline:
            xx*=t;zz=.048+(zz-.048)*t
            yy=y-.199-.009*(1-t*t)
            verts.append((x+xx,yy,zz));uv.append((xx/.052+.5,zz/.08))
    faces=[(r*n+j,r*n+(j+1)%n,(r+1)*n+(j+1)%n,(r+1)*n+j) for r in range(rings) for j in range(n)]
    edge(mesh('Closed forged sabaton toe cap '+str(side),verts,faces,'RS_Steel',uv),.003,.0012)
    curve('Sabaton toe turned edge '+str(side),verts[-n:],.0012,'RS_SteelDark',True,poly=True)


def bezier_segment(p0,p1,p2,p3,n=12):
    return [tuple((1-t)**3*p0[j]+3*(1-t)**2*t*p1[j]+3*(1-t)*t*t*p2[j]+t**3*p3[j]
                  for j in range(len(p0))) for t in [i/n for i in range(n)]]


def shield():
    # A gently curved heater, narrower than the old broad slab. Geometry is
    # defined in local X/Z and then yawed as a complete object, before fittings.
    center=Vector((.380,-.438,1.225));yaw=math.radians(-8)
    def depth(x,z): return -.035*(1-(x/.275)**2)+.004*z
    def world(x,y,z):
        p=Vector((x,y,z));p=Matrix.Rotation(yaw,3,'Z')@p
        return tuple(center+p)
    def sp(x,z,offset=0):return world(x,depth(x,z)+offset,z)
    outlines=[
      ((-.246,.373),(-.16,.405),(-.06,.408),(0,.398)),
      ((0,.398),(.06,.408),(.16,.405),(.246,.373)),
      ((.246,.373),(.253,.277),(.259,.038),(.225,-.111)),
      ((.225,-.111),(.176,-.258),(.075,-.362),(0,-.407)),
      ((0,-.407),(-.075,-.362),(-.176,-.258),(-.225,-.111)),
      ((-.225,-.111),(-.259,.038),(-.253,.277),(-.246,.373))]
    outline=sum((bezier_segment(*segment,n=16) for segment in outlines),[])
    n=len(outline); rings=16
    def radial_face(name,mat,scale=1,offset=0):
        verts=[sp(0,0,offset)];uv=[(.5,.5)]
        for r in range(1,rings+1):
            t=r/rings
            for x,z in outline:
                x*=t*scale;z*=t*scale
                verts.append(sp(x,z,offset));uv.append((x/.54+.5,z/.82+.5))
        faces=[(0,1+j,1+(j+1)%n) for j in range(n)]
        for r in range(rings-1):
            for j in range(n):faces.append((1+r*n+j,1+r*n+(j+1)%n,1+(r+1)*n+(j+1)%n,1+(r+1)*n+j))
        obj=edge(mesh(name,verts,faces,mat,uv),.012 if offset>0 else .003,.001)
        return obj
    radial_face('Heater curved wooden foundation','RS_Wood',1,.014)
    radial_face('Heater royal blue lacquer field','RS_ShieldBlue',.994,-.001)
    # Wide structural steel edge, a narrow gold inlay, and an inner dark reveal.
    for name,mat,inside,outside,y in [('Forged steel shield frame','RS_SteelDark',.926,1.011,-.003),
                                    ('Shield narrow gilt inlay','RS_Gold',.944,.958,-.008),
                                    ('Shield inner frame edge','RS_Steel',.913,.928,-.006)]:
        verts=[];uv=[]
        for r,t in enumerate([inside,outside]):
            for x,z in outline:verts.append(sp(x*t,z*t,y));uv.append((len(uv)%n/n,r))
        faces=[(j,(j+1)%n,n+(j+1)%n,n+j) for j in range(n)]
        edge(mesh(name,verts,faces,mat,uv),.004,.0014)
    # Inset filigree echoes the heater outline with a corner-return detail.
    curve('Shield fine inner gold keyline',[sp(x*.858,z*.858,-.006) for x,z in outline],.0008,'RS_Gold',True,poly=True)
    normal=Matrix.Rotation(yaw,3,'Z')@Vector((0,-1,0))
    for j in [2,8,15,24,34,42,48,55,63,73,83,91]:
        x,z=outline[j];stud('Shield frame flush bronze rivet',sp(x*.976,z*.976,-.008),normal,.0032)
    # Slender sculpted fleur-de-lis built with convex low-relief petals. It is
    # a heraldic relief laid into the blue field, never a floating fat pictogram.
    centre_segments=[
      ((0,.251),(-.016,.217),(-.048,.174),(-.039,.133)),
      ((-.039,.133),(-.033,.104),(-.009,.087),(-.012,.012)),
      ((-.012,.012),(-.012,-.031),(-.015,-.069),(0,-.111)),
      ((0,-.111),(.015,-.069),(.012,-.031),(.012,.012)),
      ((.012,.012),(.009,.087),(.033,.104),(.039,.133)),
      ((.039,.133),(.048,.174),(.016,.217),(0,.251))]
    left_segments=[
      ((-.016,.014),(-.027,.063),(-.055,.123),(-.100,.133)),
      ((-.100,.133),(-.140,.141),(-.162,.110),(-.150,.078)),
      ((-.150,.078),(-.145,.060),(-.124,.049),(-.105,.057)),
      ((-.105,.057),(-.134,.084),(-.097,.108),(-.077,.073)),
      ((-.077,.073),(-.061,.047),(-.055,.024),(-.052,-.009)),
      ((-.052,-.009),(-.050,-.033),(-.070,-.053),(-.061,-.070)),
      ((-.061,-.070),(-.039,-.075),(-.020,-.047),(-.016,.014))]
    right_segments=[tuple((-x,z) for x,z in segment) for segment in left_segments]
    tail_segments=[
      ((-.010,-.075),(-.031,-.092),(-.048,-.124),(-.054,-.146)),
      ((-.054,-.146),(-.027,-.142),(-.015,-.126),(0,-.164)),
      ((0,-.164),(.015,-.126),(.027,-.142),(.054,-.146)),
      ((.054,-.146),(.048,-.124),(.031,-.092),(.010,-.075)),
      ((.010,-.075),(.005,-.086),(-.005,-.086),(-.010,-.075))]
    from mathutils.geometry import tessellate_polygon
    for k,segments in enumerate([centre_segments,left_segments,right_segments,tail_segments]):
        smooth=sum((bezier_segment(*segment,10) for segment in segments),[])
        flat=[Vector((x,0,z)) for x,z in smooth]
        triangles=tessellate_polygon([flat]);indices={tuple(v):i for i,v in enumerate(flat)}
        faces=[tuple(v if isinstance(v,int) else indices[tuple(v)] for v in triangle) for triangle in triangles]
        verts=[sp(x,z,-.006) for x,z in smooth]
        edge(mesh('Shield fleur relief petal '+str(k),verts,faces,'RS_Gold'),.0018,.00055)
        curve('Fleur inset perimeter '+str(k),[sp(x,z,-.0065) for x,z in smooth],.00055,'RS_ThreadGold',True,poly=True)
    # Heraldic crossbar, tiny chased decorative line underneath.
    bar=[(-.060,-.039),(.060,-.039),(.060,-.055),(-.060,-.055)]
    edge(mesh('Fleur crossbar',[sp(x,z,-.012) for x,z in bar],[(0,1,2,3)],'RS_Gold'),.002,.002)
    # Two pale steel heraldic nails at upper corners, with balanced visual scale.
    for s in [-1,1]:
        path=[(s*.150,.304),(s*.120,.277),(s*.150,.250),(s*.180,.277)]
        curve('Shield corner lozenge '+str(s),[sp(x,z,-.007) for x,z in path],.0010,'RS_Gold',True,poly=True)
        stud('Shield lozenge central rivet '+str(s),sp(s*.150,.277,-.007),normal,.0024)
    # Back braces and hand grip: fitted in world space so fingers actually close
    # around the leather handle furnished by the anatomy artist's exact contract.
    for z in [-.22,.19]:
        points=[sp(x,z,.036) for x in [-.18,-.10,0,.10,.18]]
        curve('Shield rear structural leather brace '+str(z),points,.014,'RS_Leather',poly=True)
        for x in [-.18,.18]:stud('Shield back brace fastening',sp(x,z,.050),-normal,.004,'RS_SteelDark')
    grip=(.280,-.320,1.300)
    curve('Shield gripped leather handle',[(.245,-.330,1.355),(.255,-.315,1.325),grip,(.308,-.326,1.265)],.012,'RS_Leather')
    for endpoint,anchor in [((.245,-.330,1.355),sp(-.13,.13,.025)),((.308,-.326,1.265),sp(-.06,-.035,.025))]:
        curve('Shield grip anchor iron', [endpoint,anchor],.006,'RS_SteelDark',poly=True)
    # Wide forearm enarme has a visible oval loop on the rear of the shield.
    strap=[sp(.10,.08,.023),world(.14,.12,.02),world(.12,.16,-.05),sp(.10,-.12,.023)]
    curve('Shield forearm leather enarme',strap,.016,'RS_LeatherLight')
    return center


def sword():
    x=-.385;y=-.215;guardz=1.0
    # Diamond/lenticular section tapers to a narrow, believable point. The
    # recessed fuller has a pair of actual shoulders rather than a painted line.
    rows=[(.990,.026,.0042),(.95,.025,.0042),(.87,.024,.004),(.72,.023,.0037),
          (.54,.021,.0032),(.39,.018,.0028),(.30,.013,.0021),(.235,.001,.0005)]
    section=[(-1,0),(-.70,-.80),(-.26,-1),(-.16,-.61),(.16,-.61),(.26,-1),(.70,-.80),(1,0),
             (.70,.80),(.26,1),(.16,.61),(-.16,.61),(-.26,1),(-.70,.80)]
    verts=[];uv=[];n=len(section)
    for r,(z,w,d) in enumerate(rows):
        for j,(u,v) in enumerate(section):verts.append((x+u*w,y+v*d,z));uv.append((j/n,r/(len(rows)-1)))
    faces=[(r*n+j,r*n+(j+1)%n,(r+1)*n+(j+1)%n,(r+1)*n+j) for r in range(len(rows)-1) for j in range(n)]
    faces.extend([tuple(range(n-1,-1,-1)),tuple((len(rows)-1)*n+j for j in range(n))])
    edge(mesh('Royal longsword lenticular fullered blade',verts,faces,'RS_Steel',uv,False),0,.0003,2)
    # Darker fuller reflects less light without becoming a black painted stripe.
    for s in [-1,1]:
        vv=[]
        for z,w,d in rows[:-1]:
            for dx in [-.15,.15]:vv.append((x+dx*w,y+s*d*.62,z-.002))
        ff=[(r*2,r*2+1,r*2+3,r*2+2) for r in range(len(rows)-2)]
        mesh('Sword recessed fuller '+str(s),vv,ff,'RS_SteelDark')
    # Quillons carry a flared octagonal profile, forged as one smooth solid.
    verts=[];uv=[];cross=8
    for i,(dx,z,r) in enumerate([(-.119,.973,.011),(-.108,.980,.010),(-.078,.997,.008),(-.032,1.009,.008),
                               (0,1.012,.011),(.032,1.009,.008),(.078,.997,.008),(.108,.980,.010),(.119,.973,.011)]):
        for j in range(cross):
            a=2*pi*j/cross;verts.append((x+dx,y+r*cos(a),z+r*.80*sin(a)));uv.append((i/8,j/cross))
    faces=[(r*cross+j,r*cross+(j+1)%cross,(r+1)*cross+(j+1)%cross,(r+1)*cross+j) for r in range(8) for j in range(cross)]
    faces.extend([tuple(range(cross-1,-1,-1)),tuple(8*cross+j for j in range(cross))])
    edge(mesh('Sword forged swept crossguard',verts,faces,'RS_Gold',uv),0,.001,3)
    # The handle is elliptical and cord-bound at a subdued scale.
    rows=[(1.014,.013,.011),(1.026,.015,.012),(1.053,.014,.011),(1.096,.012,.010),(1.139,.011,.009)]
    band_sweep('Sword fitted leather grip',rows,'RS_Leather',centre=(x,y,0),segs=32)
    winding=[]
    for j in range(321):
        t=j/320;a=t*pi*2*11;rx=.014*(1-t)+.011*t;ry=.012*(1-t)+.009*t
        winding.append((x+rx*sin(a),y-ry*cos(a),1.024+t*.110))
    curve('Sword grip fine raised winding',winding,.00065,'RS_LeatherLight',poly=True)
    for z in [1.018,1.137]:band_sweep('Sword gilt grip ferrule '+str(z),[(z-.003,.014,.012),(z+.003,.014,.012)],'RS_Gold',centre=(x,y,0),segs=32)
    # Faceted oval pommel, small enough that fingers remain believable.
    bpy.ops.mesh.primitive_uv_sphere_add(segments=32,ring_count=16,location=(x,y,1.160))
    obj=bpy.context.object;obj.scale=(.024,.018,.027)
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    finish(obj,'Sword finely cast wheel pommel','RS_Gold')
    stud('Pommel small steel seal',(x,y-.018,1.160),(0,-1,0),.009,'RS_SteelDark')


def finalize():
    # Apply fabrication modifiers only; never globally decimate the finished
    # harness. Preserve authored seams and separate materials for Unity.
    for obj in list(PARTS):
        if obj.type!='MESH':continue
        bpy.context.view_layer.objects.active=obj
        for mod in list(obj.modifiers):
            try:bpy.ops.object.modifier_apply(modifier=mod.name)
            except RuntimeError:pass
        if not obj.data.uv_layers:
            layer=obj.data.uv_layers.new(name='UVMap')
            for loop in obj.data.loops:
                p=obj.data.vertices[loop.vertex_index].co
                layer.data[loop.index].uv=(p.x*2,p.z*2)
    group=bpy.data.collections.new('Royal soldier / forged equipment')
    bpy.context.scene.collection.children.link(group)
    for obj in PARTS:
        for collection in list(obj.users_collection):collection.objects.unlink(obj)
        group.objects.link(obj)
    bpy.ops.object.select_all(action='DESELECT')
    for obj in PARTS:obj.select_set(True)
    os.makedirs(OUT,exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=OUT+'/armor.blend')
    triangles=sum(sum(len(p.vertices)-2 for p in obj.data.polygons) for obj in PARTS if obj.type=='MESH')
    stats={'model':'Royal Kingdom infantry equipment','coordinates':'metres, Z up, front -Y',
           'objects':len(PARTS),'triangles':triangles,
           'materials':sorted({m.name for obj in PARTS for m in obj.data.materials}),
           'uvValid':all(obj.data.uv_layers.active is not None for obj in PARTS),
           'construction':'Profile-authored plates; no extracted body shells or global decimation',
           'staticPose':True,'animations':False}
    with open(OUT+'/armor-validation.json','w') as f:json.dump(stats,f,indent=2)
    print('ROYAL_ARMOR_SAVED',json.dumps(stats))


def render_preview():
    # Shape review only. Cameras and light are not part of the .blend delivery.
    bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.012))
    floor=bpy.context.object;mat=bpy.data.materials.new('Preview warm stone');mat.diffuse_color=(.16,.15,.13,1);floor.data.materials.append(mat)
    world=bpy.context.scene.world or bpy.data.worlds.new('World');bpy.context.scene.world=world;world.use_nodes=True
    world.node_tree.nodes['Background'].inputs['Color'].default_value=(.30,.37,.46,1)
    world.node_tree.nodes['Background'].inputs['Strength'].default_value=.28
    for name,pos,power,size,color in [('Large warm key',(-3,-4,5),850,4,(1,.88,.72)),('Cool fill',(3,-1,3),550,3,(.72,.82,1)),('Edge',(0,3,4),1000,3,(1,.96,.85))]:
        light=bpy.data.lights.new(name,'AREA');light.energy=power;light.shape='DISK';light.size=size;light.color=color
        obj=bpy.data.objects.new(name,light);bpy.context.collection.objects.link(obj);obj.location=pos
        obj.rotation_euler=(Vector((0,0,1.15))-obj.location).to_track_quat('-Z','Y').to_euler()
    camdata=bpy.data.cameras.new('Equipment review');cam=bpy.data.objects.new('Equipment review',camdata);bpy.context.collection.objects.link(cam)
    cam.location=(-.45,-4.5,2.30);cam.rotation_euler=(Vector((0,-.03,1.1))-cam.location).to_track_quat('-Z','Y').to_euler();camdata.type='ORTHO';camdata.ortho_scale=2.5
    scene=bpy.context.scene;scene.camera=cam;scene.render.engine='CYCLES';scene.cycles.samples=40;scene.cycles.use_denoising=True
    scene.render.resolution_x=1100;scene.render.resolution_y=1400;scene.render.resolution_percentage=100
    scene.view_settings.view_transform='AgX';scene.render.image_settings.file_format='PNG'
    scene.render.filepath=OUT+'/armor-front.png';bpy.ops.render.render(write_still=True)
    cam.location=(2.6,-4,2.4);cam.rotation_euler=(Vector((0,0,1.14))-cam.location).to_track_quat('-Z','Y').to_euler()
    scene.render.filepath=OUT+'/armor-threequarter.png';bpy.ops.render.render(write_still=True)


def build():
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    materials();breastplate();helmet();limbs();shield();sword();finalize()
    if '--no-preview' not in sys.argv:render_preview()


if __name__=='__main__':build()
