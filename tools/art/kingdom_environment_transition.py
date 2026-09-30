"""Blend the forecourt into a worn woodland approach; stage all outputs on D:."""
import bpy,json,math,random,subprocess,sys
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
CACHE=Path('D:/CodexTooling/kingdom-premium/environment')
OUTPUT=ROOT/'Assets/Game/KingdomPremium/Environment'
STAGE=CACHE/'staged';TEXTURES=STAGE/'Textures';TEXTURES.mkdir(exist_ok=True)
arguments=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
fresh='--from-base' in arguments
bpy.ops.wm.open_mainfile(filepath=str(CACHE/('kingdom_environment.blend' if fresh else 'kingdom_environment_final.blend')))
collection=bpy.data.collections['KingdomPremiumEnvironment']
manifest=json.loads(((OUTPUT if fresh else STAGE)/'material-manifest.json').read_text(encoding='utf-8'))
assets=json.loads((CACHE/'download-index.json').read_text(encoding='utf-8'))
rng=random.Random(7312)

mat=bpy.data.materials.new('ENV_WornSoilPath');mat.use_nodes=True
bsdf=mat.node_tree.nodes.get('Principled BSDF');bsdf.inputs['Roughness'].default_value=.9
record={'name':mat.name,'baseColorTexture':'','normalTexture':'','roughnessTexture':'',
        'metallicTexture':'','opacityTexture':'','color':[.67,.60,.46,1],'colorSpace':'linear',
        'metallic':0,'smoothness':.1,'tiling':[1,1],'cutout':False,'doubleSided':False}
for key,semantic in [('Diffuse','baseColorTexture'),('nor_gl','normalTexture'),('Rough','roughnessTexture')]:
    source=Path(assets['brown_mud_leaves_01']['maps'][key])
    target=TEXTURES/('courtyard_trail_'+key.lower()+'_1k.jpg')
    subprocess.run(['python','-c',
        'from PIL import Image,ImageFile; import sys; ImageFile.MAXBLOCK=16*1024*1024; image=Image.open(sys.argv[1]).convert("RGB"); image.resize((1024,1024),Image.Resampling.LANCZOS).save(sys.argv[2],quality=94,subsampling=0,optimize=False)',
        str(source),str(target)],check=True)
    record[semantic]='Textures/'+target.name
    node=mat.node_tree.nodes.new('ShaderNodeTexImage');node.image=bpy.data.images.load(str(target))
    if key!='Diffuse':node.image.colorspace_settings.name='Non-Color'
    if key=='Diffuse':
        tint=mat.node_tree.nodes.new('ShaderNodeMixRGB');tint.blend_type='MULTIPLY'
        tint.inputs[0].default_value=1;tint.inputs[2].default_value=record['color']
        mat.node_tree.links.new(node.outputs['Color'],tint.inputs[1]);mat.node_tree.links.new(tint.outputs[0],bsdf.inputs['Base Color'])
    elif key=='nor_gl':
        normal=mat.node_tree.nodes.new('ShaderNodeNormalMap');normal.inputs['Strength'].default_value=.55
        mat.node_tree.links.new(node.outputs['Color'],normal.inputs['Color']);mat.node_tree.links.new(normal.outputs[0],bsdf.inputs['Normal'])
    else:mat.node_tree.links.new(node.outputs['Color'],bsdf.inputs['Roughness'])
manifest['materials'].append(record)

def ground(x,y):
    border=min(1,max(0,(abs(x)-6)/8)+max(0,(abs(y-1)-7)/8))
    return -.035+border*(.08*math.sin(x*.9)+.055*math.cos(y*.8))

vertices=[];faces=[];segments=68;cross=8
for j in range(segments+1):
    t=j/segments;y=-6.72-17*t
    center=.48*math.sin(t*4.6)+.3*t
    half=.54+.77*min(1,t*5)+.10*math.sin(t*22)
    for i in range(cross+1):
        u=i/cross;x=center+(u*2-1)*half
        if i in (0,cross):x+=.10*math.sin(j*1.71)
        paving=max(0,1-(-y-7.0)/1.1)
        h=max(ground(x,y)+.016,.010*paving if paving>0 else -10)
        # A subtle cambered trail, never a raised slab.
        h+=.004*math.sin(j*.8+i*.75)
        vertices.append((x,y,h))
for j in range(segments):
    for i in range(cross):
        a=j*(cross+1)+i;faces.append((a,a+cross+1,a+cross+2,a+1))
mesh=bpy.data.meshes.new('Worn forecourt approach');mesh.from_pydata(vertices,[],faces);mesh.update()
uv=mesh.uv_layers.new(name='UVMap')
for p in mesh.polygons:
    p.use_smooth=True
    for loop in p.loop_indices:
        v=mesh.vertices[mesh.loops[loop].vertex_index].co
        uv.data[loop].uv=(v.x/1.7,v.y/1.7)
mesh.materials.append(mat)
obj=bpy.data.objects.new('Worn approach from gatehouse to woodland',mesh);collection.objects.link(obj)

# Break the centre of the formerly continuous paving kerb where the trail enters.
trim=bpy.data.objects.get('Courtyard_CutLimestone')
if trim:
    import bmesh
    bm=bmesh.new();bm.from_mesh(trim.data)
    doomed=[]
    for v in bm.verts:
        p=trim.matrix_world@v.co
        if abs(p.x)<1.40 and -8.2<p.y<-7.4 and p.z<.14:doomed.append(v)
    bmesh.ops.delete(bm,geom=doomed,context='VERTS');bm.to_mesh(trim.data);bm.free()

grass=[o for o in collection.objects if 'meadow tuft' in o.name or 'Dense green paving margin' in o.name]
rocks=[o for o in collection.objects if 'mossy garden boulder' in o.name]
for side in (-1,1):
    for i in range(58):
        t=rng.uniform(0,.98);y=-7.4-16*t
        x=.48*math.sin(t*4.6)+.3*t+side*rng.uniform(1.1,1.55)
        source=rng.choice(grass);obj=source.copy();obj.data=source.data;collection.objects.link(obj)
        obj.name='Grass softens irregular trail boundary';obj.location=(x,y,ground(x,y)-.014)
        obj.scale*=rng.uniform(.72,1.05);obj.rotation_euler.z=rng.uniform(0,6.28)
    for i in range(34):
        x=side*rng.uniform(5.55,6.43);y=rng.uniform(-7.8,2)
        source=rng.choice(grass);obj=source.copy();obj.data=source.data;collection.objects.link(obj)
        obj.name='Sparse grass through worn paving edge';obj.location=(x,y,-.012)
        obj.scale*=rng.uniform(.35,.65);obj.rotation_euler.z=rng.uniform(0,6.28)
    for i in range(18):
        source=rng.choice(rocks);obj=source.copy();obj.data=source.data;collection.objects.link(obj)
        obj.name='Loose scanned stones along paving transition'
        x=side*rng.uniform(5.5,6.5);y=rng.uniform(-8.25,1.6)
        obj.location=(x,y,-.045);obj.scale=(rng.uniform(.035,.10),)*3;obj.rotation_euler.z=rng.uniform(0,6.28)
    for i in range(9):
        t=rng.random();y=-8.5-12*t;x=.48*math.sin(t*4.6)+side*rng.uniform(.95,1.65)
        source=rng.choice(rocks);obj=source.copy();obj.data=source.data;collection.objects.link(obj)
        obj.name='Loose woodland approach pebble';obj.location=(x,y,ground(x,y)-.035)
        obj.scale=(rng.uniform(.035,.09),)*3;obj.rotation_euler.z=rng.uniform(0,6.28)

objects=[o for o in collection.objects if o.type=='MESH']
bpy.ops.object.select_all(action='DESELECT')
for obj in objects:obj.select_set(True)
bpy.context.view_layer.objects.active=objects[0]
bpy.ops.export_scene.fbx(filepath=str(STAGE/'Environment.fbx'),use_selection=True,object_types={'MESH'},
    apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',
    use_space_transform=True,bake_space_transform=False,use_mesh_modifiers=True,mesh_smooth_type='FACE',
    use_tspace=True,add_leaf_bones=False,bake_anim=False,path_mode='STRIP',embed_textures=False)
texture_sizes={p.name:p.stat().st_size for p in (OUTPUT/'Textures').iterdir() if p.is_file() and p.suffix!='.meta'}
texture_sizes.update({p.name:p.stat().st_size for p in TEXTURES.glob('*.jpg')})
texture_total=sum(texture_sizes.values())
assert texture_total<55*1024*1024,'Texture budget exceeded'
manifest['statistics'].update(meshObjects=len(objects),uniqueMeshes=len({o.data for o in objects}),
    evaluatedTriangles=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in objects),
    textureBytes=texture_total,fbxBytes=(STAGE/'Environment.fbx').stat().st_size)
manifest['coordinateSystem']['expectedUnityPlacement']['rotationEuler']=[0,180,0]
manifest['coordinateSystem']['unityImportConfirmed']='Root Unity bake confirmed depth Z inverted; environment root yaw 180 restores the intended courtyard placement.'
manifest['authoring']['nativePreview']=str(CACHE/'kingdom-courtyard-final.png')
manifest['authoring']['blend']=str(CACHE/'kingdom_environment_final.blend')
manifest['authoring']['finishingScript']='tools/art/kingdom_environment_transition.py --from-base'
(STAGE/'material-manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
bpy.ops.wm.save_as_mainfile(filepath=str(CACHE/'kingdom_environment_final.blend'))
bpy.context.scene.render.filepath=str(CACHE/'kingdom-courtyard-final.png')
bpy.ops.render.render(write_still=True)
print('APPROACH_TRANSITION_READY '+json.dumps(manifest['statistics']),flush=True)
