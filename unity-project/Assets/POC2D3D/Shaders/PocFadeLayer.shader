// 2D Pixel View: trees standing in front of the player, drawn see-through. They are rendered
// on their own into a low-resolution texture (depth-tested against the rest of the world, alpha
// = how far each has faded), then laid over the upscaled view and the player sprite. Pixels
// next to a faded tree get its outline at the same strength, so the silhouette stays readable.
Shader "HALKA/POC/FadeLayer"
{
    Properties
    {
        _MainTex ("Faded objects (alpha = opacity)", 2D) = "black" {}
        _OutlineColor ("Outline color", Color) = (0.145, 0.169, 0.224, 1)
        _Thickness ("Outline thickness (0, 1 or 2)", Float) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" }
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float4 _OutlineColor;
            float _Thickness;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float Alpha(float2 uv, float2 offset)
            {
                return tex2D(_MainTex, uv + offset * _MainTex_TexelSize.xy).a;
            }

            float4 frag(v2f i) : SV_Target
            {
                float2 uv = (floor(i.uv * _MainTex_TexelSize.zw) + 0.5) * _MainTex_TexelSize.xy;
                float4 color = tex2D(_MainTex, uv);
                if (color.a > 0.002) return color;
                float a = 0.0;
                if (_Thickness > 0.5)
                {
                    a = max(a, Alpha(uv, float2(1, 0)));
                    a = max(a, Alpha(uv, float2(-1, 0)));
                    a = max(a, Alpha(uv, float2(0, 1)));
                    a = max(a, Alpha(uv, float2(0, -1)));
                    if (_Thickness > 1.5)
                    {
                        a = max(a, Alpha(uv, float2(2, 0)));
                        a = max(a, Alpha(uv, float2(-2, 0)));
                        a = max(a, Alpha(uv, float2(0, 2)));
                        a = max(a, Alpha(uv, float2(0, -2)));
                        a = max(a, Alpha(uv, float2(1, 1)));
                        a = max(a, Alpha(uv, float2(-1, 1)));
                        a = max(a, Alpha(uv, float2(1, -1)));
                        a = max(a, Alpha(uv, float2(-1, -1)));
                    }
                }
                clip(a - 0.002);
                return float4(_OutlineColor.rgb, a);
            }
            ENDCG
        }
    }
}
