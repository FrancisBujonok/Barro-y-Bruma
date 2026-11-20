# Barro y Bruma - the hero's animation clips as lists of poses (see hero_pose.py for the format).
# Needs hero_pose.py loaded first (uses sheath_grip and the scabbard axis).
import math
from mathutils import Vector


def M(*dicts):
    """Merge poses: later bone rotations add to earlier ones; other keys replace."""
    out = {'b': {}}
    for d in dicts:
        for k, v in d.items():
            if k == 'b':
                for bn, r in v.items():
                    o = out['b'].get(bn, (0, 0, 0))
                    out['b'][bn] = (o[0] + r[0], o[1] + r[1], o[2] + r[2])
            elif k == 'rig':
                rr = dict(out.get('rig', {}))
                rr.update(v)
                out['rig'] = rr
            else:
                out[k] = v
    return out


def lerp(a, b, t):
    return tuple(x + (y - x) * t for x, y in zip(a, b))


def nrm(v):
    l = math.sqrt(sum(x * x for x in v)) or 1.0
    return tuple(x / l for x in v)


# ---- stances ---------------------------------------------------------------------------------------

KNEES = {'b': {'kn.R': (-8, 0, 0), 'an.R': (14, 0, 0), 'to.R': (-6, 0, 0),
               'kn.L': (-8, 0, 0), 'an.L': (14, 0, 0), 'to.L': (-6, 0, 0)}, 'pl': (0, 0, -0.02)}
GUARD_LEGS = {'b': {'kn.R': (-20, 4, 0), 'an.R': (22, 0, 0), 'to.R': (-2, 0, 0),
                    'kn.L': (12, -6, 0), 'an.L': (10, 0, 0), 'to.L': (-14, 0, 0)}, 'pl': (0, 0, -0.04)}
LEFT_RELAXED = {'b': {'el.L': (-6, -6, 0), 'wr.L': (-14, 0, 0)}}
RIGHT_RELAXED = {'b': {'el.R': (-6, 6, 0), 'wr.R': (-14, 0, 0)}}
READY_SWORD = {'ikR': (-0.27, -0.16, 1.02), 'sw': ((-0.22, -0.62, -0.75), (-0.9, 0.3, 0))}

SCAB_MOUTH = Vector((0.185, -0.06, 0.985))
SCAB_AXIS = Vector((0.10, 0.36, -0.93)).normalized()


def breathe(i, n):
    s = math.sin(2 * math.pi * i / n)
    return {'b': {'chest': (1.5 * s, 0, 0), 'neck': (-1.5 * s, 0, 0), 'sh.R': (0, 0, 0), 'sh.L': (0, 0, 0)},
            'pl': (0, 0, -0.02 - 0.008 * (1 + s) / 2)}


def idle(armed=True, n=4):
    out = []
    for i in range(n):
        p = M(KNEES, LEFT_RELAXED, breathe(i, n))
        if armed:
            p = M(p, READY_SWORD, {'b': {'chest': (0, 0, 4)}})
        else:
            p = M(p, RIGHT_RELAXED, {'sword': 'sheath'})
        out.append(p)
    return out


def gait(i, n, stride, knee, bob, lean, arm, armed, run=False):
    ph = 2 * math.pi * i / n
    s, c = math.sin(ph), math.cos(ph)
    # right leg forward when s > 0
    lift_r = max(0.0, -math.sin(ph + 0.6))   # knee lift while swinging forward
    lift_l = max(0.0, math.sin(ph + 0.6))
    b = {
        'kn.R': (-stride * s - knee * lift_r * 0.5, 0, 0), 'an.R': (knee * lift_r + 6, 0, 0),
        'kn.L': (stride * s - knee * lift_l * 0.5, 0, 0), 'an.L': (knee * lift_l + 6, 0, 0),
        'to.R': (-8 * lift_r, 0, 0), 'to.L': (-8 * lift_l, 0, 0),
        'waist': (lean, 0, 4 * s), 'chest': (lean * 0.5, 0, -6 * s), 'neck': (-lean * 1.2, 0, 2 * s),
        'el.L': (-arm * s - (10 if run else 0), -5, 0), 'wr.L': (-(arm * 0.5) * max(0, -s) - (40 if run else 12), 0, 0),
    }
    p = {'b': b, 'pl': (0, 0, -0.025 - bob * abs(c))}
    if armed:
        if run:
            hand = (-0.28, 0.06 + 0.06 * s, 0.95 + 0.02 * c)
            p = M(p, {'ikR': hand, 'sw': ((-0.25, 0.72, -0.65), (-0.95, -0.2, 0))})
        else:
            hand = (-0.27, -0.14 + 0.04 * s, 1.0 + 0.01 * c)
            p = M(p, {'ikR': hand, 'sw': ((-0.22, -0.66, -0.72), (-0.9, 0.3, 0))})
    else:
        p = M(p, {'b': {'el.R': (arm * s - (10 if run else 0), 5, 0),
                         'wr.R': (-(arm * 0.5) * max(0, s) - (40 if run else 12), 0, 0)},
                  'sword': 'sheath'})
    return p


def walk(armed=True, n=8):
    return [M(gait(i, n, 24, 34, 0.018, 3, 18, armed), {'cloak': (6 + 3 * math.sin(4 * math.pi * i / n), 0, 0)})
            for i in range(n)]


def sprint(armed=True, n=6):
    return [M(gait(i, n, 44, 70, 0.04, 16, 42, armed, run=True),
              {'rig': {'up': 0.02}, 'cloak': (30 + 6 * math.sin(2 * math.pi * i / n), 0, 4 * math.sin(2 * math.pi * i / n))})
            for i in range(n)]


def roll(armed=True):
    tuck = {'b': {'kn.R': (-95, 0, 0), 'an.R': (120, 0, 0), 'kn.L': (-85, 0, 0), 'an.L': (115, 0, 0),
                  'waist': (35, 0, 0), 'chest': (25, 0, 0), 'neck': (20, 0, 0),
                  'el.L': (-60, -10, 0), 'wr.L': (-70, 0, 0)}}
    crouch = {'b': {'kn.R': (-55, 0, 0), 'an.R': (80, 0, 0), 'kn.L': (-30, 0, 0), 'an.L': (70, 0, 0),
                    'waist': (25, 0, 0), 'chest': (10, 0, 0), 'neck': (-10, 0, 0),
                    'el.L': (-40, -10, 0), 'wr.L': (-40, 0, 0)}, 'pl': (0, 0, -0.18)}
    if armed:
        tuck = M(tuck, {'ikR': (-0.24, -0.30, 0.80), 'sw': ((-0.25, 0.95, -0.15), (0, 0, 1))})
        crouch = M(crouch, {'ikR': (-0.28, -0.22, 0.80), 'sw': ((-0.15, 0.25, -0.95), (-1, 0, 0))})
    else:
        tuck = M(tuck, {'b': {'el.R': (-60, 10, 0), 'wr.R': (-70, 0, 0)}, 'sword': 'sheath'})
        crouch = M(crouch, {'b': {'el.R': (-30, 15, 0), 'wr.R': (-30, 0, 0)}, 'sword': 'sheath'})
    piv = (0, -0.12, 0.66)
    tk = {'pl': (0, 0, -0.35), 'cloak': (-55, 0, 0)}
    frames = [
        M(crouch, {'rig': {'pitch': 10, 'up': 0.0}, 'cloak': (-10, 0, 0)}),
        M(tuck, tk, {'rig': {'pitch': 75, 'piv': piv, 'up': -0.16}}),
        M(tuck, tk, {'rig': {'pitch': 155, 'piv': piv, 'up': -0.20}}),
        M(tuck, tk, {'rig': {'pitch': 235, 'piv': piv, 'up': -0.20}}),
        M(tuck, tk, {'rig': {'pitch': 310, 'piv': piv, 'up': -0.16}}),
        M(crouch, {'rig': {'pitch': 15}, 'cloak': (-15, 0, 0)}),
        M(KNEES, {'b': {'waist': (10, 0, 0), 'kn.R': (-15, 0, 0), 'an.R': (25, 0, 0)}, 'pl': (0, 0, -0.06)},
          {'b': {'el.R': (-10, 8, 0), 'wr.R': (-20, 0, 0), 'el.L': (-10, -8, 0)}} if not armed else READY_SWORD,
          {'sword': 'sheath'} if not armed else {}),
    ]
    return frames


def hurt(armed=True):
    base = M(KNEES, LEFT_RELAXED, READY_SWORD if armed else M(RIGHT_RELAXED, {'sword': 'sheath'}))
    hit = {'b': {'waist': (-14, 0, -6), 'chest': (-12, 0, -4), 'neck': (-18, 0, 0), 'head': (-10, 6, 0),
                 'el.L': (-30, -35, 0), 'wr.L': (-30, 0, 0),
                 'kn.R': (10, 0, 0), 'an.R': (10, 0, 0), 'kn.L': (-22, 0, 0), 'an.L': (30, 0, 0)},
           'pl': (0, 0.06, -0.03)}
    if armed:
        hit_sw = {'ikR': (-0.33, 0.02, 1.12), 'sw': ((-0.5, -0.3, -0.8), (-0.8, 0.5, 0))}
    else:
        hit_sw = {'b': {'el.R': (-30, 35, 0), 'wr.R': (-30, 0, 0)}}
    f1 = M(base, hit, hit_sw)
    f2 = M(base, {'b': {k: (v[0] * 0.75, v[1] * 0.75, v[2] * 0.75) for k, v in hit['b'].items()}, 'pl': (0, 0.07, -0.05)},
           hit_sw, {'b': {'waist': (8, 0, 0), 'neck': (6, 0, 0)}})
    f3 = M(base, {'b': {'waist': (6, 0, 0), 'chest': (4, 0, 0), 'kn.L': (-10, 0, 0), 'an.L': (14, 0, 0)},
                  'pl': (0, 0.03, -0.04)})
    return [f1, f2, f3]


def death():
    piv = (0, -0.38, 0.06)
    st = []
    st.append(M(KNEES, {'b': {'waist': (-16, 0, -8), 'chest': (-10, 0, 0), 'neck': (-20, 0, 0),
                              'el.L': (-25, -30, 0), 'el.R': (-20, 25, 0)}, 'pl': (0, 0.05, -0.03)},
                {'ikR': (-0.36, 0.0, 1.05), 'sw': ((-0.6, -0.2, -0.75), (-0.8, 0.5, 0))}))
    st.append(M({'b': {'kn.R': (-40, 0, 0), 'an.R': (75, 0, 0), 'kn.L': (-35, 0, 0), 'an.L': (70, 0, 0),
                       'waist': (10, 0, 0), 'neck': (15, 0, 0), 'el.L': (-5, -10, 0), 'el.R': (-5, 10, 0)},
                 'pl': (0, 0, -0.16)}, {'ikR': (-0.33, -0.1, 0.78), 'sw': ((-0.3, -0.5, -0.8), (-0.9, 0.3, 0))}))
    knees = {'b': {'kn.R': (-85, 0, 0), 'an.R': (175, 0, 0), 'to.R': (-30, 0, 0),
                   'kn.L': (-80, 0, 0), 'an.L': (170, 0, 0), 'to.L': (-30, 0, 0),
                   'waist': (25, 0, 0), 'chest': (10, 0, 0), 'neck': (20, 0, 0),
                   'el.L': (5, -12, 0), 'el.R': (5, 12, 0)}, 'pl': (0, 0, -0.42)}
    st.append(M(knees, {'ikR': (-0.33, -0.25, 0.42), 'sw': ((-0.4, -0.6, -0.65), (-0.9, 0.3, 0))}))
    st.append(M(knees, {'b': {'waist': (10, 0, 0)}, 'rig': {'pitch': 35, 'piv': (0, -0.2, 0.06), 'fwd': -0.2}, 'cloak': (-35, 0, 0)},
                {'b': {'el.R': (-40, 20, 0), 'el.L': (-40, -20, 0)}}))
    st.append(M(knees, {'b': {'kn.R': (40, 0, 0), 'kn.L': (40, 0, 0), 'an.R': (-80, 0, 0), 'an.L': (-80, 0, 0),
                              'el.R': (-80, 30, 0), 'el.L': (-80, -30, 0)},
                        'rig': {'pitch': 70, 'piv': piv, 'fwd': -0.4}, 'cloak': (-45, 0, 0)}))
    st.append(M({'b': {'kn.R': (0, 4, 0), 'an.R': (5, 0, 0), 'kn.L': (-6, -6, 0), 'an.L': (20, 0, 0),
                       'el.R': (-150, 40, 0), 'wr.R': (-20, 0, 0), 'el.L': (-120, -50, 0), 'wr.L': (-10, 0, 0),
                       'neck': (-20, 0, 15), 'head': (-10, 0, 20)},
                 'rig': {'pitch': 90, 'piv': piv, 'up': -0.04, 'fwd': -0.85}, 'cloak': (-10, 0, 0)}))
    return st


def sheathe():
    """Ready -> sheathed (draw is this list reversed)."""
    hand_over = tuple(SCAB_MOUTH - SCAB_AXIS * 0.46)
    look = {'b': {'neck': (12, 0, 14), 'chest': (4, 0, 10)}}
    st = []
    st.append(M(KNEES, LEFT_RELAXED, READY_SWORD))
    st.append(M(KNEES, look, {'b': {'el.L': (-10, -12, 0)}},
                {'ikR': (0.02, -0.26, 1.15), 'sw': (tuple(SCAB_AXIS * 0.7 + Vector((0, -0.4, 0.2))), (0, 0, 1))}))
    st.append(M(KNEES, look, {'b': {'el.L': (-12, -14, 0)}},
                {'ikR': hand_over, 'sw': (tuple(SCAB_AXIS), (1, 0, 0))}))
    st.append(M(KNEES, look, {'b': {'el.L': (-12, -14, 0)}}, {'sword': 0.55, 'ikR': 'grip'}))
    st.append(M(KNEES, look, {'b': {'el.L': (-8, -10, 0)}}, {'sword': 'sheath', 'ikR': 'grip'}))
    st.append(M(KNEES, LEFT_RELAXED, RIGHT_RELAXED, {'sword': 'sheath'}))
    return st


# ---- attacks ---------------------------------------------------------------------------------------
# Blade directions in armature space: -Y is in front of him, -X his right, +X his left.

def swing(stance, keys):
    """keys: list of (hand_target, blade_dir, edge_dir, extra_pose)."""
    out = []
    for hand, blade, edge, extra in keys:
        p = M(stance, {'ikR': hand, 'sw': (nrm(blade), edge)}, extra)
        out.append(p)
    return out


def light1():
    # wide slash from his right to his left at chest height
    st = M(GUARD_LEGS, LEFT_RELAXED)
    return swing(st, [
        ((-0.30, -0.08, 1.12), (-0.85, 0.35, 0.2), (0, 0, 1), {'b': {'waist': (2, 0, -14), 'chest': (0, 0, -16), 'el.L': (-20, -10, 0)}}),
        ((-0.36, 0.06, 1.18), (-0.75, 0.6, 0.15), (0, 0, 1), {'b': {'waist': (0, 0, -22), 'chest': (0, 0, -24), 'el.L': (-30, -15, 0)}}),
        ((-0.10, -0.42, 1.14), (-0.15, -1.0, 0.0), (0, 0, 1), {'b': {'waist': (8, 0, 6), 'chest': (6, 0, 8), 'el.L': (-15, -25, 0)}, 'pl': (0, -0.04, -0.05)}),
        ((0.20, -0.30, 1.08), (0.95, -0.25, -0.15), (0, 0, 1), {'b': {'waist': (10, 0, 22), 'chest': (6, 0, 24), 'el.L': (10, -40, 0)}, 'pl': (0, -0.05, -0.05)}),
        ((0.12, -0.22, 1.02), (0.7, -0.45, -0.55), (0, 0, 1), {'b': {'waist': (6, 0, 16), 'chest': (4, 0, 14), 'el.L': (0, -25, 0)}, 'pl': (0, -0.04, -0.05)}),
    ])


def light2():
    # rising backhand from his low left to his high right
    st = M(GUARD_LEGS, LEFT_RELAXED)
    return swing(st, [
        ((0.14, -0.20, 0.98), (0.7, -0.3, -0.6), (0, 0, 1), {'b': {'waist': (8, 0, 18), 'chest': (6, 0, 20)}, 'pl': (0, -0.04, -0.05)}),
        ((0.18, -0.08, 0.90), (0.75, 0.25, -0.6), (0, 0, -1), {'b': {'waist': (14, 0, 26), 'chest': (8, 0, 28), 'el.L': (10, -30, 0)}, 'pl': (0, -0.03, -0.07)}),
        ((-0.06, -0.42, 1.10), (-0.1, -0.9, 0.42), (0, 0, -1), {'b': {'waist': (4, 0, 0), 'chest': (0, 0, -4), 'el.L': (-10, -30, 0)}, 'pl': (0, -0.05, -0.04)}),
        ((-0.32, -0.20, 1.40), (-0.65, -0.2, 0.75), (0, 0, -1), {'b': {'waist': (-6, 0, -22), 'chest': (-8, 0, -24), 'el.L': (-30, -15, 0)}, 'pl': (0, -0.04, -0.02)}),
        ((-0.30, -0.16, 1.22), (-0.5, -0.5, 0.2), (0, 0, -1), {'b': {'waist': (0, 0, -14), 'chest': (0, 0, -14), 'el.L': (-20, -10, 0)}, 'pl': (0, -0.03, -0.03)}),
    ])


def light3():
    # pull back and lunge into a thrust
    lunge_legs = {'b': {'kn.R': (-50, 0, 0), 'an.R': (55, 0, 0), 'to.R': (0, 0, 0),
                        'kn.L': (30, -4, 0), 'an.L': (6, 0, 0), 'to.L': (-25, 0, 0)}, 'pl': (0, -0.10, -0.10)}
    st = M(LEFT_RELAXED)
    return [
        M(GUARD_LEGS, st, {'ikR': (-0.30, 0.10, 1.10), 'sw': (nrm((-0.05, -1.0, 0.05)), (1, 0, 0)),
                           'b': {'waist': (-4, 0, -16), 'chest': (-2, 0, -14), 'el.L': (-35, -20, 0), 'wr.L': (-40, 0, 0)}}),
        M(GUARD_LEGS, st, {'ikR': (-0.32, 0.18, 1.14), 'sw': (nrm((-0.05, -1.0, 0.08)), (1, 0, 0)),
                           'b': {'waist': (-6, 0, -22), 'chest': (-4, 0, -20), 'el.L': (-50, -25, 0), 'wr.L': (-50, 0, 0)}, 'pl': (0, 0.03, -0.07)}),
        M(lunge_legs, st, {'ikR': (-0.16, -0.40, 1.16), 'sw': (nrm((0.0, -1.0, 0.0)), (1, 0, 0)),
                           'b': {'waist': (14, 0, 8), 'chest': (8, 0, 10), 'el.L': (30, -20, 0)}}),
        M(lunge_legs, st, {'ikR': (-0.08, -0.58, 1.18), 'sw': (nrm((0.0, -1.0, 0.0)), (1, 0, 0)),
                           'b': {'waist': (18, 0, 14), 'chest': (10, 0, 14), 'el.L': (45, -25, 0)}, 'pl': (0, -0.13, -0.12)}),
        M(lunge_legs, st, {'ikR': (-0.12, -0.50, 1.14), 'sw': (nrm((0.0, -1.0, -0.1)), (1, 0, 0)),
                           'b': {'waist': (16, 0, 12), 'chest': (8, 0, 12), 'el.L': (35, -22, 0)}, 'pl': (0, -0.12, -0.12)}),
        M(GUARD_LEGS, st, READY_SWORD, {'b': {'waist': (6, 0, 2)}, 'pl': (0, -0.05, -0.05)}),
    ]


def heavy1():
    # two hands: raise overhead, chop down in front
    st = {'gripL': True}
    return [
        M(GUARD_LEGS, st, {'ikR': (-0.18, -0.22, 1.20), 'sw': (nrm((-0.2, -0.6, 0.75)), (-1, 0, 0)), 'b': {'waist': (-4, 0, -6)}}),
        M(GUARD_LEGS, st, {'ikR': (-0.10, -0.05, 1.62), 'sw': (nrm((0.0, 0.55, 0.85)), (-1, 0, 0)), 'b': {'waist': (-10, 0, -6), 'chest': (-8, 0, 0), 'neck': (6, 0, 0)}, 'pl': (0, 0.02, -0.02)}),
        M(GUARD_LEGS, st, {'ikR': (-0.08, -0.02, 1.68), 'sw': (nrm((0.0, 0.85, 0.5)), (-1, 0, 0)), 'b': {'waist': (-14, 0, -8), 'chest': (-10, 0, 0), 'neck': (8, 0, 0)}, 'pl': (0, 0.03, -0.02)}),
        M(GUARD_LEGS, st, {'ikR': (-0.08, -0.45, 1.42), 'sw': (nrm((0.0, -0.75, 0.66)), (-1, 0, 0)), 'b': {'waist': (10, 0, 0), 'chest': (6, 0, 0)}, 'pl': (0, -0.05, -0.07)}),
        M(GUARD_LEGS, st, {'ikR': (-0.08, -0.50, 0.95), 'sw': (nrm((0.0, -0.6, -0.8)), (-1, 0, 0)), 'b': {'waist': (28, 0, 0), 'chest': (14, 0, 0), 'neck': (-14, 0, 0)}, 'pl': (0, -0.08, -0.13)}),
        M(GUARD_LEGS, st, {'ikR': (-0.12, -0.36, 1.00), 'sw': (nrm((-0.1, -0.7, -0.7)), (-1, 0, 0)), 'b': {'waist': (16, 0, 0), 'chest': (8, 0, 0)}, 'pl': (0, -0.05, -0.08)}),
    ]


def heavy2():
    # two hands: wind up over his left shoulder, sweep wide to his right
    st = {'gripL': True}
    return [
        M(GUARD_LEGS, st, {'ikR': (0.10, -0.25, 1.25), 'sw': (nrm((0.5, -0.3, 0.8)), (0, -1, 0)), 'b': {'waist': (0, 0, 16), 'chest': (0, 0, 18)}}),
        M(GUARD_LEGS, st, {'ikR': (0.22, -0.02, 1.40), 'sw': (nrm((0.6, 0.6, 0.5)), (0, -1, 0)), 'b': {'waist': (-4, 0, 30), 'chest': (-4, 0, 32), 'neck': (0, 0, -20)}, 'pl': (0, 0.02, -0.05)}),
        M(GUARD_LEGS, st, {'ikR': (0.10, -0.45, 1.18), 'sw': (nrm((0.75, -0.65, 0.05)), (0, 0, 1)), 'b': {'waist': (6, 0, 14), 'chest': (4, 0, 14)}, 'pl': (0, -0.04, -0.08)}),
        M(GUARD_LEGS, st, {'ikR': (-0.12, -0.50, 1.12), 'sw': (nrm((-0.2, -1.0, -0.05)), (0, 0, 1)), 'b': {'waist': (8, 0, -6), 'chest': (6, 0, -8)}, 'pl': (0, -0.06, -0.09)}),
        M(GUARD_LEGS, st, {'ikR': (-0.40, -0.25, 1.08), 'sw': (nrm((-0.95, 0.15, -0.2)), (0, 0, 1)), 'b': {'waist': (8, 0, -30), 'chest': (4, 0, -32), 'neck': (0, 0, 18)}, 'pl': (0, -0.05, -0.09)}),
        M(GUARD_LEGS, st, {'ikR': (-0.34, -0.18, 1.02), 'sw': (nrm((-0.7, -0.3, -0.6)), (0, 0, 1)), 'b': {'waist': (4, 0, -20), 'chest': (2, 0, -20)}, 'pl': (0, -0.04, -0.07)}),
    ]


def heavy3():
    # leap, sword high, slam it into the ground and stay low
    st = {'gripL': True}
    crouch = {'b': {'kn.R': (-50, 0, 0), 'an.R': (80, 0, 0), 'kn.L': (-30, 0, 0), 'an.L': (70, 0, 0)}, 'pl': (0, 0, -0.18)}
    air = {'b': {'kn.R': (-70, 0, 0), 'an.R': (90, 0, 0), 'kn.L': (-20, 0, 0), 'an.L': (60, 0, 0), 'to.L': (20, 0, 0)}}
    slam = {'b': {'kn.R': (-75, 0, 0), 'an.R': (85, 0, 0), 'kn.L': (40, 0, 0), 'an.L': (60, 0, 0), 'to.L': (-30, 0, 0)}, 'pl': (0, -0.10, -0.30)}
    return [
        M(crouch, st, {'ikR': (-0.12, -0.30, 1.02), 'sw': (nrm((-0.2, -0.3, 0.9)), (-1, 0, 0)), 'b': {'waist': (16, 0, 0)}}),
        M(air, st, {'ikR': (-0.08, -0.08, 1.66), 'sw': (nrm((0, 0.4, 0.92)), (-1, 0, 0)), 'b': {'waist': (-12, 0, 0), 'chest': (-8, 0, 0)}, 'rig': {'up': 0.22}}),
        M(air, st, {'ikR': (-0.08, 0.02, 1.70), 'sw': (nrm((0, 0.9, 0.42)), (-1, 0, 0)), 'b': {'waist': (-16, 0, 0), 'chest': (-12, 0, 0), 'neck': (10, 0, 0)}, 'rig': {'up': 0.32}}),
        M(air, st, {'ikR': (-0.08, -0.45, 1.45), 'sw': (nrm((0, -0.6, 0.8)), (-1, 0, 0)), 'b': {'waist': (10, 0, 0), 'chest': (6, 0, 0)}, 'rig': {'up': 0.18}}),
        M(slam, st, {'ikR': (-0.06, -0.55, 0.62), 'sw': (nrm((0, -0.35, -0.94)), (-1, 0, 0)), 'b': {'waist': (40, 0, 0), 'chest': (16, 0, 0), 'neck': (-24, 0, 0)}}),
        M(slam, st, {'ikR': (-0.06, -0.55, 0.60), 'sw': (nrm((0, -0.3, -0.95)), (-1, 0, 0)), 'b': {'waist': (42, 0, 0), 'chest': (16, 0, 0), 'neck': (-26, 0, 0)}, 'pl': (0, -0.10, -0.32)}),
        M(crouch, st, {'ikR': (-0.10, -0.48, 0.75), 'sw': (nrm((-0.05, -0.45, -0.9)), (-1, 0, 0)), 'b': {'waist': (30, 0, 0), 'chest': (10, 0, 0)}}),
        M(GUARD_LEGS, LEFT_RELAXED, READY_SWORD, {'b': {'waist': (8, 0, 0)}}),
    ]


CLIPS = None

# Frames that sweep the blade (they get a smear) and the phase ranges used by the game:
# startup = frames before the hit, active = the frames with the smear, recovery = the rest.
SMEAR = {'light1': [2, 3], 'light2': [2, 3], 'light3': [3], 'heavy1': [3, 4], 'heavy2': [2, 3, 4], 'heavy3': [4]}


def mark(clips):
    for k, idx in SMEAR.items():
        for i in idx:
            clips[k][i]['smear'] = True
    return clips


def build():
    global CLIPS
    sh = sheathe()
    CLIPS = {
        'idle': idle(True), 'walk': walk(True), 'sprint': sprint(True), 'roll': roll(True), 'hurt': hurt(True),
        'death': death(),
        'idle_s': idle(False), 'walk_s': walk(False), 'sprint_s': sprint(False), 'roll_s': roll(False),
        'hurt_s': hurt(False),
        'sheathe': sh,
        'light1': light1(), 'light2': light2(), 'light3': light3(),
        'heavy1': heavy1(), 'heavy2': heavy2(), 'heavy3': heavy3(),
    }
    mark(CLIPS)
    return CLIPS
