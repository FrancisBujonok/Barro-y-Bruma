# Barro y Bruma - village houses for Cenizal, built with edificio_kit.py (exec it first in the same namespace).
#   build_all('piedra')     CasaPiedra: low stone house with a thick thatch roof, chimney in the east gable.
#   build_all('entramado')  CasaEntramado: longer half-timbered house (dark beams, plaster panels), shingle roof,
#                           chimney stack through the roof.
# Both: walls a little taller than the hero, a plank door (Unity decides per house whether it is locked), windows
# with shutters, and a furnished room (hearth, bed, table and bench, shelves, barrels, sacks, hanging herbs).
import math, random
from mathutils import Vector

S = {}           # the current style's numbers (filled by setup())
FLOOR = 0.08


def setup(style):
    S.clear()
    if style == 'piedra':
        S.update(name='CasaPiedra', X=2.6, Y=2.0, H=2.5, ridge=4.3, t=0.32, door=(-0.45, 0.55), door_h=2.0,
                 wins={'S': [(1.35, 2.0, 1.1, 1.6)], 'W': [(-0.35, 0.35, 1.15, 1.6)], 'N': [(-1.4, -0.8, 1.2, 1.6)]},
                 wall='stone', roof='thatch', chimney='gable')
    else:
        S.update(name='CasaEntramado', X=3.4, Y=2.2, H=2.8, ridge=4.7, t=0.22, door=(0.8, 1.8), door_h=2.05,
                 wins={'S': [(-2.5, -1.7, 1.1, 1.75), (-0.7, 0.1, 1.1, 1.75)], 'E': [(-0.5, 0.4, 1.15, 1.7)],
                       'W': [(-0.4, 0.4, 1.15, 1.7)]},
                 wall='plaster', roof='shingle', chimney='stack')
    S['style'] = style


def mats():
    pmat('stone', 'stone', ['2a2420', '3f3e3b', '594a3c', '6e6c66'], scale=1.7, grime=0.35)
    pmat('stone_dark', 'stone', ['2a2420', '3f3e3b', '6e6c66'], scale=1.9, grime=0.2)
    pmat('plaster', 'noise', ['6e6c66', '9a968c', '9a968c', 'c4bfb2'], scale=2.0, grime=0.45, detail=4.0)
    pmat('beam', 'logs', ['141210', '2a2420', '3d332b'], scale=1.5)
    pmat('thatch', 'fibers', ['141210', '2a2420', '3d332b', '594a3c', '7a6650'], scale=1.0, coords='object',
         stretch=(8.0, 0.6, 8.0), moss={'cols': ['2a2420', '4e4b34', '4e4b34'], 'cut': 0.7})
    pmat('shingle', 'shingles', ['141210', '2a2420', '3f3e3b', '4f5a5c'], size=(0.26, 0.5), axes='xy', mortar=0.07,
         coords='object', moss={'cols': ['3d332b', '4e4b34', '4e4b34'], 'cut': 0.7})
    pmat('boards', 'planks', ['2a2420', '3d332b', '594a3c', '7a6650'], size=(1.3, 0.17), axes='zy', mortar=0.03, grime=0.2)
    pmat('boards_x', 'planks', ['2a2420', '3d332b', '594a3c', '7a6650'], size=(1.3, 0.17), axes='zx', mortar=0.03, grime=0.2)
    pmat('floor', 'planks', ['2a2420', '3d332b', '594a3c', '7a6650'], size=(1.8, 0.2), axes='xy', mortar=0.025)
    pmat('flags', 'stone', ['2a2420', '3d332b', '594a3c', '6e6c66'], scale=1.2, mortar=0.05)
    pmat('soot', 'flat', ['141210', '141210', '2a2420'])
    pmat('wood', 'noise', ['2a2420', '3d332b', '594a3c'], scale=3.0)
    pmat('wood_light', 'noise', ['594a3c', '7a6650', '9c8a70'], scale=3.0)
    pmat('iron', 'flat', ['141210', '2f3436', '3f3e3b'])
    pmat('straw', 'noise', ['7a6650', '9c8a70', 'b08a6a'], scale=9.0)
    pmat('wool', 'stripes', ['2f3436', '4f5a5c', '3d332b', '4f5a5c'], scale=2.5, dir='Y')
    pmat('wool_r', 'stripes', ['3e1a18', '6b2a24', '3d332b', '6b2a24'], scale=2.5, dir='Y')
    pmat('linen', 'noise', ['6e6c66', '9a968c', 'c4bfb2'], scale=5.0)
    pmat('clay', 'noise', ['3e1a18', '6b2a24', '8f4a36'], scale=6.0)
    pmat('herbs', 'noise', ['2a2420', '4e4b34', '6f6a45'], scale=9.0)
    pmat('ember', 'noise', ['b24a36', 'd2ac88', 'b24a36'], scale=8.0, glow=True)
    pmat('flame', 'flat', ['d2ac88', 'e6e2d4', 'd2ac88'], glow=True)


# ------------------------------------------------------------------------------------------------ walls

def wall(side, openings):
    """Box wall with holes. openings: (u0, u1, z0, z1). The strip under 0.5 m goes to the base part."""
    X, Y, H, t = S['X'], S['Y'], S['H'], S['t']
    along_x = side in 'SN'
    fixed = (-Y if side == 'S' else Y) if along_x else (-X if side == 'W' else X)
    u_lo, u_hi = (-X - t / 2, X + t / 2) if along_x else (-Y + t / 2, Y - t / 2)
    mat = S['wall']
    def B(u0, u1, z0, z1, name, part):
        if u1 - u0 < 0.01 or z1 - z0 < 0.01:
            return
        if along_x:
            box(name, (u0, fixed - t / 2, z0), (u1, fixed + t / 2, z1), mat, part)
        else:
            box(name, (fixed - t / 2, u0, z0), (fixed + t / 2, u1, z1), mat, part)
    base, up = 'wall_%s_base' % side, 'wall_%s_up' % side
    B(u_lo, u_hi, 0, 0.5, 'w%s_base' % side, base)
    # vertical strips between the openings, full height; above and below each opening, small boxes
    edges = sorted(openings)
    a = u_lo
    for k, (u0, u1, z0, z1) in enumerate(edges):
        B(a, u0, 0.5, H, 'w%s_strip%d' % (side, k), up)
        B(u0, u1, 0.5, max(z0, 0.5), 'w%s_under%d' % (side, k), up)
        B(u0, u1, z1, H, 'w%s_over%d' % (side, k), up)
        a = u1
    B(a, u_hi, 0.5, H, 'w%s_strip_end' % side, up)
    if S['style'] == 'entramado':
        timber(side, openings)


def timber(side, openings):
    """Dark beams over the plaster: sill, top plate, posts at corners and openings, a few braces."""
    X, Y, H, t = S['X'], S['Y'], S['H'], S['t']
    along_x = side in 'SN'
    out = -1 if side in 'SW' else 1
    fixed = ((-Y if side == 'S' else Y) if along_x else (-X if side == 'W' else X)) + out * (t / 2 + 0.03)
    up = 'wall_%s_up' % side
    L = X if along_x else Y
    def B(u0, u1, z0, z1, name, rot=(0, 0, 0)):
        if along_x:
            box(name, (u0, fixed - 0.04, z0), (u1, fixed + 0.04, z1), 'beam', up, rot=rot)
        else:
            box(name, (fixed - 0.04, u0, z0), (fixed + 0.04, u1, z1), 'beam', up, rot=rot)
    B(-L - 0.1, L + 0.1, 0.5, 0.64, 't%s_sill' % side)
    B(-L - 0.1, L + 0.1, H - 0.16, H, 't%s_plate' % side)
    B(-L - 0.1, L + 0.1, 1.85, 1.95, 't%s_rail' % side)
    posts = [-L, L] + [u for o in openings for u in (o[0] - 0.08, o[1] + 0.08)]
    for k, u in enumerate(posts):
        B(u - 0.08, u + 0.08, 0.5, H, 't%s_post%d' % (side, k))
    # braces in the widest plain panels
    free = sorted(posts)
    for k in range(len(free) - 1):
        a, b = free[k] + 0.08, free[k + 1] - 0.08
        if b - a > 1.1:
            m = (a + b) / 2
            ang = math.atan2(1.25, b - a)
            rot = (0, -ang, 0) if along_x else (ang, 0, 0)
            if along_x:
                ob = box('t%s_brace%d' % (side, k), (-(b - a) / 2 * 1.08, -0.04, -0.06), ((b - a) / 2 * 1.08, 0.04, 0.06), 'beam', up, rot=rot)
                ob.location = (m, fixed, 1.25)
            else:
                ob = box('t%s_brace%d' % (side, k), (-0.04, -(b - a) / 2 * 1.08, -0.06), (0.04, (b - a) / 2 * 1.08, 0.06), 'beam', up, rot=rot)
                ob.location = (fixed, m, 1.25)


def frame(side, u0, u1, z0, z1, sill=False):
    X, Y, t = S['X'], S['Y'], S['t']
    along_x = side in 'SN'
    w = (-Y if side == 'S' else Y) if along_x else (-X if side == 'W' else X)
    d = t / 2 + 0.02
    part = 'wall_%s_up' % side
    def B(a0, a1, b0, b1, name):
        if along_x:
            box(name, (a0, w - d, b0), (a1, w + d, b1), 'wood', part)
        else:
            box(name, (w - d, a0, b0), (w + d, a1, b1), 'wood', part)
    B(u0 - 0.07, u0, z0, z1 + 0.07, 'f%s%.1fa' % (side, u0))
    B(u1, u1 + 0.07, z0, z1 + 0.07, 'f%s%.1fb' % (side, u0))
    B(u0 - 0.07, u1 + 0.07, z1, z1 + 0.09, 'f%s%.1ft' % (side, u0))
    if sill:
        B(u0 - 0.1, u1 + 0.1, z0 - 0.06, z0, 'f%s%.1fs' % (side, u0))


def shutters(side, u0, u1, z0, z1, seed):
    rnd = random.Random(seed)
    X, Y, t = S['X'], S['Y'], S['t']
    along_x = side in 'SN'
    out = -1 if side in 'SW' else 1
    w = ((-Y if side == 'S' else Y) if along_x else (-X if side == 'W' else X)) + out * (t / 2 + 0.05)
    width = (u1 - u0) / 2 + 0.02
    mat = 'boards_x' if along_x else 'boards'
    part = 'wall_%s_up' % side
    for k, (a, b) in enumerate(((u0 - width - 0.06, u0 - 0.06), (u1 + 0.06, u1 + width + 0.06))):
        if rnd.random() < 0.2:
            continue                                       # one shutter lost
        if along_x:
            box('sh%s%.1f%d' % (side, u0, k), (a, w - 0.03, z0 - 0.02), (b, w + 0.03, z1 + 0.02), mat, part)
        else:
            box('sh%s%.1f%d' % (side, u0, k), (w - 0.03, a, z0 - 0.02), (w + 0.03, b, z1 + 0.02), mat, part)


def gable(side):
    X, Y, H, t, R = S['X'], S['Y'], S['H'], S['t'], S['ridge']
    x = -X if side == 'W' else X
    pts = [(-Y - t / 2, H - 0.01), (Y + t / 2, H - 0.01), (0.0, R - 0.1)]
    ob = prism('gable_' + side, pts, 'x', x - t / 2, x + t / 2, S['wall'], 'wall_%s_up' % side)
    if S['style'] == 'entramado':
        out = -1 if side == 'W' else 1
        xx = x + out * (t / 2 + 0.03)
        cyl('gbeam_%s_a' % side, (xx, -Y, H), (xx, 0, R - 0.15), 0.06, 'beam', 'wall_%s_up' % side, sides=4)
        cyl('gbeam_%s_b' % side, (xx, Y, H), (xx, 0, R - 0.15), 0.06, 'beam', 'wall_%s_up' % side, sides=4)
        cyl('gbeam_%s_c' % side, (xx, 0, H), (xx, 0, R - 0.15), 0.06, 'beam', 'wall_%s_up' % side, sides=4)


def roof():
    X, Y, H, R = S['X'], S['Y'], S['H'], S['ridge']
    thatch = S['roof'] == 'thatch'
    over_y = Y + (0.55 if thatch else 0.5)
    over_x = X + (0.5 if thatch else 0.45)
    slope = (R - H) / (Y + S['t'] / 2)
    z_r = R + (0.1 if thatch else 0.06)
    z_e = z_r - slope * over_y
    L = math.hypot(over_y, z_r - z_e)
    a = math.atan2(z_r - z_e, over_y)
    th = 0.3 if thatch else 0.07
    rows = 5 if thatch else 8
    for s in (-1, 1):
        ny, nz = s * math.sin(a), math.cos(a)
        for r in range(rows):
            t0, t1 = r / rows, (r + 1) / rows + (0.08 if thatch else 0.04)
            yc = s * over_y * (t0 + t1) / 2
            zc = z_r - (z_r - z_e) * (t0 + t1) / 2
            lift = (th / 2 + 0.02 * r) if thatch else (0.02 + 0.012 * r)
            ob = box('roof_%d_%d' % (s, r), (-over_x - (0.06 * r if thatch else 0), -L * (t1 - t0) / 2, -th / 2),
                     (over_x + (0.06 * r if thatch else 0), L * (t1 - t0) / 2, th / 2), S['roof'], 'roof',
                     rot=(-s * (a + math.radians(3)), 0, 0))
            ob.location = (0, yc + ny * lift, zc + nz * lift)
        if not thatch:
            ob = box('roofboard_%d' % s, (-over_x, -L / 2, -0.04), (over_x, L / 2, 0.04), 'boards_x', 'roof', rot=(-s * a, 0, 0))
            ob.location = (0, s * over_y / 2 - ny * 0.03, (z_r + z_e) / 2 - nz * 0.03)
    if thatch:
        cyl('ridge_roll', (-over_x - 0.1, 0, z_r + 0.22), (over_x + 0.1, 0, z_r + 0.22), 0.2, 'thatch', 'roof', sides=8)
    else:
        for s in (-1, 1):
            ob = box('ridge_%d' % s, (-over_x - 0.05, -0.13, -0.03), (over_x + 0.05, 0.13, 0.03), 'wood', 'roof',
                     rot=(-s * math.radians(40), 0, 0))
            ob.location = (0, s * 0.08, z_r + 0.1)


def chimney():
    X, Y, H, R = S['X'], S['Y'], S['H'], S['ridge']
    if S['chimney'] == 'gable':
        x = X
        box('chim', (x - 0.2, -0.45, 0), (x + 0.45, 0.45, R + 0.5), 'stone', 'chimney')
        box('chim_cap', (x - 0.24, -0.5, R + 0.5), (x + 0.5, 0.5, R + 0.6), 'stone_dark', 'chimney')
        box('chim_hole', (x - 0.1, -0.3, R + 0.6), (x + 0.32, 0.3, R + 0.61), 'soot', 'chimney')
    else:
        x, y = X - 1.3, 0.5
        box('chim', (x - 0.32, y - 0.3, R - 0.9), (x + 0.32, y + 0.3, R + 0.8), 'stone', 'roof')
        box('chim_cap', (x - 0.36, y - 0.34, R + 0.8), (x + 0.36, y + 0.34, R + 0.9), 'stone_dark', 'roof')


# ------------------------------------------------------------------------------------------------ inside

def floor():
    X, Y, t = S['X'], S['Y'], S['t']
    mat = 'flags' if S['style'] == 'piedra' else 'floor'
    box('floor', (-X + t / 2, -Y + t / 2, 0), (X - t / 2, Y - t / 2, FLOOR), mat, 'floor')
    d0, d1 = S['door']
    box('threshold', (d0, -Y - t / 2, 0), (d1, -Y + t / 2, FLOOR + 0.02), 'wood', 'floor')


def hearth(x, y0, y1, part='interior'):
    """Against an east wall at x (inside face), between y0 and y1."""
    box('hearth_base', (x - 0.55, y0, FLOOR), (x, y1, 0.3), 'stone', part)
    box('hearth_a', (x - 0.4, y0, 0.3), (x, y0 + 0.2, 1.2), 'stone', part)
    box('hearth_b', (x - 0.4, y1 - 0.2, 0.3), (x, y1, 1.2), 'stone', part)
    box('hearth_back', (x - 0.1, y0 + 0.2, 0.3), (x, y1 - 0.2, 1.2), 'soot', part)
    box('hearth_mantel', (x - 0.5, y0 - 0.05, 1.2), (x, y1 + 0.05, 1.34), 'wood', part)
    box('hearth_hood', (x - 0.3, y0 + 0.1, 1.34), (x, y1 - 0.1, S['H']), 'stone', part)
    box('embers', (x - 0.34, y0 + 0.28, 0.3), (x - 0.1, y1 - 0.28, 0.34), 'ember', 'glow')
    box('flame', (x - 0.28, (y0 + y1) / 2 - 0.06, 0.4), (x - 0.18, (y0 + y1) / 2 + 0.06, 0.62), 'flame', 'glow')
    cyl('pot', (x - 0.26, (y0 + y1) / 2, 0.6), (x - 0.26, (y0 + y1) / 2, 0.8), 0.12, 'iron', part, sides=8)


def bed(x0, y0, x1, y1, blanket='wool'):
    box('bed_frame', (x0, y0, FLOOR), (x1, y1, 0.42), 'wood', 'interior')
    box('bed_straw', (x0 + 0.05, y0 + 0.05, 0.42), (x1 - 0.05, y1 - 0.05, 0.55), 'straw', 'interior')
    along_x = (x1 - x0) > (y1 - y0)
    if along_x:
        box('bed_pillow', (x0 + 0.1, y0 + 0.15, 0.55), (x0 + 0.42, y1 - 0.15, 0.63), 'linen', 'interior')
        box('bed_blanket', (x0 + 0.5, y0 + 0.02, 0.55), (x1 - 0.02, y1 - 0.02, 0.62), blanket, 'interior')
    else:
        box('bed_pillow', (x0 + 0.15, y1 - 0.42, 0.55), (x1 - 0.15, y1 - 0.1, 0.63), 'linen', 'interior')
        box('bed_blanket', (x0 + 0.02, y0 + 0.02, 0.55), (x1 - 0.02, y1 - 0.5, 0.62), blanket, 'interior')


def table(x0, y0, x1, y1, seed):
    rnd = random.Random(seed)
    box('table_top', (x0, y0, 0.72), (x1, y1, 0.8), 'wood_light', 'interior')
    for k, (x, y) in enumerate(((x0 + 0.06, y0 + 0.06), (x1 - 0.06, y0 + 0.06), (x0 + 0.06, y1 - 0.06), (x1 - 0.06, y1 - 0.06))):
        box('tleg%d' % k, (x - 0.04, y - 0.04, FLOOR), (x + 0.04, y + 0.04, 0.72), 'wood', 'interior')
    # bench along the long side
    if x1 - x0 > y1 - y0:
        box('bench', (x0 + 0.1, y0 - 0.45, 0.4), (x1 - 0.1, y0 - 0.15, 0.46), 'wood', 'interior')
        for k, x in enumerate((x0 + 0.2, x1 - 0.2)):
            box('bleg%d' % k, (x - 0.04, y0 - 0.4, FLOOR), (x + 0.04, y0 - 0.2, 0.4), 'wood', 'interior')
    for k in range(3):
        x, y = rnd.uniform(x0 + 0.15, x1 - 0.15), rnd.uniform(y0 + 0.12, y1 - 0.12)
        cyl('dish%d' % k, (x, y, 0.8), (x, y, 0.86 + 0.06 * (k == 1)), 0.08 if k != 1 else 0.045, 'clay' if k != 1 else 'wood',
            'interior', sides=7)


def shelf(axis, w, a0, a1, z, seed):
    """Shelf on a wall: axis 'y' means the wall is a north/south wall at y = w (shelf runs along x)."""
    rnd = random.Random(seed)
    d = -0.22 if w > 0 else 0.22
    if axis == 'y':
        box('shelf%.1f' % a0, (a0, min(w, w + d), z), (a1, max(w, w + d), z + 0.05), 'wood_light', 'interior')
    else:
        box('shelf%.1f' % a0, (min(w, w + d), a0, z), (max(w, w + d), a1, z + 0.05), 'wood_light', 'interior')
    n = int((a1 - a0) / 0.28)
    for k in range(n):
        u = a0 + 0.15 + k * 0.28
        h = rnd.uniform(0.1, 0.24)
        m = rnd.choice(['clay', 'clay', 'linen', 'wood'])
        p0 = (u, w + d / 2, z + 0.05) if axis == 'y' else (w + d / 2, u, z + 0.05)
        p1 = (u, w + d / 2, z + 0.05 + h) if axis == 'y' else (w + d / 2, u, z + 0.05 + h)
        cyl('jar%.1f_%d' % (a0, k), p0, p1, rnd.uniform(0.04, 0.07), m, 'interior', sides=6)


def barrel(name, x, y, h=0.75, part='interior'):
    cyl(name, (x, y, FLOOR if part == 'interior' else 0), (x, y, h), 0.26, 'boards', part, sides=10)
    cyl(name + '_hoop', (x, y, h - 0.15), (x, y, h - 0.11), 0.275, 'iron', part, sides=10)


def herbs(x0, x1, y, z):
    cyl('herb_beam', (x0, y, z), (x1, y, z), 0.05, 'beam', 'interior', sides=5)
    n = int((x1 - x0) / 0.35)
    for k in range(n):
        x = x0 + 0.2 + k * 0.35
        cyl('herb%d' % k, (x, y, z - 0.05), (x, y, z - 0.38), 0.05, 'herbs', 'interior', sides=5, r1=0.09)


def interior():
    X, Y, t = S['X'], S['Y'], S['t']
    ix0, ix1, iy0, iy1 = -X + t / 2, X - t / 2, -Y + t / 2, Y - t / 2
    if S['style'] == 'piedra':
        hearth(ix1, -0.45, 0.45)
        bed(ix0 + 0.1, iy1 - 0.95, ix0 + 1.95, iy1 - 0.08, 'wool')
        table(-0.5, -0.2, 0.7, 0.5, 31)
        shelf('y', iy1, 0.0, 1.6, 1.4, 32)
        barrel('barrel_a', ix1 - 0.35, iy0 + 0.35)
        rock('sack_a', (ix1 - 0.95, iy0 + 0.3, FLOOR + 0.18), (0.25, 0.2, 0.28), 'linen', 'interior', seed=33, squash=1.0)
        herbs(ix0 + 0.2, ix0 + 1.6, iy0 + 0.6, 2.15)
        box('chest', (ix0 + 0.1, iy0 + 0.1, FLOOR), (ix0 + 0.75, iy0 + 0.55, 0.55), 'wood', 'interior')
    else:
        hearth(ix1, 0.2, 1.2)
        bed(ix0 + 0.1, iy1 - 0.95, ix0 + 1.95, iy1 - 0.08, 'wool_r')
        bed(ix0 + 0.1, iy0 + 0.1, ix0 + 1.0, iy0 + 1.9, 'wool')
        table(-0.6, 0.0, 1.2, 0.75, 41)
        shelf('y', iy1, 0.0, 2.2, 1.45, 42)
        shelf('x', ix1, -1.9, -0.7, 1.3, 43)
        barrel('barrel_a', ix1 - 0.35, iy0 + 0.35)
        barrel('barrel_b', ix1 - 0.95, iy0 + 0.33, 0.65)
        rock('sack_a', (2.0, -1.55, FLOOR + 0.18), (0.25, 0.2, 0.28), 'linen', 'interior', seed=44, squash=1.0)
        herbs(-1.2, 0.8, iy1 - 0.5, 2.4)
        # a loom-like frame: this family wove for a living
        for k, x in enumerate((-2.6, -1.6)):
            box('loom_post%d' % k, (x - 0.05, 0.6, FLOOR), (x + 0.05, 0.7, 1.6), 'wood', 'interior')
        box('loom_beam', (-2.65, 0.58, 1.5), (-1.55, 0.72, 1.6), 'wood', 'interior')
        box('loom_cloth', (-2.5, 0.63, 0.7), (-1.7, 0.67, 1.45), 'wool', 'interior')


def door():
    Y, t = S['Y'], S['t']
    u0, u1 = S['door']
    top = S['door_h']
    y = -Y - t / 2 + 0.06
    box('door_leaf', (u0 + 0.02, y - 0.04, FLOOR + 0.01), (u1 - 0.02, y + 0.04, top - 0.02), 'boards_x', 'door')
    for k, z in enumerate((0.45, 1.5)):
        box('door_strap_%d' % k, (u0 + 0.1, y - 0.06, z), (u1 - 0.02, y - 0.04, z + 0.07), 'iron', 'door')
    box('door_ring', (u0 + 0.12, y - 0.08, 1.0), (u0 + 0.2, y - 0.05, 1.1), 'iron', 'door')
    # two boards nailed across: only seen when Unity locks the door (part 'door_bar', a child of the leaf)
    for k, a in enumerate((0.5, -0.45)):
        ob = box('bar_%d' % k, (-0.75, -0.03, -0.08), (0.75, 0.03, 0.08), 'boards_x', 'door_bar', rot=(0, a, 0))
        ob.location = ((u0 + u1) / 2, y - 0.09, 1.1 - k * 0.15)
    box('broken_top', (u1 - 0.5, y - 0.04, 1.2), (u1 - 0.02, y + 0.04, top - 0.02), 'boards_x', 'door_broken', rot=(0, 0.12, 0))
    for k, (dx, dy, a) in enumerate(((-0.1, 0.6, 0.3), (0.3, 0.9, -0.5), (-0.4, 1.0, 1.2))):
        ob = box('broken_plank_%d' % k, (-0.08, -0.55, 0), (0.08, 0.55, 0.04), 'boards_x', 'door_broken', rot=(0, 0, a))
        ob.location = ((u0 + u1) / 2 + dx, -Y + dy, FLOOR + 0.02 + k * 0.012)


def outside():
    X, Y = S['X'], S['Y']
    rnd = random.Random(7)
    d0, d1 = S['door']
    rock('step', ((d0 + d1) / 2, -Y - 0.55, 0.02), (0.5, 0.3, 0.12), 'stone', 'ext', seed=5, squash=0.5)
    barrel('barrel_out', d1 + 0.5, -Y - 0.45, 0.8, 'ext')
    if S['style'] == 'piedra':
        for k in range(3):
            rock('pile%d' % k, (-X - 0.45, -1.0 + k * 0.6, 0.15), (0.3, 0.28, 0.22), 'stone', 'ext', seed=50 + k, squash=0.8)
    else:
        for row in range(3):
            cyl('wood_out%d' % row, (X + 0.45, -1.4, 0.1 + row * 0.19), (X + 0.45, 1.4, 0.1 + row * 0.19), 0.1, 'beam', 'ext', sides=6)


PARTS = {
    'roof': {'group': 'cabin', 'role': 'roof'},
    'chimney': {'group': 'cabin', 'role': 'static'},
    'floor': {'group': 'cabin', 'role': 'static', 'shadow': False},
    'interior': {'group': 'cabin', 'role': 'static', 'shadow': False},
    'ext': {'group': 'cabin', 'role': 'static'},
    'glow': {'group': 'cabin', 'role': 'static'},
    'door_broken': {'group': 'cabin', 'role': 'door_broken', 'normal': (0, -1, 0)},
}


def build_all(style):
    for ob in list(bpy.data.objects):
        bpy.data.objects.remove(ob, do_unlink=True)
    for m in list(bpy.data.materials):
        bpy.data.materials.remove(m)
    for me in list(bpy.data.meshes):
        bpy.data.meshes.remove(me)
    reset()
    setup(style)
    mats()
    X, Y, H = S['X'], S['Y'], S['H']
    wins = S['wins']
    d0, d1 = S['door']
    ops = {'S': [(d0, d1, 0.0, S['door_h'])], 'N': [], 'E': [], 'W': []}
    for side, lst in wins.items():
        for w in lst:
            ops[side].append(w)
    seed = 1
    for side in 'SNEW':
        wall(side, ops[side])
        for (u0, u1, z0, z1) in ops[side]:
            is_door = side == 'S' and (u0, u1) == (d0, d1)
            frame(side, u0, u1, max(z0, FLOOR), z1, sill=not is_door)
            if not is_door:
                shutters(side, u0, u1, z0, z1, seed)
                seed += 1
    gable('W')
    gable('E')
    roof()
    chimney()
    floor()
    interior()
    door()
    outside()
    bpy.context.view_layer.update()
    for side in 'SNEW':
        n = {'S': (0, -1, 0), 'N': (0, 1, 0), 'E': (1, 0, 0), 'W': (-1, 0, 0)}[side]
        PARTS['wall_%s_up' % side] = {'group': 'cabin', 'role': 'cut', 'normal': n}
        PARTS['wall_%s_base' % side] = {'group': 'cabin', 'role': 'static'}
    y = -Y - S['t'] / 2 + 0.06
    PARTS['door'] = {'group': 'cabin', 'role': 'door', 'normal': (0, -1, 0), 'pivot': (d1, y, FLOOR)}
    PARTS['door_bar'] = {'group': 'cabin', 'role': 'door_bar', 'normal': (0, -1, 0), 'pivot': (d1, y, FLOOR)}
    return len(PIECES)


def meta():
    X, Y, t = S['X'], S['Y'], S['t']
    d0, d1 = S['door']
    ix0, ix1, iy0, iy1 = -X + t / 2, X - t / 2, -Y + t / 2, Y - t / 2
    obs = [UB(-X - t / 2, Y - t / 2, X + t / 2, Y + t / 2), UB(X - t / 2, -Y - t / 2, X + t / 2, Y + t / 2),
           UB(-X - t / 2, -Y - t / 2, -X + t / 2, Y + t / 2), UB(-X - t / 2, -Y - t / 2, d0 - 0.03, -Y + t / 2),
           UB(d1 + 0.03, -Y - t / 2, X + t / 2, -Y + t / 2), UB(d1 + 0.22, -Y - 0.72, d1 + 0.78, -Y - 0.18)]
    if S['style'] == 'piedra':
        obs += [UB(ix1 - 0.6, -0.5, ix1, 0.5), UB(ix0, iy1 - 1.0, ix0 + 2.0, iy1), UB(-0.55, -0.25, 0.75, 0.55),
                UB(ix1 - 0.65, iy0, ix1, iy0 + 0.65), UB(ix0, iy0, ix0 + 0.8, iy0 + 0.6), UB(X + 0.15, -0.5, X + 0.5, 0.5),
                UB(-X - 0.8, -1.35, -X - 0.15, 0.55)]
    else:
        obs += [UB(ix1 - 0.6, 0.15, ix1, 1.25), UB(ix0, iy1 - 1.0, ix0 + 2.0, iy1), UB(ix0, iy0, ix0 + 1.05, iy0 + 1.95),
                UB(-0.65, -0.5, 1.25, 0.8), UB(ix1 - 1.25, iy0, ix1, iy0 + 0.65), UB(-2.7, 0.55, -1.5, 0.75),
                UB(X + 0.3, -1.5, X + 0.6, 1.5)]
    return {
        'name': S['name'],
        'footprint': UB(ix0, iy0, ix1, iy1),
        'obstacles': {'cabin': obs},
        'door': {'closed': UB(d0, -Y - 0.12, d1, -Y + 0.12), 'openYaw': 100, 'locked': False, 'health': 55,
                 'prompt': U((d0 + d1) / 2, -Y - 0.5, 0), 'inside': U((d0 + d1) / 2, -Y + 0.6, 0)},
        'lights': [{'group': 'cabin', 'pos': U(ix1 - 0.45, 0.0 if S['style'] == 'piedra' else 0.7, 0.7), 'radius': 3.6,
                    'color': [1.0, 0.6, 0.3], 'intensity': 0.85, 'flicker': 0.45}],
    }
