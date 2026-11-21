# Barro y Bruma - poses, chains and clips for the locked-up woodcutter (art_src/lenador/lenador.blend).
# Same pose format as hero_pose.py (b, pl, ikR, ikL, rig), without sword or cloak, plus:
#   bruma  0..1: the skin turns Bruma-violet in its shadows (the doctor's experiments surfacing).
# Load in Blender: ns = {}; exec(open(this).read(), ns); exec(open(hero_render.py).read(), ns)
import bpy, bmesh, math, json, os
from mathutils import Matrix, Vector, Euler

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
    hands = state.get('hands', {})
    if 'ikR' in state:
        _ccd(rot, pl, ['el.R', 'sh.R'], 'wr.R', state['ikR'])
    if 'ikL' in state:
        _ccd(rot, pl, ['el.L', 'sh.L'], 'wr.L', state['ikL'])
    # hands: extra rotation after the IK (armature axes)
    for n in ('ha.R', 'ha.L'):
        if n in state.get('b', {}):
            rot[n] = eul(state['b'][n])
    return rot, pl


def rig_matrix(state, d):
    r = state.get('rig', {})
    piv = Vector(r.get('piv', (0, 0, 0.9)))
    pitch = Matrix.Rotation(math.radians(r.get('pitch', 0)), 4, 'X')
    roll = Matrix.Rotation(math.radians(r.get('roll', 0)), 4, 'Y')
    yaw = Matrix.Rotation(math.radians(r.get('yaw', 0)), 4, 'Z')
    return (Matrix.Rotation(math.radians(45 * d), 4, 'Z') @ T((0, -r.get('fwd', 0), r.get('up', 0)))
            @ T(piv) @ pitch @ T(-piv) @ roll @ yaw @ Matrix.Diagonal((1.14, 1, 1, 1)))


SKIN_PARTS = ['L_Cabeza', 'L_Antebrazo.R', 'L_Antebrazo.L', 'L_Mano.R', 'L_Mano.L']


def set_bruma(k):
    """k 0..1: blends the skin ramp's two dark steps towards the Bruma violets."""
    m = bpy.data.materials['L_PielBruma']
    r = [n for n in m.node_tree.nodes if n.type == 'VALTORGB'][0].color_ramp
    # stronger Bruma reaches further up the ramp: 0.5 = shadows only, 1 = up to the mid tone
    r.elements[1].position = 0.16
    r.elements[2].position = 0.4 + 0.25 * max(0.0, k - 0.5) * 2
    target = 'L_PielBruma' if k > 0.01 else 'L_Piel'
    for n in SKIN_PARTS:
        o = bpy.data.objects[n]
        o.data.materials[0] = bpy.data.materials[target]


# ---- chains ----------------------------------------------------------------------------------------

CHAIN_LEN = {'R': 1.05, 'L': 1.05, 'pies': 0.42, 'suelo': 0.55}
LINK = 0.07


def _eye(name, off):
    return bpy.data.objects[name].matrix_world @ Vector(off)


def _ring(key):
    from_build = bpy.app.driver_namespace['LB']['RINGS'][key]
    return RIG.matrix_world @ (from_build - Vector((0, 0.02, 0.0)))


def _curve(a, b, L, n=24):
    D = (b - a).length
    sag = math.sqrt(max(0.0, 3 * D * (L - D) / 8)) if L > D else 0.0
    pts = []
    for i in range(n + 1):
        t = i / n
        p = a.lerp(b, t) + Vector((0, 0, -1)) * sag * 4 * t * (1 - t)
        p.z = max(p.z, 0.012)              # it lies on the floor rather than going through it
        pts.append(p)
    return pts


def _links(bm, pts):
    # walk the polyline placing links every LINK metres, turning each one 90 degrees
    seg = [(pts[i], pts[i + 1]) for i in range(len(pts) - 1)]
    total = sum((b - a).length for a, b in seg)
    k = 0
    s = LINK * 0.5
    while s < total:
        acc = 0.0
        for a, b in seg:
            l = (b - a).length
            if acc + l >= s:
                p = a.lerp(b, (s - acc) / max(l, 1e-6))
                t = (b - a).normalized()
                break
            acc += l
        up = Vector((0, 0, 1)) if abs(t.z) < 0.9 else Vector((1, 0, 0))
        n1 = t.cross(up).normalized()
        n2 = t.cross(n1).normalized()
        nrm = n1 if k % 2 == 0 else n2
        x = t
        z = nrm
        y = z.cross(x)
        M = Matrix((x, y, z)).transposed().to_4x4()
        M.translation = p
        bpy.app.driver_namespace['LB']['torus'](bm, 0.024, 0.0115, M, 8, 4, stretch=1.45)
        s += LINK
        k += 1


def build_chains(lengths=None):
    lengths = dict(CHAIN_LEN, **(lengths or {}))
    bpy.context.view_layer.update()
    o = bpy.data.objects.get('L_Cadenas')
    if o:
        me = o.data
        bpy.data.objects.remove(o, do_unlink=True)
        bpy.data.meshes.remove(me)
    bm = bmesh.new()
    wr = {s: _eye('L_Grillete.' + s, (0, 0, -0.068)) for s in 'RL'}
    an = {s: _eye('L_GrilleteP.' + s, (0, 0, -0.08)) for s in 'RL'}
    for s in 'RL':
        _links(bm, _curve(wr[s], _ring(s), lengths[s]))
    feet = _curve(an['R'], an['L'], lengths['pies'])
    _links(bm, feet)
    _links(bm, _curve(feet[len(feet) // 2], _ring('P'), lengths['suelo']))
    me = bpy.data.meshes.new('L_Cadenas')
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new('L_Cadenas', me)
    bpy.context.scene.collection.objects.link(ob)
    me.materials.append(bpy.data.materials['L_Cadena'])
    return ob


# ---- apply -----------------------------------------------------------------------------------------

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
    set_bruma(state.get('bruma', 0.0))
    bpy.context.view_layer.update()
    build_chains(state.get('chains'))
    return D


# ---- clips -----------------------------------------------------------------------------------------

SEAT = {'up': -0.77, 'fwd': -0.32}
LEGS = {'kn.R': (-84, 0, -14), 'an.R': (6, 0, 0), 'to.R': (25, 0, -10),
        'kn.L': (-126, 0, 9), 'an.L': (108, 0, 0), 'to.L': (-10, 0, 0)}
ARM_R = (-0.18, -0.26, 0.965)     # right wrist resting on the thigh
ARM_L = (0.075, -0.43, 1.12)      # left forearm draped over the raised knee


def seated(spine=(0, 0, 0, 0), headx=14, heady=0, headz=0, neckx=12, arms=None, breathe=0.0, bruma=0.0,
           roll=0.0, chains=None, hands=None):
    """spine: extra X on waist, chest, upchest and a Z twist. breathe: -1..1, the chest rises."""
    b = dict(LEGS)
    b['waist'] = (-14 + spine[0], roll * 0.5, spine[3] * 0.4)
    b['chest'] = (6 + spine[1] - 1.5 * breathe, roll * 0.3, spine[3] * 0.3)
    b['upchest'] = (4 + spine[2] - 1.0 * breathe, roll * 0.2, spine[3] * 0.3)
    b['neck'] = (neckx, 0, headz * 0.4)
    b['head'] = (headx, heady, headz * 0.6)
    if hands:
        b.update(hands)
    st = {'b': b, 'rig': dict(SEAT), 'bruma': bruma}
    a = arms or {}
    st['ikR'] = a.get('R', ARM_R)
    st['ikL'] = a.get('L', ARM_L)
    st['pl'] = (0, 0, 0.004 * breathe)
    if chains:
        st['chains'] = chains
    return st


def v(a, b, t):
    return tuple(x + (y - x) * t for x, y in zip(a, b))


# idle: slumped against the wall, head hanging, slow breathing (6 frames, pause at the ends)
IDLE = [seated(breathe=x) for x in (-1, -0.5, 0.4, 1, 0.5, -0.4)]

# mirar: hears someone and lifts his head to look at them (5 frames, hold the last one)
MIRAR = [seated(headx=h, neckx=n, headz=z, spine=(0, 0, s, 0)) for h, n, z, s in
         ((14, 12, 0, 0), (8, 9, 4, -1), (-2, 5, 8, -2), (-9, 3, 6, -3), (-11, 2, 5, -3))]

# brote: the Bruma surges up from inside him; he arches, yanks at the chains, folds and slumps again
_UP_R = (-0.36, -0.05, 1.42)
_UP_L = (0.36, -0.08, 1.45)
BROTE = [
    seated(breathe=0),
    seated(spine=(-4, -6, -4, 0), headx=-10, neckx=-2, bruma=0.3,
           arms={'R': v(ARM_R, _UP_R, 0.5), 'L': v(ARM_L, _UP_L, 0.4)}),
    seated(spine=(-8, -12, -8, 6), headx=-28, neckx=-10, headz=-12, bruma=0.8, roll=-6,
           arms={'R': _UP_R, 'L': _UP_L}),
    seated(spine=(-6, -10, -6, -8), headx=-20, neckx=-8, headz=14, bruma=1.0, roll=7,
           arms={'R': v(_UP_R, ARM_R, 0.2), 'L': v(_UP_L, ARM_L, 0.1)}),
    seated(spine=(10, 16, 10, 0), headx=38, neckx=18, bruma=1.0,
           arms={'R': (-0.12, -0.36, 1.02), 'L': (0.06, -0.36, 1.05)}),
    seated(spine=(12, 18, 12, 3), headx=40, neckx=20, headz=-6, bruma=0.8,
           arms={'R': (-0.11, -0.38, 1.0), 'L': (0.07, -0.37, 1.04)}),
    seated(spine=(4, 6, 4, 0), headx=30, neckx=15, bruma=0.45, breathe=1),
    seated(breathe=0.4, bruma=0.15),
]

CLIPS = {'idle': IDLE, 'mirar': MIRAR, 'brote': BROTE}
