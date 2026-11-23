# Barro y Bruma - the woodcutter's cabin (Cabana del lenador, marker k74), with its interior and the basement where
# the plague doctor keeps the woodcutter chained. Built with edificio_kit.py (exec it first in the same namespace).
#
# Cabin: log walls 6 x 4.4 m (walls a little taller than the hero, ridge at 4 m), shingle roof with moss, stone
# chimney on the east side, plank door with a padlock (the doctor locks it: break it or open it), three windows
# with shutters. Inside: hearth, bed, table and stools, shelf, crossed axes, firewood, chest, rug and the trapdoor.
# Basement: dirt floor, stone walls on the far sides (the near ones are low so the camera sees in), stairs up to
# the trapdoor; it looks like an ordinary cellar (crates, barrels, sacks, a pantry shelf). A wardrobe against the
# north wall hides an opening: pushed aside, it shows the hidden room where the doctor keeps the woodcutter
# chained, with his table of vials and the shelves of Bruma jars (the sprite brings his own wall and straw).
import math, random
from mathutils import Vector

X0, X1, Y0, Y1 = -3.0, 3.0, -2.2, 2.2      # wall centre lines
LR = 0.14                                   # log radius
PITCH = 0.24                                # course height
COURSES = 10
TOP = 0.14 + PITCH * (COURSES - 1) + LR      # top of the log walls (2.44)
RIDGE = 4.0
FLOOR = 0.1


def mats():
    pmat('logs', 'logs', ['2a2420', '3d332b', '594a3c', '7a6650'], grime=0.35)
    pmat('log_end', 'stripes', ['3d332b', '7a6650', '9c8a70'], scale=2.0, dir='RINGS')
    pmat('boards', 'planks', ['2a2420', '3d332b', '594a3c', '7a6650'], size=(1.3, 0.17), axes='zy', mortar=0.03, grime=0.2)
    pmat('boards_x', 'planks', ['2a2420', '3d332b', '594a3c', '7a6650'], size=(1.3, 0.17), axes='zx', mortar=0.03, grime=0.2)
    pmat('floor', 'planks', ['2a2420', '3d332b', '594a3c', '7a6650'], size=(1.8, 0.2), axes='xy', mortar=0.025)
    pmat('shingle', 'shingles', ['141210', '2a2420', '3d332b', '594a3c'], size=(0.26, 0.5), axes='xy', mortar=0.07,
         coords='object', moss={'cols': ['3d332b', '4e4b34', '4e4b34'], 'cut': 0.7})
    pmat('stone', 'stone', ['2a2420', '3f3e3b', '6e6c66', '7d8584'], scale=1.6, grime=0.3)
    pmat('stone_dark', 'stone', ['2a2420', '3f3e3b', '6e6c66'], scale=1.8, grime=0.2)
    pmat('soot', 'flat', ['141210', '141210', '2a2420'])
    pmat('wood', 'noise', ['2a2420', '3d332b', '594a3c'], scale=3.0)
    pmat('wood_light', 'noise', ['594a3c', '7a6650', '9c8a70'], scale=3.0)
    pmat('iron', 'flat', ['141210', '2f3436', '3f3e3b'])
    pmat('straw', 'noise', ['7a6650', '9c8a70', 'b08a6a'], scale=9.0)
    pmat('wool', 'stripes', ['3e1a18', '6b2a24', '3d332b', '6b2a24'], scale=2.5, dir='Y')
    pmat('linen', 'noise', ['6e6c66', '9a968c', 'c4bfb2'], scale=5.0)
    pmat('rug', 'stripes', ['2f3436', '4f5a5c', '6b2a24', '4f5a5c'], scale=3.0, dir='X')
    pmat('clay', 'noise', ['3e1a18', '6b2a24', '8f4a36'], scale=6.0)
    pmat('bread', 'noise', ['594a3c', '7a6650', 'b08a6a'], scale=8.0)
    pmat('dirt', 'dirt', ['2a2420', '3d332b', '594a3c', '7a6650'], scale=1.2)
    pmat('leather', 'noise', ['141210', '2a2420', '3d332b'], scale=5.0)
    pmat('glass_v', 'noise', ['2c2738', '5b5170', '5b5170'], scale=6.0)
    pmat('paper', 'noise', ['9a968c', 'c4bfb2', 'e6e2d4'], scale=6.0)
    pmat('ember', 'noise', ['b24a36', 'd2ac88', 'b24a36'], scale=8.0, glow=True)
    pmat('flame', 'flat', ['d2ac88', 'e6e2d4', 'd2ac88'], glow=True)
    pmat('bruma_glow', 'noise', ['5b5170', '9a968c', '5b5170'], scale=6.0, glow=True)


# ------------------------------------------------------------------------------------------------ walls

def _course_z(i, side):
    return 0.14 + PITCH * i + (PITCH / 2 if side in 'EW' else 0.0)


def log_wall(side, openings):
    """Logs of one wall. openings: list of (u0, u1, i0, i1): courses i0..i1 are cut between u0 and u1
    (u runs along the wall: x for S/N, y for E/W). Courses 0-1 go to the base part, the rest to 'up'."""
    along_x = side in 'SN'
    fixed = (Y0 if side == 'S' else Y1) if along_x else (X0 if side == 'W' else X1)
    ext = 0.25
    u_min, u_max = ((X0 - ext, X1 + ext) if along_x else (Y0 - ext, Y1 + ext))
    for i in range(COURSES):
        z = _course_z(i, side)
        if z + LR > TOP + 0.13:
            continue
        cuts = sorted((u0, u1) for u0, u1, i0, i1 in openings if i0 <= i <= i1)
        spans, a = [], u_min
        for u0, u1 in cuts:
            spans.append((a, u0))
            a = u1
        spans.append((a, u_max))
        part = 'wall_%s_%s' % (side, 'base' if i < 2 else 'up')
        for k, (a, b) in enumerate(spans):
            if b - a < 0.05:
                continue
            p0 = Vector((a, fixed, z)) if along_x else Vector((fixed, a, z))
            p1 = Vector((b, fixed, z)) if along_x else Vector((fixed, b, z))
            cyl('log_%s_%d_%d' % (side, i, k), p0, p1, LR, 'logs', part, sides=8, roll=(i * 0.4))


def frame(side, u0, u1, z0, z1, part, sill=False):
    """Plank posts and lintel around an opening in a log wall."""
    along_x = side in 'SN'
    w = (Y0 if side == 'S' else Y1) if along_x else (X0 if side == 'W' else X1)
    t, d = 0.07, 0.17
    def B(a0, a1, b0, b1, name):
        if along_x:
            box(name, (a0, w - d, b0), (a1, w + d, b1), 'wood', part)
        else:
            box(name, (w - d, a0, b0), (w + d, a1, b1), 'wood', part)
    B(u0 - t, u0 + 0.0, z0, z1 + t, 'frame_%s_%.1f_a' % (side, u0))
    B(u1, u1 + t, z0, z1 + t, 'frame_%s_%.1f_b' % (side, u0))
    B(u0 - t, u1 + t, z1, z1 + t, 'frame_%s_%.1f_top' % (side, u0))
    if sill:
        B(u0 - t - 0.04, u1 + t + 0.04, z0 - 0.06, z0, 'frame_%s_%.1f_sill' % (side, u0))


def shutters(side, u0, u1, z0, z1, part, broken=False):
    """Two plank shutters swung open against the outside of the wall; one may hang loose."""
    along_x = side in 'SN'
    out = -1 if side in 'SW' else 1
    w = ((Y0 if side == 'S' else Y1) if along_x else (X0 if side == 'W' else X1)) + out * 0.2
    width = (u1 - u0) / 2
    mat = 'boards_x' if along_x else 'boards'
    for k, (a, b) in enumerate(((u0 - width - 0.05, u0 - 0.05), (u1 + 0.05, u1 + width + 0.05))):
        rot = (0, 0, 0)
        zz0, zz1 = z0, z1
        if broken and k == 1:
            zz0, zz1 = z0 - 0.25, z1 - 0.3
            rot = (0, math.radians(14), 0) if along_x else (math.radians(14), 0, 0)
        if along_x:
            box('shutter_%s_%d' % (side, k), (a, w - 0.03, zz0), (b, w + 0.03, zz1), mat, part, rot=rot)
        else:
            box('shutter_%s_%d' % (side, k), (w - 0.03, a, zz0), (w + 0.03, b, zz1), mat, part, rot=rot)


def gable(side):
    x = X0 if side == 'W' else X1
    pts = [(Y0 - 0.25, TOP - 0.02), (Y1 + 0.25, TOP - 0.02), (0.0, RIDGE + 0.02)]
    prism('gable_' + side, pts, 'x', x - 0.06, x + 0.06, 'boards', 'wall_%s_up' % side)


def roof():
    slope = (RIDGE - TOP) / (Y1 + 0.25)
    over_y, over_x = 2.85, 3.55
    z_ridge = RIDGE + 0.08
    z_eave = z_ridge - slope * over_y
    L = math.hypot(over_y, z_ridge - z_eave)
    a = math.atan2(z_ridge - z_eave, over_y)
    rows = 8
    for s in (-1, 1):
        ny, nz = s * math.sin(a), math.cos(a)                # outward normal of this side (y, z)
        for r in range(rows):
            t0, t1 = r / rows, (r + 1) / rows + 0.04
            # each row of shingles is a strip, the lower rows step out a little so the courses show
            yc = s * over_y * (t0 + t1) / 2
            zc = z_ridge - (z_ridge - z_eave) * (t0 + t1) / 2 + 0.03 * (r % 2)
            lift = 0.02 + 0.012 * r
            yc += ny * lift
            zc += nz * lift
            ob = box('shingles_%d_%d' % (s, r), (-over_x, -L * (t1 - t0) / 2, -0.035), (over_x, L * (t1 - t0) / 2, 0.035),
                     'shingle', 'roof', rot=(-s * a - math.radians(3) * s, 0, 0))
            ob.location = (0, yc, zc)
        # boards under the shingles (seen from inside the overhang)
        ob = box('roofboard_%d' % s, (-over_x, -L / 2, -0.04), (over_x, L / 2, 0.04), 'boards_x', 'roof', rot=(-s * a, 0, 0))
        ob.location = (0, s * over_y / 2 - ny * 0.03, (z_ridge + z_eave) / 2 - nz * 0.03)
    # ridge cap
    for s in (-1, 1):
        ob = box('ridge_%d' % s, (-over_x - 0.05, -0.13, -0.03), (over_x + 0.05, 0.13, 0.03), 'wood', 'roof',
                 rot=(-s * math.radians(40), 0, 0))
        ob.location = (0, s * 0.08, z_ridge + 0.12)


def chimney():
    box('chim_base', (X1 + 0.12, 0.4, 0.0), (X1 + 0.82, 1.3, 2.9), 'stone', 'chimney')
    box('chim_step', (X1 + 0.12, 0.45, 2.9), (X1 + 0.7, 1.25, 3.1), 'stone', 'chimney')
    box('chim_stack', (X1 + 0.14, 0.52, 3.1), (X1 + 0.66, 1.18, 4.75), 'stone', 'chimney')
    box('chim_cap', (X1 + 0.1, 0.48, 4.75), (X1 + 0.7, 1.22, 4.85), 'stone_dark', 'chimney')
    box('chim_hole', (X1 + 0.22, 0.6, 4.85), (X1 + 0.58, 1.1, 4.86), 'soot', 'chimney')


# ------------------------------------------------------------------------------------------------ inside

def floor():
    box('floor', (X0 + 0.12, Y0 + 0.12, 0.0), (X1 - 0.12, Y1 - 0.12, FLOOR), 'floor', 'floor')
    box('threshold', (-1.35, Y0 - 0.17, 0.0), (-0.25, Y0 + 0.17, FLOOR + 0.02), 'wood', 'floor')


def hearth():
    x = X1 - 0.14
    box('hearth_base', (x - 0.55, 0.3, FLOOR), (x, 1.4, 0.32), 'stone', 'interior')
    box('hearth_jamb_a', (x - 0.42, 0.3, 0.32), (x, 0.52, 1.25), 'stone', 'interior')
    box('hearth_jamb_b', (x - 0.42, 1.18, 0.32), (x, 1.4, 1.25), 'stone', 'interior')
    box('hearth_back', (x - 0.12, 0.52, 0.32), (x, 1.18, 1.25), 'soot', 'interior')
    box('hearth_mantel', (x - 0.5, 0.25, 1.25), (x, 1.45, 1.4), 'wood', 'interior')
    box('hearth_hood', (x - 0.32, 0.42, 1.4), (x, 1.28, TOP), 'stone', 'interior')
    for k in range(3):
        cyl('firelog_%d' % k, (x - 0.38, 0.58 + k * 0.17, 0.4), (x - 0.12, 0.6 + k * 0.17, 0.45), 0.05, 'wood', 'interior', sides=6)
    box('embers', (x - 0.36, 0.6, 0.32), (x - 0.12, 1.1, 0.36), 'ember', 'glow')
    box('flame_a', (x - 0.3, 0.72, 0.45), (x - 0.2, 0.86, 0.68), 'flame', 'glow', rot=(0, 0, 0.3))
    box('flame_b', (x - 0.3, 0.88, 0.45), (x - 0.22, 0.98, 0.6), 'flame', 'glow')
    # a pot and a poker
    cyl('pot', (x - 0.28, 0.75, 0.62), (x - 0.28, 0.75, 0.82), 0.12, 'iron', 'interior', sides=8)
    cyl('poker', (x - 0.48, 1.38, 0.3), (x - 0.62, 1.48, 1.0), 0.015, 'iron', 'interior', sides=4)


def bed():
    """Along the west wall, south of the trapdoor, head to the south."""
    x0, x1, y0, y1 = X0 + 0.2, X0 + 1.05, Y0 + 0.25, -0.1
    box('bed_frame', (x0, y0, FLOOR), (x1, y1, 0.42), 'wood', 'interior')
    box('bed_head', (x0, y0, FLOOR), (x1, y0 + 0.08, 0.85), 'boards_x', 'interior')
    box('bed_straw', (x0 + 0.05, y0 + 0.08, 0.42), (x1 - 0.05, y1 - 0.04, 0.56), 'straw', 'interior')
    box('bed_pillow', (x0 + 0.15, y0 + 0.12, 0.56), (x1 - 0.15, y0 + 0.42, 0.64), 'linen', 'interior')
    box('bed_blanket', (x0 + 0.02, y0 + 0.5, 0.56), (x1 - 0.02, y1 - 0.02, 0.63), 'wool', 'interior', rot=(0, 0, 0.04))
    box('bed_blanket_fold', (x0 - 0.02, y1 - 0.32, 0.62), (x1 + 0.0, y1 - 0.02, 0.68), 'wool', 'interior')


def table():
    x0, x1, y0, y1 = -0.4, 0.8, 0.3, 1.0
    box('table_top', (x0, y0, 0.72), (x1, y1, 0.8), 'wood_light', 'interior')
    for k, (x, y) in enumerate(((x0 + 0.06, y0 + 0.06), (x1 - 0.06, y0 + 0.06), (x0 + 0.06, y1 - 0.06), (x1 - 0.06, y1 - 0.06))):
        box('table_leg_%d' % k, (x - 0.04, y - 0.04, FLOOR), (x + 0.04, y + 0.04, 0.72), 'wood', 'interior')
    cyl('bowl', (0.0, 0.62, 0.8), (0.0, 0.62, 0.86), 0.11, 'clay', 'interior', sides=8, r1=0.14)
    rock('bread', (0.4, 0.75, 0.84), (0.13, 0.08, 0.06), 'bread', 'interior', seed=3, squash=1.0)
    cyl('mug', (0.6, 0.45, 0.8), (0.6, 0.45, 0.92), 0.045, 'wood', 'interior', sides=6)
    cyl('candle', (-0.2, 0.85, 0.8), (-0.2, 0.85, 0.92), 0.025, 'linen', 'interior', sides=6)
    box('candle_flame', (-0.215, 0.835, 0.92), (-0.185, 0.865, 0.97), 'flame', 'glow')
    for k, (x, y) in enumerate(((-0.75, 0.62), (0.2, 1.35))):
        cyl('stool_%d' % k, (x, y, 0.38), (x, y, 0.45), 0.17, 'wood_light', 'interior', sides=8)
        for j in range(3):
            a = j * 2.094
            cyl('stool_%d_leg_%d' % (k, j), (x + 0.1 * math.cos(a), y + 0.1 * math.sin(a), 0.38),
                (x + 0.15 * math.cos(a), y + 0.15 * math.sin(a), FLOOR), 0.025, 'wood', 'interior', sides=4)


def wall_things():
    y = Y1 - 0.16
    # shelf with jars on the north wall
    box('shelf', (0.25, y - 0.22, 1.42), (1.65, y, 1.47), 'wood_light', 'interior')
    for k in range(3):
        box('shelf_bracket_%d' % k, (0.32 + k * 0.6, y - 0.18, 1.25), (0.36 + k * 0.6, y, 1.42), 'wood', 'interior')
    for k, (x, h, r, m) in enumerate(((0.4, 0.2, 0.06, 'clay'), (0.62, 0.14, 0.05, 'linen'), (0.9, 0.24, 0.07, 'clay'),
                                       (1.2, 0.12, 0.08, 'wood'), (1.45, 0.18, 0.05, 'clay'))):
        cyl('jar_%d' % k, (x, y - 0.11, 1.47), (x, y - 0.11, 1.47 + h), r, m, 'interior', sides=6)
    # crossed axes over the bed corner
    for k, s in enumerate((-1, 1)):
        cx = -0.55
        cyl('axe_handle_%d' % k, (cx - 0.35 * s, y - 0.02, 1.1), (cx + 0.35 * s, y - 0.02, 1.9), 0.025, 'wood_light', 'interior', sides=5)
        box('axe_head_%d' % k, (cx + 0.3 * s - 0.08, y - 0.05, 1.78), (cx + 0.3 * s + 0.1, y + 0.0, 1.98), 'iron', 'interior',
            rot=(0, -0.7 * s, 0))


def firewood_and_chest():
    for row in range(3):
        for k in range(4 - (row == 2)):
            y = -0.6 + k * 0.2 + (row % 2) * 0.1
            cyl('wood_in_%d_%d' % (row, k), (X1 - 1.0, y, FLOOR + 0.08 + row * 0.15), (X1 - 0.2, y, FLOOR + 0.08 + row * 0.15),
                0.075, 'logs', 'interior', sides=6, roll=k)
    box('chest', (1.9, Y0 + 0.2, FLOOR), (2.7, Y0 + 0.72, 0.6), 'wood', 'interior')
    box('chest_lid', (1.88, Y0 + 0.18, 0.6), (2.72, Y0 + 0.74, 0.68), 'wood_light', 'interior')
    for k in range(2):
        box('chest_band_%d' % k, (2.05 + k * 0.45, Y0 + 0.16, FLOOR), (2.1 + k * 0.45, Y0 + 0.76, 0.7), 'iron', 'interior')
    box('rug', (-0.75, -1.05, FLOOR), (1.05, -0.05, FLOOR + 0.015), 'rug', 'interior')


HATCH = (-2.65, 0.85, -1.75, 1.75)         # x0, y0, x1, y1 of the trapdoor in the floor (hinge on the north edge)


def trapdoor():
    x0, y0, x1, y1 = HATCH
    box('hatch_hole', (x0, y0, FLOOR + 0.001), (x1, y1, FLOOR + 0.004), 'soot', 'interior')
    # lid, modelled around its hinge (north edge) so Unity can swing it up
    lid = []
    lid.append(box('hatch_lid', (x0, y0, FLOOR), (x1, y1, FLOOR + 0.06), 'boards_x', 'hatch'))
    for k in range(2):
        box('hatch_band_%d' % k, (x0 + 0.15 + k * 0.5, y0, FLOOR + 0.06), (x0 + 0.21 + k * 0.5, y1, FLOOR + 0.075), 'iron', 'hatch')
    cyl('hatch_ring', ((x0 + x1) / 2 - 0.08, y0 + 0.18, FLOOR + 0.08), ((x0 + x1) / 2 + 0.08, y0 + 0.18, FLOOR + 0.08),
        0.018, 'iron', 'hatch', sides=4)


DOOR = (-1.35, -0.25)                     # door opening along the south wall
DOOR_TOP = 0.14 + PITCH * 7 + LR          # 1.96


def door():
    u0, u1 = DOOR
    y = Y0 + 0.02
    box('door_leaf', (u0 + 0.02, y - 0.04, FLOOR + 0.01), (u1 - 0.02, y + 0.04, DOOR_TOP - 0.02), 'boards_x', 'door')
    for k, z in enumerate((0.45, 1.5)):
        box('door_strap_%d' % k, (u0 + 0.1, y - 0.06, z), (u1 - 0.02, y - 0.04, z + 0.07), 'iron', 'door')
    box('door_brace', (u0 + 0.1, y - 0.055, 0.5), (u1 - 0.1, y - 0.045, 0.58), 'wood', 'door', rot=(0, -0.95, 0))
    # hasp and padlock on the outside, by the latch edge
    box('door_hasp', (u0 + 0.04, y - 0.07, 1.0), (u0 + 0.2, y - 0.05, 1.08), 'iron', 'door')
    box('door_lock', (u0 + 0.06, y - 0.11, 0.86), (u0 + 0.16, y - 0.06, 1.0), 'iron', 'door')
    cyl('door_lock_bow', (u0 + 0.08, y - 0.085, 1.0), (u0 + 0.14, y - 0.085, 1.0), 0.02, 'iron', 'door', sides=4)
    # the broken door: a splintered piece still on the hinges, planks on the floor inside and the padlock
    box('broken_top', (u1 - 0.55, y - 0.04, 1.2), (u1 - 0.02, y + 0.04, DOOR_TOP - 0.02), 'boards_x', 'door_broken',
        rot=(0, 0.12, 0))
    box('broken_low', (u1 - 0.3, y - 0.04, FLOOR + 0.01), (u1 - 0.02, y + 0.04, 0.6), 'boards_x', 'door_broken')
    for k, (x, yy, a) in enumerate(((-1.0, -1.6, 0.3), (-0.6, -1.3, -0.5), (-1.25, -1.15, 1.2))):
        ob = box('broken_plank_%d' % k, (-0.08, -0.6, 0), (0.08, 0.6, 0.04), 'boards_x', 'door_broken', rot=(0, 0, a))
        ob.location = (x, yy, FLOOR + 0.02 + k * 0.012)
    box('broken_lock', (-1.5, -1.9, FLOOR), (-1.4, -1.84, FLOOR + 0.12), 'iron', 'door_broken', rot=(0, 1.3, 0.4))


# ------------------------------------------------------------------------------------------------ outside

def outside():
    # chopping block with an axe stuck in it, and chips
    cx, cy = 2.0, -3.3
    cyl('block', (cx, cy, 0.0), (cx, cy, 0.5), 0.3, 'logs', 'ext', sides=8)
    cyl('block_top', (cx, cy, 0.5), (cx, cy, 0.505), 0.29, 'log_end', 'ext', sides=8)
    cyl('block_axe', (cx - 0.05, cy, 0.52), (cx - 0.45, cy - 0.15, 1.15), 0.025, 'wood_light', 'ext', sides=5)
    box('block_axe_head', (cx - 0.12, cy - 0.02, 0.48), (cx + 0.08, cy + 0.02, 0.62), 'iron', 'ext', rot=(0, 0.4, 0.3))
    # firewood stacked against the west wall, under the eave
    for row in range(4):
        cyl('stackrow_%d' % row, (X0 - 0.55, -1.6, 0.1 + row * 0.19), (X0 - 0.55, 1.6, 0.1 + row * 0.19),
            0.11, 'logs', 'ext', sides=6, roll=row)
        cyl('stackrow_b_%d' % row, (X0 - 0.33, -1.55, 0.1 + row * 0.19), (X0 - 0.33, 1.55, 0.1 + row * 0.19),
            0.11, 'logs', 'ext', sides=6, roll=row + 0.5)
        for k in range(9):
            y = -1.5 + k * 0.37 + (row % 2) * 0.18
            if y < 1.55:
                cyl('stack_end_%d_%d' % (row, k), (X0 - 0.67, y, 0.1 + row * 0.19), (X0 - 0.66, y, 0.1 + row * 0.19),
                    0.1, 'log_end', 'ext', sides=6)
    for k in range(5):
        box('stack_post_%d' % k, (X0 - 0.7, -1.7 + k * 0.85, 0), (X0 - 0.64, -1.64 + k * 0.85, 0.85), 'wood', 'ext')
    # barrel and bucket by the door, step stone
    cyl('barrel', (0.42, -2.78, 0.0), (0.42, -2.78, 0.8), 0.27, 'boards', 'ext', sides=10)
    for k, z in enumerate((0.15, 0.65)):
        cyl('barrel_hoop_%d' % k, (0.42, -2.78, z), (0.42, -2.78, z + 0.05), 0.285, 'iron', 'ext', sides=10)
    cyl('bucket', (0.9, -2.7, 0.0), (0.9, -2.7, 0.3), 0.15, 'wood', 'ext', sides=8, r1=0.18)
    rock('step', (-0.8, -2.7, 0.02), (0.55, 0.32, 0.12), 'stone', 'ext', seed=7, squash=0.5)


# ------------------------------------------------------------------------------------------------ basement

BX0, BX1, BY0, BY1 = -2.9, 2.9, -2.3, 2.3
STAIR_X = (-2.6, -1.8)
STAIR_BOTTOM_Y = -1.25
STEP_RUN, STEP_RISE, STEPS = 0.27, 0.28, 8


def basement():
    box('b_floor', (BX0, BY0, -0.02), (BX1, BY1, 0.04), 'dirt', 'b_floor')
    for k, (x, y, r) in enumerate(((0.9, 1.35, 0.7), (-0.6, -1.2, 0.5), (1.9, 1.6, 0.4))):
        rock('b_straw_%d' % k, (x, y, 0.04), (r, r * 0.6, 0.03), 'straw', 'b_floor', seed=20 + k, squash=0.6)
    # far walls: north and east, full height, with posts and a top beam
    H = 2.5
    # north wall: an opening behind the wardrobe leads to the hidden room; the wall is its own part so it can
    # dissolve when the hero stands in that room
    o0, o1 = SECRET_DOOR
    box('b_wall_n_a', (BX0 - 0.1, BY1, 0), (o0, BY1 + 0.3, H), 'stone', 'b_north')
    box('b_wall_n_b', (o1, BY1, 0), (BX1 + 0.1, BY1 + 0.3, H), 'stone', 'b_north')
    box('b_wall_n_lintel', (o0, BY1, 2.0), (o1, BY1 + 0.3, H), 'stone', 'b_north')
    box('b_wall_e', (BX1, BY0 - 0.1, 0), (BX1 + 0.3, BY1 + 0.3, H), 'stone', 'b_back')
    for k, x in enumerate((-2.35, -1.55, 1.75)):
        box('b_post_n_%d' % k, (x - 0.09, BY1 - 0.16, 0), (x + 0.09, BY1, H), 'wood', 'b_north')
    for k, y in enumerate((-1.3, 0.6)):
        box('b_post_e_%d' % k, (BX1 - 0.16, y - 0.09, 0), (BX1, y + 0.09, H), 'wood', 'b_back')
    box('b_beam_n', (BX0, BY1 - 0.18, H - 0.22), (BX1, BY1, H - 0.04), 'wood', 'b_north')
    box('b_beam_e', (BX1 - 0.18, BY0, H - 0.22), (BX1, BY1, H - 0.04), 'wood', 'b_back')
    # near walls: south and west stay low so the camera sees in
    box('b_wall_s', (BX0 - 0.3, BY0 - 0.3, 0), (BX1 + 0.3, BY0, 0.45), 'stone', 'b_front')
    box('b_wall_w', (BX0 - 0.3, BY0 - 0.3, 0), (BX0, BY1 + 0.3, 0.45), 'stone', 'b_front')
    # stairs up to the trapdoor (they rise towards the south-west corner)
    xa, xb = STAIR_X
    B = STAIR_BOTTOM_Y
    for k in range(STEPS):
        y0 = B + k * STEP_RUN
        z = (k + 1) * STEP_RISE
        box('b_step_%d' % k, (xa + 0.04, y0, z - 0.05), (xb - 0.04, y0 + STEP_RUN + 0.02, z), 'wood_light', 'b_stairs')
    ytop = B + STEPS * STEP_RUN
    H = STEPS * STEP_RISE
    for k, x in enumerate((xa, xb - 0.05)):
        prism('b_stringer_%d' % k, [(B - 0.05, 0), (B + 0.25, 0), (ytop, H - 0.3), (ytop, H), (B - 0.05, 0.3)],
              'x', x, x + 0.05, 'wood', 'b_stairs')
    box('b_stair_post', (xb - 0.06, B - 0.04, 0), (xb, B + 0.02, 1.1), 'wood', 'b_stairs')
    # an ordinary cellar: crates, barrels, sacks, a pantry shelf, a lantern
    for k, (x, y, z) in enumerate(((1.9, -1.85, 0), (2.45, -1.85, 0), (2.15, -1.85, 0.5))):
        box('b_crate_%d' % k, (x - 0.25, y - 0.25, z), (x + 0.25, y + 0.25, z + 0.5), 'boards', 'b_props', rot=(0, 0, 0.08 * k))
    for k, (x, y) in enumerate(((2.45, -1.0), (2.45, -0.42))):
        cyl('b_barrel_%d' % k, (x, y, 0), (x, y, 0.75), 0.26, 'boards', 'b_props', sides=10)
        cyl('b_barrel_hoop_%d' % k, (x, y, 0.6), (x, y, 0.64), 0.275, 'iron', 'b_props', sides=10)
    for k, (x, y) in enumerate(((1.3, -1.95), (0.85, -2.0))):
        rock('b_sack_%d' % k, (x, y, 0.2), (0.26, 0.22, 0.3), 'linen', 'b_props', seed=14 + k, squash=1.0)
    for k, z in enumerate((0.95, 1.55)):
        box('b_shelf_%d' % k, (BX1 - 0.3, 0.15, z), (BX1, 1.95, z + 0.05), 'wood', 'b_props')
        for j in range(5):
            y = 0.3 + j * 0.35
            if (k + j) % 3 == 0:
                cyl('b_cheese_%d_%d' % (k, j), (BX1 - 0.15, y, z + 0.05), (BX1 - 0.15, y, z + 0.17), 0.13, 'bread', 'b_props', sides=8)
            else:
                cyl('b_jar_%d_%d' % (k, j), (BX1 - 0.15, y, z + 0.05), (BX1 - 0.15, y, z + 0.27 - 0.05 * (j % 2)), 0.07,
                    'clay', 'b_props', sides=6)
    for k in range(4):
        ox = (1.35, 1.55, 1.98, 2.2)[k]
        cyl('b_onions_%d' % k, (ox, BY1 - 0.12, 2.2), (ox, BY1 - 0.12, 1.75), 0.06, 'bread',
            'b_props', sides=5, r1=0.09)
    cyl('b_lantern_rope', (0.3, -0.3, 2.3), (0.3, -0.3, 1.95), 0.01, 'iron', 'b_props', sides=4)
    cyl('b_lantern', (0.3, -0.3, 1.72), (0.3, -0.3, 1.95), 0.08, 'iron', 'b_props', sides=6)
    box('b_lantern_flame', (0.27, -0.33, 1.76), (0.33, -0.27, 1.88), 'flame', 'glow_b')
    wardrobe()
    secret_room()


SECRET_DOOR = (0.15, 1.15)                    # opening in the basement's north wall, hidden by the wardrobe
WARDROBE = (0.65, BY1 - 0.29)                 # centre of the wardrobe (pivot), against the north wall
WARDROBE_SLIDE = -1.35                        # how far it slides along x when it is pushed aside
SX0, SX1, SY0, SY1 = -1.2, BX1, BY1 + 0.3, BY1 + 3.3


def wardrobe():
    cx, cy = WARDROBE
    x0, x1, y0, y1 = cx - 0.65, cx + 0.65, cy - 0.27, cy + 0.27
    box('wardrobe_body', (x0, y0, 0), (x1, y1, 2.1), 'wood', 'b_wardrobe')
    box('wardrobe_crown', (x0 - 0.05, y0 - 0.05, 2.1), (x1 + 0.05, y1 + 0.02, 2.2), 'wood_light', 'b_wardrobe')
    box('wardrobe_foot', (x0 - 0.03, y0 - 0.03, 0), (x1 + 0.03, y1, 0.1), 'wood_light', 'b_wardrobe')
    for k, (a, b) in enumerate(((x0 + 0.06, cx - 0.01), (cx + 0.01, x1 - 0.06))):
        box('wardrobe_door_%d' % k, (a, y0 - 0.03, 0.18), (b, y0, 2.0), 'boards', 'b_wardrobe')
        hx = b - 0.08 if k == 0 else a + 0.04
        box('wardrobe_handle_%d' % k, (hx, y0 - 0.06, 1.0), (hx + 0.04, y0 - 0.03, 1.2), 'iron', 'b_wardrobe')
    # scratches on the floor where it has been dragged before: the only hint
    for k in range(3):
        box('scrape_%d' % k, (x0 - 1.2, y0 + 0.05 + k * 0.18, 0.041), (x0 + 0.1, y0 + 0.08 + k * 0.18, 0.045), 'soot', 'b_floor')


def secret_room():
    """The hidden room behind the wardrobe: the woodcutter in chains and the doctor's work."""
    H = 2.5
    box('s_floor', (SX0, SY0, -0.02), (SX1, SY1, 0.04), 'dirt', 's_floor')
    rock('s_straw', (0.6, SY1 - 0.9, 0.04), (0.9, 0.5, 0.03), 'straw', 's_floor', seed=40, squash=0.6)
    box('s_wall_n', (SX0 - 0.3, SY1, 0), (SX1 + 0.3, SY1 + 0.3, H), 'stone_dark', 's_back')
    box('s_wall_e', (SX1, SY0, 0), (SX1 + 0.3, SY1 + 0.3, H), 'stone_dark', 's_back')
    box('s_beam', (SX0, SY1 - 0.18, H - 0.22), (SX1, SY1, H - 0.04), 'wood', 's_back')
    box('s_wall_w', (SX0 - 0.3, SY0, 0), (SX0, SY1 + 0.3, 0.45), 'stone_dark', 's_front')
    # the doctor's table along the east wall
    tx0, tx1, ty0, ty1 = SX1 - 0.95, SX1 - 0.1, SY0 + 0.25, SY0 + 1.45
    box('s_table', (tx0, ty0, 0.76), (tx1, ty1, 0.83), 'wood', 's_props')
    for k, (x, y) in enumerate(((tx0 + 0.06, ty0 + 0.06), (tx1 - 0.06, ty0 + 0.06), (tx0 + 0.06, ty1 - 0.06), (tx1 - 0.06, ty1 - 0.06))):
        box('s_table_leg_%d' % k, (x - 0.04, y - 0.04, 0), (x + 0.04, y + 0.04, 0.76), 'wood', 's_props')
    box('s_book_a', (tx0 + 0.1, ty0 + 0.2, 0.83), (tx0 + 0.45, ty0 + 0.47, 0.86), 'paper', 's_props', rot=(0.12, 0, 0.1))
    box('s_book_b', (tx0 + 0.1, ty0 + 0.47, 0.83), (tx0 + 0.45, ty0 + 0.74, 0.86), 'paper', 's_props', rot=(-0.12, 0, 0.1))
    cyl('s_mortar', (tx1 - 0.2, ty0 + 0.25, 0.83), (tx1 - 0.2, ty0 + 0.25, 0.95), 0.08, 'stone_dark', 's_props', sides=8, r1=0.1)
    for k, (x, y, h) in enumerate(((tx1 - 0.2, ty1 - 0.4, 0.16), (tx1 - 0.35, ty1 - 0.25, 0.12), (tx1 - 0.15, ty1 - 0.15, 0.2),
                                   (tx0 + 0.25, ty1 - 0.2, 0.1))):
        cyl('s_vial_%d' % k, (x, y, 0.83), (x, y, 0.83 + h), 0.03, 'glass_v', 's_props', sides=6)
        cyl('s_vial_glow_%d' % k, (x, y, 0.85), (x, y, 0.83 + h * 0.6), 0.025, 'bruma_glow', 'glowv_s', sides=6)
    cyl('s_candle', (tx0 + 0.15, ty0 + 0.1, 0.83), (tx0 + 0.15, ty0 + 0.1, 0.98), 0.025, 'linen', 's_props', sides=6)
    box('s_candle_flame', (tx0 + 0.135, ty0 + 0.085, 0.98), (tx0 + 0.165, ty0 + 0.115, 1.03), 'flame', 'glow_s')
    cyl('s_stool', (tx0 - 0.35, ty0 + 0.6, 0.4), (tx0 - 0.35, ty0 + 0.6, 0.47), 0.17, 'wood', 's_props', sides=8)
    for j in range(3):
        a = j * 2.094
        cyl('s_stool_leg_%d' % j, (tx0 - 0.35 + 0.1 * math.cos(a), ty0 + 0.6 + 0.1 * math.sin(a), 0.4),
            (tx0 - 0.35 + 0.15 * math.cos(a), ty0 + 0.6 + 0.15 * math.sin(a), 0.0), 0.025, 'wood', 's_props', sides=4)
    # jars of Bruma on a shelf further along the east wall
    for k, z in enumerate((1.0, 1.55)):
        box('s_shelf_%d' % k, (SX1 - 0.3, ty1 + 0.15, z), (SX1, SY1 - 0.1, z + 0.05), 'wood', 's_props')
        for j in range(4):
            y = ty1 + 0.3 + j * 0.3
            if y > SY1 - 0.15:
                continue
            cyl('s_jar_%d_%d' % (k, j), (SX1 - 0.15, y, z + 0.05), (SX1 - 0.15, y, z + 0.27), 0.07, 'glass_v', 's_props', sides=6)
            if (k + j) % 2 == 0:
                cyl('s_jar_glow_%d_%d' % (k, j), (SX1 - 0.15, y, z + 0.07), (SX1 - 0.15, y, z + 0.18), 0.055,
                    'bruma_glow', 'glowv_s', sides=6)
    rock('s_bag', (SX0 + 0.45, SY0 + 0.4, 0.12), (0.22, 0.14, 0.14), 'leather', 's_props', seed=11, squash=1.0)
    cyl('s_bucket', (SX0 + 0.5, SY1 - 0.6, 0), (SX0 + 0.5, SY1 - 0.6, 0.28), 0.14, 'wood', 's_props', sides=8, r1=0.17)
    cyl('s_lantern_rope', (0.3, SY0 + 0.9, 2.3), (0.3, SY0 + 0.9, 1.95), 0.01, 'iron', 's_props', sides=4)
    cyl('s_lantern', (0.3, SY0 + 0.9, 1.72), (0.3, SY0 + 0.9, 1.95), 0.08, 'iron', 's_props', sides=6)
    box('s_lantern_flame', (0.27, SY0 + 0.87, 1.76), (0.33, SY0 + 0.93, 1.88), 'flame', 'glow_s')


# ------------------------------------------------------------------------------------------------ assembly

PARTS = {
    'roof': {'group': 'cabin', 'role': 'roof'},
    'chimney': {'group': 'cabin', 'role': 'static'},
    'wall_S_up': {'group': 'cabin', 'role': 'cut', 'normal': (0, -1, 0)},
    'wall_W_up': {'group': 'cabin', 'role': 'cut', 'normal': (-1, 0, 0)},
    'wall_N_up': {'group': 'cabin', 'role': 'cut', 'normal': (0, 1, 0)},
    'wall_E_up': {'group': 'cabin', 'role': 'cut', 'normal': (1, 0, 0)},
    'wall_S_base': {'group': 'cabin', 'role': 'static'},
    'wall_W_base': {'group': 'cabin', 'role': 'static'},
    'wall_N_base': {'group': 'cabin', 'role': 'static'},
    'wall_E_base': {'group': 'cabin', 'role': 'static'},
    'floor': {'group': 'cabin', 'role': 'static', 'shadow': False},
    'interior': {'group': 'cabin', 'role': 'static', 'shadow': False},
    'ext': {'group': 'cabin', 'role': 'static'},
    'glow': {'group': 'cabin', 'role': 'static', 'glowColor': 'd2ac88'},
    'door': {'group': 'cabin', 'role': 'door', 'normal': (0, -1, 0), 'pivot': (DOOR[1], Y0 + 0.02, FLOOR)},
    'door_broken': {'group': 'cabin', 'role': 'door_broken', 'normal': (0, -1, 0)},
    'hatch': {'group': 'cabin', 'role': 'hatch', 'pivot': (HATCH[0], HATCH[3], FLOOR), 'shadow': False},
    'b_floor': {'group': 'basement', 'role': 'static', 'shadow': False},
    'b_back': {'group': 'basement', 'role': 'static', 'shadow': False},
    'b_front': {'group': 'basement', 'role': 'static', 'shadow': False},
    'b_stairs': {'group': 'basement', 'role': 'static', 'shadow': False},
    'b_props': {'group': 'basement', 'role': 'static', 'shadow': False},
    'b_north': {'group': 'basement', 'role': 'secret_cut', 'normal': (0, -1, 0), 'shadow': False},
    'b_wardrobe': {'group': 'basement', 'role': 'wardrobe', 'pivot': (WARDROBE[0], WARDROBE[1], 0), 'shadow': False},
    's_floor': {'group': 'secret', 'role': 'static', 'shadow': False},
    's_back': {'group': 'secret', 'role': 'static', 'shadow': False},
    's_front': {'group': 'secret', 'role': 'static', 'shadow': False},
    's_props': {'group': 'secret', 'role': 'static', 'shadow': False},
    'glow_s': {'group': 'secret', 'role': 'static'},
    'glowv_s': {'group': 'secret', 'role': 'static'},
    'glow_b': {'group': 'basement', 'role': 'static', 'glowColor': 'd2ac88'},
    'glowv_b': {'group': 'basement', 'role': 'static', 'glowColor': '9a968c', 'glowTint': '5b5170'},
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
    win = (1.0, 1.8, 4, 6)
    log_wall('S', [(DOOR[0], DOOR[1], 0, 7), (win[0], win[1], win[2], win[3])])
    log_wall('N', [])
    log_wall('E', [(-1.25, -0.5, 4, 6)])
    log_wall('W', [(-0.3, 0.35, 5, 6)])
    z_w0 = 0.14 + PITCH * 4 - LR
    z_w1 = 0.14 + PITCH * 6 + LR
    frame('S', DOOR[0], DOOR[1], FLOOR, DOOR_TOP, 'wall_S_up')
    frame('S', win[0], win[1], z_w0, z_w1, 'wall_S_up', sill=True)
    shutters('S', win[0], win[1], z_w0, z_w1, 'wall_S_up', broken=True)
    ze0, ze1 = 0.14 + PITCH * 4 + 0.12 - LR, 0.14 + PITCH * 6 + 0.12 + LR
    frame('E', -1.25, -0.5, ze0, ze1, 'wall_E_up', sill=True)
    shutters('E', -1.25, -0.5, ze0, ze1, 'wall_E_up')
    zw0, zw1 = 0.14 + PITCH * 5 + 0.12 - LR, 0.14 + PITCH * 6 + 0.12 + LR
    frame('W', -0.3, 0.35, zw0, zw1, 'wall_W_up', sill=True)
    gable('W')
    gable('E')
    roof()
    chimney()
    floor()
    hearth()
    bed()
    table()
    wall_things()
    firewood_and_chest()
    trapdoor()
    door()
    outside()
    basement()
    bpy.context.view_layer.update()
    return len(PIECES)


def meta():
    """Everything Unity needs besides the meshes (Unity coordinates)."""
    return {
        'name': 'CabanaLenador',
        'footprint': UB(X0 + 0.15, Y0 + 0.15, X1 - 0.15, Y1 - 0.15),
        'obstacles': {
            'cabin': [UB(X0 - 0.15, Y1 - 0.15, X1 + 0.15, Y1 + 0.15), UB(X1 - 0.15, Y0 - 0.15, X1 + 0.15, Y1 + 0.15),
                      UB(X0 - 0.15, Y0 - 0.15, X0 + 0.15, Y1 + 0.15), UB(X0 - 0.15, Y0 - 0.15, DOOR[0] - 0.03, Y0 + 0.15),
                      UB(DOOR[1] + 0.03, Y0 - 0.15, X1 + 0.15, Y0 + 0.15),
                      UB(X1 + 0.1, 0.38, X1 + 0.85, 1.32), UB(X0 - 0.75, -1.7, X0 - 0.15, 1.7),
                      UB(X1 - 0.7, 0.28, X1 - 0.1, 1.42), UB(X0 + 0.15, Y0 + 0.2, X0 + 1.1, -0.05),
                      UB(1.85, Y0 + 0.15, 2.75, Y0 + 0.78), UB(X1 - 1.05, -0.7, X1 - 0.15, 0.2),
                      UB(-0.45, 0.25, 0.85, 1.05), UB(1.72, -3.58, 2.28, -3.02), UB(0.14, -3.05, 0.7, -2.5)],
            'basement': [UB(BX0 - 0.2, BY1 - 0.05, SECRET_DOOR[0], BY1 + 0.3), UB(SECRET_DOOR[1], BY1 - 0.05, BX1 + 0.2, BY1 + 0.3),
                         UB(BX1 - 0.05, BY0 - 0.2, BX1 + 0.3, BY1 + 0.2),
                         UB(BX0 - 0.3, BY0 - 0.3, BX1 + 0.3, BY0 + 0.05), UB(BX0 - 0.3, BY0 - 0.3, BX0 + 0.05, BY1 + 0.3),
                         UB(STAIR_X[0] - 0.05, STAIR_BOTTOM_Y - 0.02, STAIR_X[1] + 0.05, STAIR_BOTTOM_Y + STEPS * STEP_RUN + 0.1),
                         UB(1.6, -2.15, 2.75, -1.55), UB(2.15, -1.3, 2.75, -0.12), UB(BX1 - 0.32, 0.1, BX1, 2.0),
                         UB(0.55, -2.25, 1.6, -1.7)],
            'secret': [UB(SX0 - 0.3, SY1 - 0.05, SX1 + 0.3, SY1 + 0.3), UB(SX1 - 0.05, SY0, SX1 + 0.3, SY1 + 0.3),
                       UB(SX0 - 0.3, SY0, SX0 + 0.05, SY1 + 0.3), UB(SX1 - 1.0, SY0 + 0.2, SX1, SY0 + 1.5),
                       UB(SX1 - 0.32, SY0 + 1.55, SX1, SY1 - 0.05)],
        },
        'door': {'closed': UB(DOOR[0], Y0 - 0.12, DOOR[1], Y0 + 0.12), 'openYaw': 100, 'locked': True, 'health': 70,
                 'prompt': U(-0.8, Y0 - 0.5, 0), 'inside': U(-0.8, Y0 + 0.6, 0)},
        'hatch': {'center': U((HATCH[0] + HATCH[2]) / 2, (HATCH[1] + HATCH[3]) / 2, FLOOR), 'openPitch': 100,
                  'return': U(-1.35, 1.2, 0), 'arrive': U((STAIR_X[0] + STAIR_X[1]) / 2, STAIR_BOTTOM_Y - 0.5, 0),
                  'up': U((STAIR_X[0] + STAIR_X[1]) / 2, STAIR_BOTTOM_Y - 0.35, 0)},
        'lights': [
            {'group': 'cabin', 'pos': U(X1 - 0.45, 0.85, 0.7), 'radius': 3.6, 'color': [1.0, 0.6, 0.3], 'intensity': 0.9, 'flicker': 0.45},
            {'group': 'basement', 'pos': U(0.3, -0.3, 1.8), 'radius': 4.8, 'color': [1.0, 0.64, 0.34], 'intensity': 0.95, 'flicker': 0.3},
            {'group': 'secret', 'pos': U(0.3, SY0 + 0.9, 1.8), 'radius': 3.6, 'color': [1.0, 0.62, 0.32], 'intensity': 0.8, 'flicker': 0.35},
            {'group': 'secret', 'pos': U(SX1 - 0.3, SY0 + 2.2, 1.3), 'radius': 2.4, 'color': [0.62, 0.45, 0.95], 'intensity': 0.6, 'flicker': 0.0},
        ],
        'anchors': {'occupant': U(0.6, SY1 - 0.62, 0), 'occupantGroup': 'secret'},
        'secret': {'footprint': UB(SX0, SY0 - 0.15, SX1, SY1)},
        'wardrobe': {'slide': U(WARDROBE_SLIDE, 0, 0), 'box': [0.68, 0.3], 'prompt': U(WARDROBE[0], WARDROBE[1] - 0.9, 0)},
    }
