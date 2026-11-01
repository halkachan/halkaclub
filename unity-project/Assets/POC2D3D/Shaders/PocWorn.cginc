// ver4.0 (the user's request: a worn, decadent world "in the models and dots themselves", not over
// the whole screen, which made the game hard to play): the scenery's own colours, before the light.
// Characters (ﾊﾙｶﾁｬﾝ, the crow, the passers-by) use a copy of the material with _WornExempt = 1, so
// they keep their colours and stand out. _PocWorn (global, PocWorn.Strength) scales everything.
//   1. colours muted towards an olive grey, whites to a faded beige, a touch darker;
//   2. stains: faint, soft blotches fixed in the world (they move with the ground and walls, never
//      over a character), a little darker and mossier, and a fine speckle (a few texels in 2D);
//   3. moss at the foot of walls, trunks and posts (upright faces near the ground).
#ifndef POC_WORN_INCLUDED
#define POC_WORN_INCLUDED

float _PocWorn;
half _WornExempt;
// ver4.0 (the user: "too green overall"): the palette is set from PocWorn (global vectors), so the
// tones can be changed without touching the shader: the grey the colours are muted towards (rgb) and
// how much (a), the overall tint, the stains' tint, the colour at the foot of walls, and an extra
// muting of green (a: amount; greens are pulled towards grey, the rest is unchanged).
float4 _PocWornGrey;
float4 _PocWornTint;
float4 _PocWornStain;
float4 _PocWornFoot;
float4 _PocWornGreen;

float PocWornHash(float2 p)
{
    p = frac(p * float2(233.34, 851.73));
    p += dot(p, p + 23.45);
    return frac(p.x * p.y);
}

float PocWornNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = PocWornHash(i);
    float b = PocWornHash(i + float2(1, 0));
    float c = PocWornHash(i + float2(0, 1));
    float d = PocWornHash(i + float2(1, 1));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

half3 PocWorn(half3 colour, float3 worldPos, half3 worldNormal)
{
    half k = _PocWorn * (1.0h - _WornExempt);
    if (k <= 0.001h) return colour;
    half l = dot(colour, half3(0.299h, 0.587h, 0.114h));
    // 1. muted towards the palette's grey, tinted, a touch darker; greens muted further
    half greenness = saturate((colour.g - max(colour.r, colour.b)) * 4.0h);
    half3 c = lerp(colour, l * (half3)_PocWornGrey.rgb, saturate((half)_PocWornGrey.a + (half)_PocWornGreen.a * greenness) * k);
    c *= lerp(half3(1, 1, 1), (half3)_PocWornTint.rgb, k);
    c *= 1.0h - 0.08h * k;
    // 2. stains in the world (walls vary upwards too) and a fine speckle
    float2 q = worldPos.xz + worldPos.y * float2(0.7, -0.6);
    float n = 0.7 * PocWornNoise(q * 0.6) + 0.3 * PocWornNoise(q * 1.9 + 17.0);
    // (the user: "not too blotchy": faint stains, larger and softer than the first try)
    half blot = smoothstep(0.55h, 0.9h, (half)n);
    c *= 1.0h - 0.07h * k * blot;
    c = lerp(c, c * (half3)_PocWornStain.rgb, 0.3h * k * blot);
    half speck = (half)PocWornHash(floor(q * 16.0)) - 0.5h;
    c *= 1.0h + speck * 0.12h * k;
    // 3. moss at the foot of upright faces
    half foot = (1.0h - smoothstep(0.0h, 0.35h, (half)worldPos.y)) * (1.0h - abs(worldNormal.y));
    c = lerp(c, (half3)_PocWornFoot.rgb, 0.35h * k * foot);
    return saturate(c);
}

#endif
