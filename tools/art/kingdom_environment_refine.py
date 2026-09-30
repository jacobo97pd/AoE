"""Refine the saved courtyard without rebuilding its architectural modifiers."""
import bpy,json,shutil,subprocess,math,random
from pathlib import Path
from mathutils import Vector,Matrix
ROOT=Path(__file__).resolve().parents[2]
CACHE=Path('D:/CodexTooling/kingdom-premium/environment')
OUTPUT=ROOT/'Assets/Game/KingdomPremium/Environment'
bpy.ops.wm.open_mainfile(filepath=str(CACHE/'kingdom_environment.blend'))
collection=bpy.data.collections['KingdomPremiumEnvironment']
manifest=json.loads((OUTPUT/'material-manifest.json').read_text(encoding='utf-8'))
with bpy.data.libraries.load(str(CACHE/'tree-preserved-compact.blend'),link=False) as (source,target):
    target.objects=['KingdomBoundaryPreservedTree']
tree=target.objects[0]
mesh=tree.data
mesh.transform(tree.matrix_world)
minz=min(v.co.z for v in mesh.vertices)
mesh.transform(Matrix.Translation((0,0,-minz)))
for i,mat in enumerate(mesh.materials):
    name='ENV_TreeLeaf' if 'leaves' in mat.name else 'ENV_TreeBranch' if 'branches' in mat.name else 'ENV_TreeBark'
    mesh.materials[i]=bpy.data.materials[name]
mesh.name='CC0_tree_small_02_boundary_preserved'
for obj in collection.objects:
    if 'optimized living tree' in obj.name:obj.data=mesh
bpy.data.objects.remove(tree,do_unlink=True)

# Real green ground scan replaces the predominantly brown litter surface.
assets=json.loads((CACHE/'download-index.json').read_text(encoding='utf-8'))
grass=assets['leafy_grass']
mat=bpy.data.materials['ENV_WoodlandSoil']
record=next(m for m in manifest['materials'] if m['name']==mat.name)
for key,semantic in [('Diffuse','baseColorTexture'),('nor_gl','normalTexture'),('Rough','roughnessTexture')]:
    source=Path(grass['maps'][key]);target=OUTPUT/'Textures'/source.name
    # Convert before transfer to keep the live project within its disk allowance.
    subprocess.run(['python','-c',
        'from PIL import Image; import sys; Image.open(sys.argv[1]).convert("RGB").save(sys.argv[2],quality=94,subsampling=0,optimize=True)',
        str(source),str(target)],check=True)
    record[semantic]='Textures/'+source.name
    for node in mat.node_tree.nodes:
        if node.type=='TEX_IMAGE' and ('_diffuse' if key=='Diffuse' else '_nor_gl' if key=='nor_gl' else '_rough') in node.image.name:
            node.image=bpy.data.images.load(str(source),check_existing=True)
            if key!='Diffuse':node.image.colorspace_settings.name='Non-Color'
record['color']=[.84,.89,.78,1]
for node in mat.node_tree.nodes:
    if node.type=='MIX_RGB':node.inputs[2].default_value=record['color']
manifest['sources'].append({k:grass.get(k,'') for k in ('id','url','license','licenseUrl','author')})

for name,color in [('ENV_Slate',(.26,.38,.50,1)),('ENV_SlateWeathered',(.34,.46,.57,1))]:
    mat=bpy.data.materials[name]
    mat.diffuse_color=color
    next(m for m in manifest['materials'] if m['name']==name)['color']=list(color)
    for node in mat.node_tree.nodes:
        if node.type=='MIX_RGB':node.inputs[2].default_value=color
    # Each individual shingle is a connected component after material consolidation.
    # Narrow it by 4% to expose a real side seam between adjacent tiles.
    for obj in collection.objects:
        if obj.type!='MESH' or mat not in list(obj.data.materials):continue
        vertices=obj.data.vertices
        adjacency=[[] for _ in vertices]
        for edge in obj.data.edges:
            a,b=edge.vertices;adjacency[a].append(b);adjacency[b].append(a)
        seen=set()
        for start in range(len(vertices)):
            if start in seen:continue
            component=[];stack=[start];seen.add(start)
            while stack:
                current=stack.pop();component.append(current)
                for other in adjacency[current]:
                    if other not in seen:seen.add(other);stack.append(other)
            world=[obj.matrix_world@vertices[i].co for i in component]
            lo=min(v.x for v in world);hi=max(v.x for v in world)
            if .5<hi-lo<.55:
                inverse=obj.matrix_world.inverted();center=(lo+hi)/2
                for index,p in zip(component,world):
                    p.x=center+(p.x-center)*.948
                    vertices[index].co=inverse@p

rng=random.Random(20260910)
ferns=[o for o in collection.objects if 'fern edge cluster' in o.name]
for obj in ferns:obj.scale*=1.4
grass_objects=[o for o in collection.objects if 'meadow tuft' in o.name]
for side in (-1,1):
    for i in range(100):
        src=rng.choice(grass_objects)
        obj=src.copy();obj.data=src.data;collection.objects.link(obj)
        obj.name='Dense green paving margin'
        obj.location=(side*rng.uniform(6.05,7.5),rng.uniform(-8,7),-.035)
        obj.scale*=rng.uniform(1.1,1.5);obj.rotation_euler.z=rng.uniform(0,6.28)
    for i in range(16):
        src=rng.choice(ferns);obj=src.copy();obj.data=src.data;collection.objects.link(obj)
        obj.name='Lush fern garden margin'
        obj.location=(side*rng.uniform(6.55,8.1),rng.uniform(-7,5.8),-.035)
        obj.rotation_euler.z=rng.uniform(0,6.28)

# The unused litter texture maps are no longer part of the exported asset set.
for source in (OUTPUT/'Textures').glob('brown_mud_leaves_01_*.jpg'):
    if source.resolve().parent==(OUTPUT/'Textures').resolve():source.unlink()
subprocess.run(['python',str(ROOT/'tools/art/kingdom_environment_textures.py')],check=True)
for material in manifest['materials']:material['colorSpace']='linear'
objects=[o for o in collection.objects if o.type=='MESH']
bpy.ops.object.select_all(action='DESELECT')
for obj in objects:obj.select_set(True)
bpy.context.view_layer.objects.active=objects[0]
bpy.ops.export_scene.fbx(filepath=str(OUTPUT/'Environment.fbx'),use_selection=True,object_types={'MESH'},
    apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',
    use_space_transform=True,bake_space_transform=False,use_mesh_modifiers=True,mesh_smooth_type='FACE',
    use_tspace=True,add_leaf_bones=False,bake_anim=False,path_mode='STRIP',embed_textures=False)
tree_stats=json.loads((CACHE/'tree-preserved-compact.json').read_text(encoding='utf-8'))
manifest['statistics']['tree']={'sourceTriangles':2062487,'exportTrianglesPerTree':tree_stats['preservedTriangles'],
    'method':tree_stats['method'],'materials':tree_stats['materials']}
manifest['statistics'].update(meshObjects=len(objects),uniqueMeshes=len({o.data for o in objects}),
    evaluatedTriangles=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in objects),
    textureBytes=sum(p.stat().st_size for p in (OUTPUT/'Textures').iterdir() if p.is_file() and p.suffix!='.meta'),
    fbxBytes=(OUTPUT/'Environment.fbx').stat().st_size)
(OUTPUT/'material-manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
scene=bpy.context.scene
scene.render.filepath=str(CACHE/'kingdom-courtyard-preview.png')
bpy.ops.wm.save_as_mainfile(filepath=str(CACHE/'kingdom_environment.blend'))
bpy.ops.render.render(write_still=True)
print('REFINED_ENVIRONMENT '+json.dumps(manifest['statistics']),flush=True)
