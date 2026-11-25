# Barro y Bruma - leafy trees (five) and bushes (three) for the plains, the forest, the fields and the village.
# The old bare trees (prefab Tree) stay for the swamp and the dead forest.
# Built with edificio_kit.py (exec it first in the same namespace), baked into one atlas and exported as a
# "props" set, like the rocks: every prop is one part with its pivot at the foot of its trunk. The Unity importer
# (BuildingImporter, kind "props") makes one prefab per prop in Assets/Resources/Props/.
#
# Trunks are tapered cylinders with a few limbs; leaves are clumps (convex hulls of points on an ellipsoid) with a
# texture of leaf clusters, lighter on top and darker underneath. Hitbox: only the trunk; bushes have none (the
# hero walks through them). The canopy is written to the JSON so the tree turns see-through over the hero.
#
# Sizes in game metres at scale 1 (the hero is about 2.7 m tall in the world):
#   Roble          old oak, broad round crown            ~8 m tall, crown ~7 m across
#   Pino           dark fir in tiers                     ~9 m tall, 4 m across
#   Abedul         pale slim birch, yellowish leaves     ~7 m tall
#   ArbolOtono     rust-red leaves, a few bare twigs     ~6 m tall
#   Arbolito       young tree                            ~3.8 m tall
#   Arbusto        round bush                            ~1.4 m across, 1.1 m tall
#   ArbustoBayas   bush with red berries                 ~1.6 m across, 1.2 m tall
#   Matorral       dry bramble, low and wide             ~1.9 m across, 0.8 m tall
import math, random
from mathutils import Vector, Matrix

# Leaves use the greens of the ground shader (PPMapGround: forest, plain, swamp) so they sit with the grass;
# they are added to the palette the bake snaps to.
LEAF_GREENS = ['1f2a1f', '2b3a2a', '3b4a33', '4f5c40', '1d2622', '28342e', '364438', '4a5a4a', '666043', '7e7752']
for _h in LEAF_GREENS:
    if _h not in PALETTE:
        PALETTE.append(_h)

PROPS = ['Roble', 'Pino', 'Abedul', 'ArbolOtono', 'Arbolito', 'Arbusto', 'ArbustoBayas', 'Matorral']
SPACING = 9.0
TRUNK_R = {'Roble': 0.42, 'Pino': 0.3, 'Abedul': 0.2, 'ArbolOtono': 0.27, 'Arbolito': 0.15}


def origin(i):
    return Vector(((i - 3.5) * SPACING, 0.0, 0.0))


def leaf_mat(name, cols, cell=6.5):
    """Leaf clusters: voronoi cells, dark gaps between them, lighter where the clump faces up."""
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new('ShaderNodeOutputMaterial')
    bsdf = nt.nodes.new('ShaderNodeBsdfPrincipled')
    bsdf.inputs['Roughness'].default_value = 1.0
    nt.links.new(bsdf.outputs[0], out.inputs['Surface'])
    v = _vec(nt, 'world', (1, 1, 1))
    # warp the cells with noise so they read as tufts of leaves, not paving
    nz = nt.nodes.new('ShaderNodeTexNoise')
    nz.inputs['Scale'].default_value = 3.0
    nt.links.new(v, nz.inputs['Vector'])
    sc = nt.nodes.new('ShaderNodeVectorMath')
    sc.operation = 'SCALE'
    sc.inputs['Scale'].default_value = 0.18
    nt.links.new(nz.outputs['Color'], sc.inputs[0])
    wv = nt.nodes.new('ShaderNodeVectorMath')
    wv.operation = 'ADD'
    nt.links.new(v, wv.inputs[0])
    nt.links.new(sc.outputs[0], wv.inputs[1])
    vc = nt.nodes.new('ShaderNodeTexVoronoi')
    vc.inputs['Scale'].default_value = cell
    nt.links.new(wv.outputs[0], vc.inputs['Vector'])
    ve = nt.nodes.new('ShaderNodeTexVoronoi')
    ve.feature = 'DISTANCE_TO_EDGE'
    ve.inputs['Scale'].default_value = cell
    nt.links.new(wv.outputs[0], ve.inputs['Vector'])
    cbw = nt.nodes.new('ShaderNodeRGBToBW')
    nt.links.new(vc.outputs['Color'], cbw.inputs[0])
    geo = nt.nodes.new('ShaderNodeNewGeometry')
    sep = nt.nodes.new('ShaderNodeSeparateXYZ')
    nt.links.new(geo.outputs['Normal'], sep.inputs[0])
    fac = _math(nt, 'ADD', _math(nt, 'MULTIPLY', cbw.outputs[0], 0.25), 0.22)
    fac = _math(nt, 'ADD', fac, _math(nt, 'MULTIPLY', sep.outputs['Z'], 0.3))            # sun on the top
    fac = _math(nt, 'ADD', fac, _math(nt, 'MULTIPLY', _noise(nt, v, 2.2, 3.0), 0.3))       # big light and dark patches
    fac = _math(nt, 'ADD', fac, _math(nt, 'MULTIPLY', _noise(nt, v, 12.0, 2.0), 0.12))
    gap = _math(nt, 'MULTIPLY', _math(nt, 'LESS_THAN', ve.outputs['Distance'], 0.05),
                _math(nt, 'GREATER_THAN', _noise(nt, v, 4.0, 2.0), 0.47))                  # holes only here and there
    fac = _math(nt, 'SUBTRACT', fac, _math(nt, 'MULTIPLY', gap, 0.3))
    fac = _math(nt, 'SUBTRACT', fac, 0.1)
    fac = _math(nt, 'MAXIMUM', _math(nt, 'MINIMUM', fac, 0.999), 0.0)
    ramp = _ramp(nt, fac, cols)
    nt.links.new(ramp.outputs['Color'], bsdf.inputs['Base Color'])
    MATS[name] = m
    return m


def mats():
    pmat('corteza', 'fibers', ['141210', '2a2420', '3d332b', '594a3c'], scale=1.0, stretch=(9.0, 9.0, 1.4))
    pmat('corteza_pino', 'fibers', ['141210', '2a2420', '3e1a18', '594a3c'], scale=1.0, stretch=(7.0, 7.0, 1.2))
    pmat('corteza_abedul', 'fibers', ['141210', '9a968c', 'c4bfb2', 'e6e2d4', 'e6e2d4'], scale=1.0, stretch=(2.5, 2.5, 20.0))
    pmat('ramitas', 'noise', ['141210', '2a2420', '3d332b'], scale=3.0)
    pmat('ramitas_secas', 'noise', ['3d332b', '594a3c', '7a6650'], scale=3.0)
    leaf_mat('hojas_roble', ['141210', '1f2a1f', '2b3a2a', '3b4a33', '3b4a33', '4f5c40'])
    leaf_mat('hojas_pino', ['141210', '1d2622', '1d2622', '28342e', '364438', '4a5a4a'], cell=8.0)
    leaf_mat('hojas_abedul', ['1f2a1f', '2b3a2a', '4e4b34', '666043', '666043', '7e7752'], cell=7.5)
    leaf_mat('hojas_otono', ['141210', '2a2420', '3e1a18', '6b2a24', '6b2a24', '8f4a36'])
    leaf_mat('hojas_joven', ['141210', '2b3a2a', '3b4a33', '4f5c40', '4f5c40', '666043'], cell=7.5)
    leaf_mat('hojas_secas', ['141210', '2a2420', '3d332b', '594a3c', '594a3c', '7a6650'], cell=8.0)
    pmat('bayas', 'flat', ['6b2a24', 'b24a36', 'b24a36'])
    pmat('suelo', 'flat', ['3d332b', '3d332b', '3d332b'])


# ------------------------------------------------------------------------------------------------ geometry

def blob(name, part, c, size, seed, mat, n=44, jag=0.22, under=0.6):
    """A clump of leaves: convex hull of jittered points, flatter underneath."""
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
    ob = _obj(name, bm, mat, part, loc=c)
    ob['canopy'] = True
    return ob


def limb(name, part, pts, r0, r1, mat, sides=7):
    """A bent branch or trunk through a list of points, tapering from r0 to r1."""
    n = len(pts) - 1
    obs = []
    for k in range(n):
        a = r0 + (r1 - r0) * k / n
        b = r0 + (r1 - r0) * (k + 1) / n
        p0 = Vector(pts[k])
        p1 = Vector(pts[k + 1])
        # overlap the joints a little so no gap shows at the bends
        d = (p1 - p0).normalized()
        obs.append(cyl('%s_%d' % (name, k), p0 - d * (a * 0.3 if k else 0.0), p1 + d * b * 0.3, a, mat, part,
                       sides=sides, r1=b, roll=0.3 * k))
    return obs


def roots(name, part, o, r, mat, count, seed, reach=0.9):
    rnd = random.Random(seed)
    for k in range(count):
        a = 2 * math.pi * (k + rnd.uniform(-0.2, 0.2)) / count
        d = Vector((math.cos(a), math.sin(a), 0))
        cyl('%s_root%d' % (name, k), o + d * r * 0.4 + Vector((0, 0, r * 1.2)), o + d * (r + reach * rnd.uniform(0.7, 1.1)) + Vector((0, 0, -0.08)),
            r * 0.55, mat, part, sides=5, r1=r * 0.12)


def roble(o):
    P, R = 'Roble', TRUNK_R['Roble']
    limb('roble_trunk', P, [o, o + Vector((0.1, 0.05, 1.4)), o + Vector((-0.05, 0.1, 2.7))], R, R * 0.72, 'corteza', sides=9)
    roots('roble', P, o, R, 'corteza', 5, seed=1)
    fork = o + Vector((-0.05, 0.1, 2.6))
    tips = []
    for k, (dx, dy, dz) in enumerate(((1.7, 0.4, 2.2), (-1.5, 0.9, 2.4), (0.2, -1.6, 2.0), (-0.3, 0.2, 3.2))):
        mid = fork + Vector((dx * 0.5, dy * 0.5, dz * 0.45))
        tip = fork + Vector((dx, dy, dz))
        limb('roble_limb%d' % k, P, [fork, mid, tip], R * 0.55, R * 0.22, 'corteza')
        tips.append(tip)
    rnd = random.Random(5)
    clumps = [(tips[0], 1.6), (tips[1], 1.6), (tips[2], 1.5), (tips[3] + Vector((0, 0, 0.4)), 1.7),
              (fork + Vector((0.9, 0.9, 3.0)), 1.4), (fork + Vector((-1.0, -0.8, 2.9)), 1.4),
              (fork + Vector((1.3, -1.0, 2.6)), 1.2), (fork + Vector((-1.8, 0.0, 2.2)), 1.15),
              (fork + Vector((0.4, 1.5, 2.3)), 1.2)]
    for k, (c, s) in enumerate(clumps):
        blob('roble_hojas%d' % k, P, c, (s * rnd.uniform(0.95, 1.1), s * rnd.uniform(0.9, 1.05), s * 0.8), 100 + k, 'hojas_roble')


def pino(o):
    P, R = 'Pino', TRUNK_R['Pino']
    limb('pino_trunk', P, [o, o + Vector((0, 0, 3.0)), o + Vector((0.05, 0, 6.5))], R, R * 0.35, 'corteza_pino', sides=8)
    roots('pino', P, o, R, 'corteza_pino', 4, seed=2, reach=0.6)
    tiers = [(1.6, 2.0, 1.9), (2.9, 1.75, 1.8), (4.1, 1.45, 1.7), (5.3, 1.1, 1.55), (6.4, 0.75, 1.45), (7.4, 0.42, 1.4)]
    for k, (z, rad, h) in enumerate(tiers):
        rnd = random.Random(200 + k)
        bm = bmesh.new()
        vs = []
        # a drooping skirt of points, a few inside, and the tip
        for i in range(16):
            a = 2 * math.pi * (i + rnd.uniform(-0.3, 0.3)) / 16
            rr = rad * rnd.uniform(0.85, 1.1)
            vs.append(bm.verts.new((rr * math.cos(a), rr * math.sin(a), -0.25 * rnd.uniform(0.6, 1.2))))
        for i in range(8):
            a = 2 * math.pi * (i + 0.5) / 8
            rr = rad * 0.55
            vs.append(bm.verts.new((rr * math.cos(a), rr * math.sin(a), h * 0.38 + rnd.uniform(-0.1, 0.1))))
        vs.append(bm.verts.new((rnd.uniform(-0.08, 0.08), rnd.uniform(-0.08, 0.08), h)))
        vs.append(bm.verts.new((0, 0, -0.32)))
        res = bmesh.ops.convex_hull(bm, input=vs)
        junk = list(set(res['geom_interior']) | set(res['geom_unused']))
        bmesh.ops.delete(bm, geom=[g for g in junk if isinstance(g, bmesh.types.BMVert)], context='VERTS')
        bmesh.ops.triangulate(bm, faces=bm.faces[:])
        ob = _obj('pino_tier%d' % k, bm, 'hojas_pino', P, loc=o + Vector((0, 0, z)), rot=(0, 0, k * 0.7))
        ob['canopy'] = True


def abedul(o):
    P, R = 'Abedul', TRUNK_R['Abedul']
    top = o + Vector((0.35, -0.1, 5.2))
    limb('abedul_trunk', P, [o, o + Vector((0.1, 0, 1.8)), o + Vector((0.3, -0.05, 3.6)), top], R, R * 0.45, 'corteza_abedul', sides=7)
    limb('abedul_branch0', P, [o + Vector((0.2, 0, 3.0)), o + Vector((-0.6, 0.3, 4.0)), o + Vector((-1.0, 0.4, 4.7))], R * 0.4, R * 0.15, 'corteza_abedul', sides=5)
    limb('abedul_branch1', P, [o + Vector((0.3, 0, 3.8)), o + Vector((1.0, -0.4, 4.6)), o + Vector((1.3, -0.6, 5.1))], R * 0.35, R * 0.15, 'corteza_abedul', sides=5)
    rnd = random.Random(7)
    clumps = [(top + Vector((0, 0, 0.6)), (1.0, 0.9, 1.3)), (o + Vector((-1.0, 0.4, 5.0)), (0.9, 0.8, 1.0)),
              (o + Vector((1.3, -0.6, 5.4)), (0.85, 0.8, 1.05)), (o + Vector((0.2, 0.5, 4.5)), (0.8, 0.75, 0.9)),
              (o + Vector((0.6, -0.3, 6.3)), (0.7, 0.65, 0.85)), (o + Vector((-0.4, -0.4, 4.2)), (0.7, 0.65, 0.8))]
    for k, (c, s) in enumerate(clumps):
        blob('abedul_hojas%d' % k, P, c, s, 300 + k, 'hojas_abedul', n=28)


def otono(o):
    P, R = 'ArbolOtono', TRUNK_R['ArbolOtono']
    fork = o + Vector((0.15, 0.0, 2.1))
    limb('otono_trunk', P, [o, o + Vector((-0.1, 0.05, 1.1)), fork], R, R * 0.75, 'corteza', sides=8)
    roots('otono', P, o, R, 'corteza', 4, seed=3, reach=0.7)
    for k, (dx, dy, dz) in enumerate(((1.3, 0.3, 1.9), (-1.2, 0.6, 2.0), (0.1, -1.0, 2.3))):
        limb('otono_limb%d' % k, P, [fork, fork + Vector((dx * 0.5, dy * 0.5, dz * 0.5)), fork + Vector((dx, dy, dz))], R * 0.5, R * 0.2, 'corteza')
    # a couple of bare twigs sticking out of the crown
    limb('otono_twig0', P, [fork + Vector((0.2, 0, 2.6)), fork + Vector((0.6, 0.3, 3.9)), fork + Vector((0.5, 0.6, 4.4))], 0.07, 0.03, 'ramitas', sides=4)
    limb('otono_twig1', P, [fork + Vector((-0.6, 0.3, 2.0)), fork + Vector((-1.7, 0.5, 2.7)), fork + Vector((-2.1, 0.3, 3.1))], 0.06, 0.025, 'ramitas', sides=4)
    rnd = random.Random(9)
    clumps = [(fork + Vector((1.3, 0.3, 2.1)), 1.15), (fork + Vector((-1.2, 0.6, 2.2)), 1.2), (fork + Vector((0.1, -1.0, 2.5)), 1.1),
              (fork + Vector((0.0, 0.2, 3.1)), 1.25), (fork + Vector((0.8, 0.9, 2.6)), 0.95)]
    for k, (c, s) in enumerate(clumps):
        blob('otono_hojas%d' % k, P, c, (s, s * rnd.uniform(0.85, 1.0), s * 0.8), 400 + k, 'hojas_otono', n=30)


def arbolito(o):
    P, R = 'Arbolito', TRUNK_R['Arbolito']
    limb('arbolito_trunk', P, [o, o + Vector((0.05, 0, 0.9)), o + Vector((-0.05, 0.05, 1.8))], R, R * 0.6, 'corteza', sides=6)
    clumps = [(o + Vector((0, 0.05, 2.5)), (0.95, 0.9, 0.85)), (o + Vector((0.45, -0.3, 2.15)), (0.6, 0.55, 0.5)),
              (o + Vector((-0.4, 0.3, 2.9)), (0.55, 0.5, 0.5))]
    for k, (c, s) in enumerate(clumps):
        blob('arbolito_hojas%d' % k, P, c, s, 500 + k, 'hojas_joven', n=26)


def arbusto(o):
    P = 'Arbusto'
    for k, (c, s) in enumerate(((Vector((0, 0, 0.5)), (0.65, 0.6, 0.55)), (Vector((0.45, -0.25, 0.35)), (0.45, 0.42, 0.38)),
                                (Vector((-0.4, 0.2, 0.38)), (0.42, 0.4, 0.36)), (Vector((0.1, 0.4, 0.3)), (0.38, 0.35, 0.3)))):
        blob('arbusto_%d' % k, P, o + c, s, 600 + k, 'hojas_roble', n=24, under=0.35)


def bayas(o):
    P = 'ArbustoBayas'
    rnd = random.Random(70)
    hulls = ((Vector((0, 0, 0.55)), (0.75, 0.65, 0.6)), (Vector((0.5, 0.3, 0.38)), (0.5, 0.45, 0.4)),
             (Vector((-0.5, -0.2, 0.36)), (0.48, 0.44, 0.38)))
    for k, (c, s) in enumerate(hulls):
        blob('bayas_%d' % k, P, o + c, s, 700 + k, 'hojas_joven', n=24, under=0.35)
    for k in range(16):
        hc, hs = hulls[k % 3]
        a = rnd.uniform(0, 2 * math.pi)
        b = rnd.uniform(-0.1, 1.0)
        d = Vector((math.cos(a) * math.cos(b) * hs[0], math.sin(a) * math.cos(b) * hs[1], math.sin(b) * hs[2] * 0.9))
        p = o + hc + d * 0.97
        bm = bmesh.new()
        bmesh.ops.create_icosphere(bm, subdivisions=1, radius=0.065)
        _obj('baya_%d' % k, bm, 'bayas', P, loc=p)


def matorral(o):
    P = 'Matorral'
    for k, (c, s) in enumerate(((Vector((0, 0, 0.32)), (0.85, 0.6, 0.38)), (Vector((0.6, 0.35, 0.25)), (0.5, 0.45, 0.3)),
                                (Vector((-0.65, -0.2, 0.22)), (0.45, 0.42, 0.26)))):
        blob('matorral_%d' % k, P, o + c, s, 800 + k, 'hojas_secas', n=22, jag=0.25, under=0.3)
    rnd = random.Random(80)
    for k in range(9):
        a = rnd.uniform(0, 2 * math.pi)
        p0 = o + Vector((math.cos(a) * 0.3, math.sin(a) * 0.2, 0.25))
        p1 = o + Vector((math.cos(a) * rnd.uniform(0.9, 1.2), math.sin(a) * rnd.uniform(0.7, 0.95), rnd.uniform(0.45, 0.85)))
        cyl('matorral_twig%d' % k, p0, p1, 0.03, 'ramitas_secas', P, sides=4, r1=0.012)


def build_all():
    for ob in list(bpy.data.objects):
        bpy.data.objects.remove(ob, do_unlink=True)
    for m in list(bpy.data.materials):
        bpy.data.materials.remove(m)
    for me in list(bpy.data.meshes):
        bpy.data.meshes.remove(me)
    reset()
    mats()
    for i, f in enumerate((roble, pino, abedul, otono, arbolito, arbusto, bayas, matorral)):
        f(origin(i))
    bpy.context.view_layer.update()
    return len(PIECES)


def ground():
    """A plane under the props, only for the occlusion at their foot (not exported)."""
    bpy.ops.mesh.primitive_plane_add(size=90, location=(0, 0, 0))
    g = bpy.context.active_object
    g.name = 'ground_ao'
    g.data.materials.append(MATS['suelo'])
    return g


# ------------------------------------------------------------------------------------------------ metadata

def meta():
    props = {}
    for i, part in enumerate(PROPS):
        c = origin(i)
        obs = [ob for ob in PIECES if ob['part'] == part]
        allv = [ob.matrix_world @ v.co for ob in obs for v in ob.data.vertices]
        top = max(p.z for p in allv)
        leaves = [ob.matrix_world @ v.co for ob in obs if ob.get('canopy') for v in ob.data.vertices]
        flat = sorted(((p.x - c.x) ** 2 + (p.y - c.y) ** 2) ** 0.5 for p in leaves)
        info = {'height': round(top, 2), 'axis': 0.0}
        if part in TRUNK_R:
            r = TRUNK_R[part]
            info['obstacles'] = [[0.0, 0.0, round(r * 1.1, 3)]]
            info['half'] = [r, r]
            zs = sorted(p.z for p in leaves)
            # crown: radius (most of the leaves inside it), from the lowest leaves to the top
            info['canopy'] = [round(flat[int(len(flat) * 0.9)], 2), round(zs[int(len(zs) * 0.05)], 2), round(top, 2)]
            info['height'] = round(zs[int(len(zs) * 0.05)], 2)          # the trunk is what shows below the crown
        else:
            reach = flat[int(len(flat) * 0.95)]
            info['obstacles'] = []
            info['half'] = [round(reach, 2), round(reach, 2)]
        props[part] = info
    # warm 0.1: the lit side of the leaves turns only a little to the warm light colour, so they stay green
    return {'name': 'Arboles', 'kind': 'props', 'scale': 1.0, 'warm': 0.1, 'cool': 0.32, 'props': props}


PARTS = {name: {'group': 'props', 'role': 'static', 'pivot': tuple(origin(i))} for i, name in enumerate(PROPS)}
