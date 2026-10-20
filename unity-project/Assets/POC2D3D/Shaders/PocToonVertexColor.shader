// Flat low-poly look for Blender vertex-colored assets: stepped diffuse light, real-time
// shadow from the single directional light, and Flat ambient (set by PocTimeOfDay).
Shader "HALKA/POC/ToonVertexColor"
{
    Properties
    {
        _Bands ("Light bands", Range(1, 6)) = 3
        _ShadowFloor ("Lowest direct light", Range(0, 1)) = 0.35
        // Written to the alpha channel only (opaque rendering is unchanged). The 2D Pixel
        // View's see-through tree layer reads it (set per renderer with a property block).
        _Alpha ("Alpha written to the target", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Toon fullforwardshadows addshadow noforwardadd keepalpha
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
