// 2D Pixel View outline: runs on the low-resolution texture before the integer upscale, so
// every line is whole pixels (1 or 2) and moves with the pixel-snapped camera.
// Outer outline: a pixel gets the line color when a neighbour belongs to another, nearer
// object with an outline strength (the silhouette edge seen from the background side).
// Inner lines (optional): same object, nearer neighbour, and a strong color change
// (crown/trunk, petal/centre), never between faces of similar color.
Shader "HALKA/POC/OutlineComposite"
{
    Properties
    {
        _MainTex ("Color", 2D) = "white" {}
        _MaskTex ("Mask", 2D) = "black" {}
        _OutlineColor ("Outline color", Color) = (0.145, 0.169, 0.224, 1)
        _Thickness ("Thickness (1 or 2)", Float) = 1
        _InnerStrength ("Inner line strength", Float) = 0
        _InnerThreshold ("Inner color difference", Float) = 0.28
        _MarkerStrength ("Strength of marker-tagged meshes (grass)", Float) = 0
        _DepthEpsilon ("Depth epsilon (0..1 of far)", Float) = 0.0005
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
            float4 _MainTex_TexelSize;
            float4 _OutlineColor;
            float _Thickness;
            float _InnerStrength;
            float _InnerThreshold;
            float _MarkerStrength;
            float _DepthEpsilon;

            void Read(float2 uv, out float id, out float strength, out float depth)
            {
                float4 m = tex2D(_MaskTex, uv);
                id = floor(m.r * 255.0 + 0.5);
                strength = m.g;
                // A tiny positive strength marks decoration whose strength is set globally.
                if (strength > 0.0 && strength < 0.02) strength = _MarkerStrength;
                depth = (floor(m.b * 255.0 + 0.5) + m.a) / 255.0;
            }

            float Outer(float2 uv, float2 offset, float id0, float depth0)
            {
                float id, strength, depth;
                Read(uv + offset * _MainTex_TexelSize.xy, id, strength, depth);
                return (id != id0 && strength > 0.0 && depth < depth0 - _DepthEpsilon) ? strength : 0.0;
            }

            float Inner(float2 uv, float2 offset, float id0, float strength0, float depth0, float3 color0)
            {
                float2 uvq = uv + offset * _MainTex_TexelSize.xy;
                float id, strength, depth;
                Read(uvq, id, strength, depth);
                float3 colorq = tex2D(_MainTex, uvq).rgb;
                return (id == id0 && id0 > 0.0 && strength0 > 0.0 && depth < depth0 - _DepthEpsilon &&
                    distance(color0, colorq) > _InnerThreshold) ? strength0 * _InnerStrength : 0.0;
            }

            float4 frag(v2f_img i) : SV_Target
            {
                float4 color = tex2D(_MainTex, i.uv);
                float id0, strength0, depth0;
                Read(i.uv, id0, strength0, depth0);
                float ink = 0.0;
                ink = max(ink, Outer(i.uv, float2(1, 0), id0, depth0));
                ink = max(ink, Outer(i.uv, float2(-1, 0), id0, depth0));
                ink = max(ink, Outer(i.uv, float2(0, 1), id0, depth0));
                ink = max(ink, Outer(i.uv, float2(0, -1), id0, depth0));
                if (_Thickness > 1.5)
                {
                    ink = max(ink, Outer(i.uv, float2(2, 0), id0, depth0));
                    ink = max(ink, Outer(i.uv, float2(-2, 0), id0, depth0));
                    ink = max(ink, Outer(i.uv, float2(0, 2), id0, depth0));
                    ink = max(ink, Outer(i.uv, float2(0, -2), id0, depth0));
                    ink = max(ink, Outer(i.uv, float2(1, 1), id0, depth0));
                    ink = max(ink, Outer(i.uv, float2(-1, 1), id0, depth0));
                    ink = max(ink, Outer(i.uv, float2(1, -1), id0, depth0));
                    ink = max(ink, Outer(i.uv, float2(-1, -1), id0, depth0));
                }
                if (_InnerStrength > 0.0)
                {
                    ink = max(ink, Inner(i.uv, float2(1, 0), id0, strength0, depth0, color.rgb));
                    ink = max(ink, Inner(i.uv, float2(-1, 0), id0, strength0, depth0, color.rgb));
                    ink = max(ink, Inner(i.uv, float2(0, 1), id0, strength0, depth0, color.rgb));
                    ink = max(ink, Inner(i.uv, float2(0, -1), id0, strength0, depth0, color.rgb));
                }
                color.rgb = lerp(color.rgb, _OutlineColor.rgb, saturate(ink));
                return color;
            }
            ENDCG
        }
    }
}
