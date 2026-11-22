# Barro y Bruma - poses and clips for the survivor of the tower (art_src/superviviente/superviviente.blend).
# Same pose format as the other NPC tools (b, pl, ikR, ikL), no props to place.
import bpy, math
from mathutils import Matrix, Vector, Euler

RIG = bpy.data.objects['Rig']
ARM = RIG.data
REST = {b.name: b.matrix_local.copy() for b in ARM.bones}
PARENT = {b.name: (b.parent.name if b.parent else None) for b in ARM.bones}
LENGTH = {b.name: b.length for b in ARM.bones}
SCALE = 0.97            # young, a little shorter than the hero


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
    return Matrix.Rotation(math.radians(45 * d), 4, 'Z') @ Matrix.Diagonal((1.14 * SCALE, SCALE, SCALE, 1))


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
    bpy.context.view_layer.update()
    return D


# ---- clips -------------------------------------------------------------------------------------------
HOLD_L = (-0.03, -0.17, 1.03)        # wounded left forearm held across the belly
HOLD_R = (0.13, -0.15, 1.07)         # the right hand clutching it near the elbow
LEGS_REST = {'kn.R': (-2, 0, -3), 'an.R': (2, 0, 0), 'kn.L': (-11, 0, 5), 'an.L': (15, 0, 0), 'to.L': (-4, 0, 0)}


def body(breathe=0.0, headx=4, headz=0, lean=0.0, roll=0.0, legs=None):
    b = {'waist': (4 + lean, roll * 0.5, 0), 'chest': (4 + lean * 0.5 - 2 * breathe, roll * 0.3, 0),
         'upchest': (2 - 1.2 * breathe, 0, 0), 'neck': (0, 0, headz * 0.4), 'head': (headx, 0, headz * 0.6)}
    b.update(legs or LEGS_REST)
    return b


def st(b, ikR=HOLD_R, ikL=HOLD_L, pl=(-0.015, 0, -0.012)):
    s = {'b': b, 'pl': pl, 'ikL': ikL}
    if ikR is not None:
        s['ikR'] = ikR
    return s


# idle: holds the wounded arm, breathes fast, glances around (6 frames)
IDLE = [st(body(breathe=br, headz=hz, roll=-2), pl=(-0.015, 0, -0.012 + 0.004 * br))
        for br, hz in ((-1, 0), (1, -6), (-1, -12), (1, -6), (-1, 6), (1, 12))]


def _walk(i, n=8):
    """Limping: short, stiff steps on the left (wounded) leg, the body drops when it takes the weight."""
    p = i / n
    legs = {}
    for side, ph, amp, knee in (('R', p, 18, 30), ('L', (p + 0.5) % 1.0, 10, 12)):
        fwd = amp * math.cos(2 * math.pi * ph)
        bend = 6 + knee * max(0.0, math.sin(2 * math.pi * ph))
        legs['kn.' + side] = (-4 - fwd, 0, -3 if side == 'R' else 4)
        legs['an.' + side] = (bend, 0, 0)
    # the left leg is planted around p = 0.5: that is when he drops and leans onto it
    on_left = max(0.0, -math.cos(2 * math.pi * p))
    pl = (-0.01 + 0.025 * on_left, 0, -0.012 - 0.03 * on_left)
    roll = 5 * on_left - 2
    b = body(lean=4 + 3 * on_left, roll=roll, headz=-2 * roll, legs=legs)
    b['el.R'] = (-10 * math.cos(2 * math.pi * p), 0, 0)
    return st(b, ikR=None, pl=pl)


WALK = [_walk(i) for i in range(8)]

# llamar: lifts his good arm and waves to be seen, asking for help (6 frames; 2 to 5 loop)
_UP = (-0.33, -0.12, 1.72)
LLAMAR = [
    st(body(headx=0)),
    st(body(headx=-4, lean=-2), ikR=(-0.3, -0.18, 1.45)),
    st(body(headx=-6, lean=-3, roll=2), ikR=(-0.44, -0.1, 1.7)),
    st(body(headx=-6, lean=-3, roll=-1), ikR=(-0.2, -0.12, 1.78)),
    st(body(headx=-6, lean=-3, roll=2), ikR=(-0.44, -0.1, 1.7)),
    st(body(headx=-6, lean=-3, roll=-1), ikR=(-0.2, -0.12, 1.78)),
]

CLIPS = {'idle': IDLE, 'walk': WALK, 'llamar': LLAMAR}
