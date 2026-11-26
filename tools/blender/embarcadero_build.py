# Barro y Bruma - the rotten pier on the Lago Hondo (POI k80, "Embarcadero podrido": nets and bones).
# Built with edificio_kit.py, game metres (scale 1.0). The model's +x runs from the shore out over the water:
# x = 0 is the first water cell. Walkway over x 0..10, y -1..1; landing over x 6..10, y 1..3 (MapWorld turns those
# cells into MapGrid.WaterDeckId). The water lies WATER below the ground.
#
# All of it is wood gone grey and black: crooked pilings (some snapped short), stringers, planks with gaps and
# missing boards (the water shows through), a stretch beyond the landing collapsed into the lake with planks
# hanging into the water, a half-sunken rowboat tied to a post. On the shore, a fisherman's shack of planks with a
# sagging roof and the door hanging open, nets drying on poles, barrels, crates, a fish basket, oars, fish bones.
import math, random
from mathutils import Vector

WATER = 0.8
DECK = 0.04
LEAF = ['1f2a1f', '2b3a2a', '3b4a33', '4f5c40']
for _h in LEAF:
    if _h not in PALETTE:
        PALETTE.append(_h)


def mats():
    pmat('plank', 'planks', ['141210', '2a2420', '3f3e3b', '594a3c'], size=(2.2, 0.25), axes='yx', mortar=0.03, grime=0.5)
    pmat('wood', 'noise', ['141210', '2a2420', '3f3e3b'], scale=3.0)
    pmat('wood_grey', 'noise', ['2a2420', '3f3e3b', '6e6c66'], scale=3.0)
    pmat('wet', 'noise', ['141210', '1f1c1a', '2a2420'], scale=3.0)
    pmat('boards', 'planks', ['141210', '2a2420', '3f3e3b', '594a3c'], size=(1.3, 0.2), axes='zy', mortar=0.035, grime=0.5)
    pmat('boards_x', 'planks', ['141210', '2a2420', '3f3e3b', '594a3c'], size=(1.3, 0.2), axes='zx', mortar=0.035, grime=0.5)
    pmat('roof', 'planks', ['141210', '2a2420', '3f3e3b'], size=(1.6, 0.22), axes='yx', mortar=0.03, grime=0.4,
         moss={'cols': ['2a2420', '2b3a2a', '3b4a33'], 'cut': 0.66})
    pmat('net', 'stripes', ['141210', '3d332b', '594a3c', '141210'], scale=16.0, dir='X')
    pmat('rope', 'noise', ['594a3c', '7a6650', '9c8a70'], scale=8.0)
    pmat('iron', 'flat', ['141210', '2f3436', '3f3e3b'])
    pmat('bone', 'noise', ['9a968c', 'c4bfb2', 'c4bfb2'], scale=6.0)
    pmat('soot', 'flat', ['141210', '141210', '2a2420'])
    pmat('stone', 'stone', ['2a2420', '3f3e3b', '6e6c66'], scale=1.6, grime=0.3)
    pmat('wicker', 'stripes', ['2a2420', '3d332b', '594a3c', '7a6650'], scale=9.0, dir='Z')
    pmat('glass', 'noise', ['2c2738', '4f5a5c', '7d8584'], scale=6.0)


def piling(name, x, y, top, rnd, broken=False):
    lean = (rnd.uniform(-0.06, 0.06), rnd.uniform(-0.06, 0.06))
    zt = top if not broken else rnd.uniform(-0.45, -0.1)
    cyl(name, (x, y, -WATER - 0.4), (x + lean[0], y + lean[1], zt), rnd.uniform(0.09, 0.12), 'wood', 'pier', sides=7, roll=x)
    cyl(name + '_wet', (x - 0.002, y, -WATER - 0.12), (x + lean[0] * 0.2, y + lean[1] * 0.2, -WATER + 0.12), 0.125, 'wet', 'pier', sides=7)


def walkway():
    rnd = random.Random(1)
    # pilings along both edges of the walkway and around the landing
    pts = [(x, s * 0.95) for x in (0.6, 2.6, 4.6, 6.6, 8.6, 9.8) for s in (-1, 1)] + [(6.6, 2.9), (8.6, 2.9), (9.8, 2.9), (9.8, 1.9)]
    for k, (x, y) in enumerate(pts):
        piling('pile_%d' % k, x, y, DECK + (0.35 if k % 5 == 0 else -0.05), rnd, broken=(k == 7))
    # stringers under the planks
    for y in (-0.8, 0.0, 0.8):
        cyl('stringer_%.1f' % y, (-0.4, y, DECK - 0.17), (10.0, y + rnd.uniform(-0.03, 0.03), DECK - 0.14), 0.07, 'wood', 'pier', sides=6)
    for y in (1.6, 2.6):
        cyl('stringer_l_%.1f' % y, (6.2, y, DECK - 0.16), (10.0, y, DECK - 0.15), 0.07, 'wood', 'pier', sides=6)
    # planks across, with gaps, some missing, some broken and tilted
    x = -0.4
    k = 0
    missing = {7, 8, 15, 27}
    while x < 10.0:
        w = rnd.uniform(0.22, 0.3)
        y0, y1 = -1.0 - rnd.uniform(0, 0.08), 1.0 + rnd.uniform(0, 0.08)
        if x > 6.0:
            y1 = 3.0 + rnd.uniform(0, 0.08)
        if k not in missing:
            tilt = rnd.uniform(-0.03, 0.03)
            if k in (12, 22):
                # one end gave way
                box('plank_%d' % k, (x, y0, DECK - 0.05), (x + w, y1 * 0.55, DECK), 'plank', 'pier', rot=(0.16, tilt, 0))
            else:
                box('plank_%d' % k, (x, y0, DECK - 0.05), (x + w, y1, DECK), 'plank', 'pier', rot=(0, tilt, rnd.uniform(-0.015, 0.015)))
        x += w + rnd.uniform(0.03, 0.07)
        k += 1
    # the collapsed stretch beyond the landing: stubs and planks hanging into the lake
    for k, (x, y) in enumerate(((11.0, -0.9), (11.2, 0.9), (12.6, -0.95), (12.9, 1.0), (14.3, 0.1))):
        piling('stub_%d' % k, x, y, -0.15, rnd, broken=True)
    for k, (x, ang, drop) in enumerate(((10.4, 0.25, 0.3), (10.9, 0.45, 0.55), (11.6, 0.6, 0.75))):
        ob = box('hang_%d' % k, (-0.13, -1.0, -0.025), (0.13, 1.0, 0.025), 'plank', 'pier', rot=(0, ang, 0.1 * k))
        ob.location = (x, 0.1 * k, DECK - drop)
    # a mooring post with a rope, an old lantern on a pole at the end of the landing
    cyl('moor_post', (9.8, -0.95, DECK), (9.8, -0.95, DECK + 0.55), 0.11, 'wood_grey', 'pier', sides=7)
    cyl('lantern_pole', (9.75, 2.85, DECK), (9.8, 2.85, DECK + 1.9), 0.05, 'wood_grey', 'pier', sides=5)
    cyl('lantern_arm', (9.8, 2.85, DECK + 1.85), (9.45, 2.85, DECK + 1.85), 0.03, 'wood_grey', 'pier', sides=4)
    box('lantern', (9.38, 2.79, DECK + 1.45), (9.52, 2.93, DECK + 1.7), 'glass', 'pier')
    box('lantern_cap', (9.36, 2.77, DECK + 1.7), (9.54, 2.95, DECK + 1.75), 'iron', 'pier')
    # coil of rope, a fish basket and a bucket on the landing; fish bones
    cyl('coil', (7.3, 2.2, DECK), (7.3, 2.2, DECK + 0.1), 0.25, 'rope', 'pier', sides=10)
    cyl('fish_basket', (8.5, 2.3, DECK), (8.5, 2.3, DECK + 0.35), 0.22, 'wicker', 'pier', sides=8, r1=0.18)
    for k in range(3):
        bx, by = 6.9 + k * 0.35, 1.5 + 0.15 * k
        cyl('fishbone_%d' % k, (bx - 0.18, by, DECK + 0.01), (bx + 0.18, by + 0.05, DECK + 0.01), 0.012, 'bone', 'pier', sides=3)
        for j in range(4):
            cyl('fishrib_%d_%d' % (k, j), (bx - 0.1 + j * 0.06, by + 0.01, DECK + 0.01), (bx - 0.1 + j * 0.06, by + 0.09, DECK + 0.01), 0.006, 'bone', 'pier', sides=3)


def boat():
    """A rowboat half under water beside the landing, tied to the mooring post."""
    bx, by = 8.4, -2.2
    rnd = random.Random(5)
    bm = bmesh.new()
    L, W, H = 1.6, 0.6, 0.4
    ring_top, ring_bot = [], []
    for i in range(12):
        a = 2 * math.pi * i / 12
        ring_top.append(bm.verts.new((L * math.cos(a), W * math.sin(a), 0.0)))
        ring_bot.append(bm.verts.new((L * 0.75 * math.cos(a), W * 0.55 * math.sin(a), -H)))
    for i in range(12):
        j = (i + 1) % 12
        bm.faces.new((ring_top[i], ring_top[j], ring_bot[j], ring_bot[i]))
    bm.faces.new(list(reversed(ring_bot)))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    _obj('boat_hull', bm, 'boards_x', 'pier', loc=(bx, by, -WATER + 0.12), rot=(0.15, -0.12, 0.35))
    # the water inside it
    bm = bmesh.new()
    bmesh.ops.create_circle(bm, cap_ends=True, segments=10, radius=1.0)
    for v in bm.verts:
        v.co.x *= 1.3
        v.co.y *= 0.45
    _obj('boat_water', bm, 'wet', 'pier', loc=(bx, by, -WATER + 0.02), rot=(0.0, 0.0, 0.35))
    cyl('boat_seat', (bx - 0.2, by - 0.45, -WATER + 0.06), (bx + 0.1, by + 0.4, -WATER + 0.1), 0.05, 'wood', 'pier', sides=4)
    cyl('boat_oar', (bx + 0.4, by - 0.3, -WATER + 0.15), (bx + 1.8, by - 0.9, -WATER + 0.02), 0.025, 'wood_grey', 'pier', sides=4)
    cyl('boat_rope', (bx + 1.2, by + 0.3, -WATER + 0.15), (9.8, -0.95, DECK + 0.4), 0.015, 'rope', 'pier', sides=4)


def shack():
    """Fisherman's shack on the shore (x < 0), door open, nets drying beside it."""
    x0, x1, y0, y1 = -5.6, -2.2, -2.6, -0.2
    H = 2.3
    rnd = random.Random(9)
    for k, (x, y) in enumerate(((x0, y0), (x1, y0), (x0, y1), (x1, y1))):
        cyl('shack_post_%d' % k, (x, y, 0), (x + rnd.uniform(-0.05, 0.05), y, H + 0.1 * (y > -1)), 0.08, 'wood', 'shack', sides=6)
    door = (-4.4, -3.6)
    box('shack_wall_s_a', (x0, y0 - 0.05, 0), (door[0], y0 + 0.05, H), 'boards_x', 'shack')
    box('shack_wall_s_b', (door[1], y0 - 0.05, 0), (x1, y0 + 0.05, H), 'boards_x', 'shack')
    box('shack_wall_s_top', (door[0], y0 - 0.05, 1.9), (door[1], y0 + 0.05, H), 'boards_x', 'shack')
    box('shack_dark', (door[0], y0 + 0.06, 0), (door[1], y0 + 0.08, 1.9), 'soot', 'shack')
    box('shack_wall_n', (x0, y1 - 0.05, 0), (x1, y1 + 0.05, H + 0.25), 'boards_x', 'shack')
    box('shack_wall_w', (x0 - 0.05, y0, 0), (x0 + 0.05, y1, H + 0.1), 'boards', 'shack')
    box('shack_wall_e', (x1 - 0.05, y0, 0), (x1 + 0.05, y1, H + 0.1), 'boards', 'shack')
    # box() centres the mesh on its object origin, so the door is placed by its centre, swung out on its hinge
    dw = door[1] - door[0]
    ob = box('shack_door', (-dw / 2, -0.035, -0.915), (dw / 2, 0.035, 0.915), 'boards_x', 'shack', rot=(0, 0, -1.9))
    a = -1.9
    ob.location = (door[1] - math.cos(a) * dw / 2, y0 - math.sin(a) * dw / 2, 0.935)
    # sagging plank roof, sloping to the south
    ang = math.atan2(0.45, y1 - y0)
    for k, (a0, a1, dz, tilt, roll) in enumerate(((x0 - 0.35, -3.9, 0.1, 0.0, 0.03), (-3.9, x1 + 0.35, 0.04, 0.05, -0.04))):
        hx = (a1 - a0) / 2
        ob = box('shack_roof_%d' % k, (-hx, -(y1 - y0) / 2 - 0.4, -0.04), (hx, (y1 - y0) / 2 + 0.3, 0.04), 'roof', 'shack',
                 rot=(ang + tilt, roll, 0))
        ob.location = ((a0 + a1) / 2, (y0 + y1) / 2 - 0.05, H + dz + 0.2)
    # nets on the shack wall and on two poles
    box('net_wall', (x1 + 0.06, y0 + 0.3, 0.6), (x1 + 0.09, y1 - 0.3, 1.9), 'net', 'shack', rot=(0.06, 0, 0))
    for k, y in enumerate((1.1, 3.4)):
        cyl('net_pole_%d' % k, (-3.0, y, 0), (-3.05, y, 1.8), 0.05, 'wood_grey', 'ext', sides=5)
    cyl('net_line', (-3.0, 1.1, 1.75), (-3.05, 3.4, 1.72), 0.012, 'rope', 'ext', sides=3)
    for k in range(3):
        ob = box('net_hang_%d' % k, (-0.02, -0.38, -0.575), (0.02, 0.38, 0.575), 'net', 'ext', rot=(0.04 * k, 0.05 - 0.05 * k, 0))
        ob.location = (-3.02, 1.55 + k * 0.75, 1.72 - 0.575)
    # barrels, crates, oars, an anchor stone, a gutting board
    for k, (x, y) in enumerate(((-1.7, -2.4), (-1.3, -1.9))):
        cyl('barrel_%d' % k, (x, y, 0), (x, y, 0.78), 0.27, 'boards', 'ext', sides=10)
        cyl('barrel_hoop_%d' % k, (x, y, 0.62), (x, y, 0.66), 0.285, 'iron', 'ext', sides=10)
    for k, (x, y, z) in enumerate(((-6.1, -2.2, 0.0), (-6.1, -1.55, 0.0), (-6.1, -1.9, 0.55))):
        box('crate_%d' % k, (x - 0.28, y - 0.28, z), (x + 0.28, y + 0.28, z + 0.55), 'boards', 'ext', rot=(0, 0, 0.1 * k))
    for k in range(2):
        cyl('oar_%d' % k, (-5.75 - 0.1 * k, -0.05, 0.0), (-5.85 - 0.1 * k, 0.0, 2.0), 0.025, 'wood_grey', 'ext', sides=4)
    rock('anchor_stone', (-1.4, 0.8, 0.1), (0.25, 0.22, 0.18), 'stone', 'ext', seed=21, squash=0.8)
    cyl('anchor_rope', (-1.4, 0.8, 0.25), (-1.0, 0.6, 0.02), 0.015, 'rope', 'ext', sides=3)
    box('gut_board', (-2.0, 1.6, 0.6), (-1.1, 2.1, 0.66), 'wood_grey', 'ext')
    for k, (x, y) in enumerate(((-1.9, 1.65), (-1.2, 2.05))):
        box('gut_leg_%d' % k, (x - 0.04, y - 0.04, 0), (x + 0.04, y + 0.04, 0.6), 'wood', 'ext')
    for k in range(2):
        bx, by = -1.75 + k * 0.4, 1.85
        cyl('gut_bone_%d' % k, (bx - 0.15, by, 0.67), (bx + 0.15, by + 0.03, 0.67), 0.012, 'bone', 'ext', sides=3)


PARTS = {
    'pier': {'group': 'cabin', 'role': 'static'},
    'shack': {'group': 'cabin', 'role': 'static'},
    'ext': {'group': 'cabin', 'role': 'static'},
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
    walkway()
    boat()
    shack()
    bpy.context.view_layer.update()
    return len(PIECES)


def ground():
    pmat('ground_ao', 'flat', ['3d332b', '3d332b', '3d332b'])
    bpy.ops.mesh.primitive_plane_add(size=40, location=(10, 0, -WATER))
    w = bpy.context.active_object
    w.name = 'water_ao'
    w.data.materials.append(MATS['ground_ao'])
    bpy.ops.mesh.primitive_cube_add(size=1, location=(-10, 0, -WATER / 2))
    b = bpy.context.active_object
    b.scale = (20, 40, WATER)
    b.data.materials.append(MATS['ground_ao'])
    return [w, b]


def meta():
    return {
        'name': 'Embarcadero',
        'scale': 1.0,
        'footprint': UB(-5.6, -2.6, -2.2, -0.2),
        'obstacles': {
            'cabin': [UB(-5.7, -2.7, -2.1, -0.1), UB(-2.0, -2.7, -1.0, -1.6), UB(-6.45, -2.55, -5.75, -1.25),
                      UB(-3.15, 1.0, -2.9, 3.5), UB(-2.05, 1.55, -1.05, 2.15), UB(9.65, -1.1, 9.95, -0.8),
                      UB(9.6, 2.7, 9.95, 3.0), UB(8.25, 2.05, 8.75, 2.55)],
        },
    }
