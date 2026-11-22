# Barro y Bruma - poses and clips for the gate guard (art_src/guardia/guardia.blend).
# Same pose format as the other NPC tools plus:
#   spear  ground point of the spear butt (armature space); the shaft runs through the right hand.
#   tabard (x, y, z) degrees the tabard panels swing about the waist.
import bpy, math
from mathutils import Matrix, Vector, Euler

RIG = bpy.data.objects['Rig']
ARM = RIG.data
REST = {b.name: b.matrix_local.copy() for b in ARM.bones}
PARENT = {b.name: (b.parent.name if b.parent else None) for b in ARM.bones}
LENGTH = {b.name: b.length for b in ARM.bones}
SCALE = 1.0


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


# ---- tabard sway ------------------------------------------------------------------------------------
TABARD_PIVOT = Vector((0, 0, 1.12))
TABARD_PARTS = ['G_SobrevesteF', 'G_SobrevesteB']
for _n in TABARD_PARTS:
    _o = bpy.data.objects[_n]
    if 'rest_basis' not in _o:
        _o['rest_basis'] = [v for row in _o.matrix_basis for v in row]
TABARD_BASE = {n: Matrix([bpy.data.objects[n]['rest_basis'][i * 4:i * 4 + 4] for i in range(4)]) for n in TABARD_PARTS}


def set_tabard(r, lean):
    """Panels hang straight down: undo the spine's forward lean, then add the swing."""
    R = T(TABARD_PIVOT) @ eul((r[0] - 0.8 * lean, r[1], r[2])).to_4x4() @ T(-TABARD_PIVOT)
    for n in TABARD_PARTS:
        o = bpy.data.objects[n]
        B = REST[o.parent_bone] @ T((0, LENGTH[o.parent_bone], 0)) @ o.matrix_parent_inverse
        o.matrix_basis = B.inverted() @ R @ B @ TABARD_BASE[n]


def place_spear(D, ground):
    h = (head(D, 'ha.R') + tail(D, 'ha.R')) * 0.5
    g = Vector(ground)
    z = (h - g).normalized()
    out = Vector((1.0, 0.0, 0.0))            # the flat of the blade faces forward, its edges to the sides
    x = (out - z * out.dot(z)).normalized()
    y = z.cross(x)
    M = Matrix((x, y, z)).transposed().to_4x4()
    M.translation = g
    bpy.data.objects['G_Lanza'].matrix_basis = M


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
    bb = state.get('b', {})
    set_tabard(state.get('tabard', (0, 0, 0)), sum(bb.get(n, (0, 0, 0))[0] for n in ('waist', 'chest')))
    place_spear(D, state.get('spear', SPEAR_IDLE))
    bpy.context.view_layer.update()
    return D


# ---- clips -------------------------------------------------------------------------------------------
SPEAR_IDLE = (-0.3, -0.12, 0.0)
HAND_R = (-0.3, -0.14, 1.06)         # spear upright beside his right foot
HAND_L = (0.2, -0.06, 0.98)          # resting on the pommel of his sword
LEGS = {'kn.R': (0, 0, -6), 'an.R': (0, 0, 0), 'kn.L': (0, 0, 6), 'an.L': (0, 0, 0)}


def body(breathe=0.0, headx=0, headz=0, lean=0.0, roll=0.0, legs=None):
    b = {'waist': (lean, roll * 0.5, 0), 'chest': (1 + lean * 0.5 - 1.5 * breathe, roll * 0.3, 0),
         'upchest': (-1 - breathe, 0, 0), 'neck': (0, 0, headz * 0.4), 'head': (headx, 0, headz * 0.6)}
    b.update(legs or LEGS)
    return b


def st(b, ikR=HAND_R, ikL=HAND_L, spear=SPEAR_IDLE, pl=(0, 0, 0), tabard=(0, 0, 0)):
    s = {'b': b, 'ikR': ikR, 'spear': spear, 'pl': pl, 'tabard': tabard}
    if ikL is not None:
        s['ikL'] = ikL
    return s


# idle: on guard at the gate, spear upright, slow breath, looks left and right now and then (6 frames)
IDLE = [st(body(breathe=br, headz=hz), pl=(0, 0, 0.004 * br))
        for br, hz in ((-1, 0), (0, 0), (1, 0), (0, -10), (-1, -10), (0.5, 0))]


def _walk(i, n=8):
    p = i / n
    legs = {}
    for side, ph in (('R', p), ('L', (p + 0.5) % 1.0)):
        fwd = 20 * math.cos(2 * math.pi * ph)
        bend = 6 + 30 * max(0.0, math.sin(2 * math.pi * ph))
        legs['kn.' + side] = (-fwd, 0, -4 if side == 'R' else 4)
        legs['an.' + side] = (bend, 0, 0)
    bob = 0.012 * math.cos(4 * math.pi * p) - 0.008
    b = body(lean=3, roll=2 * math.cos(2 * math.pi * p), legs=legs)
    b['el.L'] = (16 * math.cos(2 * math.pi * p), 0, 0)
    lift = 0.06 + 0.02 * math.cos(4 * math.pi * p)        # carried upright, butt off the ground
    return st(b, ikR=(-0.3, -0.16, 1.02 + bob), ikL=None, spear=(-0.31, -0.2, lift + bob),
              pl=(0, 0, bob), tabard=(-5 * math.cos(2 * math.pi * p), 0, 0))


WALK = [_walk(i) for i in range(8)]

# alto: stops the traveller - left palm out, the spear leans forward a little, chin up (6 frames, hold the last)
ALTO = [
    st(body()),
    st(body(headx=-2), ikL=(0.24, -0.22, 1.18)),
    st(body(headx=-4, lean=-1, roll=-2), ikL=(0.3, -0.34, 1.42), ikR=(-0.3, -0.2, 1.08), spear=(-0.3, -0.06, 0.0)),
    st(body(headx=-6, lean=-2, roll=-3), ikL=(0.31, -0.38, 1.5), ikR=(-0.3, -0.24, 1.09), spear=(-0.3, -0.04, 0.0)),
    st(body(headx=-6, lean=-2, roll=-3, breathe=0.6), ikL=(0.31, -0.38, 1.51), ikR=(-0.3, -0.24, 1.09), spear=(-0.3, -0.04, 0.0)),
    st(body(headx=-6, lean=-2, roll=-3, breathe=-0.4), ikL=(0.31, -0.38, 1.5), ikR=(-0.3, -0.24, 1.09), spear=(-0.3, -0.04, 0.0)),
]
for _s in ALTO[2:]:
    _s['b']['ha.L'] = (-80, 0, 15)       # hand up, palm turned out to the traveller

CLIPS = {'idle': IDLE, 'walk': WALK, 'alto': ALTO}
