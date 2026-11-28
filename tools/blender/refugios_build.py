# Barro y Bruma - the refuges that were the old generic hut: Cabana del cazador, Cabana del Soto, Cabana del
# marismeno, Posada abandonada and Molino abandonado (a water mill). One script, one style per building:
#   build_all('cazador' | 'soto' | 'marisma' | 'posada' | 'molino'); unwrap(PIECES); bake(...); export(json, meta(), PARTS)
# Built with edificio_kit.py (exec it first). Real metres: BuildingImporter scales them by 1.4.
# Every refuge has a fire inside: the rest point (anchor 'rest') goes next to it, MapWorld moves the refuge there.
# The furniture helpers fill OBS (obstacles) and SEARCH (things to search) as they place things.
import math, random
from mathutils import Vector

FLOOR = 0.05
BASE = 0.5
LEAF = ['1f2a1f', '2b3a2a', '3b4a33', '4f5c40', '666043']
for _h in LEAF:
    if _h not in PALETTE:
        PALETTE.append(_h)

S = {}            # the current style
OBS = []          # furniture obstacles (x0, y0, x1, y1), real metres
SEARCH = []       # (name, what, table, x, y)
EXTRA_PARTS = {}


def mats():
    pmat('logs', 'logs', ['141210', '2a2420', '3d332b', '594a3c', '7a6650'], scale=1.0, grime=0.4)
    pmat('boards', 'planks', ['2a2420', '3d332b', '594a3c', '7a6650'], size=(1.3, 0.17), axes='zy', mortar=0.03, grime=0.35)
    pmat('boards_x', 'planks', ['2a2420', '3d332b', '594a3c', '7a6650'], size=(1.3, 0.17), axes='zx', mortar=0.03, grime=0.35)
    pmat('boards_f', 'planks', ['2a2420', '3d332b', '594a3c'], size=(1.6, 0.2), axes='xy', mortar=0.03, grime=0.3)
    pmat('grey_x', 'planks', ['141210', '2a2420', '3f3e3b', '594a3c'], size=(1.3, 0.2), axes='zx', mortar=0.035, grime=0.5)
    pmat('grey', 'planks', ['141210', '2a2420', '3f3e3b', '594a3c'], size=(1.3, 0.2), axes='zy', mortar=0.035, grime=0.5)
    pmat('shingle', 'shingles', ['141210', '2a2420', '3d332b', '594a3c'], size=(0.32, 0.2), axes='xy', mortar=0.05,
         moss={'cols': ['2a2420', '2b3a2a', '3b4a33'], 'cut': 0.66})
    pmat('thatch', 'fibers', ['141210', '2a2420', '3d332b', '594a3c', '7a6650'], scale=1.0, coords='object',
         stretch=(8.0, 0.6, 8.0), moss={'cols': ['2a2420', '2b3a2a', '3b4a33'], 'cut': 0.72})
    pmat('reed', 'fibers', ['2a2420', '3d332b', '594a3c', '7a6650', '9c8a70'], scale=1.0, coords='object', stretch=(9.0, 0.5, 9.0))
    pmat('stone', 'stone', ['2a2420', '3f3e3b', '594a3c', '6e6c66'], scale=1.7, grime=0.4,
         moss={'cols': ['2a2420', '2b3a2a', '3b4a33'], 'cut': 0.75})
    pmat('stone_dark', 'stone', ['141210', '2a2420', '3f3e3b', '6e6c66'], scale=1.9, grime=0.2)
    pmat('plaster', 'noise', ['594a3c', '7a6650', '9c8a70', 'b08a6a', 'c4bfb2'], scale=1.8, grime=0.6, detail=4.0)
    pmat('mud', 'noise', ['2a2420', '3d332b', '594a3c', '594a3c', '7a6650'], scale=2.4, grime=0.5, detail=4.0)
    pmat('wattle', 'stripes', ['141210', '2a2420', '3d332b', '594a3c'], scale=9.0, dir='X')
    pmat('beam', 'noise', ['141210', '2a2420', '3d332b'], scale=3.0)
    pmat('wood', 'noise', ['2a2420', '3d332b', '594a3c'], scale=3.0)
    pmat('wood_light', 'noise', ['594a3c', '7a6650', '9c8a70'], scale=3.0)
    pmat('char', 'noise', ['141210', '141210', '1f1c1a', '2a2420', '6b2a24'], scale=5.0)
    pmat('soot', 'flat', ['141210', '141210', '2a2420'])
    pmat('dirt', 'dirt', ['2a2420', '3d332b', '594a3c', '7a6650'], scale=1.2)
    pmat('ash', 'dirt', ['1f1c1a', '3f3e3b', '6e6c66', '9a968c'], scale=2.0)
    pmat('iron', 'flat', ['141210', '2f3436', '3f3e3b'])
    pmat('straw', 'noise', ['7a6650', '9c8a70', 'b08a6a'], scale=9.0)
    pmat('wool', 'stripes', ['2a2420', '594a3c', '9a968c', '594a3c'], scale=2.5, dir='Y')
    pmat('wool_r', 'stripes', ['3e1a18', '6b2a24', '3d332b', '6b2a24'], scale=2.5, dir='Y')
    pmat('pelt', 'noise', ['2a2420', '3d332b', '594a3c', '7a6650'], scale=8.0, detail=5.0)
    pmat('pelt_d', 'noise', ['141210', '2a2420', '3d332b'], scale=8.0, detail=5.0)
    pmat('meat', 'noise', ['3e1a18', '6b2a24', '8f4a36'], scale=8.0)
    pmat('linen', 'noise', ['594a3c', '6e6c66', '9a968c'], scale=5.0)
    pmat('rope', 'noise', ['594a3c', '7a6650', '9c8a70'], scale=8.0)
    pmat('net', 'stripes', ['141210', '3d332b', '594a3c', '141210'], scale=16.0, dir='X')
    pmat('wicker', 'stripes', ['2a2420', '3d332b', '594a3c', '7a6650'], scale=9.0, dir='Z')
    pmat('clay', 'noise', ['3e1a18', '6b2a24', '8f4a36'], scale=6.0)
    pmat('bone', 'noise', ['9a968c', 'c4bfb2', 'c4bfb2'], scale=6.0)
    pmat('fish', 'noise', ['3f3e3b', '6e6c66', '9a968c'], scale=7.0)
    pmat('flour', 'noise', ['9a968c', 'c4bfb2', 'e6e2d4'], scale=6.0)
    pmat('sign', 'noise', ['3d332b', '594a3c', '7a6650'], scale=3.0)
    pmat('paint', 'flat', ['b08a6a', 'd2ac88', 'd2ac88'])
    pmat('ember', 'noise', ['b24a36', 'd2ac88', 'b24a36'], scale=8.0, glow=True)
    pmat('flame', 'flat', ['d2ac88', 'e6e2d4', 'd2ac88'], glow=True)


# ------------------------------------------------------------------------------------------------ helpers

def blob(name, part, c, size, seed, mat, n=22, jag=0.2, under=0.5):
    rnd = random.Random(seed)
    bm = bmesh.new()
    vs = []
    for _ in range(n):
        d = Vector((rnd.gauss(0, 1), rnd.gauss(0, 1), rnd.gauss(0, 1)))
        if d.length < 1e-6:
            continue
        d.normalize()
        k = 1.0 + rnd.uniform(-jag, jag)
        p = Vector((d.x * size[0] * k, d.y * size[1] * k, d.z * size[2] * k))
        if p.z < 0:
            p.z *= under
        vs.append(bm.verts.new(p))
    res = bmesh.ops.convex_hull(bm, input=vs)
    junk = list(set(res['geom_interior']) | set(res['geom_unused']))
    bmesh.ops.delete(bm, geom=[g for g in junk if isinstance(g, bmesh.types.BMVert)], context='VERTS')
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    return _obj(name, bm, mat, part, loc=c)


def boxc(name, c, half, mat, part, rot=(0, 0, 0)):
    """A box given by its centre and half sizes (so a rotation turns it about its own centre)."""
    c, h = Vector(c), Vector(half)
    return box(name, c - h, c + h, mat, part, rot=rot)


def wall(side, openings):
    """One wall split around its openings: below BASE to the part that stays, above it to the part that fades."""
    X, Y, t, H = S['X'], S['Y'], S['t'], S['H']
    along_x = side in 'SN'
    w = (-Y if side == 'S' else Y) if along_x else (-X if side == 'W' else X)
    lo, hi = ((-X - t / 2, X + t / 2) if along_x else (-Y + t / 2, Y - t / 2))
    edges = sorted(set([lo, hi] + [e for o in openings for e in (o[0], o[1])]))
    k = [0]

    def mat_for(z0, z1):
        stone_to = S.get('stone_to', 0.0)
        if z1 <= stone_to + 1e-4:
            return 'stone'
        return S['wall_x'] if along_x else S['wall_y']

    def piece(u0, u1, z0, z1, part):
        if u1 - u0 < 0.02 or z1 - z0 < 0.02:
            return
        # a stone footing below the timber or plaster, if the style has one
        st = S.get('stone_to', 0.0)
        cuts = [(z0, z1)]
        if z0 < st < z1:
            cuts = [(z0, st), (st, z1)]
        for a, b in cuts:
            m = mat_for(a, b)
            tt = t + (0.06 if m == 'stone' and st > 0 else 0.0)
            if along_x:
                box('w_%s_%d' % (side, k[0]), (u0, w - tt / 2, a), (u1, w + tt / 2, b), m, part)
            else:
                box('w_%s_%d' % (side, k[0]), (w - tt / 2, u0, a), (w + tt / 2, u1, b), m, part)
            k[0] += 1

    for a, b in zip(edges[:-1], edges[1:]):
        m = (a + b) / 2
        hole = next((o for o in openings if o[0] <= m <= o[1]), None)
        if hole is None:
            piece(a, b, 0.0, BASE, 'wall_%s_base' % side)
            piece(a, b, BASE, H, 'wall_%s_up' % side)
        else:
            z0, z1 = hole[2], hole[3]
            if z0 > BASE:
                piece(a, b, 0.0, BASE, 'wall_%s_base' % side)
                piece(a, b, BASE, z0, 'wall_%s_up' % side)
            elif z0 > 0.0:
                piece(a, b, 0.0, z0, 'wall_%s_base' % side)
            piece(a, b, z1, H, 'wall_%s_up' % side)


def log_wall(side, openings):
    """Stacked round logs with the corners crossing; logs are split around the openings."""
    X, Y, H = S['X'], S['Y'], S['H']
    r = S['t'] / 2
    along_x = side in 'SN'
    w = (-Y if side == 'S' else Y) if along_x else (-X if side == 'W' else X)
    lo, hi = ((-X - 0.3, X + 0.3) if along_x else (-Y - 0.3, Y + 0.3))
    z = r if along_x else 2 * r
    rnd = random.Random(ord(side))
    k = 0
    while z < H:
        segs = [(lo, hi)]
        for (u0, u1, z0, z1) in openings:
            if z0 - r * 0.5 < z < z1 + r * 0.5:
                new = []
                for a, b in segs:
                    if b <= u0 or a >= u1:
                        new.append((a, b))
                    else:
                        if a < u0:
                            new.append((a, u0))
                        if b > u1:
                            new.append((u1, b))
                segs = new
        part = 'wall_%s_%s' % (side, 'base' if z < BASE else 'up')
        for a, b in segs:
            if b - a < 0.05:
                continue
            rr = r * rnd.uniform(0.92, 1.05)
            if along_x:
                cyl('log_%s_%d' % (side, k), (a, w, z), (b, w, z), rr, 'logs', part, sides=7, roll=k)
            else:
                cyl('log_%s_%d' % (side, k), (w, a, z), (w, b, z), rr, 'logs', part, sides=7, roll=k)
            k += 1
        z += 2 * r
    # chinking: dark mud between the logs, a thin slab inside the wall line
    for (a, b) in ([(lo + 0.3, hi - 0.3)]):
        if along_x:
            box('chink_%s' % side, (a, w - r * 0.4, 0.0), (b, w + r * 0.4, H), 'mud', 'wall_%s_up' % side)
        else:
            box('chink_%s' % side, (w - r * 0.4, a, 0.0), (w + r * 0.4, b, H), 'mud', 'wall_%s_up' % side)


def frames(side, openings, door_u=None):
    X, Y, t = S['X'], S['Y'], S['t']
    along_x = side in 'SN'
    w = (-Y if side == 'S' else Y) if along_x else (-X if side == 'W' else X)
    o = -1 if side in 'SW' else 1
    for i, (u0, u1, z0, z1) in enumerate(openings):
        is_door = door_u is not None and (u0, u1) == door_u
        parts = []
        if not is_door:
            parts.append(((u0 - 0.06, u1 + 0.06), (z0 - 0.07, z0)))
        parts.append(((u0 - 0.08, u1 + 0.08), (z1, z1 + 0.1)))
        for j, ((a, b), (c, d)) in enumerate(parts):
            if along_x:
                box('fr_%s_%d_%d' % (side, i, j), (a, w - t / 2 - 0.04, c), (b, w + t / 2 + 0.04, d), 'beam', 'wall_%s_up' % side)
            else:
                box('fr_%s_%d_%d' % (side, i, j), (w - t / 2 - 0.04, a, c), (w + t / 2 + 0.04, b, d), 'beam', 'wall_%s_up' % side)
        if not is_door:
            # a dark pane of the room behind, and a shutter swung open (or boarded up)
            if along_x:
                box('pane_%s_%d' % (side, i), (u0, w - 0.01, z0), (u1, w + 0.01, z1), 'soot', 'wall_%s_up' % side)
            else:
                box('pane_%s_%d' % (side, i), (w - 0.01, u0, z0), (w + 0.01, u1, z1), 'soot', 'wall_%s_up' % side)
            if S.get('boarded'):
                for k in range(2):
                    zz = z0 + (z1 - z0) * (0.3 + 0.4 * k)
                    if along_x:
                        boxc('board_%s_%d_%d' % (side, i, k), ((u0 + u1) / 2, w + o * (t / 2 + 0.03), zz), ((u1 - u0) / 2 + 0.12, 0.02, 0.07),
                             'grey_x', 'wall_%s_up' % side, rot=(0, 0.25 - 0.5 * k, 0))
                    else:
                        boxc('board_%s_%d_%d' % (side, i, k), (w + o * (t / 2 + 0.03), (u0 + u1) / 2, zz), (0.02, (u1 - u0) / 2 + 0.12, 0.07),
                             'grey', 'wall_%s_up' % side, rot=(0.25 - 0.5 * k, 0, 0))
            else:
                L = (u1 - u0) / 2
                if along_x:
                    boxc('shut_%s_%d' % (side, i), (u1 + 0.03 + L / 2 * 0.9, w + o * (t / 2 + 0.12), (z0 + z1) / 2), (L / 2, 0.025, (z1 - z0) / 2 + 0.02),
                         S['wall_x'] if S['wall_x'] != 'logs' else 'boards_x', 'wall_%s_up' % side, rot=(0, 0, o * 0.5))
                else:
                    boxc('shut_%s_%d' % (side, i), (w + o * (t / 2 + 0.12), u1 + 0.03 + L / 2 * 0.9, (z0 + z1) / 2), (0.025, L / 2, (z1 - z0) / 2 + 0.02),
                         'boards', 'wall_%s_up' % side, rot=(0, 0, -o * 0.5))


def gable(side, mat):
    X, Y, t, H, R = S['X'], S['Y'], S['t'], S['H'], S['ridge']
    x = -X if side == 'W' else X
    prism('gable_' + side, [(-Y - 0.1, H - 0.02), (Y + 0.1, H - 0.02), (0.0, R)], 'x', x - t / 2, x + t / 2, mat, 'wall_%s_up' % side)


def roof(mat, rows=6, holes=(), sag=0.0, over_y=0.55, over_x=0.45, thick=None, boards=True):
    """Two slopes of strips (shingle courses or bundles of thatch); `holes` = (side, row, third) left out."""
    X, Y, H, R = S['X'], S['Y'], S['H'], S['ridge']
    Yo, Xo = Y + over_y, X + over_x
    slope = (R - H) / (Y + 0.07)
    z_r = R + 0.08
    z_e = z_r - slope * Yo
    L = math.hypot(Yo, z_r - z_e)
    a = math.atan2(z_r - z_e, Yo)
    th = thick if thick is not None else (0.24 if mat in ('thatch', 'reed') else 0.07)
    rnd = random.Random(int(X * 100))
    thirds = [(-Xo, -Xo / 3), (-Xo / 3, Xo / 3), (Xo / 3, Xo)]
    for s in (-1, 1):
        ny, nz = s * math.sin(a), math.cos(a)
        for r in range(rows):
            t0, t1 = r / rows, (r + 1) / rows + 0.06
            for j, (x0, x1) in enumerate(thirds):
                if (s, r, j) in holes:
                    continue
                yc = s * Yo * (t0 + t1) / 2
                zc = z_r - (z_r - z_e) * (t0 + t1) / 2
                lift = th / 2 + 0.012 * r
                dz = -sag * (1.0 if j == 1 else 0.3) + rnd.uniform(-0.015, 0.015)
                ob = box('roof_%d_%d_%d' % (s, r, j), (x0 - 0.03, -L * (t1 - t0) / 2, -th / 2), (x1 + 0.03, L * (t1 - t0) / 2, th / 2), mat, 'roof',
                         rot=(-s * (a + math.radians(3)), rnd.uniform(-0.02, 0.02), 0))
                ob.location = ((x0 + x1) / 2, yc + ny * lift, zc + nz * lift + dz)
        if boards and not holes:
            ob = box('roofboard_%d' % s, (-Xo, -L / 2, -0.035), (Xo, L / 2, 0.035), 'boards_f', 'roof', rot=(-s * a, 0, 0))
            ob.location = (0, s * Yo / 2 - ny * 0.04, (z_r + z_e) / 2 - nz * 0.04 - sag * 0.6)
        # rafters (seen through holes and from inside the eaves)
        for k in range(5):
            x = -X + 2 * X * k / 4
            burnt = any((s, rr, jj) in holes for rr in range(rows) for jj in range(3) if thirds[jj][0] <= x <= thirds[jj][1])
            cyl('rafter_%d_%d' % (s, k), (x, 0.0, z_r - 0.06), (x, s * Yo * 0.97, z_e + 0.02), 0.05,
                'char' if burnt else 'beam', 'roof', sides=5)
    ridge_mat = 'thatch' if mat in ('thatch', 'reed') else 'wood'
    cyl('ridge', (-Xo - 0.05, 0, z_r + th * 0.6), (Xo + 0.05, 0, z_r + th * 0.6), 0.1 if ridge_mat == 'wood' else 0.16, ridge_mat, 'roof', sides=7)
    return z_r, z_e


def door_leaf(u0, u1, top, mat='boards_x', hang=0.0):
    Y, t = S['Y'], S['t']
    y = -Y - t / 2 + 0.05
    n = 4
    for k in range(n):
        a = u0 + 0.02 + k * (u1 - u0 - 0.04) / n
        b = a + (u1 - u0 - 0.04) / n - 0.008
        box('door_plank_%d' % k, (a, y - 0.035, FLOOR + 0.01), (b, y + 0.035, top - 0.02 - (0.08 if k == 1 and hang else 0)), mat, 'door')
    for k, z in enumerate((0.4, top - 0.45)):
        box('door_batten_%d' % k, (u0 + 0.06, y - 0.06, z), (u1 - 0.04, y - 0.035, z + 0.09), 'wood', 'door')
    box('door_ring', (u0 + 0.1, y - 0.08, 1.0), (u0 + 0.17, y - 0.05, 1.08), 'iron', 'door')
    EXTRA_PARTS['door'] = {'group': 'cabin', 'role': 'door', 'normal': (0, -1, 0), 'pivot': (u1, y, FLOOR)}


def floor(mat='boards_f'):
    X, Y, t = S['X'], S['Y'], S['t']
    box('floor', (-X + t / 2, -Y + t / 2, 0.0), (X - t / 2, Y - t / 2, FLOOR), mat, 'floor')
    d0, d1 = S['door']
    box('threshold', (d0, -Y - t / 2, 0.0), (d1, -Y + t / 2, FLOOR + 0.02), 'wood', 'floor')


# ------------------------------------------------------------------------------------------------ furniture

def hearth_east(y0, y1):
    x = S['X'] - S['t'] / 2
    box('hearth_base', (x - 0.6, y0, FLOOR), (x, y1, 0.3), 'stone', 'interior')
    box('hearth_a', (x - 0.42, y0, 0.3), (x, y0 + 0.2, 1.2), 'stone', 'interior')
    box('hearth_b', (x - 0.42, y1 - 0.2, 0.3), (x, y1, 1.2), 'stone', 'interior')
    box('hearth_back', (x - 0.1, y0 + 0.2, 0.3), (x, y1 - 0.2, 1.2), 'soot', 'interior')
    box('hearth_mantel', (x - 0.5, y0 - 0.05, 1.2), (x, y1 + 0.05, 1.32), 'wood', 'interior')
    box('hearth_hood', (x - 0.3, y0 + 0.1, 1.32), (x, y1 - 0.1, S['H']), 'stone', 'interior')
    box('embers', (x - 0.36, y0 + 0.28, 0.3), (x - 0.1, y1 - 0.28, 0.34), 'ember', 'glow')
    yc = (y0 + y1) / 2
    box('flame', (x - 0.3, yc - 0.07, 0.36), (x - 0.18, yc + 0.07, 0.62), 'flame', 'glow')
    cyl('hearth_pot', (x - 0.27, yc, 0.62), (x - 0.27, yc, 0.82), 0.12, 'iron', 'interior', sides=8)
    OBS.append((x - 0.65, y0, x, y1))
    return (x - 0.6, yc)


def hearth_north(x0, x1):
    y = S['Y'] - S['t'] / 2
    box('hearth_base', (x0, y - 0.6, FLOOR), (x1, y, 0.3), 'stone', 'interior')
    box('hearth_a', (x0, y - 0.42, 0.3), (x0 + 0.22, y, 1.3), 'stone', 'interior')
    box('hearth_b', (x1 - 0.22, y - 0.42, 0.3), (x1, y, 1.3), 'stone', 'interior')
    box('hearth_back', (x0 + 0.22, y - 0.1, 0.3), (x1 - 0.22, y, 1.3), 'soot', 'interior')
    box('hearth_mantel', (x0 - 0.06, y - 0.52, 1.3), (x1 + 0.06, y, 1.44), 'wood', 'interior')
    box('hearth_hood', (x0 + 0.1, y - 0.32, 1.44), (x1 - 0.1, y, S['H']), 'stone', 'interior')
    box('embers', (x0 + 0.3, y - 0.38, 0.3), (x1 - 0.3, y - 0.1, 0.34), 'ember', 'glow')
    xc = (x0 + x1) / 2
    box('flame', (xc - 0.08, y - 0.32, 0.36), (xc + 0.08, y - 0.18, 0.66), 'flame', 'glow')
    OBS.append((x0, y - 0.65, x1, y))
    return (xc, y - 0.6)


def fire_pit(cx, cy, r=0.42, seed=0):
    for k in range(9):
        a = 2 * math.pi * k / 9
        rock('pit_%d_%d' % (seed, k), (cx + r * math.cos(a), cy + r * math.sin(a), FLOOR + 0.04), (0.12, 0.1, 0.09), 'stone_dark', 'interior',
             seed=70 + k + seed, squash=0.8)
    box('pit_ash_%d' % seed, (cx - r * 0.75, cy - r * 0.75, FLOOR), (cx + r * 0.75, cy + r * 0.75, FLOOR + 0.02), 'soot', 'interior')
    for k in range(3):
        a = k * 2.1
        cyl('pit_log_%d_%d' % (seed, k), (cx + 0.25 * math.cos(a), cy + 0.25 * math.sin(a), FLOOR + 0.04), (cx, cy, FLOOR + 0.14), 0.045, 'wood', 'interior', sides=5)
    box('pit_embers_%d' % seed, (cx - 0.18, cy - 0.18, FLOOR + 0.02), (cx + 0.18, cy + 0.18, FLOOR + 0.06), 'ember', 'glow')
    box('pit_flame_%d' % seed, (cx - 0.06, cy - 0.05, FLOOR + 0.08), (cx + 0.05, cy + 0.06, FLOOR + 0.34), 'flame', 'glow', rot=(0, 0, 0.4))
    OBS.append((cx - r - 0.1, cy - r - 0.1, cx + r + 0.1, cy + r + 0.1))


def bed(x0, y0, x1, y1, blanket='wool', frame=True):
    z = 0.42 if frame else FLOOR
    if frame:
        box('bed_frame', (x0, y0, FLOOR), (x1, y1, 0.42), 'wood', 'interior')
    box('bed_straw', (x0 + 0.05, y0 + 0.05, z), (x1 - 0.05, y1 - 0.05, z + 0.12), 'straw', 'interior')
    if (x1 - x0) > (y1 - y0):
        box('bed_pillow', (x0 + 0.1, y0 + 0.15, z + 0.12), (x0 + 0.42, y1 - 0.15, z + 0.2), 'linen', 'interior')
        box('bed_blanket', (x0 + 0.5, y0 + 0.02, z + 0.12), (x1 - 0.02, y1 - 0.02, z + 0.19), blanket, 'interior')
    else:
        box('bed_pillow', (x0 + 0.15, y1 - 0.42, z + 0.12), (x1 - 0.15, y1 - 0.1, z + 0.2), 'linen', 'interior')
        box('bed_blanket', (x0 + 0.02, y0 + 0.02, z + 0.12), (x1 - 0.02, y1 - 0.5, z + 0.19), blanket, 'interior')
    OBS.append((x0, y0, x1, y1))


def table(x0, y0, x1, y1, seed, overturned=False):
    rnd = random.Random(seed)
    if overturned:
        boxc('table_top_%d' % seed, ((x0 + x1) / 2, (y0 + y1) / 2, 0.42), ((x1 - x0) / 2, 0.04, (y1 - y0) / 2), 'wood_light', 'interior')
        for k in range(2):
            cyl('table_leg_%d_%d' % (seed, k), (x0 + 0.1 + k * (x1 - x0 - 0.2), (y0 + y1) / 2 - 0.05, 0.8), (x0 + 0.1 + k * (x1 - x0 - 0.2), (y0 + y1) / 2 - 0.75, 0.82), 0.04, 'wood', 'interior', sides=5)
        OBS.append((x0, (y0 + y1) / 2 - 0.15, x1, (y0 + y1) / 2 + 0.15))
        return
    box('table_top_%d' % seed, (x0, y0, 0.72), (x1, y1, 0.8), 'wood_light', 'interior')
    for k, (x, y) in enumerate(((x0 + 0.06, y0 + 0.06), (x1 - 0.06, y0 + 0.06), (x0 + 0.06, y1 - 0.06), (x1 - 0.06, y1 - 0.06))):
        box('tleg_%d_%d' % (seed, k), (x - 0.04, y - 0.04, FLOOR), (x + 0.04, y + 0.04, 0.72), 'wood', 'interior')
    for k in range(3):
        x, y = rnd.uniform(x0 + 0.15, x1 - 0.15), rnd.uniform(y0 + 0.12, y1 - 0.12)
        cyl('dish_%d_%d' % (seed, k), (x, y, 0.8), (x, y, 0.86 + 0.06 * (k == 1)), 0.08 if k != 1 else 0.045, 'clay' if k != 1 else 'wood', 'interior', sides=7)
    OBS.append((x0, y0, x1, y1))


def bench(x0, x1, y, seed):
    box('bench_%d' % seed, (x0, y - 0.15, 0.4), (x1, y + 0.15, 0.46), 'wood', 'interior')
    for k, x in enumerate((x0 + 0.1, x1 - 0.1)):
        box('bench_leg_%d_%d' % (seed, k), (x - 0.04, y - 0.12, FLOOR), (x + 0.04, y + 0.12, 0.4), 'wood', 'interior')


def stool(x, y, seed):
    cyl('stool_%d' % seed, (x, y, 0.36), (x, y, 0.42), 0.16, 'wood_light', 'interior', sides=8)
    for j in range(3):
        a = j * 2.094
        cyl('stool_leg_%d_%d' % (seed, j), (x + 0.09 * math.cos(a), y + 0.09 * math.sin(a), 0.36), (x + 0.14 * math.cos(a), y + 0.14 * math.sin(a), FLOOR), 0.022, 'wood', 'interior', sides=4)


def shelf_north(x0, x1, z, seed, search=None, collapsed=False):
    y = S['Y'] - S['t'] / 2
    rnd = random.Random(seed)
    rot = (0, 0.25, 0) if collapsed else (0, 0, 0)
    ob = boxc('shelf_%d' % seed, ((x0 + x1) / 2, y - 0.12, z), ((x1 - x0) / 2, 0.12, 0.025), 'wood_light', 'interior', rot=rot)
    n = int((x1 - x0) / 0.26)
    for k in range(n):
        u = x0 + 0.14 + k * 0.26
        if collapsed and k % 2:
            # fallen to the floor
            cyl('jar_%d_%d' % (seed, k), (u, y - 0.35, FLOOR + 0.06), (u + 0.15, y - 0.4, FLOOR + 0.07), 0.06, 'clay', 'interior', sides=6)
            continue
        h = rnd.uniform(0.1, 0.24)
        zz = z + 0.025 + (math.tan(0.25) * (u - (x0 + x1) / 2) * -1 if collapsed else 0)
        cyl('jar_%d_%d' % (seed, k), (u, y - 0.12, zz), (u, y - 0.12, zz + h), rnd.uniform(0.04, 0.07), rnd.choice(['clay', 'clay', 'linen', 'wood']), 'interior', sides=6)
    OBS.append((x0, y - 0.26, x1, y))
    if search:
        SEARCH.append(search + ((x0 + x1) / 2, y - 0.12))


def barrel(name, x, y, h=0.75, part='interior', search=None):
    z0 = FLOOR if part == 'interior' else 0.0
    cyl(name, (x, y, z0), (x, y, h), 0.26, 'boards', part, sides=10)
    cyl(name + '_hoop', (x, y, h - 0.15), (x, y, h - 0.11), 0.275, 'iron', part, sides=10)
    OBS.append((x - 0.28, y - 0.28, x + 0.28, y + 0.28))
    if search:
        SEARCH.append(search + (x, y))


def chest(name, x0, y0, x1, y1, search=('Arcón', 'el arcón', 'cofre')):
    box(name, (x0, y0, FLOOR), (x1, y1, 0.55), 'wood', 'interior')
    box(name + '_lid', (x0 - 0.02, y0 - 0.02, 0.55), (x1 + 0.02, y1 + 0.02, 0.63), 'wood_light', 'interior')
    w = x1 - x0
    for k in range(2):
        box(name + '_band%d' % k, (x0 + w * (0.25 + 0.5 * k) - 0.03, y0 - 0.03, FLOOR), (x0 + w * (0.25 + 0.5 * k) + 0.03, y1 + 0.03, 0.65), 'iron', 'interior')
    OBS.append((x0, y0, x1, y1))
    if search:
        SEARCH.append(search + ((x0 + x1) / 2, (y0 + y1) / 2))


def crates(name, x, y, part='interior', search=('Cajones', 'los cajones', 'cajon')):
    z0 = FLOOR if part == 'interior' else 0.0
    for k, (dx, dy, z) in enumerate(((0.0, 0.0, 0.0), (0.55, 0.05, 0.0), (0.25, 0.02, 0.5))):
        boxc('%s_%d' % (name, k), (x + dx, y + dy, z0 + z + 0.25), (0.25, 0.25, 0.25), 'boards', part, rot=(0, 0, 0.12 * k - 0.1))
    OBS.append((x - 0.28, y - 0.3, x + 0.83, y + 0.33))
    if search:
        SEARCH.append(search + (x + 0.27, y))


def sack(name, x, y, part='interior', mat='linen'):
    rock(name, (x, y, (FLOOR if part == 'interior' else 0.0) + 0.18), (0.25, 0.2, 0.28), mat, part, seed=len(name) * 7 + int(x * 10), squash=1.0)


def pelt_rug(cx, cy, rot, mat='pelt'):
    bm = bmesh.new()
    pts = [(0.0, 0.62), (0.22, 0.45), (0.5, 0.5), (0.42, 0.18), (0.36, -0.35), (0.55, -0.6), (0.18, -0.48), (0.0, -0.7),
           (-0.18, -0.48), (-0.55, -0.6), (-0.36, -0.35), (-0.42, 0.18), (-0.5, 0.5), (-0.22, 0.45)]
    c = bm.verts.new((0, 0, 0.012))
    vs = [bm.verts.new((x, y, 0.012)) for x, y in pts]
    for i in range(len(vs)):
        bm.faces.new((c, vs[i], vs[(i + 1) % len(vs)]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    for f in bm.faces:
        if f.normal.z < 0:
            f.normal_flip()
    _obj('pelt_rug_%d' % int(cx * 10), bm, mat, 'interior', loc=(cx, cy, FLOOR), rot=(0, 0, rot))


# ------------------------------------------------------------------------------------------------ styles

def setup(style):
    S.clear()
    OBS.clear()
    SEARCH.clear()
    EXTRA_PARTS.clear()
    S['style'] = style
    if style == 'cazador':
        S.update(name='CabanaCazador', X=2.8, Y=2.2, t=0.26, H=2.35, ridge=4.2, door=(-0.5, 0.5), door_h=2.0,
                 wins={'S': [(1.3, 1.9, 1.05, 1.5)], 'W': [(-0.3, 0.3, 1.05, 1.45)]}, wall_x='logs', wall_y='logs')
    elif style == 'soto':
        S.update(name='CabanaSoto', X=2.5, Y=2.0, t=0.14, H=2.25, ridge=3.8, door=(-0.45, 0.45), door_h=1.95,
                 wins={'S': [(1.0, 1.6, 1.0, 1.45)], 'W': [(-0.3, 0.3, 1.0, 1.4)]}, wall_x='grey_x', wall_y='grey', stone_to=0.45,
                 boarded=True)
    elif style == 'marisma':
        S.update(name='CabanaMarisma', X=2.4, Y=1.9, t=0.16, H=1.95, ridge=3.9, door=(-0.45, 0.45), door_h=1.8,
                 wins={'W': [(-0.25, 0.25, 1.0, 1.35)]}, wall_x='mud', wall_y='mud')
    elif style == 'posada':
        S.update(name='Posada', X=4.6, Y=3.2, t=0.24, H=3.0, ridge=5.6, door=(-0.6, 0.6), door_h=2.15,
                 wins={'S': [(-3.4, -2.5, 1.2, 1.9), (2.0, 2.9, 1.2, 1.9)], 'W': [(-0.5, 0.4, 1.2, 1.85)], 'E': [(-1.6, -0.8, 1.2, 1.8)]},
                 wall_x='plaster', wall_y='plaster', stone_to=1.0)
    elif style == 'molino':
        S.update(name='Molino', X=2.5, Y=2.1, t=0.4, H=2.8, ridge=5.0, door=(-0.5, 0.5), door_h=2.0,
                 wins={'S': [(1.15, 1.7, 1.3, 1.75)], 'W': [(-0.3, 0.3, 1.2, 1.65)]}, wall_x='stone', wall_y='stone')


def shell():
    """Walls, frames, gables and floor of the current style; returns the openings."""
    d0, d1 = S['door']
    ops = {'S': [(d0, d1, 0.0, S['door_h'])], 'N': [], 'E': [], 'W': []}
    for side, lst in S['wins'].items():
        ops[side] += lst
    for side in 'SNEW':
        if S['wall_x'] == 'logs':
            log_wall(side, ops[side])
        else:
            wall(side, ops[side])
        frames(side, ops[side], door_u=(d0, d1) if side == 'S' else None)
    gm = {'logs': 'boards', 'grey_x': 'grey', 'mud': 'mud', 'plaster': 'plaster', 'stone': 'stone'}[S['wall_x']]
    gable('W', gm)
    gable('E', gm)
    return ops


def timber_frame():
    """Dark beams over the plaster of the posada: posts, a rail and braces on the outer faces."""
    X, Y, t, H = S['X'], S['Y'], S['t'], S['H']
    for side in 'SNEW':
        along_x = side in 'SN'
        w = (-Y if side == 'S' else Y) if along_x else (-X if side == 'W' else X)
        o = -1 if side in 'SW' else 1
        L = X if along_x else Y
        part = 'wall_%s_up' % side
        us = [-L + 0.05, -L / 2, 0.0, L / 2, L - 0.05] if along_x else [-L + 0.05, 0.0, L - 0.05]
        for k, u in enumerate(us):
            if side == 'S' and any(a - 0.1 < u < b + 0.1 for (a, b, _, _) in S['wins'].get('S', []) + [(S['door'][0], S['door'][1], 0, 0)]):
                continue
            if side in 'EW' and any(a - 0.1 < u < b + 0.1 for (a, b, _, _) in S['wins'].get(side, [])):
                continue
            if along_x:
                box('post_%s_%d' % (side, k), (u - 0.08, w + o * t / 2 - 0.03, 1.0), (u + 0.08, w + o * t / 2 + 0.03, H), 'beam', part)
            else:
                box('post_%s_%d' % (side, k), (w + o * t / 2 - 0.03, u - 0.08, 1.0), (w + o * t / 2 + 0.03, u + 0.08, H), 'beam', part)
        for k, z in enumerate((1.0, 2.05, H - 0.1)):
            if along_x:
                box('rail_%s_%d' % (side, k), (-L - 0.1, w + o * t / 2 - 0.03, z - 0.07), (L + 0.1, w + o * t / 2 + 0.03, z + 0.07), 'beam', part)
            else:
                box('rail_%s_%d' % (side, k), (w + o * t / 2 - 0.03, -L, z - 0.07), (w + o * t / 2 + 0.03, L, z + 0.07), 'beam', part)
        # plaster fallen off in patches: the wattle shows
        rnd = random.Random(ord(side) * 3)
        for k in range(2):
            u = rnd.uniform(-L + 0.6, L - 0.6)
            z = rnd.uniform(1.3, 2.4)
            if along_x:
                box('bare_%s_%d' % (side, k), (u - 0.3, w + o * (t / 2 + 0.005) - 0.004, z - 0.2), (u + 0.3, w + o * (t / 2 + 0.005) + 0.004, z + 0.2), 'wattle', part)
            else:
                box('bare_%s_%d' % (side, k), (w + o * (t / 2 + 0.005) - 0.004, u - 0.3, z - 0.2), (w + o * (t / 2 + 0.005) + 0.004, u + 0.3, z + 0.2), 'wattle', part)


def style_cazador():
    X, Y, t, H = S['X'], S['Y'], S['t'], S['H']
    shell()
    floor()
    roof('shingle', rows=7, over_y=0.6, over_x=0.5)
    # stone chimney outside the east gable
    box('chim_base', (X + 0.12, 0.3, 0.0), (X + 0.8, 1.2, 2.6), 'stone', 'chimney')
    box('chim_stack', (X + 0.16, 0.42, 2.6), (X + 0.68, 1.08, S['ridge'] + 0.7), 'stone', 'chimney')
    box('chim_cap', (X + 0.12, 0.38, S['ridge'] + 0.7), (X + 0.72, 1.12, S['ridge'] + 0.8), 'stone_dark', 'chimney')
    box('chim_hole', (X + 0.24, 0.5, S['ridge'] + 0.8), (X + 0.6, 1.0, S['ridge'] + 0.81), 'soot', 'chimney')
    door_leaf(*S['door'], S['door_h'])
    fire = hearth_east(0.25, 1.25)
    bed(-X + 0.2, Y - 1.05, -X + 2.05, Y - 0.2, 'pelt')
    pelt_rug(-0.1, -0.2, 0.3)
    table(-0.75, 0.15, 0.45, 0.8, 11)
    stool(-0.15, -0.25, 1)
    stool(0.75, 0.45, 2)
    shelf_north(0.1, 1.7, 1.45, 12, search=('Estante', 'el estante', 'estante'))
    chest('chest', -X + 0.2, -Y + 0.2, -X + 0.95, -Y + 0.7)
    barrel('barrel', X - 0.45, -Y + 0.45, search=('Barril', 'el barril', 'barril'))
    # bows and a spear on the north wall, an antler trophy above the bed
    y = Y - t / 2 - 0.04
    for k, x in enumerate((-0.4, -0.05)):
        for j in range(6):
            a0, a1 = -1.2 + j * 0.4, -1.2 + (j + 1) * 0.4
            cyl('bow_%d_%d' % (k, j), (x + 0.12 * math.cos(a0) * 0.6, y, 1.55 + 0.45 * math.sin(a0) * 0.9),
                (x + 0.12 * math.cos(a1) * 0.6, y, 1.55 + 0.45 * math.sin(a1) * 0.9), 0.015, 'wood', 'interior', sides=4)
        cyl('bowstring_%d' % k, (x + 0.03, y + 0.01, 1.55 - 0.42), (x + 0.03, y + 0.01, 1.55 + 0.42), 0.004, 'linen', 'interior', sides=3)
    cyl('spear', (0.35, y, 0.1), (0.42, y, 2.05), 0.02, 'wood', 'interior', sides=4)
    cyl('spear_tip', (0.42, y, 2.05), (0.43, y, 2.25), 0.035, 'iron', 'interior', sides=4, r1=0.003)
    antlers(-X + 1.1, y, 1.7, 'interior', 1)
    # outside: pelts stretched on frames against the west wall, a drying rack, a woodpile, a chopping block
    for k, yy in enumerate((-1.2, 0.9)):
        fx = -X - 0.32
        for j, (a, b) in enumerate((((-0.5, 0.3), (0.5, 0.3)), ((-0.5, 1.5), (0.5, 1.5)), ((-0.5, 0.3), (-0.5, 1.5)), ((0.5, 0.3), (0.5, 1.5)))):
            cyl('pframe_%d_%d' % (k, j), (fx, yy + a[0], a[1]), (fx, yy + b[0], b[1]), 0.025, 'wood', 'ext', sides=4)
        box('pelt_%d' % k, (fx - 0.02, yy - 0.42, 0.38), (fx + 0.01, yy + 0.42, 1.42), 'pelt' if k else 'pelt_d', 'ext')
    rx, ry = -1.6, -Y - 1.4
    for k, x in enumerate((rx - 0.9, rx + 0.9)):
        cyl('rack_post_%d' % k, (x, ry, 0.0), (x, ry, 1.5), 0.04, 'wood', 'ext', sides=5)
    cyl('rack_bar', (rx - 0.95, ry, 1.45), (rx + 0.95, ry, 1.45), 0.025, 'wood', 'ext', sides=4)
    for k in range(5):
        x = rx - 0.7 + k * 0.35
        box('strip_%d' % k, (x - 0.04, ry - 0.015, 0.95 + 0.05 * (k % 2)), (x + 0.04, ry + 0.015, 1.43), 'meat', 'ext')
    OBS.append((rx - 1.0, ry - 0.1, rx + 1.0, ry + 0.1))
    for row in range(3):
        for k in range(6 - row):
            yy = -Y + 0.15 + k * 0.24 + row * 0.12
            cyl('wood_%d_%d' % (row, k), (X + 0.25, yy - 1.75 + 1.4, 0.1 + row * 0.18), (X + 0.95, yy - 1.75 + 1.4, 0.1 + row * 0.18), 0.09, 'logs', 'ext', sides=6)
    OBS.append((X + 0.2, -Y - 0.25, X + 1.0, 0.1))
    cyl('block', (1.5, -Y - 1.5, 0.0), (1.5, -Y - 1.5, 0.5), 0.28, 'wood', 'ext', sides=8)
    cyl('axe_haft', (1.45, -Y - 1.45, 0.5), (1.3, -Y - 1.25, 1.0), 0.02, 'wood', 'ext', sides=4)
    boxc('axe_head', (1.47, -Y - 1.48, 0.53), (0.09, 0.02, 0.06), 'iron', 'ext', rot=(0, 0.3, 0.6))
    OBS.append((1.2, -Y - 1.8, 1.8, -Y - 1.2))
    S['rest'] = (fire[0] - 0.75, fire[1])
    S['smoke'] = [(X + 0.42, 0.75, S['ridge'] + 0.86)]
    S['lights'] = [((fire[0] + 0.2, fire[1], 0.6), 4.2, (1.0, 0.6, 0.3), 0.85, 0.45)]


def antlers(x, y, z, part, seed):
    boxc('antler_plaque_%d' % seed, (x, y, z), (0.14, 0.02, 0.1), 'wood', part)
    for s in (-1, 1):
        p0 = Vector((x + s * 0.06, y - 0.03, z + 0.05))
        p1 = p0 + Vector((s * 0.25, -0.05, 0.25))
        p2 = p1 + Vector((s * 0.12, 0.0, 0.25))
        cyl('antler_%d_%d_a' % (seed, s), p0, p1, 0.025, 'bone', part, sides=4)
        cyl('antler_%d_%d_b' % (seed, s), p1, p2, 0.02, 'bone', part, sides=4, r1=0.008)
        cyl('antler_%d_%d_c' % (seed, s), p1, p1 + Vector((s * -0.05, -0.02, 0.2)), 0.015, 'bone', part, sides=4, r1=0.006)


def style_soto():
    X, Y, t, H = S['X'], S['Y'], S['t'], S['H']
    shell()
    floor('dirt')
    # the east third of the roof burnt away: charred rafters show, a burnt beam fell inside
    holes = {(1, r, 2) for r in range(5)} | {(-1, r, 2) for r in range(3)} | {(1, 0, 1)}
    roof('shingle', rows=6, holes=holes, over_y=0.5, over_x=0.4)
    cyl('fallen_beam', (1.1, -1.2, FLOOR + 0.05), (2.2, 0.9, 1.4), 0.08, 'char', 'interior', sides=6)
    for k, (x, y) in enumerate(((1.6, 0.2), (1.9, -0.6), (1.2, 1.0))):
        blob('debris_%d' % k, 'interior', (x, y, FLOOR), (0.35, 0.3, 0.12), 300 + k, 'char', n=16)
    # soot on the outside of the burnt end
    for side, w in (('S', -Y - t / 2 - 0.005), ('N', Y + t / 2 + 0.005)):
        box('scorch_%s' % side, (X - 1.3, w - 0.004, 0.6), (X + 0.08, w + 0.004, H), 'soot', 'wall_%s_up' % side)
    box('scorch_E', (X + t / 2 + 0.003, -Y, 0.7), (X + t / 2 + 0.01, Y, H + 0.6), 'soot', 'wall_E_up')
    door_leaf(*S['door'], S['door_h'], mat='grey_x')
    fire_pit(0.7, 0.35)
    bed(-X + 0.25, Y - 0.95, -X + 2.0, Y - 0.25, 'wool', frame=False)
    crates('crates', X - 1.2, -Y + 0.45)
    shelf_north(-0.6, 0.9, 1.3, 21, search=('Estante', 'el estante', 'estante'), collapsed=True)
    sack('sack_a', -X + 0.5, -Y + 0.45)
    sack('sack_b', -X + 0.95, -Y + 0.4)
    cyl('pot', (0.25, 0.75, FLOOR), (0.25, 0.75, 0.25), 0.13, 'iron', 'interior', sides=8)
    # outside: charred stumps, a rain barrel, planks leaning on the wall
    for k, (x, y, r, h) in enumerate(((X + 2.2, -1.5, 0.3, 0.9), (X + 3.1, 1.2, 0.35, 0.6), (X + 1.4, 2.8, 0.25, 1.4))):
        cyl('stump_%d' % k, (x, y, 0.0), (x, y, h), r, 'char', 'ext', sides=7, r1=r * 0.8)
        OBS.append((x - r, y - r, x + r, y + r))
    barrel('rain_barrel', -X - 0.55, -1.2, 0.8, 'ext', search=('Barril', 'el barril', 'barril'))
    for k in range(3):
        boxc('lean_plank_%d' % k, (-X - 0.3, 0.6 + k * 0.28, 0.9), (0.025, 0.12, 0.9), 'grey', 'ext', rot=(0.0, -0.3, 0.05 * k))
    OBS.append((-X - 0.55, 0.4, -X - 0.1, 1.5))
    S['rest'] = (0.0, -0.45)
    S['smoke'] = [(1.7, 0.55, S['ridge'] - 0.2)]
    S['lights'] = [((0.7, 0.35, 0.5), 3.8, (1.0, 0.6, 0.3), 0.9, 0.45)]


def style_marisma():
    X, Y, t, H = S['X'], S['Y'], S['t'], S['H']
    # the hut sits on a low plank deck that runs out in front as a porch; posts sink into the mud
    dx0, dx1, dy0, dy1 = -X - 0.35, X + 0.35, -Y - 1.6, Y + 0.3
    box('deck', (dx0, dy0, 0.0), (dx1, dy1, 0.06), 'boards_f', 'floor')
    for k in range(7):
        x = dx0 + 0.1 + k * (dx1 - dx0 - 0.2) / 6
        for y in (dy0 + 0.05, dy1 - 0.05):
            cyl('stilt_%d_%.0f' % (k, y * 10), (x, y, -0.25), (x + 0.02, y, 0.05), 0.07, 'beam', 'ext', sides=6)
    for k in range(6):
        rock('mud_%d' % k, (dx0 + 0.3 + k * 0.9, dy0 - 0.1, 0.0), (0.4, 0.25, 0.05), 'mud', 'ext', seed=60 + k, squash=0.4)
    shell()
    roof('reed', rows=6, over_y=0.7, over_x=0.5, thick=0.28, sag=0.06)
    door_leaf(*S['door'], S['door_h'], mat='grey_x')
    fire_pit(0.75, 0.3, r=0.38)
    # spit with fish over the fire
    for s in (-1, 1):
        cyl('spit_post_%d' % s, (0.75 + s * 0.5, 0.3, FLOOR), (0.75 + s * 0.5, 0.3, 0.7), 0.025, 'wood', 'interior', sides=4)
    cyl('spit', (0.2, 0.3, 0.68), (1.3, 0.3, 0.68), 0.015, 'wood', 'interior', sides=4)
    for k in range(2):
        blob('spit_fish_%d' % k, 'interior', (0.6 + k * 0.3, 0.3, 0.62), (0.12, 0.04, 0.05), 400 + k, 'fish', n=12)
    bed(-X + 0.2, Y - 0.9, -X + 1.9, Y - 0.2, 'wool', frame=False)
    chest('chest', -X + 0.2, -Y + 0.2, -X + 0.85, -Y + 0.65)
    barrel('barrel', X - 0.42, Y - 0.42, search=('Barril', 'el barril', 'barril'))
    cyl('fish_basket', (X - 0.5, -Y + 0.45, FLOOR), (X - 0.5, -Y + 0.45, 0.4), 0.24, 'wicker', 'interior', sides=8, r1=0.28)
    for k in range(3):
        blob('basket_fish_%d' % k, 'interior', (X - 0.55 + 0.08 * k, -Y + 0.45, 0.42), (0.12, 0.05, 0.04), 410 + k, 'fish', n=10)
    OBS.append((X - 0.8, -Y + 0.15, X - 0.2, -Y + 0.75))
    SEARCH.append(('Canasto', 'el canasto', 'canasto', X - 0.5, -Y + 0.45))
    # nets on the north wall, eel traps
    box('net_wall', (-0.8, Y - t / 2 - 0.03, 0.5), (0.9, Y - t / 2 - 0.01, 1.7), 'net', 'interior')
    for k in range(3):
        cyl('trap_in_%d' % k, (1.2 + k * 0.05, Y - 0.35, FLOOR + 0.15 + k * 0.28), (1.85, Y - 0.4, FLOOR + 0.15 + k * 0.28), 0.14, 'wicker', 'interior', sides=7, r1=0.04)
    OBS.append((1.1, Y - 0.6, 1.9, Y - 0.1))
    # porch: nets on a frame, fish drying, a punt pole, eel traps and a ward against the Toad King
    py = -Y - 0.9
    for k, x in enumerate((-X + 0.2, -X + 1.6)):
        cyl('netpole_%d' % k, (x, py, 0.06), (x, py, 1.7), 0.04, 'beam', 'ext', sides=5)
    cyl('netline', (-X + 0.2, py, 1.65), (-X + 1.6, py, 1.62), 0.012, 'rope', 'ext', sides=3)
    box('net_hang', (-X + 0.3, py - 0.015, 0.5), (-X + 1.5, py + 0.015, 1.62), 'net', 'ext', rot=(0.05, 0, 0))
    OBS.append((-X + 0.1, py - 0.15, -X + 1.7, py + 0.15))
    cyl('fishline', (0.9, -Y - 0.25, 1.7), (2.1, -Y - 0.25, 1.7), 0.01, 'rope', 'ext', sides=3)
    for k in range(4):
        blob('dry_fish_%d' % k, 'ext', (1.0 + k * 0.33, -Y - 0.25, 1.48), (0.04, 0.03, 0.15), 420 + k, 'fish', n=10)
    cyl('punt_pole', (X + 0.25, -Y - 1.2, 0.06), (X - 0.1, -Y + 0.05, 2.9), 0.03, 'wood', 'ext', sides=4)
    for k in range(2):
        cyl('trap_out_%d' % k, (X - 0.3, -Y - 1.35 + k * 0.35, 0.2), (X - 1.0, -Y - 1.35 + k * 0.35, 0.2), 0.16, 'wicker', 'ext', sides=7, r1=0.04)
    OBS.append((X - 1.1, -Y - 1.55, X - 0.2, -Y - 0.8))
    wx, wy = -X - 0.9, -Y - 1.9
    cyl('ward_stick', (wx, wy, 0.0), (wx + 0.03, wy, 1.6), 0.025, 'wood', 'ext', sides=4)
    blob('ward_skull', 'ext', (wx + 0.03, wy, 1.68), (0.13, 0.11, 0.07), 430, 'bone', n=14)       # a frog's skull, wide and flat
    for s in (-1, 1):
        rock('ward_eye_%d' % s, (wx + 0.03 + s * 0.07, wy - 0.07, 1.7), (0.025, 0.02, 0.025), 'soot', 'ext', seed=431 + s)
    for k in range(3):
        cyl('ward_bone_%d' % k, (wx + 0.03, wy, 1.5), (wx + 0.03 + 0.07 * (k - 1), wy - 0.03, 1.2), 0.008, 'bone', 'ext', sides=3)
    S['rest'] = (0.05, -0.35)
    S['smoke'] = [(0.75, 0.0, S['ridge'] + 0.3)]
    S['lights'] = [((0.75, 0.3, 0.5), 3.6, (1.0, 0.6, 0.3), 0.85, 0.45)]
    S['deck'] = (dx0, dy0, dx1, dy1)


def style_posada():
    X, Y, t, H = S['X'], S['Y'], S['t'], S['H']
    shell()
    timber_frame()
    floor()
    # a hole in the south slope and the east end sagging
    holes = {(-1, r, 1) for r in (1, 2, 3)} | {(-1, 2, 0)}
    roof('shingle', rows=8, holes=holes, sag=0.12, over_y=0.6, over_x=0.5)
    # big chimney behind the north wall, hearth inside
    cx0, cx1 = -3.2, -1.9
    box('chim', (cx0 + 0.05, Y + t / 2, 0.0), (cx1 - 0.05, Y + t / 2 + 0.7, S['ridge'] + 0.6), 'stone', 'chimney')
    box('chim_cap', (cx0, Y + t / 2 - 0.05, S['ridge'] + 0.6), (cx1, Y + t / 2 + 0.75, S['ridge'] + 0.7), 'stone_dark', 'chimney')
    box('chim_hole', (cx0 + 0.2, Y + t / 2 + 0.15, S['ridge'] + 0.7), (cx1 - 0.2, Y + t / 2 + 0.55, S['ridge'] + 0.71), 'soot', 'chimney')
    fire = hearth_north(cx0, cx1)
    door_leaf(*S['door'], S['door_h'], hang=0.1)
    # hanging sign
    sx = 1.4
    cyl('sign_arm', (sx, -Y - t / 2, 2.65), (sx, -Y - 1.0, 2.65), 0.035, 'beam', 'wall_S_up', sides=5)
    cyl('sign_brace', (sx, -Y - t / 2, 2.25), (sx, -Y - 0.7, 2.62), 0.025, 'beam', 'wall_S_up', sides=4)
    for k, y in enumerate((-Y - 0.35, -Y - 0.9)):
        cyl('sign_chain_%d' % k, (sx, y, 2.62), (sx, y, 2.42), 0.008, 'iron', 'wall_S_up', sides=3)
    boxc('sign_board', (sx, -Y - 0.62, 2.12), (0.04, 0.36, 0.3), 'sign', 'wall_S_up', rot=(0.06, 0, 0))
    cyl('sign_mug', (sx - 0.05, -Y - 0.62, 1.98), (sx - 0.05, -Y - 0.62, 2.24), 0.09, 'paint', 'wall_S_up', sides=8)
    cyl('sign_mug_h', (sx - 0.05, -Y - 0.48, 2.05), (sx - 0.05, -Y - 0.48, 2.18), 0.03, 'paint', 'wall_S_up', sides=4)
    # bar along the east wall, barrels behind it, a shelf of mugs
    bx0, bx1, by0, by1 = 2.3, 2.85, -1.9, 1.7
    box('bar_top', (bx0 - 0.06, by0, 1.0), (bx1, by1, 1.08), 'wood_light', 'interior')
    box('bar_front', (bx0, by0, FLOOR), (bx0 + 0.1, by1, 1.0), 'boards', 'interior')
    box('bar_end', (bx0, by0, FLOOR), (X - t / 2, by0 + 0.1, 1.0), 'boards_x', 'interior')
    OBS.append((bx0 - 0.06, by0, bx1, by1))
    OBS.append((bx0, by0, X - t / 2, by0 + 0.12))
    SEARCH.append(('Barra', 'la barra', 'despensa', bx0, 0.0))
    for k in range(3):
        y = -1.2 + k * 0.85
        cyl('cask_%d' % k, (X - t / 2 - 0.05, y, 0.65), (X - t / 2 - 0.9, y, 0.65), 0.34, 'boards', 'interior', sides=10)
        box('cradle_%d' % k, (X - t / 2 - 0.9, y - 0.3, FLOOR), (X - t / 2 - 0.05, y + 0.3, 0.35), 'wood', 'interior')
    SEARCH.append(('Barriles', 'los barriles', 'barril', X - 0.6, 1.25))
    y = Y - t / 2
    box('mug_shelf', (2.1, y - 0.25, 1.6), (X - t / 2, y, 1.64), 'wood_light', 'interior')
    for k in range(6):
        cyl('mug_%d' % k, (2.25 + k * 0.32, y - 0.12, 1.64), (2.25 + k * 0.32, y - 0.12, 1.8), 0.06, 'clay' if k % 2 else 'wood', 'interior', sides=6)
    SEARCH.append(('Estante', 'el estante', 'estante', 3.2, y - 0.3))
    # tables, benches, one overturned; broken mugs
    table(-1.4, -1.6, 0.0, -0.95, 31)
    bench(-1.3, -0.1, -2.2, 1)
    table(-1.0, 0.2, 0.4, 0.85, 32)
    bench(-0.9, 0.3, 1.25, 2)
    table(0.6, -1.0, 1.8, -0.35, 33, overturned=True)
    for k in range(4):
        rock('shard_%d' % k, (0.8 + k * 0.25, -1.6 + 0.1 * k, FLOOR + 0.01), (0.05, 0.04, 0.02), 'clay', 'interior', seed=500 + k, squash=0.5)
    # stairs up along the north wall to a floor that has fallen in
    for k in range(9):
        x = 1.9 - k * 0.27
        box('step_%d' % k, (x - 0.27, Y - t / 2 - 0.85, FLOOR), (x, Y - t / 2 - 0.05, FLOOR + 0.28 * (k + 1)), 'wood', 'interior')
    OBS.append((1.9 - 9 * 0.27, Y - t / 2 - 0.9, 1.9, Y - t / 2))
    for k in range(4):
        x = -0.6 + k * 0.9
        cyl('joist_%d' % k, (x, Y - t / 2, H - 0.1), (x, Y - 1.4 - 0.3 * (k % 2), H - 0.1 - 0.15 * (k == 3)), 0.07, 'beam', 'interior', sides=5)
    boxc('upper_boards', (0.7, Y - 0.7, H - 0.02), (1.5, 0.55, 0.03), 'boards_f', 'interior', rot=(0.05, 0.02, 0))
    for k, (x, y, r) in enumerate(((-0.4, -0.3, 0.4), (0.3, 0.1, -0.7), (-0.1, -0.6, 1.2))):
        boxc('fallen_plank_%d' % k, (x, y, FLOOR + 0.03 + 0.02 * k), (0.9, 0.1, 0.025), 'boards_f', 'interior', rot=(0, 0, r))
    chest('chest', -X + 0.25, Y - 0.85, -X + 1.0, Y - 0.25)
    # outside: the stable lean-to on the east, collapsed; barrels by the door
    lx = X + 0.1
    for k, y in enumerate((-2.2, 0.0, 2.2)):
        cyl('lean_post_%d' % k, (lx + 2.0, y, 0.0), (lx + 2.0 + 0.1 * (k - 1), y, 1.9 - 0.6 * (k == 2)), 0.08, 'beam', 'ext', sides=6)
        OBS.append((lx + 1.85, y - 0.15, lx + 2.15, y + 0.15))
    boxc('lean_roof_a', (lx + 1.0, -1.0, 2.15), (1.1, 1.4, 0.04), 'grey_x', 'ext', rot=(0, 0.25, 0))
    boxc('lean_roof_b', (lx + 1.2, 1.4, 1.2), (1.1, 1.1, 0.04), 'grey_x', 'ext', rot=(0.45, 0.3, 0.2))
    box('trough', (lx + 0.5, -2.0, 0.0), (lx + 1.6, -1.6, 0.45), 'wood', 'ext')
    OBS.append((lx + 0.5, -2.0, lx + 1.6, -1.6))
    for k in range(3):
        blob('hay_%d' % k, 'ext', (lx + 0.8 + 0.4 * k, 0.6 + 0.3 * k, 0.0), (0.45, 0.4, 0.3), 520 + k, 'straw', n=16)
    barrel('door_barrel_a', 1.1, -Y - 0.5, 0.8, 'ext')
    barrel('door_barrel_b', 1.6, -Y - 0.45, 0.7, 'ext')
    S['rest'] = (fire[0], fire[1] - 0.8)
    S['smoke'] = [((cx0 + cx1) / 2, Y + t / 2 + 0.35, S['ridge'] + 0.76)]
    S['lights'] = [((fire[0], fire[1] + 0.2, 0.7), 4.8, (1.0, 0.6, 0.3), 0.9, 0.45)]


def style_molino():
    X, Y, t, H = S['X'], S['Y'], S['t'], S['H']
    shell()
    floor('dirt')
    roof('shingle', rows=7, sag=0.1, over_y=0.55, over_x=0.45)
    door_leaf(*S['door'], S['door_h'])
    # the water wheel on the east side, its axle into the wall; it turns (role 'spin', see PARTS)
    ax, az, R = X + 0.95, 1.3, 2.25
    EXTRA_PARTS['wheel'] = {'group': 'cabin', 'role': 'spin', 'pivot': (ax, 0.0, az), 'axis': (1, 0, 0), 'speed': -14.0}
    cyl('axle', (X - t / 2 - 0.9, 0.0, az), (ax + 0.45, 0.0, az), 0.12, 'beam', 'wheel', sides=8)
    n = 16
    for side in (-0.26, 0.26):
        for k in range(n):
            a0 = 2 * math.pi * k / n
            a1 = 2 * math.pi * (k + 1) / n
            p0 = Vector((ax + side, R * math.cos(a0), az + R * math.sin(a0)))
            p1 = Vector((ax + side, R * math.cos(a1), az + R * math.sin(a1)))
            cyl('rim_%.0f_%d' % (side * 10, k), p0, p1, 0.06, 'wood', 'wheel', sides=5)
    for k in range(8):
        a = 2 * math.pi * k / 8
        for side in (-0.26, 0.26):
            cyl('spoke_%.0f_%d' % (side * 10, k), (ax + side, 0, az), (ax + side, R * math.cos(a), az + R * math.sin(a)), 0.05, 'wood', 'wheel', sides=5)
    for k in range(n):
        a = 2 * math.pi * (k + 0.5) / n
        c = Vector((ax, (R - 0.12) * math.cos(a), az + (R - 0.12) * math.sin(a)))
        boxc('paddle_%d' % k, c, (0.3, 0.03, 0.22), 'grey', 'wheel', rot=(a, 0, 0))
    # the stone pier that carries the outer end of the axle, standing in the water
    box('wheel_pier', (ax + 0.55, -0.35, -1.2), (ax + 1.0, 0.35, az + 0.15), 'stone', 'ext')
    box('wheel_wall_n', (X + 0.1, R + 0.1, -1.2), (ax + 1.0, R + 0.5, 0.2), 'stone', 'ext')
    box('wheel_wall_s', (X + 0.1, -R - 0.5, -1.2), (ax + 1.0, -R - 0.1, 0.2), 'stone', 'ext')
    # inside: the pit wheel on the axle, millstones under a hopper, sacks of flour, a fire for travellers
    gx = X - t / 2 - 0.6
    for k in range(12):
        a0 = 2 * math.pi * k / 12
        a1 = 2 * math.pi * (k + 1) / 12
        cyl('gear_%d' % k, (gx, 0.95 * math.cos(a0), az + 0.95 * math.sin(a0)), (gx, 0.95 * math.cos(a1), az + 0.95 * math.sin(a1)), 0.08, 'wood', 'wheel', sides=5)
        boxc('cog_%d' % k, (gx - 0.1, 1.0 * math.cos(a0), az + 1.0 * math.sin(a0)), (0.08, 0.05, 0.05), 'wood_light', 'wheel', rot=(a0, 0, 0))
    for k in range(4):
        a = k * math.pi / 2
        cyl('gear_spoke_%d' % k, (gx, 0, az), (gx, 0.95 * math.cos(a), az + 0.95 * math.sin(a)), 0.05, 'wood', 'wheel', sides=5)
    mx, my = 0.6, -0.2
    box('mill_deck', (mx - 0.9, my - 0.9, FLOOR), (mx + 0.9, my + 0.9, 0.45), 'boards_x', 'interior')
    cyl('stone_bed', (mx, my, 0.45), (mx, my, 0.62), 0.72, 'stone', 'interior', sides=14)
    cyl('stone_run', (mx, my, 0.63), (mx, my, 0.82), 0.7, 'stone', 'interior', sides=14)
    cyl('stone_eye', (mx, my, 0.82), (mx, my, 0.83), 0.12, 'soot', 'interior', sides=8)
    hz = 1.35
    bm = bmesh.new()
    top = [bm.verts.new((mx + sx * 0.42, my + sy * 0.42, hz + 0.55)) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    bot = [bm.verts.new((mx + sx * 0.08, my + sy * 0.08, hz)) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    for i in range(4):
        j = (i + 1) % 4
        bm.faces.new((top[i], top[j], bot[j], bot[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    _obj('hopper', bm, 'boards', 'interior')
    for k, (sx, sy) in enumerate(((-1, -1), (1, -1), (1, 1), (-1, 1))):
        cyl('hopper_leg_%d' % k, (mx + sx * 0.4, my + sy * 0.4, 0.45), (mx + sx * 0.4, my + sy * 0.4, hz + 0.55), 0.035, 'wood', 'interior', sides=4)
    box('flour_dust', (mx - 1.3, my - 1.2, FLOOR), (mx + 1.2, my + 1.1, FLOOR + 0.008), 'flour', 'interior')
    OBS.append((mx - 0.9, my - 0.9, mx + 0.9, my + 0.9))
    OBS.append((gx - 0.25, -1.1, X - t / 2, 1.1))
    for k, (x, y) in enumerate(((-0.6, -Y + 0.45), (-1.05, -Y + 0.4), (-0.8, -Y + 0.85))):
        sack('flour_sack_%d' % k, x, y, mat='flour')
    OBS.append((-1.35, -Y + 0.15, -0.35, -Y + 1.1))
    SEARCH.append(('Sacos de harina', 'los sacos', 'despensa', -0.8, -Y + 0.6))
    crates('crates', -X + 0.4, Y - 0.6)
    fire_pit(-1.15, 0.45, r=0.38)
    # ladder to the loft
    for s in (-0.22, 0.22):
        cyl('ladder_%d' % (s > 0), (1.6 + s, Y - 0.35, FLOOR), (1.6 + s, Y - 0.15, H), 0.03, 'wood', 'interior', sides=4)
    for k in range(7):
        z = 0.3 + k * 0.35
        cyl('rung_%d' % k, (1.38, Y - 0.35 + 0.2 * z / H, z), (1.82, Y - 0.35 + 0.2 * z / H, z), 0.02, 'wood', 'interior', sides=4)
    OBS.append((1.3, Y - 0.45, 1.9, Y - t / 2))
    # outside: a spare millstone against the wall, sacks
    cyl('spare_stone', (-1.6, -Y - 0.32, 0.72), (-1.6, -Y - 0.12, 0.72), 0.7, 'stone', 'ext', sides=14)
    OBS.append((-2.3, -Y - 0.4, -0.9, -Y - 0.05))
    S['rest'] = (-1.15, -0.35)
    S['smoke'] = [(-1.15, 0.45, S['ridge'] + 0.4)]                # through a smoke hole by the ridge
    S['lights'] = [((-1.15, 0.45, 0.5), 3.8, (1.0, 0.6, 0.3), 0.85, 0.45)]
    S['wheel'] = (ax, R)


STYLES = {'cazador': style_cazador, 'soto': style_soto, 'marisma': style_marisma, 'posada': style_posada, 'molino': style_molino}

PARTS = {
    'roof': {'group': 'cabin', 'role': 'roof'},
    'chimney': {'group': 'cabin', 'role': 'chimney'},
    'floor': {'group': 'cabin', 'role': 'static', 'shadow': False},
    'interior': {'group': 'cabin', 'role': 'static', 'shadow': False},
    'ext': {'group': 'cabin', 'role': 'static'},
    'glow': {'group': 'cabin', 'role': 'static', 'glowColor': 'd2ac88'},
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
    STYLES[style]()
    for side in 'SNEW':
        n = {'S': (0, -1, 0), 'N': (0, 1, 0), 'E': (1, 0, 0), 'W': (-1, 0, 0)}[side]
        PARTS['wall_%s_up' % side] = {'group': 'cabin', 'role': 'cut', 'normal': n}
        PARTS['wall_%s_base' % side] = {'group': 'cabin', 'role': 'static'}
    for k in ('door', 'wheel'):
        PARTS.pop(k, None)
    PARTS.update(EXTRA_PARTS)
    bpy.context.view_layer.update()
    return len(PIECES)


def ground():
    pmat('ground_ao', 'flat', ['3d332b', '3d332b', '3d332b'])
    bpy.ops.mesh.primitive_plane_add(size=40, location=(0, 0, 0))
    g = bpy.context.active_object
    g.name = 'ground_ao'
    g.data.materials.append(MATS['ground_ao'])
    if S.get('wheel'):
        # the mill's wheel stands in water: no ground under it
        g.location.x = -20 + S['wheel'][0] - 0.2
    return [g]


def meta():
    X, Y, t = S['X'], S['Y'], S['t']
    d0, d1 = S['door']
    obs = [UB(-X - t / 2, Y - t / 2, X + t / 2, Y + t / 2), UB(X - t / 2, -Y - t / 2, X + t / 2, Y + t / 2),
           UB(-X - t / 2, -Y - t / 2, -X + t / 2, Y + t / 2), UB(-X - t / 2, -Y - t / 2, d0 - 0.03, -Y + t / 2),
           UB(d1 + 0.03, -Y - t / 2, X + t / 2, -Y + t / 2)]
    if S['style'] == 'cazador':
        obs.append(UB(X + 0.12, 0.3, X + 0.8, 1.2))
    if S['style'] == 'posada':
        obs.append(UB(-3.2, Y + t / 2, -1.9, Y + t / 2 + 0.7))
    if S['style'] == 'molino':
        ax, R = S['wheel']
        obs += [UB(X + 0.1, -R - 0.5, ax + 1.0, -R - 0.1), UB(X + 0.1, R + 0.1, ax + 1.0, R + 0.5), UB(X, -R - 0.1, ax + 1.0, R + 0.1)]
    obs += [UB(*o) for o in OBS]
    info = {
        'name': S['name'],
        'footprint': UB(-X + t / 2, -Y + t / 2, X - t / 2, Y - t / 2),
        'obstacles': {'cabin': obs},
        'door': {'closed': UB(d0, -Y - 0.12, d1, -Y + 0.12), 'openYaw': 100, 'locked': False, 'health': 50,
                 'prompt': U((d0 + d1) / 2, -Y - 0.5, 0), 'inside': U((d0 + d1) / 2, -Y + 0.6, 0)},
        'lights': [{'group': 'cabin', 'pos': U(*p), 'radius': r, 'color': list(c), 'intensity': i, 'flicker': f}
                   for (p, r, c, i, f) in S['lights']],
        'anchors': {'rest': U(S['rest'][0], S['rest'][1], 0)},
        'smoke': [U(*p) for p in S['smoke']],
        'searchables': [{'name': n, 'what': w, 'table': tb, 'at': U(x, y, 0), 'group': 'cabin'} for (n, w, tb, x, y) in SEARCH],
    }
    if S.get('wheel'):
        # where the wheel stands, for MapWorld to put it in the river (real metres, along local +x)
        info['wheelX'] = S['wheel'][0]
    return info
