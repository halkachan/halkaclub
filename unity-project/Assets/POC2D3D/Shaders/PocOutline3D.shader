// 3D view outline (feedback after RC1): the same Object ID mask as the 2D Pixel View
// (HALKA/POC/OutlineMask: ID, strength, linear depth), rendered from the 3D camera at screen
// size. A pixel gets the line color when a neighbour within _Radius pixels belongs to another,
// nearer object with an outline strength: the silhouette is drawn on the background side, as in
// 2D. Ground and road (ID 0, no strength) and the grass tufts (marker strength, off) get none.
Shader "HALKA/POC/Outline3D"
{
    Properties
    {
        _MainTex ("Color", 2D) = "white" {}
        _MaskTex ("Mask", 2D) = "black" {}
        _OutlineColor ("Outline color", Color) = (0.145, 0.169, 0.224, 1)
        _Radius ("Line width in screen pixels (1-3)", Float) = 1
        _MarkerStrength ("Strength of marker-tagged meshes (grass)", Float) = 0
        _DepthEpsilon ("Depth epsilon (0..1 of far)", Float) = 0.0004
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _MaskTex;
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

            float Outer(float2 uv, float2 offset, float id0, float depth0)
            {
                float id, strength, depth;
                Read(uv + offset * _MaskTex_TexelSize.xy, id, strength, depth);
                return (id != id0 && strength > 0.0 && depth < depth0 - _DepthEpsilon) ? strength : 0.0;
            }

            float Ring(float2 uv, float k, float id0, float depth0)
            {
                float ink = 0.0;
                ink = max(ink, Outer(uv, float2(k, 0), id0, depth0));
                ink = max(ink, Outer(uv, float2(-k, 0), id0, depth0));
                ink = max(ink, Outer(uv, float2(0, k), id0, depth0));
                ink = max(ink, Outer(uv, float2(0, -k), id0, depth0));
                ink = max(ink, Outer(uv, float2(k, k) * 0.7071, id0, depth0));
                ink = max(ink, Outer(uv, float2(-k, k) * 0.7071, id0, depth0));
                ink = max(ink, Outer(uv, float2(k, -k) * 0.7071, id0, depth0));
                ink = max(ink, Outer(uv, float2(-k, -k) * 0.7071, id0, depth0));
                return ink;
            }

            float4 frag(v2f_img i) : SV_Target
            {
                float4 color = tex2D(_MainTex, i.uv);
                float id0, strength0, depth0;
                Read(i.uv, id0, strength0, depth0);
                float ink = 0.0;
                ink = max(ink, Ring(i.uv, 1.0, id0, depth0));
                if (_Radius > 1.5) ink = max(ink, Ring(i.uv, 2.0, id0, depth0));
                if (_Radius > 2.5) ink = max(ink, Ring(i.uv, 3.0, id0, depth0));
                color.rgb = lerp(color.rgb, _OutlineColor.rgb, saturate(ink));
                return color;
            }
            ENDCG
        }
    }
}
