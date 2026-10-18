// 3D view outline (feedback after RC1): the same Object ID mask as the 2D Pixel View
// (HALKA/POC/OutlineMask: ID, strength, linear depth), rendered from the 3D camera at screen
// size. A pixel gets the line color when a neighbour within _Radius pixels belongs to another,
// nearer object with an outline strength: the silhouette is drawn on the background side, as in
// 2D. Ground and road (ID 0, no strength) and the grass tufts (marker strength, off) get none.
// Two passes (line boil, PocLineBoil.cginc): pass 0 finds the lines once per pixel and stores
// them (strength, the outlined object's ID and depth, packed like the mask); pass 1 lays them
// over the picture, each pixel reading the stored line at a displaced point at most 3/4 of a
// line width away (finer steps at screen size): never over a nearer object, joined to its object
// where it moves out, kept where its new pixel would not take it. With the boil off, pass 1 just
// reads the stored line: the same picture as one pass.
Shader "HALKA/POC/Outline3D"
{
    Properties
    {
        _MainTex ("Color", 2D) = "white" {}
        _MaskTex ("Mask", 2D) = "black" {}
        _LineTex ("Lines (pass 0)", 2D) = "black" {}
        _OutlineColor ("Outline color", Color) = (0.145, 0.169, 0.224, 1)
        _Radius ("Line width in screen pixels (1-3)", Float) = 1
        _MarkerStrength ("Strength of marker-tagged meshes (grass)", Float) = 0
        _DepthEpsilon ("Depth epsilon (0..1 of far)", Float) = 0.0004
    }
    CGINCLUDE
    #include "UnityCG.cginc"
    #include "PocLineBoil.cginc"

    sampler2D _MainTex;
    sampler2D _MaskTex;
    sampler2D _LineTex;
    float4 _MaskTex_TexelSize;
    float4 _OutlineColor;
    float _Radius;
    float _MarkerStrength;
    float _DepthEpsilon;

    void Read(float2 uv, out float id, out float strength, out float depth)
    {
        float4 m = tex2D(_MaskTex, uv);
        id = floor(m.r * 255.0 + 0.5);
        strength = m.g;
        if (strength > 0.0 && strength < 0.02) strength = _MarkerStrength;
        depth = (floor(m.b * 255.0 + 0.5) + m.a) / 255.0;
    }

    // The stored line: strength, and the nearest outlined object's ID and depth.
    void ReadLine(float2 uv, out float ink, out float id, out float depth)
    {
        float4 l = tex2D(_LineTex, uv);
        ink = l.r;
        id = floor(l.g * 255.0 + 0.5);
        depth = (floor(l.b * 255.0 + 0.5) + l.a) / 255.0;
    }
    ENDCG
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        // Pass 0: the lines.
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag

            float Outer(float2 uv, float2 offset, float id0, float depth0, inout float nearDepth, inout float nearId)
            {
                float id, strength, depth;
                Read(uv + offset * _MaskTex_TexelSize.xy, id, strength, depth);
                if (id != id0 && strength > 0.0 && depth < depth0 - _DepthEpsilon)
                {
                    if (depth < nearDepth) { nearDepth = depth; nearId = id; }
                    return strength;
                }
                return 0.0;
            }

            float Ring(float2 uv, float k, float id0, float depth0, inout float nearDepth, inout float nearId)
            {
                float ink = 0.0;
                ink = max(ink, Outer(uv, float2(k, 0), id0, depth0, nearDepth, nearId));
                ink = max(ink, Outer(uv, float2(-k, 0), id0, depth0, nearDepth, nearId));
                ink = max(ink, Outer(uv, float2(0, k), id0, depth0, nearDepth, nearId));
                ink = max(ink, Outer(uv, float2(0, -k), id0, depth0, nearDepth, nearId));
                ink = max(ink, Outer(uv, float2(k, k) * 0.7071, id0, depth0, nearDepth, nearId));
                ink = max(ink, Outer(uv, float2(-k, k) * 0.7071, id0, depth0, nearDepth, nearId));
                ink = max(ink, Outer(uv, float2(k, -k) * 0.7071, id0, depth0, nearDepth, nearId));
                ink = max(ink, Outer(uv, float2(-k, -k) * 0.7071, id0, depth0, nearDepth, nearId));
                return ink;
            }

            float4 frag(v2f_img i) : SV_Target
            {
                float id0, strength0, depth0;
                Read(i.uv, id0, strength0, depth0);
                float nearDepth = 2.0, nearId = 0.0;
                float ink = Ring(i.uv, 1.0, id0, depth0, nearDepth, nearId);
                if (_Radius > 1.5) ink = max(ink, Ring(i.uv, 2.0, id0, depth0, nearDepth, nearId));
                if (_Radius > 2.5) ink = max(ink, Ring(i.uv, 3.0, id0, depth0, nearDepth, nearId));
                nearDepth = min(nearDepth, 1.0);
                float packed = nearDepth * 255.0;
                return float4(ink, nearId / 255.0, floor(packed) / 255.0, frac(packed));
            }
            ENDCG
        }
        // Pass 1: over the picture, with the boil.
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag

            float4 frag(v2f_img i) : SV_Target
            {
                float4 color = tex2D(_MainTex, i.uv);
                float ink, nearId, nearDepth;
                ReadLine(i.uv, ink, nearId, nearDepth);
                float2 shift = BoilShift(i.uv * _MaskTex_TexelSize.zw, _Radius, 0.75);
                float2 uvd = i.uv + shift * _MaskTex_TexelSize.xy;
                if (any(saturate(uvd) != uvd)) shift = 0.0; // at the picture's edge the line stays
                if (any(shift != 0.0))
                {
                    float moved, movedId, movedDepth;
                    ReadLine(uvd, moved, movedId, movedDepth);
                    if (_HalkaLineBoilPicture > 0.5)
                    {
                        // ?debug=1 comparison: the whole picture is displaced, lines and fills.
                        color = tex2D(_MainTex, uvd);
                        ink = moved;
                    }
                    else
                    {
                        float id0, strength0, depth0;
                        Read(i.uv, id0, strength0, depth0);
                        // Never over something standing in front of the outlined object.
                        if (moved > 0.0 && id0 != movedId && depth0 < movedDepth + _DepthEpsilon) moved = 0.0;
                        float keep = 0.0;
                        if (ink > 0.0)
                        {
                            float idd, strengthd, depthd;
                            Read(uvd, idd, strengthd, depthd);
                            // Moved outward (the displaced point is on the object): joined to it.
                            bool onObject = strengthd > 0.0 && idd != id0 && depthd < depth0 - _DepthEpsilon;
                            // Moved inward: it goes to the pixel at -shift, if that pixel takes it (the
                            // same shift, not in front of the outlined object); otherwise it stays too.
                            float2 uvq = i.uv - shift * _MaskTex_TexelSize.xy;
                            float idq, strengthq, depthq;
                            Read(uvq, idq, strengthq, depthq);
                            bool taken = all(saturate(uvq) == uvq) && !BoilStays(i.uv * _MaskTex_TexelSize.zw, shift, _Radius, 0.75) &&
                                (idq == nearId || depthq >= nearDepth + _DepthEpsilon);
                            keep = (onObject || !taken) ? ink : 0.0;
                        }
                        ink = max(moved, keep);
                    }
                }
                color.rgb = lerp(color.rgb, _OutlineColor.rgb, saturate(ink));
                return color;
            }
            ENDCG
        }
    }
}
