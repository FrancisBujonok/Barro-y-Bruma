#ifndef BRUMAPX_PP_COMMON
#define BRUMAPX_PP_COMMON

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

// Set every frame by PixelDioramaRig (colours already in linear colour space).
float4 _PP_LightDir;    // xyz: unit vector pointing TOWARDS the light
float4 _PP_Cool;        // colour the shadows drift towards
float4 _PP_Warm;        // colour the lights drift towards
float4 _PP_Fog;         // background / mist colour
float4 _PP_ShadowTint;  // multiplied over the ground under cast shadows
float4 _PP_Focus;       // xyz: world position of the hero (mist stays clear around it, the island is centred on it)
float4 _PP_CamRight;    // xyz: camera right axis
float4 _PP_CamUp;       // xyz: camera up axis
float4 _PP_PPU;         // x: render pixels per world unit
float4 _PP_Island;      // xy: radius of the visible island around the hero (huge = no clipping)

float4 _PP_Tone;        // rgb: linear multiplier for every lit surface (day = 1, night = dark blue)
float4 _PP_Weather;     // x: mist density multiplier, y: mist threshold shift, z: rain amount, w: wetness
float4 _PP_LightCount;  // x: number of lamp lights in use
float4 _PP_LightPos[8]; // xyz: position, w: radius
float4 _PP_LightCol[8]; // rgb: colour * intensity

static const float PP_GROUND_Y = 0.0;

// 4x4 Bayer threshold for an integer lattice cell.
float PP_Bayer(float2 lattice)
{
    int2 p = (int2)lattice;
    uint x = (uint)(p.x & 3);
    uint y = (uint)(p.y & 3);
    const float m[16] = { 0.0, 8.0, 2.0, 10.0, 12.0, 4.0, 14.0, 6.0, 3.0, 11.0, 1.0, 9.0, 15.0, 7.0, 13.0, 5.0 };
    return (m[y * 4u + x] + 0.5) / 16.0;
}

// The render pixel a world point falls on, measured from the world origin instead of the screen.
// The camera moves in whole render pixels, so dither and noise anchored to this lattice stay glued to the world.
float2 PP_Lattice(float3 wpos)
{
    return floor(float2(dot(wpos, _PP_CamRight.xyz), dot(wpos, _PP_CamUp.xyz)) * _PP_PPU.x);
}

float PP_Hash(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float PP_Noise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = PP_Hash(i);
    float b = PP_Hash(i + float2(1.0, 0.0));
    float c = PP_Hash(i + float2(0.0, 1.0));
    float d = PP_Hash(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

float PP_Fbm(float2 p)
{
    float v = 0.0;
    float a = 0.5;
    for (int k = 0; k < 4; k++)
    {
        v += a * PP_Noise(p);
        p = p * 2.03 + 17.1;
        a *= 0.5;
    }
    return v;
}

float3 PP_SRGBToLinear(float3 c)
{
#if defined(UNITY_COLORSPACE_GAMMA)
    return c;
#else
    float3 lo = c / 12.92;
    float3 hi = pow((c + 0.055) / 1.055, 2.4);
    return lerp(hi, lo, step(c, 0.04045));
#endif
}

float3 PP_Hex(uint h)
{
    return PP_SRGBToLinear(float3((h >> 16) & 255u, (h >> 8) & 255u, h & 255u) / 255.0);
}

// Hue-shifted ramp: shadows drift to the cool colour, lights to the warm one.
float3 PP_Ramp(float3 c, float t)
{
    float3 dark = lerp(c * 0.55, _PP_Cool.rgb, 0.40);
    float3 light = lerp(c * 1.10, _PP_Warm.rgb, 0.30);
    return t < 0.5 ? lerp(dark, c, t * 2.0) : lerp(c, light, (t - 0.5) * 2.0);
}

// Time-of-day tone plus warm lamp pools. Each pool is drawn as stepped, dithered rings so it keeps the pixel look.
float3 PP_Atmos(float3 col, float3 wpos, float2 lattice)
{
    float3 lit = float3(0.0, 0.0, 0.0);
    int n = (int)_PP_LightCount.x;
    float bay = PP_Bayer(lattice);
    for (int k = 0; k < 8; k++)
    {
        if (k >= n) break;
        float3 d = wpos - _PP_LightPos[k].xyz;
        d.y *= 0.6;
        float a = saturate(1.0 - length(d) / max(_PP_LightPos[k].w, 0.01));
        a = saturate(a * 1.3);
        a = floor(a * 3.0 + bay * 0.85) / 3.0;
        lit += _PP_LightCol[k].rgb * saturate(a);
    }
    return col * min(_PP_Tone.rgb + lit, 1.12);
}

// The visible world is an island around the hero with a ragged edge that dissolves into mist.
// 0 at the hero, ~1 at the edge. Ground and props share it, so everything fades out together.
float PP_IslandRough(float3 wpos)
{
    float2 p = wpos.xz - _PP_Focus.xz;
    float edge = pow(pow(abs(p.x) / _PP_Island.x, 4.0) + pow(abs(p.y) / _PP_Island.y, 4.0), 0.25);
    return edge + (PP_Noise(wpos.xz * 0.6 + 3.1) - 0.5) * 0.20;
}

bool PP_IslandClip(float3 wpos, float2 lattice)
{
    float r = PP_IslandRough(wpos);
    return r > 1.04 || (r > 0.86 && PP_Hash(lattice) < (r - 0.86) / 0.18);
}

#endif
