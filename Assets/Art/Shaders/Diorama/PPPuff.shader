Shader "BrumaPx/Diorama/PPPuff"
{
    // A puff of smoke, steam or a bubble: a camera-facing quad drawn as a ragged dithered blob on the world
    // pixel lattice, so it keeps the pixel look and fades by thinning out. Density per renderer (_Fade, _Seed)
    // comes from a MaterialPropertyBlock set by PuffEmitter.
    Properties
    {
        _Color ("Colour (bottom)", Color) = (0.42, 0.41, 0.40, 1)
        _Color2 ("Colour (top)", Color) = (0.60, 0.59, 0.56, 1)
        _Fade ("Density", Range(0, 1)) = 1
        _Seed ("Seed", Float) = 0
        _Ring ("Ring (bubble outline)", Range(0, 1)) = 0
        _Glow ("Glow (no mist, round, flat)", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Puff"
            Cull Off
            ZWrite On
            ZTest LEqual
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "PPCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _Color2;
                float _Fade;
                float _Seed;
                float _Ring;
                float _Glow;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = v.uv;
                return o;
            }

            float4 Frag(Varyings i) : SV_Target
            {
                float2 lat = PP_Lattice(i.positionWS);
                if (PP_IslandClip(i.positionWS, lat)) discard;
                float2 c = i.uv * 2.0 - 1.0;
                if (_Glow > 0.5)
                {
                    // a firefly: a round dot (core) or a sparse halo, own light, no mist over it
                    float g = length(c);
                    if (g > 1.0) discard;
                    float gb = PP_Bayer(lat);
                    if (gb > _Fade) discard;
                    return float4(lerp(_Color.rgb, _Color2.rgb, step(g, 0.55)), 1.0);
                }
                float r = length(c) + (PP_Noise(c * 2.5 + _Seed) - 0.5) * (0.45 * (1.0 - _Ring));
                if (r > 1.0) discard;
                if (_Ring > 0.5 && r < 0.55) discard;                 // a bubble is only its skin
                // solid in the middle, dithered only at the rim; the whole puff thins out with _Fade
                float bay = PP_Bayer(lat);
                if (r > 0.72 && _Ring < 0.5 && bay > (1.0 - r) / 0.28) discard;
                if (bay > _Fade) discard;
                // lighter on the upper left (the sun), darker underneath
                float t = saturate(c.y * 0.45 - c.x * 0.2 + 0.5 + (PP_Hash(lat) - 0.5) * 0.2);
                t = floor(t * 3.0) / 2.0;
                float3 col = lerp(_Color.rgb, _Color2.rgb, t);
                return float4(PP_Atmos(col, i.positionWS, lat), 1.0);
            }
            ENDHLSL
        }
    }
}
