# Barro y Bruma - the taken (art_src/tomado/tomado.blend): a villager who looks almost healthy, which is the
# scary part. Head tilted to one side, mouth hanging open with a violet glow inside, and violet threads drifting
# out of his eyes. Plain shirt and vest, short hair.
import bpy, bmesh, math, random
from mathutils import Vector, Matrix

HEAD_C = Vector((0.0, -0.01, 1.6))
HEAD_R = (0.094, 0.104, 0.114)


def mats():
    pm = pal_mat
    pm('T_Piel', ['3d332b', '7a6650', 'b08a6a', 'd2ac88'])
    pm('T_Boca', ['141210', '141210', '2a2420', '2a2420'])
    pm('T_Brillo', ['2c2738', '5b5170', '5b5170', 'c4bfb2'])
    pm('T_Hilo', ['5b5170', '5b5170', '5b5170', 'c4bfb2'])
    pm('T_Camisa', ['2a2420', '6e6c66', '9a968c', 'c4bfb2'])
    pm('T_Chaleco', ['141210', '2a2420', '3d332b', '594a3c'])
    pm('T_Calzas', ['141210', '2a2420', '2f3436', '4f5a5c'])
    pm('T_Gorra', ['141210', '2f3436', '4f5a5c', '7d8584'])
    pm('T_Pelo', ['141210', '2a2420', '3d332b', '594a3c'])
    pm('T_Ojo', ['2c2738', '5b5170', '5b5170', 'c4bfb2'])


def to_bone(ob, bone):
    b = ARM.bones[bone]
    ob.parent = RIG
    ob.parent_type = 'BONE'
    ob.parent_bone = bone
    ob.matrix_parent_inverse = Matrix.Identity(4)
    ob.matrix_basis = (b.matrix_local @ Matrix.Translation((0, b.length, 0))).inverted()


def build_body():
    bpy.data.objects['Body'].data.materials[0] = mat('T_Calzas')
    bpy.data.objects['Tunic'].data.materials[0] = mat('T_Chaleco')
    bpy.data.objects['Boots'].data.materials[0] = mat('T_Chaleco')
    for n in ('Eye.L', 'Eye.R'):
        e = bpy.data.objects[n]
        e.data.materials[0] = mat('T_Ojo')
        w = e.matrix_world.copy()
        w.translation.y = -0.116
        e.matrix_world = w


def build_head(seed=91):
    rnd = random.Random(seed)
    bm = bmesh.new()
    ellipsoid(bm, HEAD_C, HEAD_R, 14, 10)
    ellipsoid(bm, (0, -0.108, 1.566), (0.015, 0.024, 0.024), 8, 6)
    ellipsoid(bm, (0, -0.07, 1.5), (0.042, 0.035, 0.03), 8, 6)        # jaw hanging low
    to_bone(new_obj('T_Cabeza', bm, 'T_Piel', parent_rig=True), 'head')
    bm = bmesh.new()
    ellipsoid(bm, (0, -0.094, 1.52), (0.032, 0.022, 0.036), 8, 6)
    to_bone(new_obj('T_Boca', bm, 'T_Boca', parent_rig=True), 'head')
    bm = bmesh.new()
    ellipsoid(bm, (0, -0.104, 1.518), (0.022, 0.014, 0.026), 6, 5)
    to_bone(new_obj('T_BrilloBoca', bm, 'T_Brillo', parent_rig=True), 'head')
    # violet threads out of both eyes: thick enough to survive the pixels, drifting down and out
    bm = bmesh.new()
    for s in (-1, 1):
        eye = Vector((0.036 * s, -0.12, 1.577))
        for k in range(3):
            # mostly forward and out, so the top-down camera sees them crossing the face and leaving it
            d = Vector((s * rnd.uniform(0.25, 0.8), -rnd.uniform(0.7, 1.0), rnd.uniform(-0.25, 0.15))).normalized()
            pts = [eye]
            p = eye.copy()
            for j in range(6):
                d = (d + Vector((s * rnd.uniform(0.0, 0.25), rnd.uniform(-0.15, 0.1), rnd.uniform(-0.12, 0.08)))).normalized()
                p = p + d * rnd.uniform(0.04, 0.055)
                pts.append(p.copy())
            tube(bm, pts, [0.017, 0.016, 0.014, 0.012, 0.009, 0.006, 0.004], 4)
    to_bone(new_obj('T_Hilos', bm, 'T_Hilo', parent_rig=True), 'head')
    # short dark hair
    bm = bmesh.new()
    def scalp(u):
        return u.z > -0.2 and not (u.y < -0.5 and u.z < 0.4)
    for u in scalp_dirs(rnd, 40, scalp):
        root = on_head(u, 0.95)
        pts = strand(rnd, root, u + Vector((0, 0.2, -0.5)), rnd.uniform(0.04, 0.07), 0.4, 0.3, 3)
        tube(bm, pts, [0.024, 0.016, 0.008, 0.004][:len(pts)], 4)
    to_bone(new_obj('T_Pelo', bm, 'T_Pelo', parent_rig=True), 'head')
    # no cap: from the sprite camera it covered the eyes and the threads

def build_arms(seed=92):
    rnd = random.Random(seed)
    for side in ('R', 'L'):
        el = ARM.bones['el.' + side]
        wr = ARM.bones['wr.' + side]
        ha = ARM.bones['ha.' + side]
        bm = bmesh.new()
        pts = [Vector((0, el.length * t, 0)) for t in (-0.1, 0.4, 0.9, 1.05)]
        tube(bm, pts, [0.066, 0.062, 0.06, 0.062], 8, cap_tip=False)
        new_obj('T_Manga.' + side, bm, 'T_Camisa', 'el.' + side)
        bm = bmesh.new()
        pts = [Vector((0, wr.length * t, 0)) for t in (-0.05, 0.5, 0.88)]
        tube(bm, pts, [0.058, 0.055, 0.052], 8, cap_tip=False)
        new_obj('T_Puno.' + side, bm, 'T_Camisa', 'wr.' + side)
        bm = bmesh.new()
        ellipsoid(bm, (0, ha.length * 0.62, 0), (0.028, 0.06, 0.04), 10, 7)
        new_obj('T_Mano.' + side, bm, 'T_Piel', 'ha.' + side)


def build_all():
    RIG.matrix_world = Matrix.Identity(4)
    for pb in RIG.pose.bones:
        pb.matrix_basis = Matrix.Identity(4)
    bpy.context.view_layer.update()
    mats()
    build_body()
    build_head()
    build_arms()
    bpy.context.view_layer.update()


STYLE = {'hunch': 2, 'head_tilt': 26, 'arms': 'hang', 'twitch': 0.7, 'sway': 0.5, 'attack': 'lunge',
         'gait': {'stride': 14, 'knee': 12, 'bob': 0.006, 'sway': 0.5, 'lean': 1, 'swing': 3}}


def tweak(g):
    pass
