Shader "BrumaPx/Diorama/PPSpriteShadow"
{
    // Second material of a DioramaSprite: the same frame laid on the ground along the light direction and
    // multiplied over what is there, like PPShadow does with meshes (same stencil, so shadows never darken twice).
    // A sprite has no thickness, so its width is laid across the light instead of across the screen: the shadow
    // keeps the shape of the figure whatever the hour.
    Properties
    {
        _MainTex ("Composed sheet", 2D) = "black" {}
        _Width ("Shadow width (1 = as wide as the sprite)", Range(0.3, 1.5)) = 0.8
    }

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

            TEXTURE2D(_MainTex);
            float4 _Grid;
            float4 _Feet;
            float4 _Frame;
            float _Width;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 cellPx : TEXCOORD0;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                float3 R = _PP_CamRight.xyz;
                float3 U = _PP_CamUp.xyz;
                float ppu = max(_PP_PPU.x, 1.0);

                float3 feet = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
                float fx = dot(feet, R) * ppu;
                float fy = dot(feet, U) * ppu;
                feet += R * ((round(fx) - fx) / ppu) + U * ((round(fy) - fy) / ppu);

                float2 px = v.uv * float2(_Grid.z, _Grid.w > 0.0 ? _Grid.w : _Grid.z);
                // A row of the sprite is a height: the camera looks down, so heights are squashed by its tilt.
                // No clamp here: the quad has only four corners, and a clamp per corner would bend the
                // interpolation and lift the feet off the ground. Rows under the feet are discarded below.
                float h = (px.y - _Feet.y) / (ppu * max(U.y, 0.2));
                float3 L = normalize(_PP_LightDir.xyz);
                float2 along = -L.xz / max(L.y, 0.05);                  // where a point one unit high falls
                float2 dir = normalize(along + float2(1e-4, 0.0));
                float2 across = float2(dir.y, -dir.x);                  // the ground axis the width lies on
                if (dot(across, R.xz) < 0.0) across = -across;
                float2 g = feet.xz + across * ((px.x - _Feet.x) / ppu * _Width) + along * h;
                float3 s = float3(g.x, PP_GROUND_Y + 0.02, g.y);
                o.positionCS = TransformWorldToHClip(s);
                o.cellPx = px;
                return o;
            }

            float4 Frag(Varyings i) : SV_Target
            {
                int2 cell = int2((int)_Grid.z, _Grid.w > 0.0 ? (int)_Grid.w : (int)_Grid.z);
                int2 p = clamp((int2)floor(i.cellPx), int2(0, 0), cell - 1);
                if (i.cellPx.y < _Feet.y - 1.0) discard;
                int2 org = int2((int)_Frame.x * cell.x, ((int)_Grid.y - 1 - (int)_Frame.y) * cell.y);
                if (LOAD_TEXTURE2D(_MainTex, org + p).a < 0.5) discard;
                return float4(_PP_ShadowTint.rgb, 1.0);
            }
            ENDHLSL
        }
    }
}
