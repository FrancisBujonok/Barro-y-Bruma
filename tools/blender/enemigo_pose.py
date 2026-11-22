# Barro y Bruma - shared poses and clips for the humanoid enemies built on the hero rig
# (infected, bloated, scavenger, crossbowman, fog ambusher, taken). Each enemy's build file stores its STYLE in
# the scene (custom property "enemy_style", JSON) and this module turns it into four clips with 8 views:
#   idle (4), walk (8), attack (8: 0 rest, 1-3 wind-up with 3 held, 4-5 the hit, 6-7 recovery, the same timing
#   map as the Rey Sapo's bite so Unity can follow EnemyProfile startup/active/recovery), death (6).
# Load: g = {}; exec(this, g); exec(hero_render.py, g).
import bpy, math, json
from mathutils import Matrix, Vector, Euler

RIG = bpy.data.objects['Rig']
ARM = RIG.data
REST = {b.name: b.matrix_local.copy() for b in ARM.bones}
PARENT = {b.name: (b.parent.name if b.parent else None) for b in ARM.bones}
LENGTH = {b.name: b.length for b in ARM.bones}
STYLE = json.loads(bpy.context.scene.get('enemy_style', '{}'))
SCALE = STYLE.get('scale', 1.0)
SX = STYLE.get('sx', 1.14)            # horizontal stretch of the sprite rig, 1.14 like the hero


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
    piv = Vector(r.get('piv', (0, -0.1, 0.12)))
    pitch = Matrix.Rotation(math.radians(r.get('pitch', 0)), 4, 'X')
    roll = Matrix.Rotation(math.radians(r.get('roll', 0)), 4, 'Y')
    return (Matrix.Rotation(math.radians(45 * d), 4, 'Z') @ T((0, -r.get('fwd', 0), r.get('up', 0)))
            @ T(piv) @ pitch @ roll @ T(-piv) @ Matrix.Diagonal((SX * SCALE, SCALE, SCALE * STYLE.get('sz', 1.0), 1)))


HOOKS = []          # per-enemy extras run after every pose: f(state, D)


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
    for f in HOOKS:
        f(state, D)
    bpy.context.view_layer.update()
    return D


# ---- building blocks ---------------------------------------------------------------------------------
def body(hunch=None, headx=0.0, heady=None, headz=0.0, lean=0.0, roll=0.0, breathe=0.0, legs=None, arms=None):
    """hunch: forward bend of the back (STYLE default). heady: sideways tilt of the head (the taken)."""
    h = STYLE.get('hunch', 0.0) if hunch is None else hunch
    ty = STYLE.get('head_tilt', 0.0) if heady is None else heady
    b = {'waist': (h * 0.4 + lean, roll * 0.5, 0), 'chest': (h * 0.4 + lean * 0.5 - 1.5 * breathe, roll * 0.3, 0),
         'upchest': (h * 0.2 - breathe, 0, 0), 'neck': (-h * 0.5, 0, headz * 0.4),
         'head': (-h * 0.3 + headx, ty, headz * 0.6),
         'kn.R': (-3, 0, -5), 'an.R': (4, 0, 0), 'kn.L': (-3, 0, 5), 'an.L': (4, 0, 0)}
    b.update(legs or {})
    b.update(arms if arms is not None else ARMS_REST())
    return b


def ARMS_REST(k=0.0):
    """Arm carriage by style: hang (default), forward (the infected reach), wide (the bloated), long (ambusher)."""
    a = STYLE.get('arms', 'hang')
    if a == 'forward':
        return {'el.R': (-38 - 6 * k, 0, -6), 'wr.R': (-22, 0, 0), 'el.L': (-34 + 6 * k, 0, 6), 'wr.L': (-26, 0, 0)}
    if a == 'wide':
        return {'el.R': (-8, 0, -22), 'wr.R': (-14, 0, 0), 'el.L': (-8, 0, 22), 'wr.L': (-14, 0, 0)}
    if a == 'long':
        return {'el.R': (-12 - 4 * k, 0, -8), 'wr.R': (-6, 0, 0), 'el.L': (-12 + 4 * k, 0, 8), 'wr.L': (-6, 0, 0)}
    return {'el.R': (-4 - 6 * k, 0, -4), 'wr.R': (-8, 0, 0), 'el.L': (-4 + 6 * k, 0, 4), 'wr.L': (-8, 0, 0)}


def st(b, pl=(0, 0, 0), **extra):
    s = {'b': b, 'pl': pl}
    s.update(extra)
    return s


def idle():
    tw = STYLE.get('twitch', 0.0)
    out = []
    for i, br in enumerate((-1, 0, 1, 0)):
        twitch = tw if i == 2 else 0.0
        out.append(st(body(breathe=br, headz=twitch * 14, roll=STYLE.get('sway', 2) * (1 if i < 2 else -1),
                           arms=ARMS_REST(br * 0.5)), pl=(0, 0, 0.004 * br)))
    return out


def walk(n=8):
    g = STYLE.get('gait', {})
    stride, knee, bob = g.get('stride', 18), g.get('knee', 28), g.get('bob', 0.012)
    limp, sway, swing = g.get('limp', 0.0), g.get('sway', 2.0), g.get('swing', 12)
    out = []
    for i in range(n):
        p = i / n
        legs = {}
        for side, ph in (('R', p), ('L', (p + 0.5) % 1.0)):
            amp = stride * (1 - limp) if side == 'L' else stride
            fwd = amp * math.cos(2 * math.pi * ph)
            bend = 6 + knee * (1 - 0.6 * limp * (side == 'L')) * max(0.0, math.sin(2 * math.pi * ph))
            legs['kn.' + side] = (-fwd, 0, -5 if side == 'R' else 5)
            legs['an.' + side] = (bend, 0, 0)
        on_left = max(0.0, -math.cos(2 * math.pi * p))
        z = bob * math.cos(4 * math.pi * p) - 0.008 - 0.03 * limp * on_left
        roll = sway * math.cos(2 * math.pi * p) + 5 * limp * on_left
        k = math.cos(2 * math.pi * p)
        arms = ARMS_REST(k)
        if STYLE.get('arms', 'hang') == 'hang':
            arms['el.R'] = (-swing * k, 0, -4)
            arms['el.L'] = (swing * k, 0, 4)
        out.append(st(body(lean=g.get('lean', 3), roll=roll, headz=-roll * 0.6, legs=legs, arms=arms),
                      pl=(0.02 * limp * on_left, 0, z)))
    return out


def attack():
    kind = STYLE.get('attack', 'grab')
    A = ARMS_REST()
    def fr(lean, arms, headx=0.0, legs=None, pl=(0, 0, 0), **x):
        return st(body(lean=lean, headx=headx, arms=arms, legs=legs), pl=pl, **x)
    legs_lunge = {'kn.R': (-28, 0, -5), 'an.R': (30, 0, 0), 'kn.L': (12, 0, 5), 'an.L': (8, 0, 0)}
    if kind == 'grab':          # both arms up, lunge and close on the hero
        up = {'el.R': (-120, 0, -20), 'wr.R': (-30, 0, 0), 'el.L': (-120, 0, 20), 'wr.L': (-30, 0, 0)}
        reach = {'el.R': (-85, 0, 8), 'wr.R': (-5, 0, 0), 'el.L': (-85, 0, -8), 'wr.L': (-5, 0, 0)}
        close = {'el.R': (-70, 0, 22), 'wr.R': (-40, 0, 0), 'el.L': (-70, 0, -22), 'wr.L': (-40, 0, 0)}
        return [fr(0, A), fr(-6, up, -6), fr(-10, up, -8), fr(-12, up, -10),
                fr(18, reach, 6, legs_lunge, (0, -0.08, -0.03)), fr(22, close, 8, legs_lunge, (0, -0.1, -0.04)),
                fr(10, reach, 3), fr(4, A)]
    if kind == 'slam':          # both fists overhead, smashed down in front
        up = {'el.R': (-165, 0, -12), 'wr.R': (-20, 0, 0), 'el.L': (-165, 0, 12), 'wr.L': (-20, 0, 0)}
        down = {'el.R': (-35, 0, 10), 'wr.R': (-10, 0, 0), 'el.L': (-35, 0, -10), 'wr.L': (-10, 0, 0)}
        squat = {'kn.R': (-30, 0, -8), 'an.R': (40, 0, 0), 'kn.L': (-30, 0, 8), 'an.L': (40, 0, 0)}
        return [fr(0, A), fr(-8, up, -10), fr(-12, up, -12), fr(-14, up, -14),
                fr(30, down, 14, squat, (0, -0.04, -0.12)), fr(34, down, 16, squat, (0, -0.05, -0.14)),
                fr(14, A, 6, None, (0, 0, -0.04)), fr(4, A)]
    if kind == 'swing':         # one-handed overhead blow with the weapon in the right hand
        up = dict(A, **{'el.R': (-170, 0, -25), 'wr.R': (-60, 0, 0), 'el.L': (-40, 0, 20)})
        hit = dict(A, **{'el.R': (-55, 0, 25), 'wr.R': (10, 0, 0), 'el.L': (-10, 0, 10)})
        return [fr(0, A), fr(-6, up, -4), fr(-10, up, -6), fr(-12, up, -6),
                fr(20, hit, 8, legs_lunge, (0, -0.08, -0.03)), fr(24, hit, 10, legs_lunge, (0, -0.1, -0.04)),
                fr(10, A, 4), fr(3, A)]
    if kind == 'shoot':         # the crossbow comes up, aims (held), kicks, comes down
        aim = {'ikR': (-0.06, -0.42, 1.38), 'ikL': (0.06, -0.62, 1.36)}
        kick = {'ikR': (-0.06, -0.38, 1.42), 'ikL': (0.06, -0.58, 1.42)}
        low = {'ikR': (-0.12, -0.25, 1.05), 'ikL': (0.08, -0.4, 1.08)}
        hold = {'el.R': (0, 0, 0)}
        return [st(body(), **low), st(body(lean=-2), **low), st(body(lean=-4, headx=6), **aim),
                st(body(lean=-4, headx=8), **aim), st(body(lean=-10, headx=2), **kick), st(body(lean=-6, headx=6), **aim),
                st(body(lean=-2), **low), st(body(), **low)]
    if kind == 'hook':          # the long right arm sweeps its hooks across in front
        back = dict(A, **{'el.R': (-60, 0, -95), 'wr.R': (-20, 0, 0)})
        across = dict(A, **{'el.R': (-75, 0, 70), 'wr.R': (-30, 0, 0)})
        return [fr(0, A), fr(-4, back, 0), fr(-6, back, -4), fr(-8, back, -6),
                fr(22, across, 8, legs_lunge, (0, -0.1, -0.04)), fr(26, across, 10, legs_lunge, (0, -0.12, -0.05)),
                fr(10, A, 4), fr(2, A)]
    # 'lunge': one hand flies at the hero's face, the head comes forward with it
    reach = dict(A, **{'el.R': (-95, 0, 6), 'wr.R': (-8, 0, 0)})
    cock = dict(A, **{'el.R': (-30, 0, -30), 'wr.R': (-90, 0, 0)})
    return [fr(0, A), fr(-4, cock, -4), fr(-8, cock, -8), fr(-10, cock, -10),
            fr(26, reach, 12, legs_lunge, (0, -0.12, -0.04)), fr(28, reach, 14, legs_lunge, (0, -0.13, -0.05)),
            fr(12, A, 5), fr(4, A)]


def death():
    limp = {'el.R': (10, 0, -30), 'wr.R': (-10, 0, 0), 'el.L': (10, 0, 30), 'wr.L': (-10, 0, 0)}
    buckle = {'kn.R': (-35, 0, -8), 'an.R': (60, 0, 0), 'kn.L': (-25, 0, 8), 'an.L': (50, 0, 0)}
    flat = {'kn.R': (0, 0, -8), 'an.R': (8, 0, 0), 'kn.L': (-6, 0, 8), 'an.L': (14, 0, 0)}
    spread = {'el.R': (-150, 0, -30), 'wr.R': (0, 0, 0), 'el.L': (-140, 0, 40), 'wr.L': (0, 0, 0)}
    return [
        st(body(lean=-14, headx=-20, arms=limp)),
        st(body(lean=4, headx=10, arms=limp, legs=buckle), pl=(0, 0, -0.16)),
        st(body(lean=8, headx=12, arms=limp, legs=buckle), pl=(0, 0, -0.2), rig={'pitch': 30}),
        st(body(lean=6, headx=10, arms=spread, legs=flat), pl=(0, 0, -0.08), rig={'pitch': 64}),
        st(body(lean=0, headx=-8, arms=spread, legs=flat), rig={'pitch': 90, 'up': -0.02}),
        st(body(lean=0, headx=-10, heady=20, arms=spread, legs=flat), rig={'pitch': 88, 'up': -0.03}),
    ]


def clips():
    c = {'idle': idle(), 'walk': walk(), 'attack': attack(), 'death': death()}
    for name, frames in c.items():
        for i, s in enumerate(frames):
            s['phase'] = i / len(frames)        # for pulsing props (the bloated one's sac)
            s['clip'] = name
    return c


CLIPS = clips()
