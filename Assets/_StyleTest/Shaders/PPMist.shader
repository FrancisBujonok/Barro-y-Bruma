Shader "BrumaPx/StyleTest/PPMist"
{
    Properties
    {
        _Tint ("Tint (multiplies the fog colour)", Color) = (0.94, 0.95, 0.97, 1)
        _Density ("Density", Range(0, 1)) = 0.55
        _Threshold ("Noise threshold", Range(0, 1)) = 0.42
        _Scale ("Noise scale", Float) = 0.35
        _Speed ("Drift speed", Float) = 0.4
        _Seed ("Seed", Float) = 0
        _ClearRadius ("Clear radius around hero", Float) = 3.0
        _ClearSoft ("Clear softness", Float) = 3.0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry+1" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Mist"
            Cull Off
            ZWrite On
            ZTest LEqual
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "PPCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float _Density;
                float _Threshold;
                float _Scale;
                float _Speed;
                float _Seed;
                float _ClearRadius;
                float _ClearSoft;
            CBUFFER_END

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

            float4 Frag(Varyings i) : SV_Target
            {
                // Time steps at 8 fps on purpose, so the drift feels hand-animated.
                float t = floor(_Time.y * 8.0) / 8.0;
                float2 p = i.positionWS.xz * _Scale + float2(t, t * 0.35) * _Speed + _Seed;
                float n = PP_Fbm(p);
                float dens = smoothstep(_Threshold, _Threshold + 0.22, n) * _Density;

                float d = distance(i.positionWS.xz, _PP_Focus.xz);
                dens *= smoothstep(_ClearRadius, _ClearRadius + _ClearSoft, d);

                // Fade out towards the edges of the render texture so the frame never shows as a rectangle.
                float2 uv = i.positionCS.xy / _ScreenParams.xy;
                float edgeDist = min(min(uv.x, 1.0 - uv.x), min(uv.y, 1.0 - uv.y));
                dens *= smoothstep(0.0, 0.10, edgeDist);

                if (dens <= PP_Bayer(PP_Lattice(i.positionWS))) discard;
                return float4(_PP_Fog.rgb * _Tint.rgb, 1.0);
            }
            ENDHLSL
        }
    }
}
