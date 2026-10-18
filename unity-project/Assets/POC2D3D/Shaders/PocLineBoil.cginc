// Line boil (feedback after RC1: "a mysterious world"): the 3D outline twitches a little all the
// time, like a turbulent displace on the line layer. Each line pixel is tested at a displaced
// point instead of its own; the displacement is a two-octave noise field, rounded to whole pixels
// (at most one line width) and replaced by a new random field 6 times a second (PocLineBoil).
// 2D has no boil (feedback); the 2D outline shaders do not include this file.
// The fills stay where they are. Globals set by PocLineBoil; unset (the editor checks) they are
// 0 and every line is still, exactly as before.
#ifndef HALKA_POC_LINE_BOIL
#define HALKA_POC_LINE_BOIL

float _HalkaLineBoil;        // 0 = still lines, 1 = standard, more = stronger
float _HalkaLineBoilStep;    // a new random field per step
float _HalkaLineBoilPicture; // 1 = the whole picture is displaced too (?debug=1 comparison)

// Wavelength of the field in line widths (2D: low-resolution texels; 3D: the line's pixel width).
#define HALKA_BOIL_WAVELENGTH 6.0

float BoilHash(float2 p)
{
    float3 p3 = frac(p.xyx * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float BoilValue(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    float a = BoilHash(i);
    float b = BoilHash(i + float2(1.0, 0.0));
    float c = BoilHash(i + float2(0.0, 1.0));
    float d = BoilHash(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y) * 2.0 - 1.0;
}

float2 BoilNoise(float2 p)
{
    float s = _HalkaLineBoilStep;
    float2 q = p + float2(BoilHash(float2(s, 7.0)), BoilHash(float2(13.0, s))) * 256.0;
    return float2(BoilValue(q) * 0.7 + BoilValue(q * 2.1 + 31.7) * 0.3,
        BoilValue(q + 57.3) * 0.7 + BoilValue(q * 2.1 + 91.1) * 0.3);
}

// Whole-pixel displacement for the pixel with index 'pixel'; 'width' = one line width in pixels,
// 'scale' = the largest shift in line widths (2D 1: one texel; 3D 0.75: finer, at screen size).
float2 BoilShift(float2 pixel, float width, float scale)
{
    if (_HalkaLineBoil <= 0.0) return float2(0.0, 0.0);
    float2 n = BoilNoise(floor(pixel) / (HALKA_BOIL_WAVELENGTH * width));
    return clamp(round(n * width * scale * _HalkaLineBoil), -2.0 * width, 2.0 * width);
}

// A line pixel whose displaced point is off the object moves to pixel - shift. Where that pixel
// has a different shift of its own (a step in the field), it would not take the line: the line
// then stays here as well, so it never breaks.
bool BoilStays(float2 pixel, float2 shift, float width, float scale)
{
    return any(BoilShift(pixel - shift, width, scale) != shift);
}

#endif
