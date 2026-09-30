"""Author the Kingdom premium courtyard in Blender 4.5; export actual static FBX.

Run with Blender --background --factory-startup --python tools/art/kingdom_environment.py.
Source downloads and editable .blend live on D:, never in Unity's import directory.
Coordinates are metres: Blender X=Unity X, Blender Y=Unity Z, Blender Z=Unity Y.
The facade faces -Y in Blender / -Z in Unity. Characters stand at (-2.2, -3, 0),
(0, -3, 0), (2.4, -3, 0) in Blender; no prop occupies that reservation.
"""
from __future__ import annotations
import json
import math
import random
import shutil
import subprocess
from pathlib import Path
import bpy
from mathutils import Matrix, Vector

ROOT = Path(__file__).resolve().parents[2]
CACHE = Path('D:/CodexTooling/kingdom-premium/environment')
OUTPUT = ROOT / 'Assets/Game/KingdomPremium/Environment'
TEXTURES = OUTPUT / 'Textures'
RNG = random.Random(926103)
ASSETS = json.loads((CACHE / 'download-index.json').read_text(encoding='utf-8'))
bpy.ops.wm.open_mainfile(filepath=str(CACHE / 'imports.blend'))
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1
bpy.context.preferences.filepaths.temporary_directory = str(CACHE / 'temp')
collection = bpy.data.collections.new('KingdomPremiumEnvironment')
scene.collection.children.link(collection)
OUTPUT.mkdir(parents=True, exist_ok=True)
TEXTURES.mkdir(parents=True, exist_ok=True)
MATERIALS = {}
MANIFEST = []
AUTHORED = []
SOURCE_CACHE = {}
STATS = {}


def material(name, asset=None, color=(1, 1, 1, 1), prefix='', cutout=False,
             metallic=0, roughness=.7):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    mat.diffuse_color = color
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    bsdf = nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = color
    bsdf.inputs['Roughness'].default_value = roughness
    bsdf.inputs['Metallic'].default_value = metallic
    bsdf.inputs['Specular IOR Level'].default_value = .3
    record = dict(name=name, baseColorTexture='', normalTexture='', roughnessTexture='',
                  metallicTexture='', opacityTexture='', color=list(color), metallic=metallic,
                  smoothness=1-roughness, tiling=[1, 1], cutout=cutout, doubleSided=cutout, colorSpace='linear')
    if asset:
        data = ASSETS[asset]
        keys = ({'baseColorTexture': prefix+'diff', 'normalTexture': prefix+'nor_gl',
                 'roughnessTexture': prefix+'rough', 'opacityTexture': prefix+'alpha'} if prefix else
                {'baseColorTexture': 'Diffuse', 'normalTexture': 'nor_gl',
                 'roughnessTexture': 'Rough', 'metallicTexture': 'Metal', 'opacityTexture': 'Alpha'})
        for semantic, key in keys.items():
            if key not in data['maps'] or (semantic == 'opacityTexture' and not cutout):
                continue
            source = Path(data['maps'][key])
            target = TEXTURES / source.name
            if not target.exists():
                shutil.copy2(source, target)
            record[semantic] = 'Textures/' + target.name
            node = nodes.new('ShaderNodeTexImage')
            node.image = bpy.data.images.load(str(source), check_existing=True)
            node.interpolation = 'Linear'
            if semantic != 'baseColorTexture':
                node.image.colorspace_settings.name = 'Non-Color'
            if semantic == 'normalTexture':
                normal = nodes.new('ShaderNodeNormalMap')
                normal.inputs['Strength'].default_value = .72
                links.new(node.outputs['Color'], normal.inputs['Color'])
                links.new(normal.outputs['Normal'], bsdf.inputs['Normal'])
            else:
                target_socket = {'baseColorTexture':'Base Color','roughnessTexture':'Roughness',
                                 'metallicTexture':'Metallic','opacityTexture':'Alpha'}[semantic]
                if semantic == 'baseColorTexture' and color[:3] != (1, 1, 1):
                    tint = nodes.new('ShaderNodeMixRGB')
                    tint.blend_type = 'MULTIPLY'
                    tint.inputs[0].default_value = 1
                    tint.inputs[2].default_value = color
                    links.new(node.outputs['Color'], tint.inputs[1])
                    links.new(tint.outputs[0], bsdf.inputs[target_socket])
                else:
                    links.new(node.outputs['Color'], bsdf.inputs[target_socket])
    if cutout:
        mat.surface_render_method = 'DITHERED'
        mat.use_transparency_overlap = False
        mat.use_backface_culling = False
    MATERIALS[name] = mat
    MANIFEST.append(record)
    return mat


stone = material('ENV_Limestone', 'medieval_blocks_05', (.88,.87,.81,1))
trim = material('ENV_CutLimestone', 'medieval_blocks_05', (.96,.95,.87,1))
cobble = material('ENV_CourtyardCobble', 'cobblestone_floor_08', (.90,.90,.84,1))
soil = material('ENV_WoodlandSoil', 'leafy_grass', (.32,.8,.25,1))
slate = material('ENV_Slate', 'roof_slates_02', (.26,.38,.50,1))
slate_light = material('ENV_SlateWeathered', 'roof_slates_02', (.34,.46,.57,1))
plaster = material('ENV_LimePlaster', 'white_rough_plaster', (.73,.69,.58,1))
# Fresh limewash covers the source scan's black paint stains. Its actual scanned
# roughness and normal detail remain, while the albedo is a physical plaster tint.
plaster_bsdf=plaster.node_tree.nodes.get('Principled BSDF')
for link in list(plaster_bsdf.inputs['Base Color'].links):plaster.node_tree.links.remove(link)
plaster_bsdf.inputs['Base Color'].default_value=(.73,.69,.58,1)
MANIFEST[-1]['baseColorTexture']=''
timber = material('ENV_Oak', 'wood_planks_dirt', (.50,.38,.24,1))
door_mat = material('ENV_CastleDoor', 'large_castle_door')
barrel_mat = material('ENV_Barrel', 'wine_barrel_01')
crate_mat = material('ENV_Crate', 'wooden_crate_01')
fern_mat = material('ENV_Fern', 'fern_02', (.79,.86,.71,1), cutout=True)
grass_mat = material('ENV_Grass', 'grass_medium_01', (.83,.87,.73,1), cutout=True)
rock_mat = material('ENV_MossRock', 'rock_moss_set_01')
bark_mat = material('ENV_TreeBark', 'tree_small_02')
branch_mat = material('ENV_TreeBranch', 'tree_small_02', prefix='branch_')
leaf_mat = material('ENV_TreeLeaf', 'tree_small_02', (.85,.91,.80,1), prefix='leaves_', cutout=True)
iron = material('ENV_ForgedIron', color=(.045,.055,.064,1), metallic=.85, roughness=.43)
brass = material('ENV_AgedBrass', color=(.31,.20,.065,1), metallic=.7, roughness=.38)
cloth = material('ENV_KingdomBlueCloth', color=(.035,.11,.23,1), roughness=.83)
cloth_gold = material('ENV_KingdomGoldEmbroidery', color=(.63,.43,.12,1), metallic=.18, roughness=.65)
glass = material('ENV_OldWindowGlass', color=(.07,.12,.105,1), metallic=.25, roughness=.3)
dark = material('ENV_InteriorShadow', color=(.023,.020,.016,1), roughness=1)
lantern_glass = material('ENV_LanternGlass', color=(.81,.51,.19,1), roughness=.31)
lantern_glass.node_tree.nodes.get('Principled BSDF').inputs['Emission Color'].default_value = (.32,.13,.025,1)
lantern_glass.node_tree.nodes.get('Principled BSDF').inputs['Emission Strength'].default_value = .4


def own(obj):
    for old in list(obj.users_collection):
        old.objects.unlink(obj)
    collection.objects.link(obj)
    AUTHORED.append(obj)
    return obj


def planar_uv(obj, metres=2.0, offset=(0,0)):
    """Physical world-size projection, including vertical and bevel faces."""
    mesh = obj.data
    mesh.update()
    uv = mesh.uv_layers.new(name='UVMap') if not mesh.uv_layers else mesh.uv_layers[0]
    for poly in mesh.polygons:
        n = poly.normal
        axis = max(range(3), key=lambda i: abs(n[i]))
        for loop_index in poly.loop_indices:
            p = mesh.vertices[mesh.loops[loop_index].vertex_index].co
            coord = (p.y,p.z) if axis == 0 else (p.x,p.z) if axis == 1 else (p.x,p.y)
            uv.data[loop_index].uv = (coord[0]/metres+offset[0],coord[1]/metres+offset[1])


def box(name, location, dimensions, mat, bevel=.025, rotation=(0,0,0), uv_metres=2):
    x,y,z=(float(v)*.5 for v in dimensions)
    verts=[(-x,-y,-z),(x,-y,-z),(x,y,-z),(-x,y,-z),(-x,-y,z),(x,-y,z),(x,y,z),(-x,y,z)]
    faces=[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)]
    obj=mesh_obj(name,verts,faces,mat,False,uv_metres)
    obj.location=location
    obj.rotation_euler = rotation
    planar_uv(obj, uv_metres, (RNG.random(),RNG.random()))
    if bevel:
        modifier = obj.modifiers.new('Crafted softened edges', 'BEVEL')
        modifier.width = bevel
        modifier.segments = 3
        modifier.affect = 'EDGES'
        modifier = obj.modifiers.new('Weighted corner normals', 'WEIGHTED_NORMAL')
        modifier.keep_sharp = True
    return obj


def mesh_obj(name, vertices, faces, mat, smooth=False, uv_metres=2):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    AUTHORED.append(obj)
    mesh.materials.append(mat)
    for p in mesh.polygons:
        p.use_smooth = smooth
    planar_uv(obj, uv_metres)
    return obj


def beam(name, a, b, width, depth, mat=timber):
    a,b = Vector(a),Vector(b)
    obj = box(name, (a+b)/2, (width,depth,(b-a).length), mat, min(width,depth)*.12)
    obj.rotation_euler = (b-a).to_track_quat('Z','Y').to_euler()
    return obj


def cylinder(name, location, radius, depth, mat, vertices=16, rotation=(0,0,0)):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=location, rotation=rotation)
    obj = own(bpy.context.object)
    obj.name = name
    obj.data.materials.append(mat)
    planar_uv(obj, 1)
    bevel = obj.modifiers.new('Edge bevel', 'BEVEL'); bevel.width=.01; bevel.segments=2
    return obj


def polygon_prism(name, points, depth, y, mat, bevel=.01):
    """Extrude front-profile X/Z polygon through Y."""
    n=len(points)
    verts=[(x, yy, z) for yy in (y-depth/2,y+depth/2) for x,z in points]
    faces=[tuple(reversed(range(n))),tuple(range(n,2*n))]
    faces += [(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    obj=mesh_obj(name,verts,faces,mat)
    if bevel:
        mod=obj.modifiers.new('Worn arris','BEVEL');mod.width=bevel;mod.segments=3
        obj.modifiers.new('Face normals','WEIGHTED_NORMAL')
    return obj


def source_mesh(asset_id, variant=None):
    key=(asset_id,variant)
    if key in SOURCE_CACHE: return SOURCE_CACHE[key]
    sources=[o for o in bpy.data.collections['SOURCE_'+asset_id].objects if o.type=='MESH' and (not variant or o.name==variant)]
    copies=[]
    for source in sources:
        obj=bpy.data.objects.new('PREP_'+source.name, source.data.copy())
        collection.objects.link(obj)
        obj.data.transform(source.matrix_world)
        copies.append(obj)
    bpy.ops.object.select_all(action='DESELECT')
    for obj in copies: obj.select_set(True)
    bpy.context.view_layer.objects.active=copies[0]
    if len(copies)>1: bpy.ops.object.join()
    obj=bpy.context.view_layer.objects.active
    coords=[v.co for v in obj.data.vertices]
    minimum=Vector([min(v[i] for v in coords) for i in range(3)])
    maximum=Vector([max(v[i] for v in coords) for i in range(3)])
    origin=Vector(((minimum.x+maximum.x)/2,(minimum.y+maximum.y)/2,minimum.z))
    if asset_id in ('large_castle_door','tree_small_02'): origin=Vector((0,0,minimum.z))
    obj.data.transform(Matrix.Translation(-origin))
    remap={'large_castle_door':door_mat,'wine_barrel_01':barrel_mat,'wooden_crate_01':crate_mat,
           'fern_02':fern_mat,'grass_medium_01':grass_mat,'rock_moss_set_01':rock_mat}
    if asset_id=='tree_small_02':
        preserved=CACHE/'tree-final.blend'
        if not preserved.exists():raise RuntimeError('Run kingdom_environment_tree.py in Blender first')
        with bpy.data.libraries.load(str(preserved),link=False) as (library,target):
            target.objects=['KingdomCrownTree']
        prepared=target.objects[0]
        obj.data=prepared.data
        obj.data.transform(prepared.matrix_world)
        obj.data.transform(Matrix.Translation((0,0,-min(v.co.z for v in obj.data.vertices))))
        bpy.data.objects.remove(prepared,do_unlink=True)
        for i,old in enumerate(obj.data.materials):
            obj.data.materials[i] = leaf_mat if 'leaves' in old.name else branch_mat if 'branches' in old.name else bark_mat
        after=sum(len(p.vertices)-2 for p in obj.data.polygons)
        STATS['tree']={'sourceTriangles':2062487,'exportTrianglesPerTree':after,'method':'Moderate collapse; native crown comparison against untouched source'}
        print('TREE_OPTIMIZED '+json.dumps(STATS['tree']),flush=True)
    else:
        for i in range(len(obj.data.materials)): obj.data.materials[i]=remap[asset_id]
    mesh=obj.data
    mesh.name='CC0_'+asset_id+('_'+variant if variant else '')
    bpy.data.objects.remove(obj,do_unlink=True)
    SOURCE_CACHE[key]=mesh
    return mesh


def instance(asset_id, location, scale=1, rotation=0, variant=None, label=None):
    mesh=source_mesh(asset_id,variant)
    obj=bpy.data.objects.new(label or asset_id,mesh)
    collection.objects.link(obj)
    obj.location=location
    obj.scale=(scale,)*3 if isinstance(scale,(int,float)) else scale
    obj.rotation_euler.z=rotation
    AUTHORED.append(obj)
    return obj


def arch_stones(x, y, spring, radius, width=.23, depth=.34, segments=13):
    for i in range(segments):
        a=math.pi*i/segments+.009
        b=math.pi*(i+1)/segments-.009
        pts=[(x+radius*math.cos(a),spring+radius*math.sin(a)),
             (x+(radius+width)*math.cos(a),spring+(radius+width)*math.sin(a)),
             (x+(radius+width)*math.cos(b),spring+(radius+width)*math.sin(b)),
             (x+radius*math.cos(b),spring+radius*math.sin(b))]
        polygon_prism('Individual voussoir %02d'%i,pts,depth,y,trim,.012)


def window(x,y,z,width=1.0,height=1.45):
    box('Deep window recess',(x,y+.16,z),(width+.15,.16,height+.18),dark,.01)
    box('Lead glass panes',(x,y+.06,z),(width,.055,height),glass,.009)
    box('Window stone sill',(x,y-.19,z-height/2-.07),(width+.47,.58,.15),trim,.045)
    box('Window weather lintel',(x,y-.13,z+height/2+.08),(width+.35,.38,.16),trim,.035)
    for dx in (-width/2-.07,width/2+.07):
        box('Window jamb',(x+dx,y-.08,z),(.14,.34,height+.04),trim,.022)
    for dx in (-width/2,0,width/2):
        box('Oak window mullion',(x+dx,y-.01,z),(.055,.085,height),timber,.008)
    box('Oak window transom',(x,y-.01,z+.12),(width,.085,.05),timber,.008)
    # Leaded glass diamonds are real narrow metal strips.
    for sign in (-1,1):
        for t in (-.5,-.25,0,.25,.5):
            a=Vector((x-width/2,y-.065,z+t-.23*sign))
            b=Vector((x+width/2,y-.065,z+t+.23*sign))
            beam('Leaded glass diagonal',a,b,.012,.012,iron)


def carve_window(wall, x, y, z, width, height):
    cutter=box('Temporary window opening',(x,y,z),(width,.95,height),dark,0)
    bpy.context.view_layer.objects.active=wall
    mod=wall.modifiers.new('True window opening','BOOLEAN');mod.operation='DIFFERENCE';mod.solver='EXACT';mod.object=cutter
    bpy.ops.object.modifier_apply(modifier=mod.name)
    AUTHORED.remove(cutter);bpy.data.objects.remove(cutter,do_unlink=True)


def build_gatehouse():
    # Real lower-storey masonry separated around the arched doorway.
    front_y,back_y=3.12,8.85
    for side in (-1,1):
        wall=box('Gatehouse lower facade', (side*3.05,front_y,1.87),(3.50,.52,3.68),stone,.035)
        carve_window(wall,side*3.1,front_y,2.28,1.05,1.40)
        window(side*3.1,front_y-.29,2.28,1.05,1.4)
        box('Solid stone flank',(side*4.68,6,1.88),(.48,5.6,3.70),stone,.045)
        box('Foundation plinth',(side*3.15,front_y-.06,.18),(3.7,.72,.32),trim,.045)
        box('Continuous side foundation',(side*4.7,6,.18),(.7,5.85,.32),trim,.04)
        # Discrete dressed corner stones with staggered masonry courses.
        for y in (front_y-.03,back_y-.02):
            for row in range(10):
                dimensions=(.67,.56,.34) if row%2 else (.53,.75,.34)
                box('Dressed limestone quoins',(side*4.69,y,.43+row*.35),dimensions,trim,.035)
    box('Back wall',(0,8.82,1.86),(9.6,.48,3.66),stone,.04)
    # Arch infill has a genuinely curved underside; the ironwork door sits inside it.
    radius,spring=1.04,2.04
    points=[(-1.3,3.7),(1.3,3.7),(1.3,spring)]
    points.extend([(radius*math.cos(math.pi*i/24),spring+radius*math.sin(math.pi*i/24)) for i in range(25)])
    points.append((-1.3,spring))
    polygon_prism('Solid masonry above arch',points,.53,front_y,stone,.018)
    arch_stones(-.04,front_y-.32,2.04,1.045,.24,.40,15)
    for side in (-1,1):
        for row in range(6):
            box('Portal jamb block',(side*1.21-.04,front_y-.32,.22+row*.325),(.30,.41,.31),trim,.025)
    instance('large_castle_door',(-.04,front_y-.12,.025),1.0,label='CC0 iron-bound double castle door')
    box('Door threshold',(0,front_y-.28,.035),(2.4,.68,.09),trim,.025)
    # Timber-framed upper chamber, with two recessed windows.
    upper=box('Limewashed upper chamber',(0,6.0,4.43),(9.35,5.64,1.44),plaster,.022)
    for x in (-2.85,2.85):
        carve_window(upper,x,3.2,4.46,.86,1.10)
        window(x,2.98,4.46,.86,1.10)
    for z in (3.68,5.10):
        box('Hand-hewn floor bressummer',(0,2.82,z),(9.9,.3,.25),timber,.026)
        box('Side bressummer left',(-4.78,6,z),(.25,6.5,.25),timber,.026)
        box('Side bressummer right',(4.78,6,z),(.25,6.5,.25),timber,.026)
    for x in (-4.64,-3.63,-2.03,-.75,.75,2.03,3.63,4.64):
        box('Upper facade timber stud',(x,2.84,4.39),(.17,.23,1.26),timber,.02)
    for x in (-4.35,-1.38,1.38,4.35):
        beam('Splayed oak diagonal',(x-.3,2.81,3.85),(x+.3,2.81,4.96),.12,.21)
    # Oak pegs at structural intersections.
    for x in (-4.64,-3.63,-2.03,-.75,.75,2.03,3.63,4.64):
        for z in (3.74,5.1):
            cylinder('Exposed forged timber peg',(x,2.652,z),.029,.025,iron,12,(math.pi/2,0,0))
    # Lateral plaster gables and layered roof: every tile has depth and overlap.
    for x in (-4.69,4.69):
        obj=polygon_prism('Gable infill',[(-2.84,5.1),(2.84,5.1),(0,7.38)],.17,0,plaster,0)
        # profile coordinates are temporarily X/Z, then rotated into Y/Z.
        obj.rotation_euler.z=math.pi/2;obj.location=(x,6,0)
        beam('Gable bargeboard',(x,2.8,5.1),(x,6,7.43),.17,.22)
        beam('Gable bargeboard',(x,6,7.43),(x,9.2,5.1),.17,.22)
        beam('Gable king post',(x,6,5.08),(x,6,7.38),.15,.20)
    pitch=math.atan2(2.32,3.48)
    for sign in (-1,1):
        slope= -sign*pitch
        box('Roof solid oak deck',(0,6+sign*1.76,6.10),(10.75,4.27,.14),timber,.015,(slope,0,0))
        rows,cols=11,21
        for row in range(rows):
            t=(row+.25)/rows
            y=6+sign*(3.6-3.48*t)
            z=5.10+2.32*t
            for col in range(cols):
                x=(col-(cols-1)/2)*.512+(.12 if row%2 else 0)
                box('Overlapping individual slate',(x,y,z),(.4996,.46,.048),slate if RNG.random()>.14 else slate_light,
                    .009,(slope,0,0),1.6)
    for x in [-5.2+i*.39 for i in range(28)]:
        cylinder('Segmented roof ridge capping',(x,6,7.51),.12,.40,slate_light,12,(0,math.pi/2,0))
    for y,z in ((2.36,4.99),(9.64,4.99)):
        box('Dark carved eaves fascia',(0,y,z),(11.0,.17,.24),timber,.02)
    # One masonry chimney: separate cap and inset flue, not a texture silhouette.
    box('Limestone chimney',(-3.45,7.2,7.12),(.86,.78,2.60),stone,.055)
    for z in (7.9,8.04):
        box('Chimney drip cap',(-3.45,7.2,z),(1.04,.94,.14),trim,.03)
    box('Recessed chimney flue',(-3.45,7.2,8.12),(.65,.55,.07),dark,.0)
    # Central decorated dormer over entrance.
    box('Dormer plaster',(0,4.18,6.5),(1.75,.8,1.25),plaster,.02)
    window(0,3.69,6.54,.85,.85)
    polygon_prism('Dormer front gable',[(-1.03,7.0),(1.03,7.0),(0,7.94)],.14,3.62,plaster,.012)
    for side in (-1,1):
        beam('Dormer carved roof edge',(side*1.18,3.51,6.98),(0,3.51,8.05),.14,.2)
        roof=box('Dormer slate pitch',(side*.54,4.08,7.52),(1.56,1.35,.09),slate,.018,(0,side*math.atan2(.98,1.12),0))
    beam('Dormer king post',(0,3.47,7.04),(0,3.47,7.98),.12,.18)
    # Lanterns, attached iron brackets and embossed fabric banners.
    for x in (-1.73,1.73):
        beam('Lantern wrought-iron bracket',(x,2.9,2.95),(x,2.36,2.95),.037,.037,iron)
        beam('Lantern hanging chain',(x,2.36,2.98),(x,2.36,2.62),.022,.022,iron)
        box('Lantern amber glazing',(x,2.36,2.35),(.24,.24,.43),lantern_glass,.012)
        for dx in (-.14,.14):
            for dy in (-.14,.14):
                beam('Lantern corner iron',(x+dx,2.36+dy,2.08),(x+dx,2.36+dy,2.62),.023,.023,iron)
        for z in (2.08,2.62):
            box('Lantern frame rim',(x,2.36,z),(.34,.34,.065),iron,.012)
        polygon_prism('Lantern peaked hood',[(x-.2,2.66),(x+.2,2.66),(x,2.87)],.34,2.36,iron,.01)
    for x in (-4.02,4.02):
        banner(x,2.55,3.37,.60,1.8)
    # Garden retaining walls extend the courtyard naturally into vegetation.
    for side in (-1,1):
        for row in range(3):
            for i in range(7):
                x=side*(5.20+i*.67)
                box('Low garden masonry',(x,6.3,.19+row*.28),(.66,.50,.265),stone,.034)
        box('Garden coping',(side*7.2,6.3,.97),(4.58,.65,.16),trim,.04)


def banner(x,y,top,width,length):
    cylinder('Banner crossbar',(x,y,top+.06),.033,width+.23,iron,16,(0,math.pi/2,0))
    nx,nz=8,18
    vertices=[];faces=[]
    for j in range(nz+1):
        v=j/nz
        for i in range(nx+1):
            u=i/nx
            yy=y-.05+math.sin(u*math.pi*4+v*.9)*.042*v
            zz=top-v*length + (.14*(1-abs(u-.5)*2) if j==nz else 0)
            vertices.append((x+(u-.5)*width,yy,zz))
    for j in range(nz):
        for i in range(nx):
            a=j*(nx+1)+i;faces.append((a,a+1,a+nx+2,a+nx+1))
    obj=mesh_obj('Sewn kingdom cloth banner',vertices,faces,cloth,True,.5)
    sol=obj.modifiers.new('Fabric thickness','SOLIDIFY');sol.thickness=.008
    # Thin border and a raised geometric wheat/sun heraldic motif are actual meshes.
    for side in (-1,1):
        beam('Gold woven banner border',(x+side*width*.41,y-.105,top-.11),
             (x+side*width*.41,y-.1,top-length+.15),.022,.012,cloth_gold)
    cylinder('Embossed banner sun',(x,y-.118,top-length*.46),.12,.012,cloth_gold,24,(math.pi/2,0,0))
    for a in range(8):
        angle=a*math.pi/4
        start=(x+math.cos(angle)*.155,y-.12,top-length*.46+math.sin(angle)*.155)
        end=(x+math.cos(angle)*.205,y-.12,top-length*.46+math.sin(angle)*.205)
        beam('Embroidered sunray',start,end,.026,.012,cloth_gold)


def build_ground():
    # A continuous shallow terrain mesh fills beyond the camera, no rectangular plinth.
    vertices=[];faces=[];count=96;extent=64
    for j in range(count+1):
        y=-27+extent*j/count
        for i in range(count+1):
            x=-32+extent*i/count
            border=min(1,max(0,(abs(x)-6)/8)+max(0,(abs(y-1)-7)/8))
            h=-.035+border*(.08*math.sin(x*.9)+.055*math.cos(y*.8))
            vertices.append((x,y,h))
    for j in range(count):
        for i in range(count):
            a=j*(count+1)+i;faces.append((a,a+1,a+count+2,a+count+1))
    mesh_obj('Ground woodland terrain continuous',vertices,faces,soil,True,2.6)
    # Courtyard polygon is flush with foot height; the irregular boundary is dressed
    # with real edging stones, tufts and moss scans, not an exposed flat rectangle.
    perimeter=[(-6.1,-7.6),(-4.6,-8.05),(-2.5,-7.8),(0,-8.0),(2.2,-7.78),(4.8,-7.7),
               (6.25,-6.7),(6.55,-3.8),(6.2,-1),(5.8,1.8),(5.1,3.0),
               (4.95,5.5),(-5.1,5.5),(-5.25,3),(-6.1,1),(-6.4,-1.9),(-6.15,-4.5)]
    vertices=[(0,-1,0)]+[(x,y,-.001) for x,y in perimeter]
    faces=[(0,i+1,(i+1)%len(perimeter)+1) for i in range(len(perimeter))]
    mesh_obj('Ground flush irregular cobblestone courtyard',vertices,faces,cobble,False,2)
    # Approaching worn steps / cobbles have tactile bevels and shallow worn heights.
    for side in (-1,1):
        for i in range(18):
            y=-7+i*.58
            x=side*(6.15-.17*math.sin(y*.8))
            box('Weathered courtyard edging',(x,y,-.004+RNG.uniform(-.008,.007)),
                (.22+RNG.random()*.06,.48+RNG.random()*.08,.095),trim,.027,(0,0,RNG.uniform(-.09,.09)),1.4)
    for side in (-1,1):
        for i in range(12):
            x=side*(.8+i*.43)
            box('Entrance cobble edging',(x,-7.85+RNG.uniform(-.08,.08),-.008),(.36,.25,.08),trim,.025,(0,0,RNG.uniform(-.08,.08)),1.4)
    # A narrow stone apron under the threshold and hand-cut drainage channel.
    for x in [i*.35 for i in range(-7,8)]:
        box('Entrance apron flagstone',(x,2.47,.008),(.335,.52,.07),trim,.019,(0,0,RNG.uniform(-.018,.018)),1.8)
    for y in [-5.6+i*.46 for i in range(15)]:
        box('Courtyard stone drain lip',(5.29,y,-.004),(.12,.43,.05),trim,.015)


def build_foliage_props():
    # Hero zone x[-4.8,4.8], y[-6.5,.5] remains free, including its camera sightline.
    tree_positions=[(-8.1,5.3,1.45,.25),(8.7,6.8,1.5,-.8),(-10.1,-.1,1.15,1.65),(10.1,1.4,1.2,.7),
                    (-12.5,10,1.75,1.1),(-7.5,14.5,1.65,2.2),(-1.5,16,1.55,-.7),
                    (6,15.5,1.85,.4),(13,13,1.75,-1.3),(-16,4.5,1.5,-.8),(16,6,1.65,1.4)]
    for x,y,scale,rotation in tree_positions:
        instance('tree_small_02',(x,y,-.035),scale,rotation,label='CC0 optimized living tree')
    rocks=['rock_moss_set_01_rock%02d'%i for i in range(1,7)]
    for i,(x,y,s) in enumerate([(-7,-2,.48),(7.4,-.8,.5),(-7.3,2,.55),(8.3,3.1,.52),(-9,7,.60),(8.7,8.8,.5),(-8,-6,.34),(8,-5.3,.39)]):
        instance('rock_moss_set_01',(x,y,-.19),s,RNG.uniform(0,6.28),rocks[i%6],label='CC0 mossy garden boulder')
    ferns=['fern_02_'+v for v in 'abcd']
    grass=['grass_medium_01_small_a_LOD0','grass_medium_01_small_b_LOD0',
           'grass_medium_01_mid_a_LOD0','grass_medium_01_mid_b_LOD0','grass_medium_01_tall_a_LOD0',
           'grass_medium_01_tall_b_LOD0','grass_medium_01_tall_c_LOD0']
    # Grounded fronds and textured individual blade mesh clusters, never ball canopies.
    for side in (-1,1):
        for i in range(41):
            y=RNG.uniform(-7,9.5);x=side*RNG.uniform(6.7,10.5)
            instance('fern_02',(x,y,-.038),RNG.uniform(1.4,2.52),RNG.uniform(0,6.28),RNG.choice(ferns),label='CC0 fern edge cluster')
        for i in range(275):
            y=RNG.uniform(-8,10.0);x=side*RNG.uniform(6.05,7.5) if i<100 else side*RNG.uniform(6.15,11.2)
            instance('grass_medium_01',(x,y,-.045),RNG.uniform(1.15,2.35),RNG.uniform(0,6.28),RNG.choice(grass),label='CC0 meadow tuft')
        for i in range(24):
            # Small sparse tufts break paving boundaries while preserving centre stage.
            y=RNG.uniform(-7,1.5);x=side*RNG.uniform(5.8,6.35)
            instance('grass_medium_01',(x,y,-.02),RNG.uniform(.8,1.15),RNG.uniform(0,6.28),RNG.choice(grass[:2]),label='Sparse cobble edge grass')
    for x,y,scale,rotation in [(-3.93,1.54,1.15,.3),(-4.45,2.13,1.05,-.2),(-3.76,2.4,1.13,1.3),(4.12,1.83,1.0,.65)]:
        instance('wine_barrel_01',(x,y,.008),scale,rotation,label='CC0 coopered iron-hoop barrel')
    for x,y,z,scale,rotation in [(3.13,1.53,0,1.45,.15),(3.12,1.56,.48,1.24,-.04),(4.3,.65,0,1.4,-.3),(-4.25,.52,0,1.3,.08)]:
        instance('wooden_crate_01',(x,y,z+.015),scale,rotation,label='CC0 detailed merchant chest')
    # A small crafted bench and water trough give the space a used, human scale.
    box('Oak bench seat',(-5.15,.5,.49),(.56,2.1,.12),timber,.04)
    for y in (-.17,1.17):
        box('Bench trestle foot',(-5.15,y,.24),(.43,.20,.48),timber,.028)
    beam('Bench stretcher',(-5.15,-.28,.21),(-5.15,1.28,.21),.12,.14)
    for x in (6.88,7.84):
        box('Trough side wall',(x,4.54,.39),(.15,1.85,.67),trim,.035)
    for y in (3.67,5.40):
        box('Trough end wall',(7.36,y,.39),(1.12,.16,.67),trim,.035)
    box('Trough dark interior',(7.36,4.54,.11),(.8,1.58,.14),stone,.02)


def finish_export():
    subprocess.run(['python',str(ROOT/'tools/art/kingdom_environment_textures.py')],check=True)
    # The untouched 2M-triangle imports remain in imports.blend on D:. This authored
    # file needs only optimised meshes and avoids retaining hidden source geometry.
    for source_collection in list(bpy.data.collections):
        if not source_collection.name.startswith('SOURCE_'):continue
        for obj in list(source_collection.objects):bpy.data.objects.remove(obj,do_unlink=True)
        bpy.data.collections.remove(source_collection)
    # Apply authoring modifiers, consolidate architecture by semantic material;
    # scanned props/trees keep mesh instancing and all source UVs.
    bpy.ops.object.select_all(action='DESELECT')
    for index,obj in enumerate(list(AUTHORED)):
        if obj.type!='MESH':continue
        bpy.context.view_layer.objects.active=obj
        for mod in list(obj.modifiers):
            bpy.ops.object.modifier_apply(modifier=mod.name)
        if index%200==0:print('APPLY authoring modifiers '+str(index)+'/'+str(len(AUTHORED)),flush=True)
    groups={}
    for obj in AUTHORED:
        if obj.data.name.startswith('CC0_'):continue
        names=tuple(m.name for m in obj.data.materials)
        groups.setdefault(names,[]).append(obj)
    for names,objects in groups.items():
        bpy.ops.object.select_all(action='DESELECT')
        for obj in objects:obj.select_set(True)
        bpy.context.view_layer.objects.active=objects[0]
        if len(objects)>1:bpy.ops.object.join()
        obj=bpy.context.view_layer.objects.active
        obj.name='Courtyard_'+names[0].replace('ENV_','')
    authored=[obj for obj in collection.objects if obj.type=='MESH']
    bpy.ops.object.select_all(action='DESELECT')
    for obj in authored:obj.select_set(True)
    bpy.context.view_layer.objects.active=authored[0]
    bpy.ops.export_scene.fbx(filepath=str(OUTPUT/'Environment.fbx'), use_selection=True,
        object_types={'MESH'}, apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
        axis_forward='-Z',axis_up='Y', use_space_transform=True, bake_space_transform=False,
        use_mesh_modifiers=True, mesh_smooth_type='FACE', use_tspace=True,
        add_leaf_bones=False, bake_anim=False, path_mode='STRIP', embed_textures=False)
    texture_bytes=sum(p.stat().st_size for p in TEXTURES.iterdir() if p.is_file() and p.suffix!='.meta')
    if texture_bytes>55*1024*1024:raise RuntimeError('Texture transfer budget exceeded: '+str(texture_bytes))
    STATS.update(meshObjects=len(authored), uniqueMeshes=len(set(o.data for o in authored)),
                 evaluatedTriangles=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in authored),
                 textureBytes=texture_bytes, fbxBytes=(OUTPUT/'Environment.fbx').stat().st_size)
    manifest=dict(materials=MANIFEST,
        sources=[{k:data.get(k,'') for k in ('id','url','license','licenseUrl','author')} for data in ASSETS.values()],
        coordinateSystem={'authoring':'Blender right-handed Z up, depth +Y',
            'export':'FBX forward -Z, up Y, metres, scale 1; standard Blender FBX axis transform',
            'intendedUnity':'Facade faces -Z. Ground is Y=0. Building centre X=0 Z=6.',
            'unityCharacterPositions':[[-2.2,0,-3],[0,0,-3],[2.4,0,-3]],
            'expectedUnityPlacement':{'position':[0,0,0],'rotationEuler':[0,180,0],'scale':[1,1,1]}},
        authoring={'script':'tools/art/kingdom_environment.py','blend':str(CACHE/'kingdom_environment.blend'),
                   'nativePreview':str(CACHE/'kingdom-courtyard-preview.png'),
                   'qualityStatus':'Native environment asset. Unity/player appearance and performance require separate validation.'},
        statistics=STATS)
    (OUTPUT/'material-manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
    print('ENVIRONMENT_EXPORTED '+json.dumps(STATS),flush=True)


def preview():
    scene.render.engine='CYCLES'
    scene.cycles.samples=24
    scene.cycles.use_denoising=True
    scene.cycles.device='CPU'
    scene.render.resolution_x=1680;scene.render.resolution_y=1100;scene.render.resolution_percentage=100
    scene.render.image_settings.file_format='PNG'
    scene.world.use_nodes=True
    background=scene.world.node_tree.nodes.get('Background')
    background.inputs['Color'].default_value=(.48,.61,.77,1)
    background.inputs['Strength'].default_value=.32
    bpy.ops.object.light_add(type='SUN',location=(-8,-10,15))
    sun=bpy.context.object;sun.name='PREVIEW warm afternoon sun';sun.data.energy=2.4
    sun.data.angle=math.radians(12);sun.data.color=(1,.84,.66)
    sun.rotation_euler=(math.radians(29),math.radians(-25),math.radians(-35))
    bpy.ops.object.light_add(type='AREA',location=(3,-10,10))
    fill=bpy.context.object;fill.data.energy=1000;fill.data.shape='DISK';fill.data.size=12
    fill.rotation_euler=(Vector((0,3,2))-fill.location).to_track_quat('-Z','Y').to_euler()
    bpy.ops.object.camera_add(location=(13,-19,14))
    camera=bpy.context.object;camera.name='PREVIEW courtyard camera'
    camera.rotation_euler=(Vector((0,2,2.2))-camera.location).to_track_quat('-Z','Y').to_euler()
    camera.data.type='ORTHO';camera.data.ortho_scale=28
    camera.data.lens=50;camera.data.clip_end=200;scene.camera=camera
    scene.view_settings.view_transform='AgX'
    scene.view_settings.look='AgX - Medium High Contrast'
    scene.view_settings.exposure=.35
    bpy.ops.wm.save_as_mainfile(filepath=str(CACHE/'kingdom_environment.blend'))
    scene.render.filepath=str(CACHE/'kingdom-courtyard-preview.png')
    bpy.ops.render.render(write_still=True)
    print('NATIVE_PREVIEW_READY '+scene.render.filepath,flush=True)


print('BUILD premium courtyard',flush=True)
build_ground()
build_gatehouse()
print('BUILD real scanned foliage and props',flush=True)
build_foliage_props()
finish_export()
preview()
