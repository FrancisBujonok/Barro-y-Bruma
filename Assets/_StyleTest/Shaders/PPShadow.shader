Shader "BrumaPx/StyleTest/PPShadow"
{
    // Second material slot on every shadow-casting object: draws the mesh flattened onto the ground along
    // the light direction and multiplies it over whatever is there. The stencil stops overlapping shadows
    // from darkening the ground twice.
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry+2" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "CastShadow"
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend DstColor Zero
            Stencil
            {
                Ref 2
                ReadMask 3
                WriteMask 1
                Comp Equal
                Pass IncrSat
            }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "PPCommon.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                float3 p = TransformObjectToWorld(v.positionOS.xyz);
                float3 L = normalize(_PP_LightDir.xyz);
                float h = max(p.y - PP_GROUND_Y, 0.0);
                float3 s = p - L * (h / max(L.y, 0.05));
                s.y = PP_GROUND_Y + 0.02;
                o.positionCS = TransformWorldToHClip(s);
                return o;
            }

            float4 Frag(Varyings i) : SV_Target
            {
                return float4(_PP_ShadowTint.rgb, 1.0);
            }
            ENDHLSL
        }
    }
}
