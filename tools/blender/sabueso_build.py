# Barro y Bruma - the hound (art_src/sabueso/sabueso.blend): a thin hunting dog with the fur gone in patches,
# ribs showing, torn ears, its collar still on and milky eyes. Its own quadruped rig (replaces the hero rig in a
# copy of the hero file, so camera, light, palette materials and render settings are the same).
# Poses use the generic armature-space code of enemigo_pose.py; the clips are defined here (dog_clips).
import bpy, bmesh, math, random
from mathutils import Vector, Matrix

# bone: (head, tail, parent)
BONES = {
    'pelvis': ((0, 0.26, 0.56), (0, 0.06, 0.6), None),
    'spine': ((0, 0.06, 0.6), (0, -0.16, 0.64), 'pelvis'),
    'chest': ((0, -0.16, 0.64), (0, -0.3, 0.66), 'spine'),
    'neck': ((0, -0.3, 0.68), (0, -0.42, 0.8), 'chest'),
    'head': ((0, -0.42, 0.8), (0, -0.62, 0.75), 'neck'),
    'jaw': ((0, -0.47, 0.74), (0, -0.62, 0.71), 'head'),
    'tail': ((0, 0.3, 0.57), (0, 0.48, 0.55), 'pelvis'),
    'tail2': ((0, 0.48, 0.55), (0, 0.64, 0.47), 'tail'),
}
for s, x in (('L', 0.085), ('R', -0.085)):
    BONES.update({
        'fl.' + s: ((x, -0.24, 0.56), (x, -0.25, 0.32), 'chest'),
        'fl2.' + s: ((x, -0.25, 0.32), (x, -0.23, 0.06), 'fl.' + s),
        'fp.' + s: ((x, -0.23, 0.06), (x, -0.3, 0.02), 'fl2.' + s),
        'hl.' + s: ((x, 0.24, 0.54), (x, 0.32, 0.32), 'pelvis'),
        'hl2.' + s: ((x, 0.32, 0.32), (x, 0.36, 0.12), 'hl.' + s),
        'hl3.' + s: ((x, 0.36, 0.12), (x, 0.34, 0.03), 'hl2.' + s),
        'hp.' + s: ((x, 0.34, 0.03), (x, 0.27, 0.02), 'hl3.' + s),
    })


def mats():
    pm = pal_mat
    pm('S_Pelaje', ['2a2420', '3d332b', '594a3c', '7a6650'])
    pm('S_PielPelada', ['3d332b', '7a6650', '9c8a70', 'b08a6a'])
    pm('S_Costilla', ['141210', '2a2420', '594a3c', '594a3c'])
    pm('S_Collar', ['141210', '2a2420', '3d332b', '594a3c'])
    pm('S_Hierro', ['141210', '3f3e3b', '6e6c66', '9a968c'])
    pm('S_Ojo', ['6e6c66', '9a968c', 'c4bfb2', 'e6e2d4'])
    pm('S_Boca', ['141210', '3e1a18', '6b2a24', '6b2a24'])
    pm('S_Diente', ['594a3c', '9a968c', 'c4bfb2', 'e6e2d4'])


def clear_hero():
    for o in list(bpy.data.objects):
        if o.type in ('MESH', 'ARMATURE', 'EMPTY'):
            bpy.data.objects.remove(o, do_unlink=True)


def build_rig():
    arm = bpy.data.armatures.new('Rig')
    rig = bpy.data.objects.new('Rig', arm)
    bpy.context.scene.collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    # run from a timer or the MCP there is no window context: give the operators one explicitly
    win = bpy.context.window_manager.windows[0]
    area = next((a for a in win.screen.areas if a.type == 'VIEW_3D'), win.screen.areas[0])
    ctx = dict(window=win, area=area, active_object=rig, object=rig, selected_objects=[rig],
               edit_object=rig, view_layer=bpy.context.view_layer)
    with bpy.context.temp_override(**ctx):
        bpy.ops.object.mode_set(mode='EDIT')
        _edit_bones(arm)
        bpy.ops.object.mode_set(mode='OBJECT')
    return rig


def _edit_bones(arm):
    for n, (h, t, p) in BONES.items():
        b = arm.edit_bones.new(n)
        b.head = h
        b.tail = t
        b.roll = 0
    for n, (h, t, p) in BONES.items():
        if p:
            arm.edit_bones[n].parent = arm.edit_bones[p]
            arm.edit_bones[n].use_connect = False


def build_body(rig):
    """Rigid pieces parented to the bones (the skin-modifier body bent badly under linear blend skinning).
    Torso, neck, head, tail and every leg segment are separate meshes; spheres cover the joints."""
    V = Vector
    torso = bmesh.new()
    ellipsoid(torso, (0, 0.24, 0.57), (0.1, 0.12, 0.11), 12, 8)
    to_bone(new_obj('Body', torso, 'S_Pelaje', parent_rig=True), 'pelvis')
    bm = bmesh.new()
    ellipsoid(bm, (0, 0.06, 0.6), (0.085, 0.13, 0.09), 12, 8)
    ellipsoid(bm, (0, -0.12, 0.625), (0.105, 0.13, 0.135), 12, 8)
    to_bone(new_obj('S_Lomo', bm, 'S_Pelaje', parent_rig=True), 'spine')
    bm = bmesh.new()
    ellipsoid(bm, (0, -0.26, 0.635), (0.11, 0.09, 0.135), 12, 8)
    to_bone(new_obj('S_Pecho', bm, 'S_Pelaje', parent_rig=True), 'chest')
    bm = bmesh.new()
    tube(bm, [V((0, -0.28, 0.66)), V((0, -0.36, 0.74)), V((0, -0.43, 0.8))], [0.07, 0.064, 0.058], 8)
    to_bone(new_obj('S_Cuello', bm, 'S_Pelaje', parent_rig=True), 'neck')
    bm = bmesh.new()
    ellipsoid(bm, (0, -0.46, 0.8), (0.066, 0.08, 0.068), 12, 8)
    tube(bm, [V((0, -0.5, 0.792)), V((0, -0.57, 0.775)), V((0, -0.625, 0.755))], [0.042, 0.034, 0.026], 8)
    to_bone(new_obj('S_Craneo', bm, 'S_Pelaje', parent_rig=True), 'head')
    bm = bmesh.new()
    tube(bm, [V((0, -0.48, 0.745)), V((0, -0.55, 0.73)), V((0, -0.61, 0.718))], [0.03, 0.024, 0.017], 6)
    to_bone(new_obj('S_Mandibula', bm, 'S_Pelaje', parent_rig=True), 'jaw')
    for bone, r in (('tail', (0.03, 0.022)), ('tail2', (0.02, 0.008))):
        bm = bmesh.new()
        h, t, _ = BONES[bone]
        tube(bm, [V(h), (V(h) + V(t)) / 2, V(t)], [r[0], (r[0] + r[1]) / 2, r[1]], 6)
        to_bone(new_obj('S_Cola_' + bone, bm, 'S_Pelaje', parent_rig=True), bone)
    legs = {'fl': (0.052, 0.032), 'fl2': (0.03, 0.022), 'fp': (0.024, 0.022),
            'hl': (0.068, 0.04), 'hl2': (0.04, 0.026), 'hl3': (0.024, 0.022), 'hp': (0.024, 0.022)}
    for s in ('L', 'R'):
        for k, (r0, r1) in legs.items():
            n = k + '.' + s
            h, t, _ = BONES[n]
            h, t = V(h), V(t)
            bm = bmesh.new()
            tube(bm, [h, (h + t) / 2, t], [r0, (r0 + r1) / 2, r1], 8)
            ellipsoid(bm, h, (r0 * 1.02,) * 3, 8, 6)                  # joint cap
            if k in ('fp', 'hp'):
                ellipsoid(bm, t + V((0, 0, 0.004)), (0.028, 0.034, 0.02), 8, 6)   # paw
            to_bone(new_obj('S_Pata_' + n, bm, 'S_Pelaje', parent_rig=True), n)
    return bpy.data.objects['Body']


def to_bone(ob, bone):
    b = ARM.bones[bone]
    ob.parent = RIG
    ob.parent_type = 'BONE'
    ob.parent_bone = bone
    ob.matrix_parent_inverse = Matrix.Identity(4)
    ob.matrix_basis = (b.matrix_local @ Matrix.Translation((0, b.length, 0))).inverted()


def build_details(seed=101):
    rnd = random.Random(seed)
    # bald patches of pale skin on the flank, haunch, neck and shoulder
    for bone, items in (('spine', [((0.1, -0.08, 0.62), (0.025, 0.07, 0.06)), ((-0.1, 0.0, 0.6), (0.025, 0.06, 0.05))]),
                        ('pelvis', [((0.1, 0.24, 0.54), (0.03, 0.06, 0.06))]),
                        ('neck', [((-0.06, -0.36, 0.74), (0.02, 0.045, 0.04))]),
                        ('chest', [((0.11, -0.25, 0.6), (0.02, 0.05, 0.05))])):
        bm = bmesh.new()
        for c, r in items:
            ellipsoid(bm, c, r, 8, 6)
        to_bone(new_obj('S_Calva_' + bone, bm, 'S_PielPelada', parent_rig=True), bone)
    # ribs: dark ridges on both flanks
    bm = bmesh.new()
    for s in (-1, 1):
        for k in range(4):
            y = -0.2 + 0.06 * k
            pts = [Vector((s * (0.1 + 0.005 * j), y + 0.005 * j, 0.7 - 0.05 * j)) for j in range(4)]
            tube(bm, pts, [0.008] * 4, 4, cap_tip=False)
    to_bone(new_obj('S_Costillas', bm, 'S_Costilla', parent_rig=True), 'spine')
    # torn ears
    bm = bmesh.new()
    for s in (-1, 1):
        v0 = bm.verts.new((s * 0.04, -0.44, 0.86))
        v1 = bm.verts.new((s * 0.08, -0.39, 0.86))
        v2 = bm.verts.new((s * 0.12, -0.43, 0.74 if s > 0 else 0.8))     # the right one torn short
        bm.faces.new((v0, v1, v2))
    ob = new_obj('S_Orejas', bm, 'S_Pelaje', parent_rig=True, smooth=False)
    to_bone(ob, 'head')
    ob.modifiers.new('Grosor', 'SOLIDIFY').thickness = 0.016
    # milky eyes, the mouth and a few teeth
    bm = bmesh.new()
    for s in (-1, 1):
        ellipsoid(bm, (s * 0.04, -0.5, 0.815), (0.012, 0.012, 0.01), 6, 5)
    to_bone(new_obj('S_Ojos', bm, 'S_Ojo', parent_rig=True), 'head')
    bm = bmesh.new()
    ellipsoid(bm, (0, -0.56, 0.715), (0.025, 0.06, 0.012), 8, 5)
    to_bone(new_obj('S_Lengua', bm, 'S_Boca', parent_rig=True), 'jaw')
    bm = bmesh.new()
    for x in (-0.022, 0.022):
        tube(bm, [Vector((x, -0.6, 0.735)), Vector((x, -0.6, 0.71))], [0.006, 0.002], 4)
    to_bone(new_obj('S_Dientes', bm, 'S_Diente', parent_rig=True), 'head')
    # the collar it still wears, with a rusty ring
    bm = bmesh.new()
    pts = [Vector((0.075 * math.sin(2 * math.pi * i / 16), -0.36 - 0.065 * math.cos(2 * math.pi * i / 16) * 0.6,
                   0.74 + 0.06 * math.cos(2 * math.pi * i / 16))) for i in range(17)]
    tube(bm, pts, [0.016] * 17, 5, cap_tip=False)
    to_bone(new_obj('S_Collar', bm, 'S_Collar', parent_rig=True), 'neck')
    bm = bmesh.new()
    torus(bm, 0.02, 0.006, Matrix.Translation((0, -0.42, 0.7)) @ Matrix.Rotation(math.pi / 2, 4, 'Y'), 8, 4)
    to_bone(new_obj('S_Argolla', bm, 'S_Hierro', parent_rig=True), 'neck')


def build_all():
    global RIG, ARM
    clear_hero()
    mats()
    RIG = build_rig()
    ARM = RIG.data
    build_body(RIG)
    build_details()
    bpy.context.view_layer.update()


STYLE = {'scale': 1.0, 'sx': 1.4}


# ---- dog clips (armature axes: +X tips forward-pointing bones down and swings down-pointing legs back) --------
def dog(breathe=0.0, head=0.0, neck=0.0, jaw=0.0, spine=0.0, tail=0.0, tailz=0.0, legs=None):
    b = {'spine': (spine - 1.5 * breathe, 0, 0), 'chest': (1.0 * breathe, 0, 0), 'neck': (neck, 0, 0),
         'head': (head, 0, 0), 'jaw': (jaw, 0, 0), 'tail': (tail, 0, tailz), 'tail2': (tail * 0.5, 0, tailz * 0.6)}
    b.update(legs or {})
    return b


def dog_clips():
    c = {}
    c['idle'] = [{'b': dog(breathe=br, head=12 + 3 * br, neck=10, tailz=w), 'pl': (0, 0, 0.004 * br)}
                 for br, w in ((-1, -8), (0, 0), (1, 8), (0, 0))]
    run = []
    for i in range(8):
        p = i / 8
        legs = {}
        for leg, ph, front in (('fl.L', p, True), ('hl.R', p, False), ('fl.R', p + 0.5, True), ('hl.L', p + 0.5, False)):
            a = 2 * math.pi * ph
            swing = -28 * math.cos(a)                       # forward when cos > 0
            lift = 40 * max(0.0, math.sin(a))
            s = leg[-1]
            # the poser composes each bone on top of its parent, so knees and paws get only their own bend
            if front:
                legs['fl.' + s] = (swing, 0, 0)
                legs['fl2.' + s] = (lift * 0.7, 0, 0)          # forearm folds back under the chest
                legs['fp.' + s] = (lift * 0.6, 0, 0)
            else:
                legs['hl.' + s] = (swing, 0, 0)
                legs['hl2.' + s] = (lift * 0.4, 0, 0)
                legs['hl3.' + s] = (-lift * 0.9, 0, 0)         # the hock lifts the hind paw
                legs['hp.' + s] = (lift * 0.3, 0, 0)
        bob = 0.025 * math.cos(4 * math.pi * p)
        run.append({'b': dog(head=4 - 6 * math.cos(4 * math.pi * p), neck=4, spine=3 * math.sin(4 * math.pi * p),
                             tail=-10, tailz=12 * math.sin(2 * math.pi * p), legs=legs), 'pl': (0, 0, bob)})
    c['walk'] = run
    crouch = {'fl.L': (-12, 0, 0), 'fl.R': (-12, 0, 0), 'fl2.L': (18, 0, 0), 'fl2.R': (18, 0, 0),
              'fp.L': (-6, 0, 0), 'fp.R': (-6, 0, 0),
              'hl.L': (-22, 0, 0), 'hl.R': (-22, 0, 0), 'hl2.L': (42, 0, 0), 'hl2.R': (42, 0, 0),
              'hl3.L': (-20, 0, 0), 'hl3.R': (-20, 0, 0)}
    leap = {'fl.L': (-52, 0, 0), 'fl.R': (-46, 0, 0), 'fl2.L': (12, 0, 0), 'fl2.R': (12, 0, 0),
            'fp.L': (8, 0, 0), 'fp.R': (8, 0, 0),
            'hl.L': (32, 0, 0), 'hl.R': (38, 0, 0), 'hl2.L': (8, 0, 0), 'hl2.R': (8, 0, 0),
            'hl3.L': (-4, 0, 0), 'hl3.R': (-4, 0, 0)}
    c['attack'] = [
        {'b': dog(head=10, neck=8)},
        {'b': dog(head=22, neck=18, spine=4, tail=10, legs=crouch), 'pl': (0, 0.04, -0.04)},
        {'b': dog(head=26, neck=20, jaw=12, spine=6, tail=14, legs=crouch), 'pl': (0, 0.06, -0.06)},
        {'b': dog(head=26, neck=20, jaw=18, spine=6, tail=14, legs=crouch), 'pl': (0, 0.07, -0.07)},
        {'b': dog(head=-10, neck=-18, jaw=40, spine=-6, tail=-20, legs=leap), 'pl': (0, -0.28, 0.1)},
        {'b': dog(head=-4, neck=-10, jaw=4, spine=-4, tail=-14, legs=leap), 'pl': (0, -0.3, 0.06)},
        {'b': dog(head=6, neck=4, jaw=6), 'pl': (0, -0.12, 0)},
        {'b': dog(head=10, neck=8)},
    ]
    stiff = {'fl.L': (-30, 0, 0), 'fl.R': (-20, 0, 0), 'hl.L': (30, 0, 0), 'hl.R': (20, 0, 0)}
    c['death'] = [
        {'b': dog(head=-25, neck=-20, jaw=30, tail=-20)},
        {'b': dog(head=20, neck=15, jaw=10, legs=crouch), 'pl': (0, 0, -0.1)},
        {'b': dog(head=20, neck=10, jaw=10, legs=crouch), 'pl': (0, 0, -0.14), 'rig': {'roll': 40, 'piv': (0, 0, 0.1)}},
        {'b': dog(head=10, neck=0, jaw=20, legs=stiff), 'pl': (0, 0, -0.06), 'rig': {'roll': 86, 'piv': (0, 0, 0.1)}},
        {'b': dog(head=8, neck=-4, jaw=24, legs=stiff), 'pl': (0, 0, -0.07), 'rig': {'roll': 90, 'piv': (0, 0, 0.1)}},
        {'b': dog(head=8, neck=-4, jaw=26, legs=stiff, tail=10), 'pl': (0, 0, -0.07), 'rig': {'roll': 90, 'piv': (0, 0, 0.1)}},
    ]
    for name, frames in c.items():
        for i, s in enumerate(frames):
            s['phase'] = i / len(frames)
            s['clip'] = name
    return c


def tweak(g):
    g['CLIPS'] = dog_clips()
