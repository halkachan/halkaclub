// ver4.0: Rin and Len (the user's 1頭身 models). The same stepped toon light and shadow as
// ToonVertexColor, for a model that keeps its own texture: colour = vertex rgb * lerp(1, texture,
// vertex alpha). Alpha 1 marks the faces whose original material used the texture (multiplied by
// the vertex colour for Len's, by white for Rin's); alpha 0 the plain vertex-colour faces.
Shader "HALKA/POC/ToonTextureTint"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Bands ("Light bands", Range(1, 6)) = 3
        _ShadowFloor ("Lowest direct light", Range(0, 1)) = 0.35
        _Alpha ("Alpha written to the target", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Toon fullforwardshadows addshadow noforwardadd keepalpha
        #pragma target 3.0

        sampler2D _MainTex;
        half _Bands;
        half _ShadowFloor;
        half _Alpha;

        struct Input
        {
            float2 uv_MainTex;
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
            half3 tex = tex2D(_MainTex, IN.uv_MainTex).rgb;
            o.Albedo = IN.color.rgb * lerp(half3(1, 1, 1), tex, IN.color.a);
            o.Alpha = _Alpha;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
