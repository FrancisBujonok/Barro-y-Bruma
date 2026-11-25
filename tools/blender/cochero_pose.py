# Barro y Bruma - poses and clips for the coachman (art_src/cochero/cochero.blend).
# Same pose format as the other NPC tools (b, pl, ikR, ikL) plus:
#   hat    (lift, tilt): the top hat follows the head; lift raises it off the head (m), tilt tips it forward (deg)
#   onR    ('head' | 'hat', point): right-hand IK target given as a rest-space point carried by the head or the
#          hat, solved after the body pose (so the hand finds the brim or the mouth whatever the bow)
#   whip   direction of the broken whip's stick from the left fist (armature space)
#   skirt  (x, y, z) degrees the coat skirts swing about the waist
import bpy, math
from mathutils import Matrix, Vector, Euler

RIG = bpy.data.objects['Rig']
ARM = RIG.data
REST = {b.name: b.matrix_local.copy() for b in ARM.bones}
PARENT = {b.name: (b.parent.name if b.parent else None) for b in ARM.bones}
LENGTH = {b.name: b.length for b in ARM.bones}
SCALE = 1.03            # tall and gaunt


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


# ---- hat ----------------------------------------------------------------------------------------------
HAT_PIVOT = Vector((0, -0.1, 1.69))         # front of the crown of the head: the hat tips about it


def hat_matrix(D, hat):
    lift, tilt = hat
    return D['head'] @ T(HAT_PIVOT) @ eul((tilt, 0, 0)).to_4x4() @ T(-HAT_PIVOT) @ T((0, 0, lift))


def solve(state):
    rot = {k: eul(v) for k, v in state.get('b', {}).items()}
    pl = state.get('pl', (0, 0, 0))
    if 'ikL' in state:
        _ccd(rot, pl, ['el.L', 'sh.L'], 'wr.L', state['ikL'])
    target = state.get('ikR')
    if 'onR' in state:
        what, p = state['onR']
        D = fk(rot, pl)
        M = D['head'] if what == 'head' else hat_matrix(D, state.get('hat', (0, 0)))
        target = M @ Vector(p)
    if target is not None:
        _ccd(rot, pl, ['el.R', 'sh.R'], 'wr.R', target)
    for n in ('ha.R', 'ha.L'):
        if n in state.get('b', {}):
            rot[n] = eul(state['b'][n])
    return rot, pl


def rig_matrix(state, d):
    return Matrix.Rotation(math.radians(45 * d), 4, 'Z') @ Matrix.Diagonal((1.14 * SCALE, SCALE, SCALE, 1))


# ---- coat skirts, hat and whip ------------------------------------------------------------------------
SKIRT_PIVOT = Vector((0, 0, 1.04))
SKIRTS = ['C_FaldonFR', 'C_FaldonFL', 'C_FaldonBR', 'C_FaldonBL']


def set_skirts(D, r):
    """The skirts hang from the waist and stay roughly plumb whatever the lean, plus a swing."""
    pw = D['waist'] @ SKIRT_PIVOT
    M = T(pw) @ eul(r).to_4x4() @ T(-SKIRT_PIVOT)
    for n in SKIRTS:
        bpy.data.objects[n].matrix_basis = M


def place_whip(D, direction):
    g = (head(D, 'ha.L') * 0.4 + tail(D, 'ha.L') * 0.6)
    z = Vector(direction).normalized()
    out = Vector((1.0, 0.0, 0.0))
    x = (out - z * out.dot(z)).normalized()
    y = z.cross(x)
    M = Matrix((x, y, z)).transposed().to_4x4()
    M.translation = g
    bpy.data.objects['C_Latigo'].matrix_basis = M


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
    bpy.data.objects['C_Galera'].matrix_basis = hat_matrix(D, state.get('hat', (0, 0)))
    set_skirts(D, state.get('skirt', (0, 0, 0)))
    place_whip(D, state.get('whip', WHIP_DOWN))
    bpy.context.view_layer.update()
    return D


# ---- clips -------------------------------------------------------------------------------------------
RIBS_R = (-0.07, -0.16, 1.15)        # right hand pressed on his bruised ribs
WHIP_L = (0.25, -0.1, 0.9)           # left fist low at his side, holding the broken whip
WHIP_DOWN = (0.1, -0.45, -0.89)
LEGS_REST = {'kn.R': (-7, 0, -4), 'an.R': (10, 0, 0), 'kn.L': (0, 0, 4), 'an.L': (0, 0, 0)}
BRIM = (-0.06, -0.2, 1.64)           # rest-space point under the right front of the brim (carried by the hat)
MOUTH = (-0.02, -0.19, 1.5)          # in front of the mouth (carried by the head)


def body(breathe=0.0, headx=5, headz=0, heady=0, lean=3.0, roll=0.0, legs=None):
    b = {'waist': (lean, roll * 0.5, 0), 'chest': (2 + lean * 0.5 - 1.5 * breathe, roll * 0.3, 0),
         'upchest': (1 - breathe, 0, 0), 'neck': (2, heady * 0.4, headz * 0.4), 'head': (headx, heady * 0.6, headz * 0.6)}
    b.update(legs or LEGS_REST)
    return b


def st(b, ikR=RIBS_R, ikL=WHIP_L, pl=(0.01, 0, -0.006), hat=(0, 0), whip=WHIP_DOWN, skirt=(0, 0, 0), onR=None):
    s = {'b': b, 'ikL': ikL, 'pl': pl, 'hat': hat, 'whip': whip, 'skirt': skirt}
    if onR is not None:
        s['onR'] = onR
    elif ikR is not None:
        s['ikR'] = ikR
    return s


# idle: dazed after the crash, hand on the ribs, shallow breath, sways and shakes his head once (6 frames)
IDLE = [st(body(breathe=br, roll=rl, heady=hy, headz=hz), pl=(0.01, 0, -0.006 + 0.003 * br), skirt=(0, rl * 0.6, 0))
        for br, rl, hy, hz in ((-1, -2, 4, 0), (0, -1, 4, 0), (1, 1, 2, 0), (0, 2, 0, -8), (-1, 1, 0, 6), (0.5, -1, 3, 0))]


def _walk(i, n=8):
    """Limping on the right leg (the scraped knee): short stiff step, the body drops onto the left."""
    p = i / n
    legs = {}
    for side, ph, amp, knee in (('L', p, 18, 30), ('R', (p + 0.5) % 1.0, 10, 12)):
        fwd = amp * math.cos(2 * math.pi * ph)
        bend = 6 + knee * max(0.0, math.sin(2 * math.pi * ph))
        legs['kn.' + side] = (-4 - fwd, 0, -4 if side == 'R' else 4)
        legs['an.' + side] = (bend, 0, 0)
    on_right = max(0.0, -math.cos(2 * math.pi * p))
    pl = (-0.025 * on_right + 0.01, 0, -0.008 - 0.025 * on_right)
    roll = -5 * on_right + 2
    sw = math.cos(2 * math.pi * p)
    b = body(lean=5 + 2 * on_right, roll=roll, headz=-2 * roll, legs=legs)
    return st(b, ikL=(0.25, -0.1 - 0.07 * sw, 0.92), pl=pl, whip=(0.1 + 0.1 * sw, -0.45, -0.89),
              skirt=(-6 * sw, -roll * 0.5, 0))


WALK = [_walk(i) for i in range(8)]

# saludo: tips his hat to the traveller with a small pained bow (6 frames; the last one is the rest pose again)
SALUDO = [
    st(body()),
    st(body(headx=8, lean=4), onR=('hat', BRIM)),
    st(body(headx=14, lean=10, breathe=0.3), hat=(0.05, -14), onR=('hat', BRIM)),
    st(body(headx=16, lean=12), hat=(0.06, -16), onR=('hat', BRIM)),
    st(body(headx=8, lean=5), hat=(0.0, 0), onR=('hat', BRIM)),
    st(body()),
]

# tos: doubles over coughing into his hand, twice (6 frames, ambient)
TOS = [
    st(body()),
    st(body(headx=10, lean=9), onR=('head', MOUTH)),
    st(body(headx=20, lean=20, breathe=-1), onR=('head', MOUTH), pl=(0.01, 0, -0.02), skirt=(4, 0, 0)),
    st(body(headx=12, lean=11, breathe=1), onR=('head', MOUTH)),
    st(body(headx=22, lean=22, breathe=-1), onR=('head', MOUTH), pl=(0.01, 0, -0.022), skirt=(5, 0, 0)),
    st(body(headx=8, lean=6), ikR=(-0.06, -0.19, 1.3)),
]

CLIPS = {'idle': IDLE, 'walk': WALK, 'saludo': SALUDO, 'tos': TOS}
