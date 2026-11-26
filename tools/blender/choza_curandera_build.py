# Barro y Bruma - the healer's hut (Cabana de la curandera, refuge k5, at the edge of the swamp).
# Built with edificio_kit.py (exec it first in the same namespace), baked and exported like the woodcutter's cabin:
#   build_all(); unwrap(PIECES); bake([PIECES], 1024, atlas); export(json, meta(), PARTS)
# Real metres; BuildingImporter scales it by 1.4 to match the characters.
#
# A crooked forest hut: wattle-and-daub walls in a frame of rough, leaning posts on a stone footing, the daub
# fallen off in patches so the woven sticks show; a steep, sagging thatch roof with moss, ragged eaves and a hole
# where the rafters show; a leaning stone chimney on the east gable and a log propping the wall; a lean-to on the
# west with firewood and herbs drying. Inside: a fire pit with a cauldron on a tripod (green brew), the counter
# where she attends, shelves crowded with jars, bunches of herbs hanging from a beam, a straw bed, baskets.
# Outside, east: her garden, three raised beds (cabbages, leeks, herbs with flowers, beans on poles) inside a low
# wattle fence, and a scarecrow.
import math, random
from mathutils import Vector

X0, X1, Y0, Y1 = -2.3, 2.3, -1.8, 1.8
T = 0.14                                       # wall thickness
WALL_H = 2.1
RIDGE = 3.7
FLOOR = 0.05
BASE = 0.5                                     # the low strip of wall that never fades
DOOR = (-0.55, 0.4)
DOOR_TOP = 1.85
WIN_S = (1.2, 1.7, 1.05, 1.45)                # u0, u1, z0, z1
WIN_W = (-0.35, 0.35, 1.0, 1.45)

LEAF_GREENS = ['1f2a1f', '2b3a2a', '3b4a33', '4f5c40', '666043', '7e7752']
for _h in LEAF_GREENS:
    if _h not in PALETTE:
        PALETTE.append(_h)


def mats():
    pmat('daub', 'noise', ['3d332b', '594a3c', '7a6650', '7a6650', '9c8a70'], scale=2.2, grime=0.5, detail=4.0)
    pmat('wattle', 'stripes', ['141210', '2a2420', '3d332b', '594a3c'], scale=9.0, dir='X')
    pmat('timber', 'logs', ['141210', '2a2420', '3d332b', '594a3c'], scale=1.5, grime=0.3)
    pmat('thatch', 'fibers', ['141210', '2a2420', '3d332b', '594a3c', '7a6650'], scale=1.0, coords='object',
         stretch=(8.0, 0.6, 8.0), moss={'cols': ['2a2420', '2b3a2a', '3b4a33'], 'cut': 0.74})
    pmat('stone', 'stone', ['2a2420', '3f3e3b', '594a3c', '6e6c66'], scale=1.7, grime=0.4)
    pmat('stone_dark', 'stone', ['2a2420', '3f3e3b', '6e6c66'], scale=1.9, grime=0.2)
    pmat('boards', 'planks', ['2a2420', '3d332b', '594a3c', '7a6650'], size=(1.3, 0.17), axes='zy', mortar=0.03, grime=0.35)
    pmat('boards_x', 'planks', ['2a2420', '3d332b', '594a3c', '7a6650'], size=(1.3, 0.17), axes='zx', mortar=0.03, grime=0.35)
    pmat('boards_f', 'planks', ['2a2420', '3d332b', '594a3c'], size=(1.6, 0.2), axes='xy', mortar=0.03, grime=0.3)
    pmat('dirt', 'dirt', ['2a2420', '3d332b', '594a3c', '7a6650'], scale=1.2)
    pmat('soil', 'dirt', ['141210', '2a2420', '3d332b', '3d332b'], scale=2.5)
    pmat('soot', 'flat', ['141210', '141210', '2a2420'])
    pmat('wood', 'noise', ['2a2420', '3d332b', '594a3c'], scale=3.0)
    pmat('wood_light', 'noise', ['594a3c', '7a6650', '9c8a70'], scale=3.0)
    pmat('twig', 'noise', ['141210', '2a2420', '3d332b'], scale=4.0)
    pmat('iron', 'flat', ['141210', '2f3436', '3f3e3b'])
    pmat('straw', 'noise', ['7a6650', '9c8a70', 'b08a6a'], scale=9.0)
    pmat('wool_r', 'stripes', ['3e1a18', '6b2a24', '3d332b', '6b2a24'], scale=2.5, dir='Y')
    pmat('patch', 'stripes', ['2f3436', '4f5a5c', '594a3c', '4f5a5c'], scale=4.0, dir='X')
    pmat('linen', 'noise', ['594a3c', '6e6c66', '9a968c'], scale=5.0)
    pmat('rag', 'noise', ['3d332b', '594a3c', '6e6c66'], scale=4.0)
    pmat('clay', 'noise', ['3e1a18', '6b2a24', '8f4a36'], scale=6.0)
    pmat('glass_v', 'noise', ['2c2738', '5b5170', '5b5170'], scale=6.0)
    pmat('glass_g', 'noise', ['1f2a1f', '3b4a33', '4f5c40'], scale=6.0)
    pmat('leather', 'noise', ['141210', '2a2420', '3d332b'], scale=5.0)
    pmat('paper', 'noise', ['9a968c', 'c4bfb2', 'e6e2d4'], scale=6.0)
    pmat('bone', 'noise', ['9a968c', 'c4bfb2', 'c4bfb2'], scale=6.0)
    pmat('herbs', 'noise', ['1f2a1f', '2b3a2a', '3b4a33', '4f5c40'], scale=9.0)
    pmat('herbs_dry', 'noise', ['3d332b', '594a3c', '7a6650', '9c8a70'], scale=9.0)
    pmat('cabbage', 'noise', ['1f2a1f', '2b3a2a', '3b4a33', '4f5c40', '666043'], scale=6.0)
    pmat('leek', 'noise', ['2b3a2a', '3b4a33', '4f5c40', '666043'], scale=8.0)
    pmat('lavender', 'noise', ['2c2738', '5b5170', '5b5170'], scale=9.0)
    pmat('flower_y', 'noise', ['b08a6a', 'd2ac88', 'd2ac88'], scale=9.0)
    pmat('ember', 'noise', ['b24a36', 'd2ac88', 'b24a36'], scale=8.0, glow=True)
    pmat('flame', 'flat', ['d2ac88', 'e6e2d4', 'd2ac88'], glow=True)
    pmat('brew', 'noise', ['4f5c40', '666043', '7e7752'], scale=7.0, glow=True)


# ------------------------------------------------------------------------------------------------ helpers

def blob(name, part, c, size, seed, mat, n=24, jag=0.2, under=0.5):
    """A lumpy clump (cabbage, bunch of herbs, leaves): convex hull of jittered points."""
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


def post(name, x, y, z0, z1, r, part, lean=(0.0, 0.0), mat='timber'):
    return cyl(name, (x, y, z0), (x + lean[0], y + lean[1], z1), r, mat, part, sides=7, roll=x + y)


# ------------------------------------------------------------------------------------------------ walls

def wall(side, openings):
    """Daub infill of one wall, cut around its openings (u0, u1, z0, z1); u runs along x (S, N) or y (E, W).
    Below BASE it goes to the part that always stays, above it to the part that fades."""
    along_x = side in 'SN'
    w = (Y0 if side == 'S' else Y1) if along_x else (X0 if side == 'W' else X1)
    lo, hi = ((X0 - T / 2, X1 + T / 2) if along_x else (Y0 + T / 2, Y1 - T / 2))
    edges = sorted(set([lo, hi] + [e for o in openings for e in (o[0], o[1])]))
    k = 0

    def piece(u0, u1, z0, z1, part):
        nonlocal k
        if u1 - u0 < 0.02 or z1 - z0 < 0.02:
            return
        if along_x:
            box('daub_%s_%d' % (side, k), (u0, w - T / 2, z0), (u1, w + T / 2, z1), 'daub', part)
        else:
            box('daub_%s_%d' % (side, k), (w - T / 2, u0, z0), (w + T / 2, u1, z1), 'daub', part)
        k += 1

    for a, b in zip(edges[:-1], edges[1:]):
        m = (a + b) / 2
        hole = next((o for o in openings if o[0] <= m <= o[1]), None)
        if hole is None:
            piece(a, b, 0.0, BASE, 'wall_%s_base' % side)
            piece(a, b, BASE, WALL_H, 'wall_%s_up' % side)
        else:
            z0, z1 = hole[2], hole[3]
            if z0 > BASE:
                piece(a, b, 0.0, BASE, 'wall_%s_base' % side)
                piece(a, b, BASE, z0, 'wall_%s_up' % side)
            elif z0 > 0.0:
                piece(a, b, 0.0, z0, 'wall_%s_base' % side)
            piece(a, b, z1, WALL_H, 'wall_%s_up' % side)


def bare_patch(name, side, u0, u1, z0, z1, part, seed):
    """Daub fallen off: the woven sticks show, with a dark ragged edge."""
    along_x = side in 'SN'
    out = -1 if side in 'SW' else 1
    w = ((Y0 if side == 'S' else Y1) if along_x else (X0 if side == 'W' else X1)) + out * (T / 2 + 0.004)
    rnd = random.Random(seed)
    for j in range(3):
        a = u0 + rnd.uniform(-0.06, 0.06) * j
        b = u1 - rnd.uniform(0.0, 0.12) * j
        c = z0 + 0.05 * j
        d = z1 - rnd.uniform(0.0, 0.1) * j
        mat = 'soot' if j == 0 else 'wattle'
        o = out * 0.004 * j
        if along_x:
            box('%s_%d' % (name, j), (a, w - 0.005 + o, c), (b, w + 0.005 + o, d), mat, part)
        else:
            box('%s_%d' % (name, j), (w - 0.005 + o, a, c), (w + 0.005 + o, b, d), mat, part)
        if j == 0:
            u0, u1, z0, z1 = u0 + 0.04, u1 - 0.04, z0 + 0.04, z1 - 0.04


def frame_and_footing():
    rnd = random.Random(3)
    # stone footing all round, outside the wall line
    for side in 'SNEW':
        along_x = side in 'SN'
        w = (Y0 if side == 'S' else Y1) if along_x else (X0 if side == 'W' else X1)
        out = -1 if side in 'SW' else 1
        n = 9 if along_x else 7
        L0, L1 = (X0 - 0.15, X1 + 0.15) if along_x else (Y0 + 0.1, Y1 - 0.1)
        for i in range(n):
            u = L0 + (L1 - L0) * (i + 0.5) / n
            if side == 'S' and DOOR[0] - 0.1 < u < DOOR[1] + 0.1:
                continue
            c = (u, w + out * 0.1, 0.06) if along_x else (w + out * 0.1, u, 0.06)
            rock('footing_%s_%d' % (side, i), c, (0.28 if along_x else 0.2, 0.2 if along_x else 0.28, 0.12),
                 'stone', 'wall_%s_base' % side, seed=10 + i + ord(side), squash=0.7)
    # posts: corners and between the panels, every one a little off true
    posts = [('SW', X0, Y0, 'S'), ('SE', X1, Y0, 'E'), ('NW', X0, Y1, 'N'), ('NE', X1, Y1, 'N'),
             ('S1', DOOR[0] - 0.07, Y0, 'S'), ('S2', DOOR[1] + 0.07, Y0, 'S'), ('S3', 1.95, Y0, 'S'),
             ('N1', -1.0, Y1, 'N'), ('N2', 0.8, Y1, 'N'), ('E1', X1, 0.0, 'E'), ('W1', X0, 0.0, 'W')]
    for name, x, y, side in posts:
        lean = (rnd.uniform(-0.06, 0.06), rnd.uniform(-0.06, 0.06))
        r = 0.11 if len(name) == 2 and name[1] in 'WE' and name[0] in 'SN' else 0.085
        f = BASE / WALL_H
        post('post_%s_lo' % name, x, y, 0.0, BASE, r, 'wall_%s_base' % side, (lean[0] * f, lean[1] * f))
        post('post_%s_hi' % name, x + lean[0] * f, y + lean[1] * f, BASE, WALL_H + 0.08, r, 'wall_%s_up' % side,
             (lean[0] * (1 - f), lean[1] * (1 - f)))
    # top plates and the lintel over the door
    cyl('plate_S', (X0 - 0.2, Y0, WALL_H), (X1 + 0.2, Y0, WALL_H + 0.03), 0.09, 'timber', 'wall_S_up', sides=7)
    cyl('plate_N', (X0 - 0.2, Y1, WALL_H + 0.02), (X1 + 0.2, Y1, WALL_H), 0.09, 'timber', 'wall_N_up', sides=7)
    cyl('plate_E', (X1, Y0 - 0.15, WALL_H + 0.1), (X1, Y1 + 0.15, WALL_H + 0.1), 0.08, 'timber', 'wall_E_up', sides=7)
    cyl('plate_W', (X0, Y0 - 0.15, WALL_H + 0.1), (X0, Y1 + 0.15, WALL_H + 0.08), 0.08, 'timber', 'wall_W_up', sides=7)
    box('lintel', (DOOR[0] - 0.15, Y0 - 0.1, DOOR_TOP), (DOOR[1] + 0.15, Y0 + 0.1, DOOR_TOP + 0.12), 'timber', 'wall_S_up',
        rot=(0, 0.03, 0))
    # braces
    cyl('brace_N', (-2.2, Y1 + 0.02, 0.6), (-1.1, Y1 + 0.02, 1.85), 0.06, 'timber', 'wall_N_up', sides=6)
    cyl('brace_E', (X1 + 0.02, 0.1, 1.85), (X1 + 0.02, 1.3, 0.6), 0.06, 'timber', 'wall_E_up', sides=6)
    # windows: rough frames
    u0, u1, z0, z1 = WIN_S
    box('win_S_sill', (u0 - 0.08, Y0 - 0.12, z0 - 0.06), (u1 + 0.08, Y0 + 0.1, z0), 'timber', 'wall_S_up')
    box('win_S_head', (u0 - 0.06, Y0 - 0.1, z1), (u1 + 0.06, Y0 + 0.1, z1 + 0.06), 'timber', 'wall_S_up')
    box('win_S_shutter', (u1 + 0.02, Y0 - 0.14, z0 - 0.05), (u1 + 0.45, Y0 - 0.1, z1 + 0.02), 'boards_x', 'wall_S_up',
        rot=(0, 0.12, 0))
    u0, u1, z0, z1 = WIN_W
    box('win_W_sill', (X0 - 0.12, u0 - 0.08, z0 - 0.06), (X0 + 0.1, u1 + 0.08, z0), 'timber', 'wall_W_up')
    box('win_W_head', (X0 - 0.1, u0 - 0.06, z1), (X0 + 0.1, u1 + 0.06, z1 + 0.06), 'timber', 'wall_W_up')
    box('win_W_rag', (X0 + 0.08, u0 - 0.02, z0 + 0.05), (X0 + 0.1, u1 + 0.02, z1 - 0.02), 'rag', 'interior')
    bare_patch('bare_S', 'S', -1.95, -1.35, 0.7, 1.25, 'wall_S_up', 1)
    bare_patch('bare_S2', 'S', 0.55, 0.95, 0.55, 0.85, 'wall_S_up', 2)
    bare_patch('bare_W', 'W', 0.85, 1.5, 0.6, 1.1, 'wall_W_up', 3)
    bare_patch('bare_E', 'E', -1.45, -0.85, 1.0, 1.55, 'wall_E_up', 4)


def gable(side):
    x = X0 if side == 'W' else X1
    pts = [(Y0 - 0.1, WALL_H + 0.05), (Y1 + 0.1, WALL_H + 0.05), (0.0, RIDGE)]
    prism('gable_' + side, pts, 'x', x - T / 2, x + T / 2, 'daub', 'wall_%s_up' % side)
    o = -1 if side == 'W' else 1
    cyl('gable_post_' + side, (x + o * 0.03, 0.05, WALL_H), (x + o * 0.03, 0.0, RIDGE - 0.1), 0.06, 'timber', 'wall_%s_up' % side, sides=6)
    bare_patch('bare_gable_' + side, side, -0.55, 0.15, 2.35, 2.75, 'wall_%s_up' % side, 5 if side == 'W' else 6)


# ------------------------------------------------------------------------------------------------ roof

def roof():
    over_y, over_x = Y1 + 0.32, X1 + 0.45     # the eave stays above the door
    slope = (RIDGE - WALL_H) / (Y1 + 0.07)
    z_r = RIDGE + 0.08
    z_e = z_r - slope * over_y
    L = math.hypot(over_y, z_r - z_e)
    a = math.atan2(z_r - z_e, over_y)
    th = 0.26
    rows = 4
    rnd = random.Random(9)
    segs = [(-over_x, -over_x / 3), (-over_x / 3, over_x / 3), (over_x / 3, over_x)]
    for s in (-1, 1):
        ny, nz = s * math.sin(a), math.cos(a)
        for r in range(rows):
            t0, t1 = r / rows, (r + 1) / rows + 0.1
            for j, (x0, x1) in enumerate(segs):
                # the middle sags; the ends of the lowest row are ragged
                sag = (-0.1 if j == 1 else -0.03) + rnd.uniform(-0.025, 0.025)
                if s == -1 and r == 2 and j == 2:
                    # the hole: only half a strip left, the rafters show under the gap
                    x0 = x0 + (x1 - x0) * 0.55
                ext = rnd.uniform(-0.08, 0.05) if r == rows - 1 else 0.0
                yc = s * over_y * (t0 + t1) / 2
                zc = z_r - (z_r - z_e) * (t0 + t1) / 2
                lift = th / 2 + 0.03 * r
                ob = box('thatch_%d_%d_%d' % (s, r, j), (x0 - 0.04, -L * (t1 - t0) / 2 - ext / 2, -th / 2),
                         (x1 + 0.04, L * (t1 - t0) / 2 + ext / 2, th / 2), 'thatch', 'roof',
                         rot=(-s * (a + math.radians(3)), rnd.uniform(-0.03, 0.03), 0))
                ob.location = ((x0 + x1) / 2, yc + ny * lift + s * ext * 0.4, zc + nz * lift + sag)
        # rafters under the thatch
        for k, x in enumerate((-2.0, -0.7, 0.6, 1.9)):
            cyl('rafter_%d_%d' % (s, k), (x, 0.0, z_r - 0.08), (x, s * over_y * 0.95, z_e + 0.02), 0.05, 'twig', 'roof', sides=5)
    cyl('ridge_roll', (-over_x - 0.1, 0, z_r + 0.18), (over_x + 0.1, 0.03, z_r + 0.12), 0.17, 'thatch', 'roof', sides=7)
    # a few sticks laid on the thatch to hold it down
    for k, x in enumerate((-1.6, 0.2, 1.7)):
        for s in (-1, 1):
            cyl('weight_%d_%d' % (k, s), (x, s * 0.25, z_r + 0.1), (x + 0.1, s * over_y * 0.7, z_e + 0.55), 0.035, 'twig', 'roof', sides=4)


def chimney():
    box('chim_base', (X1 + 0.1, 0.25, 0.0), (X1 + 0.8, 1.15, 2.2), 'stone', 'chimney')
    ob = box('chim_stack', (-0.27, -0.3, 0.0), (0.27, 0.3, 2.0), 'stone', 'chimney', rot=(0.05, -0.07, 0))
    ob.location = (X1 + 0.44, 0.7, 3.15)          # box() places the object at the centre of the box
    ob = box('chim_cap', (-0.32, -0.35, 0.0), (0.32, 0.35, 0.1), 'stone_dark', 'chimney', rot=(0.05, -0.07, 0))
    ob.location = (X1 + 0.3, 0.6, 4.17)
    ob = box('chim_hole', (-0.18, -0.2, 0.0), (0.18, 0.2, 0.01), 'soot', 'chimney', rot=(0.05, -0.07, 0))
    ob.location = (X1 + 0.3, 0.6, 4.225)
    # the log propping the east wall
    cyl('prop_log', (X1 + 1.1, -1.0, 0.0), (X1 + 0.1, -1.0, 1.65), 0.09, 'timber', 'ext', sides=6)
    rock('prop_stone', (X1 + 1.12, -1.0, 0.05), (0.22, 0.2, 0.12), 'stone', 'ext', seed=31, squash=0.6)


def lean_to():
    """Open shed on the west: firewood and herbs drying. Fades with the west wall."""
    x = X0 - 1.25
    for k, y in enumerate((-1.45, 1.35)):
        post('lean_post_%d' % k, x, y, 0.0, 1.35, 0.07, 'lean', lean=(0.04 * (k - 0.5), 0.0))
    cyl('lean_beam', (x, Y0 + 0.2, 1.33), (x, Y1 - 0.25, 1.36), 0.06, 'timber', 'lean', sides=6)
    run = 1.35
    ang = math.atan2(1.72 - 1.3, run)
    ob = box('lean_roof', (-run / 2 - 0.25, Y0 + 0.05, -0.05), (run / 2 + 0.1, Y1 - 0.1, 0.05), 'boards_f', 'lean', rot=(0, ang, 0))
    ob.location = (X0 - run / 2 - 0.05, 0.0, (1.72 + 1.32) / 2 + 0.07)
    ob = box('lean_thatch', (-run / 2 - 0.3, Y0, -0.08), (run / 2 + 0.1, Y1 - 0.05, 0.08), 'thatch', 'lean', rot=(0, ang, 0.02))
    ob.location = (X0 - run / 2 - 0.05, 0.0, (1.72 + 1.32) / 2 + 0.17)
    # firewood
    for row in range(3):
        for k in range(7 - row):
            y = -1.2 + k * 0.22 + row * 0.11
            cyl('lean_wood_%d_%d' % (row, k), (X0 - 0.95, y, 0.1 + row * 0.17), (X0 - 0.25, y, 0.1 + row * 0.17), 0.08, 'timber', 'lean', sides=6)
    # drying pole with bunches of herbs
    cyl('dry_pole', (x + 0.05, 0.25, 1.2), (X0 - 0.12, 0.25, 1.5), 0.025, 'twig', 'lean', sides=4)
    for k in range(4):
        t = (k + 0.5) / 4
        px = x + 0.05 + (X0 - 0.12 - x - 0.05) * t
        pz = 1.2 + 0.3 * t
        cyl('dry_str_%d' % k, (px, 0.25, pz), (px, 0.25, pz - 0.18), 0.006, 'linen', 'lean', sides=3)
        blob('dry_herb_%d' % k, 'lean', (px, 0.25, pz - 0.3), (0.08, 0.08, 0.16), 50 + k, 'herbs_dry' if k % 2 else 'herbs', n=14)
    # a basket and a chopping stump
    cyl('lean_basket', (x + 0.3, 1.0, 0.0), (x + 0.3, 1.0, 0.32), 0.2, 'wattle', 'lean', sides=8, r1=0.25)
    cyl('lean_stump', (x + 0.15, -0.3, 0.0), (x + 0.15, -0.3, 0.42), 0.22, 'timber', 'lean', sides=8)


# ------------------------------------------------------------------------------------------------ inside

FIRE = (-0.25, 0.55)
COUNTER = (0.95, 2.05, -1.35, -0.8)            # x0, x1, y0, y1


def floor():
    box('floor', (X0 + T / 2, Y0 + T / 2, 0.0), (X1 - T / 2, Y1 - T / 2, FLOOR), 'dirt', 'floor')
    box('threshold', (DOOR[0], Y0 - 0.12, 0.0), (DOOR[1], Y0 + 0.12, FLOOR + 0.02), 'timber', 'floor')
    for k, (x, y, r) in enumerate(((-1.4, -0.9, 0.45), (1.3, 0.9, 0.35), (-0.9, 1.2, 0.3))):
        rock('floor_straw_%d' % k, (x, y, FLOOR), (r, r * 0.6, 0.02), 'straw', 'floor', seed=60 + k, squash=0.5)


def fire_pit():
    fx, fy = FIRE
    for k in range(9):
        a = 2 * math.pi * k / 9
        rock('pit_stone_%d' % k, (fx + 0.42 * math.cos(a), fy + 0.42 * math.sin(a), FLOOR + 0.04), (0.12, 0.1, 0.09),
             'stone_dark', 'interior', seed=70 + k, squash=0.8)
    box('pit_ash', (fx - 0.32, fy - 0.32, FLOOR), (fx + 0.32, fy + 0.32, FLOOR + 0.02), 'soot', 'interior')
    for k in range(3):
        a = k * 2.1
        cyl('pit_log_%d' % k, (fx + 0.25 * math.cos(a), fy + 0.25 * math.sin(a), FLOOR + 0.04), (fx, fy, FLOOR + 0.14), 0.045, 'wood', 'interior', sides=5)
    box('pit_embers', (fx - 0.18, fy - 0.18, FLOOR + 0.02), (fx + 0.18, fy + 0.18, FLOOR + 0.06), 'ember', 'glow')
    box('pit_flame_a', (fx - 0.06, fy - 0.05, FLOOR + 0.08), (fx + 0.05, fy + 0.06, FLOOR + 0.32), 'flame', 'glow', rot=(0, 0, 0.4))
    box('pit_flame_b', (fx + 0.05, fy - 0.1, FLOOR + 0.08), (fx + 0.13, fy, FLOOR + 0.24), 'flame', 'glow')
    # tripod and cauldron
    top = Vector((fx, fy, 1.45))
    for k in range(3):
        a = k * 2.094 + 0.3
        cyl('tripod_%d' % k, (fx + 0.62 * math.cos(a), fy + 0.62 * math.sin(a), FLOOR), top, 0.03, 'twig', 'interior', sides=4)
    cyl('chain', (fx, fy, 1.42), (fx, fy, 0.78), 0.012, 'iron', 'interior', sides=4)
    cyl('cauldron_low', (fx, fy, 0.38), (fx, fy, 0.55), 0.17, 'iron', 'interior', sides=10, r1=0.25)
    cyl('cauldron_up', (fx, fy, 0.55), (fx, fy, 0.74), 0.25, 'iron', 'interior', sides=10, r1=0.21)
    cyl('cauldron_rim', (fx, fy, 0.74), (fx, fy, 0.78), 0.23, 'iron', 'interior', sides=10)
    cyl('cauldron_brew', (fx, fy, 0.72), (fx, fy, 0.735), 0.2, 'brew', 'glow_brew', sides=10)
    cyl('ladle', (fx + 0.1, fy + 0.05, 0.6), (fx + 0.32, fy + 0.2, 1.0), 0.015, 'wood_light', 'interior', sides=4)


def counter():
    """Where she attends: a heavy table with her mortar, scales, herbs and bottles."""
    x0, x1, y0, y1 = COUNTER
    box('counter_top', (x0, y0, 0.8), (x1, y1, 0.88), 'boards_f', 'interior', rot=(0, 0, 0.02))
    for k, (x, y) in enumerate(((x0 + 0.07, y0 + 0.07), (x1 - 0.07, y0 + 0.07), (x0 + 0.07, y1 - 0.07), (x1 - 0.07, y1 - 0.07))):
        box('counter_leg_%d' % k, (x - 0.05, y - 0.05, FLOOR), (x + 0.05, y + 0.05, 0.8), 'wood', 'interior')
    box('counter_shelf', (x0 + 0.05, y0 + 0.05, 0.25), (x1 - 0.05, y1 - 0.05, 0.29), 'boards_f', 'interior')
    for k in range(3):
        cyl('counter_pot_%d' % k, (x0 + 0.25 + k * 0.35, (y0 + y1) / 2, 0.29), (x0 + 0.25 + k * 0.35, (y0 + y1) / 2, 0.5), 0.1, 'clay', 'interior', sides=7)
    cyl('mortar', (x0 + 0.25, y0 + 0.25, 0.88), (x0 + 0.25, y0 + 0.25, 1.0), 0.09, 'stone_dark', 'interior', sides=8, r1=0.11)
    cyl('pestle', (x0 + 0.27, y0 + 0.26, 0.95), (x0 + 0.38, y0 + 0.2, 1.15), 0.02, 'wood_light', 'interior', sides=4)
    # scales
    cyl('scale_post', (x1 - 0.35, y0 + 0.3, 0.88), (x1 - 0.35, y0 + 0.3, 1.2), 0.012, 'iron', 'interior', sides=4)
    cyl('scale_beam', (x1 - 0.55, y0 + 0.3, 1.2), (x1 - 0.15, y0 + 0.3, 1.2), 0.01, 'iron', 'interior', sides=4)
    for k, x in enumerate((x1 - 0.55, x1 - 0.15)):
        cyl('scale_pan_%d' % k, (x, y0 + 0.3, 1.02 + 0.02 * k), (x, y0 + 0.3, 1.04 + 0.02 * k), 0.07, 'iron', 'interior', sides=8)
    for k, (x, y, h, m) in enumerate(((x0 + 0.55, y1 - 0.12, 0.2, 'glass_g'), (x0 + 0.68, y1 - 0.2, 0.14, 'glass_v'),
                                       (x0 + 0.8, y1 - 0.1, 0.24, 'glass_g'), (x1 - 0.12, y1 - 0.14, 0.16, 'clay'))):
        cyl('counter_bottle_%d' % k, (x, y, 0.88), (x, y, 0.88 + h), 0.04, m, 'interior', sides=6)
    blob('counter_herbs', 'interior', (x0 + 0.65, y0 + 0.2, 0.92), (0.16, 0.08, 0.05), 81, 'herbs', n=16)
    cyl('candle', (x1 - 0.12, y1 - 0.35, 0.88), (x1 - 0.12, y1 - 0.35, 1.0), 0.025, 'linen', 'interior', sides=6)
    box('candle_flame', (x1 - 0.135, y1 - 0.365, 1.0), (x1 - 0.105, y1 - 0.335, 1.05), 'flame', 'glow')
    box('counter_book', (x0 + 0.05, y1 - 0.3, 0.88), (x0 + 0.35, y1 - 0.05, 0.92), 'paper', 'interior', rot=(0, 0, 0.2))


def shelves():
    """North wall: two long shelves crowded with jars, bottles, a skull of some beast and bundles."""
    y = Y1 - T / 2
    for lvl, z in enumerate((0.95, 1.45)):
        box('shelf_%d' % lvl, (-0.7, y - 0.25, z), (X1 - 0.15, y, z + 0.04), 'boards_f', 'interior', rot=(0, 0.01 * (lvl * 2 - 1), 0))
        for k in range(3):
            box('shelf_br_%d_%d' % (lvl, k), (-0.62 + k * 1.3, y - 0.2, z - 0.15), (-0.58 + k * 1.3, y, z), 'wood', 'interior')
    rnd = random.Random(90)
    kinds = ['clay', 'glass_g', 'glass_v', 'clay', 'linen', 'glass_g', 'wood', 'clay']
    for lvl, z in enumerate((0.99, 1.49)):
        x = -0.6
        k = 0
        while x < X1 - 0.3:
            r = rnd.uniform(0.04, 0.08)
            h = rnd.uniform(0.1, 0.26)
            m = kinds[(k + lvl * 3) % len(kinds)]
            cyl('jar_%d_%d' % (lvl, k), (x + r, y - 0.13, z), (x + r, y - 0.13, z + h), r, m, 'interior', sides=6, r1=r * rnd.uniform(0.6, 1.0))
            x += 2 * r + rnd.uniform(0.03, 0.08)
            k += 1
    blob('beast_skull', 'interior', (1.6, y - 0.12, 1.58), (0.12, 0.08, 0.08), 91, 'bone', n=16)
    for s in (-1, 1):
        cyl('skull_horn_%d' % s, (1.6 + s * 0.08, y - 0.1, 1.62), (1.6 + s * 0.22, y - 0.12, 1.78), 0.02, 'bone', 'interior', sides=4, r1=0.006)


def herb_beam():
    """Bunches of herbs and garlic hanging from a beam across the hut."""
    yb = 1.0
    cyl('herb_beam', (X0 + 0.05, yb, 1.82), (X1 - 0.05, yb + 0.05, 1.84), 0.06, 'timber', 'interior', sides=6)
    rnd = random.Random(95)
    for k in range(9):
        x = X0 + 0.45 + k * 0.48
        if abs(x - FIRE[0]) < 0.3:
            continue
        drop = rnd.uniform(0.18, 0.35)
        cyl('herb_str_%d' % k, (x, yb, 1.78), (x, yb, 1.78 - drop), 0.006, 'linen', 'interior', sides=3)
        if k % 3 == 2:
            for j in range(3):
                blob('garlic_%d_%d' % (k, j), 'interior', (x + 0.03 * (j - 1), yb, 1.7 - drop - j * 0.1), (0.05, 0.05, 0.045), 100 + k * 3 + j, 'bone', n=10)
        else:
            blob('herb_bunch_%d' % k, 'interior', (x, yb, 1.62 - drop), (0.09, 0.09, 0.18), 120 + k, 'herbs_dry' if k % 2 else 'herbs', n=14)


def bed():
    x0, x1, y0, y1 = X0 + 0.15, X0 + 1.0, 0.15, Y1 - 0.2
    box('bed_straw', (x0, y0, FLOOR), (x1, y1, 0.22), 'straw', 'interior')
    box('bed_blanket', (x0 + 0.02, y0 + 0.02, 0.22), (x1 - 0.05, y1 - 0.4, 0.28), 'wool_r', 'interior', rot=(0, 0, -0.04))
    box('bed_patch', (x0 + 0.2, y0 + 0.35, 0.28), (x0 + 0.5, y0 + 0.65, 0.285), 'patch', 'interior')
    box('bed_pillow', (x0 + 0.12, y1 - 0.38, 0.22), (x1 - 0.12, y1 - 0.08, 0.32), 'linen', 'interior')


def corner_things():
    # baskets and a sack by the door, a broom
    cyl('basket_a', (-1.6, -1.25, FLOOR), (-1.6, -1.25, 0.35), 0.2, 'wattle', 'interior', sides=8, r1=0.24)
    for k in range(4):
        blob('roots_%d' % k, 'interior', (-1.6 + 0.08 * (k % 2) - 0.04, -1.25 + 0.08 * (k // 2) - 0.04, 0.38), (0.06, 0.05, 0.04), 130 + k, 'herbs_dry', n=10)
    cyl('basket_b', (-2.0, -0.85, FLOOR), (-2.0, -0.85, 0.28), 0.17, 'wattle', 'interior', sides=8, r1=0.2)
    rock('sack', (-1.15, -1.45, 0.22), (0.2, 0.18, 0.24), 'linen', 'interior', seed=40, squash=1.0)
    cyl('broom', (-0.8, -1.55, FLOOR), (-0.95, -1.62, 1.35), 0.018, 'twig', 'interior', sides=4)
    blob('broom_head', 'interior', (-0.79, -1.55, 0.2), (0.1, 0.06, 0.22), 141, 'straw', n=12)
    # barrel of water by the counter
    cyl('barrel', (X1 - 0.35, -0.25, FLOOR), (X1 - 0.35, -0.25, 0.75), 0.25, 'boards', 'interior', sides=10)
    cyl('barrel_hoop', (X1 - 0.35, -0.25, 0.6), (X1 - 0.35, -0.25, 0.64), 0.265, 'iron', 'interior', sides=10)
    # a stool by the fire
    cyl('stool', (0.55, 0.15, 0.36), (0.55, 0.15, 0.42), 0.16, 'wood_light', 'interior', sides=8)
    for j in range(3):
        a = j * 2.094
        cyl('stool_leg_%d' % j, (0.55 + 0.09 * math.cos(a), 0.15 + 0.09 * math.sin(a), 0.36),
            (0.55 + 0.14 * math.cos(a), 0.15 + 0.14 * math.sin(a), FLOOR), 0.022, 'wood', 'interior', sides=4)


def door():
    u0, u1 = DOOR
    y = Y0 + 0.02
    # crooked planks, one shorter than the rest
    for k in range(4):
        a = u0 + 0.03 + k * (u1 - u0 - 0.06) / 4
        b = a + (u1 - u0 - 0.06) / 4 - 0.01
        top = DOOR_TOP - 0.03 - (0.12 if k == 2 else 0.0)
        box('door_plank_%d' % k, (a, y - 0.035, FLOOR + 0.02), (b, y + 0.035, top), 'boards_x', 'door', rot=(0, 0.01 * (k - 1.5), 0))
    for k, z in enumerate((0.4, 1.45)):
        box('door_batten_%d' % k, (u0 + 0.06, y - 0.06, z), (u1 - 0.04, y - 0.035, z + 0.09), 'wood', 'door')
    cyl('door_pull', (u0 + 0.12, y - 0.09, 0.95), (u0 + 0.12, y - 0.09, 1.1), 0.02, 'twig', 'door', sides=4)
    # a charm hanging on the door: twigs bound in a star
    for k in range(3):
        a = k * 1.047
        cyl('charm_%d' % k, ((u0 + u1) / 2 + 0.12 * math.cos(a), y - 0.07, 1.62 + 0.12 * math.sin(a)),
            ((u0 + u1) / 2 - 0.12 * math.cos(a), y - 0.07, 1.62 - 0.12 * math.sin(a)), 0.01, 'twig', 'door', sides=3)


# ------------------------------------------------------------------------------------------------ outside

GARDEN = (3.55, 7.9, -2.1, 2.0)                # x0, x1, y0, y1 of the fence
GATE = (3.55, 4.45)                           # gap in the south fence


def outside():
    # step stone, rain barrel at the south-east corner, a bench by the door
    rock('step', ((DOOR[0] + DOOR[1]) / 2, Y0 - 0.55, 0.02), (0.5, 0.3, 0.1), 'stone', 'ext', seed=7, squash=0.5)
    cyl('rain_barrel', (X1 + 0.45, Y0 - 0.35, 0.0), (X1 + 0.45, Y0 - 0.35, 0.8), 0.28, 'boards', 'ext', sides=10)
    cyl('rain_hoop', (X1 + 0.45, Y0 - 0.35, 0.62), (X1 + 0.45, Y0 - 0.35, 0.66), 0.295, 'iron', 'ext', sides=10)
    box('bench', (0.75, Y0 - 0.55, 0.38), (1.85, Y0 - 0.25, 0.45), 'boards_f', 'ext')
    for k, x in enumerate((0.85, 1.75)):
        box('bench_leg_%d' % k, (x - 0.05, Y0 - 0.52, 0.0), (x + 0.05, Y0 - 0.28, 0.38), 'wood', 'ext')
    blob('bench_herbs', 'ext', (1.1, Y0 - 0.4, 0.5), (0.18, 0.1, 0.06), 150, 'herbs_dry', n=14)
    cyl('bench_bowl', (1.55, Y0 - 0.4, 0.45), (1.55, Y0 - 0.4, 0.52), 0.1, 'clay', 'ext', sides=8, r1=0.13)
    # bones and a crow's skull on a stick by the path: a warning or a charm
    cyl('charm_stick', (-1.6, Y0 - 1.25, 0.0), (-1.62, Y0 - 1.27, 1.25), 0.025, 'twig', 'ext', sides=4)
    blob('charm_skull', 'ext', (-1.62, Y0 - 1.27, 1.3), (0.06, 0.08, 0.055), 151, 'bone', n=12)
    for k in range(3):
        cyl('charm_bone_%d' % k, (-1.62, Y0 - 1.27, 1.15), (-1.62 + 0.06 * (k - 1), Y0 - 1.3, 0.9), 0.008, 'bone', 'ext', sides=3)


def garden():
    gx0, gx1, gy0, gy1 = GARDEN
    rnd = random.Random(200)

    def fence(name, p0, p1):
        p0, p1 = Vector(p0), Vector(p1)
        L = (p1 - p0).length
        n = max(1, int(round(L / 0.8)))
        for i in range(n + 1):
            p = p0 + (p1 - p0) * (i / n)
            post('%s_post_%d' % (name, i), p.x, p.y, 0.0, 0.78 + rnd.uniform(-0.06, 0.06), 0.045, 'garden',
                 lean=(rnd.uniform(-0.04, 0.04), rnd.uniform(-0.04, 0.04)), mat='twig')
        d = (p1 - p0).normalized()
        nrm = Vector((-d.y, d.x, 0))
        mid = (p0 + p1) / 2
        ang = math.atan2(d.y, d.x)
        ob = box('%s_wattle' % name, (-L / 2, -0.02, 0.12), (L / 2, 0.02, 0.62), 'wattle', 'garden', rot=(0, 0, ang))
        ob.location = (mid.x, mid.y, 0.37)
        for j, z in enumerate((0.3, 0.6)):
            cyl('%s_rail_%d' % (name, j), (p0.x, p0.y, z), (p1.x, p1.y, z + rnd.uniform(-0.04, 0.04)), 0.02, 'twig', 'garden', sides=4)

    fence('fence_s', (GATE[1], gy0, 0), (gx1, gy0, 0))
    fence('fence_e', (gx1, gy0, 0), (gx1, gy1, 0))
    fence('fence_n', (gx1, gy1, 0), (gx0, gy1, 0))
    fence('fence_w', (gx0, gy1, 0), (gx0, gy0 + 0.6, 0))
    post('gate_post', gx0, gy0, 0.0, 0.9, 0.05, 'garden', mat='twig')
    # three raised beds running north-south
    beds = [(4.15, 'cabbage'), (5.45, 'leek'), (6.85, 'herb')]
    for b, (cx, crop) in enumerate(beds):
        bx0, bx1, by0, by1 = cx - 0.42, cx + 0.42, gy0 + 0.45, gy1 - 0.45
        for k, (a, c, d, e) in enumerate(((bx0, by0, bx1, by0 + 0.05), (bx0, by1 - 0.05, bx1, by1),
                                          (bx0, by0, bx0 + 0.05, by1), (bx1 - 0.05, by0, bx1, by1))):
            box('bed%d_board_%d' % (b, k), (a, c, 0.0), (d, e, 0.2), 'boards_f', 'garden')
        box('bed%d_soil' % b, (bx0 + 0.05, by0 + 0.05, 0.0), (bx1 - 0.05, by1 - 0.05, 0.17), 'soil', 'garden')
        rows = int((by1 - by0 - 0.2) / 0.42)
        for j in range(rows):
            y = by0 + 0.3 + j * 0.42
            for i, x in enumerate((cx - 0.18, cx + 0.18)):
                s = rnd.uniform(0.85, 1.15)
                if crop == 'cabbage':
                    blob('cab_%d_%d_%d' % (b, j, i), 'garden', (x, y, 0.2), (0.15 * s, 0.15 * s, 0.11 * s), 300 + j * 2 + i, 'cabbage', n=18, under=0.3)
                elif crop == 'leek':
                    for t in range(3):
                        a = t * 2.1 + i
                        cyl('leek_%d_%d_%d_%d' % (b, j, i, t), (x, y, 0.17), (x + 0.06 * math.cos(a), y + 0.06 * math.sin(a), 0.42 * s), 0.025, 'leek', 'garden', sides=4, r1=0.004)
                else:
                    if (j + i) % 3 == 0:
                        blob('flower_%d_%d_%d' % (b, j, i), 'garden', (x, y, 0.24), (0.11, 0.11, 0.09), 400 + j * 2 + i, 'herbs', n=14)
                        for t in range(4):
                            box('bloom_%d_%d_%d_%d' % (b, j, i, t), (x - 0.02 + 0.05 * (t % 2) - 0.03, y - 0.02 + 0.05 * (t // 2) - 0.03, 0.31),
                                (x + 0.02 + 0.05 * (t % 2) - 0.03, y + 0.02 + 0.05 * (t // 2) - 0.03, 0.35), 'flower_y', 'garden')
                    else:
                        blob('lav_%d_%d_%d' % (b, j, i), 'garden', (x, y, 0.26), (0.1, 0.1, 0.14), 500 + j * 2 + i, 'lavender', n=14)
    # bean poles at the north end of the leek bed
    cx = 5.45
    top = Vector((cx, gy1 - 0.3, 1.55))
    for k in range(3):
        a = k * 2.094
        cyl('bean_pole_%d' % k, (cx + 0.35 * math.cos(a), gy1 - 0.3 + 0.3 * math.sin(a), 0.0), top, 0.018, 'twig', 'garden', sides=4)
        for j in range(3):
            t = 0.25 + j * 0.25
            p = Vector((cx + 0.35 * math.cos(a), gy1 - 0.3 + 0.3 * math.sin(a), 0.0)).lerp(top, t)
            blob('bean_leaf_%d_%d' % (k, j), 'garden', p, (0.1, 0.1, 0.12), 600 + k * 3 + j, 'herbs', n=12)
    # scarecrow in the north-east corner
    sx, sy = gx1 - 0.55, gy1 - 0.5
    cyl('crow_post', (sx, sy, 0.0), (sx, sy, 1.85), 0.04, 'twig', 'garden', sides=5)
    cyl('crow_arms', (sx - 0.55, sy, 1.45), (sx + 0.55, sy + 0.05, 1.4), 0.03, 'twig', 'garden', sides=4)
    rock('crow_head', (sx, sy, 1.85), (0.14, 0.13, 0.16), 'linen', 'garden', seed=61, squash=1.0)
    cyl('crow_hat', (sx, sy, 1.95), (sx + 0.02, sy, 2.2), 0.2, 'leather', 'garden', sides=8, r1=0.04)
    box('crow_coat', (sx - 0.3, sy - 0.06, 0.85), (sx + 0.3, sy + 0.06, 1.48), 'rag', 'garden', rot=(0, 0.05, 0))
    box('crow_sleeve', (sx + 0.25, sy - 0.05, 1.15), (sx + 0.5, sy + 0.05, 1.43), 'rag', 'garden', rot=(0, -0.3, 0))
    # watering pot and a basket of picked cabbages
    cyl('water_pot', (gx0 + 0.35, gy0 + 0.25, 0.0), (gx0 + 0.35, gy0 + 0.25, 0.28), 0.12, 'clay', 'garden', sides=8, r1=0.09)
    cyl('pick_basket', (4.8, gy0 + 0.25, 0.0), (4.8, gy0 + 0.25, 0.25), 0.18, 'wattle', 'garden', sides=8, r1=0.22)
    for k in range(2):
        blob('picked_%d' % k, 'garden', (4.75 + 0.1 * k, gy0 + 0.25, 0.28), (0.1, 0.1, 0.08), 700 + k, 'cabbage', n=14)


# ------------------------------------------------------------------------------------------------ parts and meta

PARTS = {
    'roof': {'group': 'cabin', 'role': 'roof'},
    'chimney': {'group': 'cabin', 'role': 'chimney'},
    'wall_S_up': {'group': 'cabin', 'role': 'cut', 'normal': (0, -1, 0)},
    'wall_W_up': {'group': 'cabin', 'role': 'cut', 'normal': (-1, 0, 0)},
    'wall_N_up': {'group': 'cabin', 'role': 'cut', 'normal': (0, 1, 0)},
    'wall_E_up': {'group': 'cabin', 'role': 'cut', 'normal': (1, 0, 0)},
    'wall_S_base': {'group': 'cabin', 'role': 'static'},
    'wall_W_base': {'group': 'cabin', 'role': 'static'},
    'wall_N_base': {'group': 'cabin', 'role': 'static'},
    'wall_E_base': {'group': 'cabin', 'role': 'static'},
    'lean': {'group': 'cabin', 'role': 'cut', 'normal': (-1, 0, 0)},
    'floor': {'group': 'cabin', 'role': 'static', 'shadow': False},
    'interior': {'group': 'cabin', 'role': 'static', 'shadow': False},
    'ext': {'group': 'cabin', 'role': 'static'},
    'garden': {'group': 'cabin', 'role': 'static'},
    'glow': {'group': 'cabin', 'role': 'static', 'glowColor': 'd2ac88'},
    'glow_brew': {'group': 'cabin', 'role': 'static', 'glowColor': '7e7752', 'glowTint': '4f5c40'},
    'door': {'group': 'cabin', 'role': 'door', 'normal': (0, -1, 0), 'pivot': (DOOR[1], Y0 + 0.02, FLOOR)},
}


def build_all():
    for ob in list(bpy.data.objects):
        bpy.data.objects.remove(ob, do_unlink=True)
    for m in list(bpy.data.materials):
        bpy.data.materials.remove(m)
    for me in list(bpy.data.meshes):
        bpy.data.meshes.remove(me)
    reset()
    mats()
    wall('S', [(DOOR[0], DOOR[1], 0.0, DOOR_TOP), WIN_S])
    wall('N', [])
    wall('E', [])
    wall('W', [WIN_W])
    frame_and_footing()
    gable('W')
    gable('E')
    roof()
    chimney()
    lean_to()
    floor()
    fire_pit()
    counter()
    shelves()
    herb_beam()
    bed()
    corner_things()
    door()
    outside()
    garden()
    bpy.context.view_layer.update()
    return len(PIECES)


def ground():
    bpy.ops.mesh.primitive_plane_add(size=40, location=(2.5, 0, 0))
    g = bpy.context.active_object
    g.name = 'ground_ao'
    pmat('ground_ao', 'flat', ['3d332b', '3d332b', '3d332b'])
    g.data.materials.append(MATS['ground_ao'])
    return g


def meta():
    """Everything Unity needs besides the meshes (Unity coordinates, real metres: the importer scales them)."""
    gx0, gx1, gy0, gy1 = GARDEN
    cx0, cx1, cy0, cy1 = COUNTER
    fence = [UB(GATE[1], gy0 - 0.08, gx1 + 0.08, gy0 + 0.08), UB(gx1 - 0.08, gy0 - 0.08, gx1 + 0.08, gy1 + 0.08),
             UB(gx0 - 0.08, gy1 - 0.08, gx1 + 0.08, gy1 + 0.08), UB(gx0 - 0.08, gy0 + 0.55, gx0 + 0.08, gy1 + 0.08)]
    beds = [UB(cx - 0.44, gy0 + 0.43, cx + 0.44, gy1 - 0.43) for cx in (4.15, 5.45, 6.85)]
    return {
        'name': 'ChozaCurandera',
        'footprint': UB(X0 + 0.1, Y0 + 0.1, X1 - 0.1, Y1 - 0.1),
        'obstacles': {
            'cabin': [UB(X0 - 0.12, Y1 - 0.12, X1 + 0.12, Y1 + 0.12), UB(X1 - 0.12, Y0 - 0.12, X1 + 0.12, Y1 + 0.12),
                      UB(X0 - 0.12, Y0 - 0.12, X0 + 0.12, Y1 + 0.12), UB(X0 - 0.12, Y0 - 0.12, DOOR[0] - 0.04, Y0 + 0.12),
                      UB(DOOR[1] + 0.04, Y0 - 0.12, X1 + 0.12, Y0 + 0.12),
                      UB(X1 + 0.1, 0.25, X1 + 0.82, 1.15), UB(X1 + 0.2, -1.12, X1 + 1.25, -0.88),
                      UB(X0 - 1.4, Y0 + 0.1, X0 - 0.12, Y1 - 0.15),
                      UB(cx0, cy0, cx1, cy1), UB(FIRE[0] - 0.5, FIRE[1] - 0.5, FIRE[0] + 0.5, FIRE[1] + 0.5),
                      UB(-0.7, Y1 - 0.35, X1 - 0.12, Y1 - 0.07), UB(X0 + 0.12, 0.12, X0 + 1.02, Y1 - 0.15),
                      UB(X1 - 0.62, -0.52, X1 - 0.1, 0.02), UB(-2.2, -1.5, -1.38, -0.65),
                      UB(X1 + 0.15, Y0 - 0.65, X1 + 0.75, Y0 - 0.05), UB(0.72, Y0 - 0.58, 1.88, Y0 - 0.22)] + fence + beds +
                     [UB(gx1 - 0.7, gy1 - 0.65, gx1 - 0.4, gy1 - 0.35)],
        },
        'door': {'closed': UB(DOOR[0], Y0 - 0.12, DOOR[1], Y0 + 0.12), 'openYaw': 95, 'locked': False, 'health': 40,
                 'prompt': U((DOOR[0] + DOOR[1]) / 2, Y0 - 0.5, 0), 'inside': U((DOOR[0] + DOOR[1]) / 2, Y0 + 0.6, 0)},
        'lights': [
            {'group': 'cabin', 'pos': U(FIRE[0], FIRE[1], 0.6), 'radius': 3.4, 'color': [1.0, 0.6, 0.3], 'intensity': 0.9, 'flicker': 0.45},
            {'group': 'cabin', 'pos': U(FIRE[0], FIRE[1], 0.95), 'radius': 1.6, 'color': [0.55, 0.8, 0.4], 'intensity': 0.45, 'flicker': 0.15},
            {'group': 'cabin', 'pos': U(cx1 - 0.12, cy1 - 0.35, 1.05), 'radius': 1.8, 'color': [1.0, 0.7, 0.4], 'intensity': 0.6, 'flicker': 0.3},
        ],
        # she stands behind her counter, facing the door; the rest point is by the fire
        'anchors': {'occupant': U((cx0 + cx1) / 2, cy1 + 0.45, 0), 'occupantGroup': 'cabin', 'rest': U(FIRE[0] + 0.1, FIRE[1] - 0.9, 0)},
        'smoke': [U(X1 + 0.3, 0.6, 4.3)],
        'bubbles': [{'pos': U(FIRE[0], FIRE[1], 0.74), 'radius': 0.17}],
        'searchables': [
            {'name': 'Estantes', 'what': 'los estantes', 'table': 'botica', 'at': U(0.6, Y1 - 0.2, 0), 'group': 'cabin'},
            {'name': 'Canasto', 'what': 'el canasto', 'table': 'canasto', 'at': U(-1.6, -1.25, 0), 'group': 'cabin'},
            {'name': 'Barril', 'what': 'el barril', 'table': 'barril', 'at': U(X1 - 0.35, -0.25, 0), 'group': 'cabin'},
        ],
    }
