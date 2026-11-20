Shader "BrumaPx/Diorama/PPSprite"
{
    // A layered character sheet drawn as one quad in the diorama. The quad is built in the vertex shader: it faces
    // the camera, stands on the object's origin (the feet) and is snapped to the render-pixel lattice, so one texel
    // of the sheet is exactly one pixel of the 480x270 picture. The whole sprite takes the depth of its feet.
    // The outline is drawn here, over the composed picture, so layers are authored without one.
    //
    // Zone map (same grid as the sheets): R = zone * 16, G = 255 where a bandage wraps, B = growth order 1..255.
    // Wounds and Bruma are painted only where the composed pixel is skin or cloth: the compositor writes alpha
    // below 1 on pixels that come from armour, weapons, capes and scarves.
    // Per renderer (MaterialPropertyBlock): _MainTex, _ZoneTex, _Grid, _Feet, _Frame, _ZoneWound, _ZoneBruma.
    Properties
    {
        _MainTex ("Composed sheet", 2D) = "black" {}
        _ZoneTex ("Zone map", 2D) = "black" {}
        _OutlineColor ("Outline colour", Color) = (0.078, 0.071, 0.063, 1)
        _Outline ("Outline (0 = off)", Range(0, 1)) = 1
        _DepthBias ("Pull towards the camera (world units)", Float) = 0.6
        _LightHeight ("Height where lamp light is read", Float) = 0.9
        _BrumaReach ("Share of a zone the Bruma covers at 100", Range(0.2, 1)) = 0.95
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "Geometry+1" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Sprite"
            Tags { "LightMode" = "Universal2D" }
            Cull Off
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "PPCommon.hlsl"

            TEXTURE2D(_MainTex);
            TEXTURE2D(_ZoneTex);
            float4 _OutlineColor;
            float _Outline;
            float _DepthBias;
            float _LightHeight;
            float _BrumaReach;
            float4 _Grid;    // x columns, y rows, z cell width in texels, w cell height (0 = square)
            float4 _Feet;    // xy: feet inside the cell, texels from the left and from the bottom
            float4 _Frame;   // x column, y row (row 0 is the top row of the sheet)
            float _ZoneWound[16];   // 0 none, 1 open, 2 cleaned, 3 bandaged
            float _ZoneBruma[16];   // 0 to 1

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 cellPx : TEXCOORD0;
                float3 feetWS : TEXCOORD1;
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
                float3 w = feet + R * ((px.x - _Feet.x) / ppu) + U * ((px.y - _Feet.y) / ppu);
                o.positionCS = TransformWorldToHClip(w);

                // Everything sorts at the depth of the feet, a little towards the camera.
                float3 F = cross(R, U);
                float4 d = TransformWorldToHClip(feet - F * _DepthBias);
                o.positionCS.z = d.z / d.w * o.positionCS.w;

                o.cellPx = px;
                o.feetWS = feet;
                return o;
            }

            float Alpha(int2 p, int2 org, int2 cell)
            {
                if (p.x < 0 || p.y < 0 || p.x >= cell.x || p.y >= cell.y) return 0.0;
                return LOAD_TEXTURE2D(_MainTex, org + p).a;
            }

            float3 Zones(float3 col, int2 p, int2 org)
            {
                float4 z = LOAD_TEXTURE2D(_ZoneTex, org + p);
                int zone = clamp((int)round(z.r * 255.0 / 16.0), 0, 15);
                if (zone == 0) return col;
                float order = z.b;                       // 0..1: where this pixel sits in the growth of the zone
                float wound = _ZoneWound[zone];
                float level = _ZoneBruma[zone] * _BrumaReach;
                float h = PP_Hash(float2(p) + zone * 7.31);

                // Bruma: a stain that grows from a point of the zone. Solid inside, sparse at its edge.
                if (level > 0.02 && order < level)
                {
                    bool edge = order > level - 0.18;
                    if (!edge)
                        col = h < 0.18 ? PP_Hex(0x2C2738) : (h < 0.70 ? PP_Hex(0x5B5170) : col);
                    else if (h < 0.30)
                        col = PP_Hex(0x5B5170);
                }

                // A bandage goes over everything, on the band of the zone. Open and cleaned wounds are a small spot.
                float h2 = PP_Hash(float2(p.yx) * 1.7 + zone * 3.17);
                if (wound > 2.5)
                {
                    if (z.g > 0.5)
                        col = (p.y & 1) == 0 ? PP_Hex(0xE6E2D4) : (h2 < 0.3 ? PP_Hex(0x9A968C) : PP_Hex(0xC4BFB2));
                }
                else if (wound > 1.5)
                {
                    if (order < 0.2) col = h2 < 0.5 ? PP_Hex(0x6B2A24) : PP_Hex(0x3E1A18);
                }
                else if (wound > 0.5)
                {
                    if (order < 0.2) col = h2 < 0.5 ? PP_Hex(0xB24A36) : PP_Hex(0x6B2A24);
                }
                return col;
            }

            float4 Frag(Varyings i) : SV_Target
            {
                int2 cell = int2((int)_Grid.z, _Grid.w > 0.0 ? (int)_Grid.w : (int)_Grid.z);
                int2 p = clamp((int2)floor(i.cellPx), int2(0, 0), cell - 1);
                int2 org = int2((int)_Frame.x * cell.x, ((int)_Grid.y - 1 - (int)_Frame.y) * cell.y);

                float2 lat = PP_Lattice(i.feetWS) + float2(p) - _Feet.xy;
                if (PP_IslandClip(i.feetWS, lat)) discard;

                float4 c = LOAD_TEXTURE2D(_MainTex, org + p);
                float3 col;
                if (c.a < 0.5)
                {
                    if (_Outline < 0.5) discard;
                    float n = max(max(Alpha(p + int2(1, 0), org, cell), Alpha(p + int2(-1, 0), org, cell)),
                                  max(Alpha(p + int2(0, 1), org, cell), Alpha(p + int2(0, -1), org, cell)));
                    if (n < 0.5) discard;
                    col = _OutlineColor.rgb;
                }
                else
                {
                    col = c.rgb;
                    if (c.a > 0.97) col = Zones(col, p, org);     // skin or cloth, not armour
                }

                col = PP_Atmos(col, i.feetWS + float3(0.0, _LightHeight, 0.0), lat);
                return float4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
