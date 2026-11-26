# Barro y Bruma - the stone bridge over the Rio Ceniza (POI k7, "Puente de piedra", the only crossing).
# Built with edificio_kit.py like the houses, but in game metres (scale 1.0): it has to fit the map cells.
# The bridge runs along x. The water under it (cells MapGrid.WaterDeckId) spans x -4..4, y -4..4 and lies
# WATER_DEPTH below the ground; the abutments sit on the banks at x +-4..7.
#
# An old beam bridge of big dressed stones, not arches: a thick deck on three massive piers with pointed
# cutwaters against the current (north), low parapets with coping stones that flare into wing walls at both ends.
# Worn: cobbles with cart ruts, moss and ivy hanging down the face, a stretch of the far parapet fallen into the
# river and patched with a wooden rail, blocks lying on the deck and in the water. At the west end, a weathered
# stele with a niche and a candle stub; at the east end, crates and barrels pushed against the parapet (the
# bandits use the bridge for their ambushes).
import math, random
from mathutils import Vector

WATER = 0.8                     # must match MapGrid.WaterDepth / WATER_DEPTH in PPMapGround
HX = 4.0                        # half length of the water under the bridge
HW = 4.0                        # half width of the road
LX = 7.0                        # half length of the whole bridge (abutments)
PAR_IN, PAR_OUT = 3.55, 4.1     # parapet, from the road edge inwards
PAR_H = 0.95
DECK = 0.04

LEAF = ['1f2a1f', '2b3a2a', '3b4a33', '4f5c40']
for _h in LEAF:
    if _h not in PALETTE:
        PALETTE.append(_h)


def mats():
    pmat('ashlar', 'bricks', ['2a2420', '3f3e3b', '6e6c66', '7d8584'], size=(0.7, 0.35), axes='xz', mortar=0.05, grime=0.45,
         moss={'cols': ['2a2420', '2b3a2a', '3b4a33'], 'cut': 0.72})
    pmat('ashlar_y', 'bricks', ['2a2420', '3f3e3b', '6e6c66', '7d8584'], size=(0.7, 0.35), axes='yz', mortar=0.05, grime=0.45,
         moss={'cols': ['2a2420', '2b3a2a', '3b4a33'], 'cut': 0.72})
    pmat('coping', 'bricks', ['3f3e3b', '6e6c66', '7d8584', '9a968c'], size=(0.9, 0.5), axes='xy', mortar=0.04, grime=0.3,
         moss={'cols': ['2a2420', '2b3a2a', '3b4a33'], 'cut': 0.6})
    pmat('cobbles', 'stone', ['2a2420', '3f3e3b', '594a3c', '6e6c66'], scale=2.6, mortar=0.06, grime=0.3)
    pmat('ruts', 'flat', ['2a2420', '2a2420', '3d332b'])
    pmat('stone', 'stone', ['2a2420', '3f3e3b', '6e6c66', '7d8584'], scale=1.6, grime=0.35)
    pmat('stone_wet', 'stone', ['141210', '1f1c1a', '2a2420', '3f3e3b'], scale=1.6)
    pmat('ivy', 'noise', ['1f2a1f', '2b3a2a', '3b4a33', '4f5c40'], scale=9.0)
    pmat('wood', 'noise', ['2a2420', '3d332b', '594a3c'], scale=3.0)
    pmat('boards', 'planks', ['2a2420', '3d332b', '594a3c', '7a6650'], size=(1.3, 0.17), axes='zy', mortar=0.03, grime=0.3)
    pmat('boards_x', 'planks', ['2a2420', '3d332b', '594a3c', '7a6650'], size=(1.3, 0.17), axes='zx', mortar=0.03, grime=0.3)
    pmat('iron', 'flat', ['141210', '2f3436', '3f3e3b'])
    pmat('linen', 'noise', ['594a3c', '6e6c66', '9a968c'], scale=5.0)
    pmat('rope', 'noise', ['594a3c', '7a6650', '9c8a70'], scale=8.0)
    pmat('soot', 'flat', ['141210', '141210', '2a2420'])
    pmat('flame', 'flat', ['d2ac88', 'e6e2d4', 'd2ac88'], glow=True)


# ------------------------------------------------------------------------------------------------ structure

def deck():
    # the road surface, then the thick slab under it (its faces show over the water)
    box('cobbles', (-LX, -PAR_IN, DECK - 0.06), (LX, PAR_IN, DECK), 'cobbles', 'deck')
    for k, y in enumerate((-1.2, 1.1)):
        box('rut_%d' % k, (-LX + 0.2, y - 0.18, DECK), (LX - 0.2, y + 0.18, DECK + 0.004), 'ruts', 'deck')
    box('slab_s', (-HX - 0.4, -PAR_OUT, -0.55), (HX + 0.4, -PAR_IN + 0.3, DECK - 0.06), 'ashlar', 'deck')
    box('slab_n', (-HX - 0.4, PAR_IN - 0.3, -0.55), (HX + 0.4, PAR_OUT, DECK - 0.06), 'ashlar', 'deck')
    box('slab_mid', (-HX - 0.4, -PAR_IN + 0.3, -0.45), (HX + 0.4, PAR_IN - 0.3, DECK - 0.06), 'stone', 'deck')
    # a string course of projecting stones under the parapets
    for s in (-1, 1):
        box('string_%d' % s, (-HX - 0.4, s * PAR_OUT - 0.08 * (s < 0), -0.62), (HX + 0.4, s * PAR_OUT + 0.08 * (s > 0), -0.5), 'coping', 'deck')


def pier(x, k):
    """Massive pier with pointed cutwaters, from below the water up to the slab."""
    w = 0.55
    zb, zt = -WATER - 0.35, -0.5
    box('pier_%d' % k, (x - w, -PAR_OUT + 0.1, zb), (x + w, PAR_OUT - 0.1, zt), 'ashlar_y', 'deck')
    for s in (-1, 1):
        y0 = s * (PAR_OUT - 0.1)
        y1 = s * (PAR_OUT + 0.95)
        pts = [(x - w, zb), (x + w, zb), (x + w, zt), (x - w, zt)]
        # pointed nose: a prism whose section narrows to the tip
        bm = bmesh.new()
        a = [bm.verts.new((x - w, y0, zb)), bm.verts.new((x + w, y0, zb)), bm.verts.new((x + w, y0, zt)), bm.verts.new((x - w, y0, zt))]
        tip = [bm.verts.new((x, y1, zb)), bm.verts.new((x, y1 - s * 0.2, zt))]
        bm.faces.new((a[0], a[1], tip[0]))
        bm.faces.new((a[3], tip[1], a[2]))
        bm.faces.new((a[0], tip[0], tip[1], a[3]))
        bm.faces.new((a[1], a[2], tip[1], tip[0]))
        bm.faces.new((a[0], a[3], a[2], a[1]))
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        _obj('cutwater_%d_%d' % (k, s), bm, 'ashlar' if s < 0 else 'ashlar', 'deck')
        # dark wet band at the water line
        box('pier_wet_%d_%d' % (k, s), (x - w - 0.01, s * (PAR_OUT - 0.1) - (0.01 if s < 0 else -0.01) - (0 if s < 0 else 0.0), -WATER - 0.1),
            (x + w + 0.01, s * (PAR_OUT - 0.1) + (0.01 if s > 0 else -0.01), -WATER + 0.08), 'stone_wet', 'deck')


def abutments():
    for s in (-1, 1):
        x0, x1 = (s * HX, s * (HX + 0.9)) if s > 0 else (s * (HX + 0.9), s * HX)
        box('abut_%d' % s, (x0, -PAR_OUT, -WATER - 0.3), (x1, PAR_OUT, DECK - 0.06), 'ashlar_y', 'deck')
        box('abut_wet_%d' % s, (x0 - 0.01, -PAR_OUT - 0.01, -WATER - 0.1), (x1 + 0.01, -PAR_OUT + 0.3, -WATER + 0.06), 'stone_wet', 'deck')


def parapets():
    rnd = random.Random(4)
    gap = (0.7, 2.3)                                   # the fallen stretch, on the far (north) side
    for s in (-1, 1):
        part = 'parapet_S' if s < 0 else 'parapet_N'
        y0, y1 = (-PAR_OUT, -PAR_IN) if s < 0 else (PAR_IN, PAR_OUT)
        spans = [(-5.6, 5.6)] if s < 0 else [(-5.6, gap[0]), (gap[1], 5.6)]
        for j, (a, b) in enumerate(spans):
            box('par_%d_%d' % (s, j), (a, y0, DECK - 0.06), (b, y1, PAR_H), 'ashlar', part)
            # coping stones, one by one, a few out of line
            x = a
            k = 0
            while x < b - 0.05:
                L = min(rnd.uniform(0.75, 1.0), b - x)
                dz = rnd.uniform(-0.02, 0.02)
                rz = rnd.uniform(-0.03, 0.03)
                box('cope_%d_%d_%d' % (s, j, k), (x + 0.02, y0 - 0.06, PAR_H + dz), (x + L - 0.02, y1 + 0.06, PAR_H + 0.13 + dz), 'coping', part,
                    rot=(0, 0, rz))
                x += L
                k += 1
        # wing walls splaying out at both ends
        for e in (-1, 1):
            xa = e * 5.6
            xb = e * 7.0
            ya = s * (PAR_IN + PAR_OUT) / 2
            yb = s * (PAR_OUT + 0.9)
            mid = Vector(((xa + xb) / 2, (ya + yb) / 2, 0))
            L = math.hypot(xb - xa, yb - ya)
            ang = math.atan2(yb - ya, xb - xa)
            # box() puts the object at the centre of the box: place each piece by its centre
            ob = box('wing_%d_%d' % (s, e), (-L / 2 - 0.15, -0.27, DECK - 0.06), (L / 2, 0.27, PAR_H - 0.25), 'ashlar', part, rot=(0, 0, ang))
            ob.location = (mid.x, mid.y, (DECK - 0.06 + PAR_H - 0.25) / 2)
            ob = box('wing_cope_%d_%d' % (s, e), (-L / 2 - 0.15, -0.32, PAR_H - 0.25), (L / 2 + 0.05, 0.32, PAR_H - 0.13), 'coping', part, rot=(0, 0.06 * e, ang))
            ob.location = (mid.x, mid.y, PAR_H - 0.19)
            ob = box('wing_end_%d_%d' % (s, e), (-0.3, -0.3, DECK - 0.06), (0.3, 0.3, PAR_H - 0.1), 'ashlar', part)
            ob.location = (xb, yb, (DECK - 0.06 + PAR_H - 0.1) / 2)
    # the gap: broken stumps of the wall, a wooden rail across it, blocks fallen on the deck and in the river
    box('gap_stump', (gap[0], PAR_IN, DECK - 0.06), (gap[1], PAR_OUT, 0.32), 'ashlar', 'parapet_N')
    for k, x in enumerate((gap[0] + 0.15, gap[1] - 0.15)):
        cyl('rail_post_%d' % k, (x, (PAR_IN + PAR_OUT) / 2, 0.3), (x + 0.03, (PAR_IN + PAR_OUT) / 2, 1.0), 0.06, 'wood', 'parapet_N', sides=6)
    cyl('rail', (gap[0] + 0.05, (PAR_IN + PAR_OUT) / 2, 0.88), (gap[1] - 0.05, (PAR_IN + PAR_OUT) / 2 + 0.03, 0.82), 0.05, 'wood', 'parapet_N', sides=6)
    cyl('rail_rope', (gap[0] + 0.15, (PAR_IN + PAR_OUT) / 2 - 0.06, 0.95), (gap[1] - 0.15, (PAR_IN + PAR_OUT) / 2 - 0.06, 0.95), 0.015, 'rope', 'parapet_N', sides=4)
    for k, (x, y, r) in enumerate(((1.1, 2.9, 0.0), (1.8, 3.1, 0.6), (2.6, 2.6, -0.4))):
        box('fallen_%d' % k, (-0.35, -0.2, 0.0), (0.35, 0.2, 0.32), 'ashlar', 'deck', rot=(0.08 * k, 0.1, r))
        PIECES[-1].location = (x, y, DECK + 0.16)
    for k, (x, y) in enumerate(((1.2, 5.2), (2.0, 5.6), (1.5, 4.7))):
        rock('river_block_%d' % k, (x, y, -WATER - 0.05), (0.35, 0.28, 0.22), 'stone', 'deck', seed=40 + k, squash=0.8)


def ivy():
    """Ivy and moss hanging down the south face (the one the camera sees)."""
    rnd = random.Random(8)
    for k in range(9):
        x = rnd.uniform(-HX, HX)
        L = rnd.uniform(0.35, WATER - 0.05)
        w = rnd.uniform(0.15, 0.4)
        box('ivy_%d' % k, (x - w / 2, -PAR_OUT - 0.035, DECK - L), (x + w / 2, -PAR_OUT - 0.015, PAR_H * rnd.uniform(0.2, 0.9)), 'ivy', 'deck')
        for j in range(3):
            box('ivy_leaf_%d_%d' % (k, j), (x - w / 2 - 0.04, -PAR_OUT - 0.05, DECK - L * (j + 1) / 3),
                (x + w / 2 + 0.04, -PAR_OUT - 0.03, DECK - L * (j + 1) / 3 + 0.12), 'ivy', 'deck')


def ends():
    # stele with a niche and a candle stub at the west end, by the south wing wall
    sx, sy = -7.6, -3.9
    box('stele', (sx - 0.3, sy - 0.18, 0.0), (sx + 0.3, sy + 0.18, 1.35), 'stone', 'ext', rot=(0.03, -0.04, 0.1))
    box('stele_cap', (sx - 0.36, sy - 0.22, 1.35), (sx + 0.36, sy + 0.22, 1.47), 'coping', 'ext', rot=(0.03, -0.04, 0.1))
    box('stele_niche', (sx - 0.15, sy - 0.2, 0.75), (sx + 0.15, sy - 0.12, 1.1), 'soot', 'ext', rot=(0.03, -0.04, 0.1))
    cyl('stele_candle', (sx, sy - 0.19, 0.76), (sx, sy - 0.19, 0.83), 0.025, 'linen', 'ext', sides=6)
    box('stele_flame', (sx - 0.012, sy - 0.2, 0.83), (sx + 0.012, sy - 0.18, 0.87), 'flame', 'glow')
    rock('stele_base', (sx, sy, 0.03), (0.45, 0.32, 0.1), 'stone', 'ext', seed=50, squash=0.6)
    # bandits' things at the east end, against the north wing wall
    for k, (x, y, z, r) in enumerate(((6.5, 3.1, 0.0, 0.1), (7.1, 3.0, 0.0, -0.2), (6.8, 3.05, 0.55, 0.3))):
        box('crate_%d' % k, (-0.28, -0.28, 0.0), (0.28, 0.28, 0.55), 'boards', 'ext', rot=(0, 0, r))
        PIECES[-1].location = (x, y, DECK + z + 0.275)
    for k, (x, y) in enumerate(((7.6, 2.6), (6.1, 2.75))):
        cyl('barrel_%d' % k, (x, y, DECK), (x, y, DECK + 0.78), 0.27, 'boards', 'ext', sides=10)
        cyl('barrel_hoop_%d' % k, (x, y, DECK + 0.6), (x, y, DECK + 0.64), 0.285, 'iron', 'ext', sides=10)
    ob = box('plank_lean', (-0.12, -0.9, 0.0), (0.12, 0.9, 0.04), 'boards_x', 'ext', rot=(0, 1.1, 0.2))
    ob.location = (5.7, 3.2, 0.55)


PARTS = {
    'deck': {'group': 'cabin', 'role': 'static'},
    'parapet_S': {'group': 'cabin', 'role': 'cut', 'normal': (0, -1, 0)},
    'parapet_N': {'group': 'cabin', 'role': 'static'},
    'ext': {'group': 'cabin', 'role': 'static'},
    'glow': {'group': 'cabin', 'role': 'static', 'glowColor': 'd2ac88'},
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
    deck()
    for k, x in enumerate((-2.0, 2.0)):
        pier(x, k)
    abutments()
    parapets()
    ivy()
    ends()
    bpy.context.view_layer.update()
    return len(PIECES)


def ground():
    """Banks and water for the bake only: occlusion under the parapets, not exported."""
    bpy.ops.mesh.primitive_plane_add(size=30, location=(0, 0, -WATER))
    w = bpy.context.active_object
    w.name = 'water_ao'
    pmat('ground_ao', 'flat', ['3d332b', '3d332b', '3d332b'])
    w.data.materials.append(MATS['ground_ao'])
    obs = [w]
    for s in (-1, 1):
        bpy.ops.mesh.primitive_cube_add(size=1, location=(s * (HX + 6), 0, -WATER / 2))
        b = bpy.context.active_object
        b.scale = (12, 30, WATER)
        b.data.materials.append(MATS['ground_ao'])
        obs.append(b)
    return obs


def meta():
    return {
        'name': 'PuentePiedra',
        'scale': 1.0,
        'footprint': UB(-LX, -PAR_IN, LX, PAR_IN),
        'obstacles': {
            'cabin': [UB(-5.6, -PAR_OUT, 5.6, -PAR_IN), UB(-5.6, PAR_IN, 5.6, PAR_OUT),
                      UB(-7.1, -4.9, -5.5, -3.6), UB(5.5, -4.9, 7.1, -3.6), UB(-7.1, 3.6, -5.5, 4.9), UB(5.5, 3.6, 7.1, 4.9),
                      UB(-8.0, -4.2, -7.2, -3.6), UB(5.8, 2.4, 7.9, 3.4), UB(0.75, 2.4, 2.95, 3.4)],
        },
        'lights': [
            {'group': 'cabin', 'pos': U(-7.6, -4.15, 0.85), 'radius': 1.6, 'color': [1.0, 0.65, 0.35], 'intensity': 0.55, 'flicker': 0.35},
        ],
        'searchables': [
            {'name': 'Cajones', 'what': 'los cajones de los bandidos', 'table': 'cajon', 'at': U(6.8, 3.05, 0), 'group': 'cabin'},
            {'name': 'Barril', 'what': 'el barril', 'table': 'barril', 'at': U(7.6, 2.6, 0), 'group': 'cabin'},
        ],
    }
