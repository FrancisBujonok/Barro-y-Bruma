# Barro y Bruma - the fog ambusher (art_src/emboscador/emboscador.blend): a thin, very tall figure, grey like the
# fog, faceless, wrapped in hanging grey strips; long arms that end in iron hooks. Only seen well when it strikes.
import bpy, bmesh, math, random
from mathutils import Vector, Matrix

HEAD_C = Vector((0.0, 0.0, 1.62))
HEAD_R = (0.07, 0.085, 0.12)


def mats():
    pm = pal_mat
    pm('E_Niebla', ['3f3e3b', '6e6c66', '9a968c', 'c4bfb2'])
    pm('E_Jirones', ['2a2420', '3f3e3b', '6e6c66', '9a968c'])
    pm('E_Garfio', ['141210', '2a2420', '3f3e3b', '6e6c66'])
    pm('E_Ojo', ['141210', '141210', '2a2420', '2a2420'])


def to_bone(ob, bone):
    b = ARM.bones[bone]
    ob.parent = RIG
    ob.parent_type = 'BONE'
    ob.parent_bone = bone
    ob.matrix_parent_inverse = Matrix.Identity(4)
    ob.matrix_basis = (b.matrix_local @ Matrix.Translation((0, b.length, 0))).inverted()


def build_body():
    body = bpy.data.objects['Body']
    body.data.materials[0] = mat('E_Niebla')
    sk = body.data.skin_vertices[0].data
    thin = {0: 0.7, 1: 0.6, 2: 0.7, 3: 0.75, 4: 0.7, 7: 0.7, 11: 0.7, 8: 0.6, 12: 0.6, 9: 0.55, 13: 0.55,
            15: 0.65, 19: 0.65, 16: 0.6, 20: 0.6, 17: 0.6, 21: 0.6}
    if 'shaped' not in body:
        for i, s in thin.items():
            sk[i].radius = (sk[i].radius[0] * s, sk[i].radius[1] * s)
        body['shaped'] = 1
    for n in ('Tunic', 'Boots'):
        o = bpy.data.objects[n]
        o.hide_render = o.hide_viewport = True
    for n in ('Eye.L', 'Eye.R'):
        e = bpy.data.objects[n]
        e.hide_render = e.hide_viewport = True


def build_head(seed=81):
    bm = bmesh.new()
    ellipsoid(bm, HEAD_C, HEAD_R, 12, 10)
    to_bone(new_obj('E_Cabeza', bm, 'E_Niebla', parent_rig=True), 'head')
    # two dark hollows instead of eyes
    bm = bmesh.new()
    for s in (-1, 1):
        ellipsoid(bm, (0.03 * s, -0.075, 1.62), (0.016, 0.01, 0.024), 6, 5)
    to_bone(new_obj('E_Ojos', bm, 'E_Ojo', parent_rig=True), 'head')


def build_strips(seed=82):
    """Grey strips hanging from the head, shoulders, arms and hips like torn fog."""
    rnd = random.Random(seed)
    groups = [('head', 10, (0.0, 0.0, 1.66), 0.08, (0.22, 0.4)),
              ('upchest', 16, (0.0, 0.0, 1.36), 0.17, (0.3, 0.55)),
              ('pelvis', 12, (0.0, 0.0, 0.95), 0.12, (0.25, 0.45))]
    for bone, n, c, rad, lens in groups:
        bm = bmesh.new()
        for i in range(n):
            a = 2 * math.pi * i / n + rnd.uniform(-0.2, 0.2)
            root = Vector((c[0] + rad * math.sin(a), c[1] - rad * 0.8 * math.cos(a), c[2] + rnd.uniform(-0.03, 0.03)))
            L = rnd.uniform(*lens)
            d = Vector((math.sin(a) * 0.25, -math.cos(a) * 0.2, -1.0)).normalized()
            pts = [root + d * L * t + Vector((rnd.uniform(-0.02, 0.02), rnd.uniform(-0.02, 0.02), 0)) * t for t in (0, 0.33, 0.66, 1.0)]
            tube(bm, pts, [0.03, 0.028, 0.02, 0.008], 4)
        to_bone(new_obj('E_Jirones_' + bone, bm, 'E_Jirones', parent_rig=True), bone)
    for side in ('R', 'L'):
        el = ARM.bones['el.' + side]
        bm = bmesh.new()
        for i in range(4):
            t = 0.2 + 0.2 * i
            root = Vector((0.0, el.length * t, 0.0))
            pts = [root, root + Vector((rnd.uniform(-0.02, 0.02), 0.05, -0.12)), root + Vector((0, 0.08, -0.22))]
            tube(bm, pts, [0.022, 0.016, 0.006], 4)
        new_obj('E_JironesBrazo.' + side, bm, 'E_Jirones', 'el.' + side)


def build_hooks():
    """Three long curved hooks from each hand, curling forward at the tips."""
    for side in ('R', 'L'):
        ha = ARM.bones['ha.' + side]
        bm = bmesh.new()
        for k, x in enumerate((-0.025, 0.0, 0.025)):
            pts = []
            for j in range(7):
                t = j / 6
                pts.append(Vector((x * (1 + t), ha.length * 0.6 + 0.32 * t, -0.12 * t * t)))
            tube(bm, pts, [0.012, 0.011, 0.01, 0.009, 0.007, 0.005, 0.003], 4)
        new_obj('E_Garfios.' + side, bm, 'E_Garfio', 'ha.' + side)


def build_all():
    RIG.matrix_world = Matrix.Identity(4)
    for pb in RIG.pose.bones:
        pb.matrix_basis = Matrix.Identity(4)
    bpy.context.view_layer.update()
    mats()
    build_body()
    build_head()
    build_strips()
    build_hooks()
    bpy.context.view_layer.update()


STYLE = {'hunch': 24, 'arms': 'long', 'sway': 1, 'attack': 'hook', 'scale': 1.28, 'sx': 1.0, 'sz': 1.04,
         'gait': {'stride': 22, 'knee': 32, 'bob': 0.016, 'sway': 1, 'lean': 8, 'swing': 4}}


def tweak(g):
    pass
