# Barro y Bruma - big rocks (five variants) for the paths, cliff edges and landmarks.
# Built with edificio_kit.py (exec it first in the same namespace), baked into one atlas and exported as a
# "props" set: every rock is one part with its pivot at the centre of its base. The Unity importer
# (BuildingImporter, kind "props") makes one prefab per rock in Assets/Resources/Props/ with a hitbox fitted to
# the foot of the rock (one to three circles), so the hero can walk right up to the stone.
#
# The rocks are convex hulls of jittered points on an ellipsoid: flat facets, like split stone, which read well
# with the toon shading. Texture: grey-brown stone in strata, dark cracks, pale lichen and moss on top.
#
# Sizes are game metres at scale 1 (the hero is about 2.7 m tall in the world):
#   RocaPena     big lumpy boulder          ~3.4 x 2.2 m, 2.4 m tall
#   RocaAguja    tall leaning needle        ~1.7 x 1.1 m, 3.3 m tall
#   RocaLosa     stacked slabs (strata)     ~2.9 x 2.1 m, 1.2 m tall
#   RocaPartida  boulder split in two       ~2.9 x 2.2 m, 2.0 m tall
#   RocaMonton   heap of stones             ~2.9 x 2.6 m, 1.3 m tall
import math, random
from mathutils import Vector, Matrix

ROCKS = ['RocaPena', 'RocaAguja', 'RocaLosa', 'RocaPartida', 'RocaMonton']
SPACING = 5.0
SINK = 0.12                         # how far the base goes into the ground


def origin(i):
    return Vector(((i - 2) * SPACING, 0.0, 0.0))


def rock_mat():
    """Stone in strata with cracks, lichen and moss; colours from the palette."""
    m = bpy.data.materials.new('roca')
    m.use_nodes = True
    nt = m.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new('ShaderNodeOutputMaterial')
    bsdf = nt.nodes.new('ShaderNodeBsdfPrincipled')
    bsdf.inputs['Roughness'].default_value = 1.0
    nt.links.new(bsdf.outputs[0], out.inputs['Surface'])
    v = _vec(nt, 'world', (1, 1, 1))
    # strata: distorted horizontal bands
    w = nt.nodes.new('ShaderNodeTexWave')
    w.wave_type = 'BANDS'
    w.bands_direction = 'Z'
    w.inputs['Scale'].default_value = 1.1
    w.inputs['Distortion'].default_value = 4.0
    w.inputs['Detail'].default_value = 2.0
    nt.links.new(v, w.inputs['Vector'])
    body = _math(nt, 'ADD', _math(nt, 'MULTIPLY', w.outputs['Fac'], 0.12),
                 _math(nt, 'MULTIPLY', _noise(nt, v, 1.1, 3.0, 0.5), 0.62))
    body = _math(nt, 'ADD', body, _math(nt, 'MULTIPLY', _noise(nt, v, 9.0, 2.0), 0.22))
    body = _math(nt, 'SUBTRACT', body, 0.02)
    # cracks: thin voronoi edges, warped
    warp = nt.nodes.new('ShaderNodeVectorMath')
    warp.operation = 'ADD'
    nt.links.new(v, warp.inputs[0])
    nz = nt.nodes.new('ShaderNodeTexNoise')
    nz.inputs['Scale'].default_value = 2.0
    nt.links.new(v, nz.inputs['Vector'])
    sc = nt.nodes.new('ShaderNodeVectorMath')
    sc.operation = 'SCALE'
    sc.inputs['Scale'].default_value = 0.35
    nt.links.new(nz.outputs['Color'], sc.inputs[0])
    nt.links.new(sc.outputs[0], warp.inputs[1])
    vo = nt.nodes.new('ShaderNodeTexVoronoi')
    vo.feature = 'DISTANCE_TO_EDGE'
    vo.inputs['Scale'].default_value = 0.85
    nt.links.new(warp.outputs[0], vo.inputs['Vector'])
    crack = _math(nt, 'LESS_THAN', vo.outputs['Distance'], 0.02)
    body = _math(nt, 'SUBTRACT', body, _math(nt, 'MULTIPLY', crack, 0.55))
    # damp and darker near the ground
    z = _zpos(nt)
    low = _math(nt, 'MAXIMUM', _math(nt, 'SUBTRACT', 1.0, _math(nt, 'DIVIDE', z, 0.45)), 0.0)
    body = _math(nt, 'SUBTRACT', body, _math(nt, 'MULTIPLY', low, 0.3))
    body = _math(nt, 'MAXIMUM', _math(nt, 'MINIMUM', body, 0.999), 0.0)
    col = _ramp(nt, body, ['141210', '2a2420', '3f3e3b', '6e6c66', '7d8584', '9a968c'])
    col = col.outputs['Color']

    def mix(base, mask, other):
        mx = nt.nodes.new('ShaderNodeMix')
        mx.data_type = 'RGBA'
        nt.links.new(mask, mx.inputs['Factor'])
        nt.links.new(base, mx.inputs[6])
        nt.links.new(other, mx.inputs[7])
        return mx.outputs[2]

    geo = nt.nodes.new('ShaderNodeNewGeometry')
    sep = nt.nodes.new('ShaderNodeSeparateXYZ')
    nt.links.new(geo.outputs['Normal'], sep.inputs[0])
    # pale lichen spots, anywhere but the underside
    lich = _math(nt, 'GREATER_THAN', _noise(nt, v, 6.0, 2.0, 0.6), 0.7)
    lich = _math(nt, 'MULTIPLY', lich, _math(nt, 'GREATER_THAN', sep.outputs['Z'], -0.2))
    col = mix(col, lich, _ramp(nt, _noise(nt, v, 14.0), ['9c8a70', 'c4bfb2']).outputs['Color'])
    # moss on the faces looking up
    mask = _math(nt, 'GREATER_THAN', _math(nt, 'ADD', _math(nt, 'MULTIPLY', sep.outputs['Z'], 0.55),
                                           _math(nt, 'MULTIPLY', _noise(nt, v, 2.2, 3.0), 0.5)), 0.74)
    col = mix(col, mask, _ramp(nt, _noise(nt, v, 8.0, 2.0), ['2a2420', '4e4b34', '6f6a45']).outputs['Color'])
    nt.links.new(col, bsdf.inputs['Base Color'])
    MATS['roca'] = m
    pmat('suelo', 'flat', ['3d332b', '3d332b', '3d332b'])
    return m


# ------------------------------------------------------------------------------------------------ geometry

def _points(rnd, size, n, jag):
    pts = []
    for _ in range(n):
        d = Vector((rnd.gauss(0, 1), rnd.gauss(0, 1), rnd.gauss(0, 1)))
        if d.length < 1e-6:
            continue
        d.normalize()
        k = 1.0 + rnd.uniform(-jag, jag)
        p = Vector((d.x * size[0] * k, d.y * size[1] * k, d.z * size[2] * k))
        if p.z < 0:
            p.z *= 0.3                                         # broad, flattish foot
        pts.append(p)
    return pts


def _hull_obj(name, pts, loc, part, rot_z=0.0, lean=(0.0, 0.0), dissolve=7.0):
    bm = bmesh.new()
    vs = [bm.verts.new(p) for p in pts]
    res = bmesh.ops.convex_hull(bm, input=vs)
    junk = list(set(res['geom_interior']) | set(res['geom_unused']))
    bmesh.ops.delete(bm, geom=[g for g in junk if isinstance(g, bmesh.types.BMVert)], context='VERTS')
    bmesh.ops.dissolve_limit(bm, angle_limit=math.radians(dissolve), verts=bm.verts[:], edges=bm.edges[:])
    for v in bm.verts:
        v.co.x += lean[0] * max(v.co.z, 0.0)
        v.co.y += lean[1] * max(v.co.z, 0.0)
    rz = Matrix.Rotation(rot_z, 3, 'Z')
    for v in bm.verts:
        v.co = rz @ v.co
    zmin = min(v.co.z for v in bm.verts)
    for v in bm.verts:
        v.co.z -= zmin + SINK
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    return _obj(name, bm, 'roca', part, loc=loc)


def hull(name, part, loc, size, seed, n=48, jag=0.16, rot_z=0.0, lean=(0.0, 0.0), dissolve=7.0):
    rnd = random.Random(seed)
    return _hull_obj(name, _points(rnd, size, n, jag), loc, part, rot_z, lean, dissolve)


def split_hull(name, part, loc, size, seed, gap=0.16, n=56, jag=0.15, tilt=0.12):
    """One boulder cut in two along a near-vertical plane; the halves lean apart."""
    rnd = random.Random(seed)
    pts = _points(rnd, size, n, jag)
    normal = Vector((1.0, 0.25, 0.0)).normalized()
    halves = []
    for side in (1, -1):
        sel = [p for p in pts if side * p.dot(normal) > 0]
        flat = [p - normal * p.dot(normal) for p in sel]       # the clean split face
        halves.append(sel + flat)
    obs = []
    for k, (side, hp) in enumerate(zip((1, -1), halves)):
        rot = Matrix.Rotation(-side * tilt, 3, Vector((-normal.y, normal.x, 0.0)))
        hp = [rot @ p + normal * side * gap * 0.5 for p in hp]
        obs.append(_hull_obj('%s_%d' % (name, k), hp, loc, part))
    return obs


def build_all():
    for ob in list(bpy.data.objects):
        bpy.data.objects.remove(ob, do_unlink=True)
    for m in list(bpy.data.materials):
        bpy.data.materials.remove(m)
    for me in list(bpy.data.meshes):
        bpy.data.meshes.remove(me)
    reset()
    rock_mat()
    o = [origin(i) for i in range(5)]
    # 1. big lumpy boulder: a main body and a shoulder
    hull('pena_a', 'RocaPena', o[0], (1.35, 1.15, 1.8), seed=11, n=60)
    hull('pena_b', 'RocaPena', o[0] + Vector((0.85, -0.55, 0)), (0.75, 0.7, 0.95), seed=12, n=36)
    hull('pena_c', 'RocaPena', o[0] + Vector((-0.95, 0.5, 0)), (0.55, 0.55, 0.45), seed=13, n=30)
    # 2. tall needle, leaning, with a broken chunk at its foot
    hull('aguja_a', 'RocaAguja', o[1], (0.72, 0.6, 2.4), seed=21, n=46, jag=0.2, lean=(0.12, 0.05))
    hull('aguja_b', 'RocaAguja', o[1] + Vector((0.55, -0.45, 0)), (0.4, 0.35, 0.42), seed=22, n=24)
    # 3. stacked slabs, each a little turned
    hull('losa_a', 'RocaLosa', o[2], (1.65, 1.15, 0.5), seed=31, n=40, jag=0.1, dissolve=10)
    hull('losa_b', 'RocaLosa', o[2] + Vector((0.2, 0.1, 0.52)), (1.3, 0.9, 0.33), seed=32, n=36, jag=0.1, rot_z=0.2, dissolve=10)
    hull('losa_c', 'RocaLosa', o[2] + Vector((-0.25, 0.15, 0.88)), (0.8, 0.6, 0.26), seed=33, n=30, jag=0.12, rot_z=-0.25, dissolve=10)
    # 4. split boulder
    split_hull('partida', 'RocaPartida', o[3], (1.35, 1.05, 1.75), seed=41)
    # 5. heap of stones
    hull('monton_a', 'RocaMonton', o[4] + Vector((-0.45, 0.35, 0)), (0.95, 0.85, 1.3), seed=51, n=40)
    hull('monton_b', 'RocaMonton', o[4] + Vector((0.75, -0.15, 0)), (0.7, 0.6, 0.95), seed=52, n=34)
    hull('monton_c', 'RocaMonton', o[4] + Vector((0.1, -0.85, 0)), (0.55, 0.5, 0.7), seed=53, n=30)
    for k, (x, y, r) in enumerate(((-1.25, -0.6, 0.28), (1.35, 0.6, 0.22), (-0.2, 1.05, 0.25), (1.1, -0.95, 0.18))):
        hull('monton_p%d' % k, 'RocaMonton', o[4] + Vector((x, y, 0)), (r, r * 0.85, r * 0.7), seed=60 + k, n=14)
    bpy.context.view_layer.update()
    return len(PIECES)


def ground():
    """A plane under the rocks, only for the occlusion at their foot (not exported)."""
    bpy.ops.mesh.primitive_plane_add(size=40, location=(0, 0, 0))
    g = bpy.context.active_object
    g.name = 'ground_ao'
    g.data.materials.append(MATS['suelo'])
    return g


# ------------------------------------------------------------------------------------------------ hitboxes

def footprint(part):
    """Points of the rock near the ground (below 0.5 m), relative to its pivot, in Blender XY."""
    i = ROCKS.index(part)
    c = origin(i)
    pts = []
    for ob in PIECES:
        if ob['part'] != part:
            continue
        mw = ob.matrix_world
        for v in ob.data.vertices:
            p = mw @ v.co
            if p.z < 0.5:
                pts.append(Vector((p.x - c.x, p.y - c.y)))
    return pts


def fit_circles(pts, shrink=0.88):
    """One to three circles along the main axis of the foot: a little inside the stone, never bigger."""
    n = len(pts)
    mx = sum(p.x for p in pts) / n
    my = sum(p.y for p in pts) / n
    sxx = sum((p.x - mx) ** 2 for p in pts) / n
    syy = sum((p.y - my) ** 2 for p in pts) / n
    sxy = sum((p.x - mx) * (p.y - my) for p in pts) / n
    ang = 0.5 * math.atan2(2 * sxy, sxx - syy)
    ax = Vector((math.cos(ang), math.sin(ang)))
    ay = Vector((-ax.y, ax.x))
    us = [(p - Vector((mx, my))).dot(ax) for p in pts]
    ws = [(p - Vector((mx, my))).dot(ay) for p in pts]
    u0, u1, w0, w1 = min(us), max(us), min(ws), max(ws)
    L, W = u1 - u0, w1 - w0
    cu, cw = (u0 + u1) / 2, (w0 + w1) / 2
    r = min(W, L) * 0.5 * shrink
    # end circles reach the ends of the foot; more in between only if the gap would be too wide
    k = 1 if L - 2 * r < 0.15 else min(3, 1 + math.ceil((L - 2 * r) / (1.5 * r)))
    circles = []
    for j in range(k):
        u = cu if k == 1 else (u0 + r) + (L - 2 * r) * j / (k - 1)
        p = Vector((mx, my)) + ax * u + ay * cw
        circles.append([round(p.x, 3), round(p.y, 3), round(r, 3)])     # Unity: x, z
    return circles, (round(L / 2, 3), round(W / 2, 3)), math.degrees(ang)


def meta():
    props = {}
    for i, part in enumerate(ROCKS):
        circles, half, ang = fit_circles(footprint(part))
        top = max((ob.matrix_world @ v.co).z for ob in PIECES if ob['part'] == part for v in ob.data.vertices)
        props[part] = {'obstacles': circles, 'half': list(half), 'axis': round(ang, 1), 'height': round(top, 2)}
    return {'name': 'Rocas', 'kind': 'props', 'scale': 1.0, 'props': props}


PARTS = {name: {'group': 'props', 'role': 'static', 'pivot': tuple(origin(i))} for i, name in enumerate(ROCKS)}
