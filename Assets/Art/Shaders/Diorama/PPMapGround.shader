Shader "BrumaPx/Diorama/PPMapGround"
{
    // Ground for the chapter map: the terrain type of every 2 m cell comes from a texture (_PP_MapTex, set by MapWorld),
    // each type gets its own four-tone dithered palette, and the borders between types are warped so they are not square.
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

            // Four tones per terrain id: l b d p a c k e m r v
            static const uint PAL[44] =
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
                0x4A3B2Cu, 0x5E4A36u, 0x735C42u, 0x85694Au    // 10 aldea
            };

            int CellId(float2 p)
            {
                int ix = clamp((int)floor((p.x - _PP_MapInfo.x) * 0.5), 0, (int)_PP_MapInfo.z - 1);
                int iy = clamp((int)floor((p.y - _PP_MapInfo.y) * 0.5), 0, (int)_PP_MapInfo.w - 1);
                return (int)round(LOAD_TEXTURE2D(_PP_MapTex, int2(ix, iy)).r * 255.0);
            }

            float4 Frag(Varyings i) : SV_Target
            {
                float3 wp = i.positionWS;
                float2 p = wp.xz;
                float2 lat = PP_Lattice(wp);
                float hr = PP_Hash(lat);
                float bay = PP_Bayer(lat);

                float rough = PP_IslandRough(wp);
                if (rough > 1.04) discard;
                if (rough > 0.86 && hr < (rough - 0.86) / 0.18) discard;

                // Ragged borders: look the terrain up at a slightly warped position.
                float2 warp = float2(PP_Noise(p * 0.7 + 11.0), PP_Noise(p * 0.7 + 53.0)) - 0.5;
                int id = CellId(p + warp * 0.8);

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

                float q = clamp(floor(v * 4.0 + (bay - 0.5) * 0.6 + 0.5), 0.0, 3.0);
                float3 col = PP_Hex(PAL[min(id, 10) * 4 + (int)q]);

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
                    else if (PP_Hash(lat - 1.0) > 0.988) col = PP_Hex(PAL[min(id, 10) * 4]);
                }

                col *= lerp(1.0, 0.82, _PP_Weather.w);
                col = PP_Atmos(col, wp, lat);
                col = lerp(col, _PP_Fog.rgb, smoothstep(0.72, 1.0, rough) * 0.45);
                return float4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
