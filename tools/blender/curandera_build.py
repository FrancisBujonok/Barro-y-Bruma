# Barro y Bruma - builds the old healer of the swamp (art_src/curandera/curandera.blend) on a copy of the hero.
# Needs lenador_build.py executed first in the SAME namespace (it reuses its mesh helpers):
#   ns = {}; exec(lenador_build, ns); exec(this, ns); ns['build_all']()
# Authored in armature space with the rig at rest; poses in curandera_pose.py.
import bpy, bmesh, math, random
from mathutils import Vector, Matrix

HEAD_C = Vector((0.0, -0.012, 1.595))      # used by strand() and on_head() of the shared helpers
HEAD_R = (0.088, 0.098, 0.108)
STAFF_LEN = 1.42
HOOK = Vector((0.13, 0.0, STAFF_LEN + 0.02))   # end of the crook, in staff space (Z up, crook towards +X)


def mats():
    pm = pal_mat
    pm('C_Piel', ['3d332b', '7a6650', '9c8a70', 'd2ac88'], base='T_Botas')
    pm('C_Pelo', ['2a2420', '6e6c66', '9a968c', 'c4bfb2'], base='T_Botas')
    pm('C_PeloLuz', ['3f3e3b', '9a968c', 'c4bfb2', 'e6e2d4'], base='T_Botas')
    pm('C_Vestido', ['141210', '2a2420', '3d332b', '594a3c'], base='T_Botas')
    pm('C_Chal', ['141210', '2f3436', '4f5a5c', '7d8584'], base='T_Botas')
    pm('C_Delantal', ['2a2420', '6e6c66', '9a968c', 'c4bfb2'], base='T_Botas')
    pm('C_Cuerda', ['2a2420', '594a3c', '7a6650', '9c8a70'], base='T_Botas')
    pm('C_Cuero', ['141210', '2a2420', '3d332b', '594a3c'], base='T_Botas')
    pm('C_Hierba', ['141210', '4e4b34', '6f6a45', '9c8a70'], base='T_Botas')
    pm('C_Hueso', ['594a3c', '9a968c', 'c4bfb2', 'e6e2d4'], base='T_Botas')
    pm('C_Piedra', ['141210', '2f3436', '4f5a5c', '7d8584'], base='T_Botas')
    pm('C_Madera', ['141210', '2a2420', '594a3c', '7a6650'], base='T_Botas')
    pm('C_Hierro', ['141210', '3f3e3b', '6e6c66', '9a968c'], base='T_Botas')
    pm('C_Luz', ['b24a36', 'd2ac88', 'e6e2d4', 'e6e2d4'], base='T_Botas')
    pm('C_Vidrio', ['141210', '4e4b34', '6f6a45', '9c8a70'], base='T_Botas')
    pm('C_Ojo', ['6e6c66', '9a968c', 'c4bfb2', 'e6e2d4'], base='T_Botas')


def to_bone(ob, bone):
    b = ARM.bones[bone]
    ob.parent = RIG
    ob.parent_type = 'BONE'
    ob.parent_bone = bone
    ob.matrix_parent_inverse = Matrix.Identity(4)
    ob.matrix_basis = (b.matrix_local @ Matrix.Translation((0, b.length, 0))).inverted()


# ---------------------------------------------------------------------------------------------- body
def build_body():
    for n in ('Buckle', 'Belt'):
        kill(n)
    bpy.data.objects['Body'].data.materials[0] = mat('C_Vestido')
    bpy.data.objects['Tunic'].data.materials[0] = mat('C_Vestido')
    bpy.data.objects['Boots'].data.materials[0] = mat('C_Cuero')
    body = bpy.data.objects['Body']
    sk = body.data.skin_vertices[0].data
    # slight old woman: narrow shoulders, thin arms, softer hips, thin neck
    shrink = {3: 0.86, 2: 0.9, 7: 0.85, 11: 0.85, 8: 0.82, 12: 0.82, 9: 0.82, 13: 0.82, 10: 0.8, 14: 0.8,
              4: 0.85, 0: 1.08, 15: 1.05, 19: 1.05}
    if 'shaped' not in body:
        for i, s in shrink.items():
            sk[i].radius = (sk[i].radius[0] * s, sk[i].radius[1] * s)
        body['shaped'] = 1
    for n in ('Eye.L', 'Eye.R'):
        e = bpy.data.objects[n]
        e.data.materials[0] = mat('C_Ojo')
        w = e.matrix_world.copy()
        w.translation.y = -0.108
        w.translation.z = 1.585
        e.matrix_world = w


def build_head():
    bm = bmesh.new()
    ellipsoid(bm, HEAD_C, HEAD_R, 14, 10)
    ellipsoid(bm, (0, -0.1, 1.582), (0.013, 0.024, 0.028), 8, 6)       # long nose, bridge
    ellipsoid(bm, (0, -0.114, 1.556), (0.015, 0.02, 0.017), 8, 6)      # and its drooping tip
    ellipsoid(bm, (0, -0.074, 1.505), (0.028, 0.03, 0.024), 8, 6)      # small pointed chin
    ob = new_obj('C_Cabeza', bm, 'C_Piel', parent_rig=True)
    to_bone(ob, 'head')


def build_hair(seed=21):
    rnd = random.Random(seed)
    bm = bmesh.new()
    def scalp(u):
        if u.z < -0.3:
            return False
        if u.y < -0.5 and u.z < 0.5:
            return False
        return True
    for u in scalp_dirs(rnd, 90, scalp):
        root = on_head(u, 0.9)
        if u.y < -0.35:                      # parted in the middle, swept to the sides of the face
            side = 1.0 if u.x >= 0 else -1.0
            pts = strand(rnd, root, Vector((side * 0.8, -0.25, -0.5)), rnd.uniform(0.3, 0.44), 0.55, 0.15, 7)
        elif rnd.random() < 0.2:             # frizzy wisps out to the sides, never up: a wild silhouette
            pts = strand(rnd, root, Vector((u.x * 1.6, u.y + 0.15, -0.15)), rnd.uniform(0.12, 0.2), 0.3, 0.4, 4)
        else:                                # long and loose, down the back
            d0 = Vector((u.x, u.y, min(u.z, 0.15))) + Vector((0, 0.35, -0.45))
            pts = strand(rnd, root, d0, rnd.uniform(0.34, 0.52), 0.6, 0.15, 7)
        r0 = rnd.uniform(0.028, 0.036)
        radii = [r0 * (1 - 0.7 * i / (len(pts) - 1)) for i in range(len(pts))]
        tube(bm, pts, radii, 5, mat_index=1 if rnd.random() < 0.2 else 0)
    cap = ellipsoid(bm, HEAD_C + Vector((0, 0.01, 0.014)), (HEAD_R[0] + 0.014, HEAD_R[1] + 0.01, HEAD_R[2] + 0.004), 12, 8)
    for v in cap:
        rel = v.co - HEAD_C
        if (rel.z < -0.01 and rel.y < 0.0) or (rel.y < -0.055 and rel.z < 0.05):
            v.co = HEAD_C + rel * 0.85
    # two thin braids from the temples over the shoulders, tied with a bead and a bone
    beads = []
    for s in (-1, 1):
        p = Vector((0.075 * s, -0.05, 1.56))
        d = Vector((0.25 * s, -0.25, -1.0)).normalized()
        for k in range(9):
            ellipsoid(bm, p, (0.017, 0.017, 0.022), 6, 5)
            p = p + d * 0.032
            d = (d + Vector((0, -0.05, -0.15))).normalized()
        beads.append(p.copy())
    ob = new_obj('C_Pelo', bm, ['C_Pelo', 'C_PeloLuz'], parent_rig=True)
    to_bone(ob, 'head')
    bm = bmesh.new()
    for p in beads:
        ellipsoid(bm, p + Vector((0, 0, -0.012)), (0.016, 0.016, 0.016), 6, 5)
        ellipsoid(bm, p + Vector((0, 0, -0.045)), (0.008, 0.008, 0.024), 5, 4)
    ob = new_obj('C_Cuentas', bm, 'C_Hueso', parent_rig=True)
    to_bone(ob, 'head')


# ---------------------------------------------------------------------------------------------- clothes
def skirt_r(z):
    """Half sizes of the skirt at height z (armature, standing)."""
    t = max(0.0, min(1.0, (1.0 - z) / 0.95))
    return 0.155 + 0.115 * t ** 0.8, 0.135 + 0.105 * t ** 0.8


def build_skirt(seed=4):
    rnd = random.Random(seed)
    bm = bmesh.new()
    sides, rings = 18, 7
    zs = [1.0 - 0.95 * k / (rings - 1) for k in range(rings)]
    vs = []
    for k, z in enumerate(zs):
        rx, ry = skirt_r(z)
        ring = []
        for i in range(sides):
            a = 2 * math.pi * i / sides
            fold = 1 + 0.07 * math.sin(a * 5 + k * 0.7) * (k / (rings - 1))
            zz = z + (rnd.uniform(-0.035, 0.03) if k == rings - 1 else 0)
            ring.append(bm.verts.new((rx * fold * math.sin(a), -ry * fold * math.cos(a), zz)))
        vs.append(ring)
    for k in range(rings - 1):
        for i in range(sides):
            bm.faces.new((vs[k][i], vs[k][(i + 1) % sides], vs[k + 1][(i + 1) % sides], vs[k + 1][i]))
    ob = new_obj('C_Falda', bm, 'C_Vestido', parent_rig=True)
    to_bone(ob, 'pelvis')
    # apron over the front, ragged at the bottom
    bm = bmesh.new()
    cols, rows = 6, 6
    grid = []
    for r in range(rows):
        z = 0.97 - 0.52 * r / (rows - 1)
        rx, ry = skirt_r(z)
        half = 0.13 + 0.04 * r / (rows - 1)
        row = []
        for c in range(cols):
            x = -half + 2 * half * c / (cols - 1)
            a = math.asin(max(-0.95, min(0.95, x / (rx * 1.05))))
            y = -ry * math.cos(a) - 0.014
            zz = z + (rnd.uniform(-0.03, 0.01) if r == rows - 1 else 0)
            row.append(bm.verts.new((x, y, zz)))
        grid.append(row)
    for r in range(rows - 1):
        for c in range(cols - 1):
            bm.faces.new((grid[r][c], grid[r][c + 1], grid[r + 1][c + 1], grid[r + 1][c]))
    ob = new_obj('C_Delantal', bm, 'C_Delantal', parent_rig=True)
    to_bone(ob, 'pelvis')
    # rope belt with herb bundles on the left hip and a leather pouch on the right
    bm = bmesh.new()
    pts = []
    for i in range(25):
        a = 2 * math.pi * i / 24
        pts.append(Vector((0.17 * math.sin(a), -0.15 * math.cos(a) - 0.002, 1.0 - 0.012 * math.cos(a))))
    tube(bm, pts, [0.012] * len(pts), 5, cap_tip=False)
    ob = new_obj('C_Cinto', bm, 'C_Cuerda', parent_rig=True)
    to_bone(ob, 'pelvis')
    bm = bmesh.new()
    for k, (x, y) in enumerate(((0.17, -0.05), (0.16, 0.03), (0.175, -0.12))):
        top = Vector((x, y, 0.99))
        for j in range(5):
            off = Vector((rnd.uniform(-0.02, 0.02), rnd.uniform(-0.02, 0.02), 0))
            tube(bm, [top + off * 0.3, top + off + Vector((0.01, 0, -0.07)), top + off * 1.6 + Vector((0.02, 0, -0.14))],
                 [0.01, 0.014, 0.006], 4)
    ob = new_obj('C_Hierbas', bm, 'C_Hierba', parent_rig=True)
    to_bone(ob, 'pelvis')
    bm = bmesh.new()
    ellipsoid(bm, (-0.175, -0.07, 0.92), (0.035, 0.03, 0.05), 8, 6)
    ob = new_obj('C_Bolsa', bm, 'C_Cuero', parent_rig=True)
    to_bone(ob, 'pelvis')


def build_shawl(seed=6):
    """Wool shawl over the shoulders: open in front, a long point down the back, ragged hem."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    sides, rings = 24, 6
    vs = []
    for k in range(rings):
        t = k / (rings - 1)
        ring = []
        for i in range(sides + 1):
            a = -math.pi * 0.86 + 2 * math.pi * 0.86 * i / sides     # leaves the front open
            back = max(0.0, math.cos(a - math.pi))                    # 1 at the back
            front = max(0.0, math.cos(a))
            rx = 0.08 + 0.185 * t ** 0.7
            ry = 0.07 + (0.17 - 0.03 * back) * t ** 0.7
            z = 1.445 - (0.2 + 0.13 * back ** 3 - 0.05 * front ** 2) * t
            if k == rings - 1:
                z += rnd.uniform(-0.025, 0.02)
            ring.append(bm.verts.new((rx * math.sin(a), -ry * math.cos(a) + 0.01, z)))
        vs.append(ring)
    for k in range(rings - 1):
        for i in range(sides):
            bm.faces.new((vs[k][i], vs[k][i + 1], vs[k + 1][i + 1], vs[k + 1][i]))
    ob = new_obj('C_Chal', bm, 'C_Chal', parent_rig=True)
    to_bone(ob, 'upchest')
    m = ob.modifiers.new('Grosor', 'SOLIDIFY')
    m.thickness = 0.012
    # necklace of dark stones with one small bone in the middle
    bm = bmesh.new()
    bm2 = bmesh.new()
    for j in range(11):
        t = -1 + 2 * j / 10
        p = (0.075 * t, -0.075 - 0.045 * (1 - t * t), 1.425 - 0.1 * (1 - t * t))
        if j == 5:
            ellipsoid(bm2, (p[0], p[1] - 0.005, p[2] - 0.02), (0.012, 0.01, 0.026), 6, 5)
        else:
            ellipsoid(bm, p, (0.011, 0.011, 0.011), 6, 4)
    ob = new_obj('C_Collar', bm, 'C_Piedra', parent_rig=True)
    to_bone(ob, 'upchest')
    ob = new_obj('C_Colgante', bm2, 'C_Hueso', parent_rig=True)
    to_bone(ob, 'upchest')


def build_arms(seed=8):
    rnd = random.Random(seed)
    for side in ('R', 'L'):
        wr = ARM.bones['wr.' + side]
        ha = ARM.bones['ha.' + side]
        bm = bmesh.new()
        L = wr.length
        pts = [Vector((0, L * t, 0)) for t in (0.3, 0.6, 0.85, 1.0)]
        tube(bm, pts, [0.045, 0.055, 0.068, 0.078], 9, cap_tip=False, jag=0.02, rnd=rnd)
        new_obj('C_Puno.' + side, bm, 'C_Vestido', 'wr.' + side)
        bm = bmesh.new()
        ellipsoid(bm, (0, ha.length * 0.55, 0), (0.025, 0.055, 0.036), 10, 7)
        ellipsoid(bm, (0.024 * (1 if side == 'L' else -1), ha.length * 0.2, -0.018), (0.01, 0.028, 0.011), 6, 5)
        new_obj('C_Mano.' + side, bm, 'C_Piel', 'ha.' + side)


# ---------------------------------------------------------------------------------------------- props
def build_staff(seed=12):
    """Gnarled staff in its own space (Z up from the ground), with a crook, herbs and a hanging lantern."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    pts, radii = [], []
    n = 12
    for k in range(n + 1):
        z = STAFF_LEN * k / n
        pts.append(Vector((rnd.uniform(-0.012, 0.012), rnd.uniform(-0.012, 0.012), z)))
        radii.append(0.027 - 0.006 * k / n)
    for k in range(1, 6):                    # the crook bends over towards +X
        a = math.pi * k / 5
        pts.append(Vector((0.065 - 0.065 * math.cos(a), 0, STAFF_LEN + 0.065 * math.sin(a))))
        radii.append(0.015 - 0.0012 * k)
    tube(bm, pts, radii, 6, cap_tip=True)
    for z in (0.35, 0.8, 1.15):
        ellipsoid(bm, (rnd.uniform(-0.01, 0.01), 0, z), (0.027, 0.027, 0.03), 6, 5)
    ob = new_obj('C_Baston', bm, 'C_Madera', parent_rig=True, smooth=False)
    # herbs and a feather tied under the crook
    bm = bmesh.new()
    top = Vector((0.0, 0.0, STAFF_LEN - 0.04))
    for j in range(6):
        off = Vector((rnd.uniform(-0.03, 0.03), rnd.uniform(-0.03, 0.03), 0))
        tube(bm, [top, top + off + Vector((0, 0, -0.08)), top + off * 1.5 + Vector((0, 0, -0.16))], [0.01, 0.012, 0.005], 4)
    ob2 = new_obj('C_BastonHierbas', bm, 'C_Hierba', parent_rig=True)
    ob2.parent = ob
    ob2.matrix_basis = Matrix.Identity(4)
    # lantern, separate so it can swing: origin at its hanging ring
    bm = bmesh.new()
    torus(bm, 0.012, 0.004, Matrix.Rotation(math.pi / 2, 4, 'X'), 8, 4)
    m = bmesh.ops.create_cone(bm, cap_ends=True, segments=6, radius1=0.045, radius2=0.012, depth=0.04)
    for v in m['verts']:
        v.co.z -= 0.035
    for zc in (-0.06, -0.15):
        m = bmesh.ops.create_cube(bm, size=1.0)
        for v in m['verts']:
            v.co = Vector((v.co.x * 0.075, v.co.y * 0.075, zc + v.co.z * 0.018))
    for sx, sy in ((1, 1), (1, -1), (-1, 1), (-1, -1)):
        m = bmesh.ops.create_cube(bm, size=1.0)
        for v in m['verts']:
            v.co = Vector((sx * 0.03 + v.co.x * 0.012, sy * 0.03 + v.co.y * 0.012, -0.105 + v.co.z * 0.09))
    for v in bm.verts:
        v.co = v.co * 1.5
    lan = new_obj('C_Farol', bm, 'C_Hierro', parent_rig=True, smooth=False)
    bm = bmesh.new()
    ellipsoid(bm, (0, 0, -0.158), (0.05, 0.05, 0.062), 8, 6)
    glow = new_obj('C_FarolLuz', bm, 'C_Luz', parent_rig=True)
    glow.parent = lan
    glow.matrix_basis = Matrix.Identity(4)
    # remedy vial for the left hand (only shown when she offers it)
    bm = bmesh.new()
    ellipsoid(bm, (0, 0.05, -0.02), (0.018, 0.018, 0.026), 8, 6)
    m = bmesh.ops.create_cone(bm, cap_ends=True, segments=6, radius1=0.008, radius2=0.008, depth=0.025)
    for v in m['verts']:
        v.co = v.co + Vector((0, 0.05, 0.013))
    vial = new_obj('C_Frasco', bm, 'C_Vidrio', 'ha.L')
    vial.hide_render = True


def build_all():
    RIG.matrix_world = Matrix.Identity(4)
    for pb in RIG.pose.bones:
        pb.matrix_basis = Matrix.Identity(4)
    bpy.context.view_layer.update()
    mats()
    build_body()
    build_head()
    build_hair()
    build_skirt()
    build_shawl()
    build_arms()
    build_staff()
    bpy.context.view_layer.update()
