// 2D Pixel View: the existing hand-drawn player sprite, drawn over the upscaled view at its own
// pixel density (64 px = 1 unit, as in the 2D game), never resampled into the low-resolution
// texture. Draw order follows the outline mask: the sprite is treated as an upright card at the
// bottom of the player's cell (TEXCOORD1.z = its linear depth, packed like the mask), so things
// South of the player cover it and things North of it stay behind, like the 2D game's order.
// The ground (ID 0) and grass tufts (_GroundLayerId) are under the player as in the 2D game
// and never cover it. Where a covering object's silhouette meets the sprite, its outline is
// drawn on the sprite.
Shader "HALKA/POC/PixelSprite"
{
    Properties
    {
        _MainTex ("Sprite", 2D) = "white" {}
        _MaskTex ("Mask", 2D) = "black" {}
        _UseMask ("Use the mask for draw order", Float) = 1
        _OutlineColor ("Outline color", Color) = (0.145, 0.169, 0.224, 1)
        _Thickness ("Outline thickness (0, 1 or 2)", Float) = 1
        _MarkerStrength ("Strength of marker-tagged meshes (grass)", Float) = 0
        _DepthEpsilon ("Depth epsilon (0..1 of far)", Float) = 0.0005
        _GroundLayerId ("Object ID drawn under the player besides the ground (grass tufts)", Float) = 250
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" }
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _MaskTex;
            float4 _MaskTex_TexelSize;
            float _UseMask;
            float4 _OutlineColor;
            float _Thickness;
            float _MarkerStrength;
            float _DepthEpsilon;
            float _GroundLayerId;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float3 mask : TEXCOORD1; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 mask : TEXCOORD1; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.mask = v.mask;
                return o;
            }

            void Read(float2 uv, out float strength, out float depth)
            {
                float4 m = tex2D(_MaskTex, uv);
                float id = floor(m.r * 255.0 + 0.5);
                strength = m.g;
                if (strength > 0.0 && strength < 0.02) strength = _MarkerStrength;
                depth = (floor(m.b * 255.0 + 0.5) + m.a) / 255.0;
                // Ground layer: treated as far away, so it never covers or outlines the sprite.
                if (id == 0.0 || id == _GroundLayerId) { strength = 0.0; depth = 2.0; }
            }

            float Cover(float2 uv, float2 offset, float depth0)
            {
                float strength, depth;
                Read(uv + offset * _MaskTex_TexelSize.xy, strength, depth);
                return (strength > 0.0 && depth < depth0 - _DepthEpsilon) ? strength : 0.0;
            }

            float4 frag(v2f i) : SV_Target
            {
                float4 color = tex2D(_MainTex, i.uv);
                clip(color.a - 0.5);
                if (_UseMask > 0.5)
                {
                    // The low-resolution texel under this screen pixel.
                    float2 uv = (floor(i.mask.xy * _MaskTex_TexelSize.zw) + 0.5) * _MaskTex_TexelSize.xy;
                    float strength0, depth0;
                    Read(uv, strength0, depth0);
                    clip(depth0 + _DepthEpsilon - i.mask.z);
                    if (_Thickness > 0.5)
                    {
                        float ink = 0.0;
                        ink = max(ink, Cover(uv, float2(1, 0), i.mask.z));
                        ink = max(ink, Cover(uv, float2(-1, 0), i.mask.z));
                        ink = max(ink, Cover(uv, float2(0, 1), i.mask.z));
                        ink = max(ink, Cover(uv, float2(0, -1), i.mask.z));
                        if (_Thickness > 1.5)
                        {
                            ink = max(ink, Cover(uv, float2(2, 0), i.mask.z));
                            ink = max(ink, Cover(uv, float2(-2, 0), i.mask.z));
                            ink = max(ink, Cover(uv, float2(0, 2), i.mask.z));
                            ink = max(ink, Cover(uv, float2(0, -2), i.mask.z));
                            ink = max(ink, Cover(uv, float2(1, 1), i.mask.z));
                            ink = max(ink, Cover(uv, float2(-1, 1), i.mask.z));
                            ink = max(ink, Cover(uv, float2(1, -1), i.mask.z));
                            ink = max(ink, Cover(uv, float2(-1, -1), i.mask.z));
                        }
                        color.rgb = lerp(color.rgb, _OutlineColor.rgb, saturate(ink));
                    }
                }
                color.a = 1.0;
                return color;
            }
            ENDCG
        }
    }
}
