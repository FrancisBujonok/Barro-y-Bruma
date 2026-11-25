# Barro y Bruma - builds the coachman (art_src/cochero/cochero.blend) on a copy of the hero.
# Francis: closer to a gravedigger, a tall top hat, a somewhat ragged suit; a little torn and hurt, as if the
# carriage had crashed a short while ago.
# Design (Claude): tall and gaunt, long pale face with sideburns. Stovepipe hat dented on one side by the crash
# and sitting crooked, with a mourning crepe band whose two tails hang down the back (the gravedigger touch).
# Long faded frock coat with split tails and muddy hem, the right shoulder seam torn open with the shirt showing,
# oxblood waistcoat, dirty high collar and a loose cravat. Pinstripe trousers torn at the right knee. A cut on
# the forehead with blood running down the left cheek and onto the collar. He holds the broken coach whip.
# Needs lenador_build.py executed first in the SAME namespace (mesh helpers): see enemigo_base.setup().
import bpy, bmesh, math, random
from mathutils import Vector, Matrix

HEAD_C = Vector((0.0, -0.01, 1.6))
HEAD_R = (0.086, 0.1, 0.122)          # long, narrow face
HAT_Z0 = 1.668                        # where the hat band sits on the head
HAT_H = 0.27


def mats():
    pm = pal_mat
    pm('C_Piel', ['3d332b', '7a6650', '9c8a70', 'd2ac88'], base='T_Botas')       # ashen
    pm('C_Pelo', ['141210', '2a2420', '3f3e3b', '6e6c66'], base='T_Botas')
    pm('C_Levita', ['141210', '2f3436', '4f5a5c', '6e6c66'], base='T_Botas')      # faded mourning black, gone slate
    pm('C_Galera', ['141210', '141210', '2a2420', '6e6c66'], base='T_Botas')
    pm('C_Crespon', ['141210', '141210', '2a2420', '2a2420'], base='T_Botas')
    pm('C_Chaleco', ['141210', '3e1a18', '6b2a24', '8f4a36'], base='T_Botas')
    pm('C_Camisa', ['2a2420', '6e6c66', '9a968c', 'c4bfb2'], base='T_Botas')
    pm('C_Pantalon', ['141210', '2a2420', '3d332b', '594a3c'], base='T_Jubon')     # the stripes read as pinstripe
    pm('C_Cuero', ['141210', '141210', '2a2420', '3d332b'], base='T_Botas')
    pm('C_Barro', ['141210', '2a2420', '3d332b', '594a3c'], base='T_Botas')
    pm('C_Sangre', ['141210', '3e1a18', '6b2a24', '6b2a24'], base='T_Botas')
    pm('C_Laton', ['3d332b', '594a3c', '9c8a70', 'b08a6a'], base='T_Botas')
    pm('C_Madera', ['141210', '2a2420', '594a3c', '7a6650'], base='T_Botas')
    pm('C_Ojo', ['141210', '141210', '2a2420', '3d332b'], base='T_Botas')


def to_bone(ob, bone):
    b = ARM.bones[bone]
    ob.parent = RIG
    ob.parent_type = 'BONE'
    ob.parent_bone = bone
    ob.matrix_parent_inverse = Matrix.Identity(4)
    ob.matrix_basis = (b.matrix_local @ Matrix.Translation((0, b.length, 0))).inverted()


def build_body():
    bpy.data.objects['Body'].data.materials[0] = mat('C_Pantalon')
    bpy.data.objects['Tunic'].data.materials[0] = mat('C_Levita')
    o = bpy.data.objects.get('Boots')
    if o:
        o.data.materials[0] = mat('C_Cuero')
    for n in ('Eye.L', 'Eye.R'):
        e = bpy.data.objects[n]
        e.data.materials[0] = mat('C_Ojo')
        w = e.matrix_world.copy()
        w.translation.y = -0.108
        e.matrix_world = w
    body = bpy.data.objects['Body']
    sk = body.data.skin_vertices[0].data
    gaunt = {3: 0.93, 2: 0.93, 1: 0.95, 7: 0.92, 11: 0.92, 8: 0.92, 12: 0.92}
    if 'shaped' not in body:
        for i, s in gaunt.items():
            sk[i].radius = (sk[i].radius[0] * s, sk[i].radius[1] * s)
        body['shaped'] = 1


def build_head(seed=61):
    rnd = random.Random(seed)
    bm = bmesh.new()
    ellipsoid(bm, HEAD_C, HEAD_R, 14, 10)
    ellipsoid(bm, (0, -0.1, 1.565), (0.014, 0.03, 0.026), 8, 6)        # long thin nose
    ellipsoid(bm, (0, -0.062, 1.495), (0.036, 0.034, 0.03), 8, 6)      # long chin
    to_bone(new_obj('C_Cabeza', bm, 'C_Piel', parent_rig=True), 'head')
    # thin grey hair at the back and sides (the rest is under the hat), and long sideburns down to the jaw
    bm = bmesh.new()
    def scalp(u):
        return -0.25 < u.z < 0.45 and u.y > -0.35
    for u in scalp_dirs(rnd, 34, scalp):
        root = on_head(u, 0.93)
        pts = strand(rnd, root, u + Vector((0, 0.25, -0.6)), rnd.uniform(0.04, 0.07), 0.45, 0.25, 3)
        tube(bm, pts, [0.02, 0.014, 0.007, 0.004][:len(pts)], 4)
    for s in (-1, 1):
        for k in range(3):
            u = Vector((0.93 * s, -0.12 - 0.08 * k, 0.12)).normalized()
            root = on_head(u, 0.98)
            pts = [root, root + Vector((0.004 * s, -0.01, -0.04)), root + Vector((0.006 * s, -0.018, -0.085))]
            tube(bm, pts, [0.014, 0.013, 0.008], 4)
    to_bone(new_obj('C_Pelo', bm, 'C_Pelo', parent_rig=True), 'head')
    # the cut on the forehead and the blood running down the left cheek
    bm = bmesh.new()
    pts = [Vector((0.02, -0.105, 1.665)), Vector((0.05, -0.098, 1.64)), Vector((0.062, -0.09, 1.6)),
           Vector((0.066, -0.08, 1.555)), Vector((0.062, -0.07, 1.51))]
    tube(bm, pts, [0.012, 0.011, 0.009, 0.008, 0.006], 4)
    to_bone(new_obj('C_Corte', bm, 'C_Sangre', parent_rig=True), 'head')


def _hat_tilt():
    """The crash left the hat crooked: tipped back and to his left."""
    pv = Vector((0, 0, HAT_Z0))
    return (Matrix.Translation(pv) @ Matrix.Rotation(math.radians(-6), 4, 'X')
            @ Matrix.Rotation(math.radians(9), 4, 'Y') @ Matrix.Translation(-pv))


def build_hat(seed=63):
    """Stovepipe top hat, its crown dented in on the right side, curled brim, crepe band with two tails.
    The hat is not bone-parented: cochero_pose.py places it every frame (it follows the head, or the hand when
    he tips it)."""
    rnd = random.Random(seed)
    M = _hat_tilt()
    c = Vector((HEAD_C.x, HEAD_C.y + 0.004, HAT_Z0))
    sides, levels = 20, 7
    bm = bmesh.new()
    rings = []
    for k in range(levels):
        t = k / (levels - 1)
        z = c.z + HAT_H * t
        ring = []
        for i in range(sides):
            a = 2 * math.pi * i / sides
            rx, ry = 0.097 + 0.012 * t, 0.107 + 0.01 * t           # flares a little towards the top
            x, y = rx * math.sin(a), -ry * math.cos(a)
            # dent: pushed in on his right side (-x), upper part, a little towards the front
            dent = max(0.0, -math.sin(a)) ** 2 * max(0.0, (t - 0.3) / 0.7) * (0.6 + 0.4 * max(0.0, math.cos(a)))
            x *= 1 - 0.32 * dent
            y *= 1 - 0.12 * dent
            z2 = z - 0.03 * dent * t
            ring.append(bm.verts.new(M @ Vector((c.x + x, c.y + y, z2))))
        rings.append(ring)
    for k in range(levels - 1):
        for i in range(sides):
            bm.faces.new((rings[k][i], rings[k][(i + 1) % sides], rings[k + 1][(i + 1) % sides], rings[k + 1][i]))
    bm.faces.new(rings[-1])                                       # top
    # brim: narrow, curled up at the sides, a little longer at front and back
    outer = []
    for i in range(sides):
        a = 2 * math.pi * i / sides
        side = abs(math.sin(a))
        outer.append(bm.verts.new(M @ Vector((c.x + 0.148 * math.sin(a), c.y - (0.15 if math.cos(a) > 0 else 0.165) * math.cos(a),
                                                c.z + 0.004 + 0.03 * side ** 2))))
    for i in range(sides):
        bm.faces.new((rings[0][i], rings[0][(i + 1) % sides], outer[(i + 1) % sides], outer[i]))
    ob = new_obj('C_Galera', bm, 'C_Galera', parent_rig=True)
    ob.modifiers.new('Grosor', 'SOLIDIFY').thickness = 0.008
    # mourning crepe band and its two tails down the back
    bm = bmesh.new()
    pts = []
    for i in range(sides + 1):
        a = 2 * math.pi * i / sides
        pts.append(M @ Vector((c.x + 0.101 * math.sin(a), c.y - 0.111 * math.cos(a), c.z + 0.03)))
    tube(bm, pts, [0.02] * len(pts), 4, cap_tip=False)
    for s in (-1, 1):
        p0 = M @ Vector((c.x + 0.03 * s, c.y + 0.11, c.z + 0.03))
        q = [p0]
        for j in range(4):
            q.append(q[-1] + Vector((0.006 * s + rnd.uniform(-0.006, 0.006), 0.016 - 0.006 * j, -0.065)))
        tube(bm, q, [0.018, 0.017, 0.015, 0.013, 0.01], 4)
    band = new_obj('C_Crespon', bm, 'C_Crespon', parent_rig=True)
    band.parent = ob
    band.matrix_basis = Matrix.Identity(4)


def build_coat(seed=65):
    rnd = random.Random(seed)
    # standing coat collar at the back of the neck
    bm = bmesh.new()
    pts = []
    for i in range(13):
        a = math.pi * (0.25 + 1.5 * i / 12)
        pts.append(Vector((0.085 * math.sin(a), -0.075 * math.cos(a) + 0.01, 1.455)))
    tube(bm, pts, [0.03] * len(pts), 5, cap_tip=False)
    to_bone(new_obj('C_Cuello', bm, 'C_Levita', parent_rig=True), 'upchest')
    # lapels: two flaps from the collar down to the waist, folded open over the waistcoat
    for s in (-1, 1):
        bm = bmesh.new()
        v = [bm.verts.new(p) for p in ((0.05 * s, -0.11, 1.43), (0.12 * s, -0.12, 1.38), (0.09 * s, -0.14, 1.12),
                                        (0.035 * s, -0.142, 1.06), (0.02 * s, -0.14, 1.3))]
        bm.faces.new(v if s > 0 else list(reversed(v)))
        ob = new_obj('C_Solapa.' + ('L' if s > 0 else 'R'), bm, 'C_Levita', parent_rig=True, smooth=False)
        to_bone(ob, 'chest')
        ob.modifiers.new('Grosor', 'SOLIDIFY').thickness = 0.012
    # waistcoat in the opening, with three brass buttons
    bm = bmesh.new()
    ellipsoid(bm, (0, -0.118, 1.22), (0.075, 0.032, 0.17), 10, 8)
    to_bone(new_obj('C_Chaleco', bm, 'C_Chaleco', parent_rig=True), 'chest')
    bm = bmesh.new()
    ellipsoid(bm, (0, -0.122, 1.37), (0.05, 0.022, 0.06), 8, 6)        # shirt front above the waistcoat
    to_bone(new_obj('C_Pechera', bm, 'C_Camisa', parent_rig=True), 'chest')
    bm = bmesh.new()
    for z in (1.3, 1.22, 1.14):
        ellipsoid(bm, (0, -0.15, z), (0.011, 0.008, 0.011), 6, 4)
    to_bone(new_obj('C_Botones', bm, 'C_Laton', parent_rig=True), 'chest')
    # dirty high shirt collar, a loose cravat knot with its ends askew, blood on the collar
    bm = bmesh.new()
    pts = []
    for i in range(17):
        a = 2 * math.pi * i / 16
        pts.append(Vector((0.064 * math.sin(a), -0.058 * math.cos(a) - 0.005, 1.47)))
    tube(bm, pts, [0.018] * len(pts), 5, cap_tip=False)
    to_bone(new_obj('C_CuelloCamisa', bm, 'C_Camisa', parent_rig=True), 'neck')
    bm = bmesh.new()
    ellipsoid(bm, (0.01, -0.075, 1.425), (0.03, 0.022, 0.022), 8, 6)
    tube(bm, [Vector((0.012, -0.09, 1.41)), Vector((0.03, -0.11, 1.35)), Vector((0.045, -0.12, 1.3))], [0.018, 0.016, 0.012], 4)
    tube(bm, [Vector((0.0, -0.09, 1.41)), Vector((-0.02, -0.115, 1.37)), Vector((-0.04, -0.125, 1.33))], [0.016, 0.014, 0.01], 4)
    to_bone(new_obj('C_Corbata', bm, 'C_Crespon', parent_rig=True), 'upchest')
    bm = bmesh.new()
    ellipsoid(bm, (0.05, -0.05, 1.462), (0.02, 0.016, 0.016), 6, 5)
    to_bone(new_obj('C_SangreCuello', bm, 'C_Sangre', parent_rig=True), 'neck')
    # coat skirts: two front panels open over the legs, two back tails split in the middle; muddy, frayed hem.
    # Authored in armature rest space; cochero_pose.py hangs them from the waist every frame.
    for name, a0, a1 in (('C_FaldonFR', -0.95, -0.42), ('C_FaldonFL', 0.42, 0.95),
                         ('C_FaldonBR', -0.4, -0.02), ('C_FaldonBL', 0.02, 0.4)):
        back = 'B' in name
        bm = bmesh.new()
        cols, rows = 5, 7
        grid = []
        for r in range(rows):
            t = r / (rows - 1)
            z = 1.04 - 0.56 * t
            row = []
            for k in range(cols):
                u = a0 + (a1 - a0) * k / (cols - 1)
                ang = math.pi * u + (0 if back else math.pi)          # back panels around +Y, front around -Y
                rx = 0.15 + 0.07 * t
                ry = 0.12 + 0.06 * t
                zz = z
                if r == rows - 1:
                    zz += rnd.uniform(-0.035, 0.01)                   # frayed
                    if 'FR' in name and k == 0:
                        zz += 0.08                                    # a torn corner
                row.append(bm.verts.new((rx * math.sin(ang), -ry * math.cos(ang), zz)))
            grid.append(row)
        for r in range(rows - 1):
            for k in range(cols - 1):
                f = bm.faces.new((grid[r][k], grid[r][k + 1], grid[r + 1][k + 1], grid[r + 1][k]))
                f.material_index = 1 if r >= rows - 3 else 0          # mud on the lower part
        ob = new_obj(name, bm, ['C_Levita', 'C_Barro'], parent_rig=True)
        ob.modifiers.new('Grosor', 'SOLIDIFY').thickness = 0.01


def build_arms(seed=67):
    rnd = random.Random(seed)
    for side in ('R', 'L'):
        el = ARM.bones['el.' + side]
        wr = ARM.bones['wr.' + side]
        ha = ARM.bones['ha.' + side]
        bm = bmesh.new()
        L = el.length
        if side == 'R':
            # the right shoulder seam burst in the crash: the sleeve starts lower and the shirt shows
            pts = [Vector((0, L * t, 0)) for t in (0.22, 0.5, 0.8, 1.08)]
            tube(bm, pts, [0.066, 0.064, 0.062, 0.064], 8, cap_tip=False)
            # torn edge hanging from the top of the sleeve
            v = [bm.verts.new(p) for p in ((0.06, L * 0.22, -0.02), (-0.06, L * 0.22, -0.02), (0.0, L * 0.38, -0.075))]
            bm.faces.new(v)
        else:
            pts = [Vector((0, L * t, 0)) for t in (-0.12, 0.3, 0.7, 1.08)]
            tube(bm, pts, [0.07, 0.066, 0.062, 0.064], 8, cap_tip=False)
        new_obj('C_Manga.' + side, bm, 'C_Levita', 'el.' + side)
        bm = bmesh.new()
        L = wr.length
        pts = [Vector((0, L * t, 0)) for t in (-0.05, 0.45, 0.82, 0.95)]
        tube(bm, pts, [0.056, 0.053, 0.05, 0.058], 8, cap_tip=False, jag=0.02, rnd=rnd)   # worn cuffs
        new_obj('C_Antebrazo.' + side, bm, 'C_Levita', 'wr.' + side)
        bm = bmesh.new()
        ellipsoid(bm, (0, ha.length * 0.6, 0), (0.028, 0.06, 0.04), 10, 7)
        ellipsoid(bm, (0.026 * (1 if side == 'L' else -1), ha.length * 0.25, -0.02), (0.011, 0.03, 0.012), 6, 5)
        new_obj('C_Guante.' + side, bm, 'C_Cuero', 'ha.' + side)
    # shirt showing through the torn right shoulder
    bm = bmesh.new()
    ellipsoid(bm, (-0.185, 0.0, 1.365), (0.04, 0.05, 0.034), 8, 6)
    to_bone(new_obj('C_HombroCamisa', bm, 'C_Camisa', parent_rig=True), 'sh.R')


def build_legs():
    # torn right knee: a pale scrape and a flap of trouser cloth
    kn = ARM.bones['kn.R']
    bm = bmesh.new()
    ellipsoid(bm, (0, kn.length * 0.98, -0.06), (0.045, 0.045, 0.016), 8, 5)
    new_obj('C_Rodilla', bm, 'C_Piel', 'kn.R')
    bm = bmesh.new()
    ellipsoid(bm, (0.0, kn.length * 0.99, -0.072), (0.02, 0.022, 0.008), 6, 4)
    new_obj('C_RasponSangre', bm, 'C_Sangre', 'kn.R')


def build_whip(seed=69):
    """The broken coach whip, in its own space: the grip at the origin, the stick along +Z, snapped short with
    a splinter, the lash hanging from the tip. cochero_pose.py puts it in the left hand every frame."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    tube(bm, [Vector((0, 0, -0.07)), Vector((0, 0, 0.1)), Vector((0.004, 0, 0.32)), Vector((0.008, 0, 0.5))],
         [0.016, 0.014, 0.011, 0.009], 5, cap_tip=False)
    tube(bm, [Vector((0.008, 0, 0.5)), Vector((0.016, 0.004, 0.56))], [0.006, 0.002], 4)      # splinter
    ob = new_obj('C_Latigo', bm, 'C_Madera', parent_rig=True, smooth=False)
    bm = bmesh.new()
    p = Vector((0.008, 0, 0.49))
    pts = [p]
    d = Vector((0.2, -0.1, -1.0)).normalized()
    for j in range(7):
        d = (d + Vector((rnd.uniform(-0.15, 0.15), rnd.uniform(-0.15, 0.15), -0.1))).normalized()
        pts.append(pts[-1] + d * 0.075)
    tube(bm, pts, [0.009, 0.008, 0.008, 0.007, 0.006, 0.005, 0.004, 0.003], 4)
    lash = new_obj('C_Tralla', bm, 'C_Cuero', parent_rig=True)
    lash.parent = ob
    lash.matrix_basis = Matrix.Identity(4)


def build_all():
    RIG.matrix_world = Matrix.Identity(4)
    for pb in RIG.pose.bones:
        pb.matrix_basis = Matrix.Identity(4)
    bpy.context.view_layer.update()
    mats()
    build_body()
    build_head()
    build_hat()
    build_coat()
    build_arms()
    build_legs()
    build_whip()
    bpy.context.view_layer.update()
