# Barro y Bruma - the infected: villagers still dressed for the trade they had, with the Bruma's violet climbing
# their necks. Three variants on one body (art_src/infectado/infectado.blend):
#   1 herrero  - bald with a dark fringe, bare thick arms, leather apron over a sweat-stained shirt
#   2 aldeana  - headscarf, bodice and long skirt, a linen apron
#   3 campesino - shaggy hair, open ragged shirt, rope belt, trousers torn at the knee, barefoot
# Run in the namespace returned by enemigo_base.setup (it has the lenador_build helpers).
import bpy, bmesh, math, random
from mathutils import Vector, Matrix

HEAD_C = Vector((0.0, -0.01, 1.6))
HEAD_R = (0.094, 0.104, 0.114)
VARIANTS = {1: 'herrero', 2: 'aldeana', 3: 'campesino'}


def mats():
    pm = pal_mat
    pm('I_Piel', ['2a2420', '594a3c', '9c8a70', 'b08a6a'])          # pallid, greyish
    pm('I_Bruma', ['2c2738', '2c2738', '5b5170', '5b5170'])
    pm('I_Ojo', ['6e6c66', '9a968c', 'c4bfb2', 'e6e2d4'])           # milky
    pm('I_PeloOsc', ['141210', '141210', '2a2420', '3d332b'])
    pm('I_PeloPaja', ['141210', '3d332b', '594a3c', '7a6650'])
    pm('I_Camisa', ['2a2420', '594a3c', '7a6650', '9c8a70'])
    pm('I_CamisaGris', ['141210', '3f3e3b', '6e6c66', '9a968c'])
    pm('I_Calzas', ['141210', '2a2420', '3d332b', '594a3c'])
    pm('I_Cuero', ['141210', '2a2420', '3d332b', '594a3c'])
    pm('I_Mandil', ['141210', '2a2420', '594a3c', '7a6650'])
    pm('I_Panuelo', ['141210', '3e1a18', '6b2a24', '8f4a36'])
    pm('I_Falda', ['141210', '2f3436', '4f5a5c', '7d8584'])
    pm('I_Delantal', ['2a2420', '6e6c66', '9a968c', 'c4bfb2'])
    pm('I_Soga', ['2a2420', '594a3c', '7a6650', '9c8a70'])


def to_bone(ob, bone):
    b = ARM.bones[bone]
    ob.parent = RIG
    ob.parent_type = 'BONE'
    ob.parent_bone = bone
    ob.matrix_parent_inverse = Matrix.Identity(4)
    ob.matrix_basis = (b.matrix_local @ Matrix.Translation((0, b.length, 0))).inverted()


def tag(ob, variant):
    ob['variant'] = variant          # 0 = every variant
    return ob


def build_common(seed=51):
    rnd = random.Random(seed)
    bpy.data.objects['Body'].data.materials[0] = mat('I_Calzas')
    for n in ('Eye.L', 'Eye.R'):
        e = bpy.data.objects[n]
        e.data.materials[0] = mat('I_Ojo')
        w = e.matrix_world.copy()
        w.translation.y = -0.116
        e.matrix_world = w
    bm = bmesh.new()
    ellipsoid(bm, HEAD_C, HEAD_R, 14, 10)
    ellipsoid(bm, (0, -0.108, 1.566), (0.015, 0.024, 0.024), 8, 6)
    ellipsoid(bm, (0, -0.07, 1.51), (0.042, 0.035, 0.03), 8, 6)
    to_bone(tag(new_obj('I_Cabeza', bm, 'I_Piel', parent_rig=True), 0), 'head')
    # Bruma: blotches up the neck, onto a cheek and the jaw, and up the forearms
    bm = bmesh.new()
    for p, r in (((0.03, -0.07, 1.47), (0.04, 0.03, 0.05)), ((-0.04, -0.06, 1.49), (0.03, 0.03, 0.04)),
                 ((0.06, -0.075, 1.54), (0.022, 0.03, 0.03)), ((0.05, 0.02, 1.46), (0.04, 0.04, 0.04)),
                 ((-0.07, -0.06, 1.57), (0.016, 0.03, 0.02))):
        ellipsoid(bm, p, r, 8, 6)
    to_bone(tag(new_obj('I_BrumaCuello', bm, 'I_Bruma', parent_rig=True), 0), 'head')
    for side in ('R', 'L'):
        wr = ARM.bones['wr.' + side]
        ha = ARM.bones['ha.' + side]
        bm = bmesh.new()
        L = wr.length
        pts = [Vector((0, L * t, 0)) for t in (-0.05, 0.4, 0.8, 1.02)]
        tube(bm, pts, [0.06, 0.056, 0.049, 0.045], 8, cap_tip=False)
        tag(new_obj('I_Antebrazo.' + side, bm, 'I_Piel', 'wr.' + side), 0)
        bm = bmesh.new()
        ellipsoid(bm, (0, ha.length * 0.62, 0), (0.03, 0.062, 0.04), 10, 7)
        ellipsoid(bm, (0.026 * (1 if side == 'L' else -1), ha.length * 0.25, -0.02), (0.011, 0.03, 0.012), 6, 5)
        tag(new_obj('I_Mano.' + side, bm, 'I_Piel', 'ha.' + side), 0)
        bm = bmesh.new()
        ellipsoid(bm, (0.0, L * 0.55, -0.05), (0.04, 0.07, 0.012), 6, 4)
        tag(new_obj('I_BrumaBrazo.' + side, bm, 'I_Bruma', 'wr.' + side), 0)


def build_herrero(seed=52):
    rnd = random.Random(seed)
    # bald crown with a dark fringe round the back of the head
    bm = bmesh.new()
    for i in range(30):
        a = math.pi * (0.25 + 1.5 * i / 29)
        root = Vector((HEAD_C.x + HEAD_R[0] * 0.98 * math.sin(a), HEAD_C.y - HEAD_R[1] * 0.98 * math.cos(a), HEAD_C.z + 0.0))
        d = Vector((math.sin(a) * 0.4, -math.cos(a) * 0.4, -1))
        tube(bm, [root, root + d.normalized() * 0.04, root + d.normalized() * 0.07], [0.022, 0.018, 0.008], 5)
    to_bone(tag(new_obj('I1_Pelo', bm, 'I_PeloOsc', parent_rig=True), 1), 'head')
    # short sleeves of the shirt and a heavy leather apron from chest to knee
    for side in ('R', 'L'):
        el = ARM.bones['el.' + side]
        bm = bmesh.new()
        pts = [Vector((0, el.length * t, 0)) for t in (-0.1, 0.3, 0.55)]
        tube(bm, pts, [0.07, 0.066, 0.066], 8, cap_tip=False, jag=0.015, rnd=rnd)
        tag(new_obj('I1_Manga.' + side, bm, 'I_CamisaGris', 'el.' + side), 1)
    bm = bmesh.new()
    cols, rows = 5, 8
    grid = []
    for r in range(rows):
        z = 1.36 - 0.86 * r / (rows - 1)
        half = 0.13 + 0.05 * r / (rows - 1)
        depth = 0.13 + (0.03 if z < 1.0 else 0.0)
        grid.append([bm.verts.new((-half + 2 * half * k / (cols - 1), -(depth - 0.025 * ((k - 2) / 2) ** 2), z))
                     for k in range(cols)])
    for r in range(rows - 1):
        for k in range(cols - 1):
            bm.faces.new((grid[r][k], grid[r][k + 1], grid[r + 1][k + 1], grid[r + 1][k]))
    ob = tag(new_obj('I1_Mandil', bm, 'I_Cuero', parent_rig=True), 1)
    to_bone(ob, 'chest')
    ob.modifiers.new('Grosor', 'SOLIDIFY').thickness = 0.01


def build_aldeana(seed=53):
    rnd = random.Random(seed)
    # headscarf tied under the chin, the knot hanging behind
    bm = bmesh.new()
    m = ellipsoid(bm, HEAD_C + Vector((0, 0.008, 0.012)), (HEAD_R[0] + 0.016, HEAD_R[1] + 0.014, HEAD_R[2] + 0.012), 14, 10)
    drop = [v for v in m if (v.co.y - HEAD_C.y) < -0.07 and (v.co.z - HEAD_C.z) < 0.06]
    bmesh.ops.delete(bm, geom=drop, context='VERTS')
    p = Vector((0, 0.1, 1.56))
    tube(bm, [p, p + Vector((0.01, 0.03, -0.06)), p + Vector((0.02, 0.04, -0.12))], [0.03, 0.026, 0.012], 5)
    to_bone(tag(new_obj('I2_Panuelo', bm, 'I_Panuelo', parent_rig=True), 2), 'head')
    # long skirt and apron, rigid on the pelvis (the shuffle is short)
    bm = bmesh.new()
    sides, rings = 16, 6
    vs = []
    for k in range(rings):
        z = 1.0 - 0.93 * k / (rings - 1)
        t = (1.0 - z) / 0.93
        rx, ry = 0.16 + 0.09 * t, 0.14 + 0.08 * t
        ring = [bm.verts.new((rx * math.sin(2 * math.pi * i / sides) * (1 + 0.05 * math.sin(i * 3.0) * t),
                              -ry * math.cos(2 * math.pi * i / sides),
                              z + (rnd.uniform(-0.03, 0.02) if k == rings - 1 else 0))) for i in range(sides)]
        vs.append(ring)
    for k in range(rings - 1):
        for i in range(sides):
            bm.faces.new((vs[k][i], vs[k][(i + 1) % sides], vs[k + 1][(i + 1) % sides], vs[k + 1][i]))
    to_bone(tag(new_obj('I2_Falda', bm, 'I_Falda', parent_rig=True), 2), 'pelvis')
    bm = bmesh.new()
    grid = []
    for r in range(5):
        z = 0.98 - 0.5 * r / 4
        t = (1.0 - z) / 0.93
        ry = 0.14 + 0.08 * t
        grid.append([bm.verts.new((-0.12 + 0.24 * k / 4, -ry - 0.012, z)) for k in range(5)])
    for r in range(4):
        for k in range(4):
            bm.faces.new((grid[r][k], grid[r][k + 1], grid[r + 1][k + 1], grid[r + 1][k]))
    to_bone(tag(new_obj('I2_Delantal', bm, 'I_Delantal', parent_rig=True), 2), 'pelvis')
    for side in ('R', 'L'):
        el = ARM.bones['el.' + side]
        bm = bmesh.new()
        pts = [Vector((0, el.length * t, 0)) for t in (-0.1, 0.4, 0.9, 1.05)]
        tube(bm, pts, [0.068, 0.064, 0.062, 0.064], 8, cap_tip=False, jag=0.012, rnd=rnd)
        tag(new_obj('I2_Manga.' + side, bm, 'I_Falda', 'el.' + side), 2)


def build_campesino(seed=54):
    rnd = random.Random(seed)
    bm = bmesh.new()
    def scalp(u):
        return u.z > -0.15 and not (u.y < -0.5 and u.z < 0.4)
    for u in scalp_dirs(rnd, 70, scalp):
        root = on_head(u, 0.92)
        pts = strand(rnd, root, u + Vector((0, 0.2, -0.4)), rnd.uniform(0.07, 0.14), 0.45, 0.45, 4)
        tube(bm, pts, [rnd.uniform(0.026, 0.032) * (1 - 0.75 * i / (len(pts) - 1)) for i in range(len(pts))], 5)
    cap = ellipsoid(bm, HEAD_C + Vector((0, 0.008, 0.014)), (HEAD_R[0] + 0.012, HEAD_R[1] + 0.01, HEAD_R[2] + 0.004), 12, 8)
    for v in cap:
        rel = v.co - HEAD_C
        if (rel.z < -0.01 and rel.y < 0.0) or (rel.y < -0.055 and rel.z < 0.055):
            v.co = HEAD_C + rel * 0.85
    to_bone(tag(new_obj('I3_Pelo', bm, 'I_PeloPaja', parent_rig=True), 3), 'head')
    # ragged shirt sleeves to the elbow and a rope belt
    for side in ('R', 'L'):
        el = ARM.bones['el.' + side]
        bm = bmesh.new()
        pts = [Vector((0, el.length * t, 0)) for t in (-0.1, 0.4, 0.9, 1.08)]
        tube(bm, pts, [0.07, 0.066, 0.064, 0.068], 8, cap_tip=False, jag=0.03, rnd=rnd)
        tag(new_obj('I3_Manga.' + side, bm, 'I_Camisa', 'el.' + side), 3)
    bm = bmesh.new()
    pts = [Vector((0.165 * math.sin(2 * math.pi * i / 24), -0.14 * math.cos(2 * math.pi * i / 24), 1.0)) for i in range(25)]
    tube(bm, pts, [0.012] * 25, 5, cap_tip=False)
    to_bone(tag(new_obj('I3_Soga', bm, 'I_Soga', parent_rig=True), 3), 'pelvis')
    # bare feet: skin over the feet instead of boots
    for side in ('R', 'L'):
        to = ARM.bones['to.' + side]
        bm = bmesh.new()
        ellipsoid(bm, (0, to.length * 0.4, 0), (0.045, 0.09, 0.035), 8, 6)
        tag(new_obj('I3_Pie.' + side, bm, 'I_Piel', 'to.' + side), 3)


def show_variant(v):
    """Hides every piece of the other variants and recolours the shared ones."""
    for o in bpy.data.objects:
        if 'variant' in o:
            on = o['variant'] in (0, v)
            o.hide_render = not on
            o.hide_viewport = not on
    tun = bpy.data.objects['Tunic']
    boots = bpy.data.objects['Boots']
    tun.data.materials[0] = mat({1: 'I_CamisaGris', 2: 'I_Falda', 3: 'I_Camisa'}[v])
    boots.hide_render = boots.hide_viewport = (v == 3)
    boots.data.materials[0] = mat('I_Cuero')


def build_all():
    RIG.matrix_world = Matrix.Identity(4)
    for pb in RIG.pose.bones:
        pb.matrix_basis = Matrix.Identity(4)
    bpy.context.view_layer.update()
    mats()
    build_common()
    build_herrero()
    build_aldeana()
    build_campesino()
    show_variant(1)
    bpy.context.view_layer.update()


STYLE = {'hunch': 16, 'arms': 'forward', 'twitch': 1.0, 'sway': 3, 'attack': 'grab',
         'gait': {'stride': 14, 'knee': 18, 'bob': 0.01, 'limp': 0.35, 'sway': 3, 'lean': 6}}
