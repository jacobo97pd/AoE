"""New Kingdom infantry anatomy and tailored garments; Blender 4.5 authoring.

Standalone: blender -b --factory-startup --python tools/art/royal_soldier_body.py
Outputs on D: only; append the separate authored objects when assembling armour.
CC0 anatomical source: Blender Studio Human Base Meshes bundle v1.4.1,
Body Male - Realistic, Dan Ulrich. No previous Kingdom meshes are reused.
Coordinates: metres, Z up, front -Y. Static gripping pose, not a rig.
"""
import bpy, bmesh, math, json, random, sys
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from math import sin, cos, pi, exp

HERE = Path(__file__).resolve().parent
OUT = Path('D:/CodexTooling/royal-soldier')
BASE = Path('D:/CodexTooling/kingdom-premium/human-base-meshes/human-base-meshes-bundle-v1.4.1/human_base_meshes_bundle.blend')
RNG = random.Random(77831)
PARTS=[]
MATS={}

def clamp(t,a=0.,b=1.): return min(b,max(a,t))
def smooth(a,b,t):
    t=clamp((t-a)/(b-a));return t*t*(3-2*t)
def mesh(name,verts,faces,mat,uv=None):
    me=bpy.data.meshes.new(name);me.from_pydata(verts,[],faces);me.update()
    ob=bpy.data.objects.new(name,me);bpy.context.collection.objects.link(ob)
    me.materials.append(MATS[mat]);PARTS.append(ob)
    for p in me.polygons:p.use_smooth=True
    layer=me.uv_layers.new(name='UVMap')
    for p in me.polygons:
        for li in p.loop_indices:
            vi=me.loops[li].vertex_index
            layer.data[li].uv=uv[vi] if uv else (me.vertices[vi].co.x,me.vertices[vi].co.z)
    return ob

def apply(ob):
    bpy.context.view_layer.objects.active=ob
    for m in list(ob.modifiers):bpy.ops.object.modifier_apply(modifier=m.name)
    return ob

def finish(ob,solid=0.,subdiv=0):
    if subdiv:
        s=ob.modifiers.new('Tailored surface subdivision','SUBSURF');s.levels=subdiv;s.render_levels=subdiv
    if solid:
        s=ob.modifiers.new('Woven edge thickness','SOLIDIFY');s.thickness=solid;s.offset=0
    apply(ob);return ob

def curve(name,points,radius,mat,radii=None,closed=False,res=3):
    cu=bpy.data.curves.new(name,'CURVE');cu.dimensions='3D';cu.resolution_u=res;cu.bevel_depth=radius;cu.bevel_resolution=2;cu.use_fill_caps=True
    sp=cu.splines.new('BEZIER');sp.bezier_points.add(len(points)-1)
    for i,(b,p) in enumerate(zip(sp.bezier_points,points)):
        b.co=p;b.handle_left_type='AUTO';b.handle_right_type='AUTO';b.radius=radii[i] if radii else 1
    sp.use_cyclic_u=closed
    ob=bpy.data.objects.new(name,cu);bpy.context.collection.objects.link(ob);cu.materials.append(MATS[mat]);PARTS.append(ob)
    return ob

def sphere(name,center,radii,mat,segments=32,rings=20):
    verts=[];uv=[]
    for i in range(rings+1):
        a=pi*i/rings
        for j in range(segments+1):
            b=2*pi*j/segments
            verts.append((center[0]+radii[0]*sin(a)*cos(b),center[1]+radii[1]*sin(a)*sin(b),center[2]+radii[2]*cos(a)));uv.append((j/segments,i/rings))
    faces=[(i*(segments+1)+j,i*(segments+1)+j+1,(i+1)*(segments+1)+j+1,(i+1)*(segments+1)+j) for i in range(rings) for j in range(segments)]
    return mesh(name,verts,faces,mat,uv)

def ring_surface(name,profile,mat,segments=72,wrinkle=None):
    # profile: (z,centreX,centreY,radiusX,radiusY)
    vs=[];uv=[]
    for i,(z,cx,cy,rx,ry) in enumerate(profile):
        for j in range(segments+1):
            a=2*pi*j/segments;dw=wrinkle(i/(len(profile)-1),a) if wrinkle else 0
            vs.append((cx+(rx+dw)*cos(a),cy+(ry+dw)*sin(a),z));uv.append((j/segments,i/(len(profile)-1)))
    fs=[(i*(segments+1)+j,i*(segments+1)+j+1,(i+1)*(segments+1)+j+1,(i+1)*(segments+1)+j) for i in range(len(profile)-1) for j in range(segments)]
    return mesh(name,vs,fs,mat,uv)

def patch(name,fn,mat,nu=44,nv=64,solid=.003):
    vs=[];uv=[]
    for i in range(nv+1):
        for j in range(nu+1):vs.append(fn(j/nu,i/nv));uv.append((j/nu,i/nv))
    fs=[(i*(nu+1)+j,i*(nu+1)+j+1,(i+1)*(nu+1)+j+1,(i+1)*(nu+1)+j) for i in range(nv) for j in range(nu)]
    return finish(mesh(name,vs,fs,mat,uv),solid,0)

def edge_stitches(name,fn,mat='RS_ThreadIvory',count=70,across=.003):
    # Separate short threads lie on the surface, not an oversized decorative rope.
    for i in range(count):
        t=(i+.2)/count;p=Vector(fn(t));q=Vector(fn(min(1,t+.4/count)))
        curve(name,[p,q],.0007,mat,res=1)

def head_mapping(p):
    x,y,z=p
    jaw=exp(-((z-1.474)/.038)**2)
    return Vector((x*1.22*(1+.105*jaw),y*1.20+.033,1.70+(z-1.44)*1.28))

def clean_neck(ob):
    """Tuck source shoulder remnants inside the padded standing collar."""
    for v in ob.data.vertices:
        x,y,z=v.co
        if z>=1.709:continue
        blend=1-smooth(1.687,1.709,z)
        radius=math.sqrt((x/.064)**2+((y-.008)/.061)**2)
        target=min(1,1/max(.001,radius))
        factor=1+(target-1)*blend
        v.co.x*=factor;v.co.y=.008+(y-.008)*factor
        v.co.z-=.015*(1-smooth(1.65,1.68,z))
    ob.data.update()

def extract_shell(src,name,predicate,mat,offset=0.):
    cp=src.copy();cp.data=src.data.copy();bpy.context.collection.objects.link(cp);cp.name=name
    bm=bmesh.new();bm.from_mesh(cp.data)
    bmesh.ops.delete(bm,geom=[f for f in bm.faces if not predicate(f.calc_center_median())],context='FACES')
    bmesh.ops.delete(bm,geom=[v for v in bm.verts if not v.link_faces],context='VERTS')
    bm.to_mesh(cp.data);bm.free();cp.data.update()
    cp.data.materials.clear();cp.data.materials.append(MATS[mat])
    for v in cp.data.vertices:v.co+=v.normal*offset
    for p in cp.data.polygons:p.use_smooth=True
    PARTS.append(cp);return cp

def anatomy():
    with bpy.data.libraries.load(str(BASE),link=False) as (a,b):b.objects=['GEO-body_male_realistic']
    src=b.objects[0];bpy.context.collection.objects.link(src);src.parent=None;src.location=(0,0,0);src.rotation_euler=(0,0,0);src.animation_data_clear()
    for m in list(src.modifiers):
        if m.type=='MULTIRES':m.levels=1;m.render_levels=1
        else:src.modifiers.remove(m)
    apply(src)
    head=extract_shell(src,'RS_Anatomical_Head_Neck',lambda p:p.z>1.402,'RS_Skin')
    orig=[v.co.copy() for v in head.data.vertices]
    colors=head.data.color_attributes.new(name='Color',type='FLOAT_COLOR',domain='POINT')
    for i,v in enumerate(head.data.vertices):
        x,y,z=orig[i]
        # Close the upper lid toward the iris and bring the brow ridge down.
        # The anatomical socket stays continuous; no painted eyelid overlay.
        eye_band=exp(-((abs(x)-.033)/.018)**4)*smooth(.065,.12,-y)
        dz=-.0029*eye_band*exp(-((z-1.583)/.009)**2)
        dz+=.0027*eye_band*exp(-((z-1.565)/.006)**2)
        v.co=head_mapping((x,y,z+dz))
        brow=exp(-((abs(x)-.037)/.026)**2-((z-1.596)/.012)**2)*smooth(.055,.12,-y)
        v.co.y-=.004*brow
        cheek=exp(-((abs(x)-.050)/.026)**2-((z-1.536)/.036)**2)*smooth(.02,.10,-y)
        lips=exp(-(x/.026)**4-((z-1.505)/.009)**4)*smooth(.10,.145,-y)
        lower_eye=exp(-((abs(x)-.033)/.020)**2-((z-1.56)/.013)**2)*smooth(.08,.12,-y)
        nose=exp(-(x/.013)**2-((z-1.533)/.020)**2)*smooth(.125,.16,-y)
        beard_top=1.484+.58*abs(x)
        beard_zone=smooth(0,.019,beard_top-z)*smooth(1.437,1.452,z)*smooth(.025,.075,-y)
        stubble=.48*beard_zone
        col=(1+.035*cheek+.025*nose-.09*lower_eye-stubble,1-.045*cheek-.19*lips-.12*lower_eye-stubble,1-.04*cheek-.17*lips-.10*lower_eye-stubble,1)
        colors.data[i].color=col
    clean_neck(head)
    head.data.update()
    bvh=BVHTree.FromPolygons([v.co.copy() for v in head.data.vertices],[list(p.vertices) for p in head.data.polygons])
    def front(x,z,off=.001):
        hit=bvh.ray_cast(Vector((x,-1,z)),Vector((0,1,0)),2)
        return hit[0].y-off if hit[0] is not None else -.10
    # Deep-set adult eyes. Small realistic irises, framed by original eyelids.
    for s in [-1,1]:
        c=head_mapping((s*.0329,-.1205,1.5737))
        sphere('RS_Anatomical_Eyeball',c,(.0162,.0162,.0162),'RS_EyeIvory',40,24)
        sphere('RS_GreenHazel_Iris',(c.x,c.y-.01565,c.z+.0003),(.0059,.00065,.0059),'RS_Iris',40,20)
        sphere('RS_Pupil',(c.x,c.y-.0163,c.z+.0003),(.00255,.0003,.00255),'RS_Pupil',32,16)
        # Individual eyebrow hairs avoid solid cartoon eyebrows.
        for k in range(32):
            t=k/31;x=s*(.017+.052*t);z=1.89+.011*t-.007*t*t
            curve('RS_Brow_Hair',[(x-s*.002,front(x-s*.002,z-.001,.0007),z-.001),(x,front(x,z+.002,.0013),z+.002),(x+s*.003,front(x+s*.003,z+.003,.0008),z+.003)],.0008,'RS_Hair',radii=[.3,1,.02],res=2)
    sculpted_beard(head)
    scalp=extract_shell(src,'RS_Fitted_Scalp',lambda p:p.z>1.601+.055*smooth(.01,.15,-p.y),'RS_Hair',.0025)
    for v in scalp.data.vertices:v.co=head_mapping(v.co)
    for s in [-1,1]:
        # Temple/sideburn groom beneath the helmet opening.
        for k in range(40):
            t=k/39;x=s*(.086+RNG.uniform(-.003,.003));z=1.905-.112*t;y=.002-.056*t
            curve('RS_Temple_Hair',[(x,y,z),(x+s*.001,y+.001,z-.01),(x,y+.006,z-.019)],.001,'RS_Hair',[.3,1,.08],res=2)
    bpy.data.objects.remove(src,do_unlink=True)
    return head

def sculpted_beard(head):
    """A continuous fitted short-beard mass, never a face-extraction cutout.

    The sampled surface wraps the jaw at 2–4mm depth and tapers into skin at
    its irregular perimeter. Fine locks rest on that mass, not isolated dots.
    """
    bvh=BVHTree.FromPolygons([v.co.copy() for v in head.data.vertices],[list(p.vertices) for p in head.data.polygons])
    def radial(theta,z):
        d=Vector((sin(theta),-cos(theta),0));start=Vector((0,.004,z))+d*.50
        hit=bvh.ray_cast(start,-d,1)
        if hit[0] is None:return Vector((.073*sin(theta),-.105*cos(theta),z)),d
        return hit[0],hit[1]
    def beard(u,v):
        theta=(u-.5)*2.24;side=abs(sin(theta))
        low=1.704+.041*side**1.5;high=1.766+.061*side**1.12
        high+=.00055*sin(theta*47)+.00025*sin(theta*93)
        low+=.0006*sin(theta*38+.5)
        z=low+(high-low)*v
        p,n=radial(theta,z)
        margin=(sin(pi*v)**.65)*(sin(pi*u)**.5)
        depth=.0003+.0034*margin+.00025*sin(theta*53+v*18)*margin
        return p+n*depth
    patch('RS_Continuous_Short_Beard',beard,'RS_Hair',144,46,.00065)
    groom_rng=random.Random(21261)
    for k in range(170):
        u=groom_rng.uniform(.03,.97);v=groom_rng.uniform(.18,.92)
        span=groom_rng.uniform(.08,.17)
        pts=[beard(u,v),beard(u+.002*sin(k),v-span*.5),beard(u+.003*sin(k),v-span)]
        curve('RS_Beard_Integrated_Lock',pts,groom_rng.uniform(.00045,.00085),'RS_Hair',[.25,1,.03],res=2)
    for k in range(120):
        u=(k+.2+groom_rng.random()*.5)/120
        lower=Vector(beard(u,.96));edge=Vector(beard(u,1))
        tip=edge+Vector((groom_rng.uniform(-.0005,.0005),.0002,groom_rng.uniform(.0007,.0015)))
        curve('RS_Beard_Soft_Perimeter',[lower,edge,tip],.00043,'RS_Hair',[1,.6,.01],res=2)
    # The moustache is a single continuous shallow surface across the philtrum.
    # Its corners extend down to meet the sides of the beard; lips stay free.
    def moustache(u,v):
        x=(u-.5)*.078;t=abs(x)/.039
        mid=1.795-.012*t*t
        half=(.004+.004*sin(pi*t)**.6)*max(0,1-t*t)**.65
        z=mid+(v*2-1)*half
        hit=bvh.ray_cast(Vector((x,-.5,z)),Vector((0,1,0)),1)
        if hit[0] is None:return Vector((x,-.15,z))
        depth=.0003+.0020*(sin(pi*v)**.7)*(sin(pi*u)**.5)
        return hit[0]+hit[1]*depth
    patch('RS_Continuous_Moustache',moustache,'RS_Hair',88,20,.0005)
    for k in range(80):
        u=(k+.5)/80+groom_rng.uniform(-.002,.002);v=groom_rng.uniform(.56,.95)
        direction=-1 if u<.5 else 1
        pts=[moustache(u,v),moustache(clamp(u+.009*direction),v-.24),moustache(clamp(u+.019*direction),v-.48)]
        pts=[Vector(p)+Vector((0,-.00062,0)) for p in pts]
        mat='RS_HairDetail' if k%5==0 and 'RS_HairDetail' in MATS else 'RS_Hair'
        curve('RS_Moustache_Integrated_Lock',pts,groom_rng.uniform(.00060,.00078),mat,[.20,1,.02],res=3)

def torso_and_arms():
    profile=[]
    for i in range(45):
        t=i/44;z=1.04+.57*t
        rx=.18+.045*sin(pi*t)-.005*t;ry=.117+.013*sin(pi*t)
        if t>.88:rx-=.105*smooth(.88,1,t);ry-=.053*smooth(.88,1,t)
        profile.append((z,0,.008,rx,ry))
    finish(ring_surface('RS_Tailored_Underarm_Gambeson',profile,'RS_IvoryCloth',64,lambda t,a:.003*sin(a*12+1.2*t)*sin(pi*t)),.002)
    collar=ring_surface('RS_Standing_Padded_Collar',[(1.59,0,.008,.079,.074),(1.62,0,.008,.074,.070),(1.66,0,.008,.068,.068),(1.671,0,.008,.067,.067)],'RS_IvoryCloth',72)
    finish(collar,.003,1)
    curve('RS_Collar_Seamed_Rim',[(.068*cos(a),.008+.068*sin(a),1.666) for a in [i*2*pi/72 for i in range(72)]],.0017,'RS_ThreadIvory',closed=True)
    for s in [-1,1]:
        shoulder=Vector((s*.27,0,1.60));elbow=Vector((s*.36 if s<0 else s*.37,-.015 if s<0 else 0,1.30 if s<0 else 1.34));wrist=Vector((s*.38 if s<0 else .28,-.15 if s<0 else -.28,1.10 if s<0 else 1.32))
        # Continuous sleeve follows the actual elbow and wrist path. A rounded
        # Catmull interpolation avoids separate, intersecting limb cylinders.
        def center(t):
            if t<.52:return shoulder.lerp(elbow,t/.52)
            return elbow.lerp(wrist,(t-.52)/.48)
        vs=[];uv=[];nr=70;ns=56
        for i in range(nr+1):
            t=i/nr;c=center(t);tangent=(center(min(1,t+.005))-center(max(0,t-.005))).normalized()
            u=tangent.cross(Vector((0,1,0))).normalized();v=tangent.cross(u).normalized()
            radius=.078*(1-t)+.047*t+.005*sin(t*pi)
            for j in range(ns+1):
                a=j*2*pi/ns
                fold=.002*sin(a*10+18*t)+.006*exp(-((t-.52)/.14)**2)*sin(t*65+a*1.6)
                # Diamond quilting is actual shallow padded relief.
                quilt=.0028*(sin(t*17*pi+a*3)**2*sin(t*17*pi-a*3)**2)
                p=c+(u*cos(a)+v*sin(a))*(radius+fold+quilt)
                vs.append(p);uv.append((j/ns,t*2))
        fs=[(i*(ns+1)+j,i*(ns+1)+j+1,(i+1)*(ns+1)+j+1,(i+1)*(ns+1)+j) for i in range(nr) for j in range(ns)]
        finish(mesh('RS_Padded_Articulated_Sleeve',vs,fs,'RS_IvoryCloth',uv),.002)

def legs_and_boots():
    # Trousers have independent tailored legs and a covered, continuous seat.
    finish(ring_surface('RS_Trouser_Seat',[(.98,0,.02,.174,.12),(1.025,0,.018,.186,.126),(1.10,0,.014,.18,.124),(1.16,0,.01,.171,.115)],'RS_IvoryCloth',72),.003,1)
    for s in [-1,1]:
        advance=-.10 if s<0 else .06
        prof=[]
        for i in range(70):
            t=i/69;z=.16+.90*t;cx=s*(.16-.04*t);cy=advance*(1-t)+.014*t
            r=.051+.05*t+.014*sin(t*pi)
            inset=1-smooth(.41,.52,z);r=r*(1-inset)+.044*inset;cx=cx*(1-inset)+s*.16*inset;cy=cy*(1-inset)+(advance+.015)*inset
            prof.append((z,cx,cy,r,r*.86))
        def wrinkles(t,a):
            knee=exp(-((t-.48)/.17)**2);ankle=exp(-((t-.12)/.07)**2)
            return (.004*sin(a*7+t*15)+knee*.009*sin(t*69+sin(a)*1.6)+ankle*.005*sin(t*117+a))*smooth(.23,.4,t)
        finish(ring_surface('RS_Tailored_Breeches',prof,'RS_IvoryCloth',64,wrinkles),.002)
        # Last modeled along the toe-to-heel axis, with a real welt and sole.
        def shoe(name,scalez,offset,mat):
            profiles=[(-.196,.006,.021,.048),(-.182,.040,.034,.060),(-.145,.061,.041,.065),(-.09,.065,.05,.068),(-.035,.059,.075,.074),(.025,.055,.096,.098),(.079,.048,.08,.088),(.092,.014,.025,.047)]
            vs=[];uv=[];ns=48
            for i,(y,rx,rz,cz) in enumerate(profiles):
                for j in range(ns+1):
                    a=j*2*pi/ns
                    z=max(.02,cz+rz*sin(a))*scalez+offset
                    vs.append((s*.16+rx*cos(a),advance+y,z));uv.append((j/ns,i/(len(profiles)-1)))
            fs=[(i*(ns+1)+j,i*(ns+1)+j+1,(i+1)*(ns+1)+j+1,(i+1)*(ns+1)+j) for i in range(len(profiles)-1) for j in range(ns)]
            fs.append(tuple(reversed(range(ns))))
            fs.append(tuple((len(profiles)-1)*(ns+1)+j for j in range(ns)))
            return finish(mesh(name,vs,fs,mat,uv),.002,2)
        shoe('RS_Leather_Sculpted_Boot_Last',1,0,'RS_Leather')
        shoe('RS_Layered_Leather_Sole',.17,.004,'RS_Sole')
        # Supple shafts underneath greaves, irregular tension folds at ankle.
        prof=[]
        for i in range(30):
            t=i/29;z=.095+.35*t
            prof.append((z,s*.16,advance+.015,.057+.012*t,.061+.005*t))
        finish(ring_surface('RS_Fitted_Leather_Boot_Shaft',prof,'RS_Leather',64,lambda t,a:.0035*sin(t*44+a*.8)*exp(-((t-.17)/.17)**2)),.003)
        # Two stitched side seams; front is reserved for armour.
        for q in [-1,1]:
            pts=[(s*.16+q*(.058+.012*t),advance+.025,.105+.32*t) for t in [i/20 for i in range(21)]]
            curve('RS_Boot_Back_Seam',pts,.0014,'RS_LeatherLight')

def tabard_point(s,u,v):
    start=.011+.008*v;end=.16+.091*v
    x=s*(start+(end-start)*u)
    z=1.163-v*(.50 if s<0 else .44)+.023*sin(pi*u)*v+.01*u*v
    y=-.157-.016*u-.052*v+(.006+.015*v)*sin(u*3*pi+v*.45)+.0035*sin(v*17+u*8)*v
    waist_y=-.156*math.sqrt(max(.01,1-(x/.188)**2))-.003
    y=waist_y*(1-smooth(0,.24,v))+y*smooth(0,.24,v)
    return(x,y,z)

def kingdom_embroidery():
    # Relief follows the actual cloth surface, including the hanging pleat.
    # This is a small embroidered insignia, with no painted front-facing decal.
    def p(x,z):return Vector(tabard_point(-1,.52-x/.195,.63-z/.50))+Vector((0,-.0032,0))
    def lobe(name,outline):
        pts=[]
        for i,point in enumerate(outline):
            a=Vector(outline[(i-1)%len(outline)]);b=Vector(point);c=Vector(outline[(i+1)%len(outline)]);d=Vector(outline[(i+2)%len(outline)])
            for k in range(5):
                t=k/5;q=.5*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t)
                pts.append((q.x,q.y,0))
        ob=mesh(name,pts,[tuple(range(len(pts)))],'RS_ThreadGold',[(v[0]*10,v[1]*10) for v in pts])
        bm=bmesh.new();bm.from_mesh(ob.data);bmesh.ops.triangulate(bm,faces=list(bm.faces))
        bmesh.ops.subdivide_edges(bm,edges=list(bm.edges),cuts=3,use_grid_fill=True)
        bm.to_mesh(ob.data);bm.free()
        for vert in ob.data.vertices:vert.co=p(vert.co.x,vert.co.y)
        ob.data.update();finish(ob,.0007)
    lobe('RS_Embroidered_Fleur_Centre',[(0,.06),(-.012,.029),(-.01,.008),(0,-.022),(.01,.008),(.012,.029)])
    left=[(-.005,.01),(-.020,.030),(-.039,.027),(-.046,.012),(-.038,.004),(-.027,.007),(-.03,.017),(-.019,.016),(-.012,.001),(-.007,-.025),(-.001,-.025)]
    lobe('RS_Embroidered_Fleur_Left',left);lobe('RS_Embroidered_Fleur_Right',[(-x,z) for x,z in reversed(left)])
    lobe('RS_Embroidered_Fleur_Foot',[(-.007,-.018),(-.014,-.035),(-.006,-.031),(0,-.037),(.006,-.031),(.014,-.035),(.007,-.018)])
    lobe('RS_Embroidered_Fleur_Band',[(-.022,-.008),(-.022,-.015),(.022,-.015),(.022,-.008)])

def cape_surface(u,v):
    width=.27+.54*smooth(0,.75,v)+.15*(1-v);cx=-.105
    x=cx+(u-.5)*width
    z=1.604-v*(1.09+.13*u)-.018*sin(u*pi)*(1-v)+.04*sin(u*pi)*v
    y=.075+.23*smooth(0,.75,v)+.055*sin(v*pi)+(.006+.048*v)*sin(u*5.4*pi+.4*v)+.013*sin(u*11*pi+v)*v
    y+=.115*(1-v)**1.4
    return(x,y,z)

def back_yoke_point(u,v):
    # The upper seam follows the curved neck/shoulder junction. The lower seam
    # is the mantle surface itself, with its tangent, rather than a straight lip.
    upper=Vector((-.245+.38*u,.064+.073*sin(pi*u),1.627+.012*sin(pi*u)+.012*u))
    lower=Vector(cape_surface(u,.02))
    tangent=Vector(cape_surface(u,.026))-lower
    c1=upper+(lower-upper)*.30
    c2=lower-tangent*5
    p=upper*(1-v)**3+3*c1*(1-v)**2*v+3*c2*(1-v)*v*v+lower*v**3
    p.y+=.003*sin(6*pi*u+.7*v)*sin(pi*v)
    p.z+=.002*sin(4*pi*u+.7*v)*sin(pi*v)
    return p

def garments():
    # Two separately cut front skirts with an intentional central opening and
    # unequal drape over the forward and rear leg. No cylindrical robe.
    for s in [-1,1]:
        def panel(u,v,s=s):
            return tabard_point(s,u,v)
        patch('RS_Open_Split_Blue_Tabard',panel,'RS_BlueCloth',48,72,.004)
        for u in [0,1]:
            curve('RS_Tabard_Woven_Border',[Vector(panel(u,t))+Vector((0,-.002,0)) for t in [i/90 for i in range(91)]],.0042,'RS_ThreadGold')
            edge_stitches('RS_Tabard_Edge_Stitch',lambda t,u=u:Vector(panel(.027 if u==0 else .973,t))+Vector((0,-.003,0)),count=80)
        curve('RS_Tabard_Weighted_Hem',[Vector(panel(t,1))+Vector((0,-.002,0)) for t in [i/60 for i in range(61)]],.0047,'RS_ThreadGold')
        # Actual topstitch follows each hanging pleat subtly.
        edge_stitches('RS_Tabard_Hem_Stitch',lambda t:Vector(panel(t,.982))+Vector((0,-.003,0)),count=45)
    kingdom_embroidery()
    def rear(u,v):
        x=(u-.5)*(.35+.12*v);z=1.157-.37*v+.02*sin(u*pi);y=.134+.067*v+.019*sin(u*5*pi)*v
        return (x,y,z)
    patch('RS_Short_Back_Tabard',rear,'RS_BlueCloth',48,48,.004)
    # Asymmetric blue campaign cape, drawn into its left-shoulder pin. Fold
    # frequency and amplitude develop under gravity rather than a pleated tube.
    def cape(u,v):
        return cape_surface(u,v)
    patch('RS_Asymmetric_Heavy_Wool_Cape',cape,'RS_BlueCloth',80,100,.007)
    # A cloth lining, inset by 3mm, with its own material and low contrast.
    patch('RS_Cape_Inner_Weave',lambda u,v:Vector(cape(u,v))+Vector((0,-.004,0)),'RS_BlueCloth',64,80,.002)
    for u in [0,1]:
        curve('RS_Cape_Rolled_Edge',[cape(u,i/90) for i in range(91)],.006,'RS_BlueCloth')
        edge_stitches('RS_Cape_Hand_Stitch',lambda t,u=u:Vector(cape(.014 if u==0 else .986,t))+Vector((0,-.004,0)),count=100)
    curve('RS_Cape_Subtle_Gold_Hem',[cape(i/90,1) for i in range(91)],.004,'RS_ThreadGold')
    # Shoulder fold stays behind the polished breastplate.
    def shoulder(u,v):
        x=-.245+u*.14;y=.035+v*.16;z=1.647+.022*sin(v*pi)-.05*v+.005*sin(u*pi*2)
        return(x,y,z)
    patch('RS_Cape_Shoulder_Attachment',shoulder,'RS_BlueCloth',30,40,.004)
    # A tailored back yoke overlaps the scarf hem and the mantle's upper edge.
    # Its entire surface is outside the forged backplate, so the connection is
    # physically visible from behind rather than hidden by a presentation angle.
    def yoke(u,v):
        return back_yoke_point(u,v)
    patch('RS_Continuous_Cape_Back_Yoke',yoke,'RS_BlueCloth',58,38,.004)

def leather_goods():
    belt=ring_surface('RS_Saddler_Leather_Belt',[(1.145,0,0,.184,.151),(1.151,0,0,.188,.154),(1.198,0,0,.184,.152),(1.201,0,0,.181,.149)],'RS_Leather',96)
    finish(belt,.004,1)
    for z in [1.151,1.194]:
        curve('RS_Belt_Burnished_Edge',[(.187*cos(a),.154*sin(a),z) for a in [i*2*pi/120 for i in range(120)]],.0015,'RS_LeatherLight',closed=True)
        for i in range(100):
            a=i*2*pi/100;b=a+.025
            curve('RS_Belt_Saddle_Stitch',[(.188*cos(a),.155*sin(a),z),(.188*cos(b),.155*sin(b),z)],.0008,'RS_ThreadIvory',res=1)
    # Pouch has a convex front, gathered gusset and visibly overlapped flap.
    bag=sphere('RS_Leather_Utility_Pouch',(.21,.025,1.068),(.057,.038,.076),'RS_LeatherLight',48,32)
    def flap(u,v):
        x=.21+(u-.5)*.105;y=-.014-.009*sin(pi*u)-.005*sin(pi*v);z=1.137-.064*v+.012*(u-.5)**2
        return(x,y,z)
    patch('RS_Pouch_Overlapping_Flap',flap,'RS_Leather',32,32,.003)
    curve('RS_Pouch_Flap_Seam',[Vector(flap(i/50,1))+Vector((0,-.002,0)) for i in range(51)],.001,'RS_ThreadIvory')

def gloves():
    # Palm and fingers are anatomically placed around the actual weapon grips.
    # Four curved, connected finger segments encircle a vertical grip; the thumb
    # opposes them. Palm creases and knuckle pads remain leather, not metal.
    for s in [-1,1]:
        c=Vector((-.385,-.215,1.065)) if s<0 else Vector((.28,-.32,1.30))
        wrist=Vector((-.38,-.15,1.10)) if s<0 else Vector((.28,-.28,1.32))
        palm=sphere('RS_Closed_Leather_Palm',c+Vector((0,.027,0)),(.037,.025,.055),'RS_Leather',44,28)
        sphere('RS_Glove_Thumb_Base',c+Vector((-.024 if s<0 else .024,.014,.027)),(.016,.022,.027),'RS_Leather',32,20)
        # Cuff opening fitted at wrist, reserved top for steel gauntlet plate.
        direction=(c-wrist).normalized();u=direction.cross(Vector((1,0,0))).normalized();v=direction.cross(u).normalized()
        vs=[];uv=[];n=48
        for i in range(12):
            t=i/11;cc=wrist-direction*.019+direction*.06*t
            for j in range(n+1):
                a=j*2*pi/n;r=.041-.005*t+.0015*sin(a*7+t*8)
                vs.append(cc+(u*cos(a)+v*sin(a))*r);uv.append((j/n,t))
        fs=[(i*(n+1)+j,i*(n+1)+j+1,(i+1)*(n+1)+j+1,(i+1)*(n+1)+j) for i in range(11) for j in range(n)]
        finish(mesh('RS_Glove_Wrist_Gusset',vs,fs,'RS_Leather',uv),.002)
        for finger in range(4):
            z=c.z+.034-finger*.022
            # Angular arcs wrap from dorsal knuckle to fingertips on the grip.
            points=[]
            for k in range(9):
                a=-.20+3.95*k/8;r=.028 if finger<3 else .025
                points.append((c.x+r*cos(a),c.y+r*sin(a),z-.0015*sin(a)))
            curve('RS_Anatomical_Curled_Finger',points,.0105 if finger<3 else .009,'RS_Leather',radii=[.83,1,1,1,.98,.9,.78,.55,.20],res=3)
            # Fine joint seams across two visible knuckles, not transverse rings.
            for a in [.15,1.10]:
                p=Vector((c.x+.029*cos(a),c.y+.029*sin(a),z));d=Vector((-sin(a),cos(a),0))*.006
                curve('RS_Glove_Finger_Seam',[p-d+Vector((0,0,.009)),p+Vector((0,0,.010)),p+d+Vector((0,0,.009))],.00075,'RS_LeatherLight',res=2)
        th=[c+Vector((-.026 if s<0 else .026,.028,.033)),c+Vector((-.041 if s<0 else .041,.005,.037)),c+Vector((-.028 if s<0 else .028,-.024,.031)),c+Vector((-.009 if s<0 else .009,-.033,.022))]
        curve('RS_Opposed_Gripping_Thumb',th,.015,'RS_Leather',radii=[1,1,.8,.2],res=5)

def fit_tailoring():
    """Fit the independent cloth patterns inside the authored armour profiles.

    Positions are the same anatomical anchors as royal_soldier_armor.py. The
    cloth remains continuous; no triangle deletion or invisible clipping masks.
    """
    chest=[(1.205,.172,.012,.116),(1.225,.177,.01,.124),(1.255,.183,.009,.136),
           (1.30,.198,.012,.155),(1.36,.219,.018,.179),(1.42,.237,.024,.192),
           (1.47,.246,.028,.185),(1.515,.236,.03,.170),(1.545,.216,.027,.147)]
    def interp(rows,z):
        if z<=rows[0][0]:return rows[0][1:]
        if z>=rows[-1][0]:return rows[-1][1:]
        for a,b in zip(rows,rows[1:]):
            if a[0]<=z<=b[0]:
                t=(z-a[0])/(b[0]-a[0]);return tuple(x+(y-x)*t for x,y in zip(a[1:],b[1:]))
    for ob in list(bpy.data.objects):
        if ob.type!='MESH':continue
        if ob.name.startswith('RS_Tailored_Underarm_Gambeson'):
            for v in ob.data.vertices:
                x,y,z=v.co
                if z>1.553:continue
                width,ey,depth=interp(chest,z)
                if z<1.205:width=.16;depth=.11;ey=.015
                x=clamp(x,-width*.93,width*.93)
                u=abs(x)/width
                front=ey-depth*math.sqrt(max(0,1-u*u*.92))-.006+.013
                back=.015+depth*.75*math.sqrt(max(0,1-u*u*.92))-.013
                v.co.x=x;v.co.y=clamp(y,front,back)
        elif ob.name.startswith('RS_Padded_Articulated_Sleeve'):
            side=-1 if sum(v.co.x for v in ob.data.vertices)<0 else 1
            sh=Vector((side*.27,0,1.60));el=Vector((-.36,-.015,1.30) if side<0 else (.37,0,1.34));wr=Vector((-.38,-.15,1.10) if side<0 else (.28,-.28,1.32))
            for v in ob.data.vertices:
                candidates=[]
                for a,b in [(sh,el),(el,wr)]:
                    axis=b-a;t=clamp((v.co-a).dot(axis)/axis.length_squared);c=a+axis*t;candidates.append(((v.co-c).length_squared,c))
                c=min(candidates,key=lambda q:q[0])[1]
                v.co=c+(v.co-c)*.77
        elif ob.name.startswith('RS_Tailored_Breeches'):
            side=-1 if sum(v.co.x for v in ob.data.vertices)<0 else 1
            advance=-.10 if side<0 else .06;kneey=-.07 if side<0 else .045
            for v in ob.data.vertices:
                x,y,z=v.co;t=clamp((z-.16)/.90)
                oldcx=side*(.16-.04*t);oldcy=advance*(1-t)+.014*t
                inset=1-smooth(.41,.52,z);oldcx=oldcx*(1-inset)+side*.16*inset;oldcy=oldcy*(1-inset)+(advance+.015)*inset
                if z<.59:
                    u=clamp((z-.15)/.44);cx=side*(.16-.01*u);cy=advance+(kneey-advance)*u
                else:
                    u=clamp((z-.59)/.47);cx=side*(.15-.03*u);cy=kneey+(.014-kneey)*u
                radius=interp([(.16,.044),(.43,.044),(.52,.05),(.59,.057),(.65,.059),(.76,.070),(.91,.092),(1.06,.101)],z)[0]
                oldradius=.051+.05*t+.014*sin(t*pi);oldradius=oldradius*(1-inset)+.044*inset
                ratio=radius/oldradius
                v.co.x=cx+(x-oldcx)*ratio;v.co.y=cy+(y-oldcy)*ratio
        ob.data.update()

def campaign_collar():
    """Draped blue neck scarf joins the campaign cape over the cuirass opening."""
    def cowl(u,v):
        a=u*2*pi-.15*v
        front=max(0,-sin(a));back=max(0,sin(a));left=max(0,-cos(a))
        outer_x=.148+.040*left;outer_y=.128+.052*front
        rx=.072+(outer_x-.072)*v;ry=.067+(outer_y-.067)*v
        fold=.008*sin(2.5*pi*v+.65*cos(a))*sin(pi*v)
        x=(rx+fold)*cos(a);y=.008+(ry+fold)*sin(a)
        front_angle=abs(math.atan2(sin(a+pi/2),cos(a+pi/2)))
        pointed_fall=max(0,1-front_angle/1.45)
        lower=1.639-.113*pointed_fall-.016*left-.009*sin(a*2)
        z=1.693+(lower-1.693)*v
        z+=.013*sin(2.5*pi*v+.65*cos(a))*sin(pi*v)
        return(x,y,z)
    patch('RS_Draped_Blue_Campaign_Collar',cowl,'RS_BlueCloth',128,36,.005)
    curve('RS_Campaign_Collar_Folded_Neck_Rim',[cowl(i/120,0) for i in range(120)],.0021,'RS_BlueCloth',closed=True)
    curve('RS_Campaign_Collar_Weighted_Hem',[cowl(i/120,1) for i in range(120)],.002,'RS_BlueCloth',closed=True)

def preview():
    # A neutral photographic look-dev stage is preview-only, never exported.
    scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=48
    scene.cycles.use_denoising=True;scene.render.resolution_x=1200;scene.render.resolution_y=1600;scene.render.resolution_percentage=100
    scene.world.color=(.16,.16,.16);scene.view_settings.view_transform='AgX'
    stage=bpy.data.materials.new('Preview_Stage');stage.use_nodes=True;stage.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=(.08,.10,.115,1)
    bpy.ops.mesh.primitive_plane_add(size=200);floor=bpy.context.object;floor.name='PREVIEW_ONLY_Floor';floor.data.materials.append(stage)
    def area(name,loc,energy,size,col):
        d=bpy.data.lights.new(name,'AREA');d.energy=energy;d.shape='DISK';d.size=size;d.color=col
        o=bpy.data.objects.new(name,d);bpy.context.collection.objects.link(o);o.location=loc;o.rotation_euler=(Vector((0,0,1))-o.location).to_track_quat('-Z','Y').to_euler()
    area('PREVIEW_ONLY_Key',(-3,-4,5),500,4,(1,.86,.73));area('PREVIEW_ONLY_Fill',(3,-2,3),160,3,(.67,.80,1));area('PREVIEW_ONLY_Rim',(1,3,4),650,3,(.80,.88,1))
    data=bpy.data.cameras.new('PREVIEW_ONLY_Camera');cam=bpy.data.objects.new('PREVIEW_ONLY_Camera',data);bpy.context.collection.objects.link(cam);cam.location=(2.8,-6.8,2.65);cam.rotation_euler=(Vector((0,0,1.05))-cam.location).to_track_quat('-Z','Y').to_euler();data.type='ORTHO';data.ortho_scale=2.4;scene.camera=cam
    scene.render.filepath=str(OUT/'body-preview.png');bpy.ops.render.render(write_still=True)
    cam.location=(.35,-3.4,1.88);cam.rotation_euler=(Vector((0,-.03,1.855))-cam.location).to_track_quat('-Z','Y').to_euler();data.ortho_scale=.46;scene.render.filepath=str(OUT/'body-face-preview.png');bpy.ops.render.render(write_still=True)
    # Append callers only need RS_* meshes and their semantic materials.
    for ob in list(bpy.data.objects):
        if ob.name.startswith('PREVIEW_ONLY'):bpy.data.objects.remove(ob,do_unlink=True)

def validate_groom_bounds():
    # Degenerate end handles in Blender's automatic Bezier interpolation can
    # generate an implausibly long final moustache fibre. Reject it explicitly.
    for ob in list(PARTS):
        if not ob.name.startswith('RS_Moustache_Integrated_Lock') or ob.type!='MESH':continue
        zs=[v.co.z for v in ob.data.vertices]
        if min(zs)<1.775 or max(zs)>1.81:
            print('REMOVED_OUT_OF_REGION_GROOM',ob.name,flush=True)
            PARTS.remove(ob);bpy.data.objects.remove(ob,do_unlink=True)

def main():
    OUT.mkdir(parents=True,exist_ok=True)
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    sys.path.insert(0,str(HERE))
    import royal_soldier_materials as surface_library
    MATS.update(surface_library.register_materials())
    for label,fn in [('Anatomy',anatomy),('Torso and sleeves',torso_and_arms),('Legs and boots',legs_and_boots),('Tailored garments',garments),('Leather goods',leather_goods),('Gripping gloves',gloves),('Armour fitted cloth',fit_tailoring),('Blue campaign collar',campaign_collar)]:
        print('AUTHORING',label,flush=True);fn()
    print('CONVERTING_AUTHORED_CURVES',flush=True)
    bpy.ops.object.select_all(action='DESELECT')
    for ob in PARTS:
        if ob.type=='CURVE':ob.select_set(True);bpy.context.view_layer.objects.active=ob
    bpy.ops.object.convert(target='MESH')
    bpy.ops.object.select_all(action='DESELECT')
    validate_groom_bounds()
    for ob in PARTS:
        ob['royalSoldierPart']='body';ob['source']='Authored for Royal Soldier; anatomical head CC0 Blender Studio Human Base Meshes'
        if ob.type=='MESH':
            if not ob.data.uv_layers:ob.data.uv_layers.new(name='UVMap')
            ob.data.update();bm=bmesh.new();bm.from_mesh(ob.data);bmesh.ops.recalc_face_normals(bm,faces=bm.faces);bm.to_mesh(ob.data);bm.free()
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'body.blend'))
    report={'source':str(BASE),'sourceLicense':'CC0','sourceObject':'GEO-body_male_realistic / Dan Ulrich','authoredScript':str(Path(__file__).resolve()),'meshObjects':len(PARTS),'triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in PARTS if o.type=='MESH'),'materialNames':list(MATS),'coordinateConvention':'metres; Z up; front -Y; ground0','staticPose':True,'noRigOrAnimation':True,'colorAttribute':{'name':'Color','domain':'POINT','semantics':'linear multiplicative tint; white neutral; skin only'},'anchors':{'swordGrip':[-.385,-.215,1.065],'shieldGrip':[.28,-.32,1.30]}}
    (OUT/'body-manifest.json').write_text(json.dumps(report,indent=2))
    preview()
    print('ROYAL_SOLDIER_BODY_OK',json.dumps(report))

if __name__=='__main__':main()
