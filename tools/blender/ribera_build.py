# Barro y Bruma - water plants: reeds and cattails at the foot of the banks, lily pads on open water.
# A "props" set like the rocks and the trees (edificio_kit.py first, then build_all, unwrap, bake, export).
# Pivot at the water line: MapWorld places them WaterDepth below the ground, on the water drawn by the ground shader.
import math, random
from mathutils import Vector

PROPS = ['Juncos', 'Totoras', 'Nenufares']
SPACING = 4.0
GREENS = ['1f2a1f', '2b3a2a', '3b4a33', '4f5c40', '666043', '7e7752']
for _h in GREENS:
    if _h not in PALETTE:
        PALETTE.append(_h)


def origin(i):
    return Vector(((i - 1) * SPACING, 0.0, 0.0))


def mats():
    pmat('junco', 'noise', ['1f2a1f', '2b3a2a', '3b4a33', '4f5c40', '666043'], scale=7.0)
    pmat('junco_seco', 'noise', ['3d332b', '594a3c', '7a6650', '9c8a70'], scale=7.0)
    pmat('totora', 'noise', ['141210', '2a2420', '3d332b'], scale=10.0)
    pmat('hoja_agua', 'noise', ['1f2a1f', '2b3a2a', '3b4a33', '4f5c40'], scale=5.0)
    pmat('flor', 'noise', ['9a968c', 'c4bfb2', 'e6e2d4'], scale=8.0)
    pmat('flor_c', 'flat', ['b08a6a', 'd2ac88', 'd2ac88'])
    pmat('lodo', 'flat', ['141210', '2a2420', '2a2420'])


def blade(name, part, base, h, lean, r, mat, rot=0.0):
    """A blade of reed: a thin four-sided cone, bent a little."""
    a = Vector(base)
    mid = a + Vector((lean[0] * 0.4, lean[1] * 0.4, h * 0.55))
    tip = a + Vector((lean[0], lean[1], h))
    cyl(name + '_a', a, mid, r, mat, part, sides=4, r1=r * 0.7, roll=rot)
    cyl(name + '_b', mid, tip, r * 0.7, mat, part, sides=4, r1=0.003, roll=rot)


def reeds(o, part, cattails, seed):
    rnd = random.Random(seed)
    n = 22
    for k in range(n):
        ang = rnd.uniform(0, 2 * math.pi)
        rr = rnd.uniform(0.0, 0.42) ** 0.8
        base = o + Vector((math.cos(ang) * rr, math.sin(ang) * rr * 0.8, -0.05))
        h = rnd.uniform(0.8, 1.5)
        out = Vector((math.cos(ang), math.sin(ang), 0)) * rnd.uniform(0.1, 0.35)
        mat = 'junco_seco' if rnd.random() < 0.22 else 'junco'
        blade('%s_%d' % (part, k), part, base, h, (out.x, out.y), 0.022, mat, rot=ang)
        if cattails and k % 4 == 0:
            top = base + Vector((out.x * 0.9, out.y * 0.9, h * 0.92))
            cyl('%s_head_%d' % (part, k), top - Vector((0, 0, 0.18)), top, 0.035, 'totora', part, sides=6)
    # a ring of mud at the foot
    bm = bmesh.new()
    bmesh.ops.create_circle(bm, cap_ends=True, segments=10, radius=0.5)
    _obj('%s_mud' % part, bm, 'lodo', part, loc=o + Vector((0, 0, 0.005)))


def lilies(o):
    P = 'Nenufares'
    rnd = random.Random(30)
    for k in range(6):
        ang = k * 1.05 + rnd.uniform(-0.3, 0.3)
        rr = rnd.uniform(0.15, 0.6)
        c = o + Vector((math.cos(ang) * rr, math.sin(ang) * rr, 0.0))
        rad = rnd.uniform(0.16, 0.28)
        bm = bmesh.new()
        # a disc with a notch cut to the centre
        notch = rnd.uniform(0, 2 * math.pi)
        verts = [bm.verts.new((0, 0, 0.015))]
        segs = 12
        for i in range(segs + 1):
            a = notch + 0.35 + (2 * math.pi - 0.7) * i / segs
            verts.append(bm.verts.new((math.cos(a) * rad, math.sin(a) * rad, 0.012 + 0.006 * math.sin(i))))
        for i in range(1, segs + 1):
            bm.faces.new((verts[0], verts[i], verts[i + 1]))
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        for f in bm.faces:
            if f.normal.z < 0:
                f.normal_flip()
        _obj('%s_pad_%d' % (P, k), bm, 'hoja_agua', P, loc=c)
    # two pale flowers
    for k, (x, y) in enumerate(((0.2, 0.1), (-0.3, -0.25))):
        c = o + Vector((x, y, 0.02))
        for j in range(6):
            a = j * 1.047
            ob = box('%s_petal_%d_%d' % (P, k, j), (-0.025, 0.0, 0.0), (0.025, 0.09, 0.025), 'flor', P, rot=(0.5, 0, a))
            ob.location = c
        box('%s_core_%d' % (P, k), (-0.025, -0.025, 0.02), (0.025, 0.025, 0.05), 'flor_c', P)
        PIECES[-1].location = c


def build_all():
    for ob in list(bpy.data.objects):
        bpy.data.objects.remove(ob, do_unlink=True)
    for m in list(bpy.data.materials):
        bpy.data.materials.remove(m)
    for me in list(bpy.data.meshes):
        bpy.data.meshes.remove(me)
    reset()
    mats()
    reeds(origin(0), 'Juncos', False, 10)
    reeds(origin(1), 'Totoras', True, 20)
    lilies(origin(2))
    bpy.context.view_layer.update()
    return len(PIECES)


def meta():
    props = {}
    for i, part in enumerate(PROPS):
        c = origin(i)
        vs = [ob.matrix_world @ v.co for ob in PIECES if ob['part'] == part for v in ob.data.vertices]
        reach = max(((p.x - c.x) ** 2 + (p.y - c.y) ** 2) ** 0.5 for p in vs)
        props[part] = {'obstacles': [], 'half': [round(reach, 2), round(reach, 2)], 'axis': 0.0,
                       'height': round(max(p.z for p in vs), 2)}
    return {'name': 'Ribera', 'kind': 'props', 'scale': 1.0, 'warm': 0.12, 'cool': 0.32, 'props': props}


PARTS = {name: {'group': 'props', 'role': 'static', 'pivot': tuple(origin(i))} for i, name in enumerate(PROPS)}
