Shader "BrumaPx/StyleTest/PPGround"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Ground"
            Cull Off
            ZWrite On
            ZTest LEqual
            // Marks the pixels that really are ground, so cast shadows can be limited to them.
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

            // Trodden paths as line segments in world X/Z (ax, az, bx, bz).
            static const float4 PP_SEGS[5] =
            {
                float4(-0.7, -40.0, -0.7, 3.7),     // main path up to the hut door
                float4(-0.7, -10.0, 12.0, -14.0),   // east branch, towards the old graveyard
                float4(12.0, -14.0, 40.0, -11.0),
                float4(-0.7, 2.0, -14.0, 8.0),      // west branch, towards the well
                float4(-14.0, 8.0, -40.0, 20.0)
            };

            float PathDist(float2 p)
            {
                p += float2(sin(p.y * 0.55), cos(p.x * 0.5)) * 0.45;   // wobble so paths are not ruler-straight
                float d = 1e6;
                for (int k = 0; k < 5; k++)
                {
                    float2 a = PP_SEGS[k].xy;
                    float2 b = PP_SEGS[k].zw;
                    float2 ab = b - a;
                    float t = saturate(dot(p - a, ab) / dot(ab, ab));
                    d = min(d, distance(p, a + ab * t));
                }
                return d;
            }

            float4 Frag(Varyings i) : SV_Target
            {
                float3 wp = i.positionWS;
                float2 p = wp.xz;
                // Noise and dither are keyed to the world's pixel lattice, so they scroll with the ground.
                float2 lat = PP_Lattice(wp);
                float hr = PP_Hash(lat);
                float bay = PP_Bayer(lat);

                // Ragged, dithered edge that dissolves into the mist-coloured background. It follows the hero.
                float rough = PP_IslandRough(wp);
                if (rough > 1.04) discard;
                if (rough > 0.86 && hr < (rough - 0.86) / 0.18) discard;

                // Mud body: four tones from the project palette, dithered between each other.
                float large = PP_Fbm(p * 0.45);
                float fine = PP_Noise(p * 2.4);
                float v = large * 0.95 + (fine - 0.5) * 0.30 + (hr - 0.5) * 0.10 - 0.10;
                float q = clamp(floor(v * 4.0 + (bay - 0.5) * 0.6 + 0.5), 0.0, 3.0);

                float3 mud0 = PP_Hex(0x2A2420u);
                float3 mud1 = PP_Hex(0x3D332Bu);
                float3 mud2 = PP_Hex(0x594A3Cu);
                float3 mud3 = PP_Hex(0x7A6650u);
                float3 col = q < 0.5 ? mud0 : (q < 1.5 ? mud1 : (q < 2.5 ? mud2 : mud3));

                // Trodden paths with two dark ruts.
                float pd = PathDist(p);
                float pathW = 0.95 + (PP_Noise(p * 1.3) - 0.5) * 0.6;
                if (pd < pathW)
                {
                    float pv = v + 0.18 + (bay - 0.5) * 0.3;
                    col = pv > 0.62 ? PP_Hex(0x9C8A70u) : PP_Hex(0x7A6650u);
                    if (abs(pd - 0.35) < 0.07) col = mud1;
                }

                // Puddles: a dark rim, then grey-green water with horizontal glints.
                float pud = PP_Fbm(p * 0.33 + 40.0);
                if (pud > 0.628)
                {
                    float streak = step(0.72, PP_Noise(float2(p.x * 2.2, p.y * 6.0) + 7.0));
                    col = pud > 0.648 ? lerp(PP_Hex(0x4F5A5Cu), PP_Hex(0x7D8584u), streak) : mud0;
                }

                // Pebbles.
                if (hr > 0.988) col = PP_Hex(0x6E6C66u);
                else if (PP_Hash(lat - 1.0) > 0.988) col = mud0;

                // Fade towards the mist colour near the edge.
                col = lerp(col, _PP_Fog.rgb, smoothstep(0.72, 1.0, rough) * 0.45);
                return float4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
