# Barro y Bruma - three places of the chapter map, in the building pipeline (real metres, scaled 1.4 in Unity):
#   'establo'  Establo quemado (k85): the charred frame of a stable, the roof fallen in, stalls, burnt hay, bones.
#   'carreta'  Carreta volcada (k76): a merchant's cart on its side, a wheel off, goods spilled, arrows in the boards,
#              the merchant under his cloak and his satchel (the note about the Toad King).
#   'altar'    Altar torcido (k83): a leaning slab on a heap of stones, crooked standing stones in a ring, a squat
#              crowned frog idol, offerings (bones, fish, bowls, candles), charms on cords.
# build_all(kind); unwrap(PIECES); bake(...); export(json, meta(), PARTS)   (edificio_kit.py first)
import math, random
from mathutils import Vector

LEAF = ['1f2a1f', '2b3a2a', '3b4a33', '4f5c40']
for _h in LEAF:
    if _h not in PALETTE:
        PALETTE.append(_h)

K = {}
OBS = []
SEARCH = []


def mats():
    pmat('char', 'noise', ['141210', '141210', '1f1c1a', '2a2420', '6b2a24'], scale=5.0)
    pmat('char_beam', 'logs', ['141210', '1f1c1a', '2a2420', '3d332b'], scale=1.0)
    pmat('beam', 'noise', ['141210', '2a2420', '3d332b'], scale=3.0)
    pmat('wood', 'noise', ['2a2420', '3d332b', '594a3c'], scale=3.0)
    pmat('wood_light', 'noise', ['594a3c', '7a6650', '9c8a70'], scale=3.0)
    pmat('boards', 'planks', ['2a2420', '3d332b', '594a3c', '7a6650'], size=(1.3, 0.17), axes='zy', mortar=0.03, grime=0.35)
    pmat('boards_x', 'planks', ['2a2420', '3d332b', '594a3c', '7a6650'], size=(1.3, 0.17), axes='zx', mortar=0.03, grime=0.35)
    pmat('boards_f', 'planks', ['2a2420', '3d332b', '594a3c'], size=(1.6, 0.2), axes='xy', mortar=0.03, grime=0.3)
    pmat('ash', 'dirt', ['141210', '1f1c1a', '3f3e3b', '6e6c66'], scale=2.0)
    pmat('straw', 'noise', ['7a6650', '9c8a70', 'b08a6a'], scale=9.0)
    pmat('straw_burnt', 'noise', ['141210', '3d332b', '594a3c', '7a6650'], scale=9.0)
    pmat('bone', 'noise', ['9a968c', 'c4bfb2', 'c4bfb2'], scale=6.0)
    pmat('iron', 'flat', ['141210', '2f3436', '3f3e3b'])
    pmat('stone', 'stone', ['2a2420', '3f3e3b', '594a3c', '6e6c66'], scale=1.6, grime=0.4,
         moss={'cols': ['2a2420', '2b3a2a', '3b4a33'], 'cut': 0.66})
    pmat('stone_dark', 'stone', ['141210', '2a2420', '3f3e3b', '6e6c66'], scale=1.8, grime=0.3,
         moss={'cols': ['141210', '2b3a2a', '3b4a33'], 'cut': 0.6})
    pmat('monolith', 'noise', ['141210', '2a2420', '3f3e3b', '6e6c66', '7d8584'], scale=2.2, grime=0.5, detail=5.0,
         moss={'cols': ['141210', '2b3a2a', '3b4a33'], 'cut': 0.62})
    pmat('idol', 'stone', ['1f2a1f', '2a2420', '3b4a33', '4f5c40', '6e6c66'], scale=2.5, mortar=0.03)
    pmat('crown', 'noise', ['3d332b', '7a6650', 'b08a6a'], scale=6.0)
    pmat('cloak', 'noise', ['141210', '2a2420', '2f3436', '3f3e3b'], scale=4.0)
    pmat('linen', 'noise', ['594a3c', '6e6c66', '9a968c'], scale=5.0)
    pmat('leather', 'noise', ['141210', '2a2420', '3d332b'], scale=5.0)
    pmat('rope', 'noise', ['594a3c', '7a6650', '9c8a70'], scale=8.0)
    pmat('cloth_r', 'stripes', ['3e1a18', '6b2a24', '3d332b', '6b2a24'], scale=2.5, dir='Y')
    pmat('clay', 'noise', ['3e1a18', '6b2a24', '8f4a36'], scale=6.0)
    pmat('fish', 'noise', ['3f3e3b', '6e6c66', '9a968c'], scale=7.0)
    pmat('fletch', 'flat', ['9a968c', 'c4bfb2', 'c4bfb2'])
    pmat('mud', 'noise', ['141210', '1d2622', '28342e', '2a2420'], scale=2.5)
    pmat('soot', 'flat', ['141210', '141210', '2a2420'])
    pmat('flame', 'flat', ['d2ac88', 'e6e2d4', 'd2ac88'], glow=True)
    pmat('glow_v', 'flat', ['5b5170', '8a7ab0', '8a7ab0'], glow=True)


def blob(name, part, c, size, seed, mat, n=20, jag=0.2, under=0.5):
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
    c, h = Vector(c), Vector(half)
    return box(name, c - h, c + h, mat, part, rot=rot)


def patch(name, cx, cy, rx, ry, mat, part, seed, z=0.006):
    """An irregular flat patch on the ground (ash, mud)."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    c = bm.verts.new((0, 0, z))
    n = 14
    vs = []
    for i in range(n):
        a = 2 * math.pi * i / n
        k = rnd.uniform(0.75, 1.15)
        vs.append(bm.verts.new((rx * k * math.cos(a), ry * k * math.sin(a), z)))
    for i in range(n):
        bm.faces.new((c, vs[i], vs[(i + 1) % n]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    for f in bm.faces:
        if f.normal.z < 0:
            f.normal_flip()
    return _obj(name, bm, mat, part, loc=(cx, cy, 0))


def wheel(name, c, axis_rot, r, part, broken=0):
    """A cart wheel: rim of segments, spokes, hub. axis_rot = (rx, ry, rz) turns a wheel lying in the XZ plane."""
    from mathutils import Euler
    E = Euler(axis_rot).to_matrix()
    c = Vector(c)
    n = 12
    for k in range(n - broken):
        a0, a1 = 2 * math.pi * k / n, 2 * math.pi * (k + 1) / n
        p0 = c + E @ Vector((r * math.cos(a0), 0, r * math.sin(a0)))
        p1 = c + E @ Vector((r * math.cos(a1), 0, r * math.sin(a1)))
        cyl('%s_rim_%d' % (name, k), p0, p1, 0.045, 'wood', part, sides=5)
    for k in range(6 - (1 if broken else 0)):
        a = 2 * math.pi * k / 6
        cyl('%s_spoke_%d' % (name, k), c, c + E @ Vector((r * 0.95 * math.cos(a), 0, r * 0.95 * math.sin(a))), 0.025, 'wood', part, sides=4)
    cyl('%s_hub' % name, c + E @ Vector((0, -0.12, 0)), c + E @ Vector((0, 0.12, 0)), 0.09, 'wood_light', part, sides=8)


# ------------------------------------------------------------------------------------------------ establo

def establo():
    P = 'main'
    X, Y = 3.6, 2.6
    patch('ash_floor', 0.0, 0.0, X + 0.8, Y + 0.7, 'ash', P, 1)
    rnd = random.Random(2)
    # charred posts, some broken short, some leaning
    posts = [(-X, -Y, 2.3), (0.0, -Y, 0.9), (X, -Y, 1.6), (-X, 0.0, 2.6), (X, 0.0, 2.1), (-X, Y, 2.5), (0.0, Y, 2.9), (X, Y, 1.2),
             (-1.2, -Y, 0.5), (1.2, Y, 2.0)]
    for k, (x, y, h) in enumerate(posts):
        lean = (rnd.uniform(-0.15, 0.15), rnd.uniform(-0.15, 0.15))
        cyl('post_%d' % k, (x, y, 0.0), (x + lean[0], y + lean[1], h), 0.13, 'char_beam', P, sides=7)
        OBS.append((x - 0.18, y - 0.18, x + 0.18, y + 0.18))
    # what is left of the top beams, and rafters fallen to the ground
    cyl('plate_n', (-X, Y, 2.5), (0.0, Y, 2.85), 0.11, 'char_beam', P, sides=6)
    cyl('plate_w', (-X, -Y, 2.25), (-X, Y, 2.5), 0.11, 'char_beam', P, sides=6)
    cyl('ridge_fallen', (-X + 0.3, -0.2, 2.5), (X - 0.4, 0.4, 0.15), 0.12, 'char_beam', P, sides=6)
    for k in range(6):
        x = -X + 0.6 + k * 1.15
        a = rnd.uniform(-0.6, 0.6)
        L = rnd.uniform(1.4, 2.6)
        p0 = Vector((x, rnd.uniform(-1.6, 1.6), 0.08))
        p1 = p0 + Vector((math.sin(a) * L * 0.4, math.cos(a) * L, rnd.uniform(0.0, 0.9)))
        cyl('rafter_%d' % k, p0, p1, 0.07, 'char', P, sides=5)
    # a corner of roof still hanging from the west plate, charred planks
    boxc('roof_scrap', (-X + 0.7, Y - 0.9, 2.0), (0.8, 1.0, 0.04), 'char', P, rot=(0.5, -0.4, 0.1))
    # stalls along the north: partitions burnt to stumps
    for k, x in enumerate((-2.2, -0.8, 0.6, 2.0)):
        h = (0.9, 0.5, 1.1, 0.35)[k]
        box('stall_%d' % k, (x - 0.05, Y - 1.6, 0.0), (x + 0.05, Y - 0.1, h), 'char', P)
        OBS.append((x - 0.08, Y - 1.6, x + 0.08, Y - 0.1))
    box('manger', (-3.2, Y - 0.55, 0.0), (-1.2, Y - 0.15, 0.6), 'char', P)
    OBS.append((-3.2, Y - 0.55, -1.2, Y - 0.15))
    # burnt and unburnt hay, a trough, a pitchfork, a cart wheel against a post
    for k, (x, y, s, m) in enumerate(((1.5, -0.8, 0.7, 'straw_burnt'), (2.6, 1.3, 0.5, 'straw'), (-0.4, 1.4, 0.5, 'straw_burnt'))):
        blob('hay_%d' % k, P, (x, y, 0.0), (s, s * 0.8, s * 0.55), 30 + k, m, n=18)
    OBS.append((0.8, -1.4, 2.2, -0.2))
    box('trough', (X + 0.3, -1.8, 0.0), (X + 0.7, -0.4, 0.45), 'wood', P)
    OBS.append((X + 0.3, -1.8, X + 0.7, -0.4))
    cyl('fork_haft', (2.9, -1.9, 0.0), (2.4, -1.1, 1.5), 0.02, 'wood', P, sides=4)
    for k in range(3):
        cyl('fork_tine_%d' % k, (2.4 + 0.04 * (k - 1), -1.1, 1.5), (2.38 + 0.04 * (k - 1), -1.0, 1.75), 0.008, 'iron', P, sides=3)
    wheel('wheel', (X + 0.25, 0.45, 0.6), (0.0, 0.25, 1.4), 0.6, P)
    # a horse that did not get out: skull and ribs in the ash
    hx, hy = 0.4, 0.6
    blob('horse_skull', P, (hx, hy, 0.12), (0.32, 0.12, 0.13), 40, 'bone', n=16)
    for k in range(7):
        x = hx + 0.5 + k * 0.14
        for s in (-1, 1):
            cyl('rib_%d_%d' % (k, s), (x, hy, 0.25), (x + 0.04, hy + s * 0.38, 0.03), 0.018, 'bone', P, sides=3)
    cyl('spine', (hx + 0.4, hy, 0.26), (hx + 1.6, hy, 0.2), 0.03, 'bone', P, sides=4)
    # an iron-bound chest that came through the fire
    box('chest', (-3.1, -2.1, 0.0), (-2.4, -1.6, 0.5), 'char', P)
    box('chest_lid', (-3.12, -2.12, 0.5), (-2.38, -1.58, 0.57), 'char', P, rot=(0.0, 0.0, 0.05))
    for k in range(2):
        box('chest_band_%d' % k, (-3.0 + 0.4 * k, -2.14, 0.0), (-2.94 + 0.4 * k, -1.56, 0.59), 'iron', P)
    OBS.append((-3.15, -2.15, -2.35, -1.55))
    SEARCH.append(('Arcón chamuscado', 'el arcón chamuscado', 'cofre', -2.75, -1.85))
    SEARCH.append(('Pesebre', 'el pesebre', 'canasto', -2.2, Y - 0.35))
    K.update(name='EstabloQuemado', footprint=(-X, -Y, X, Y), lights=[])


# ------------------------------------------------------------------------------------------------ carreta

def carreta():
    P = 'main'
    # the cart on its right side: the bed stands up like a wall, its floor facing south (to the camera)
    L, W, D = 3.0, 1.4, 0.5             # bed length, width, side height
    yb = 0.2                            # where the floor of the bed is now
    boxc('bed_floor', (0.0, yb, W / 2 + 0.05), (L / 2, 0.05, W / 2), 'boards_x', P, rot=(0, 0, 0.0))
    boxc('bed_side_up', (0.0, yb + D / 2, W + 0.08), (L / 2, D / 2, 0.04), 'boards_f', P)
    boxc('bed_side_down', (0.0, yb + D / 2, 0.06), (L / 2, D / 2, 0.04), 'boards_f', P)
    boxc('bed_end_a', (-L / 2, yb + D / 2, W / 2 + 0.05), (0.04, D / 2, W / 2), 'boards', P)
    boxc('bed_end_b', (L / 2, yb + D / 2, W / 2 + 0.05), (0.04, D / 2, W / 2), 'boards', P)
    # axle with one wheel still on (sticking up) and one lying in the grass
    cyl('axle', (0.4, yb - 0.25, 0.08), (0.4, yb - 0.25, W + 0.25), 0.05, 'wood', P, sides=6)
    wheel('wheel_up', (0.4, yb - 0.3, W + 0.35), (math.pi / 2, 0.0, 0.0), 0.7, P)
    wheel('wheel_ground', (2.6, -1.4, 0.06), (math.pi / 2, 0.0, 0.3), 0.7, P, broken=2)
    # the shafts, one snapped
    cyl('shaft_a', (L / 2, yb - 0.2, 0.25), (L / 2 + 1.8, yb - 0.5, 0.06), 0.045, 'wood', P, sides=5)
    cyl('shaft_b', (L / 2, yb - 0.2, W - 0.15), (L / 2 + 0.9, yb - 0.3, W - 0.6), 0.045, 'wood', P, sides=5)
    cyl('shaft_b_end', (L / 2 + 1.2, yb - 0.7, 0.05), (L / 2 + 2.0, yb - 0.9, 0.08), 0.045, 'wood', P, sides=5)
    OBS.append((-L / 2 - 0.1, yb - 0.4, L / 2 + 0.1, yb + D + 0.1))
    # arrows stuck in the floor of the cart (an ambush)
    rnd = random.Random(7)
    for k in range(6):
        x = rnd.uniform(-1.2, 1.2)
        z = rnd.uniform(0.3, 1.3)
        p0 = Vector((x, yb - 0.06, z))
        d = Vector((rnd.uniform(-0.15, 0.15), -1.0, rnd.uniform(-0.1, 0.25))).normalized()
        cyl('arrow_%d' % k, p0, p0 + d * 0.55, 0.008, 'wood_light', P, sides=3)
        boxc('fletch_%d' % k, p0 + d * 0.52, (0.004, 0.05, 0.03), 'fletch', P)
    # spilled goods: crates (one broken open), sacks, a barrel rolled away, cloth
    for k, (x, y, r) in enumerate(((-1.2, -0.9, 0.3), (-0.4, -1.3, -0.5))):
        boxc('crate_%d' % k, (x, y, 0.25), (0.25, 0.25, 0.25), 'boards', P, rot=(0, 0, r))
        OBS.append((x - 0.32, y - 0.32, x + 0.32, y + 0.32))
    for k, (x, y, r) in enumerate(((0.5, -1.6, 0.2), (0.8, -1.9, 1.3), (0.2, -2.0, -0.7))):
        boxc('board_%d' % k, (x, y, 0.02), (0.3, 0.06, 0.015), 'boards_f', P, rot=(0, 0, r))
    for k, (x, y) in enumerate(((-2.0, -0.2), (-2.3, -0.7))):
        rock('sack_%d' % k, (x, y, 0.15), (0.3, 0.22, 0.22), 'linen', P, seed=60 + k, squash=1.0)
    OBS.append((-2.6, -1.0, -1.7, 0.1))
    cyl('barrel', (1.9, -2.6, 0.27), (2.5, -2.4, 0.27), 0.26, 'boards', P, sides=10)
    OBS.append((1.6, -2.9, 2.7, -2.1))
    boxc('cloth', (-0.9, -1.9, 0.01), (0.5, 0.35, 0.01), 'cloth_r', P, rot=(0, 0, 0.4))
    for k in range(5):
        rock('apple_%d' % k, (-0.7 + 0.2 * k, -2.2 + 0.07 * k, 0.04), (0.04, 0.04, 0.04), 'clay', P, seed=70 + k, squash=1.0)
    # the merchant, face down under his cloak, his satchel open by his hand
    mx, my = 1.0, -0.85                 # in front of the cart, where the camera sees him
    blob('body', P, (mx, my, 0.0), (0.85, 0.3, 0.22), 80, 'cloak', n=22, under=0.2)
    blob('hood', P, (mx - 0.85, my + 0.05, 0.0), (0.18, 0.17, 0.14), 81, 'cloak', n=14, under=0.2)
    blob('boot_a', P, (mx + 0.88, my - 0.1, 0.0), (0.16, 0.07, 0.08), 82, 'leather', n=12, under=0.2)
    blob('boot_b', P, (mx + 0.85, my + 0.15, 0.0), (0.16, 0.07, 0.08), 83, 'leather', n=12, under=0.2)
    OBS.append((mx - 1.0, my - 0.3, mx + 1.0, my + 0.3))
    boxc('satchel', (mx - 0.5, my - 0.55, 0.08), (0.2, 0.12, 0.08), 'leather', P, rot=(0, 0, 0.3))
    boxc('note', (mx - 0.15, my - 0.75, 0.01), (0.1, 0.07, 0.004), 'linen', P, rot=(0, 0, -0.4))
    SEARCH.append(('Morral del comerciante', 'el morral del comerciante', 'comerciante', mx - 0.5, my - 0.55))
    SEARCH.append(('Cajones', 'los cajones', 'cajon', -0.8, -1.1))
    K.update(name='CarretaVolcada', footprint=(-2.6, -2.6, 2.8, 1.4), lights=[])


# ------------------------------------------------------------------------------------------------ altar

def altar():
    P = 'main'
    patch('mud', 0.0, 0.0, 3.2, 2.8, 'mud', P, 3)
    rnd = random.Random(5)
    # heap of stones under a leaning slab
    for k in range(10):
        a = 2 * math.pi * k / 10
        rock('heap_%d' % k, (0.9 * math.cos(a), 0.6 * math.sin(a), 0.15), (0.32, 0.3, 0.24), 'stone', P, seed=10 + k, squash=0.7)
    boxc('slab', (0.0, 0.0, 0.62), (1.1, 0.65, 0.11), 'stone_dark', P, rot=(0.06, -0.14, 0.1))
    OBS.append((-1.2, -0.8, 1.2, 0.8))
    # the idol: a squat crowned frog, mouth wide
    ix, iy, iz = 0.1, 0.2, 0.78
    blob('idol_body', P, (ix, iy, iz), (0.55, 0.46, 0.48), 20, 'idol', n=26, under=0.3)
    blob('idol_head', P, (ix, iy - 0.1, iz + 0.55), (0.46, 0.36, 0.26), 21, 'idol', n=20)
    for s in (-1, 1):
        blob('idol_eye_%d' % s, P, (ix + s * 0.26, iy - 0.14, iz + 0.78), (0.12, 0.12, 0.1), 22 + s, 'idol', n=12)
        rock('idol_pupil_%d' % s, (ix + s * 0.27, iy - 0.25, iz + 0.79), (0.04, 0.025, 0.04), 'glow_v', 'glow', seed=24 + s)
        blob('idol_leg_%d' % s, P, (ix + s * 0.46, iy - 0.22, iz - 0.05), (0.18, 0.26, 0.13), 26 + s, 'idol', n=12)
    boxc('idol_mouth', (ix, iy - 0.45, iz + 0.47), (0.33, 0.012, 0.03), 'soot', P)
    for k in range(5):
        a = -0.6 + k * 0.3
        cyl('crown_%d' % k, (ix + 0.24 * math.sin(a), iy - 0.1 + 0.15 * math.cos(a), iz + 0.76), (ix + 0.27 * math.sin(a), iy - 0.1 + 0.17 * math.cos(a), iz + 1.05),
            0.045, 'crown', P, sides=4, r1=0.008)
    cyl('crown_band', (ix - 0.27, iy - 0.1, iz + 0.78), (ix + 0.27, iy - 0.1, iz + 0.78), 0.04, 'crown', P, sides=5)
    # offerings on the slab and at its foot: bones, fish, bowls, candles
    for k, (x, y) in enumerate(((-0.7, -0.35), (0.65, -0.4), (-0.5, 0.3))):
        cyl('bowl_%d' % k, (x, y, 0.72), (x, y, 0.8), 0.1, 'clay', P, sides=8, r1=0.13)
    for k, (x, y) in enumerate(((-0.25, -0.45), (0.85, -0.05))):
        blob('fish_%d' % k, P, (x, y, 0.76), (0.15, 0.05, 0.04), 30 + k, 'fish', n=12)
    for k in range(4):
        x = -1.4 + k * 0.9
        cyl('offer_bone_%d' % k, (x, -1.0, 0.03), (x + 0.3, -1.1 + 0.1 * k, 0.04), 0.022, 'bone', P, sides=4)
    blob('skull', P, (1.3, -0.9, 0.08), (0.12, 0.14, 0.1), 35, 'bone', n=14)
    for k, (x, y, h) in enumerate(((-0.9, -0.15, 0.14), (-0.75, 0.05, 0.1), (0.8, 0.2, 0.16), (0.45, -0.55, 0.08), (-1.3, -1.1, 0.12))):
        z = 0.72 if abs(x) < 1.0 and abs(y) < 0.6 else 0.0
        cyl('candle_%d' % k, (x, y, z), (x, y, z + h), 0.03, 'linen', P, sides=6)
        box('candle_flame_%d' % k, (x - 0.012, y - 0.012, z + h), (x + 0.012, y + 0.012, z + h + 0.05), 'flame', 'glow')
    SEARCH.append(('Ofrendas', 'las ofrendas', 'ofrendas', 0.0, -0.75))
    # crooked standing stones in a ring, one fallen; cords with charms between two of them
    stones = []
    for k in range(6):
        a = 2 * math.pi * k / 6 + 0.3
        x, y = 2.5 * math.cos(a), 2.1 * math.sin(a)
        h = rnd.uniform(1.3, 2.1)
        lean = (rnd.uniform(-0.25, 0.25), rnd.uniform(-0.25, 0.25))
        if k == 4:
            blob('standing_%d' % k, P, (x, y, 0.0), (0.9, 0.35, 0.32), 60 + k, 'monolith', n=20, jag=0.15, under=0.2)
            OBS.append((x - 0.8, y - 0.8, x + 0.8, y + 0.8))
            continue
        ob = blob('standing_%d' % k, P, (x, y, 0.0), (0.3, 0.25, h), 60 + k, 'monolith', n=22, jag=0.15, under=0.05)
        ob.rotation_euler = (lean[0], lean[1], a)
        OBS.append((x - 0.3, y - 0.3, x + 0.3, y + 0.3))
        stones.append((x, y, h))
    for i in range(2):
        (x0, y0, h0), (x1, y1, h1) = stones[i], stones[i + 1]
        for j in range(8):
            t0, t1 = j / 8, (j + 1) / 8
            sag0 = math.sin(math.pi * t0) * 0.35
            sag1 = math.sin(math.pi * t1) * 0.35
            cyl('cord_%d_%d' % (i, j), (x0 + (x1 - x0) * t0, y0 + (y1 - y0) * t0, h0 * 0.85 - sag0), (x0 + (x1 - x0) * t1, y0 + (y1 - y0) * t1, h0 * 0.85 - sag1),
                0.008, 'rope', P, sides=3)
        for j in range(3):
            t = 0.25 + j * 0.25
            x, y = x0 + (x1 - x0) * t, y0 + (y1 - y0) * t
            z = h0 * 0.85 - math.sin(math.pi * t) * 0.35
            cyl('charm_str_%d_%d' % (i, j), (x, y, z), (x, y, z - 0.15), 0.004, 'rope', P, sides=3)
            blob('charm_%d_%d' % (i, j), P, (x, y, z - 0.2), (0.04, 0.03, 0.05), 50 + i * 3 + j, 'bone', n=10)
    K.update(name='AltarTorcido', footprint=(-2.8, -2.4, 2.8, 2.4),
             lights=[((0.0, -0.2, 1.0), 3.2, (1.0, 0.65, 0.35), 0.55, 0.35), ((ix, iy - 0.4, iz + 0.8), 2.0, (0.55, 0.42, 0.85), 0.5, 0.0)])


KINDS = {'establo': establo, 'carreta': carreta, 'altar': altar}

PARTS = {
    'main': {'group': 'cabin', 'role': 'static'},
    'glow': {'group': 'cabin', 'role': 'static', 'glowColor': 'd2ac88'},
}


def build_all(kind):
    for ob in list(bpy.data.objects):
        bpy.data.objects.remove(ob, do_unlink=True)
    for m in list(bpy.data.materials):
        bpy.data.materials.remove(m)
    for me in list(bpy.data.meshes):
        bpy.data.meshes.remove(me)
    reset()
    K.clear()
    OBS.clear()
    SEARCH.clear()
    mats()
    KINDS[kind]()
    bpy.context.view_layer.update()
    return len(PIECES)


def ground():
    pmat('ground_ao', 'flat', ['3d332b', '3d332b', '3d332b'])
    bpy.ops.mesh.primitive_plane_add(size=30, location=(0, 0, 0))
    g = bpy.context.active_object
    g.name = 'ground_ao'
    g.data.materials.append(MATS['ground_ao'])
    return [g]


def meta():
    return {
        'name': K['name'],
        'footprint': UB(*K['footprint']),
        'obstacles': {'cabin': [UB(*o) for o in OBS]},
        'lights': [{'group': 'cabin', 'pos': U(*p), 'radius': r, 'color': list(c), 'intensity': i, 'flicker': f}
                   for (p, r, c, i, f) in K['lights']],
        'searchables': [{'name': n, 'what': w, 'table': tb, 'at': U(x, y, 0), 'group': 'cabin'} for (n, w, tb, x, y) in SEARCH],
    }
