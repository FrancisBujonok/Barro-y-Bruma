Shader "BrumaPx/StyleTest/PPToon"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.35, 0.29, 0.24, 1)
        _Levels ("Light Levels", Range(2, 6)) = 4
        _Ambient ("Ambient", Range(0, 1)) = 0.2
        _Dither ("Dither (0 = flat bands)", Range(0, 1)) = 0.85
        _Grain ("Surface grain", Range(0, 0.6)) = 0.12
        _GrainScale ("Grain scale", Float) = 3
        _Emissive ("Emissive (flat colour, no shading)", Range(0, 1)) = 0
        _OutlineColor ("Outline colour", Color) = (0.08, 0.07, 0.06, 1)
        _OutlineTexels ("Outline width (render texels)", Range(0, 3)) = 1
        _CastShadow ("Cast shadow (builder adds the PPShadow slot)", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "PPCommon.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float _Levels;
            float _Ambient;
            float _Dither;
            float _Grain;
            float _GrainScale;
            float _Emissive;
            float4 _OutlineColor;
            float _OutlineTexels;
            float _CastShadow;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float3 normalWS : TEXCOORD1;
        };

        Varyings VertToon(Attributes v)
        {
            Varyings o;
            o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
            o.normalWS = TransformObjectToWorldNormal(v.normalOS);
            o.positionCS = TransformWorldToHClip(o.positionWS);
            return o;
        }

        // Inverted hull: push every vertex away from the object centre by N render texels.
        // Works for the convex, origin-centred primitives this test scene is built from.
        Varyings VertOutline(Attributes v)
        {
            Varyings o;
            float3 pws = TransformObjectToWorld(v.positionOS.xyz);
            float3 dirWS = TransformObjectToWorldDir(normalize(v.positionOS.xyz + 1e-5), true);
            float4 cs = TransformWorldToHClip(pws);
            float4 cs2 = TransformWorldToHClip(pws + dirWS);
            float2 dir = normalize(cs2.xy / cs2.w - cs.xy / cs.w + float2(1e-5, 1e-5));
            cs.xy += dir * _OutlineTexels * (2.0 / _ScreenParams.xy) * cs.w;
            o.positionCS = cs;
            o.positionWS = pws;
            o.normalWS = float3(0, 1, 0);
            return o;
        }

        float4 FragOutline(Varyings i) : SV_Target
        {
            if (PP_IslandClip(i.positionWS, PP_Lattice(i.positionWS))) discard;
            return float4(_OutlineColor.rgb, 1.0);
        }

        float4 FragToon(Varyings i) : SV_Target
        {
            float2 lat = PP_Lattice(i.positionWS);
            if (PP_IslandClip(i.positionWS, lat)) discard;

            float3 baseCol = _BaseColor.rgb;
            if (_Emissive > 0.5) return float4(baseCol, 1.0);

            float3 N = normalize(i.normalWS);
            // Fixed shading light: high and from the left, so top = bright, left face = mid, right face = dark.
            float3 SL = normalize(float3(-0.30, 0.80, 0.35));
            float ndl = saturate(dot(N, SL) * 1.1);
            float v = _Ambient + (1.0 - _Ambient) * ndl;

            float2 gp = float2(i.positionWS.x + i.positionWS.z, i.positionWS.y * 1.5) * _GrainScale;
            v += (PP_Noise(gp) - 0.5) * _Grain;

            float steps = max(_Levels - 1.0, 1.0);
            float bay = PP_Bayer(lat);
            float q = clamp(floor(v * steps + (bay - 0.5) * _Dither + 0.5), 0.0, steps);
            return float4(PP_Ramp(baseCol, q / steps), 1.0);
        }
        ENDHLSL

        // The URP 2D Renderer draws one pass per LightMode tag it knows (untagged = SRPDefaultUnlit, Universal2D),
        // so the outline and the shading are tagged differently. Shadows live in a separate material (PPShadow).
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
