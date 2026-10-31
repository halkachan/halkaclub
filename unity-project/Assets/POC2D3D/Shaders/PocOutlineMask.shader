// 2D Pixel View outline mask (replacement shader for the pixel camera).
// R = object ID / 255 and G = outline strength, both from the mesh's UV2 (written by
// PocOutlineTag, so static batching keeps working); B + A = linear depth in 16 bits.
// Untagged meshes (ground, road) have UV2 = (0, 0): ID 0, no outline of their own.
// ver4.0: models drawn from both sides (RenderType "OpaqueTwoSided": Rin and Len, whose original
// faces do not all point outward) are masked from both sides too; everything else as before.
Shader "HALKA/POC/OutlineMask"
{
    CGINCLUDE
    #include "UnityCG.cginc"

    struct appdata { float4 vertex : POSITION; float2 tag : TEXCOORD1; };
    struct v2f { float4 pos : SV_POSITION; float2 tag : TEXCOORD0; float depth : TEXCOORD1; };

    v2f vert(appdata v)
    {
        v2f o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.tag = v.tag;
        o.depth = -UnityObjectToViewPos(v.vertex).z * _ProjectionParams.w;
        return o;
    }

    float4 frag(v2f i) : SV_Target
    {
        float d = saturate(i.depth) * 255.0;
        float hi = floor(d);
        return float4(i.tag.x / 255.0, i.tag.y, hi / 255.0, d - hi);
    }
    ENDCG

    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDCG
        }
    }
    SubShader
    {
        Tags { "RenderType" = "OpaqueTwoSided" }
        Pass
        {
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDCG
        }
    }
}
