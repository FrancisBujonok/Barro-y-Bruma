// Barro y Bruma — base visual assets (pxl source).
// Scale: 32 px per world tile (Unity: 32 pixels per unit).
// Build:  node .claude/skills/pxl-pixel-art/bin/pxl.js build tools/art/barro_y_bruma.px.js
// Live:   node .claude/skills/pxl-pixel-art/bin/pxl.js serve tools/art/barro_y_bruma.px.js

// ── 1. Palette: 16 desaturated, oppressive colors ────────────────────────────
// Mud, ash, old iron and dried blood. Every asset uses only these keys.
pal({
  // soot / mud
  k: "#141210", // soot black (outlines, deepest shadow)
  K: "#2a2420", // wet umber
  m: "#3d332b", // dark mud
  M: "#594a3c", // mud
  b: "#7a6650", // dry earth
  B: "#9c8a70", // cracked clay
  // ash
  a: "#3f3e3b", // packed ash
  A: "#6e6c66", // ash
  s: "#9a968c", // light ash
  S: "#c4bfb2", // fresh ash / bone
  // old iron
  i: "#2f3436", // dark iron
  I: "#4f5a5c", // iron
  o: "#7d8584", // worn steel edge
  // dried blood / rust
  r: "#3e1a18", // old blood
  R: "#6b2a24", // dried blood
  w: "#8f4a36", // rust
})
ramps("kKmMbB", "kaAsS", "kiIo", "krRw")

// 16x16 swatch: 4x4 blocks, one row per material family.
sprite("palette", { w: 16, h: 16 }, (c) => {
  const keys = "kKmM" + "bBaA" + "sSiI" + "orRw"
  for (let n = 0; n < 16; n++) c.rect((n % 4) * 4, Math.floor(n / 4) * 4, 4, 4, keys[n])
})

// ── 2. Player: ragged hooded hunter, top-down 2.5D (facing camera) ───────────
// Left half (9 cols) mirrored to an 18-wide body. Placed in a 30x30 canvas so the
// blade hangs outside the cloak; the soot outline grows it to 32x32.
// Light comes from the upper left: lit after mirroring so the shading is asymmetric.
// Narrow hood over broad shoulders sells the high camera; arms hang outside
// the cloak, hands bare. Column 0 is the arm, column 1 the shadow beside it.
sprite("_hunter_body", { mx: true }, `
  ......mMM
  .....mMMb
  ....mMMbb
  ....mMbbb
  ...mMMbbB
  ...mMbbbb
  ...mMbkkk
  ...mbkkKK
  ...mbkKSk
  ...mbkKkk
  ...mMkkKK
  ..mmMMkkk
  .mMRRRRRR
  mMRrRRwRR
  IoIIrRrrR
  iIIiMMMbM
  MiIiMMbbM
  MmMmMMbMM
  bmMmMbbMM
  MmmKKKKKK
  bmmKwKKKw
  BmMMMMbMM
  .mMMMbMMm
  .mMMbMMmm
  .mMbMMmMm
  mM.MMm.Mm
  m..mM..m.
  ....KiI..
  ...iiIi..
  ...ii....`)

sprite("player", { w: 30, h: 30, outline: "k" },
  stamp("_hunter_body", 3, 0),
  // upper-left light, lower-right shade: solid steps along diagonal bands
  light([3, 0, 11, 0, 6, 22, 3, 22], 1),
  shade([21, 6, 21, 29, 12, 29, 18, 6], 1),
  // asymmetry: rag torn off the left hem, ash caught on the right shoulder
  px(3, 25, "_"), px(3, 26, "_"), px(4, 26, "_"),
  px(17, 14, "A"), px(18, 14, "s"),
  // notched hunting blade hanging from the right hand
  px(20, 18, "w"), px(20, 19, "w"),   // grip above the fist
  hline(19, 22, 3, "i"),              // guard
  vline(20, 23, 6, "o"),              // edge
  vline(21, 23, 5, "I"),              // flat
  px(20, 29, "o"),                    // tip
  px(21, 26, "_"))                    // notch

// Contact shadow under the feet (Unity draws it semi-transparent below the player).
sprite("player_shadow", { w: 18, h: 5 }, (c) => {
  for (let y = 0; y < 5; y++) for (let x = 0; x < 18; x++) {
    const u = (x - 8.5) / 9, v = (y - 2) / 2.5
    if (u * u + v * v <= 1) c.px(x, y, "k")
  }
})

// ── 3. Floor tiles: mud cobblestones buried in ash (4 interchangeable variants) ──
// Rounded cobbles in staggered rows, wet mud in the gaps. A periodic "ash depth"
// field decides burial: ash fills the low gaps first, then films and finally buries
// the stones; drift crests facing the upper-left light catch the palest ash.
// Variants share the stone layout and the field along a 2px border, and differ only
// inside it (extra drifts, a bare muddy patch, which stones are wet or cracked), so
// any variant can sit next to any other without a seam.
const T = 32
const wrap = (v) => ((v % T) + T) % T
const hash = (x, y) => ((x * 73856093) ^ (y * 19349663)) >>> 0
const TAU = Math.PI * 2
const smooth = (t) => (t <= 0 ? 0 : t >= 1 ? 1 : t * t * (3 - 2 * t))
// 0 on the 2px border, rising to 1 by 8px in: variant changes live only inside.
const inner = (x, y) => {
  const ex = Math.min(wrap(x), T - 1 - wrap(x)), ey = Math.min(wrap(y), T - 1 - wrap(y))
  return smooth((Math.min(ex, ey) - 1) / 6)
}
// Shared field: kept below the stone-burial threshold, so it only varies the joints
// and dusts stones; real drifts come from the per-variant mounds and land in
// different places, which hides the tile repeat.
const baseDepth = (x, y) => 0.7 * (
  0.30 * Math.sin(TAU * (x + 2 * y) / T + 1.0) +
  0.25 * Math.sin(TAU * (3 * x - y) / T + 2.0) +
  0.20 * Math.sin(TAU * (2 * x + 3 * y) / T + 0.5) +
  0.15 * Math.sin(TAU * (-4 * x + 5 * y) / T + 3.0))
// Per-variant mounds (+) and hollows (-): [cx, cy, radius, height]
const VARIANTS = [
  [],                                          // 0: plain, the most common
  [[16, 15, 8, 1.1]],                          // 1: one big drift across the middle
  [[10, 20, 5, 0.9], [22, 10, 4, 0.8]],        // 2: two small drifts
  [[15, 16, 7, -0.9], [24, 23, 3, 0.7]],       // 3: bare mud patch, a little drift beside it
]
const ashDepth = (x, y, v) => {
  let bump = 0
  for (const [cx, cy, r, amp] of VARIANTS[v]) {
    const d2 = ((x - cx) ** 2 + (y - cy) ** 2) / (r * r)
    bump += amp * Math.exp(-d2 * 1.6)
  }
  return baseDepth(x, y) + bump * inner(x, y)
}

for (let v = 0; v < VARIANTS.length; v++) {
  sprite("floor_ash_cobble_" + v, { w: T, h: T }, (c) => {
    // gaps: wet mud, with drier patches
    c.rect(0, 0, T, T, "K")
    for (let y = 0; y < T; y++) for (let x = 0; x < T; x++) if (hash(x, y) % 7 === 0) c.px(x, y, "m")

    // cobbles: 4 rows x 4, every other row shifted half a stone (same layout in every variant)
    let n = 0
    for (let row = 0; row < 4; row++) for (let col = 0; col < 4; col++, n++) {
      const h = hash(row * 7 + 3, col * 13 + 5)
      const cx = col * 8 + (row % 2) * 4 + 3.5 + ((h % 3) - 1) * 0.5
      const cy = row * 8 + 3.5 + (((h >> 3) % 3) - 1) * 0.5
      const rx = 3.9 + (h >> 5) % 4 * 0.12, ry = 3.6 + (h >> 7) % 4 * 0.12
      // stones clear of the border may change per variant; edge stones never do
      const interior = cx - rx > 2 && cx + rx < T - 3 && cy - ry > 2 && cy + ry < T - 3
      const hv = interior ? hash(n + 11, v * 31 + 7) : h
      const dark = (hv >> 9) % 6 === 0                 // a wetter, darker stone now and then
      const [hi, lit, body, low, rim] = dark ? ["b", "M", "m", "K", "K"] : ["B", "b", "M", "m", "K"]
      for (let dy = -4; dy <= 4; dy++) for (let dx = -4; dx <= 4; dx++) {
        const u = (Math.round(cx) + dx - cx) / rx, w = (Math.round(cy) + dy - cy) / ry
        const d = u * u + w * w
        if (d > 1) continue
        const slope = u + w                            // < 0 faces the upper-left light
        let k = body
        if (slope < -0.55) k = lit
        if (slope < -1.05 && d > 0.45) k = hi
        if (slope > 0.6) k = low
        if (w > 0.75 && d > 0.7) k = rim               // contact shadow where it sinks in the mud
        c.px(wrap(Math.round(cx) + dx), wrap(Math.round(cy) + dy), k)
      }
      // a hairline crack on some stones, from the lower-right edge inward
      if (interior ? hv % 4 === 1 : n % 5 === 2) {
        const x = Math.round(cx) + 2, y = Math.round(cy) + 1
        c.px(wrap(x), wrap(y), rim); c.px(wrap(x - 1), wrap(y - 1), rim); c.px(wrap(x - 1), wrap(y - 2), rim)
      }
    }

    // ash burial from the depth field
    for (let y = 0; y < T; y++) for (let x = 0; x < T; x++) {
      const d = ashDepth(x, y, v)
      const facesLight = ashDepth(x - 1, y - 1, v) < d - 0.1
      const powder = hash(x, y) % 3 === 0              // grainy, broken edges
      if (c.is(x, y, "Km")) {
        // joints fill first: loose ash over drifts, mid ash almost everywhere,
        // packed ash in hollows, bare mud only at the very bottom
        if (d > 0.6) c.px(x, y, "s")
        else if (d > -0.35 || (d > -0.5 && !powder)) c.px(x, y, "A")
        else if (d > -0.75 || !powder) c.px(x, y, "a")
      } else {
        // stones only go under where the drift is deep; its powdery rim is mid ash
        if (d > 0.85 && facesLight) c.px(x, y, "S")
        else if (d > 0.65 || (d > 0.57 && !powder)) c.px(x, y, "s")
        else if (d > 0.47 && !powder) c.px(x, y, "A")
      }
    }
  })
}

// Seam check only (not a game asset): variants mixed in a 4x3 block, player on top.
scene("check_floor_mix", { w: 128, h: 96 }, (c) => {
  const order = [0, 1, 0, 2, 3, 0, 2, 0, 0, 2, 1, 3]
  order.forEach((v, i) => c.stamp("floor_ash_cobble_" + v, (i % 4) * T, Math.floor(i / 4) * T))
  c.stamp("player_shadow", 39, 57)
  c.stamp("player", 32, 30)
})
