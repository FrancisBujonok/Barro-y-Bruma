# Barro y Bruma - render, pixelize and zone maps for the Blender hero. Needs hero_pose.py loaded first
# in the same namespace (apply, RIG, ...). Run inside Blender.
import bpy, json, os, math
import numpy as np
from mathutils import Vector

ROOT = r"C:\Users\franc\Documents\PixelGame\BrumaPx"
SRC = os.path.join(ROOT, r"art_src\heroe_blender")
RENDER = os.path.join(SRC, "render8")

PAL_HEX = ["141210", "2a2420", "3d332b", "594a3c", "7a6650", "9c8a70", "3f3e3b", "6e6c66", "9a968c", "c4bfb2",
           "2f3436", "4f5a5c", "7d8584", "3e1a18", "6b2a24", "8f4a36", "b08a6a", "d2ac88", "e6e2d4", "2c2738",
           "5b5170", "4e4b34", "6f6a45", "b24a36"]
PAL = np.array([[int(h[i:i + 2], 16) for i in (0, 2, 4)] for h in PAL_HEX], dtype=np.int32)

ZONE_IDS = ["cabeza", "torso", "hombro.d", "hombro.i", "brazo.d", "brazo.i", "antebrazo.d", "antebrazo.i",
            "muslo.d", "muslo.i", "rodilla.d", "rodilla.i", "pantorrilla.d", "pantorrilla.i"]
NO_POS = ['Blade', 'Grip', 'Guard', 'Pommel', 'Blade_S', 'Grip_S', 'Guard_S', 'Pommel_S', 'Scabbard', 'Chape', 'Smear']


# ---- images ----------------------------------------------------------------------------------------

def px_load(path):
    img = bpy.data.images.load(path, check_existing=False)
    img.colorspace_settings.name = 'Non-Color'
    w, h = img.size
    a = np.empty(w * h * 4, dtype=np.float32)
    img.pixels.foreach_get(a)
    bpy.data.images.remove(img)
    return a.reshape(h, w, 4)[::-1].copy()


def px_save(arr, path):
    """arr: HxWx4 uint8 or float 0..1, top row first."""
    a = np.asarray(arr)
    if a.dtype != np.float32:
        a = a.astype(np.float32) / 255.0
    h, w = a.shape[:2]
    img = bpy.data.images.new("px_tmp", w, h, alpha=True)
    img.colorspace_settings.name = 'Non-Color'
    img.alpha_mode = 'STRAIGHT'
    img.pixels.foreach_set(a[::-1].reshape(-1))
    img.filepath_raw = path
    img.file_format = 'PNG'
    img.save()
    bpy.data.images.remove(img)


def pixelize(a, f=4):
    """a: HxWx4 float render. Returns (rgba uint8 at 1/f with outline, body mask before the outline)."""
    H, W = a.shape[0] // f, a.shape[1] // f
    rgb = np.round(np.clip(a[..., :3], 0, 1) * 255).astype(np.int32)
    alpha = a[..., 3] >= 0.5
    key = (rgb[..., 0] << 16) | (rgb[..., 1] << 8) | rgb[..., 2]
    u, inv = np.unique(key, return_inverse=True)
    uc = np.stack([u >> 16, (u >> 8) & 255, u & 255], -1)
    near = ((uc[:, None, :] - PAL[None]) ** 2).sum(-1).argmin(-1)
    idx = near[inv.reshape(key.shape)]
    idx[~alpha] = -1
    blk = idx[:H * f, :W * f].reshape(H, f, W, f).transpose(0, 2, 1, 3).reshape(H, W, f * f)
    counts = (blk[..., None] == np.arange(len(PAL))).sum(2)
    solid = (blk >= 0).sum(2) * 2 >= f * f
    out = np.where(solid, counts.argmax(-1), -1)
    # clean isolated pixels: a pixel whose 4 neighbours hold 3 or more of one other colour takes it
    pad = np.pad(out, 1, constant_values=-1)
    nb = np.stack([pad[:-2, 1:-1], pad[2:, 1:-1], pad[1:-1, :-2], pad[1:-1, 2:]], -1)
    res = out.copy()
    for c in range(len(PAL)):
        m = ((nb == c).sum(-1) >= 3) & (out >= 0) & (out != c)
        res[m] = c
    body = res >= 0
    pad = np.pad(body, 1)
    ring = (pad[:-2, 1:-1] | pad[2:, 1:-1] | pad[1:-1, :-2] | pad[1:-1, 2:]) & ~body
    img = np.zeros((H, W, 4), np.uint8)
    img[body, :3] = PAL[res[body]]
    img[body, 3] = 255
    img[ring, :3] = PAL[0]
    img[ring, 3] = 255
    return img, body


# ---- zones -----------------------------------------------------------------------------------------

def _zone_of(bone, t):
    base, side = (bone.split('.') + [''])[:2]
    s = {'R': '.d', 'L': '.i'}.get(side, '')
    if base in ('neck', 'head', 'crown'):
        return 'cabeza'
    if base in ('waist', 'chest', 'upchest', 'pelvis', 'hip'):
        return 'torso'
    if base == 'sh':
        return 'hombro' + s
    if base == 'el':
        return 'brazo' + s
    if base == 'wr':
        return 'antebrazo' + s
    if base == 'kn':
        return ('rodilla' if t > 0.85 else 'muslo') + s
    if base == 'an':
        return ('rodilla' if t < 0.12 else 'pantorrilla') + s
    return None


def _band(bone, t, zone):
    base = bone.split('.')[0]
    if base in ('el', 'wr'):
        return 0.25 <= t <= 0.75
    if base == 'kn':
        return 0.25 <= t <= 0.75 or zone.startswith('rodilla')
    if base == 'an':
        return 0.3 <= t <= 0.8 or zone.startswith('rodilla')
    if base == 'sh':
        return True
    if base == 'crown':
        return t < 0.45
    if base == 'chest':
        return 0.2 <= t <= 0.85
    return False


def zones_np(pos, bones, f=4):
    """pos: HxWx4 float position pass (world*0.2+0.5, alpha = covered). bones: {name: [head, tail]} world."""
    H, W = pos.shape[0] // f, pos.shape[1] // f
    names = [n for n in bones if _zone_of(n, 0.5) is not None or n.split('.')[0] in ('kn', 'an')]
    heads = np.array([bones[n][0] for n in names], np.float32)
    tails = np.array([bones[n][1] for n in names], np.float32)
    P = (pos[..., :3] - 0.5) / 0.2
    cov = pos[..., 3] > 0.5
    v = tails - heads
    ll = (v ** 2).sum(-1)
    rel = P[:, :, None, :] - heads[None, None]
    t = np.clip((rel * v[None, None]).sum(-1) / np.maximum(ll, 1e-6), 0, 1)
    closest = heads[None, None] + t[..., None] * v[None, None]
    dist = ((P[:, :, None, :] - closest) ** 2).sum(-1)
    k = dist.argmin(-1)
    tk = np.take_along_axis(t, k[..., None], -1)[..., 0]
    zone = np.zeros(k.shape, np.int32)
    band = np.zeros(k.shape, np.float32)
    order = np.zeros(k.shape, np.float32)
    lengths = np.sqrt(ll)
    seeds = {}
    for i, n in enumerate(names):
        z = _zone_of(n, 0.5)
        if z is None:
            continue
    for i, n in enumerate(names):
        m = (k == i) & cov
        if not m.any():
            continue
        tt = tk[m]
        zs = [_zone_of(n, x) for x in tt]
        zi = np.array([ZONE_IDS.index(z) + 1 if z else 0 for z in zs], np.int32)
        bd = np.array([1.0 if (z and _band(n, x, z)) else 0.0 for z, x in zip(zs, tt)], np.float32)
        base = n.split('.')[0]
        if base in ('neck', 'head', 'crown'):
            seed, sc = np.array(bones['head'][0]) + 0.9 * (np.array(bones['head'][1]) - np.array(bones['head'][0])), 0.2
        elif base in ('waist', 'chest', 'upchest', 'pelvis', 'hip'):
            seed, sc = np.array(bones['chest'][0]) + 0.5 * (np.array(bones['chest'][1]) - np.array(bones['chest'][0])), 0.45
        else:
            seed, sc = heads[i] + 0.45 * v[i], 0.5 * lengths[i] + 0.12
        o = np.sqrt(((P[m] - seed) ** 2).sum(-1)) / sc
        zone[m] = zi
        band[m] = bd
        order[m] = np.clip(o, 0, 1)
    zb = zone[:H * f, :W * f].reshape(H, f, W, f).transpose(0, 2, 1, 3).reshape(H, W, f * f)
    counts = (zb[..., None] == np.arange(15)).sum(2)
    counts[..., 0] = 0
    zv = counts.argmax(-1)
    has = counts.max(-1) > 0
    bb = band[:H * f, :W * f].reshape(H, f, W, f).transpose(0, 2, 1, 3).reshape(H, W, f * f).mean(-1)
    ob = order[:H * f, :W * f].reshape(H, f, W, f).transpose(0, 2, 1, 3).reshape(H, W, f * f)
    obm = np.where(zb > 0, ob, np.nan)
    with np.errstate(all='ignore'):
        om = np.nanmean(obm, -1)
    om = np.nan_to_num(om)
    out = np.zeros((H, W, 4), np.uint8)
    out[has, 0] = (zv[has] * 16).astype(np.uint8)
    out[has, 1] = np.where(bb[has] >= 0.5, 255, 0)
    out[has, 2] = np.clip(np.round(om[has] * 254 + 1), 1, 255).astype(np.uint8)
    out[has, 3] = 255
    return out


# ---- rendering -------------------------------------------------------------------------------------

def pos_material():
    m = bpy.data.materials.get('M_PosPass')
    if m:
        return m
    m = bpy.data.materials.new('M_PosPass')
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    geo = nt.nodes.new('ShaderNodeNewGeometry')
    mul = nt.nodes.new('ShaderNodeVectorMath'); mul.operation = 'MULTIPLY_ADD'
    mul.inputs[1].default_value = (0.2, 0.2, 0.2)
    mul.inputs[2].default_value = (0.5, 0.5, 0.5)
    em = nt.nodes.new('ShaderNodeEmission')
    out = nt.nodes.new('ShaderNodeOutputMaterial')
    nt.links.new(geo.outputs['Position'], mul.inputs[0])
    nt.links.new(mul.outputs[0], em.inputs['Color'])
    nt.links.new(em.outputs[0], out.inputs['Surface'])
    return m


def bones_world():
    M = RIG.matrix_world
    return {pb.name: [list(M @ pb.head), list(M @ pb.tail)] for pb in RIG.pose.bones}


def render_frame(path_color, path_pos=None, path_bones=None):
    sc = bpy.context.scene
    vl = bpy.context.view_layer
    sc.render.filepath = path_color
    sc.render.image_settings.file_format = 'PNG'
    sc.render.image_settings.color_mode = 'RGBA'
    sc.render.image_settings.color_depth = '8'
    bpy.ops.render.render(write_still=True)
    if path_pos:
        vt = sc.view_settings.view_transform
        hidden = {}
        for n in NO_POS:
            o = bpy.data.objects.get(n)
            if o:
                hidden[n] = o.hide_render
                o.hide_render = True
        vl.material_override = pos_material()
        sc.view_settings.view_transform = 'Raw'
        sc.render.image_settings.color_depth = '16'
        sc.render.filepath = path_pos
        bpy.ops.render.render(write_still=True)
        vl.material_override = None
        sc.view_settings.view_transform = vt
        sc.render.image_settings.color_depth = '8'
        for n, h in hidden.items():
            bpy.data.objects[n].hide_render = h
    if path_bones:
        with open(path_bones, 'w') as fh:
            json.dump(bones_world(), fh)


# ---- swing smears ----------------------------------------------------------------------------------

def _lin(hexs):
    c = [int(hexs[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return [x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c] + [1.0]


def smear_materials():
    out = []
    for name, hx in (('M_SmearNew', 'e6e2d4'), ('M_SmearOld', 'c4bfb2')):
        m = bpy.data.materials.get(name)
        if m is None:
            m = bpy.data.materials.new(name)
            m.use_nodes = True
            nt = m.node_tree
            nt.nodes.clear()
            em = nt.nodes.new('ShaderNodeEmission')
            em.inputs['Color'].default_value = _lin(hx)
            o = nt.nodes.new('ShaderNodeOutputMaterial')
            nt.links.new(em.outputs[0], o.inputs['Surface'])
        out.append(m)
    return out


def sword_world():
    bpy.context.view_layer.update()
    return SWORD.matrix_world.copy()


def build_smear(Ma, Mb, n=8, r_old=0.88, r_new=0.42):
    """A ribbon swept by the blade (from r0 of its length to the tip) between two sword matrices."""
    from mathutils import Matrix
    la, qa, sa = Ma.decompose()
    lb, qb, sb = Mb.decompose()
    rows = []
    for k in range(n + 1):
        t = k / n
        M = Matrix.LocRotScale(la.lerp(lb, t), qa.slerp(qb, t), sa.lerp(sb, t))
        r0 = r_old + (r_new - r_old) * t      # a crescent: thin at the old end, wide at the blade
        rows.append((M @ Vector((0, 0, -0.72 * r0)), M @ Vector((0, 0, -0.74))))
    me = bpy.data.meshes.new('Smear')
    verts = [v for row in rows for v in row]
    faces = [(2 * k, 2 * k + 1, 2 * k + 3, 2 * k + 2) for k in range(n)]
    me.from_pydata([tuple(v) for v in verts], [], faces)
    new, old = smear_materials()
    me.materials.append(old)
    me.materials.append(new)
    for k, p in enumerate(me.polygons):
        p.material_index = 1 if k >= n // 2 else 0
    ob = bpy.data.objects.new('Smear', me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def clear_smear():
    ob = bpy.data.objects.get('Smear')
    if ob:
        me = ob.data
        bpy.data.objects.remove(ob)
        bpy.data.meshes.remove(me)


def pose_frame(frames, i, d):
    """Applies frame i and adds its smear (a frame with 'smear': True sweeps from the previous one)."""
    clear_smear()
    st = frames[i]
    if st.get('smear') and i > 0:
        apply(frames[i - 1], d)
        Ma = sword_world()
        apply(st, d)
        Mb = sword_world()
        build_smear(Ma, Mb)
    else:
        apply(st, d)


def render_clip(name, frames, dirs, full=True):
    """frames: list of states. Renders color (+ position pass and bones when full) for each dir."""
    os.makedirs(RENDER, exist_ok=True)
    for d in dirs:
        for i, st in enumerate(frames):
            pose_frame(frames, i, d)
            tag = "%s_d%d_f%d" % (name, d, i + 1)
            if full:
                render_frame(os.path.join(RENDER, tag + ".png"), os.path.join(RENDER, "pos_" + tag + ".png"),
                             os.path.join(RENDER, "bones_" + tag + ".json"))
            else:
                render_frame(os.path.join(RENDER, tag + ".png"))
    clear_smear()


def preview(name, frames, dirs, out):
    """Quick check: color only, pixelized, all frames of the given dirs in one strip (rows = dirs)."""
    tiles = []
    for d in dirs:
        row = []
        for i, st in enumerate(frames):
            pose_frame(frames, i, d)
            p = os.path.join(RENDER, "_prev.png")
            render_frame(p)
            img, _ = pixelize(px_load(p))
            row.append(img)
        tiles.append(np.concatenate(row, 1))
    clear_smear()
    px_save(np.concatenate(tiles, 0), out)


# ---- sheets for Unity ------------------------------------------------------------------------------

UNITY_DIR = os.path.join(ROOT, r"Assets\Characters\HeroBlender")
CELL = 128
FEET = (64, 36)          # feet inside a 128 cell: from the left, from the bottom


def build_sheets(clips, smear=None, views=8, names=None):
    """Pixelizes the renders of every clip, makes its zone map and writes one cropped sheet per clip
    (columns = frames, rows = views) plus _zonas_<clip>.png and _meta.json."""
    os.makedirs(UNITY_DIR, exist_ok=True)
    smear = smear or {}
    meta = {"views": views, "zones": ZONE_IDS, "clips": []}
    names = names or list(clips.keys())
    for name in names:
        n = len(clips[name])
        cells, zcells = {}, {}
        x0, y0, x1, y1 = CELL, CELL, 0, 0
        for d in range(views):
            for i in range(n):
                tag = "%s_d%d_f%d" % (name, d, i + 1)
                img, body = pixelize(px_load(os.path.join(RENDER, tag + ".png")))
                with open(os.path.join(RENDER, "bones_" + tag + ".json")) as fh:
                    bones = json.load(fh)
                z = zones_np(px_load(os.path.join(RENDER, "pos_" + tag + ".png")), bones)
                z[~body] = 0
                cells[(d, i)] = img
                zcells[(d, i)] = z
                ys, xs = np.nonzero(img[..., 3])
                if len(xs):
                    x0, x1 = min(x0, xs.min()), max(x1, xs.max() + 1)
                    y0, y1 = min(y0, ys.min()), max(y1, ys.max() + 1)
        # keep the feet column inside and pad one pixel
        x0, y0 = max(0, x0 - 1), max(0, y0 - 1)
        x1, y1 = min(CELL, x1 + 1), min(CELL, y1 + 1)
        w, h = int(x1 - x0), int(y1 - y0)
        sheet = np.zeros((views * h, n * w, 4), np.uint8)
        zsheet = np.zeros((views * h, n * w, 4), np.uint8)
        for (d, i), img in cells.items():
            sheet[d * h:(d + 1) * h, i * w:(i + 1) * w] = img[y0:y1, x0:x1]
            zsheet[d * h:(d + 1) * h, i * w:(i + 1) * w] = zcells[(d, i)][y0:y1, x0:x1]
        px_save(sheet, os.path.join(UNITY_DIR, name + ".png"))
        px_save(zsheet, os.path.join(UNITY_DIR, "_zonas_" + name + ".png"))
        feet_x = FEET[0] - x0
        feet_y = FEET[1] - (CELL - y1)          # rows counted from the bottom of the crop
        hit = smear.get(name)
        entry = {"name": name, "frames": n, "w": w, "h": h, "feetX": int(feet_x), "feetY": int(feet_y)}
        if hit:
            entry["hitFrom"] = int(min(hit))
            entry["hitTo"] = int(max(hit) + 1)
        meta["clips"].append(entry)
    with open(os.path.join(UNITY_DIR, "_meta.json"), "w") as fh:
        json.dump(meta, fh, indent=1)
    return meta
