# Barro y Bruma - ground clutter: ferns, mushrooms, pebbles, tall grass, fallen logs, a milestone and a signpost.
# A "props" set like the rocks and the trees (edificio_kit.py first, then build_all, unwrap, bake, export).
# The small ones have no hitbox (the hero walks through them); the logs, the milestone and the signpost do.
import math, random
from mathutils import Vector

PROPS = ['Helecho', 'Hongos', 'Piedritas', 'Pastizal', 'TroncoCaido', 'TroncoPodrido', 'Mojon', 'Cartel']
SPACING = 4.0
GREENS = ['1f2a1f', '2b3a2a', '3b4a33', '4f5c40', '666043', '7e7752', '1d2622', '28342e', '364438']
for _h in GREENS:
    if _h not in PALETTE:
        PALETTE.append(_h)

# hitboxes in Unity x, z around the pivot: [x, z, r]
OBSTACLES = {}


def origin(i):
    return Vector(((i % 4) * SPACING, (i // 4) * SPACING, 0.0))


def mats():
    pmat('helecho', 'noise', ['1f2a1f', '2b3a2a', '3b4a33', '4f5c40', '666043'], scale=6.0)
    pmat('helecho_seco', 'noise', ['3d332b', '594a3c', '7a6650', '8f4a36'], scale=6.0)
    pmat('pasto', 'noise', ['2b3a2a', '3b4a33', '4f5c40', '666043', '7e7752'], scale=8.0)
    pmat('pasto_seco', 'noise', ['594a3c', '7a6650', '9c8a70', '7e7752'], scale=8.0)
    pmat('tallo', 'noise', ['9a968c', 'c4bfb2', 'c4bfb2'], scale=6.0)
    pmat('sombrero', 'noise', ['3d332b', '594a3c', '8f4a36', 'b08a6a'], scale=9.0)
    pmat('sombrero_rojo', 'noise', ['3e1a18', '6b2a24', 'b24a36', 'b24a36'], scale=9.0)
    pmat('sombrero_palido', 'noise', ['9a968c', 'c4bfb2', 'e6e2d4'], scale=9.0)
    pmat('punto', 'flat', ['e6e2d4', 'e6e2d4', 'e6e2d4'])
    pmat('yesca', 'noise', ['3d332b', '594a3c', '7a6650', 'b08a6a'], scale=7.0)
    pmat('guijarro', 'noise', ['3f3e3b', '6e6c66', '9a968c', '7d8584'], scale=5.0)
    pmat('corteza', 'fibers', ['141210', '2a2420', '3d332b', '594a3c'], scale=1.0, stretch=(1.4, 9.0, 9.0),
         moss={'cols': ['1f2a1f', '2b3a2a', '3b4a33'], 'cut': 0.72})
    pmat('corteza_podrida', 'fibers', ['141210', '141210', '2a2420', '3d332b'], scale=1.0, stretch=(1.4, 9.0, 9.0),
         moss={'cols': ['1d2622', '28342e', '364438'], 'cut': 0.64})
    pmat('madera_corte', 'noise', ['594a3c', '7a6650', '9c8a70', 'b08a6a'], scale=5.0)
    pmat('madera_podrida', 'noise', ['2a2420', '3d332b', '594a3c'], scale=5.0)
    pmat('madera', 'noise', ['2a2420', '3d332b', '594a3c'], scale=3.0)
    pmat('tabla', 'noise', ['594a3c', '7a6650', '9c8a70'], scale=4.0)
    pmat('pintura', 'flat', ['141210', '141210', '141210'])
    pmat('piedra', 'stone', ['3f3e3b', '6e6c66', '7d8584', '9a968c'], scale=1.2, mortar=0.02, grime=0.4,
         moss={'cols': ['2a2420', '2b3a2a', '3b4a33'], 'cut': 0.64})
    pmat('trapo', 'noise', ['3e1a18', '6b2a24', '8f4a36'], scale=6.0)
    pmat('soga', 'noise', ['594a3c', '7a6650', '9c8a70'], scale=8.0)
    pmat('tierra', 'flat', ['2a2420', '3d332b', '3d332b'])


def solid(name, verts, faces, mat, part):
    return mesh(name, verts, faces, mat, part)


def slab(name, pts, thick, mat, part):
    """A flat piece given as a closed polygon of world points, given a little thickness downwards."""
    top = [Vector(p) for p in pts]
    bot = [p - Vector((0, 0, thick)) for p in top]
    n = len(pts)
    faces = [list(range(n)), list(range(2 * n - 1, n - 1, -1))]
    for i in range(n):
        j = (i + 1) % n
        faces.append([i, j, n + j, n + i])
    return solid(name, top + bot, faces, mat, part)


def blade(name, part, base, h, lean, r, mat, rot=0.0):
    a = Vector(base)
    mid = a + Vector((lean[0] * 0.35, lean[1] * 0.35, h * 0.55))
    tip = a + Vector((lean[0], lean[1], h))
    cyl(name + '_a', a, mid, r, mat, part, sides=4, r1=r * 0.7, roll=rot)
    cyl(name + '_b', mid, tip, r * 0.7, mat, part, sides=4, r1=0.003, roll=rot)


def mushroom(name, part, base, h, cap, mat, lean=(0.0, 0.0), dots=False, flat=0.45):
    b = Vector(base)
    top = b + Vector((lean[0], lean[1], h))
    cyl(name + '_stem', b, top, cap * 0.22, 'tallo', part, sides=6, r1=cap * 0.18)
    cyl(name + '_cap', top - Vector((0, 0, cap * 0.12)), top + Vector((0, 0, cap * flat)), cap, mat, part,
        sides=8, r1=cap * 0.25)
    if dots:
        rnd = random.Random(len(name))
        for k in range(3):
            a = rnd.uniform(0, 2 * math.pi)
            p = top + Vector((math.cos(a) * cap * 0.55, math.sin(a) * cap * 0.55, cap * 0.22))
            box(name + '_dot%d' % k, p - Vector((0.012, 0.012, 0.01)), p + Vector((0.012, 0.012, 0.02)),
                'punto', part)


# ------------------------------------------------------------------------------------------------ small things

def frond(name, part, o, ang, length, rise, mat):
    """A fern frond: a spine that arches out and droops, with leaflets on both sides, given thickness."""
    d = Vector((math.cos(ang), math.sin(ang), 0))
    s = Vector((-d.y, d.x, 0))
    n = 12
    top = []
    for i in range(n + 1):
        t = i / n
        sp = o + d * (length * t) + Vector((0, 0, rise * math.sin(math.pi * min(1.0, t * 1.15)) + 0.02))
        w = 0.13 * math.sin(math.pi * (0.12 + 0.88 * t)) * (1.0 - 0.45 * t) + 0.012
        w *= 1.0 if i % 2 else 0.45          # notches between the leaflets
        top.append((sp + s * w - Vector((0, 0, w * 0.45)), sp, sp - s * w - Vector((0, 0, w * 0.45))))
    verts = []
    for row in top:
        verts.extend(row)
    for row in top:
        verts.extend(p - Vector((0, 0, 0.012)) for p in row)
    m = len(top) * 3
    faces = []
    for i in range(n):
        a, b = i * 3, (i + 1) * 3
        faces.append([a, a + 1, b + 1, b])
        faces.append([a + 1, a + 2, b + 2, b + 1])
        faces.append([m + b, m + b + 1, m + a + 1, m + a])
        faces.append([m + b + 1, m + b + 2, m + a + 2, m + a + 1])
    for i in range(n):
        a, b = i * 3, (i + 1) * 3
        faces.append([a, b, m + b, m + a])
        faces.append([a + 2, m + a + 2, m + b + 2, b + 2])
    faces.append([0, m, m + 1, 1])
    faces.append([1, m + 1, m + 2, 2])
    e = n * 3
    faces.append([e, e + 1, m + e + 1, m + e])
    faces.append([e + 1, e + 2, m + e + 2, m + e + 1])
    solid(name, verts, faces, mat, part)


def fern(o):
    P = 'Helecho'
    rnd = random.Random(41)
    k = 9
    for i in range(k):
        ang = 2 * math.pi * i / k + rnd.uniform(-0.25, 0.25)
        dry = i in (2, 6)
        frond('%s_f%d' % (P, i), P, o, ang, rnd.uniform(0.4, 0.5) if dry else rnd.uniform(0.6, 0.85),
              0.12 if dry else rnd.uniform(0.3, 0.42), 'helecho_seco' if dry else 'helecho')
    # two young fronds still curled
    for i, ang in enumerate((0.6, 3.6)):
        b = o + Vector((math.cos(ang) * 0.05, math.sin(ang) * 0.05, 0))
        cyl('%s_young%d' % (P, i), b, b + Vector((0, 0, 0.38)), 0.015, 'helecho', P, sides=4)
        c = b + Vector((math.cos(ang) * 0.04, math.sin(ang) * 0.04, 0.42))
        rock('%s_curl%d' % (P, i), c, (0.045, 0.045, 0.05), 'helecho', P, seed=i, squash=1.0)
    bm = bmesh.new()
    bmesh.ops.create_circle(bm, cap_ends=True, segments=8, radius=0.2)
    _obj('%s_soil' % P, bm, 'tierra', P, loc=o + Vector((0, 0, 0.004)))


def mushrooms(o):
    P = 'Hongos'
    spots = [((0.0, 0.0), 0.26, 0.15, 'sombrero_rojo', True), ((0.16, 0.08), 0.17, 0.11, 'sombrero_rojo', True),
             ((-0.14, 0.1), 0.12, 0.08, 'sombrero', False), ((0.05, -0.17), 0.2, 0.12, 'sombrero', False),
             ((-0.2, -0.12), 0.09, 0.06, 'sombrero_palido', False), ((0.24, -0.1), 0.07, 0.05, 'sombrero', False),
             ((-0.06, 0.24), 0.08, 0.05, 'sombrero_palido', False)]
    for k, ((x, y), h, cap, mat, dots) in enumerate(spots):
        lean = (x * 0.25, y * 0.25)
        mushroom('%s_%d' % (P, k), P, o + Vector((x, y, 0)), h, cap, mat, lean, dots)
    # one fallen over
    b = o + Vector((0.3, 0.22, 0.03))
    cyl('%s_fallen_stem' % P, b, b + Vector((-0.12, 0.05, 0.0)), 0.02, 'tallo', P, sides=6)
    cyl('%s_fallen_cap' % P, b + Vector((-0.12, 0.05, 0.0)), b + Vector((-0.17, 0.07, 0.02)), 0.07, 'sombrero', P,
        sides=8, r1=0.02)
    bm = bmesh.new()
    bmesh.ops.create_circle(bm, cap_ends=True, segments=9, radius=0.34)
    _obj('%s_soil' % P, bm, 'tierra', P, loc=o + Vector((0, 0, 0.004)))


def pebbles(o):
    P = 'Piedritas'
    rnd = random.Random(52)
    for k in range(9):
        a = rnd.uniform(0, 2 * math.pi)
        rr = rnd.uniform(0.0, 0.55)
        s = rnd.uniform(0.05, 0.13) * (1.4 if k == 0 else 1.0)
        c = o + Vector((math.cos(a) * rr, math.sin(a) * rr, s * 0.25))
        rock('%s_%d' % (P, k), c, (s * rnd.uniform(0.9, 1.4), s, s), 'guijarro', P, seed=k + 5, squash=0.6)


def tall_grass(o):
    P = 'Pastizal'
    rnd = random.Random(63)
    for k in range(30):
        ang = rnd.uniform(0, 2 * math.pi)
        rr = rnd.uniform(0.0, 0.32) ** 0.8
        base = o + Vector((math.cos(ang) * rr, math.sin(ang) * rr * 0.85, -0.02))
        h = rnd.uniform(0.3, 0.62)
        out = Vector((math.cos(ang), math.sin(ang), 0)) * rnd.uniform(0.08, 0.26)
        mat = 'pasto_seco' if rnd.random() < 0.3 else 'pasto'
        blade('%s_%d' % (P, k), P, base, h, (out.x, out.y), 0.018, mat, rot=ang)
        if k % 7 == 0:
            tip = base + Vector((out.x, out.y, h))
            cyl('%s_seed%d' % (P, k), tip - Vector((out.x * 0.1, out.y * 0.1, 0.1)), tip + Vector((0, 0, 0.02)),
                0.022, 'pasto_seco', P, sides=5, r1=0.006)


# ------------------------------------------------------------------------------------------------ logs

def log(o, P, rotten):
    rnd = random.Random(70 + rotten)
    L = 2.5 if not rotten else 2.2
    r = 0.26 if not rotten else 0.3
    sink = 0.82 if not rotten else 0.45
    bark = 'corteza_podrida' if rotten else 'corteza'
    a = o + Vector((-L / 2, 0.0, r * sink))
    b = o + Vector((L / 2, 0.15, r * sink * 0.9))
    cyl(P + '_trunk', a, b, r, bark, P, sides=9, r1=r * 0.86)
    d = (b - a).normalized()
    # cut face at the thick end; a broken, splintered one at the other
    cut = 'madera_podrida' if rotten else 'madera_corte'
    cyl(P + '_cut', a - d * 0.012, a + d * 0.004, r * 0.9, cut, P, sides=9)
    if rotten:
        for k in range(6):
            ang = 2 * math.pi * k / 6 + rnd.uniform(-0.3, 0.3)
            base = b + Vector((0, math.cos(ang) * r * 0.62, math.sin(ang) * r * 0.62))
            cyl('%s_splinter%d' % (P, k), base - d * 0.05, base + d * rnd.uniform(0.12, 0.3), r * 0.22,
                'madera_podrida', P, sides=4, r1=0.01)
        # hollow end: a dark disc set a little inside
        cyl(P + '_hollow', b - d * 0.02, b + d * 0.002, r * 0.62, 'pintura', P, sides=8)
    else:
        cyl(P + '_cut2', b - d * 0.004, b + d * 0.012, r * 0.86 * 0.9, cut, P, sides=9)
    # branch stubs
    for k, (t, ang, ln) in enumerate(((0.3, 1.2, 0.35), (0.62, -0.9, 0.5), (0.8, 2.2, 0.25))):
        p = a + (b - a) * t
        dirv = Vector((d.y * 0.3, math.cos(ang), math.sin(ang))).normalized()
        cyl('%s_stub%d' % (P, k), p + dirv * r * 0.6, p + dirv * (r + ln), r * 0.28, bark, P, sides=6, r1=r * 0.14)
    # shelf fungi on the sides
    for k in range(5 if rotten else 3):
        t = rnd.uniform(0.15, 0.85)
        side = -1 if k % 2 else 1
        p = a + (b - a) * t + Vector((0, side * r * 0.95, rnd.uniform(-0.05, 0.12)))
        rr = rnd.uniform(0.07, 0.12)
        cyl('%s_shelf%d' % (P, k), p - Vector((0, 0, 0.018)), p + Vector((0, 0, 0.018)), rr,
            'sombrero_palido' if rotten else 'yesca', P, sides=7)
    # small mushrooms on top of the moss
    for k in range(4 if not rotten else 6):
        t = rnd.uniform(0.1, 0.9)
        p = a + (b - a) * t + Vector((0, rnd.uniform(-0.08, 0.08), r * 0.92))
        mushroom('%s_m%d' % (P, k), P, p, rnd.uniform(0.06, 0.12), rnd.uniform(0.035, 0.06),
                 'sombrero_palido' if rotten else ('sombrero_rojo' if k == 0 else 'sombrero'),
                 dots=(k == 0 and not rotten))
    # ferns and grass leaning on it
    if not rotten:
        for k, (t, side) in enumerate(((0.2, -1), (0.7, 1))):
            p = a + (b - a) * t + Vector((0, side * (r + 0.1), 0))
            for j in range(4):
                ang = (math.pi / 2 if side > 0 else -math.pi / 2) + (j - 1.5) * 0.45
                frond('%s_fern%d_%d' % (P, k, j), P, p, ang, 0.42, 0.22, 'helecho')
    else:
        for k in range(10):
            t = rnd.uniform(0.0, 1.0)
            side = -1 if k % 2 else 1
            base = a + (b - a) * t + Vector((0, side * (r + 0.05), 0))
            blade('%s_reed%d' % (P, k), P, base, rnd.uniform(0.4, 0.8), (0.0, side * 0.12), 0.018, 'pasto')
    # hitbox: circles along the trunk, a little inside it
    n = 4
    circ = []
    for k in range(n):
        t = (k + 0.5) / n
        p = a + (b - a) * t - o
        circ.append([round(p.x, 3), round(p.y, 3), round(r * 0.95, 3)])
    OBSTACLES[P] = circ


# ------------------------------------------------------------------------------------------------ road marks

def milestone(o):
    P = 'Mojon'
    # a squat waystone: a slightly tapered block with a low pointed cap
    b0, b1, h = (0.2, 0.14), (0.16, 0.11), 0.55
    vs = [o + Vector((x * b0[0], y * b0[1], 0.0)) for x, y in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    vs += [o + Vector((x * b1[0], y * b1[1], h)) for x, y in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    vs.append(o + Vector((0, 0, h + 0.12)))
    solid(P + '_body', vs, [[0, 3, 2, 1], [0, 1, 5, 4], [1, 2, 6, 5], [2, 3, 7, 6], [3, 0, 4, 7],
                            [4, 5, 8], [5, 6, 8], [6, 7, 8], [7, 4, 8]], 'piedra', P)
    # carved marks on the faces the camera sees (south and west): a line and three strokes, a notch
    f = o + Vector((0, -0.128, 0))
    box(P + '_mk1', f + Vector((-0.11, -0.006, 0.42)), f + Vector((0.11, 0.0, 0.45)), 'pintura', P)
    for k in range(3):
        x = -0.06 + k * 0.06
        box('%s_mk%d' % (P, k + 2), f + Vector((x - 0.012, -0.006, 0.2)), f + Vector((x + 0.012, 0.0, 0.37)),
            'pintura', P)
    w = o + Vector((-0.185, 0, 0))
    box(P + '_st', w + Vector((-0.006, -0.06, 0.3)), w + Vector((0.0, 0.06, 0.33)), 'pintura', P)
    # stones at the foot and a tuft of grass
    for k, (x, y, s) in enumerate(((-0.25, -0.12, 0.08), (0.24, -0.05, 0.06), (0.1, 0.2, 0.07), (-0.18, 0.17, 0.05))):
        rock('%s_foot%d' % (P, k), o + Vector((x, y, s * 0.3)), (s * 1.3, s, s), 'guijarro', P, seed=k, squash=0.6)
    rnd = random.Random(80)
    for k in range(7):
        base = o + Vector((0.22 + rnd.uniform(-0.05, 0.05), -0.16 + rnd.uniform(-0.05, 0.05), 0))
        blade('%s_g%d' % (P, k), P, base, rnd.uniform(0.18, 0.32), (rnd.uniform(-0.08, 0.1), rnd.uniform(-0.1, 0.05)),
              0.014, 'pasto')
    OBSTACLES[P] = [[0.0, 0.0, 0.24]]


def arrow(name, part, o, z, ang, length, mat):
    """A pointed board nailed to the post, pointing along ang."""
    d = Vector((math.cos(ang), math.sin(ang), 0))
    s = Vector((-d.y, d.x, 0))
    h, t = 0.17, 0.035
    u0 = 0.04
    prof = [(u0, -h / 2), (length - 0.12, -h / 2), (length, 0.0), (length - 0.12, h / 2), (u0, h / 2)]
    verts = []
    for side in (-1, 1):
        for u, v in prof:
            verts.append(o + d * u + s * (side * t / 2) + Vector((0, 0, z + v)))
    n = len(prof)
    faces = [list(range(n)), list(range(2 * n - 1, n - 1, -1))]
    for i in range(n):
        j = (i + 1) % n
        faces.append([i, j, n + j, n + i])
    solid(name, verts, faces, mat, part)


def signpost(o):
    P = 'Cartel'
    cyl(P + '_post', o + Vector((0, 0, -0.05)), o + Vector((0, 0, 1.75)), 0.065, 'madera', P, sides=6)
    cyl(P + '_cap', o + Vector((0, 0, 1.75)), o + Vector((0, 0, 1.82)), 0.09, 'madera', P, sides=6, r1=0.03)
    arrow(P + '_a1', P, o, 1.52, math.radians(-30), 0.78, 'tabla')
    arrow(P + '_a2', P, o, 1.28, math.radians(200), 0.66, 'tabla')
    arrow(P + '_a3', P, o, 1.06, math.radians(110), 0.55, 'tabla')
    # painted strokes on the boards, a rag tied to the post, stones holding the foot
    for k, (z, ang, ln) in enumerate(((1.52, -30, 0.78), (1.28, 200, 0.66), (1.06, 110, 0.55))):
        d = Vector((math.cos(math.radians(ang)), math.sin(math.radians(ang)), 0))
        s = Vector((-d.y, d.x, 0))
        for side in (-1, 1):
            c = o + d * (ln * 0.45) + s * (side * 0.019) + Vector((0, 0, z))
            cyl('%s_paint%d_%d' % (P, k, side), c - d * ln * 0.22, c + d * ln * 0.22, 0.012, 'pintura', P, sides=4)
    cyl(P + '_knot', o + Vector((0, 0, 0.88)), o + Vector((0, 0, 0.95)), 0.08, 'soga', P, sides=6)
    slab(P + '_rag', [o + Vector((0.07, -0.02, 0.92)), o + Vector((0.14, -0.06, 0.92)), o + Vector((0.2, -0.1, 0.62)),
                      o + Vector((0.12, -0.04, 0.68))], 0.012, 'trapo', P)
    for k in range(5):
        a = k * 1.26
        rock('%s_foot%d' % (P, k), o + Vector((math.cos(a) * 0.14, math.sin(a) * 0.14, 0.04)), (0.08, 0.07, 0.07),
             'guijarro', P, seed=k + 20, squash=0.7)
    OBSTACLES[P] = [[0.0, 0.0, 0.14]]


def build_all():
    for ob in list(bpy.data.objects):
        bpy.data.objects.remove(ob, do_unlink=True)
    for m in list(bpy.data.materials):
        bpy.data.materials.remove(m)
    for me in list(bpy.data.meshes):
        bpy.data.meshes.remove(me)
    reset()
    OBSTACLES.clear()
    mats()
    fern(origin(0))
    mushrooms(origin(1))
    pebbles(origin(2))
    tall_grass(origin(3))
    log(origin(4), 'TroncoCaido', False)
    log(origin(5), 'TroncoPodrido', True)
    milestone(origin(6))
    signpost(origin(7))
    bpy.context.view_layer.update()
    return len(PIECES)


def meta():
    props = {}
    for i, part in enumerate(PROPS):
        c = origin(i)
        vs = [ob.matrix_world @ v.co for ob in PIECES if ob['part'] == part for v in ob.data.vertices]
        xs = [p.x - c.x for p in vs]
        ys = [p.y - c.y for p in vs]
        props[part] = {'obstacles': OBSTACLES.get(part, []),
                       'half': [round(max(abs(min(xs)), max(xs)), 2), round(max(abs(min(ys)), max(ys)), 2)],
                       'axis': 0.0, 'height': round(max(p.z for p in vs), 2)}
    return {'name': 'Suelo', 'kind': 'props', 'scale': 1.0, 'warm': 0.12, 'cool': 0.3, 'props': props}


PARTS = {name: {'group': 'props', 'role': 'static', 'pivot': tuple(origin(i))} for i, name in enumerate(PROPS)}
