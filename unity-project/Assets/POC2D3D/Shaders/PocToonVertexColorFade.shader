// Phase 4: the 3D view's see-through tree. Same stepped toon lighting as ToonVertexColor, drawn
// at _Alpha opacity: a depth-only pass first, so only the tree's nearest surface is blended
// (no inner faces showing through), then the colour pass over what is already drawn (the player
// behind it). It stays in the AlphaTest range of the queue and keeps its shadow-caster pass, so
// the tree's shadow on the ground does not change while it fades.
Shader "HALKA/POC/ToonVertexColorFade"
{
    Properties
    {
        _Bands ("Light bands", Range(1, 6)) = 3
        _ShadowFloor ("Lowest direct light", Range(0, 1)) = 0.35
        _Alpha ("Opacity", Range(0, 1)) = 0.4
    }
    SubShader
    {
        Tags { "Queue" = "AlphaTest+10" "RenderType" = "Opaque" "IgnoreProjector" = "True" }
        LOD 200

        Pass
        {
            Name "DEPTH ONLY"
            ZWrite On
            ColorMask 0
        }

        CGPROGRAM
        #pragma surface surf Toon fullforwardshadows addshadow noforwardadd alpha:fade
        #pragma target 3.0

        half _Bands;
        half _ShadowFloor;
        half _Alpha;

        struct Input
        {
            float4 color : COLOR;
        };

        half4 LightingToon(SurfaceOutput s, half3 lightDir, half atten)
        {
            half ndl = saturate(dot(s.Normal, lightDir));
            half lit = floor(ndl * _Bands + 0.5h) / _Bands;
            lit = lerp(_ShadowFloor, 1.0h, lit) * lerp(_ShadowFloor, 1.0h, step(0.5h, atten));
            half4 c;
            c.rgb = s.Albedo * _LightColor0.rgb * lit;
            c.a = s.Alpha;
            return c;
        }

        void surf(Input IN, inout SurfaceOutput o)
        {
            o.Albedo = IN.color.rgb;
            o.Alpha = _Alpha;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
