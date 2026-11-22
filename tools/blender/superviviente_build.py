# Barro y Bruma - builds the survivor of the tower (art_src/superviviente/superviviente.blend): a young
# explorer, a cousin of the hero's look without his hood and red cloak. Built on a copy of the hero.
# Needs lenador_build.py executed first in the SAME namespace (mesh helpers):
#   ns = {}; exec(lenador_build, ns); exec(this, ns); ns['build_all']()
import bpy, bmesh, math, random
from mathutils import Vector, Matrix

HEAD_C = Vector((0.0, -0.008, 1.6))
HEAD_R = (0.094, 0.104, 0.114)


def mats():
    pm = pal_mat
    pm('S_Piel', ['3d332b', '8f4a36', 'b08a6a', 'd2ac88'], base='T_Botas')
    pm('S_Pelo', ['141210', '3e1a18', '6b2a24', '8f4a36'], base='T_Botas')
    pm('S_Jubon', ['141210', '2a2420', '4e4b34', '6f6a45'], base='T_Botas')
    pm('S_Camisa', ['2a2420', '6e6c66', '9a968c', 'c4bfb2'], base='T_Botas')
    pm('S_Calzas', ['141210', '2a2420', '3f3e3b', '6e6c66'], base='T_Botas')
    pm('S_Capa', ['141210', '2f3436', '4f5a5c', '7d8584'], base='T_Botas')
    pm('S_Panuelo', ['141210', '3e1a18', '8f4a36', 'b24a36'], base='T_Botas')
    pm('S_Venda', ['6e6c66', '9a968c', 'c4bfb2', 'e6e2d4'], base='T_Botas')
    pm('S_Sangre', ['141210', '3e1a18', '6b2a24', '6b2a24'], base='T_Botas')
    pm('S_Cuero', ['141210', '2a2420', '3d332b', '594a3c'], base='T_Botas')
    pm('S_Manta', ['2a2420', '594a3c', '7a6650', '9c8a70'], base='T_Botas')
    pm('S_Ojo', ['141210', '141210', '2a2420', '3d332b'], base='T_Botas')


def to_bone(ob, bone):
    b = ARM.bones[bone]
    ob.parent = RIG
    ob.parent_type = 'BONE'
    ob.parent_bone = bone
    ob.matrix_parent_inverse = Matrix.Identity(4)
    ob.matrix_basis = (b.matrix_local @ Matrix.Translation((0, b.length, 0))).inverted()


def build_body():
    bpy.data.objects['Body'].data.materials[0] = mat('S_Calzas')
    bpy.data.objects['Tunic'].data.materials[0] = mat('S_Jubon')
    for n in ('Eye.L', 'Eye.R'):
        e = bpy.data.objects[n]
        e.data.materials[0] = mat('S_Ojo')
        w = e.matrix_world.copy()
        w.translation.y = -0.116
        e.matrix_world = w
    # the sheathed sword stays visible on his hip
    for n in ('Blade_S', 'Grip_S', 'Guard_S', 'Pommel_S'):
        o = bpy.data.objects.get(n)
        if o:
            o.hide_render = False
            o.hide_viewport = False
    body = bpy.data.objects['Body']
    sk = body.data.skin_vertices[0].data
    slim = {3: 0.95, 2: 0.95, 7: 0.94, 11: 0.94, 8: 0.94, 12: 0.94}       # a bit lankier than the hero
    if 'shaped' not in body:
        for i, s in slim.items():
            sk[i].radius = (sk[i].radius[0] * s, sk[i].radius[1] * s)
        body['shaped'] = 1


def build_head():
    bm = bmesh.new()
    ellipsoid(bm, HEAD_C, HEAD_R, 14, 10)
    ellipsoid(bm, (0, -0.108, 1.567), (0.014, 0.022, 0.022), 8, 6)      # straight young nose
    ellipsoid(bm, (0, -0.07, 1.51), (0.04, 0.035, 0.03), 8, 6)          # jaw
    ob = new_obj('S_Cabeza', bm, 'S_Piel', parent_rig=True)
    to_bone(ob, 'head')


def build_hair(seed=31):
    """Short, messy auburn hair, a lock falling over the forehead. Cut by himself, with a knife."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    def scalp(u):
        if u.z < -0.15:
            return False
        if u.y < -0.5 and u.z < 0.42:
            return False
        return True
    for u in scalp_dirs(rnd, 80, scalp):
        root = on_head(u, 0.92)
        if u.y < -0.3:
            pts = strand(rnd, root, u + Vector((0.2 * (1 if u.x > 0 else -1), -0.5, -0.4)), rnd.uniform(0.06, 0.1), 0.4, 0.4, 3)
        elif u.z < 0.25:                     # sides and nape: short, over the ears
            pts = strand(rnd, root, u + Vector((0, 0.2, -0.5)), rnd.uniform(0.06, 0.1), 0.45, 0.35, 3)
        else:                                # top: tousled, sticking up and back a little
            pts = strand(rnd, root, u + Vector((0, 0.45, -0.1)), rnd.uniform(0.05, 0.09), 0.3, 0.3, 3)
        r0 = rnd.uniform(0.026, 0.034)
        tube(bm, pts, [r0 * (1 - 0.75 * i / (len(pts) - 1)) for i in range(len(pts))], 5)
    cap = ellipsoid(bm, HEAD_C + Vector((0, 0.008, 0.014)), (HEAD_R[0] + 0.012, HEAD_R[1] + 0.01, HEAD_R[2] + 0.004), 12, 8)
    for v in cap:
        rel = v.co - HEAD_C
        if (rel.z < -0.01 and rel.y < 0.0) or (rel.y < -0.055 and rel.z < 0.055):
            v.co = HEAD_C + rel * 0.85
    ob = new_obj('S_Pelo', bm, 'S_Pelo', parent_rig=True)
    to_bone(ob, 'head')
    # bandage round the head, a dark stain over the left temple
    bm = bmesh.new()
    pts = []
    for i in range(23):
        a = 2 * math.pi * i / 22
        pts.append(Vector((HEAD_C.x + (HEAD_R[0] + 0.014) * math.sin(a), HEAD_C.y - (HEAD_R[1] + 0.012) * math.cos(a),
                           HEAD_C.z + 0.035 + 0.02 * math.cos(a))))
    tube(bm, pts, [0.016] * len(pts), 5, cap_tip=False)
    ob = new_obj('S_VendaCabeza', bm, 'S_Venda', parent_rig=True)
    to_bone(ob, 'head')
    bm = bmesh.new()
    ellipsoid(bm, (0.075, -0.07, 1.64), (0.016, 0.02, 0.016), 6, 5)
    ob = new_obj('S_Mancha', bm, 'S_Sangre', parent_rig=True)
    to_bone(ob, 'head')


def build_arms(seed=33):
    rnd = random.Random(seed)
    for side in ('R', 'L'):
        el = ARM.bones['el.' + side]
        wr = ARM.bones['wr.' + side]
        ha = ARM.bones['ha.' + side]
        bm = bmesh.new()
        L = el.length
        pts = [Vector((0, L * t, 0)) for t in (-0.1, 0.3, 0.7, 1.0, 1.1)]
        tube(bm, pts, [0.064, 0.06, 0.058, 0.062, 0.066], 8, cap_tip=False, jag=0.012, rnd=rnd)  # rolled up
        new_obj('S_Manga.' + side, bm, 'S_Camisa', 'el.' + side)
        bm = bmesh.new()
        L = wr.length
        pts = [Vector((0, L * t, 0)) for t in (-0.05, 0.4, 0.8, 1.02)]
        tube(bm, pts, [0.05, 0.047, 0.041, 0.038], 8, cap_tip=False)
        new_obj('S_Antebrazo.' + side, bm, 'S_Piel', 'wr.' + side)
        bm = bmesh.new()
        ellipsoid(bm, (0, ha.length * 0.6, 0), (0.028, 0.058, 0.04), 10, 7)
        ellipsoid(bm, (0.026 * (1 if side == 'L' else -1), ha.length * 0.25, -0.02), (0.011, 0.03, 0.012), 6, 5)
        new_obj('S_Mano.' + side, bm, 'S_Piel', 'ha.' + side)
        # leather bracer on the right forearm; the left one is wrapped in a bandage (the wound)
        bm = bmesh.new()
        if side == 'R':
            pts = [Vector((0, L * t, 0)) for t in (0.45, 0.7, 0.92)]
            tube(bm, pts, [0.05, 0.048, 0.045], 8, cap_tip=False)
            new_obj('S_Brazal', bm, 'S_Cuero', 'wr.R')
        else:
            pts = [Vector((0, L * t, 0)) for t in (0.2, 0.45, 0.7, 0.9)]
            tube(bm, pts, [0.054, 0.053, 0.05, 0.045], 8, cap_tip=False)
            new_obj('S_VendaBrazo', bm, 'S_Venda', 'wr.L')
            bm = bmesh.new()
            ellipsoid(bm, (0.0, L * 0.5, -0.05), (0.03, 0.04, 0.008), 6, 4)
            new_obj('S_ManchaBrazo', bm, 'S_Sangre', 'wr.L')


def build_gear(seed=35):
    rnd = random.Random(seed)
    # red neckerchief: a roll round the neck and a point down the chest
    bm = bmesh.new()
    pts = []
    for i in range(21):
        a = 2 * math.pi * i / 20
        pts.append(Vector((0.075 * math.sin(a), -0.065 * math.cos(a) + 0.005, 1.425 - 0.012 * math.cos(a))))
    tube(bm, pts, [0.022] * len(pts), 6, cap_tip=False)
    v0 = bm.verts.new((-0.05, -0.085, 1.42))
    v1 = bm.verts.new((0.05, -0.085, 1.42))
    v2 = bm.verts.new((0.01, -0.13, 1.31))
    bm.faces.new((v0, v1, v2))
    ob = new_obj('S_Panuelo', bm, 'S_Panuelo', parent_rig=True, smooth=False)
    to_bone(ob, 'upchest')
    # short travelling cape over the left shoulder only, ragged hem
    bm = bmesh.new()
    sides, rings = 14, 5
    vs = []
    for k in range(rings):
        t = k / (rings - 1)
        ring = []
        for i in range(sides + 1):
            a = 0.32 * math.pi + 0.85 * math.pi * i / sides        # over the left shoulder and down the back
            rx = 0.1 + 0.16 * t ** 0.8
            ry = 0.085 + 0.13 * t ** 0.8
            z = 1.44 - (0.3 + 0.08 * math.sin(a)) * t
            if k == rings - 1:
                z += rnd.uniform(-0.03, 0.02)
            ring.append(bm.verts.new((rx * math.sin(a) + 0.04 * t, -ry * math.cos(a) + 0.02, z)))
        vs.append(ring)
    for k in range(rings - 1):
        for i in range(sides):
            bm.faces.new((vs[k][i], vs[k][i + 1], vs[k + 1][i + 1], vs[k + 1][i]))
    ob = new_obj('S_Capa', bm, 'S_Capa', parent_rig=True)
    to_bone(ob, 'upchest')
    ob.modifiers.new('Grosor', 'SOLIDIFY').thickness = 0.012
    # backpack with a rolled blanket on top and two straps
    bm = bmesh.new()
    m = bmesh.ops.create_cube(bm, size=1.0)
    for v in m['verts']:
        v.co = Vector((v.co.x * 0.26, 0.17 + v.co.y * 0.12, 1.2 + v.co.z * 0.3))
    for side in (-1, 1):
        pts = [Vector((0.09 * side, 0.11, 1.33)), Vector((0.1 * side, 0.0, 1.43)), Vector((0.11 * side, -0.1, 1.36)),
               Vector((0.12 * side, -0.12, 1.22)), Vector((0.12 * side, 0.02, 1.08))]
        tube(bm, pts, [0.012] * 5, 4, cap_tip=False)
    ob = new_obj('S_Mochila', bm, 'S_Cuero', parent_rig=True, smooth=False)
    to_bone(ob, 'chest')
    bm = bmesh.new()
    pts = [Vector((x, 0.17, 1.39)) for x in (-0.17, -0.06, 0.06, 0.17)]
    tube(bm, pts, [0.055] * 4, 10, cap_tip=False)
    ob = new_obj('S_Manta', bm, 'S_Manta', parent_rig=True)
    to_bone(ob, 'chest')


def build_all():
    RIG.matrix_world = Matrix.Identity(4)
    for pb in RIG.pose.bones:
        pb.matrix_basis = Matrix.Identity(4)
    bpy.context.view_layer.update()
    mats()
    build_body()
    build_head()
    build_hair()
    build_arms()
    build_gear()
    bpy.context.view_layer.update()
