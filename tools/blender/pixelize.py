"""Uso: python pixelize.py render_512.png sprite_128.png
Render de Blender (4x) -> sprite de pixel art en la paleta de 24.
Votacion por bloque (el color mas frecuente de cada 4x4), ajuste a paleta, limpieza de pixeles sueltos y contorno."""
import sys
from collections import Counter
from PIL import Image

# Paleta de Barro y Bruma: 16 del juego + 8 de la extension
PAL = {'0': '#141210', '1': '#2a2420', '2': '#3d332b', '3': '#594a3c', '4': '#7a6650', '5': '#9c8a70',
       '6': '#3f3e3b', '7': '#6e6c66', '8': '#9a968c', '9': '#c4bfb2', 'a': '#2f3436', 'b': '#4f5a5c',
       'c': '#7d8584', 'd': '#3e1a18', 'e': '#6b2a24', 'f': '#8f4a36', 'g': '#b08a6a', 'h': '#d2ac88',
       'i': '#e6e2d4', 'j': '#2c2738', 'k': '#5b5170', 'm': '#4e4b34', 'n': '#6f6a45', 'o': '#b24a36'}
RGB = {k: tuple(int(v[i:i+2], 16) for i in (1, 3, 5)) for k, v in PAL.items()}
OUTLINE = RGB['0']

def nearest(c):
    return min(RGB.values(), key=lambda p: sum((a-b)**2 for a, b in zip(p, c)))

def pixelize(src, f=4, outline=True, clean=True):
    im = Image.open(src).convert('RGBA'); W, H = im.width // f, im.height // f
    px = im.load(); out = Image.new('RGBA', (W, H), (0, 0, 0, 0)); op = out.load()
    for y in range(H):
        for x in range(W):
            cnt = Counter(); solid = 0
            for j in range(f):
                for i in range(f):
                    r, g, b, a = px[x*f+i, y*f+j]
                    if a >= 128: cnt[(r, g, b)] += 1; solid += 1
            if solid * 2 >= f * f:
                op[x, y] = nearest(cnt.most_common(1)[0][0]) + (255,)
    if clean:   # un pixel rodeado por 3 o 4 vecinos de un mismo color distinto toma ese color
        cp = out.copy().load()
        for y in range(1, H-1):
            for x in range(1, W-1):
                c = cp[x, y]
                if c[3] == 0: continue
                n = [cp[x+1, y], cp[x-1, y], cp[x, y+1], cp[x, y-1]]
                cn = Counter(v for v in n if v[3] and v != c)
                if cn and cn.most_common(1)[0][1] >= 3: op[x, y] = cn.most_common(1)[0][0]
    if outline:
        cp = out.copy().load()
        for y in range(H):
            for x in range(W):
                if cp[x, y][3]: continue
                if any(0 <= x+a < W and 0 <= y+b < H and cp[x+a, y+b][3] for a, b in ((1,0),(-1,0),(0,1),(0,-1))):
                    op[x, y] = OUTLINE + (255,)
    return out

if __name__ == '__main__':
    s = pixelize(sys.argv[1]); s.save(sys.argv[2])
    bb = s.getbbox(); print('caja', bb, 'alto', bb[3]-bb[1], 'ancho', bb[2]-bb[0])
