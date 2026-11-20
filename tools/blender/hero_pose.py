# Barro y Bruma - posing tool for the Blender hero (art_src/heroe_blender/heroe_referencia.blend).
# Run inside Blender: exec(open(r"...\tools\blender\hero_pose.py").read())
#
# A pose ("state") is a dict:
#   b     {bone: (x, y, z)} rotations in degrees, in ARMATURE axes, applied about the bone's posed head
#         on top of what its parents did. Axes: the hero faces -Y, +X is his left, +Z up.
#         Arms and legs hang down, so -X swings them forward; for the right arm +Y lifts it outwards
#         (to his right), for the left arm -Y does. On the spine +X leans forward, +Z turns his chest
#         towards his left.
#   pl    pelvis offset (x, y, z) in metres (hip drop, sway).
#   ikR / ikL  target for the right / left hand (armature space). Solved on the shoulder and elbow.
#   sw    (blade_dir, edge_dir): where the blade points and where its edge faces (armature space).
#         Solved on the right hand bone after the arm IK.
#   gripL True: the left hand goes to the grip, under the right hand (two-handed blows).
#   rig   {fwd, up, pitch, roll, yaw, piv}: whole-body offsets (metres, degrees). pitch tips him
#         forward about piv, roll leans him to his left, yaw turns him (degrees, + = to his left).
#   sword 'hand' (default), 'sheath', or a float s in 0..1: sheathed sword pulled out by s.
import bpy, math, json, os
from mathutils import Matrix, Vector, Euler, Quaternion

RIG = bpy.data.objects['Rig']
ARM = RIG.data
REST = {b.name: b.matrix_local.copy() for b in ARM.bones}
PARENT = {b.name: (b.parent.name if b.parent else None) for b in ARM.bones}
LENGTH = {b.name: b.length for b in ARM.bones}


def _depth(n):
    d = 0
    while PARENT[n]:
        n = PARENT[n]
        d += 1
    return d


ORDER = sorted(REST.keys(), key=_depth)
SWORD = bpy.data.objects['Sword']
SWORD_LOCAL = SWORD.matrix_parent_inverse @ SWORD.matrix_basis if 'SWORD_LOCAL' not in globals() else SWORD_LOCAL


def T(v):
    return Matrix.Translation(Vector(v))


def eul(v):
    return Euler((math.radians(v[0]), math.radians(v[1]), math.radians(v[2])), 'XYZ').to_matrix()


def fk(rot, pl=(0, 0, 0)):
    """rot: {bone: 3x3 Matrix (global)}. Returns {bone: 4x4 delta in armature space}."""
    D = {}
    for n in ORDER:
        p = PARENT[n]
        Dp = D[p] if p else T(pl)
        h = Dp @ REST[n].translation
        R = rot.get(n)
        if R is None:
            D[n] = Dp
        else:
            D[n] = T(h) @ R.to_4x4() @ T(-h) @ Dp
    return D


def head(D, n):
    return D[n] @ REST[n].translation


def tail(D, n):
    return D[n] @ (REST[n] @ Vector((0, LENGTH[n], 0)))


def sword_matrix(D):
    return D['ha.R'] @ REST['ha.R'] @ T((0, LENGTH['ha.R'], 0)) @ SWORD_LOCAL


def _ccd(rot, pl, chain, end, target, iters=24):
    target = Vector(target)
    for _ in range(iters):
        for n in chain:
            D = fk(rot, pl)
            e = tail(D, end)
            j = head(D, n)
            a, b = e - j, target - j
            if a.length < 1e-5 or b.length < 1e-5:
                continue
            q = a.rotation_difference(b)
            rot[n] = q.to_matrix() @ rot.get(n, Matrix.Identity(3))
        if (tail(fk(rot, pl), end) - target).length < 0.004:
            break


def _frame(d, e):
    d = Vector(d).normalized()
    e = Vector(e)
    e = (e - d * e.dot(d)).normalized()
    n = d.cross(e)
    return Matrix((d, e, n)).transposed()


def solve(state):
    rot = {k: eul(v) for k, v in state.get('b', {}).items()}
    pl = state.get('pl', (0, 0, 0))
    if 'ikR' in state:
        tgt = state['ikR']
        if isinstance(tgt, str):     # 'grip': the hand goes to the sheathed grip
            D = fk(rot, pl)
            sv = state.get('sword', 'sheath')
            s = 0.0 if sv in ('hand', 'sheath') else float(sv)
            tgt = D['pelvis'] @ (hold_rest() @ Vector((0, 0, 0.62 * s + 0.09)))
        _ccd(rot, pl, ['el.R', 'sh.R'], 'wr.R', tgt)
    if 'sw' in state:
        d, e = state['sw']
        D = fk(rot, pl)
        S = sword_matrix(D).to_3x3()
        cur = _frame(S @ Vector((0, 0, -1)), S @ Vector((1, 0, 0)))
        want = _frame(d, e)
        Q = want @ cur.transposed()
        rot['ha.R'] = Q @ rot.get('ha.R', Matrix.Identity(3))
    if state.get('gripL'):
        D = fk(rot, pl)
        g = sword_matrix(D) @ Vector((0, 0, 0.13))
        _ccd(rot, pl, ['el.L', 'sh.L'], 'wr.L', g)
    elif 'ikL' in state:
        _ccd(rot, pl, ['el.L', 'sh.L'], 'wr.L', state['ikL'])
    return rot, pl


def rig_matrix(state, d):
    r = state.get('rig', {})
    piv = Vector(r.get('piv', (0, 0, 0.9)))
    pitch = Matrix.Rotation(math.radians(r.get('pitch', 0)), 4, 'X')    # + = tip forward (towards -Y)
    roll = Matrix.Rotation(math.radians(r.get('roll', 0)), 4, 'Y')
    yaw = Matrix.Rotation(math.radians(r.get('yaw', 0)), 4, 'Z')
    return (Matrix.Rotation(math.radians(45 * d), 4, 'Z') @ T((0, -r.get('fwd', 0), r.get('up', 0)))
            @ T(piv) @ pitch @ T(-piv) @ roll @ yaw @ Matrix.Diagonal((1.14, 1, 1, 1)))


def apply(state, d=0):
    rot, pl = solve(state)
    D = fk(rot, pl)
    for n in ORDER:
        pb = RIG.pose.bones[n]
        pose = D[n] @ REST[n]
        p = PARENT[n]
        if p:
            ppose = D[p] @ REST[p]
            rel = REST[p].inverted() @ REST[n]
            pb.matrix_basis = rel.inverted() @ ppose.inverted() @ pose
        else:
            pb.matrix_basis = REST[n].inverted() @ pose
    RIG.matrix_world = rig_matrix(state, d)
    set_sword(state.get('sword', 'hand'))
    if 'cloak' in state:
        set_cloak(state['cloak'])
    else:   # keep the cloak hanging when he leans: undo most of the spine's forward lean
        b = state.get('b', {})
        lean = sum(b.get(n, (0, 0, 0))[0] for n in ('waist', 'chest', 'upchest'))
        set_cloak((-0.8 * lean, 0, 0))
    bpy.context.view_layer.update()
    return D


# ---- scabbard and sheathed sword -------------------------------------------------------------------

SWORD_PARTS = ['Blade', 'Grip', 'Guard', 'Pommel']


def ensure_scabbard():
    """Leather scabbard on the left hip, tip back and down, and a copy of the sword inside it."""
    if 'Scabbard' in bpy.data.objects:
        return
    import bmesh
    mat_sc = bpy.data.materials.get('T_Vaina') or bpy.data.materials['T_Botas'].copy()
    mat_sc.name = 'T_Vaina'
    mat_tip = bpy.data.materials['T_Hebilla']
    # Holder empty on the waist bone: its -Z is the scabbard axis (mouth at the origin).
    hold = bpy.data.objects.new('ScabbardHold', None)
    bpy.context.scene.collection.objects.link(hold)
    hold.parent = RIG
    hold.parent_type = 'BONE'
    hold.parent_bone = 'pelvis'
    # pelvis bone: head (0,1,0.93) tail (0,0,0.93), so bone space Y points to -Y in armature space.
    # Place it in armature space and convert.
    mouth = Vector((0.185, -0.06, 0.985))
    axis = Vector((0.10, 0.36, -0.93)).normalized()     # down, a little back and out
    edge = Vector((0, 0, 1)).cross(axis).normalized()
    z = -axis
    x = edge
    y = z.cross(x)
    M = Matrix((x, y, z)).transposed().to_4x4()
    M.translation = mouth
    bone_tail = REST['pelvis'] @ T((0, LENGTH['pelvis'], 0))
    hold.matrix_parent_inverse = Matrix.Identity(4)
    hold.matrix_basis = bone_tail.inverted() @ M
    # Scabbard mesh: a flat tapered box along -Z of the holder, 0.66 m long.
    bm = bmesh.new()
    L, w0, w1, t = 0.66, 0.034, 0.024, 0.016
    vs = []
    for z_, w in ((0.02, w0), (-L, w1)):
        for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
            vs.append(bm.verts.new((sx * w, sy * t, z_)))
    for f in ((0, 1, 2, 3), (7, 6, 5, 4), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)):
        bm.faces.new([vs[i] for i in f])
    me = bpy.data.meshes.new('Scabbard')
    bm.to_mesh(me)
    sc = bpy.data.objects.new('Scabbard', me)
    sc.data.materials.append(mat_sc)
    bpy.context.scene.collection.objects.link(sc)
    sc.parent = hold
    # metal chape at the tip
    bm = bmesh.new()
    vs = []
    for z_, w in ((-L + 0.07, w1 + 0.004), (-L - 0.01, w1 * 0.6)):
        for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
            vs.append(bm.verts.new((sx * w, sy * (t + 0.003), z_)))
    for f in ((0, 1, 2, 3), (7, 6, 5, 4), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)):
        bm.faces.new([vs[i] for i in f])
    me = bpy.data.meshes.new('Chape')
    bm.to_mesh(me)
    ch = bpy.data.objects.new('Chape', me)
    ch.data.materials.append(mat_tip)
    bpy.context.scene.collection.objects.link(ch)
    ch.parent = hold
    # Sheathed copy of the sword: an empty under the holder, blade along -Z like the real one.
    ss = bpy.data.objects.new('SwordSheath', None)
    bpy.context.scene.collection.objects.link(ss)
    ss.parent = hold
    for n in SWORD_PARTS:
        src = bpy.data.objects[n]
        c = src.copy()
        c.name = n + '_S'
        bpy.context.scene.collection.objects.link(c)
        c.parent = ss
        c.matrix_parent_inverse = Matrix.Identity(4)
        c.matrix_basis = src.matrix_basis.copy()


CLOAK_PIVOT = Vector((0, 0.03, 1.40))
CLOAK_PARTS = ['Cloak', 'ScarfTail']
# The rest placement is kept on the objects themselves, so reloading this file never picks up a posed cloak.
for _n in CLOAK_PARTS:
    _o = bpy.data.objects[_n]
    if 'rest_basis' not in _o:
        _o['rest_basis'] = [v for row in _o.matrix_basis for v in row]
CLOAK_BASE = {n: Matrix([bpy.data.objects[n]['rest_basis'][i * 4:i * 4 + 4] for i in range(4)]) for n in CLOAK_PARTS}


def set_cloak(r):
    """Swing the cloak (and scarf tail) about the neck, in torso axes. +X sends the hem backwards."""
    R = T(CLOAK_PIVOT) @ eul(r).to_4x4() @ T(-CLOAK_PIVOT)
    for n in CLOAK_PARTS:
        o = bpy.data.objects[n]
        B = REST[o.parent_bone] @ T((0, LENGTH[o.parent_bone], 0)) @ o.matrix_parent_inverse
        o.matrix_basis = B.inverted() @ R @ B @ CLOAK_BASE[n]


def hold_rest():
    hold = bpy.data.objects['ScabbardHold']
    return REST['pelvis'] @ T((0, LENGTH['pelvis'], 0)) @ hold.matrix_parent_inverse @ hold.matrix_basis


def set_sword(mode):
    hand = mode == 'hand'
    for n in SWORD_PARTS:
        bpy.data.objects[n].hide_render = not hand
        c = bpy.data.objects.get(n + '_S')
        if c:
            c.hide_render = hand
    ss = bpy.data.objects.get('SwordSheath')
    if ss:
        s = 0.0 if mode in ('hand', 'sheath') else float(mode)
        ss.location = (0, 0, 0.62 * s)


def sheath_grip(D=None):
    """Armature-space position of the sheathed grip (to put the hand there)."""
    bpy.context.view_layer.update()
    g = bpy.data.objects['Grip_S'].matrix_world.translation
    return RIG.matrix_world.inverted() @ g
