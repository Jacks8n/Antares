Shader "SDF/Shading"
{
    Properties
    {
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }

        Pass
        {
            ZTest Always

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D GBuffer0;
            sampler2D SceneRM0;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = float4(v.vertex.xyz, 1);
                o.uv = v.uv;
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float4 col = tex2D(GBuffer0, i.uv);
                return col;
            }
            ENDCG
        }
    }
}
