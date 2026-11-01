// ver4.0 (the user's request after the second look: a more decadent, worn mood, from a reference
// picture's colours only, and "noisier"): one full-screen pass over the 2D and the 3D view (the UI is
// drawn after it and keeps its colours). The user chose the "中" preview with grain only:
//   1. colours muted (saturation x0.62 at strength 0.75), 2. darker overall (x0.775),
//   3. shade toned deep moss green-grey, light warm sepia, 4. the brightest parts a little flatter,
//   5. a warm glow in the middle where she stands, mist at the left and right edges (outdoors only),
//      dark mossy corners, 6. fine film grain, changing every frame.
// The same steps as the preview script (HARUKA_WORLD_V40_LOGS/mood_preview); gamma colour space.
Shader "HALKA/POC/MoodGrade"
{
    Properties
    {
        _MainTex ("Picture", 2D) = "white" {}
        _Strength ("Strength (the user's choice: 0.75)", Range(0, 1)) = 0.75
        _Grain ("Grain (standard deviation)", Range(0, 0.1)) = 0.03
        _Mist ("Mist at the side edges (0 indoors)", Range(0, 1)) = 1
        _Seed ("Grain seed (changes every frame)", Float) = 0
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
            float _Strength;
            float _Grain;
            float _Mist;
            float _Seed;

            float Hash(float2 p)
            {
                p = frac(p * float2(443.897, 441.423));
                p += dot(p, p.yx + 19.19);
                return frac((p.x + p.y) * p.x);
            }

            float4 frag(v2f_img i) : SV_Target
            {
                float4 src = tex2D(_MainTex, i.uv);
                float3 c = src.rgb;
                float k = _Strength;
                float l = dot(c, float3(0.299, 0.587, 0.114));
                c = l + (c - l) * (1.0 - 0.5 * k);
                c *= 1.0 - 0.3 * k;
                float t = smoothstep(0.05, 0.7, l);
                float3 tint = lerp(float3(0.70, 0.86, 0.76), float3(1.06, 1.0, 0.84), t);
                c *= 1.0 + (tint - 1.0) * k;
                c = c > 0.6 ? 0.6 + (c - 0.6) * (1.0 - 0.25 * k) : c;
                float2 p = i.uv * 2.0 - 1.0;
                float d = sqrt(p.x * 0.85 * p.x * 0.85 + p.y * p.y);
                float glow = 1.0 - smoothstep(0.0, 0.75, d);
                c *= 1.0 + 0.18 * k * glow * float3(1.0, 0.9, 0.7);
                float side = smoothstep(0.6, 1.0, abs(p.x)) * _Mist;
                c = c * (1.0 - 0.3 * k * side) + float3(0.42, 0.47, 0.43) * 0.3 * k * side;
                float vig = smoothstep(0.55, 1.4, d);
                c = c * (1.0 - 0.7 * k * vig) + float3(0.05, 0.07, 0.06) * 0.7 * k * vig;
                // grain: two uniform samples summed, about a normal distribution
                float2 px = floor(i.uv * _ScreenParams.xy);
                float n = (Hash(px + _Seed) + Hash(px * 1.37 + _Seed * 2.11 + 17.0) - 1.0) * 2.45;
                c += n * _Grain;
                return float4(saturate(c), src.a);
            }
            ENDCG
        }
    }
}
