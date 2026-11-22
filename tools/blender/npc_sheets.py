# Barro y Bruma - renders every clip of a loaded pose namespace (g: CLIPS, apply, render_frame, pixelize...) in
# 8 views in the background (bpy.app.timers) and then writes one cropped sheet per clip plus _meta.json, the
# format CharacterSetBuilder.ClipSheetSet reads. Shared by NPCs and enemies.
#   start(g, name_prefix='', clips=None)   -> starts the job (state in driver_namespace['SHEET_JOB'])
#   sheets(g, unity_dir, clips=None)        -> after the job: crops, writes PNGs and meta
import bpy, os, json
import numpy as np

CELL, FEET = 128, (64, 36)


def start(g, clips=None, views=8):
    names = clips or list(g['CLIPS'].keys())
    jobs = [(n, d, i) for n in names for d in range(views) for i in range(len(g['CLIPS'][n]))]
    st = {'jobs': jobs, 'k': 0, 'done': False, 'err': None}
    bpy.app.driver_namespace['SHEET_JOB'] = st

    def tick():
        try:
            if st['k'] >= len(st['jobs']):
                st['done'] = True
                return None
            n, d, i = st['jobs'][st['k']]
            g['apply'](g['CLIPS'][n][i], d)
            g['render_frame'](os.path.join(g['RENDER'], "%s_d%d_f%d.png" % (n, d, i + 1)))
            st['k'] += 1
            return 0.01
        except Exception:
            import traceback
            st['err'] = traceback.format_exc()
            return None
    bpy.app.timers.register(tick, first_interval=0.1)
    return len(jobs)


def sheets(g, unity_dir, clips=None, views=8):
    os.makedirs(unity_dir, exist_ok=True)
    names = clips or list(g['CLIPS'].keys())
    meta = {"views": views, "clips": []}
    for name in names:
        n = len(g['CLIPS'][name])
        cells = {}
        x0, y0, x1, y1 = CELL, CELL, 0, 0
        for d in range(views):
            for i in range(n):
                img, _ = g['pixelize'](g['px_load'](os.path.join(g['RENDER'], "%s_d%d_f%d.png" % (name, d, i + 1))))
                cells[(d, i)] = img
                ys, xs = np.nonzero(img[..., 3])
                if len(xs):
                    x0, x1 = min(x0, xs.min()), max(x1, xs.max() + 1)
                    y0, y1 = min(y0, ys.min()), max(y1, ys.max() + 1)
        x0, y0, x1, y1 = max(0, x0 - 1), max(0, y0 - 1), min(CELL, x1 + 1), min(CELL, y1 + 1)
        w, h = int(x1 - x0), int(y1 - y0)
        sheet = np.zeros((views * h, n * w, 4), np.uint8)
        for (d, i), img in cells.items():
            sheet[d * h:(d + 1) * h, i * w:(i + 1) * w] = img[y0:y1, x0:x1]
        g['px_save'](sheet, os.path.join(unity_dir, name + ".png"))
        meta["clips"].append({"name": name, "frames": n, "dirs": views, "w": w, "h": h,
                              "feetX": int(FEET[0] - x0), "feetY": int(FEET[1] - (CELL - y1))})
    with open(os.path.join(unity_dir, "_meta.json"), "w") as fh:
        json.dump(meta, fh, indent=1)
    return meta
