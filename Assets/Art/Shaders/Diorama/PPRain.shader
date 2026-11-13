Shader "BrumaPx/Diorama/PPRain"
{
    Properties
    {
        _QuadSize ("Quad size in world units (xy)", Vector) = (24.6, 13.9, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry+200" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Rain"
            Cull Off
            ZWrite Off
            ZTest Always
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "PPCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _QuadSize;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            float4 Frag(Varyings i) : SV_Target
            {
                float rain = _PP_Weather.z;
                if (rain <= 0.01) discard;

                // Pixel grid of the render texture, y growing upwards (the quad covers the view, so uv maps to it).
                float2 px = floor((i.uv - 0.5) * _QuadSize.xy * _PP_PPU.x + _ScreenParams.xy * 0.5);
                float t = floor(_Time.y * 14.0) / 14.0;     // stepped, like the mist

                float lane = px.x + floor(px.y * 0.28);     // slanted lanes, so streaks lean in whole pixels
                float h = PP_Hash(float2(lane, 7.31));
                if (PP_Hash(float2(lane, 91.7)) > 0.075 * rain) discard;

                float speed = 90.0 + h * 80.0;
                float period = 80.0 + floor(h * 40.0);
                float len = 6.0 + floor(h * 6.0);
                float ph = fmod(px.y + t * speed + h * period, period);
                if (ph > len) discard;

                float3 col = saturate(_PP_Fog.rgb * 1.45 + 0.10);
                col = lerp(col, col * 0.70, ph / len);      // brightest at the bottom end, which leads
                return float4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
