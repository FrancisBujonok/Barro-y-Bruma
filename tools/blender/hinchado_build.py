# Barro y Bruma - the bloated (art_src/hinchado/hinchado.blend): a huge bald man in rotten trousers with a sac of
# Bruma grown onto his back like a hump that pulses and glows. In Unity he carries a violet light.
import bpy, bmesh, math, random
from mathutils import Vector, Matrix

HEAD_C = Vector((0.0, -0.03, 1.57))
HEAD_R = (0.09, 0.1, 0.105)
SAC_C = Vector((0.0, 0.27, 1.22))


def mats():
    pm = pal_mat
    pm('H_Piel', ['2a2420', '594a3c', '9c8a70', 'b08a6a'])
    pm('H_Pustula', ['2c2738', '2c2738', '5b5170', '5b5170'])
    pm('H_Saco', ['2c2738', '5b5170', '5b5170', 'c4bfb2'])          # bright: it glows
    pm('H_Vena', ['141210', '2c2738', '2c2738', '5b5170'])
    pm('H_Calzas', ['141210', '2a2420', '3d332b', '594a3c'])
    pm('H_Cuerda', ['2a2420', '594a3c', '7a6650', '9c8a70'])
    pm('H_Ojo', ['6e6c66', '9a968c', 'c4bfb2', 'e6e2d4'])


def to_bone(ob, bone):
    b = ARM.bones[bone]
    ob.parent = RIG
    ob.parent_type = 'BONE'
    ob.parent_bone = bone
    ob.matrix_parent_inverse = Matrix.Identity(4)
    ob.matrix_basis = (b.matrix_local @ Matrix.Translation((0, b.length, 0))).inverted()


def build_body():
    body = bpy.data.objects['Body']
    body.data.materials[0] = mat('H_Piel')
    sk = body.data.skin_vertices[0].data
    fat = {0: 1.75, 1: 2.15, 2: 1.75, 3: 1.4, 4: 1.9, 15: 1.55, 19: 1.55, 16: 1.35, 20: 1.35,
           7: 1.45, 11: 1.45, 8: 1.4, 12: 1.4, 9: 1.3, 13: 1.3, 10: 1.2, 14: 1.2}
    if 'shaped' not in body:
        for i, s in fat.items():
            sk[i].radius = (sk[i].radius[0] * s, sk[i].radius[1] * s * (1.15 if i in (0, 1, 2) else 1.0))
        body['shaped'] = 1
    bpy.data.objects['Tunic'].hide_render = True
    bpy.data.objects['Tunic'].hide_viewport = True
    bpy.data.objects['Boots'].data.materials[0] = mat('H_Calzas')
    for n in ('Eye.L', 'Eye.R'):
        e = bpy.data.objects[n]
        e.data.materials[0] = mat('H_Ojo')
        w = e.matrix_world.copy()
        w.translation.y = -0.125
        w.translation.z = 1.555
        e.matrix_world = w


def build_head(seed=61):
    rnd = random.Random(seed)
    bm = bmesh.new()
    ellipsoid(bm, HEAD_C, HEAD_R, 14, 10)
    ellipsoid(bm, (0, -0.11, 1.535), (0.07, 0.05, 0.05), 10, 7)       # jowls
    ellipsoid(bm, (0, -0.128, 1.55), (0.016, 0.022, 0.02), 8, 6)
    to_bone(new_obj('H_Cabeza', bm, 'H_Piel', parent_rig=True), 'head')
    bm = bmesh.new()
    for i in range(6):
        a = rnd.uniform(-1.2, 1.2)
        ellipsoid(bm, (HEAD_R[0] * 0.95 * math.sin(a), HEAD_C.y - HEAD_R[1] * 0.8 * math.cos(a), HEAD_C.z + rnd.uniform(-0.04, 0.06)),
                  (0.016, 0.016, 0.016), 6, 5)
    to_bone(new_obj('H_PustulasCara', bm, 'H_Pustula', parent_rig=True), 'head')


def build_clothes(seed=62):
    rnd = random.Random(seed)
    # rotten trousers: rigid tubes over thighs and shins, a rope holding them under the belly
    for side in ('R', 'L'):
        for bone, r0, r1 in (('kn.' + side, 0.14, 0.1), ('an.' + side, 0.095, 0.075)):
            b = ARM.bones[bone]
            bm = bmesh.new()
            pts = [Vector((0, b.length * t, 0)) for t in (-0.05, 0.5, 0.95)]
            tube(bm, pts, [r0, (r0 + r1) / 2, r1], 8, cap_tip=False, jag=0.02 if bone.startswith('an') else 0, rnd=rnd)
            new_obj('H_Calza.' + bone, bm, 'H_Calzas', bone)
    bm = bmesh.new()
    pts = [Vector((0.25 * math.sin(2 * math.pi * i / 24), -0.24 * math.cos(2 * math.pi * i / 24) - 0.02, 0.9 - 0.03 * math.cos(2 * math.pi * i / 24))) for i in range(25)]
    tube(bm, pts, [0.014] * 25, 5, cap_tip=False)
    to_bone(new_obj('H_Cuerda', bm, 'H_Cuerda', parent_rig=True), 'pelvis')


def build_sac(seed=63):
    rnd = random.Random(seed)
    bm = bmesh.new()
    ellipsoid(bm, SAC_C, (0.21, 0.18, 0.25), 16, 12)
    for p, r in (((0.14, 0.33, 1.36), (0.08, 0.07, 0.08)), ((-0.12, 0.35, 1.08), (0.09, 0.07, 0.09)),
                 ((0.05, 0.4, 1.0), (0.06, 0.05, 0.06))):
        ellipsoid(bm, p, r, 10, 7)
    ob = new_obj('H_Saco', bm, 'H_Saco', parent_rig=True)
    to_bone(ob, 'chest')
    # dark veins over the sac and fleshy rim where it grows out of his back
    bm = bmesh.new()
    for k in range(7):
        a = rnd.uniform(0, 2 * math.pi)
        pts = []
        for j in range(6):
            t = j / 5
            ang = a + t * rnd.uniform(-0.6, 0.6)
            pts.append(SAC_C + Vector((0.215 * math.cos(ang) * math.sin(0.4 + t * 2.4), 0.185 * math.sin(0.4 + t * 2.4) * 1.0,
                                       0.255 * math.cos(0.4 + t * 2.4))) * 1.0)
        tube(bm, [SAC_C + (p - SAC_C) * 1.02 for p in pts], [0.012] * 6, 4, cap_tip=False)
    to_bone(new_obj('H_Venas', bm, 'H_Vena', parent_rig=True), 'chest')
    bm = bmesh.new()
    pts = [Vector((0.2 * math.sin(2 * math.pi * i / 16), 0.16, 1.22 + 0.22 * math.cos(2 * math.pi * i / 16))) for i in range(17)]
    tube(bm, pts, [0.035] * 17, 6, cap_tip=False)
    to_bone(new_obj('H_Borde', bm, 'H_Piel', parent_rig=True), 'chest')
    # pustules over the shoulders and the belly
    bm = bmesh.new()
    for p in ((0.24, 0.0, 1.38), (-0.22, 0.05, 1.36), (0.12, -0.27, 1.05), (-0.16, -0.25, 0.98), (0.0, -0.3, 1.15)):
        ellipsoid(bm, p, (0.03, 0.03, 0.03), 6, 5)
    to_bone(new_obj('H_Pustulas', bm, 'H_Pustula', parent_rig=True), 'chest')


def build_all():
    RIG.matrix_world = Matrix.Identity(4)
    for pb in RIG.pose.bones:
        pb.matrix_basis = Matrix.Identity(4)
    bpy.context.view_layer.update()
    mats()
    build_body()
    build_head()
    build_clothes()
    build_sac()
    bpy.context.view_layer.update()


STYLE = {'hunch': 8, 'arms': 'wide', 'sway': 4, 'attack': 'slam', 'scale': 1.04,
         'gait': {'stride': 10, 'knee': 14, 'bob': 0.02, 'sway': 6, 'lean': 4, 'swing': 6}}


def tweak(g):
    """The sac swells and shrinks with every frame of every clip."""
    sac = bpy.data.objects['H_Saco']
    base = sac.matrix_basis.copy()
    sac['base'] = [v for row in base for v in row]
    B = Matrix([sac['base'][i * 4:i * 4 + 4] for i in range(4)])
    REST = g['REST']

    def pulse(state, D):
        k = 1.0 + 0.09 * math.sin(2 * math.pi * state.get('phase', 0.0) * (2 if state.get('clip') == 'walk' else 1))
        if state.get('clip') == 'death':
            k = 1.0 + 0.25 * state.get('phase', 0.0)
        bone = ARM.bones['chest']
        piv = SAC_C
        M = Matrix.Translation(piv) @ Matrix.Diagonal((k, k * 1.05, k, 1)) @ Matrix.Translation(-piv)
        P = bone.matrix_local @ Matrix.Translation((0, bone.length, 0))
        sac.matrix_basis = P.inverted() @ M @ P @ B
    g['HOOKS'].append(pulse)
