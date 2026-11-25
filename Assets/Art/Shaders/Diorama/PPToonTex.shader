Shader "BrumaPx/Diorama/PPToonTex"
{
    // PPToon with a palette texture (baked in Blender, point-sampled) instead of a flat colour, an outline that
    // follows smoothed normals kept in the vertex colour (works on any mesh, not only convex primitives), and a
    // dithered cut-away (_Cut, set per renderer) so the roof and the walls facing the camera can dissolve when the
    // hero walks into a building.
    Properties
    {
        _MainTex ("Palette texture", 2D) = "white" {}
        _Levels ("Light Levels", Range(2, 6)) = 4
        _Ambient ("Ambient", Range(0, 1)) = 0.3
        _Dither ("Dither (0 = flat bands)", Range(0, 1)) = 0.85
        _Emissive ("Emissive (flat colour, no shading)", Range(0, 1)) = 0
        _Tint ("Tint (multiplies the texture)", Color) = (1, 1, 1, 1)
        _OutlineColor ("Outline colour", Color) = (0.08, 0.07, 0.06, 1)
        _OutlineTexels ("Outline width (render texels)", Range(0, 3)) = 1
        _Cut ("Cut-away (0 = solid, 1 = gone)", Range(0, 1)) = 0
        _WarmMix ("How much the lit side turns to the warm light colour", Range(0, 1)) = 0.3
        _CoolMix ("How much the shaded side turns to the cool shadow colour", Range(0, 1)) = 0.4
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "PPCommon.hlsl"

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float _Levels;
            float _Ambient;
            float _Dither;
            float _Emissive;
            float4 _Tint;
            float4 _OutlineColor;
            float _OutlineTexels;
            float _Cut;
            float _WarmMix;
            float _CoolMix;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            float4 color : COLOR;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float3 normalWS : TEXCOORD1;
            float2 uv : TEXCOORD2;
        };

        // Dissolves in a 4x4 Bayer pattern anchored to the world lattice, so it reads as pixel art.
        bool CutAway(float2 lat)
        {
            return _Cut > 0.001 && PP_Bayer(lat) < _Cut * 1.0001;
        }

        Varyings VertToon(Attributes v)
        {
            Varyings o;
            o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
            o.normalWS = TransformObjectToWorldNormal(v.normalOS);
            o.positionCS = TransformWorldToHClip(o.positionWS);
            o.uv = v.uv;
            return o;
        }

        Varyings VertOutline(Attributes v)
        {
            Varyings o;
            float3 pws = TransformObjectToWorld(v.positionOS.xyz);
            float3 n = v.color.rgb * 2.0 - 1.0;
            if (dot(n, n) < 0.01) n = v.normalOS;
            float3 dirWS = TransformObjectToWorldDir(normalize(n), true);
            float4 cs = TransformWorldToHClip(pws);
            float4 cs2 = TransformWorldToHClip(pws + dirWS);
            float2 dir = normalize(cs2.xy / cs2.w - cs.xy / cs.w + float2(1e-5, 1e-5));
            cs.xy += dir * _OutlineTexels * (2.0 / _ScreenParams.xy) * cs.w;
            o.positionCS = cs;
            o.positionWS = pws;
            o.normalWS = float3(0, 1, 0);
            o.uv = v.uv;
            return o;
        }

        float4 FragOutline(Varyings i) : SV_Target
        {
            float2 lat = PP_Lattice(i.positionWS);
            if (PP_IslandClip(i.positionWS, lat) || CutAway(lat)) discard;
            return float4(_OutlineColor.rgb * _PP_Tone.rgb, 1.0);
        }

        // PP_Ramp with the warm and cool amounts per material: dark greens washed 30 % towards the warm light
        // colour turn tan, so leaves use less.
        float3 RampMix(float3 c, float t)
        {
            float3 dark = lerp(c * 0.55, _PP_Cool.rgb, _CoolMix);
            float3 light = lerp(c * 1.10, _PP_Warm.rgb, _WarmMix);
            return t < 0.5 ? lerp(dark, c, t * 2.0) : lerp(c, light, (t - 0.5) * 2.0);
        }

        float4 FragToon(Varyings i) : SV_Target
        {
            float2 lat = PP_Lattice(i.positionWS);
            if (PP_IslandClip(i.positionWS, lat) || CutAway(lat)) discard;

            float3 baseCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).rgb * _Tint.rgb;
            if (_Emissive > 0.5) return float4(baseCol, 1.0);

            float3 N = normalize(i.normalWS);
            float3 SL = normalize(float3(-0.30, 0.80, 0.35));
            float ndl = saturate(dot(N, SL) * 1.1);
            float v = _Ambient + (1.0 - _Ambient) * ndl;

            float steps = max(_Levels - 1.0, 1.0);
            float bay = PP_Bayer(lat);
            float q = clamp(floor(v * steps + (bay - 0.5) * _Dither + 0.5), 0.0, steps);
            return float4(PP_Atmos(RampMix(baseCol, q / steps), i.positionWS, lat), 1.0);
        }
        ENDHLSL

        Pass
        {
            Name "Outline"
            Cull Front
            ZWrite On
            ZTest LEqual
            HLSLPROGRAM
            #pragma vertex VertOutline
            #pragma fragment FragOutline
            ENDHLSL
        }

        Pass
        {
            Name "Toon"
            Tags { "LightMode" = "Universal2D" }
            Cull Back
            ZWrite On
            ZTest LEqual
            HLSLPROGRAM
            #pragma vertex VertToon
            #pragma fragment FragToon
            ENDHLSL
        }
    }
}
