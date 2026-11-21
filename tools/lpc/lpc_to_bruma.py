"""Convierte capas del Universal LPC Spritesheet a la paleta de 24 de Barro y Bruma.

Uso:  python3 lpc_to_bruma.py <carpeta spritesheets de LPC> <carpeta de salida>

Por cada capa escribe una hoja con la MISMA grilla (9 columnas x 13 filas de 128 px):
  filas 0-3 idle (arriba, izquierda, abajo, derecha), 4-7 walk, 8-11 slash, 12 hurt.
El cuerpo de LPC (64x64) queda centrado en la celda de 128 para que entren las armas.
Las capas salen SIN contorno: el contorno lo pone el shader sobre el sprite compuesto.
"""
import sys, os, csv, json, colorsys
from PIL import Image

SRC = sys.argv[1] if len(sys.argv) > 1 else '/home/claude/lpc/repo/spritesheets'
OUT = sys.argv[2] if len(sys.argv) > 2 else '/home/claude/lpc/work/out'

PAL = {'0': '#141210', '1': '#2a2420', '2': '#3d332b', '3': '#594a3c', '4': '#7a6650', '5': '#9c8a70',
       '6': '#3f3e3b', '7': '#6e6c66', '8': '#9a968c', '9': '#c4bfb2', 'a': '#2f3436', 'b': '#4f5a5c',
       'c': '#7d8584', 'd': '#3e1a18', 'e': '#6b2a24', 'f': '#8f4a36', 'g': '#b08a6a', 'h': '#d2ac88',
       'i': '#e6e2d4', 'j': '#2c2738', 'k': '#5b5170', 'm': '#4e4b34', 'n': '#6f6a45', 'o': '#b24a36'}
RGB = {k: tuple(int(v[i:i + 2], 16) for i in (1, 3, 5)) for k, v in PAL.items()}

CELL = 128
ANIMS = [('idle', 2, 4), ('walk', 9, 4), ('slash', 6, 4), ('hurt', 6, 1)]     # nombre, fotogramas, direcciones
COLS = 9
ROWS = sum(a[2] for a in ANIMS)
BAYER = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]

# Rampas de oscuro a claro.
SKIN = '23gh'
R = dict(skin=SKIN, hair='0123', linen='6789', wool='1234', shoe='012', oxide='0deffo', leather='012345',
         dleather='01123', mail='0abc9', steel='0abc9i', wood='01234', rag='01223', scarf='0deef')


def warm(h, s, v):
    return s > 0.22 and (h < 0.14 or h > 0.92)


def grey(h, s, v):
    return s <= 0.22


def blue(h, s, v):
    return s > 0.25 and 0.45 < h < 0.75


# capa: (id, orden, [(fuente por animacion)], reglas [(predicado o None, rampa)], trama de malla)
def src(path, **over):
    d = {a[0]: '%s/%s.png' % (path, a[0]) for a in ANIMS}
    d.update(over)
    return d


SW = 'weapon/sword/arming'
AX = 'weapon/blunt/waraxe'
LAYERS = [
    dict(id='capa_atras', z=0, slot='capa', src=src('cape/tattered/bg'), rules=[(None, R['rag'])]),
    dict(id='arma_ligera_atras', z=1, slot='arma', src={'idle': SW + '/universal/bg/idle/steel.png', 'walk': SW + '/universal/bg/walk/steel.png',
                                                           'slash': SW + '/attack_slash/bg/steel.png', 'hurt': SW + '/universal/bg/hurt/steel.png'},
         rules=[(warm, R['wood']), (None, R['steel'])]),
    dict(id='arma_pesada_atras', z=1, slot='arma', src={'idle': AX + '/behind/walk/waraxe.png', 'walk': AX + '/behind/walk/waraxe.png',
                                                           'slash': AX + '/attack_slash/behind/waraxe.png', 'hurt': None},
         rules=[(warm, R['wood']), (None, R['steel'])], idle_from_walk=True),
    dict(id='cuerpo', z=10, slot='cuerpo', src=src('body/bodies/male'), rules=[(None, R['skin'])]),
    dict(id='cabeza', z=11, slot='cuerpo', src=src('head/heads/human/male'), rules=[(blue, '11'), (grey, 'gh'), (None, R['skin'])]),
    dict(id='zapatos', z=20, slot='base', src=src('feet/shoes/basic/male'), rules=[(None, R['shoe'])]),
    dict(id='calzas', z=21, slot='base', src=src('legs/pants/male'), rules=[(None, R['wool'])]),
    dict(id='camisa', z=22, slot='base', src=src('torso/clothes/longsleeve/longsleeve/male'), rules=[(None, R['linen'])]),
    dict(id='pelo', z=23, slot='base', src=src('hair/messy1/adult'), rules=[(None, R['hair'])]),
    # cuero
    dict(id='polainas_cuero', z=30, slot='piernas', src=src('legs/pants/male'), rules=[(None, R['dleather'])]),
    dict(id='polainas_cuero_botas', z=31, slot='piernas', src=src('feet/boots/fold/male'), rules=[(None, R['leather'][:5])]),
    dict(id='jubon_cuero', z=32, slot='torso', src=src('torso/armour/leather/male'), rules=[(grey, '0789'), (None, R['leather'])]),
    dict(id='mangas_cuero', z=33, slot='brazos', src=src('arms/bracers/male'), rules=[(None, R['leather'][:5])]),
    dict(id='mangas_cuero_guantes', z=34, slot='brazos', src=src('arms/hands/gloves/male'), rules=[(None, R['dleather'])]),
    dict(id='capucha_cuero', z=40, slot='cabeza', src=src('hat/cloth/hood/adult'), rules=[(None, R['oxide'])]),
    # malla
    dict(id='grebas_malla', z=30, slot='piernas', src=src('legs/armour/plate/male'), rules=[(None, R['mail'])], mail=True),
    dict(id='grebas_malla_pies', z=31, slot='piernas', src=src('feet/armour/plate/male'), rules=[(None, R['mail'])]),
    dict(id='cota_malla', z=32, slot='torso', src=src('torso/chainmail/male'), rules=[(None, R['mail'])]),
    dict(id='mangas_malla', z=33, slot='brazos', src=src('arms/armour/plate/male'), rules=[(None, R['mail'])], mail=True),
    dict(id='cofia_malla', z=40, slot='cabeza', src=src('hat/helmet/mail/adult'), rules=[(None, R['mail'])]),
    dict(id='capelina', z=41, slot='cabeza', src=src('hat/helmet/kettle/adult'), rules=[(warm, R['wood']), (None, R['steel'])]),
    # accesorios
    dict(id='bufanda', z=45, slot='cuello', src=src('neck/scarf'), rules=[(None, R['scarf'])]),
    dict(id='capa_frente', z=46, slot='capa', src=src('cape/tattered/fg'), rules=[(None, R['rag'])]),
    dict(id='escudo', z=50, slot='escudo', src=src('shield/heater/original/paint/fg'), rules=[(None, R['wood'])]),
    dict(id='arma_ligera', z=60, slot='arma', src={'idle': SW + '/universal/fg/idle/steel.png', 'walk': SW + '/universal/fg/walk/steel.png',
                                                    'slash': SW + '/attack_slash/fg/steel.png', 'hurt': SW + '/universal/fg/hurt/steel.png'},
         rules=[(warm, R['wood']), (None, R['steel'])]),
    dict(id='arma_pesada', z=60, slot='arma', src={'idle': AX + '/walk/waraxe.png', 'walk': AX + '/walk/waraxe.png',
                                                    'slash': AX + '/attack_slash/waraxe.png', 'hurt': AX + '/hurt/waraxe.png'},
         rules=[(warm, R['wood']), (None, R['steel'])], idle_from_walk=True),
]


def load_frames(layer):
    """Devuelve {(anim, dir, frame): imagen RGBA de 128x128} con el cuerpo centrado."""
    out = {}
    for name, nfr, ndir in ANIMS:
        p = layer['src'].get(name)
        if not p or not os.path.exists(os.path.join(SRC, p)):
            continue
        im = Image.open(os.path.join(SRC, p)).convert('RGBA')
        rows = ndir
        cell = im.height // rows
        if cell not in (64, 128, 192):
            cell = 64
        n_here = im.width // cell
        for d in range(ndir):
            for f in range(nfr):
                sf = f
                if name == 'idle' and layer.get('idle_from_walk'):
                    sf = 0
                if sf >= n_here:
                    sf = n_here - 1
                c = im.crop((sf * cell, d * cell, (sf + 1) * cell, (d + 1) * cell))
                fr = Image.new('RGBA', (CELL, CELL), (0, 0, 0, 0))
                off = (CELL - cell) // 2
                fr.paste(c, (off, off))
                out[(name, d, f)] = fr
    return out


def convert(layer, frames):
    """Rampa por luminancia + tramado ordenado + mugre hacia los pies."""
    groups = {}
    for key, fr in frames.items():
        px = fr.load()
        for y in range(CELL):
            for x in range(CELL):
                r, g, b, a = px[x, y]
                if a < 128:
                    continue
                h, s, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
                gi = len(layer['rules']) - 1
                for i, (pred, _) in enumerate(layer['rules']):
                    if pred is None or pred(h, s, v):
                        gi = i
                        break
                lum = 0.299 * r + 0.587 * g + 0.114 * b
                groups.setdefault(gi, []).append((key, x, y, lum))
    res = {k: {} for k in frames}
    for gi, items in groups.items():
        ramp = layer['rules'][gi][1]
        lums = sorted(i[3] for i in items)
        lo, hi = lums[int(len(lums) * 0.02)], lums[int(len(lums) * 0.985)]
        span = max(hi - lo, 1.0)
        n = len(ramp)
        for key, x, y, lum in items:
            t = min(max((lum - lo) / span, 0.0), 1.0) * (n - 1)
            bay = (BAYER[y & 3][x & 3] + 0.5) / 16.0
            if t > 0.6:                                    # el trazo mas oscuro de LPC se queda entero
                t += (bay - 0.5) * 0.55                    # tramado entre tonos vecinos
                if y > 84 and bay > 0.55:                  # mugre: de las rodillas para abajo
                    t -= 0.6
                if layer.get('mail') and (x + y) % 2 == 0:
                    t -= 0.7                               # trama de malla
            idx = int(min(max(round(t), 0), n - 1))
            res[key][(x, y)] = ramp[idx]
    return res


def sheet_pos(name, d, f):
    row = 0
    for a, nfr, ndir in ANIMS:
        if a == name:
            return f * CELL, (row + d) * CELL
        row += ndir


def write_sheet(frames_px, path):
    im = Image.new('RGBA', (COLS * CELL, ROWS * CELL), (0, 0, 0, 0))
    px = im.load()
    for (name, d, f), pix in frames_px.items():
        ox, oy = sheet_pos(name, d, f)
        for (x, y), c in pix.items():
            px[ox + x, oy + y] = RGB[c] + (255,)
    im.save(path)
    return im


# ---- Mapa de zonas: 14 zonas del BodyProfile, indice 1..14 guardado en el canal rojo (indice * 16)
ZONES = ['cabeza', 'torso', 'hombro.d', 'hombro.i', 'brazo.d', 'brazo.i', 'antebrazo.d', 'antebrazo.i',
         'muslo.d', 'muslo.i', 'rodilla.d', 'rodilla.i', 'pantorrilla.d', 'pantorrilla.i']


def _h(x, y, k):
    """Ruido fijo por pixel (el mismo en cada corrida)."""
    n = (x * 374761393 + y * 668265263 + k * 2246822519) & 0xFFFFFFFF
    n = ((n ^ (n >> 13)) * 1274126177) & 0xFFFFFFFF
    return ((n ^ (n >> 16)) & 0xFFFF) / 65535.0


def zone_maps(conv):
    """Mapa de zonas con tres canales, sobre la misma grilla que las capas:
    R = numero de zona * 16 (1..14, 0 = sin zona)
    G = 255 donde se enrolla la venda (una franja dentro de la zona)
    B = orden de avance (1..255): la Bruma de una zona al nivel L tapa los pixeles con B/255 < L;
        la herida abierta es la mancha con B/255 < 0.2.
    Direcciones: 0 arriba, 1 izquierda, 2 abajo, 3 derecha."""
    out = {}
    body = conv['cuerpo']
    for key in body:
        name, d, f = key
        head = set(conv['cabeza'].get(key, {}))
        vest = set(conv['jubon_cuero'].get(key, {}))
        pants = set(conv['calzas'].get(key, {}))
        shoes = set(conv['zapatos'].get(key, {}))
        pix = set(body[key]) | head
        z = {}
        if not pix:
            out[key] = z
            continue
        xs = [p[0] for p in pix]
        cx = (min(xs) + max(xs)) / 2.0

        def side(x):
            right = x >= cx
            if d == 0:
                return 0 if right else 1
            return 1 if right else 0      # 0 = .d, 1 = .i

        for p in head:
            z[p] = 1
        for p in vest:
            if p in pix and p not in head:
                z[p] = 2
        legs = [p for p in pants if p in pix and p not in head and p not in shoes]
        arms = [p for p in pix if p not in head and p not in vest and p not in pants and p not in shoes]
        for s in (0, 1):
            g = [p for p in arms if side(p[0]) == s]
            if g:
                y0, y1 = min(p[1] for p in g), max(p[1] for p in g)
                h = max(y1 - y0 + 1, 1)
                for p in g:
                    t = (p[1] - y0) / h
                    if t < 0.30: z[p] = 3 + s          # hombro
                    elif t < 0.55: z[p] = 5 + s        # brazo
                    elif t < 0.80: z[p] = 7 + s        # antebrazo (la mano queda sin zona)
            g = [p for p in legs if side(p[0]) == s]
            if g:
                y0, y1 = min(p[1] for p in g), max(p[1] for p in g)
                h = max(y1 - y0 + 1, 1)
                for p in g:
                    t = (p[1] - y0) / h
                    if t < 0.15: continue              # cadera: es del torso, sin zona
                    if t < 0.52: z[p] = 9 + s          # muslo
                    elif t < 0.70: z[p] = 11 + s       # rodilla
                    else: z[p] = 13 + s                # pantorrilla
        # por zona: franja de venda y orden de avance
        groups = {}
        for p, i in z.items():
            groups.setdefault(i, []).append(p)
        px = {}
        for i, g in groups.items():
            ys = [p[1] for p in g]; xs2 = [p[0] for p in g]
            y0, y1, x0, x1 = min(ys), max(ys), min(xs2), max(xs2)
            h, w = y1 - y0 + 1, x1 - x0 + 1
            if i == 1:
                band = (y0 + int(h * 0.26), y0 + int(h * 0.40))           # frente
            elif i == 2:
                band = (y0 + int(h * 0.30), y0 + int(h * 0.30) + max(2, int(h * 0.30)))
            else:
                m = (y0 + y1) / 2.0
                hb = max(1.0, h * 0.30)
                band = (int(round(m - hb)), int(round(m + hb)) - 1)
            sx = x0 + w * (0.35 + 0.3 * _h(i, 1, 7))
            sy = y0 + h * (0.35 + 0.3 * _h(i, 2, 7))
            far = max(1.0, max(((p[0] - sx) ** 2 + (p[1] - sy) ** 2) ** 0.5 for p in g))
            for p in g:
                dist = ((p[0] - sx) ** 2 + (p[1] - sy) ** 2) ** 0.5 / far
                t = min(max(dist * 0.85 + (_h(p[0], p[1], i) - 0.5) * 0.30, 0.0), 1.0)
                px[p] = (i * 16, 255 if band[0] <= p[1] <= band[1] else 0, 1 + int(t * 254))
        out[key] = px
    im = Image.new('RGBA', (COLS * CELL, ROWS * CELL), (0, 0, 0, 0))
    ip = im.load()
    for (name, d, f), px in out.items():
        ox, oy = sheet_pos(name, d, f)
        for (x, y), v in px.items():
            ip[ox + x, oy + y] = v + (255,)
    return im, out


def credits(used):
    rows = {}
    path = os.path.join(SRC, '..', 'CREDITS.csv')
    with open(path, newline='', encoding='utf-8') as fh:
        for r in csv.DictReader(fh, skipinitialspace=True):
            rows[r['filename'].strip()] = r
    lines = ['# Créditos de las capas LPC usadas en Barro y Bruma', '',
             'Sprites derivados del Universal LPC Spritesheet Character Generator',
             '(https://github.com/LiberatedPixelCup/Universal-LPC-Spritesheet-Character-Generator).',
             'Fueron recoloreados a la paleta del juego y recompuestos; siguen bajo las licencias de origen.', '',
             '| Capa del juego | Archivo LPC | Autores | Licencias |', '|---|---|---|---|']
    missing = []
    for lid, p in used:
        r = rows.get(p)
        if not r:
            # los creditos suelen estar por carpeta o por otra animacion de la misma pieza
            base = p.rsplit('/', 1)[0]
            cand = [k for k in rows if k.startswith(base + '/')] or [k for k in rows if k.startswith(base.rsplit('/', 1)[0] + '/')]
            r = rows.get(cand[0]) if cand else None
        if r:
            lines.append('| %s | %s | %s | %s |' % (lid, p, r['authors'].strip(), r['licenses'].strip()))
        else:
            missing.append(p)
            lines.append('| %s | %s | (sin fila en CREDITS.csv: revisar a mano) | |' % (lid, p))
    return '\n'.join(lines) + '\n', missing


def main():
    os.makedirs(OUT + '/capas', exist_ok=True)
    conv, used, info = {}, [], []
    for layer in LAYERS:
        fr = load_frames(layer)
        if not fr:
            print('SIN FUENTE', layer['id'])
            continue
        c = convert(layer, fr)
        conv[layer['id']] = c
        write_sheet(c, '%s/capas/%s.png' % (OUT, layer['id']))
        for a, p in layer['src'].items():
            if p and os.path.exists(os.path.join(SRC, p)):
                used.append((layer['id'], p))
        info.append({'id': layer['id'], 'z': layer['z'], 'slot': layer['slot'], 'anims': sorted({k[0] for k in fr})})
        print('ok', layer['id'], len(fr), 'fotogramas')
    zim, _ = zone_maps(conv)
    zim.save(OUT + '/capas/_zonas.png')
    txt, missing = credits(used)
    open(OUT + '/CREDITOS-LPC.md', 'w', encoding='utf-8').write(txt)
    json.dump({'cell': CELL, 'cols': COLS, 'rows': ROWS, 'anims': [{'name': a, 'frames': n, 'dirs': d} for a, n, d in ANIMS],
               'dir_order': ['arriba', 'izquierda', 'abajo', 'derecha'], 'layers': info, 'zones': ZONES, 'palette': PAL},
              open(OUT + '/capas/_meta.json', 'w'), indent=1)
    print('creditos sin fila:', missing)
    return conv


if __name__ == '__main__':
    main()
