"""Inspect and prepare the additional pirate artwork supplied by the user.

No source archive is overwritten. Large working files remain on D:.
Run Blender --background --python tools/art/prepare_pirate_crew.py -- --inspect.
"""
import bpy
import hashlib
import json
import sys
import time
import zipfile
from pathlib import Path

import numpy as np
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
from prepare_corsair_mesh import connected_islands

WORK = Path('D:/CodexTooling/pirate-crew')
ZIP = Path('C:/Users/jacob/Downloads/Meshy_AI_Crimson_Tide_Buccanee_0911083354_texture_fbx.zip')


def extract_source():
    root = (WORK / 'source').resolve()
    root.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(ZIP) as archive:
        for entry in archive.infolist():
            target = (root / entry.filename).resolve()
            if not target.is_relative_to(root):
                raise ValueError('Archive path escapes the intended source directory.')
            if entry.is_dir():
                target.mkdir(parents=True, exist_ok=True)
            else:
                if target.suffix.lower() not in {'.fbx', '.png', '.jpg', '.jpeg'}:
                    raise ValueError('Unexpected archive entry: ' + entry.filename)
                target.parent.mkdir(parents=True, exist_ok=True)
                if not target.exists() or target.stat().st_size != entry.file_size:
                    target.write_bytes(archive.read(entry))
    return next(root.rglob('*.fbx'))


def inspect():
    WORK.mkdir(parents=True, exist_ok=True)
    source = extract_source()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(source))
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    report = {'sourceZip': str(ZIP), 'sourceSha256': hashlib.sha256(ZIP.read_bytes()).hexdigest(),
              'objects': [], 'actions': [a.name for a in bpy.data.actions], 'images': []}
    for obj in bpy.context.scene.objects:
        row = {'name': obj.name, 'type': obj.type}
        if obj.type == 'MESH':
            coords, labels, islands = connected_islands(obj)
            np.savez_compressed(WORK / (obj.name + '-topology.npz'), labels=labels)
            row.update(vertices=len(obj.data.vertices), triangles=sum(len(p.vertices)-2 for p in obj.data.polygons),
                       materials=[m.name for m in obj.data.materials],
                       min=coords.min(axis=0).tolist(), max=coords.max(axis=0).tolist(), islands=islands)
        report['objects'].append(row)
        print(json.dumps(row), flush=True)
    for image in bpy.data.images:
        report['images'].append({'name': image.name, 'size': list(image.size), 'path': image.filepath})
    (WORK / 'inspection.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    bpy.ops.wm.save_as_mainfile(filepath=str(WORK / 'imported.blend'), compress=True)
    print('CREW_INSPECTION_SAVED', flush=True)
    render_views()


def render_views():
    scene = bpy.context.scene
    meshes = [o for o in scene.objects if o.type == 'MESH']
    world_points = [o.matrix_world @ Vector(c) for o in meshes for c in o.bound_box]
    low = Vector(tuple(min(p[i] for p in world_points) for i in range(3)))
    high = Vector(tuple(max(p[i] for p in world_points) for i in range(3)))
    center = (low + high) * .5
    span = high - low
    scene.render.engine = 'BLENDER_EEVEE_NEXT'
    scene.render.resolution_x = 1400
    scene.render.resolution_y = 1200
    scene.render.resolution_percentage = 100
    scene.view_settings.view_transform = 'AgX'
    world = bpy.data.worlds.new('Pirate source inspection')
    world.use_nodes = True
    world.node_tree.nodes['Background'].inputs['Color'].default_value = (.28,.32,.36,1)
    world.node_tree.nodes['Background'].inputs['Strength'].default_value = .45
    scene.world = world
    size = max(span)
    for name,offset,energy in [('Key',(-1.5,-2,2.5),230),('Fill',(1.7,-1,1.5),130),('Rim',(.5,2,2),240)]:
        data = bpy.data.lights.new(name, 'AREA')
        obj = bpy.data.objects.new(name, data)
        scene.collection.objects.link(obj)
        obj.location = center + Vector(offset) * size
        obj.rotation_euler = (center-obj.location).to_track_quat('-Z','Y').to_euler()
        data.energy = energy * size * size
        data.size = size * 1.3
    data = bpy.data.cameras.new('Inspection camera')
    data.type = 'ORTHO'
    cam = bpy.data.objects.new('Inspection camera', data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    for label,direction in [('front',(0,-1,.035)),('side',(1,0,.035)),('back',(0,1,.035))]:
        cam.location = center + Vector(direction) * size * 4
        cam.rotation_euler = (center-cam.location).to_track_quat('-Z','Y').to_euler()
        width = span.y if label == 'side' else span.x
        data.ortho_scale = max(span.z*(1400/1200),width)*1.14
        scene.render.filepath = str(WORK / (label + '.png'))
        bpy.ops.render.render(write_still=True)
    print('CREW_INSPECTION_OK', flush=True)


CREW = {
    'BoardingRaider': {'counts': [466680], 'origin': (-.565, -.005), 'title': 'Saqueador de abordaje'},
    'GunpowderCorsair': {'counts': [461896,1961], 'origin': (.065, -.125), 'title': 'Corsario de pólvora'},
    'TreasureSeeker': {'counts': [507590,10,6], 'origin': (.685, -.11), 'title': 'Buscadora de tesoros'},
}


def separate_source():
    bpy.ops.wm.open_mainfile(filepath=str(WORK / 'imported.blend'))
    obj = next(o for o in bpy.context.scene.objects if o.type == 'MESH')
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.separate(type='LOOSE')
    bpy.ops.object.mode_set(mode='OBJECT')
    return [o for o in bpy.context.scene.objects if o.type == 'MESH']


def prepare():
    manifest = {'heightMetres':2.35, 'faceAxis':'Blender -Y', 'sourceSha256':json.loads((WORK/'inspection.json').read_text())['sourceSha256'], 'characters':[]}
    # The unrelated left red sleeve fragment and floor props remain separately recoverable.
    pieces = separate_source()
    decor_counts = {49471,168002,330303,3426}
    decor = {o for o in pieces if len(o.data.vertices) in decor_counts}
    assert len(decor) == 4
    for i,o in enumerate(sorted(decor,key=lambda o:len(o.data.vertices))):o.name='Crew_RemovedDisplayProp_'+str(i)
    bpy.data.libraries.write(str(WORK/'decor.blend'),decor,compress=True)
    for name, spec in CREW.items():
        pieces = separate_source()
        selected = [o for o in pieces if len(o.data.vertices) in spec['counts']]
        assert len(selected) == len(spec['counts']), 'The reviewed topology counts changed.'
        for obj in list(bpy.context.scene.objects):
            if obj not in selected:bpy.data.objects.remove(obj,do_unlink=True)
        bpy.ops.object.select_all(action='DESELECT')
        for obj in selected:obj.select_set(True)
        bpy.context.view_layer.objects.active = selected[0]
        if len(selected)>1:bpy.ops.object.join()
        source = bpy.context.view_layer.objects.active
        p = np.empty(len(source.data.vertices)*3,np.float32)
        source.data.vertices.foreach_get('co',p)
        p=p.reshape(-1,3)
        ground=float(p[:,2].min())
        scale=2.35/(float(p[:,2].max())-ground)
        origin=(*spec['origin'],ground)
        p=(p-np.array(origin))*scale
        source.data.vertices.foreach_set('co',p.astype(np.float32).ravel())
        source.data.update()
        count=sum(len(poly.vertices)-2 for poly in source.data.polygons)
        record={'name':name,'title':spec['title'],'sourceVertices':len(source.data.vertices),'sourceTriangles':count,
                'sourceOrigin':origin,'scale':scale,'normalizedBounds':[p.min(axis=0).tolist(),p.max(axis=0).tolist()],'lods':[]}
        for level,target in enumerate([140000,45000,12000]):
            started=time.time()
            lod=source.copy();lod.data=source.data.copy()
            bpy.context.collection.objects.link(lod)
            lod.name=f'{name}_LOD{level}';lod.data.name=lod.name+'_Mesh'
            bpy.ops.object.select_all(action='DESELECT');lod.select_set(True);bpy.context.view_layer.objects.active=lod
            modifier=lod.modifiers.new('Source-preserving reduction','DECIMATE')
            modifier.decimate_type='COLLAPSE';modifier.ratio=target/count;modifier.use_collapse_triangulate=True
            bpy.ops.object.modifier_apply(modifier=modifier.name)
            for polygon in lod.data.polygons:polygon.use_smooth=True
            lod.hide_render=level>0;lod.hide_set(level>0)
            row={'name':lod.name,'vertices':len(lod.data.vertices),'triangles':sum(len(poly.vertices)-2 for poly in lod.data.polygons),
                 'uvLayers':[layer.name for layer in lod.data.uv_layers],'materials':[m.name for m in lod.data.materials]}
            record['lods'].append(row)
            print(name,json.dumps(row),'seconds',round(time.time()-started,2),flush=True)
        bpy.data.objects.remove(source,do_unlink=True)
        bpy.data.orphans_purge(do_recursive=True)
        bpy.ops.wm.save_as_mainfile(filepath=str(WORK/(name+'-prepared.blend')),compress=True)
        manifest['characters'].append(record)
        (WORK/'preparation-manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
        print('CREW_CHARACTER_READY '+name,flush=True)
    print('CREW_PREPARATION_OK',flush=True)


def coordinate_reviews():
    import math
    for name in CREW:
        if '--character' in sys.argv and name != sys.argv[sys.argv.index('--character')+1]:
            continue
        bpy.ops.wm.open_mainfile(filepath=str(WORK/(name+'-prepared.blend')))
        for obj in bpy.context.scene.objects:
            obj.hide_render=not obj.name.endswith('_LOD0')
            obj.hide_set(not obj.name.endswith('_LOD0'))
        scene=bpy.context.scene
        scene.render.engine='BLENDER_EEVEE_NEXT';scene.render.resolution_x=1400;scene.render.resolution_y=1600;scene.render.resolution_percentage=100
        world=bpy.data.worlds.new('Grid background');world.use_nodes=True;world.node_tree.nodes['Background'].inputs['Color'].default_value=(.3,.34,.38,1);world.node_tree.nodes['Background'].inputs['Strength'].default_value=.45;scene.world=world
        for title,loc,power in [('Key',(-3,-4,5),700),('Fill',(3,-2,4),400),('Rim',(1,4,5),700)]:
            data=bpy.data.lights.new(title,'AREA');obj=bpy.data.objects.new(title,data);scene.collection.objects.link(obj);obj.location=loc;obj.rotation_euler=(Vector((0,0,1.2))-obj.location).to_track_quat('-Z','Y').to_euler();data.energy=power;data.size=4
        data=bpy.data.cameras.new('Coordinate camera');data.type='ORTHO';data.ortho_scale=2.9
        cam=bpy.data.objects.new('Coordinate camera',data);scene.collection.objects.link(cam);scene.camera=cam
        mat=bpy.data.materials.new('Grid ink');mat.use_nodes=True
        bsdf=mat.node_tree.nodes.get('Principled BSDF');bsdf.inputs['Base Color'].default_value=(.15,.18,.2,1);bsdf.inputs['Emission Color'].default_value=(.2,.23,.25,1);bsdf.inputs['Emission Strength'].default_value=1
        def line(points):
            c=bpy.data.curves.new('Grid','CURVE');c.dimensions='3D';c.bevel_depth=.001;c.bevel_resolution=0
            s=c.splines.new('POLY');s.points.add(len(points)-1)
            for p,co in zip(s.points,points):p.co=(*co,1)
            o=bpy.data.objects.new('Grid',c);scene.collection.objects.link(o);o.data.materials.append(mat);return o
        def label(body,loc,side):
            c=bpy.data.curves.new('Label','FONT');c.body=body;c.size=.04
            o=bpy.data.objects.new('Label',c);scene.collection.objects.link(o);o.location=loc;o.rotation_euler=(math.pi/2,0,math.pi/2 if side else 0);o.data.materials.append(mat);return o
        for side in [False,True]:
            objects=[]
            for z in np.arange(0,2.76,.25):
                a,b=((-1,-1.15,float(z)),(-1,1.15,float(z))) if side else ((-1.15,.95,float(z)),(1.15,.95,float(z)))
                objects.append(line([a,b]));objects.append(label(f'z {z:.2f}',(-.99,-1.20,float(z)+.01) if side else (-1.20,.94,float(z)+.01),side))
            for a in np.arange(-1,1.01,.25):
                ends=[(-1,float(a),-.05),(-1,float(a),2.75)] if side else [(float(a),.95,-.05),(float(a),.95,2.75)]
                objects.append(line(ends));objects.append(label(f'{a:+.2f}',(-.99,float(a)-.03,-.13) if side else (float(a)-.03,.94,-.13),side))
            cam.location=(7,0,1.25) if side else (0,-7,1.25);cam.rotation_euler=(Vector((0,0,1.25))-cam.location).to_track_quat('-Z','Y').to_euler()
            scene.render.filepath=str(WORK/(name+('-side-grid.png' if side else '-front-grid.png')))
            bpy.ops.render.render(write_still=True)
            for obj in objects:bpy.data.objects.remove(obj,do_unlink=True)


if __name__ == '__main__':
    if '--prepare' in sys.argv:prepare()
    elif '--coordinates' in sys.argv:coordinate_reviews()
    elif '--renders' in sys.argv:
        bpy.ops.wm.open_mainfile(filepath=str(WORK/'imported.blend'));render_views()
    else:inspect()
