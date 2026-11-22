# Barro y Bruma - builds the gate guard of Cenizal (art_src/guardia/guardia.blend) on a copy of the hero.
# A village guard in a land of Bruma: plain gear, nothing noble. Kettle helmet with a wide brim and a cloth
# curtain over the nape, a cloth mask over nose and mouth against the Bruma, a sprig of herbs on the helmet,
# short mail collar, ash-grey tabard of Cenizal with a red ember, quilted gambeson and a spear.
# Needs lenador_build.py executed first in the SAME namespace (mesh helpers).
import bpy, bmesh, math, random
from mathutils import Vector, Matrix

HEAD_C = Vector((0.0, -0.01, 1.6))
HEAD_R = (0.096, 0.106, 0.116)
SPEAR_LEN = 2.05


def mats():
    pm = pal_mat
    pm('G_Piel', ['3d332b', '7a6650', 'b08a6a', 'd2ac88'], base='T_Botas')
    pm('G_Gambeson', ['141210', '2a2420', '594a3c', '7a6650'], base='T_Jubon')   # quilted, its stripes show
    pm('G_Calzas', ['141210', '2a2420', '2f3436', '4f5a5c'], base='T_Botas')
    pm('G_Cuero', ['141210', '2a2420', '3d332b', '594a3c'], base='T_Botas')
    pm('G_Malla', ['141210', '2a2420', '3f3e3b', '6e6c66'], base='T_Botas')
    pm('G_Casco', ['141210', '3f3e3b', '6e6c66', '9a968c'], base='T_Botas')
    pm('G_Oxido', ['141210', '3e1a18', '6b2a24', '8f4a36'], base='T_Botas')
    pm('G_Sobreveste', ['2a2420', '6e6c66', '9a968c', 'c4bfb2'], base='T_Botas')
    pm('G_Brasa', ['3e1a18', '6b2a24', 'b24a36', 'b24a36'], base='T_Botas')
    pm('G_Mascara', ['141210', '3e1a18', '6b2a24', '8f4a36'], base='T_Botas')
    pm('G_Tela', ['141210', '2a2420', '594a3c', '7a6650'], base='T_Botas')
    pm('G_Hierba', ['141210', '4e4b34', '6f6a45', '9c8a70'], base='T_Botas')
    pm('G_Madera', ['141210', '2a2420', '594a3c', '7a6650'], base='T_Botas')
    pm('G_Hoja', ['141210', '6e6c66', '9a968c', 'c4bfb2'], base='T_Botas')
    pm('G_Ojo', ['141210', '141210', '2a2420', '3d332b'], base='T_Botas')


def to_bone(ob, bone):
    b = ARM.bones[bone]
    ob.parent = RIG
    ob.parent_type = 'BONE'
    ob.parent_bone = bone
    ob.matrix_parent_inverse = Matrix.Identity(4)
    ob.matrix_basis = (b.matrix_local @ Matrix.Translation((0, b.length, 0))).inverted()


def build_body():
    bpy.data.objects['Body'].data.materials[0] = mat('G_Calzas')
    bpy.data.objects['Tunic'].data.materials[0] = mat('G_Gambeson')
    for n in ('Boots', 'Belt', 'Pouch'):
        o = bpy.data.objects.get(n)
        if o:
            o.data.materials[0] = mat('G_Cuero')
    g = bpy.data.objects.get('Gloves')
    if g:
        g.data.materials[0] = mat('G_Cuero')
    for n in ('Blade_S', 'Grip_S', 'Guard_S', 'Pommel_S'):
        o = bpy.data.objects.get(n)
        if o:
            o.hide_render = False
            o.hide_viewport = False
    for n in ('Eye.L', 'Eye.R'):
        e = bpy.data.objects[n]
        e.data.materials[0] = mat('G_Ojo')
        w = e.matrix_world.copy()
        w.translation.y = -0.118
        e.matrix_world = w
    body = bpy.data.objects['Body']
    sk = body.data.skin_vertices[0].data
    sturdy = {2: 1.06, 3: 1.06, 1: 1.06, 0: 1.04, 4: 1.15}
    if 'shaped' not in body:
        for i, s in sturdy.items():
            sk[i].radius = (sk[i].radius[0] * s, sk[i].radius[1] * s)
        body['shaped'] = 1


def build_head(seed=41):
    rnd = random.Random(seed)
    bm = bmesh.new()
    ellipsoid(bm, HEAD_C, HEAD_R, 14, 10)
    ellipsoid(bm, (0, -0.11, 1.57), (0.015, 0.024, 0.024), 8, 6)
    ob = new_obj('G_Cabeza', bm, 'G_Piel', parent_rig=True)
    to_bone(ob, 'head')
    # cloth mask over nose and mouth, knotted at the back of the head
    bm = bmesh.new()
    m = ellipsoid(bm, HEAD_C + Vector((0, -0.004, -0.004)), (HEAD_R[0] + 0.012, HEAD_R[1] + 0.016, HEAD_R[2] + 0.01), 14, 10)
    drop = [v for v in m if (v.co.z - HEAD_C.z) > -0.036 or (v.co.z - HEAD_C.z) < -0.13]
    bmesh.ops.delete(bm, geom=drop, context='VERTS')
    tail = Vector((0, 0.11, 1.585))
    for k in range(2):
        d = Vector((rnd.uniform(-0.3, 0.3), 1, -1.6)).normalized()
        tube(bm, [tail, tail + d * 0.06, tail + d * 0.12], [0.014, 0.012, 0.006], 4)
    ob = new_obj('G_Mascara', bm, 'G_Mascara', parent_rig=True)
    to_bone(ob, 'head')


def build_helmet(seed=43):
    """Kettle hat: a rounded crown with a riveted band and a wide brim sloping down, a cloth curtain at the
    nape, and a sprig of dried herbs with a red ribbon tucked into the band (against the Bruma)."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    c = HEAD_C + Vector((0, 0.004, 0.02))
    sides = 20
    rings = []
    for k in range(7):                       # crown: from the top down to the band
        ang = math.pi / 2 * k / 6
        ring = []
        for i in range(sides):
            a = 2 * math.pi * i / sides
            r = math.sin(ang)
            ring.append(bm.verts.new((c.x + (HEAD_R[0] + 0.02) * r * math.sin(a),
                                      c.y - (HEAD_R[1] + 0.02) * r * math.cos(a),
                                      c.z + 0.012 + (HEAD_R[2] + 0.005) * math.cos(ang) * 1.05)))
        rings.append(ring)
    for k in range(6):
        for i in range(sides):
            bm.faces.new((rings[k][i], rings[k][(i + 1) % sides], rings[k + 1][(i + 1) % sides], rings[k + 1][i]))
    # brim: outward and slightly down
    band = rings[-1]
    outer = []
    for i in range(sides):
        a = 2 * math.pi * i / sides
        outer.append(bm.verts.new((c.x + 0.2 * math.sin(a), c.y - 0.205 * math.cos(a), c.z + 0.012 - 0.05)))
    for i in range(sides):
        bm.faces.new((band[i], band[(i + 1) % sides], outer[(i + 1) % sides], outer[i]))
    ob = new_obj('G_Casco', bm, 'G_Casco', parent_rig=True)
    to_bone(ob, 'head')
    ob.modifiers.new('Grosor', 'SOLIDIFY').thickness = 0.01
    # rusty band where the brim meets the crown, with rivets
    bm = bmesh.new()
    pts = []
    for i in range(sides + 1):
        a = 2 * math.pi * i / sides
        pts.append(Vector((c.x + (HEAD_R[0] + 0.024) * math.sin(a), c.y - (HEAD_R[1] + 0.024) * math.cos(a), c.z + 0.02)))
    tube(bm, pts, [0.01] * len(pts), 4, cap_tip=False)
    ob = new_obj('G_Banda', bm, 'G_Oxido', parent_rig=True)
    to_bone(ob, 'head')
    # cloth curtain hanging from the back of the helmet over the nape
    bm = bmesh.new()
    cols, rows = 9, 4
    grid = []
    for r in range(rows):
        row = []
        for k in range(cols):
            a = math.pi * (0.55 + 0.9 * k / (cols - 1))
            rad = 0.125 + 0.03 * r
            z = c.z - 0.005 - 0.055 * r + (rnd.uniform(-0.012, 0.01) if r == rows - 1 else 0)
            row.append(bm.verts.new((c.x + rad * math.sin(a), c.y - rad * 1.05 * math.cos(a), z)))
        grid.append(row)
    for r in range(rows - 1):
        for k in range(cols - 1):
            bm.faces.new((grid[r][k], grid[r][k + 1], grid[r + 1][k + 1], grid[r + 1][k]))
    ob = new_obj('G_Cortina', bm, 'G_Tela', parent_rig=True)
    to_bone(ob, 'head')
    ob.modifiers.new('Grosor', 'SOLIDIFY').thickness = 0.008
    # herbs and ribbon on the left side of the band
    bm = bmesh.new()
    root = Vector((0.1, 0.0, c.z + 0.03))
    for j in range(5):
        d = Vector((0.25 + rnd.uniform(-0.15, 0.15), 0.25 + rnd.uniform(-0.2, 0.2), 1.0)).normalized()
        tube(bm, [root, root + d * 0.06, root + d * 0.11], [0.009, 0.008, 0.004], 4)
    ob = new_obj('G_Ramito', bm, 'G_Hierba', parent_rig=True)
    to_bone(ob, 'head')
    bm = bmesh.new()
    p = Vector((0.11, 0.03, c.z + 0.02))
    tube(bm, [p, p + Vector((0.02, 0.03, -0.06)), p + Vector((0.03, 0.05, -0.12))], [0.01, 0.009, 0.005], 4)
    ob = new_obj('G_Cinta', bm, 'G_Brasa', parent_rig=True)
    to_bone(ob, 'head')


def build_armor(seed=45):
    rnd = random.Random(seed)
    # short mail collar (aventail) over the shoulders
    bm = bmesh.new()
    sides, rings = 22, 4
    vs = []
    for k in range(rings):
        t = k / (rings - 1)
        ring = []
        for i in range(sides):
            a = 2 * math.pi * i / sides
            rx = 0.085 + 0.17 * t ** 0.8
            ry = 0.075 + 0.11 * t ** 0.8
            z = 1.47 - 0.16 * t - (rnd.uniform(0, 0.015) if k == rings - 1 else 0)
            ring.append(bm.verts.new((rx * math.sin(a), -ry * math.cos(a) + 0.01, z)))
        vs.append(ring)
    for k in range(rings - 1):
        for i in range(sides):
            bm.faces.new((vs[k][i], vs[k][(i + 1) % sides], vs[k + 1][(i + 1) % sides], vs[k + 1][i]))
    ob = new_obj('G_Malla', bm, 'G_Malla', parent_rig=True)
    to_bone(ob, 'upchest')
    ob.modifiers.new('Grosor', 'SOLIDIFY').thickness = 0.012
    # tabard: front and back panels from the shoulders to mid-thigh, frayed hem
    for name, sy in (('G_SobrevesteF', -1), ('G_SobrevesteB', 1)):
        bm = bmesh.new()
        cols, rows = 5, 7
        grid = []
        for r in range(rows):
            z = 1.36 - 0.72 * r / (rows - 1)
            half = 0.12 + 0.03 * (r / (rows - 1))
            depth = 0.125 if z > 1.0 else 0.125 + (1.0 - z) * 0.08
            row = []
            for k in range(cols):
                x = -half + 2 * half * k / (cols - 1)
                y = sy * (depth - 0.03 * (x / half) ** 2)
                zz = z + (rnd.uniform(-0.02, 0.01) if r == rows - 1 else 0)
                row.append(bm.verts.new((x, y, zz)))
            grid.append(row)
        for r in range(rows - 1):
            for k in range(cols - 1):
                bm.faces.new((grid[r][k], grid[r][k + 1], grid[r + 1][k + 1], grid[r + 1][k]))
        ob = new_obj(name, bm, 'G_Sobreveste', parent_rig=True)
        to_bone(ob, 'chest')
        ob.modifiers.new('Grosor', 'SOLIDIFY').thickness = 0.008
    # the ember of Cenizal on the chest
    bm = bmesh.new()
    ellipsoid(bm, (0, -0.135, 1.2), (0.035, 0.012, 0.045), 8, 5)
    ob = new_obj('G_Brasa', bm, 'G_Brasa', parent_rig=True)
    to_bone(ob, 'chest')
    # leather shoulder pads
    for s in (-1, 1):
        bm = bmesh.new()
        ellipsoid(bm, (0.2 * s, 0.0, 1.39), (0.07, 0.075, 0.035), 10, 6)
        ob = new_obj('G_Hombrera.' + ('L' if s > 0 else 'R'), bm, 'G_Cuero', parent_rig=True)
        to_bone(ob, 'sh.' + ('L' if s > 0 else 'R'))


def build_spear(seed=47):
    """Spear in its own space: Z up from the butt. Ash shaft, leaf blade, a ribbon under the blade."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    n = 10
    tube(bm, [Vector((rnd.uniform(-0.004, 0.004), 0, SPEAR_LEN * k / n)) for k in range(n + 1)],
         [0.018] * (n + 1), 6, cap_tip=False)
    ob = new_obj('G_Lanza', bm, 'G_Madera', parent_rig=True, smooth=False)
    bm = bmesh.new()
    # iron socket and a leaf-shaped blade
    tube(bm, [Vector((0, 0, SPEAR_LEN - 0.02)), Vector((0, 0, SPEAR_LEN + 0.07))], [0.022, 0.016], 6, cap_tip=False)
    prof = [(0.0, 0.07), (0.035, 0.12), (0.03, 0.2), (0.0, 0.3)]
    vs = []
    for x, z in prof:
        vs.append((bm.verts.new((x, 0.006, SPEAR_LEN + z)), bm.verts.new((-x, 0.006, SPEAR_LEN + z)),
                   bm.verts.new((x, -0.006, SPEAR_LEN + z)), bm.verts.new((-x, -0.006, SPEAR_LEN + z))))
    for i in range(len(vs) - 1):
        a, b = vs[i], vs[i + 1]
        bm.faces.new((a[0], b[0], b[1], a[1]))
        bm.faces.new((a[3], b[3], b[2], a[2]))
        bm.faces.new((a[0], a[2], b[2], b[0]))
        bm.faces.new((a[1], b[1], b[3], a[3]))
    blade = new_obj('G_Punta', bm, 'G_Hoja', parent_rig=True, smooth=False)
    blade.parent = ob
    blade.matrix_basis = Matrix.Identity(4)
    bm = bmesh.new()
    p = Vector((0.015, 0, SPEAR_LEN - 0.05))
    tube(bm, [p, p + Vector((0.05, 0.01, -0.06)), p + Vector((0.07, 0.02, -0.16)), p + Vector((0.09, 0.02, -0.24))],
         [0.012, 0.012, 0.01, 0.006], 4)
    rib = new_obj('G_CintaLanza', bm, 'G_Brasa', parent_rig=True)
    rib.parent = ob
    rib.matrix_basis = Matrix.Identity(4)


def build_all():
    RIG.matrix_world = Matrix.Identity(4)
    for pb in RIG.pose.bones:
        pb.matrix_basis = Matrix.Identity(4)
    bpy.context.view_layer.update()
    mats()
    build_body()
    build_head()
    build_helmet()
    build_armor()
    build_spear()
    bpy.context.view_layer.update()
