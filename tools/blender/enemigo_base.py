# Barro y Bruma - common start for an enemy (or NPC) built on a copy of the hero in Blender.
#   setup(name, keep=(...), style={...}) opens heroe_referencia.blend, saves it as art_src/<name>/<name>.blend,
#   removes the hero's gear (except what `keep` lists), registers pal_mat, loads the mesh helpers of
#   lenador_build.py into a namespace (returned) and stores the enemy STYLE in the scene.
#   load_pose(name) loads enemigo_pose.py + hero_render.py + npc_sheets.py into a namespace.
import bpy, os, json

ROOT = r"C:\Users\franc\Documents\PixelGame\BrumaPx"
TOOLS = ROOT + r"\tools\blender\\"
GEAR = ['Blade', 'Grip', 'Guard', 'Pommel', 'Sword', 'Blade_S', 'Grip_S', 'Guard_S', 'Pommel_S', 'SwordSheath',
        'Scabbard', 'Chape', 'ScabbardHold', 'Cloak', 'Hood', 'Scarf', 'ScarfTail', 'Gloves', 'Pouch', 'Belt', 'Buckle']


def lin(h):
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return tuple([x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c] + [1.0])


def pal_mat(name, hexes, base='T_Botas'):
    m = bpy.data.materials.get(name)
    if m is None:
        m = bpy.data.materials[base].copy()
        m.name = name
    r = [n for n in m.node_tree.nodes if n.type == 'VALTORGB'][0].color_ramp
    for e, h in zip(r.elements, hexes):
        e.color = lin(h)
    return m


def setup(name, keep=(), style=None, fresh=True):
    if fresh:
        bpy.ops.wm.open_mainfile(filepath=ROOT + r"\art_src\heroe_blender\heroe_referencia.blend")
        d = ROOT + "\\art_src\\" + name
        os.makedirs(d, exist_ok=True)
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(d, name + ".blend"))
        for n in GEAR:
            if n in keep:
                continue
            o = bpy.data.objects.get(n)
            if o:
                bpy.data.objects.remove(o, do_unlink=True)
    bpy.app.driver_namespace['pal_mat'] = pal_mat
    ns = {'__name__': name + '_build'}
    exec(open(TOOLS + "lenador_build.py", encoding='utf-8').read(), ns)
    if style is not None:
        bpy.context.scene['enemy_style'] = json.dumps(style)
    bpy.app.driver_namespace['EB'] = ns
    return ns


def load_pose(name, render_sub='render'):
    g = {'__name__': 'enemigo_pose'}
    exec(open(TOOLS + "enemigo_pose.py", encoding='utf-8').read(), g)
    exec(open(TOOLS + "hero_render.py", encoding='utf-8').read(), g)
    sh = {}
    exec(open(TOOLS + "npc_sheets.py", encoding='utf-8').read(), sh)
    g['SHEETS'] = sh
    g['RENDER'] = ROOT + "\\art_src\\" + name + "\\" + render_sub
    os.makedirs(g['RENDER'], exist_ok=True)
    bpy.app.driver_namespace['EP'] = g
    return g
