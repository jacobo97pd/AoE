"""Blender side of the clip retargeting in meshy_unit_finish.py: copy the clips of one Meshy rig onto another. Free.

Both rigs come out of Meshy's auto-rigger, so they share bone names and hierarchy. They do not share rest poses: every
bone's rest rotation points along that rig's own limb, a different figure is a different size, and a clip's rotations
mean "turn this much from my rest", so copying them bone by bone would bend one rig's arms by the other's limb angles.
The motion is therefore copied the way a retargeter does it, in armature space:

    delta(b)                = pose_rotation_source(b) @ rest_rotation_source(b)^-1   (how far the bone turned, in world axes)
    pose_rotation_target(b) = delta(b) @ rest_rotation_target(b)                      (keeps the twist)

and then the arms are swung the shortest way until each segment points where the source's segment points. Rest poses
differ there for real (an arm that hangs at its side in the source's pose would only be 36 degrees lower than its T-pose
rest on a target whose rest arm is that much higher). The spine, hips, legs and head are not aimed: Meshy's fitter places
those joints wherever suits the figure's body (a thick dwarf's spine joints sit a hand's breadth off the midline, one
behind and one in front of it), which is not a difference of pose, and aiming such a link along the source's straight
one would stretch the figure to a giant. A bone with no child (a hand) takes its parent's swing. Each bone's local
rotation on the target is then solved from its parent's.

This file is read as text by meshy_unit_finish.py and run inside its Blender session, where it defines
``retarget_clip_file``; it also runs on its own as a self-test:

    blender -b --factory-startup -P tools/art/meshy_retarget_blender.py -- --self-test <rig.glb> <clip.glb> [<target rig.glb>]
"""
import math
import sys

import bpy
from mathutils import Matrix


def action_curves(action):
    if len(action.slots) and len(action.layers) and len(action.layers[0].strips):
        return action.layers[0].strips[0].channelbag(action.slots[0]).fcurves
    return action.fcurves


def bone_order(armature):
    """Bone names, every parent before its children."""
    order = []

    def visit(bone):
        order.append(bone.name)
        for child in bone.children:
            visit(child)

    for bone in armature.data.bones:
        if bone.parent is None:
            visit(bone)
    return order


# The bones whose segments are aimed at the source's (see above); each points at its only child.
AIMED = ('LeftShoulder', 'LeftArm', 'LeftForeArm', 'RightShoulder', 'RightArm', 'RightForeArm')


def retarget_actions(src, dst, actions):
    """One new action on `dst` for each of `actions` (played by `src`); returns {source action name: new action}."""
    scene = bpy.context.scene
    names = bone_order(dst)
    missing = [n for n in names if n not in src.data.bones]
    if missing:
        raise RuntimeError('the source rig has no bones %s' % missing)
    roots = [n for n in names if dst.data.bones[n].parent is None]
    if len(roots) != 1:
        raise RuntimeError('expected one root bone, found %s' % roots)
    root = roots[0]
    rest_s = {n: src.data.bones[n].matrix_local.copy() for n in names}
    rest_t = {n: dst.data.bones[n].matrix_local.copy() for n in names}
    parent = {n: (dst.data.bones[n].parent.name if dst.data.bones[n].parent else None) for n in names}
    offset = {n: rest_t[parent[n]].inverted() @ rest_t[n] for n in names if parent[n]}
    ratio = rest_t[root].translation.z / rest_s[root].translation.z
    child = {n: dst.data.bones[n].children[0].name for n in AIMED}
    # Where the child sits in its parent's own frame, on the target: the vector that the parent's rotation carries.
    along = {n: rest_t[n].to_3x3().inverted() @ (rest_t[child[n]].translation - rest_t[n].translation) for n in AIMED}
    src.animation_data_create()
    dst.animation_data_create()
    made = {}
    for action in actions:
        frames = sorted({round(key.co.x, 4) for curve in action_curves(action) for key in curve.keyframe_points})
        src.animation_data.action = action
        if len(action.slots):
            src.animation_data.action_slot = action.slots[0]
        target = bpy.data.actions.new('retargeted_' + action.name)
        dst.animation_data.action = target
        last = {}
        for frame in frames:
            whole = math.floor(frame)
            scene.frame_set(whole, subframe=frame - whole)
            pose = {n: src.pose.bones[n].matrix.copy() for n in names}
            placed, swing = {}, {}
            for n in names:
                rotation = (pose[n].to_3x3() @ rest_s[n].to_3x3().inverted()) @ rest_t[n].to_3x3()
                if n in AIMED:
                    aimed = rotation @ along[n]
                    wanted = pose[child[n]].translation - pose[n].translation
                    swing[n] = aimed.rotation_difference(wanted).to_matrix()
                elif parent[n] in AIMED and not dst.data.bones[n].children:
                    swing[n] = swing[parent[n]]
                else:
                    swing[n] = Matrix.Identity(3)
                rotation = swing[n] @ rotation
                if parent[n] is None:
                    travel = (pose[n].translation - rest_s[n].translation) * ratio
                    world = Matrix.Translation(rest_t[n].translation + travel) @ rotation.to_4x4()
                    basis = rest_t[n].inverted() @ world
                else:
                    base = placed[parent[n]] @ offset[n]
                    basis = (base.to_3x3().inverted() @ rotation).to_4x4()
                    world = base @ basis
                placed[n] = world
                quaternion = basis.to_quaternion()
                if n in last:
                    quaternion.make_compatible(last[n])
                last[n] = quaternion
                bone = dst.pose.bones[n]
                bone.rotation_mode = 'QUATERNION'
                bone.rotation_quaternion = quaternion
                bone.keyframe_insert('rotation_quaternion', frame=frame, group=n)
                if parent[n] is None:
                    bone.location = basis.translation
                    bone.keyframe_insert('location', frame=frame, group=n)
        # Samples 0.8 of a frame apart: straight lines between them, as the source has them, not Bezier overshoot.
        for curve in action_curves(target):
            for key in curve.keyframe_points:
                key.interpolation = 'LINEAR'
        made[action.name] = target
    return made


def retarget_clip_file(path, dst):
    """Import the clip file `path` (a Meshy rig playing its clips), retarget every clip onto `dst`, and drop the import."""
    objects_before, actions_before = set(bpy.data.objects), set(bpy.data.actions)
    bpy.ops.import_scene.gltf(filepath=path)
    imported = [o for o in bpy.data.objects if o not in objects_before]
    sources = [a for a in bpy.data.actions if a not in actions_before]
    src = next(o for o in imported if o.type == 'ARMATURE')
    made = retarget_actions(src, dst, sources)
    for o in imported:
        bpy.data.objects.remove(o, do_unlink=True)
    for action in sources:
        bpy.data.actions.remove(action)
    for name, action in made.items():
        action.name = name
    return sorted(made)


def _self_test():
    argv = [a for a in sys.argv[sys.argv.index('--') + 1:] if a != '--self-test']
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=argv[0])
    first = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
    for action in list(bpy.data.actions):
        bpy.data.actions.remove(action)
    bpy.ops.import_scene.gltf(filepath=argv[1])
    clip_rig = next(o for o in bpy.data.objects if o.type == 'ARMATURE' and o is not first)
    actions = list(bpy.data.actions)
    # By default the clips go onto the rig they came from (a copy of it): every bone must end up where the clip put it.
    dst = first
    if len(argv) > 2:
        bpy.ops.import_scene.gltf(filepath=argv[2])
        dst = next(o for o in bpy.data.objects if o.type == 'ARMATURE' and o not in (first, clip_rig))
    made = retarget_actions(clip_rig, dst, actions)
    scene = bpy.context.scene
    worst = 0.0
    for name, new in made.items():
        old = next(a for a in actions if a.name == name)
        frames = sorted({round(k.co.x, 4) for c in action_curves(old) for k in c.keyframe_points})
        for frame in frames[::max(1, len(frames) // 8)]:
            clip_rig.animation_data.action = old
            if len(old.slots):
                clip_rig.animation_data.action_slot = old.slots[0]
            dst.animation_data.action = new
            if len(new.slots):
                dst.animation_data.action_slot = new.slots[0]
            scene.frame_set(math.floor(frame), subframe=frame - math.floor(frame))
            if len(argv) == 2:
                for bone in dst.pose.bones:
                    other = clip_rig.pose.bones[bone.name].matrix
                    worst = max(worst, max(abs(a - b) for ra, rb in zip(bone.matrix, other) for a, b in zip(ra, rb)))
        print('RETARGET_ACTION', name, 'keys', len(frames), 'slots', [s.identifier for s in new.slots], 'curves', len(action_curves(new)))
    print('RETARGET_SELF_TEST worst matrix difference %.5f (armature units, cm)' % worst if len(argv) == 2 else 'RETARGET_CROSS_TEST done')


if __name__ == '__main__' and '--self-test' in sys.argv:
    _self_test()
