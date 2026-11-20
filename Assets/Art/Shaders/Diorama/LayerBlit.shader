Shader "Hidden/BrumaPx/LayerBlit"
{
    // Used by CharacterCompositor: draws one layer sheet over the texture being composed. Pixels are written whole
    // (no blending), so the alpha that reaches the composed texture is exactly the one written here:
    // 1 for skin and cloth, 0.94 for layers that cover the body (_Cover = 1). PPSprite reads that alpha to know
    // where wounds and Bruma may show.
    Properties
    {
        _MainTex ("Layer", 2D) = "black" {}
        _Cover ("Covers the body", Float) = 0
    }

    SubShader
    {
        Pass
        {
            ZTest Always
            ZWrite Off
            Cull Off
            Blend One Zero

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SamplerState bruma_point_clamp_sampler;
            float _Cover;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = float4(v.positionOS.xy * 2.0 - 1.0, 0.0, 1.0);
                o.uv = v.uv;
                #if UNITY_UV_STARTS_AT_TOP
                o.positionCS.y = -o.positionCS.y;
                #endif
                return o;
            }

            float4 Frag(Varyings i) : SV_Target
            {
                float4 c = _MainTex.Sample(bruma_point_clamp_sampler, i.uv);
                clip(c.a - 0.5);
                return float4(c.rgb, _Cover > 0.5 ? 0.94 : 1.0);
            }
            ENDHLSL
        }
    }
}
