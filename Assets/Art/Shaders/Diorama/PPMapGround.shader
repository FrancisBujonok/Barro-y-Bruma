Shader "BrumaPx/Diorama/PPMapGround"
{
    // Ground for the chapter map: the terrain type of every 2 m cell comes from a texture (_PP_MapTex, set by MapWorld),
    // each type gets its own four-tone dithered palette, and the borders between types are warped so they are not square.
    // Water is drawn below the ground, with banks, current, foam and ash floating on it (see "water below the ground").
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Ground"
            Cull Off
            ZWrite On
            ZTest LEqual
            Stencil
            {
                Ref 2
                WriteMask 2
                Comp Always
                Pass Replace
            }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "PPCommon.hlsl"

            TEXTURE2D(_PP_MapTex);
            float4 _PP_MapInfo;   // x: xMin, y: zMin (south edge), z: columns, w: rows

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }

            // Four tones per terrain id: l b d p a c k e m r v, and 11 cave (carved by CaveLair)
            static const uint PAL[48] =
            {
                0x3A3A2Au, 0x4E4B34u, 0x666043u, 0x7E7752u,   // 0 llanura
                0x1F2A1Fu, 0x2B3A2Au, 0x3B4A33u, 0x4F5C40u,   // 1 bosque
                0x2A2724u, 0x3A3631u, 0x4D473Fu, 0x645C50u,   // 2 bosque muerto
                0x1D2622u, 0x28342Eu, 0x364438u, 0x4A5A4Au,   // 3 pantano
                0x2C3A40u, 0x3A4C54u, 0x4F6068u, 0x6A7C80u,   // 4 agua
                0x6A5744u, 0x7A6650u, 0x8E7B60u, 0x9C8A70u,   // 5 camino
                0x3A2E22u, 0x4A3C2Au, 0x5E4A32u, 0x6E593Eu,   // 6 campo
                0x35332Fu, 0x4A4741u, 0x5F5B54u, 0x77736Au,   // 7 escarpado
                0x2B2A29u, 0x3D3B38u, 0x524F4Bu, 0x6A665Fu,   // 8 montana
                0x3B3733u, 0x544F48u, 0x6E6A60u, 0x8A8578u,   // 9 ruinas
                0x4A3B2Cu, 0x5E4A36u, 0x735C42u, 0x85694Au,   // 10 aldea
                0x141210u, 0x1F1C1Au, 0x2A2724u, 0x3A3631u    // 11 cueva
            };

            int CellId(float2 p)
            {
                int ix = clamp((int)floor((p.x - _PP_MapInfo.x) * 0.5), 0, (int)_PP_MapInfo.z - 1);
                int iy = clamp((int)floor((p.y - _PP_MapInfo.y) * 0.5), 0, (int)_PP_MapInfo.w - 1);
                return (int)round(LOAD_TEXTURE2D(_PP_MapTex, int2(ix, iy)).r * 255.0);
            }

            // ---------------------------------------------------------------- water below the ground
            // Water (4) and the decks over it (12: bridges, piers) are a hole in the ground. Through the hole the
            // shader looks down along the camera ray: it meets either the water surface WATER_DEPTH below, or first
            // the earth bank of the far shore. Depth is written for that point, so bridge piers and posts that go down
            // into the water show, and the near shore hides the water under its lip as a real bank would.
            static const float WATER_DEPTH = 0.8;

            float2 BorderWarp(float2 p)
            {
                return (float2(PP_Noise(p * 0.7 + 11.0), PP_Noise(p * 0.7 + 53.0)) - 0.5) * 0.8;
            }

            bool IsWaterId(int id) { return id == 4 || id == 12; }
            bool IsWater(float2 p) { return IsWaterId(CellId(p + BorderWarp(p))); }

            float3 WaterPal(int k)
            {
                // deep to light: the four water tones of the map, then foam
                if (k <= 0) return PP_Hex(0x1D2A2Fu);
                if (k == 1) return PP_Hex(0x2C3A40u);
                if (k == 2) return PP_Hex(0x3A4C54u);
                if (k == 3) return PP_Hex(0x4F6068u);
                if (k == 4) return PP_Hex(0x6A7C80u);
                return PP_Hex(0x9A968Cu);
            }

            float3 BankPal(int k)
            {
                if (k <= 0) return PP_Hex(0x1F1C1Au);
                if (k == 1) return PP_Hex(0x2A2420u);
                if (k == 2) return PP_Hex(0x3D332Bu);
                return PP_Hex(0x594A3Cu);
            }

            // Flow along the river: water on both sides along one axis and land across it. Open water stays still.
            float2 FlowDir(float2 w, out float still)
            {
                bool n = IsWaterId(CellId(w + float2(0.0, 5.0))), s = IsWaterId(CellId(w - float2(0.0, 5.0)));
                bool e = IsWaterId(CellId(w + float2(5.0, 0.0))), o = IsWaterId(CellId(w - float2(5.0, 0.0)));
                bool alongZ = n && s, alongX = e && o;
                still = (alongZ && alongX) ? 1.0 : 0.0;
                if (alongX && !alongZ) return float2(1.0, 0.0);
                return float2(0.0, -1.0);   // the Rio Ceniza runs south, towards the lake
            }

            // Colour of the water surface at w (world xz). Returns the colour; lat is the screen lattice for dither.
            float3 WaterColour(float2 w, float2 lat, float2 fwdXZ)
            {
                float t = _Time.y;
                float bay = PP_Bayer(lat);
                float still;
                float2 f = FlowDir(w, still);
                float2 side = float2(-f.y, f.x);
                float u = dot(w, f), s = dot(w, side);
                float speed = lerp(0.9, 0.0, still);
                float um = u - t * speed;                    // coordinate that travels with the current

                // slow large patches drifting with the current, and long ripples stretched along it
                float v = PP_Fbm(float2(s * 0.35, um * 0.22) + 5.0) * 0.6 + 0.12;
                float rip = PP_Noise(float2(s * 2.4, um * 0.55));
                float rip2 = PP_Noise(float2(s * 1.3 + 9.0, (u - t * speed * 1.6) * 0.8));
                if (still > 0.5)
                {
                    // open water: lazy cross ripples that come and go
                    rip = PP_Noise(w * 1.1 + float2(t * 0.12, -t * 0.07));
                    rip2 = PP_Noise(w * 0.7 - float2(t * 0.05, t * 0.1) + 31.0);
                }
                v += step(0.76, rip) * 0.24 + step(0.82, rip2) * 0.14;

                // the far bank throws a strip of shadow on the water; foam where the water meets it
                bool wallAhead = !IsWater(w + fwdXZ * 0.55);
                if (wallAhead) v -= 0.22;
                float edge = 0.0;
                if (!IsWater(w + float2(0.32, 0.0)) || !IsWater(w - float2(0.32, 0.0)) ||
                    !IsWater(w + float2(0.0, 0.32)) || !IsWater(w - float2(0.0, 0.32))) edge = 1.0;

                float q = clamp(floor(v * 4.0 + (bay - 0.5) * 0.7 + 0.5), 0.0, 3.0) + 1.0;
                float3 col = WaterPal((int)q);
                if (edge > 0.5 && PP_Noise(float2(s * 3.0, um * 2.0) + t * 0.3) > 0.45) col = WaterPal(bay > 0.5 ? 5 : 4);

                // glints that blink, and flakes of ash riding the current
                if (PP_Hash(lat + floor(t * 3.0) * 7.0) > 0.992 && v > 0.35) col = WaterPal(5);
                float2 fl = floor(float2(s * 2.5, um * 2.5));
                if (PP_Hash(fl + 3.7) > 0.985) col = PP_Hex(0x2A2724u);
                else if (PP_Hash(fl + 9.1) > 0.993) col = PP_Hex(0x645C50u);
                return col;
            }

            float3 BankColour(float2 q, float d, float2 lat)
            {
                float bay = PP_Bayer(lat);
                float along = q.x + q.y;
                float v = 0.55 + (PP_Noise(float2(along * 1.6, d * 9.0)) - 0.5) * 0.5;
                v -= step(0.6, frac(d * 7.0 + PP_Noise(float2(along * 0.8, 3.0)) * 1.5)) * 0.18;   // layers of earth
                v -= step(0.82, PP_Noise(float2(along * 5.0, 1.0))) * 0.3;                          // roots and cracks
                if (d < 0.08) v += 0.25;                                                            // the lip
                if (d > WATER_DEPTH - 0.14) v -= 0.35;                                              // wet foot
                float k = clamp(floor(v * 3.0 + (bay - 0.5) * 0.7 + 0.5), 0.0, 3.0);
                float3 col = BankPal((int)k);
                if (d > WATER_DEPTH - 0.05 && PP_Noise(float2(along * 3.0, _Time.y * 0.8)) > 0.5) col = WaterPal(4);   // lapping foam
                return col;
            }

            // ---------------------------------------------------------------- ground detail
            // Drawn on the world-anchored screen-pixel lattice (lat), so every mark is whole pixels and stays put.

            float3 Tone(int id, int k) { return PP_Hex(PAL[min(id, 11) * 4 + clamp(k, 0, 3)]); }

            // Tufts of grass: tiny hand-made sprites (5x4 px, 2 bits a pixel: 0 none, 1 dark root, 2 mid, 3 light tip)
            // dropped on a 7x6 px grid of the world-anchored pixel lattice, one at most per grid cell.
            static const uint4 TUFT[4] =
            {
                uint4(84u, 184u, 907u, 48u),
                uint4(80u, 744u, 35u, 48u),
                uint4(20u, 59u, 200u, 3u),
                uint4(325u, 906u, 140u, 192u)
            };

            int Blade(float2 lat, float density, float maxLen, float seed)
            {
                float2 cell = floor(lat / float2(7.0, 6.0));
                float h = PP_Hash(cell + seed * 7.1);
                if (h > density) return 0;
                float2 jit = floor(float2(PP_Hash(cell + 3.3 + seed), PP_Hash(cell + 8.7 + seed)) * float2(3.0, 3.0));
                int2 lp = (int2)(lat - cell * float2(7.0, 6.0) - jit);
                if (lp.x < 0 || lp.x > 4 || lp.y < 0 || lp.y > 3) return 0;
                int k = min((int)(PP_Hash(cell + 5.9 + seed) * maxLen), 3);
                uint4 sp = TUFT[k];
                uint row = lp.y == 0 ? sp.x : (lp.y == 1 ? sp.y : (lp.y == 2 ? sp.z : sp.w));
                uint v = (row >> (uint)(lp.x * 2)) & 3u;
                return v == 0u ? 0 : (v == 1u ? 3 : (v == 2u ? 1 : 2));
            }

            bool IsRoadAt(float2 p) { return CellId(p) == 5; }

            // Cart ruts on straight stretches of road: 1 in a rut, 2 on the grassy strip between them.
            int Rut(float2 p, out float wet)
            {
                wet = 0.0;
                bool rx = IsRoadAt(p + float2(3.0, 0.0)) && IsRoadAt(p - float2(3.0, 0.0)) && IsRoadAt(p + float2(6.0, 0.0)) && IsRoadAt(p - float2(6.0, 0.0));
                bool rz = IsRoadAt(p + float2(0.0, 3.0)) && IsRoadAt(p - float2(0.0, 3.0)) && IsRoadAt(p + float2(0.0, 6.0)) && IsRoadAt(p - float2(0.0, 6.0));
                if (rx == rz) return 0;                            // a crossing, a bend, or a lone cell
                float2 across = rx ? float2(0.0, 1.0) : float2(1.0, 0.0);
                float lo = 0.0, hi = 0.0;
                [loop] for (int k = 1; k <= 12; k++) { if (!IsRoadAt(p - across * (k * 0.5))) break; lo = k * 0.5; }
                [loop] for (int j = 1; j <= 12; j++) { if (!IsRoadAt(p + across * (j * 0.5))) break; hi = j * 0.5; }
                if (lo + hi < 2.4 || lo + hi > 5.6) return 0;
                float centre = (hi - lo) * 0.5;                    // offset of the road centre from p, across
                float along = dot(p, rx ? float2(1.0, 0.0) : float2(0.0, 1.0));
                float wob = (PP_Noise(float2(along * 0.15, 3.0)) - 0.5) * 0.5;
                float d = abs(-centre + wob);
                if (abs(d - 0.75) < 0.17)
                {
                    wet = step(0.66, PP_Fbm(float2(along * 0.4, 9.0)));
                    return 1;
                }
                if (d < 0.32) return 2;
                return 0;
            }

            float3 GroundDetail(int id, float2 p, float2 lat, float3 col, int q)
            {
                float clump = PP_Noise(p * 0.55 + 13.0);
                float hr2 = PP_Hash(lat + 71.0);
                if (id == 0 || id == 1 || id == 3 || id == 10)
                {
                    float dens = id == 0 ? lerp(0.10, 0.75, clump) : (id == 1 ? lerp(0.08, 0.5, clump) : (id == 3 ? lerp(0.12, 0.6, clump) : lerp(0.0, 0.3, clump)));
                    int b = Blade(lat, dens, 4.0, (float)id);
                    if (b == 3) col = Tone(id, 0);
                    else if (b == 1) col = Tone(id, 2);
                    else if (b == 2) col = id == 0 ? PP_Hex(0x948C5Eu) : (id == 1 ? PP_Hex(0x627048u) : Tone(id, 3));
                }
                if (id == 0)
                {
                    // flowers in patches
                    float patch = PP_Noise(p * 0.3 + 5.0);
                    if (patch > 0.68 && hr2 > 0.975)
                    {
                        float kind = PP_Hash(floor(p * 0.25));
                        col = kind < 0.4 ? PP_Hex(0xD2AC88u) : (kind < 0.75 ? PP_Hex(0xC4BFB2u) : PP_Hex(0x8A7AB0u));
                    }
                }
                else if (id == 1)
                {
                    // fallen leaves and moss
                    float leaf = PP_Noise(p * 0.4 + 21.0);
                    if (leaf > 0.62 && hr2 > 0.965)
                    {
                        float k = PP_Hash(lat + 5.0);
                        col = k < 0.55 ? PP_Hex(0x594A3Cu) : (k < 0.85 ? PP_Hex(0x6B2A24u) : PP_Hex(0x8F4A36u));
                    }
                    if (PP_Fbm(p * 0.7 + 3.0) > 0.66) col = lerp(col, PP_Hex(0x3B4A33u), 0.5);
                }
                else if (id == 2)
                {
                    // dry tufts, twigs and pale ash
                    int b = Blade(lat, lerp(0.05, 0.35, clump), 4.0, 2.0);
                    if (b == 3) col = Tone(2, 0);
                    else if (b == 1 || b == 2) col = PP_Hex(0x7A6650u);
                    float tw = PP_Hash(floor((lat + float2(lat.y, 0.0)) / float2(3.0, 1.0)));
                    if (tw > 0.985) col = PP_Hex(0x1F1C1Au);
                    else if (hr2 > 0.99) col = PP_Hex(0x9A968Cu);
                }
                else if (id == 3)
                {
                    // duckweed specks
                    if (hr2 > 0.96 && PP_Noise(p * 0.8) > 0.55) col = hr2 > 0.985 ? PP_Hex(0x666043u) : PP_Hex(0x4F5C40u);
                }
                else if (id == 5)
                {
                    float wet;
                    int rut = Rut(p, wet);
                    if (rut == 1) col = wet > 0.5 ? (PP_Hash(lat + 2.0) > 0.85 ? PP_Hex(0x6A7C80u) : PP_Hex(0x3A4C54u)) : Tone(5, 0);
                    else if (rut == 2)
                    {
                        int b = Blade(lat, 0.45, 4.0, 5.0);
                        if (b == 3) col = Tone(5, 0);
                        else if (b != 0) col = PP_Hex(0x666043u);
                    }
                    // pebbles: a light pixel with its shadow under it
                    if (PP_Hash(lat + 33.0) > 0.982) col = PP_Hash(lat + 34.0) > 0.5 ? PP_Hex(0x9A968Cu) : PP_Hex(0x6E6C66u);
                    else if (PP_Hash(lat + float2(0.0, 1.0) + 33.0) > 0.982) col = Tone(5, 0);
                }
                else if (id == 6)
                {
                    int b = Blade(lat, lerp(0.05, 0.3, clump), 4.0, 6.0);
                    if (b == 3) col = Tone(6, 0);
                    else if (b != 0) col = PP_Hex(0x9C8A70u);
                }
                return col;
            }

            float3 Finish(float3 col, float3 wp, float2 lat, float rough)
            {
                col *= lerp(1.0, 0.82, _PP_Weather.w);
                col = PP_Atmos(col, wp, lat);
                return lerp(col, _PP_Fog.rgb, smoothstep(0.72, 1.0, rough) * 0.45);
            }

            float4 Frag(Varyings i, out float depthOut : SV_Depth) : SV_Target
            {
                float3 wp = i.positionWS;
                float2 p = wp.xz;
                float2 lat = PP_Lattice(wp);
                float hr = PP_Hash(lat);
                float bay = PP_Bayer(lat);
                depthOut = i.positionCS.z;

                float rough = PP_IslandRough(wp);
                if (rough > 1.04) discard;
                if (rough > 0.86 && hr < (rough - 0.86) / 0.18) discard;

                // Ragged borders: look the terrain up at a slightly warped position.
                float2 warp = float2(PP_Noise(p * 0.7 + 11.0), PP_Noise(p * 0.7 + 53.0)) - 0.5;
                int id = CellId(p + warp * 0.8);

                if (IsWaterId(id))
                {
                    // follow the camera ray down: bank of the far shore, or the water surface
                    float3 fwd = normalize(cross(_PP_CamRight.xyz, _PP_CamUp.xyz));
                    float2 off = fwd.xz * (WATER_DEPTH / max(-fwd.y, 0.2));
                    float2 fwdXZ = normalize(fwd.xz);
                    float tHit = 1.0;
                    float tPrev = 0.0;
                    [loop] for (int k = 1; k <= 10; k++)
                    {
                        float tk = k / 10.0;
                        if (!IsWater(p + off * tk)) { tHit = tk; break; }
                        tPrev = tk;
                    }
                    float3 hit;
                    float3 col;
                    if (tHit < 1.0)
                    {
                        float tm = (tPrev + tHit) * 0.5;
                        if (!IsWater(p + off * tm)) tHit = tm; else tPrev = tm;
                        float tw = (tPrev + tHit) * 0.5;
                        float d = WATER_DEPTH * tw;
                        float2 q = p + off * tw;
                        hit = float3(q.x, -d, q.y);
                        col = BankColour(q, d, lat);
                    }
                    else
                    {
                        float2 w = p + off;
                        hit = float3(w.x, -WATER_DEPTH, w.y);
                        col = WaterColour(w, lat, fwdXZ);
                    }
                    float4 hc = TransformWorldToHClip(hit);
                    depthOut = hc.z / hc.w;
                    return float4(Finish(col, hit, lat, rough), 1.0);
                }

                float large = PP_Fbm(p * 0.45);
                float fine = PP_Noise(p * 2.4);
                float v = large * 0.95 + (fine - 0.5) * 0.30 + (hr - 0.5) * 0.10 - 0.10;

                if (id == 5) v = v * 0.6 + 0.30;
                if (id == 4) v = v * 0.5 + 0.28 + step(0.78, PP_Noise(float2(p.x * 1.6, p.y * 5.0 + _Time.y * 0.15))) * 0.3;
                if (id == 6)
                {
                    float2 dir = float2(0.7071, 0.7071);
                    float f = frac(dot(p, dir) / 0.9);
                    v = v * 0.5 + 0.18 + (f < 0.5 ? 0.0 : 0.28);
                }
                if (id == 9)
                {
                    float2 cobble = frac(p * 1.25 + float2(step(0.5, frac(floor(p.y * 1.25) * 0.5)) * 0.5, 0.0));
                    float joint = (cobble.x < 0.12 || cobble.y < 0.12) ? -0.28 : 0.0;
                    v = v * 0.7 + 0.22 + joint;
                }
                if (id == 7)
                {
                    float strata = step(0.55, frac(p.y * 0.9 + PP_Noise(p * 0.5) * 2.0));
                    v = v * 0.8 + 0.1 + strata * 0.18;
                }

                if (id == 11)
                {
                    // broken slabs with dark seams
                    float2 slab = frac(p * 0.55 + PP_Noise(p * 0.3) * 0.6);
                    float seam = (slab.x < 0.08 || slab.y < 0.08) ? -0.3 : 0.0;
                    v = v * 0.7 + 0.12 + seam;
                }
                float q = clamp(floor(v * 4.0 + (bay - 0.5) * 0.6 + 0.5), 0.0, 3.0);
                float3 col = PP_Hex(PAL[min(id, 11) * 4 + (int)q]);
                col = GroundDetail(id, p, lat, col, (int)q);
                if (id == 11)
                {
                    // standing water that shines a little
                    float pud = PP_Fbm(p * 0.4 + 17.0);
                    if (pud > 0.63) col = pud > 0.66 && PP_Noise(float2(p.x * 3.0, p.y * 7.0)) > 0.8 ? PP_Hex(0x4F5A5Cu) : PP_Hex(0x161A1Bu);
                }

                if (id == 3)
                {
                    float pud = PP_Fbm(p * 0.33 + 40.0);
                    if (pud > 0.60)
                    {
                        float streak = step(0.72, PP_Noise(float2(p.x * 2.2, p.y * 6.0) + 7.0));
                        col = pud > 0.625 ? lerp(PP_Hex(0x4F5A5Cu), PP_Hex(0x7D8584u), streak) : PP_Hex(0x1D2622u);
                    }
                }
                if (id != 4)
                {
                    if (hr > 0.988) col = PP_Hex(0x6E6C66u);
                    else if (PP_Hash(lat - 1.0) > 0.988) col = PP_Hex(PAL[min(id, 11) * 4]);
                }

                return float4(Finish(col, wp, lat, rough), 1.0);
            }
            ENDHLSL
        }
    }
}
