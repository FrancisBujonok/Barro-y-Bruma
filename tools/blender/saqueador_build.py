# Barro y Bruma - scavengers who wear what they take off the dead (art_src/saqueador/saqueador.blend).
# Two looks on one body:
#   saqueador  - someone else's dented nasal helmet, a bark mask, a stained and torn Cenizal tabard over a dark
#                gambeson, a wood axe
#   ballestero - a cloth hood, a bark mask, a fur scrap and one leather pauldron, a bandolier of bolts, a crossbow
import bpy, bmesh, math, random
from mathutils import Vector, Matrix

HEAD_C = Vector((0.0, -0.01, 1.6))
HEAD_R = (0.094, 0.104, 0.114)


def mats():
    pm = pal_mat
    pm('Q_Piel', ['3d332b', '7a6650', 'b08a6a', 'd2ac88'])
    pm('Q_Gambeson', ['141210', '2a2420', '3d332b', '594a3c'])
    pm('Q_Calzas', ['141210', '2a2420', '2f3436', '4f5a5c'])
    pm('Q_Cuero', ['141210', '2a2420', '3d332b', '594a3c'])
    pm('Q_Corteza', ['141210', '3d332b', '594a3c', '7a6650'])
    pm('Q_Hierro', ['141210', '3f3e3b', '6e6c66', '9a968c'])
    pm('Q_Oxido', ['141210', '3e1a18', '6b2a24', '8f4a36'])
    pm('Q_Sobreveste', ['2a2420', '6e6c66', '9a968c', 'c4bfb2'])
    pm('Q_Mancha', ['141210', '3e1a18', '3e1a18', '6b2a24'])
    pm('Q_Brasa', ['3e1a18', '6b2a24', 'b24a36', 'b24a36'])
    pm('Q_Capucha', ['141210', '2a2420', '4e4b34', '6f6a45'])
    pm('Q_Piel_Animal', ['2a2420', '594a3c', '7a6650', '9c8a70'])
    pm('Q_Madera', ['141210', '2a2420', '594a3c', '7a6650'])
    pm('Q_Pluma', ['3f3e3b', '9a968c', 'c4bfb2', 'e6e2d4'])
    pm('Q_Ojo', ['141210', '141210', '2a2420', '3d332b'])


def to_bone(ob, bone):
    b = ARM.bones[bone]
    ob.parent = RIG
    ob.parent_type = 'BONE'
    ob.parent_bone = bone
    ob.matrix_parent_inverse = Matrix.Identity(4)
    ob.matrix_basis = (b.matrix_local @ Matrix.Translation((0, b.length, 0))).inverted()


def tag(ob, v):
    ob['variant'] = v
    return ob


def build_common(seed=71):
    bpy.data.objects['Body'].data.materials[0] = mat('Q_Calzas')
    bpy.data.objects['Tunic'].data.materials[0] = mat('Q_Gambeson')
    bpy.data.objects['Boots'].data.materials[0] = mat('Q_Cuero')
    for n in ('Belt', 'Pouch', 'Gloves'):
        o = bpy.data.objects.get(n)
        if o:
            o.data.materials[0] = mat('Q_Cuero')
    for n in ('Eye.L', 'Eye.R'):
        e = bpy.data.objects[n]
        e.data.materials[0] = mat('Q_Ojo')
        w = e.matrix_world.copy()
        w.translation.y = -0.135
        e.matrix_world = w
    bm = bmesh.new()
    ellipsoid(bm, HEAD_C, HEAD_R, 14, 10)
    to_bone(tag(new_obj('Q_Cabeza', bm, 'Q_Piel', parent_rig=True), 0), 'head')
    # bark mask: a curved plate of bark over the face with two holes for the eyes (the eyes show dark through it)
    bm = bmesh.new()
    cols, rows = 6, 6
    grid = []
    for r in range(rows):
        z = 1.66 - 0.16 * r / (rows - 1)
        row = []
        for k in range(cols):
            a = -0.9 + 1.8 * k / (cols - 1)
            rad = HEAD_R[1] + 0.022 + (0.01 if r in (2, 3) else 0)
            row.append(bm.verts.new((math.sin(a) * (HEAD_R[0] + 0.02), HEAD_C.y - rad * math.cos(a), z)))
        grid.append(row)
    for r in range(rows - 1):
        for k in range(cols - 1):
            if r == 1 and k in (1, 3):     # eye holes
                continue
            bm.faces.new((grid[r][k], grid[r][k + 1], grid[r + 1][k + 1], grid[r + 1][k]))
    ob = tag(new_obj('Q_Mascara', bm, 'Q_Corteza', parent_rig=True, smooth=False), 0)
    to_bone(ob, 'head')
    ob.modifiers.new('Grosor', 'SOLIDIFY').thickness = 0.012


def build_saqueador(seed=72):
    rnd = random.Random(seed)
    c = HEAD_C + Vector((0, 0.004, 0.02))
    # dented conical helmet with a nasal bar, rust patches
    bm = bmesh.new()
    sides = 16
    rings = []
    for k in range(6):
        t = k / 5
        ring = []
        for i in range(sides):
            a = 2 * math.pi * i / sides
            dent = 0.012 if (i in (3, 4) and k in (2, 3)) else 0.0
            r = (HEAD_R[0] + 0.02) * math.sin(t * math.pi / 2) - dent
            ring.append(bm.verts.new((c.x + r * math.sin(a), c.y - r * 1.08 * math.cos(a), c.z + 0.17 - 0.17 * t ** 1.4)))
        rings.append(ring)
    for k in range(5):
        for i in range(sides):
            bm.faces.new((rings[k][i], rings[k][(i + 1) % sides], rings[k + 1][(i + 1) % sides], rings[k + 1][i]))
    m = bmesh.ops.create_cube(bm, size=1.0)
    for v in m['verts']:
        v.co = Vector((v.co.x * 0.018, c.y - HEAD_R[1] - 0.04 + v.co.y * 0.012, c.z - 0.03 + v.co.z * 0.09))
    ob = tag(new_obj('Q1_Casco', bm, 'Q_Hierro', parent_rig=True, smooth=False), 1)
    to_bone(ob, 'head')
    bm = bmesh.new()
    ellipsoid(bm, (0.05, -0.06, c.z + 0.08), (0.03, 0.03, 0.03), 6, 5)
    ellipsoid(bm, (-0.06, 0.04, c.z + 0.05), (0.035, 0.03, 0.03), 6, 5)
    to_bone(tag(new_obj('Q1_Oxido', bm, 'Q_Oxido', parent_rig=True), 1), 'head')
    # the stolen Cenizal tabard: torn hem, a big dark stain over the ember
    for name, sy in (('Q1_SobrevesteF', -1), ('Q1_SobrevesteB', 1)):
        bm = bmesh.new()
        cols, rows = 5, 7
        grid = []
        for r in range(rows):
            z = 1.36 - 0.66 * r / (rows - 1)
            half = 0.12 + 0.03 * r / (rows - 1)
            depth = 0.13 if z > 1.0 else 0.13 + (1.0 - z) * 0.08
            row = []
            for k in range(cols):
                x = -half + 2 * half * k / (cols - 1)
                zz = z + (rnd.uniform(-0.07, 0.02) if r == rows - 1 else 0)
                row.append(bm.verts.new((x, sy * (depth - 0.03 * (x / half) ** 2), zz)))
            grid.append(row)
        for r in range(rows - 1):
            for k in range(cols - 1):
                if r == rows - 2 and k == 1 and sy < 0:
                    continue                    # a rip
                bm.faces.new((grid[r][k], grid[r][k + 1], grid[r + 1][k + 1], grid[r + 1][k]))
        ob = tag(new_obj(name, bm, 'Q_Sobreveste', parent_rig=True), 1)
        to_bone(ob, 'chest')
        ob.modifiers.new('Grosor', 'SOLIDIFY').thickness = 0.008
    bm = bmesh.new()
    ellipsoid(bm, (0.0, -0.142, 1.2), (0.03, 0.01, 0.04), 8, 5)
    to_bone(tag(new_obj('Q1_Brasa', bm, 'Q_Brasa', parent_rig=True), 1), 'chest')
    bm = bmesh.new()
    ellipsoid(bm, (0.04, -0.146, 1.12), (0.07, 0.01, 0.09), 8, 5)
    to_bone(tag(new_obj('Q1_Mancha', bm, 'Q_Mancha', parent_rig=True), 1), 'chest')
    # wood axe in the right hand, along the hand bone
    ha = ARM.bones['ha.R']
    bm = bmesh.new()
    tube(bm, [Vector((0, 0.0, 0)), Vector((0, 0.35, 0)), Vector((0, 0.68, 0))], [0.017, 0.016, 0.016], 6, cap_tip=False)
    ob = tag(new_obj('Q1_Mango', bm, 'Q_Madera', 'ha.R', Matrix.Translation((0, ha.length * 0.5, 0))), 1)
    bm = bmesh.new()
    prof = [(0.0, 0.58), (0.0, 0.7), (-0.13, 0.74), (-0.15, 0.6), (-0.06, 0.6)]
    vs = [(bm.verts.new((0.012, y, x)), bm.verts.new((-0.012, y, x))) for x, y in prof]
    n = len(vs)
    bm.faces.new([v[0] for v in vs])
    bm.faces.new([v[1] for v in reversed(vs)])
    for i in range(n):
        a, b = vs[i], vs[(i + 1) % n]
        bm.faces.new((a[0], b[0], b[1], a[1]))
    tag(new_obj('Q1_Hacha', bm, 'Q_Hierro', 'ha.R', Matrix.Translation((0, ha.length * 0.5, 0))), 1)


def build_ballestero(seed=73):
    rnd = random.Random(seed)
    # cloth hood over the head and shoulders
    bm = bmesh.new()
    m = ellipsoid(bm, HEAD_C + Vector((0, 0.012, 0.012)), (HEAD_R[0] + 0.03, HEAD_R[1] + 0.03, HEAD_R[2] + 0.025), 14, 10)
    drop = [v for v in m if (v.co.y - HEAD_C.y) < -0.075 and abs(v.co.x) < 0.075 and (v.co.z - HEAD_C.z) < 0.07]
    bmesh.ops.delete(bm, geom=drop, context='VERTS')
    sides = 18
    ring0 = [bm.verts.new((0.12 * math.sin(2 * math.pi * i / sides), -0.1 * math.cos(2 * math.pi * i / sides) + 0.02, 1.47)) for i in range(sides)]
    ring1 = [bm.verts.new((0.24 * math.sin(2 * math.pi * i / sides), -0.18 * math.cos(2 * math.pi * i / sides) + 0.02, 1.3 + rnd.uniform(-0.02, 0.02))) for i in range(sides)]
    for i in range(sides):
        bm.faces.new((ring0[i], ring0[(i + 1) % sides], ring1[(i + 1) % sides], ring1[i]))
    ob = tag(new_obj('Q2_Capucha', bm, 'Q_Capucha', parent_rig=True), 2)
    to_bone(ob, 'head')
    ob.modifiers.new('Grosor', 'SOLIDIFY').thickness = 0.01
    # fur scrap on the left shoulder, leather pauldron on the right, bandolier of bolts across the chest
    bm = bmesh.new()
    ellipsoid(bm, (0.17, 0.02, 1.36), (0.12, 0.12, 0.05), 10, 6)
    for k in range(7):
        a = rnd.uniform(0, 2 * math.pi)
        p = Vector((0.17 + 0.1 * math.cos(a), 0.02 + 0.1 * math.sin(a), 1.33))
        tube(bm, [p, p + Vector((0.02 * math.cos(a), 0.02 * math.sin(a), -0.07))], [0.02, 0.008], 4)
    to_bone(tag(new_obj('Q2_Piel', bm, 'Q_Piel_Animal', parent_rig=True), 2), 'upchest')
    bm = bmesh.new()
    ellipsoid(bm, (-0.2, 0.0, 1.39), (0.075, 0.08, 0.04), 10, 6)
    to_bone(tag(new_obj('Q2_Hombrera', bm, 'Q_Cuero', parent_rig=True), 2), 'sh.R')
    bm = bmesh.new()
    pts = [Vector((0.15 - 0.3 * t, -0.135 - 0.02 * math.sin(math.pi * t), 1.38 - 0.42 * t)) for t in (0, 0.25, 0.5, 0.75, 1)]
    tube(bm, pts, [0.016] * 5, 5, cap_tip=False)
    for t in (0.2, 0.35, 0.5, 0.65):
        p = Vector((0.15 - 0.3 * t, -0.15, 1.38 - 0.42 * t))
        tube(bm, [p, p + Vector((0.0, -0.01, 0.07))], [0.006, 0.006], 4)
    to_bone(tag(new_obj('Q2_Bandolera', bm, 'Q_Cuero', parent_rig=True), 2), 'chest')
    # crossbow in its own space: stock along +Y (butt at 0, prod at the front), placed every frame by a hook
    bm = bmesh.new()
    m = bmesh.ops.create_cube(bm, size=1.0)
    for v in m['verts']:
        v.co = Vector((v.co.x * 0.04, 0.27 + v.co.y * 0.54, v.co.z * 0.05))
    pts = [Vector((math.sin(a) * 0.3, 0.5 - 0.09 * math.cos(a), 0.01)) for a in [(-1 + 2 * i / 10) * 1.1 for i in range(11)]]
    tube(bm, pts, [0.014] * len(pts), 5, cap_tip=False)
    ob = tag(new_obj('Q2_Ballesta', bm, 'Q_Madera', parent_rig=True, smooth=False), 2)
    bm = bmesh.new()
    tube(bm, [Vector((-0.28, 0.43, 0.012)), Vector((0, 0.22, 0.03)), Vector((0.28, 0.43, 0.012))], [0.004] * 3, 3, cap_tip=False)
    tube(bm, [Vector((0, 0.2, 0.04)), Vector((0, 0.56, 0.04))], [0.01, 0.008], 4)
    s = tag(new_obj('Q2_Cuerda', bm, 'Q_Pluma', parent_rig=True), 2)
    s.parent = ob
    s.matrix_basis = Matrix.Identity(4)


def show_variant(v):
    for o in bpy.data.objects:
        if 'variant' in o:
            on = o['variant'] in (0, v)
            o.hide_render = not on
            o.hide_viewport = not on


def build_all():
    RIG.matrix_world = Matrix.Identity(4)
    for pb in RIG.pose.bones:
        pb.matrix_basis = Matrix.Identity(4)
    bpy.context.view_layer.update()
    mats()
    build_common()
    build_saqueador()
    build_ballestero()
    show_variant(1)
    bpy.context.view_layer.update()


STYLES = {
    1: {'hunch': 8, 'arms': 'hang', 'sway': 2, 'attack': 'swing',
        'gait': {'stride': 18, 'knee': 28, 'bob': 0.012, 'sway': 2, 'lean': 5, 'swing': 10}},
    2: {'hunch': 6, 'arms': 'hang', 'sway': 1.5, 'attack': 'shoot',
        'gait': {'stride': 16, 'knee': 26, 'bob': 0.01, 'sway': 1.5, 'lean': 4, 'swing': 0}},
}
STYLE = STYLES[1]

# the crossbowman carries the crossbow low in both hands, aims it up to the shoulder to shoot
LOW_R, LOW_L = (-0.12, -0.24, 1.04), (0.08, -0.4, 1.08)


def tweak(g, variant):
    if variant != 2:
        return
    for name in ('idle', 'walk'):
        for s in g['CLIPS'][name]:
            s['ikR'] = LOW_R
            s['ikL'] = LOW_L
    xb = bpy.data.objects['Q2_Ballesta']

    def place(state, D):
        if state.get('clip') == 'death' and state.get('phase', 0) > 0.3:
            M = Matrix.Translation((0.25, -0.75, 0.03)) @ Matrix.Rotation(math.radians(70), 4, 'Z')
            xb.matrix_basis = M
            return
        if state.get('clip') == 'death':
            M = Matrix.Translation((0.15, -0.45, 0.6)) @ Matrix.Rotation(math.radians(-50), 4, 'X')
            xb.matrix_basis = M
            return
        hR = (g['head'](D, 'ha.R') + g['tail'](D, 'ha.R')) * 0.5
        hL = (g['head'](D, 'ha.L') + g['tail'](D, 'ha.L')) * 0.5
        y = (hL - hR)
        if y.length < 1e-4:
            y = Vector((0, -1, 0))
        y.normalize()
        x = y.cross(Vector((0, 0, 1)))
        if x.length < 1e-4:
            x = Vector((1, 0, 0))
        x.normalize()
        z = x.cross(y)
        M = Matrix((x, y, z)).transposed().to_4x4()
        M.translation = hR - y * 0.06
        xb.matrix_basis = M
    g['HOOKS'].append(place)
