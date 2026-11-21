# Barro y Bruma - builds the locked-up woodcutter (art_src/lenador/lenador.blend) on top of a copy of the
# hero model: hair, beard, rolled sleeves, bare forearms, shackles, wall with rings and straw.
# Run once in Blender with the rig at rest: exec(open(r"...\tools\blender\lenador_build.py").read())
# Every piece is authored in ARMATURE space with the hero standing; the seated pose comes from lenador_pose.py.
import bpy, bmesh, math, random
from mathutils import Vector, Matrix

RIG = bpy.data.objects['Rig']
ARM = RIG.data
FLOOR = 0.77            # armature z of the floor once he sits (rig 'up' = -FLOOR)
WALL_Y = 0.20           # armature y of the wall face (he faces -Y, the wall is behind him)
pal_mat = bpy.app.driver_namespace['pal_mat']


def mat(n):
    return bpy.data.materials[n]


def kill(name):
    o = bpy.data.objects.get(name)
    if o:
        me = o.data if o.type == 'MESH' else None
        bpy.data.objects.remove(o, do_unlink=True)
        if me and me.users == 0:
            bpy.data.meshes.remove(me)


def new_obj(name, bm, material, parent_bone=None, local=None, parent_rig=False, smooth=True):
    """bm in local coordinates. parent_bone: placed with the bone's rest matrix @ local (Y along the bone)."""
    kill(name)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    if smooth:
        for p in me.polygons:
            p.use_smooth = True
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    if isinstance(material, (list, tuple)):
        for m in material:
            me.materials.append(mat(m))
    else:
        me.materials.append(mat(material))
    if parent_bone:
        b = ARM.bones[parent_bone]
        ob.parent = RIG
        ob.parent_type = 'BONE'
        ob.parent_bone = parent_bone
        # bone parenting puts the child at the bone TAIL, Y along the bone
        tail = b.matrix_local @ Matrix.Translation((0, b.length, 0))
        ob.matrix_parent_inverse = Matrix.Identity(4)
        ob.matrix_basis = tail.inverted() @ b.matrix_local @ (local or Matrix.Identity(4))
    elif parent_rig:
        ob.parent = RIG
        ob.matrix_parent_inverse = Matrix.Identity(4)
        ob.matrix_basis = local or Matrix.Identity(4)
    return ob


def frame_from(t):
    t = t.normalized()
    up = Vector((0, 0, 1)) if abs(t.z) < 0.9 else Vector((1, 0, 0))
    a = t.cross(up).normalized()
    b = t.cross(a).normalized()
    return a, b


def tube(bm, pts, radii, sides=6, cap_tip=True, jag=None, rnd=None, mat_index=0):
    """A tapered tube through pts. jag: random extra length of the last ring per vertex (ragged ends)."""
    rings = []
    for i, p in enumerate(pts):
        t = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)])
        a, b = frame_from(t)
        ring = []
        for k in range(sides):
            ang = 2 * math.pi * k / sides
            off = (a * math.cos(ang) + b * math.sin(ang)) * radii[i]
            q = p + off
            if jag and i == len(pts) - 1:
                q = q + t.normalized() * (rnd.uniform(-jag, jag))
            ring.append(bm.verts.new(q))
        rings.append(ring)
    for i in range(len(rings) - 1):
        for k in range(sides):
            f = bm.faces.new((rings[i][k], rings[i][(k + 1) % sides], rings[i + 1][(k + 1) % sides], rings[i + 1][k]))
            f.material_index = mat_index
    if cap_tip:
        tip = bm.verts.new(pts[-1] + (pts[-1] - pts[-2]).normalized() * radii[-1])
        for k in range(sides):
            f = bm.faces.new((rings[-1][k], rings[-1][(k + 1) % sides], tip))
            f.material_index = mat_index
    root = bm.faces.new(list(reversed(rings[0])))
    root.material_index = mat_index


def ellipsoid(bm, c, r, segs=12, rings=8):
    m = bmesh.ops.create_uvsphere(bm, u_segments=segs, v_segments=rings, radius=1.0)
    for v in m['verts']:
        v.co = Vector((v.co.x * r[0] + c[0], v.co.y * r[1] + c[1], v.co.z * r[2] + c[2]))
    return m['verts']


def ring_mesh(bm, r_in, r_out, h, sides=14):
    """Hollow cylinder along local Y (a cuff)."""
    vs = []
    for (r, y) in ((r_out, -h / 2), (r_out, h / 2), (r_in, h / 2), (r_in, -h / 2)):
        vs.append([bm.verts.new((r * math.cos(2 * math.pi * k / sides), y, r * math.sin(2 * math.pi * k / sides)))
                   for k in range(sides)])
    for j in range(4):
        A, B = vs[j], vs[(j + 1) % 4]
        for k in range(sides):
            bm.faces.new((A[k], A[(k + 1) % sides], B[(k + 1) % sides], B[k]))


def torus(bm, R, r, M, segs=10, sides=5, stretch=1.0):
    vs = []
    for i in range(segs):
        u = 2 * math.pi * i / segs
        ring = []
        for k in range(sides):
            w = 2 * math.pi * k / sides
            x = (R + r * math.cos(w)) * math.cos(u) * stretch
            y = (R + r * math.cos(w)) * math.sin(u)
            z = r * math.sin(w)
            ring.append(bm.verts.new(M @ Vector((x, y, z))))
        vs.append(ring)
    for i in range(segs):
        for k in range(sides):
            bm.faces.new((vs[i][k], vs[(i + 1) % segs][k], vs[(i + 1) % segs][(k + 1) % sides], vs[i][(k + 1) % sides]))


def bone_pt(name, t):
    b = ARM.bones[name]
    return b.head_local.lerp(b.tail_local, t)


# ---------------------------------------------------------------------------------------------- body
def build_body():
    for n in ('Buckle',):
        kill(n)
    bpy.data.objects['Body'].data.materials[0] = mat('L_Calzas')
    bpy.data.objects['Tunic'].data.materials[0] = mat('L_Tunica')
    bpy.data.objects['Belt'].data.materials[0] = mat('L_Trapo')
    bpy.data.objects['Boots'].data.materials[0] = mat('L_Trapo')
    for n in ('Eye.L', 'Eye.R'):
        o = bpy.data.objects[n]
        o.data.materials[0] = mat('L_Ojo')
    # a woodcutter: broader chest and shoulders, thicker arms and neck
    body = bpy.data.objects['Body']
    sk = body.data.skin_vertices[0].data
    grow = {2: 1.12, 3: 1.14, 1: 1.08, 4: 1.25, 7: 1.15, 11: 1.15, 8: 1.12, 12: 1.12, 9: 1.08, 13: 1.08}
    if 'grown' not in body:
        for i, s in grow.items():
            sk[i].radius = (sk[i].radius[0] * s, sk[i].radius[1] * s)
        body['grown'] = 1
    tun = bpy.data.objects['Tunic']
    if 'Push' not in tun.modifiers:
        m = tun.modifiers.new('Push', 'DISPLACE')
        m.strength = 0.016
        m.mid_level = 0.0
        with bpy.context.temp_override(object=tun):
            bpy.ops.object.modifier_move_to_index(modifier='Push', index=0)


# ---------------------------------------------------------------------------------------------- head
HEAD_C = Vector((0.0, -0.01, 1.60))
HEAD_R = (0.098, 0.108, 0.118)


def build_head():
    bm = bmesh.new()
    ellipsoid(bm, HEAD_C, HEAD_R, 14, 10)
    # nose: a little wedge out of the face
    ellipsoid(bm, (0, -0.118, 1.565), (0.018, 0.03, 0.03), 8, 6)
    # brow ridge
    ellipsoid(bm, (0, -0.095, 1.605), (0.07, 0.025, 0.02), 10, 6)
    ob = new_obj('L_Cabeza', bm, 'L_Piel', parent_rig=True)
    # parent to the head bone keeping its rest place
    b = ARM.bones['head']
    tail = b.matrix_local @ Matrix.Translation((0, b.length, 0))
    ob.parent_type = 'BONE'
    ob.parent_bone = 'head'
    ob.matrix_basis = tail.inverted()
    return ob


def scalp_dirs(rnd, n, region):
    out = []
    while len(out) < n:
        u = Vector((rnd.uniform(-1, 1), rnd.uniform(-1, 1), rnd.uniform(-1, 1)))
        if u.length < 0.2 or u.length > 1:
            continue
        u.normalize()
        if region(u):
            out.append(u)
    return out


def strand(rnd, root, d0, length, gravity, wild, segs=5, face_window=True):
    pts = [root]
    d = d0.normalized()
    step = length / segs
    for k in range(segs):
        d = (d + Vector((0, 0, -gravity)) + Vector((rnd.uniform(-wild, wild), rnd.uniform(-wild, wild),
                                                    rnd.uniform(-wild, wild)))).normalized()
        p = pts[-1] + d * step
        # stay outside the skull
        rel = Vector(((p.x - HEAD_C.x) / (HEAD_R[0] + 0.012), (p.y - HEAD_C.y) / (HEAD_R[1] + 0.012),
                      (p.z - HEAD_C.z) / (HEAD_R[2] + 0.012)))
        if rel.length < 1.0:
            rel.normalize()
            p = Vector((HEAD_C.x + rel.x * (HEAD_R[0] + 0.012), HEAD_C.y + rel.y * (HEAD_R[1] + 0.012),
                        HEAD_C.z + rel.z * (HEAD_R[2] + 0.012)))
        # keep the face open: hair that would fall over it is pushed aside, like curtains
        if face_window and p.y < HEAD_C.y - 0.04 and p.z < HEAD_C.z + 0.035 and abs(p.x - HEAD_C.x) < 0.078:
            sx = 1.0 if (p.x - HEAD_C.x) >= 0 else -1.0
            p = Vector((HEAD_C.x + sx * 0.078, p.y + 0.02, p.z))
            d = (d + Vector((sx * 0.4, 0.3, 0))).normalized()
        pts.append(p)
    return pts


def on_head(u, k=0.97):
    return Vector((HEAD_C.x + u.x * HEAD_R[0] * k, HEAD_C.y + u.y * HEAD_R[1] * k, HEAD_C.z + u.z * HEAD_R[2] * k))


def build_hair(seed=7):
    rnd = random.Random(seed)
    bm = bmesh.new()
    # scalp: everything above the ears, the face left open below the brow
    def scalp(u):
        if u.z < -0.25:
            return False
        if u.y < -0.45 and u.z < 0.45:      # face
            return False
        return True
    for u in scalp_dirs(rnd, 95, scalp):
        root = on_head(u, 0.9)
        front = u.y < -0.3
        if front:                            # short matted fringe that stops at the brow
            L = rnd.uniform(0.05, 0.085)
            pts = strand(rnd, root, u + Vector((0, -0.3, -0.5)), L, 0.5, 0.3, 3)
        elif rnd.random() < 0.14:            # a few wild locks sticking out
            L = rnd.uniform(0.07, 0.13)
            pts = strand(rnd, root, u * 1.5 + Vector((0, 0.1, 0.15)), L, 0.1, 0.45, 4)
        else:                                # long, heavy, uneven, down to the shoulders
            L = rnd.uniform(0.2, 0.33) if u.z < 0.6 else rnd.uniform(0.16, 0.26)
            back = 0.45 if u.y < 0 else 0.15
            pts = strand(rnd, root, u + Vector((0, back, -0.25)), L, 0.42, 0.28, 6)
        r0 = rnd.uniform(0.03, 0.04)
        radii = [r0 * (1 - 0.75 * i / (len(pts) - 1)) for i in range(len(pts))]
        mi = 1 if rnd.random() < 0.05 else 0
        tube(bm, pts, radii, 5, mat_index=mi)
    # a cap of hair over the skull so no scalp shows between locks
    cap = ellipsoid(bm, HEAD_C + Vector((0, 0.012, 0.016)), (HEAD_R[0] + 0.016, HEAD_R[1] + 0.012, HEAD_R[2] + 0.004), 12, 8)
    for v in cap:
        rel = v.co - HEAD_C
        if rel.z < -0.01 and rel.y < 0.0 or (rel.y < -0.06 and rel.z < 0.05):
            v.co = HEAD_C + rel * 0.85      # tuck the cap in over the face
    ob = new_obj('L_Pelo', bm, ['L_Pelo', 'L_Canas'], parent_rig=True)
    b = ARM.bones['head']
    ob.parent_type = 'BONE'
    ob.parent_bone = 'head'
    ob.matrix_basis = (b.matrix_local @ Matrix.Translation((0, b.length, 0))).inverted()
    return ob


def build_beard(seed=11):
    rnd = random.Random(seed)
    bm = bmesh.new()
    def jaw(u):
        # below the nose, plus the sides of the jaw up to the ears; the eyes and nose stay clear
        return u.y < -0.1 and u.z < 0.0 and (u.z < -0.42 or abs(u.x) > 0.62)
    for u in scalp_dirs(rnd, 60, jaw):
        root = on_head(u, 0.95)
        chin = max(0.0, -u.y) * max(0.0, -u.z)
        L = rnd.uniform(0.11, 0.17) + 0.12 * chin
        d = Vector((u.x * 0.7, -0.5, -1.0))
        pts = strand(rnd, root, d, L, 0.35, 0.35, 5, face_window=False)
        r0 = rnd.uniform(0.028, 0.036)
        radii = [r0 * (1 - 0.7 * i / (len(pts) - 1)) for i in range(len(pts))]
        mi = 1 if rnd.random() < 0.18 else 0
        tube(bm, pts, radii, 5, mat_index=mi)
    # moustache: two heavy hanks under the nose, drooping over the mouth
    for s in (-1, 1):
        root = Vector((0.015 * s, -0.118, 1.535))
        pts = strand(rnd, root, Vector((0.6 * s, -0.4, -0.5)), 0.08, 0.5, 0.2, 3, face_window=False)
        tube(bm, pts, [0.018, 0.016, 0.012, 0.006], 5)
    # a solid core so the beard is one mass, not sticks
    ellipsoid(bm, (0, -0.1, 1.49), (0.075, 0.05, 0.08), 10, 7)
    pal_mat('L_Barba', ['141210', '2a2420', '594a3c', '7a6650'], base='T_Botas')
    ob = new_obj('L_Barba', bm, ['L_Barba', 'L_Canas'], parent_rig=True)
    b = ARM.bones['head']
    ob.parent_type = 'BONE'
    ob.parent_bone = 'head'
    ob.matrix_basis = (b.matrix_local @ Matrix.Translation((0, b.length, 0))).inverted()
    return ob


# ---------------------------------------------------------------------------------------------- arms
def build_arms(seed=3):
    rnd = random.Random(seed)
    for side in ('R', 'L'):
        el = ARM.bones['el.' + side]
        wr = ARM.bones['wr.' + side]
        ha = ARM.bones['ha.' + side]
        # sleeve over the upper arm, torn past the elbow (the left one worse)
        bm = bmesh.new()
        L = el.length
        end = 1.12 if side == 'R' else 1.05
        pts = [Vector((0, L * t, 0)) for t in (-0.15, 0.2, 0.55, 0.85, end)]
        tube(bm, pts, [0.074, 0.07, 0.066, 0.064, 0.066], 8, cap_tip=False,
             jag=0.035 if side == 'R' else 0.06, rnd=rnd)
        new_obj('L_Manga.' + side, bm, 'L_Tunica', 'el.' + side)
        # bare forearm
        bm = bmesh.new()
        L = wr.length
        pts = [Vector((0, L * t, 0)) for t in (-0.05, 0.35, 0.75, 1.02)]
        tube(bm, pts, [0.06, 0.056, 0.049, 0.045], 8, cap_tip=False)
        new_obj('L_Antebrazo.' + side, bm, 'L_Piel', 'wr.' + side)
        # hand: a mitten and a thumb
        bm = bmesh.new()
        ellipsoid(bm, (0, ha.length * 0.6, 0), (0.03, 0.06, 0.042), 10, 7)
        ellipsoid(bm, (0.028 * (1 if side == 'L' else -1), ha.length * 0.25, -0.02), (0.012, 0.03, 0.013), 6, 5)
        new_obj('L_Mano.' + side, bm, 'L_Piel', 'ha.' + side)
        # shackle on the wrist, with an eye for the chain
        bm = bmesh.new()
        ring_mesh(bm, 0.044, 0.06, 0.05)
        torus(bm, 0.016, 0.006, Matrix.Translation((0, 0, -0.068)) @ Matrix.Rotation(math.pi / 2, 4, 'Y'), 8, 4)
        new_obj('L_Grillete.' + side, bm, 'L_Hierro', 'wr.' + side,
                Matrix.Translation((0, wr.length * 0.86, 0)), smooth=False)


def build_legs():
    for side in ('R', 'L'):
        an = ARM.bones['an.' + side]
        bm = bmesh.new()
        ring_mesh(bm, 0.054, 0.072, 0.055)
        torus(bm, 0.018, 0.0065, Matrix.Translation((0, 0, -0.08)) @ Matrix.Rotation(math.pi / 2, 4, 'Y'), 8, 4)
        new_obj('L_GrilleteP.' + side, bm, 'L_Hierro', 'an.' + side,
                Matrix.Translation((0, an.length * 0.78, 0)), smooth=False)
    # a ragged patch on the left knee
    kn = ARM.bones['kn.L']
    bm = bmesh.new()
    ellipsoid(bm, (0, kn.length, -0.055), (0.055, 0.05, 0.02), 8, 5)
    new_obj('L_Remiendo', bm, 'L_Trapo', 'kn.L')


# ---------------------------------------------------------------------------------------------- wall
RINGS = {'R': Vector((-0.42, WALL_Y - 0.02, FLOOR + 0.95)), 'L': Vector((0.42, WALL_Y - 0.02, FLOOR + 0.95)),
         'P': Vector((0.0, WALL_Y - 0.02, FLOOR + 0.06))}


def build_wall(seed=5):
    rnd = random.Random(seed)
    bm = bmesh.new()
    x = -0.86
    while x < 0.86:
        w = rnd.uniform(0.15, 0.2)
        h = rnd.uniform(1.42, 1.56)
        tilt = rnd.uniform(-0.01, 0.01)
        cx = x + w / 2
        m = bmesh.ops.create_cube(bm, size=1.0)
        for v in m['verts']:
            v.co = Vector((cx + v.co.x * (w - 0.012), WALL_Y + 0.045 + v.co.y * 0.09 + rnd.uniform(-0.004, 0.004),
                           FLOOR + (v.co.z + 0.5) * h + v.co.x * tilt))
        x += w
    # two cross beams
    for z in (0.32, 1.18):
        m = bmesh.ops.create_cube(bm, size=1.0)
        for v in m['verts']:
            v.co = Vector((v.co.x * 1.74, WALL_Y - 0.02 + v.co.y * 0.05, FLOOR + z + v.co.z * 0.11))
    new_obj('L_Pared', bm, 'L_Madera', parent_rig=True, smooth=False)
    # iron rings with their plates
    bm = bmesh.new()
    for k, p in RINGS.items():
        m = bmesh.ops.create_cube(bm, size=1.0)
        for v in m['verts']:
            v.co = Vector((p.x + v.co.x * 0.07, WALL_Y - 0.05 + v.co.y * 0.02, p.z + 0.03 + v.co.z * 0.09))
        torus(bm, 0.035, 0.009, Matrix.Translation(p - Vector((0, 0.02, 0.0))) @ Matrix.Rotation(math.pi / 2, 4, 'Y'), 10, 5)
    new_obj('L_Argollas', bm, 'L_Hierro', parent_rig=True, smooth=False)


def build_straw(seed=9):
    rnd = random.Random(seed)
    bm = bmesh.new()
    # a flattened bed of old straw under and around him
    for i in range(7):
        c = (rnd.uniform(-0.45, 0.45), rnd.uniform(-0.55, 0.12), FLOOR)
        ellipsoid(bm, c, (rnd.uniform(0.16, 0.28), rnd.uniform(0.12, 0.22), rnd.uniform(0.025, 0.045)), 9, 5)
    for i in range(30):
        a = rnd.uniform(0, math.pi * 2)
        c = Vector((rnd.uniform(-0.45, 0.45), rnd.uniform(-0.6, 0.1), FLOOR + 0.02))
        d = Vector((math.cos(a), math.sin(a), rnd.uniform(-0.05, 0.25)))
        L = rnd.uniform(0.1, 0.2)
        tube(bm, [c, c + d * L * 0.5, c + d * L], [0.012, 0.01, 0.007], 4)
    pal_mat('L_Paja', ['141210', '3d332b', '7a6650', '9c8a70'], base='T_Botas')
    new_obj('L_Paja', bm, 'L_Paja', parent_rig=True, smooth=False)


def build_all():
    RIG.matrix_world = Matrix.Identity(4)
    for pb in RIG.pose.bones:
        pb.matrix_basis = Matrix.Identity(4)
    bpy.context.view_layer.update()
    build_body()
    build_head()
    build_hair()
    build_beard()
    build_arms()
    build_legs()
    build_wall()
    build_straw()
    bpy.context.view_layer.update()
