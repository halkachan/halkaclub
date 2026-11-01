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
        _GroundOnly ("Phase 5: draw only where the world shows ground (the 2D game's grass layer)", Float) = 0
        _Shadow ("ver4.0: draw the texture's shape as a see-through shadow of _ShadowColor", Float) = 0
        _ShadowColor ("ver4.0: the shadow's color and opacity", Color) = (0.09, 0.1, 0.14, 0.45)
        _Worn ("ver4.0: the worn palette for the 2D game's grass (not the characters)", Range(0, 1)) = 0
        _Smooth ("ver4.0: dots drawn evenly at an in-between size (each dot's edge blended over one screen pixel)", Float) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" }
        Cull Off ZWrite Off ZTest Always
        // ver4.0: blended for the characters' shadows; a sprite is always written with alpha 1.
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _Smooth;
            sampler2D _MaskTex;
            float4 _MaskTex_TexelSize;
            float _UseMask;
            float4 _OutlineColor;
            float _Thickness;
            float _MarkerStrength;
            float _DepthEpsilon;
            float _GroundLayerId;
            float _GroundOnly;
            float _Shadow;
            float4 _ShadowColor;
            float _Worn;

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

            // ver4.0 (the user: the crow's eyes looked different sizes): at an in-between dot size (1.5
            // screen pixels per dot) plain nearest sampling gives some dots 1 pixel and others 2. Here
            // the dot's inside is its own colour and only its edge is blended over one screen pixel, so
            // every dot shows at the same size ("sharp bilinear", by hand: the texture is point-filtered).
            float4 SampleEven(float2 uv)
            {
                float2 size = _MainTex_TexelSize.zw;
                float2 t = uv * size - 0.5;
                float2 b = floor(t);
                float2 f = t - b;
                float2 w = max(fwidth(uv * size), 1e-4);
                float2 k = saturate((f - 0.5) / w + 0.5);
                float2 uv0 = (b + 0.5) * _MainTex_TexelSize.xy;
                float2 uv1 = (b + 1.5) * _MainTex_TexelSize.xy;
                float4 c00 = tex2D(_MainTex, uv0);
                float4 c10 = tex2D(_MainTex, float2(uv1.x, uv0.y));
                float4 c01 = tex2D(_MainTex, float2(uv0.x, uv1.y));
                float4 c11 = tex2D(_MainTex, uv1);
                // premultiplied, so a transparent neighbour never darkens the edge colour
                c00.rgb *= c00.a; c10.rgb *= c10.a; c01.rgb *= c01.a; c11.rgb *= c11.a;
                float4 c = lerp(lerp(c00, c10, k.x), lerp(c01, c11, k.x), k.y);
                c.rgb /= max(c.a, 1e-4);
                return c;
            }

            float4 frag(v2f i) : SV_Target
            {
                float4 color = _Smooth > 0.5 ? SampleEven(i.uv) : tex2D(_MainTex, i.uv);
                clip(color.a - 0.5);
                if (_GroundOnly > 0.5)
                {
                    // Under every object, like a ground decoration: only on ground texels.
                    float2 g = (floor(i.mask.xy * _MaskTex_TexelSize.zw) + 0.5) * _MaskTex_TexelSize.xy;
                    float gid = floor(tex2D(_MaskTex, g).r * 255.0 + 0.5);
                    clip((gid == 0.0 || gid == _GroundLayerId) ? 1.0 : -1.0);
                }
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
                if (_Shadow > 0.5) return _ShadowColor;
                if (_Worn > 0.001)
                {
                    // the scenery's worn palette (PocWorn.cginc step 1): muted, olive grey, faded
                    float l = dot(color.rgb, float3(0.299, 0.587, 0.114));
                    color.rgb = lerp(color.rgb, l * float3(1.0, 1.02, 0.9), 0.4 * _Worn);
                    color.rgb *= lerp(float3(1, 1, 1), float3(0.95, 0.94, 0.84), _Worn) * (1.0 - 0.08 * _Worn);
                }
                color.a = 1.0;
                return color;
            }
            ENDCG
        }
    }
}
