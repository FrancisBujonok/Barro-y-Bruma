# Barro y Bruma - building kit for Blender.
# Buildings are modelled from code in pieces (logs, planks, stones, furniture). Every piece gets a procedural
# material whose colours come from the 24-colour palette; all the pieces of a building share one texture atlas that
# is baked (albedo x ambient occlusion) and snapped back to the palette. The pieces are then joined by "part"
# (roof, the wall facing the camera, the interior, the basement...) and written to a JSON file that the Unity
# importer (Assets/Editor/BuildingImporter.cs) turns into meshes and a prefab.
#
# Blender: X east, Y north, Z up.  Unity: x east, y up, z north.  The export swaps Y and Z and flips the winding.
import bpy, bmesh, math, random, json, os
from mathutils import Vector, Matrix, Euler

PALETTE = ['141210', '2a2420', '3d332b', '594a3c', '7a6650', '9c8a70', '3f3e3b', '6e6c66', '9a968c', 'c4bfb2',
           '2f3436', '4f5a5c', '7d8584', '3e1a18', '6b2a24', '8f4a36', 'b08a6a', 'd2ac88', 'e6e2d4', '2c2738',
           '5b5170', '4e4b34', '6f6a45', 'b24a36']

PIECES = []
MATS = {}
GLOW_MATS = set()          # pieces with these materials are exported in their own part and shown unlit in Unity


def reset():
    PIECES.clear()
    MATS.clear()
    GLOW_MATS.clear()


def srgb_to_lin(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def hexlin(h):
    return (srgb_to_lin(int(h[0:2], 16) / 255), srgb_to_lin(int(h[2:4], 16) / 255),
            srgb_to_lin(int(h[4:6], 16) / 255), 1.0)


# ------------------------------------------------------------------------------------------------ geometry

def _obj(name, bm, mat, part, loc=(0, 0, 0), rot=(0, 0, 0)):
    me = bpy.data.meshes.new(name)
    bm.normal_update()
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    ob.location = loc
    ob.rotation_euler = rot
    me.materials.append(MATS[mat])
    ob['part'] = part
    ob['mat'] = mat
    PIECES.append(ob)
    return ob


def box(name, mn, mx, mat, part, rot=(0, 0, 0)):
    """Axis-aligned box from min to max corner; the object sits at the centre (rotation turns it about it)."""
    mn, mx = Vector(mn), Vector(mx)
    c, s = (mn + mx) / 2, mx - mn
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co = Vector((v.co.x * s.x, v.co.y * s.y, v.co.z * s.z))
    return _obj(name, bm, mat, part, loc=c, rot=rot)


def cyl(name, p0, p1, r, mat, part, sides=8, r1=None, cap=True, roll=0.0):
    """Cylinder (or cone when r1 is given) from p0 to p1. Local X runs along it."""
    p0, p1 = Vector(p0), Vector(p1)
    d = p1 - p0
    L = d.length
    r1 = r if r1 is None else r1
    bm = bmesh.new()
    rings = []
    for k, (x, rr) in enumerate(((-L / 2, r), (L / 2, r1))):
        ring = []
        for i in range(sides):
            a = 2 * math.pi * i / sides + roll
            ring.append(bm.verts.new((x, rr * math.cos(a), rr * math.sin(a))))
        rings.append(ring)
    for i in range(sides):
        bm.faces.new((rings[0][i], rings[0][(i + 1) % sides], rings[1][(i + 1) % sides], rings[1][i]))
    if cap:
        bm.faces.new(list(reversed(rings[0])))
        bm.faces.new(rings[1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    q = d.normalized().to_track_quat('X', 'Z')
    return _obj(name, bm, mat, part, loc=(p0 + p1) / 2, rot=q.to_euler())


def prism(name, pts, axis, t0, t1, mat, part):
    """A flat polygon given in the plane across `axis` ('x': points are (y, z); 'y': points are (x, z)),
    extruded from t0 to t1 along that axis."""
    bm = bmesh.new()
    def P(a, b, t):
        return (t, a, b) if axis == 'x' else (a, t, b)
    f0 = [bm.verts.new(P(a, b, t0)) for a, b in pts]
    f1 = [bm.verts.new(P(a, b, t1)) for a, b in pts]
    n = len(pts)
    bm.faces.new(f0)
    bm.faces.new(list(reversed(f1)))
    for i in range(n):
        bm.faces.new((f0[i], f0[(i + 1) % n], f1[(i + 1) % n], f1[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return _obj(name, bm, mat, part)


def mesh(name, verts, faces, mat, part):
    bm = bmesh.new()
    vs = [bm.verts.new(v) for v in verts]
    for f in faces:
        bm.faces.new([vs[i] for i in f])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return _obj(name, bm, mat, part)


def rock(name, c, r, mat, part, seed=0, squash=0.7):
    """A lumpy stone: an icosphere pushed in and out."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=1, radius=1.0)
    for v in bm.verts:
        k = rnd.uniform(0.75, 1.15)
        v.co = Vector((v.co.x * r[0] * k, v.co.y * r[1] * k, v.co.z * r[2] * k * squash))
    return _obj(name, bm, mat, part, loc=c)


# ------------------------------------------------------------------------------------------------ materials

def _ramp(nt, fac_socket, cols):
    ramp = nt.nodes.new('ShaderNodeValToRGB')
    ramp.color_ramp.interpolation = 'CONSTANT'
    el = ramp.color_ramp.elements
    n = len(cols)
    while len(el) < n:
        el.new(0.5)
    for i, h in enumerate(cols):
        el[i].position = i / n
        el[i].color = hexlin(h)
    nt.links.new(fac_socket, ramp.inputs['Fac'])
    return ramp


def _math(nt, op, a, b=None):
    m = nt.nodes.new('ShaderNodeMath')
    m.operation = op
    if isinstance(a, (int, float)):
        m.inputs[0].default_value = a
    else:
        nt.links.new(a, m.inputs[0])
    if b is not None:
        if isinstance(b, (int, float)):
            m.inputs[1].default_value = b
        else:
            nt.links.new(b, m.inputs[1])
    return m.outputs[0]


def _vec(nt, coords, scale):
    """Coordinates scaled per axis: coords is 'world' or 'object'."""
    tc = nt.nodes.new('ShaderNodeTexCoord')
    src = tc.outputs['Object'] if coords == 'object' else None
    if src is None:
        geo = nt.nodes.new('ShaderNodeNewGeometry')
        src = geo.outputs['Position']
    mp = nt.nodes.new('ShaderNodeMapping')
    mp.inputs['Scale'].default_value = scale
    nt.links.new(src, mp.inputs['Vector'])
    return mp.outputs['Vector']


def _noise(nt, vec, scale, detail=2.0, rough=0.55):
    n = nt.nodes.new('ShaderNodeTexNoise')
    n.inputs['Scale'].default_value = scale
    n.inputs['Detail'].default_value = detail
    n.inputs['Roughness'].default_value = rough
    nt.links.new(vec, n.inputs['Vector'])
    return n.outputs['Fac']


def _zpos(nt):
    geo = nt.nodes.new('ShaderNodeNewGeometry')
    sep = nt.nodes.new('ShaderNodeSeparateXYZ')
    nt.links.new(geo.outputs['Position'], sep.inputs[0])
    return sep.outputs['Z']


def pmat(name, kind, cols, scale=1.0, grime=0.0, moss=None, coords='world', glow=False, **kw):
    """Procedural palette material. kind: noise, logs, planks, shingles, stone, stripes, dirt, flat."""
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new('ShaderNodeOutputMaterial')
    bsdf = nt.nodes.new('ShaderNodeBsdfPrincipled')
    bsdf.inputs['Roughness'].default_value = 1.0
    nt.links.new(bsdf.outputs[0], out.inputs['Surface'])

    if kind == 'flat':
        fac = _math(nt, 'ADD', _math(nt, 'MULTIPLY', _noise(nt, _vec(nt, coords, (1, 1, 1)), 9.0 * scale), 0.3), 0.35)
    elif kind == 'noise':
        fac = _noise(nt, _vec(nt, coords, (1, 1, 1)), 4.0 * scale, kw.get('detail', 3.0))
    elif kind == 'logs':
        # horizontal logs: grain runs along them (bands in Z), knots from noise
        v = _vec(nt, coords, (0.25, 0.25, 1.0))
        w = nt.nodes.new('ShaderNodeTexWave')
        w.wave_type = 'BANDS'
        w.bands_direction = 'Z'
        w.inputs['Scale'].default_value = 2.2 * scale
        w.inputs['Distortion'].default_value = 5.0
        w.inputs['Detail'].default_value = 2.0
        nt.links.new(v, w.inputs['Vector'])
        fac = _math(nt, 'ADD', _math(nt, 'MULTIPLY', w.outputs['Fac'], 0.45),
                    _math(nt, 'MULTIPLY', _noise(nt, _vec(nt, coords, (1, 1, 1)), 3.0 * scale, 3.0), 0.6))
    elif kind in ('planks', 'shingles', 'bricks'):
        # brick texture: long boards, rows of shingles or blocks; mortar is the dark gap
        bw, rh = kw.get('size', (1.6, 0.2))
        axes = kw.get('axes', 'xy')
        v = _vec(nt, coords, (1, 1, 1))
        if axes != 'xy':
            sep = nt.nodes.new('ShaderNodeSeparateXYZ')
            comb = nt.nodes.new('ShaderNodeCombineXYZ')
            nt.links.new(v, sep.inputs[0])
            a, b = axes[0].upper(), axes[1].upper()
            nt.links.new(sep.outputs[a], comb.inputs['X'])
            nt.links.new(sep.outputs[b], comb.inputs['Y'])
            v = comb.outputs[0]
        br = nt.nodes.new('ShaderNodeTexBrick')
        br.offset = kw.get('offset', 0.5)
        br.squash_frequency = 1
        br.inputs['Scale'].default_value = 1.0
        br.inputs['Brick Width'].default_value = bw
        br.inputs['Row Height'].default_value = rh
        br.inputs['Mortar Size'].default_value = kw.get('mortar', 0.02)
        br.inputs['Mortar Smooth'].default_value = 0.0
        br.inputs['Bias'].default_value = 0.0
        br.inputs['Color1'].default_value = (0.42, 0.42, 0.42, 1)
        br.inputs['Color2'].default_value = (0.72, 0.72, 0.72, 1)
        br.inputs['Mortar'].default_value = (0.0, 0.0, 0.0, 1)
        nt.links.new(v, br.inputs['Vector'])
        bw_node = nt.nodes.new('ShaderNodeRGBToBW')
        nt.links.new(br.outputs['Color'], bw_node.inputs[0])
        fac = _math(nt, 'ADD', _math(nt, 'MULTIPLY', bw_node.outputs[0], 0.85),
                    _math(nt, 'MULTIPLY', _noise(nt, _vec(nt, coords, (1, 1, 1)), 6.0 * scale, 2.0), 0.3))
        fac = _math(nt, 'SUBTRACT', fac, 0.08)
    elif kind == 'fibers':
        # long streaks (thatch, straw): noise squashed along one axis, contrast pushed up
        st = kw.get('stretch', (24.0, 1.2, 24.0))
        n = _noise(nt, _vec(nt, coords, st), scale, 2.0, 0.6)
        fac = _math(nt, 'ADD', _math(nt, 'MULTIPLY', _math(nt, 'SUBTRACT', n, 0.5), 2.4), 0.5)
    elif kind == 'stone':
        v = _vec(nt, coords, (1, 1, 1))
        vo = nt.nodes.new('ShaderNodeTexVoronoi')
        vo.feature = 'DISTANCE_TO_EDGE'
        vo.inputs['Scale'].default_value = 3.0 * scale
        nt.links.new(v, vo.inputs['Vector'])
        vc = nt.nodes.new('ShaderNodeTexVoronoi')
        vc.inputs['Scale'].default_value = 3.0 * scale
        nt.links.new(v, vc.inputs['Vector'])
        cbw = nt.nodes.new('ShaderNodeRGBToBW')
        nt.links.new(vc.outputs['Color'], cbw.inputs[0])
        edge = _math(nt, 'GREATER_THAN', vo.outputs['Distance'], kw.get('mortar', 0.06))      # 0 in the mortar
        body = _math(nt, 'ADD', _math(nt, 'MULTIPLY', cbw.outputs[0], 0.55),
                     _math(nt, 'MULTIPLY', _noise(nt, v, 8.0 * scale), 0.35))
        fac = _math(nt, 'MULTIPLY', _math(nt, 'ADD', body, 0.22), edge)
    elif kind == 'stripes':
        v = _vec(nt, coords, (1, 1, 1))
        w = nt.nodes.new('ShaderNodeTexWave')
        if kw.get('dir') == 'RINGS':
            w.wave_type = 'RINGS'
            w.rings_direction = 'SPHERICAL'
        else:
            w.wave_type = 'BANDS'
            w.bands_direction = kw.get('dir', 'X')
        w.wave_profile = 'SAW'
        w.inputs['Scale'].default_value = 3.0 * scale
        w.inputs['Distortion'].default_value = kw.get('distortion', 0.6)
        nt.links.new(v, w.inputs['Vector'])
        fac = _math(nt, 'ADD', _math(nt, 'MULTIPLY', w.outputs['Fac'], 0.8),
                    _math(nt, 'MULTIPLY', _noise(nt, v, 10.0 * scale), 0.25))
    elif kind == 'dirt':
        v = _vec(nt, coords, (1, 1, 1))
        base = _math(nt, 'MULTIPLY', _noise(nt, v, 2.5 * scale, 4.0), 0.85)
        specks = _math(nt, 'GREATER_THAN', _noise(nt, v, 40.0 * scale, 1.0), 0.66)
        fac = _math(nt, 'ADD', base, _math(nt, 'MULTIPLY', specks, 0.5))
    else:
        raise ValueError(kind)

    if grime > 0:
        # darker near the ground: mud and damp at the foot of walls
        z = _zpos(nt)
        low = _math(nt, 'MAXIMUM', _math(nt, 'SUBTRACT', 1.0, _math(nt, 'DIVIDE', z, 0.7)), 0.0)
        fac = _math(nt, 'SUBTRACT', fac, _math(nt, 'MULTIPLY', low, grime))
    fac = _math(nt, 'MAXIMUM', _math(nt, 'MINIMUM', fac, 0.999), 0.0)
    ramp = _ramp(nt, fac, cols)
    col = ramp.outputs['Color']
    if moss:
        # patches of moss (top-facing parts more)
        geo = nt.nodes.new('ShaderNodeNewGeometry')
        sep = nt.nodes.new('ShaderNodeSeparateXYZ')
        nt.links.new(geo.outputs['Normal'], sep.inputs[0])
        mask = _math(nt, 'GREATER_THAN',
                     _math(nt, 'ADD', _noise(nt, _vec(nt, 'world', (1, 1, 1)), 1.6, 3.0),
                           _math(nt, 'MULTIPLY', sep.outputs['Z'], 0.12)), moss.get('cut', 0.6))
        mr = _ramp(nt, _noise(nt, _vec(nt, 'world', (1, 1, 1)), 7.0, 2.0), moss['cols'])
        mix = nt.nodes.new('ShaderNodeMix')
        mix.data_type = 'RGBA'
        nt.links.new(mask, mix.inputs['Factor'])
        nt.links.new(col, mix.inputs[6])
        nt.links.new(mr.outputs['Color'], mix.inputs[7])
        col = mix.outputs[2]
    nt.links.new(col, bsdf.inputs['Base Color'])
    MATS[name] = m
    if glow:
        GLOW_MATS.add(name)
    return m


# ------------------------------------------------------------------------------------------------ UV, bake

def _view3d_ctx():
    win = bpy.context.window_manager.windows[0]
    area = next((a for a in win.screen.areas if a.type == 'VIEW_3D'), win.screen.areas[0])
    region = next((r for r in area.regions if r.type == 'WINDOW'), area.regions[0])
    return win, area, region


def unwrap(objs, margin=0.004):
    win, area, region = _view3d_ctx()
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    with bpy.context.temp_override(window=win, area=area, region=region, active_object=objs[0],
                                   object=objs[0], selected_objects=objs, selected_editable_objects=objs):
        bpy.ops.object.mode_set(mode='EDIT')
        bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=margin, area_weight=0.0,
                                 correct_aspect=True, scale_to_bounds=False)
        bpy.ops.uv.select_all(action='SELECT')
        bpy.ops.uv.pack_islands(rotate=True, margin=margin)
        bpy.ops.object.mode_set(mode='OBJECT')


def texel_density(objs, size):
    a3 = auv = 0.0
    for o in objs:
        me = o.data
        uv = me.uv_layers.active.data
        mw = o.matrix_world
        for p in me.polygons:
            vs = [mw @ me.vertices[i].co for i in p.vertices]
            us = [uv[li].uv for li in p.loop_indices]
            for k in range(1, len(vs) - 1):
                a3 += ((vs[k] - vs[0]).cross(vs[k + 1] - vs[0])).length / 2
                e1, e2 = us[k] - us[0], us[k + 1] - us[0]
                auv += abs(e1.x * e2.y - e1.y * e2.x) / 2
    return math.sqrt(auv * size * size / max(a3, 1e-6))


def _set_target(img):
    for m in MATS.values():
        nt = m.node_tree
        node = nt.nodes.get('BakeTarget')
        if node is None:
            node = nt.nodes.new('ShaderNodeTexImage')
            node.name = 'BakeTarget'
        node.image = img
        nt.nodes.active = node


def bake(groups, size, out_png, palette=True, ao_dist=0.35):
    """Bakes albedo and AO into one atlas, multiplies them and snaps to the palette. Runs synchronously.
    groups: lists of pieces baked separately (the basement shares the cabin's spot, so each group hides the
    other one while its occlusion is baked)."""
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    sc.cycles.device = 'CPU'
    sc.cycles.samples = 16
    sc.render.bake.margin = 3
    if sc.world is None:
        sc.world = bpy.data.worlds.new('World')
    sc.world.light_settings.distance = ao_dist
    alb = bpy.data.images.new('atlas_albedo', size, size, alpha=False)
    ao = bpy.data.images.new('atlas_ao', size, size, alpha=False)
    ao.colorspace_settings.name = 'Non-Color'
    win, area, region = _view3d_ctx()
    everything = [o for g in groups for o in g]
    for gi, objs in enumerate(groups):
        for o in everything:
            o.hide_render = o not in objs
        for o in bpy.context.view_layer.objects:
            o.select_set(False)
        for o in objs:
            o.select_set(True)
        bpy.context.view_layer.objects.active = objs[0]
        sc.render.bake.use_clear = gi == 0
        with bpy.context.temp_override(window=win, area=area, region=region, active_object=objs[0],
                                       object=objs[0], selected_objects=objs, selected_editable_objects=objs):
            sc.cycles.samples = 4
            _set_target(alb)
            bpy.ops.object.bake(type='DIFFUSE', pass_filter={'COLOR'}, margin=3, use_clear=gi == 0)
            sc.cycles.samples = 24
            _set_target(ao)
            bpy.ops.object.bake(type='AO', margin=3, use_clear=gi == 0)
    for o in everything:
        o.hide_render = False
    n = size * size
    a = [0.0] * (n * 4)
    b = [0.0] * (n * 4)
    alb.pixels.foreach_get(a)
    ao.pixels.foreach_get(b)
    pal = [(int(h[0:2], 16) / 255, int(h[2:4], 16) / 255, int(h[4:6], 16) / 255) for h in PALETTE]
    out = [0.0] * (n * 4)
    cache = {}
    for i in range(n):
        k = i * 4
        o = 0.72 + 0.28 * min(1.0, b[k] * 1.1)                   # AO: only corners and gaps darken
        r, g, bb = a[k] * o, a[k + 1] * o, a[k + 2] * o
        if palette:
            key = (int(r * 64), int(g * 64), int(bb * 64))
            c = cache.get(key)
            if c is None:
                best, bd = None, 9.0
                for p in pal:
                    dr, dg, db = r - p[0], g - p[1], bb - p[2]
                    d = 0.3 * dr * dr + 0.59 * dg * dg + 0.11 * db * db
                    if d < bd:
                        bd, best = d, p
                c = best
                cache[key] = c
            r, g, bb = c
        out[k], out[k + 1], out[k + 2], out[k + 3] = r, g, bb, 1.0
    res = bpy.data.images.new('atlas_final', size, size, alpha=False)
    res.pixels.foreach_set(out)
    res.filepath_raw = out_png
    res.file_format = 'PNG'
    res.save()
    return out_png


# ------------------------------------------------------------------------------------------------ export

def _u(v):
    return (round(v.x, 4), round(v.z, 4), round(v.y, 4))


def export(path, info, parts_info):
    """Joins pieces by part and writes the JSON for Unity. parts_info: {part: {group, role, normal, pivot}}."""
    data = {'format': 'bruma-building', 'version': 1}
    data.update(info)
    parts = {}
    for part in sorted(set(o['part'] for o in PIECES)):
        objs = [o for o in PIECES if o['part'] == part]
        pinfo = dict(parts_info.get(part, {}))
        pivot = Vector(pinfo.get('pivot', (0, 0, 0)))
        # smooth normals by position, for the outline
        acc = {}
        tris = []
        for o in objs:
            me = o.data
            me.calc_loop_triangles()
            mw = o.matrix_world
            nm = mw.to_3x3().inverted().transposed()
            uvd = me.uv_layers.active.data
            for t in me.loop_triangles:
                n = (nm @ t.normal).normalized()
                pts = [mw @ me.vertices[me.loops[li].vertex_index].co for li in t.loops]
                area = ((pts[1] - pts[0]).cross(pts[2] - pts[0])).length / 2
                for p in pts:
                    key = (round(p.x, 3), round(p.y, 3), round(p.z, 3))
                    acc[key] = acc.get(key, Vector()) + n * max(area, 1e-5)
                tris.append((pts, n, [uvd[li].uv.copy() for li in t.loops]))
        index = {}
        verts, norms, uvs, cols, ids = [], [], [], [], []
        for pts, n, us in tris:
            tri = []
            for p, uv in zip(pts, us):
                key = (round(p.x, 3), round(p.y, 3), round(p.z, 3))
                sn = acc[key].normalized() if acc[key].length > 1e-6 else n
                q = p - pivot
                k = (_u(q), _u(n), (round(uv.x, 5), round(uv.y, 5)))
                i = index.get(k)
                if i is None:
                    i = len(verts)
                    index[k] = i
                    verts.append(_u(q))
                    norms.append(_u(n))
                    uvs.append(k[2])
                    s = _u(sn)
                    cols.append((round(s[0] * 0.5 + 0.5, 3), round(s[1] * 0.5 + 0.5, 3), round(s[2] * 0.5 + 0.5, 3)))
                tri.append(i)
            ids.extend((tri[0], tri[2], tri[1]))                 # flip winding for Unity
        pinfo['pivot'] = list(_u(pivot))
        if 'normal' in pinfo:
            pinfo['normal'] = list(_u(Vector(pinfo['normal'])))
        mats = set(o['mat'] for o in objs)
        pinfo.setdefault('glow', bool(mats) and mats <= GLOW_MATS)
        pinfo['v'] = [c for v in verts for c in v]
        pinfo['n'] = [c for v in norms for c in v]
        pinfo['uv'] = [c for v in uvs for c in v]
        pinfo['c'] = [c for v in cols for c in v]
        pinfo['t'] = ids
        parts[part] = pinfo
    data['parts'] = parts
    with open(path, 'w', encoding='utf-8') as f:
        json.dump(data, f, separators=(',', ':'))
    return {p: len(parts[p]['t']) // 3 for p in parts}


def U(x, y, z):
    """Blender point to the Unity coordinates used in the metadata."""
    return [round(x, 4), round(z, 4), round(y, 4)]


def UB(x0, y0, x1, y1):
    """Blender ground rectangle to a Unity obstacle box: [centre x, centre z, half x, half z]."""
    return [round((x0 + x1) / 2, 3), round((y0 + y1) / 2, 3), round(abs(x1 - x0) / 2, 3), round(abs(y1 - y0) / 2, 3)]
