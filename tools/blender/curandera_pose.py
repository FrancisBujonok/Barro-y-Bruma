# Barro y Bruma - poses and clips for the healer (art_src/curandera/curandera.blend).
# Same pose format as lenador_pose.py plus:
#   staff  (ground point, side): ground point of the staff in armature space; the staff then runs through
#          the right hand wherever the IK left it.
#   swing  degrees the lantern swings (about the armature X axis).
#   skirt  (x, y, z) degrees the skirt and apron swing about the waist.
#   vial   True: the remedy vial shows in her left hand.
import bpy, math
from mathutils import Matrix, Vector, Euler

RIG = bpy.data.objects['Rig']
ARM = RIG.data
REST = {b.name: b.matrix_local.copy() for b in ARM.bones}
PARENT = {b.name: (b.parent.name if b.parent else None) for b in ARM.bones}
LENGTH = {b.name: b.length for b in ARM.bones}
SCALE = 0.93            # she is smaller than the hero


def _depth(n):
    d = 0
    while PARENT[n]:
        n = PARENT[n]
        d += 1
    return d


ORDER = sorted(REST.keys(), key=_depth)
SWORD = None


def T(v):
    return Matrix.Translation(Vector(v))


def eul(v):
    return Euler((math.radians(v[0]), math.radians(v[1]), math.radians(v[2])), 'XYZ').to_matrix()


def fk(rot, pl=(0, 0, 0)):
    D = {}
    for n in ORDER:
        p = PARENT[n]
        Dp = D[p] if p else T(pl)
        h = Dp @ REST[n].translation
        R = rot.get(n)
        D[n] = Dp if R is None else T(h) @ R.to_4x4() @ T(-h) @ Dp
    return D


def head(D, n):
    return D[n] @ REST[n].translation


def tail(D, n):
    return D[n] @ (REST[n] @ Vector((0, LENGTH[n], 0)))


def _ccd(rot, pl, chain, end, target, iters=30):
    target = Vector(target)
    for _ in range(iters):
        for n in chain:
            D = fk(rot, pl)
            e = tail(D, end)
            j = head(D, n)
            a, b = e - j, target - j
            if a.length < 1e-5 or b.length < 1e-5:
                continue
            rot[n] = a.rotation_difference(b).to_matrix() @ rot.get(n, Matrix.Identity(3))
        if (tail(fk(rot, pl), end) - target).length < 0.004:
            break


def solve(state):
    rot = {k: eul(v) for k, v in state.get('b', {}).items()}
    pl = state.get('pl', (0, 0, 0))
    if 'ikR' in state:
        _ccd(rot, pl, ['el.R', 'sh.R'], 'wr.R', state['ikR'])
    if 'ikL' in state:
        _ccd(rot, pl, ['el.L', 'sh.L'], 'wr.L', state['ikL'])
    for n in ('ha.R', 'ha.L'):
        if n in state.get('b', {}):
            rot[n] = eul(state['b'][n])
    return rot, pl


def rig_matrix(state, d):
    r = state.get('rig', {})
    return (Matrix.Rotation(math.radians(45 * d), 4, 'Z') @ T((0, -r.get('fwd', 0), r.get('up', 0)))
            @ Matrix.Diagonal((1.14 * SCALE, SCALE, SCALE, 1)))


# ---- skirt sway --------------------------------------------------------------------------------------
SKIRT_PIVOT = Vector((0, 0, 1.0))
SKIRT_PARTS = ['C_Falda', 'C_Delantal']
SHAWL_PIVOT = Vector((0, 0.02, 1.42))
for _n in SKIRT_PARTS + ['C_Chal']:
    _o = bpy.data.objects[_n]
    if 'rest_basis' not in _o:
        _o['rest_basis'] = [v for row in _o.matrix_basis for v in row]
SKIRT_BASE = {n: Matrix([bpy.data.objects[n]['rest_basis'][i * 4:i * 4 + 4] for i in range(4)])
              for n in SKIRT_PARTS + ['C_Chal']}


def set_skirt(r):
    R = T(SKIRT_PIVOT) @ eul(r).to_4x4() @ T(-SKIRT_PIVOT)
    for n in SKIRT_PARTS:
        o = bpy.data.objects[n]
        B = REST[o.parent_bone] @ T((0, LENGTH[o.parent_bone], 0)) @ o.matrix_parent_inverse
        o.matrix_basis = B.inverted() @ R @ B @ SKIRT_BASE[n]


def set_shawl(lean):
    """The shawl hangs: undo most of the forward lean of the spine so its back point falls, not sticks out."""
    R = T(SHAWL_PIVOT) @ eul((-0.65 * lean, 0, 0)).to_4x4() @ T(-SHAWL_PIVOT)
    o = bpy.data.objects['C_Chal']
    B = REST[o.parent_bone] @ T((0, LENGTH[o.parent_bone], 0)) @ o.matrix_parent_inverse
    o.matrix_basis = B.inverted() @ R @ B @ SKIRT_BASE['C_Chal']


# ---- staff and lantern -------------------------------------------------------------------------------
def place_staff(D, ground, swing):
    LB = bpy.app.driver_namespace['CB']
    # hand centre (armature space) from the solved right hand bone
    h = (head(D, 'ha.R') + tail(D, 'ha.R')) * 0.5
    g = Vector(ground)
    z = (h - g).normalized()
    out = Vector((-1.0, -0.4, 0.0))          # the crook leans to her right and a bit forward
    x = (out - z * out.dot(z)).normalized()
    y = z.cross(x)
    M = Matrix((x, y, z)).transposed().to_4x4()
    M.translation = g
    bpy.data.objects['C_Baston'].matrix_basis = M
    hook = M @ LB['HOOK']
    L = T(hook) @ Matrix.Rotation(math.radians(swing), 4, 'X') @ Matrix.Rotation(math.radians(swing * 0.4), 4, 'Y')
    bpy.data.objects['C_Farol'].matrix_basis = L


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
    set_skirt(state.get('skirt', (0, 0, 0)))
    bb = state.get('b', {})
    set_shawl(sum(bb.get(n, (0, 0, 0))[0] for n in ('waist', 'chest', 'upchest')))
    place_staff(D, state.get('staff', STAFF_IDLE), state.get('swing', 0.0))
    bpy.data.objects['C_Frasco'].hide_render = not state.get('vial', False)
    bpy.data.objects['C_Frasco'].hide_viewport = False
    bpy.context.view_layer.update()
    return D


# ---- clips -------------------------------------------------------------------------------------------
STAFF_IDLE = (-0.3, -0.3, 0.0)
HAND_R = (-0.255, -0.24, 0.96)
HAND_L = (0.02, -0.17, 1.2)          # holding the shawl closed at her chest


def body(breathe=0.0, headx=-17, heady=0, headz=0, lean=0.0, roll=0.0, legs=None, pl_z=0.0):
    """Hunched: the back bends forward, the neck and head come back up to look ahead."""
    b = {'waist': (12 + lean, roll * 0.5, 0), 'chest': (14 + lean * 0.6 - 1.5 * breathe, roll * 0.3, 0),
         'upchest': (8 - 1.0 * breathe, 0, 0), 'neck': (-14, 0, headz * 0.4), 'head': (headx, heady, headz * 0.6),
         'kn.R': (-8, 0, -3), 'an.R': (12, 0, 0), 'kn.L': (-8, 0, 3), 'an.L': (12, 0, 0)}
    if legs:
        b.update(legs)
    return b


def st(b, ikR=HAND_R, ikL=HAND_L, staff=STAFF_IDLE, swing=0.0, skirt=(0, 0, 0), pl=(0, 0, -0.02), vial=False):
    return {'b': b, 'ikR': ikR, 'ikL': ikL, 'staff': staff, 'swing': swing, 'skirt': skirt, 'pl': pl, 'vial': vial}


# idle: leaning on the staff, slow breath, the lantern sways a little (6 frames)
IDLE = [st(body(breathe=br, headz=hz), swing=sw, pl=(0, 0, -0.02 + 0.004 * br))
        for br, hz, sw in ((-1, 0, -6), (-0.4, 2, -2), (0.5, 3, 3), (1, 2, 6), (0.4, 0, 2), (-0.5, -1, -3))]


def _walk(i, n=8):
    p = i / n
    q = (p + 0.5) % 1.0
    legs = {}
    for side, ph in (('R', p), ('L', q)):
        fwd = 15 * math.cos(2 * math.pi * ph)
        bend = 10 + 24 * max(0.0, math.sin(2 * math.pi * ph))
        legs['kn.' + side] = (-8 - fwd, 0, -3 if side == 'R' else 3)
        legs['an.' + side] = (bend, 0, 0)
    # staff advances with the LEFT leg: planted while the left leg pushes back (q 0 -> 0.5), swung forward after
    if q < 0.5:
        s = q / 0.5
        gy, gz = -0.34 + 0.3 * s, 0.0
    else:
        s = (q - 0.5) / 0.5
        gy, gz = -0.04 - 0.3 * s, 0.07 * math.sin(math.pi * s)
    staff = (-0.31, gy, gz)
    hand = (-0.26, -0.13 + gy * 0.45, 0.97 + gz * 0.6)
    bob = -0.02 + 0.012 * math.cos(4 * math.pi * p)
    roll = 3 * math.cos(2 * math.pi * p)
    sk = (-4 * math.cos(2 * math.pi * p), 0, 3 * math.sin(2 * math.pi * p))
    return st(body(roll=roll, headz=-roll * 0.5, legs=legs), ikR=hand, staff=staff,
              swing=-8 * math.sin(2 * math.pi * q), skirt=sk, pl=(0, 0, bob))


WALK = [_walk(i) for i in range(8)]

# ofrecer: takes a remedy out of the apron and holds it out to the traveller (6 frames, hold the last)
OFRECER = [
    st(body()),
    st(body(headx=-6, lean=3), ikL=(0.1, -0.2, 0.92)),
    st(body(headx=-4, lean=4), ikL=(0.09, -0.19, 0.86), vial=True),
    st(body(headx=-10, lean=2), ikL=(0.14, -0.3, 1.0), vial=True),
    st(body(headx=-18, lean=6, heady=4), ikL=(0.2, -0.43, 1.02), vial=True, swing=3),
    st(body(headx=-18, lean=6, heady=6), ikL=(0.21, -0.44, 1.03), vial=True, swing=-2),
]

CLIPS = {'idle': IDLE, 'walk': WALK, 'ofrecer': OFRECER}
