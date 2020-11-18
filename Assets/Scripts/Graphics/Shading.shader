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

            SamplerState Sampler_Clamp_Point;
            Texture2D<float4> _UnityFBInput0;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = v.vertex;
                o.uv = v.uv;
                return o;
            }

            float4 frag(v2f i) : SV_TARGET0
            {
                float4 col = _UnityFBInput0.Sample(Sampler_Clamp_Point, float2(i.uv.x, i.uv.y));
                return col;
            }
            ENDCG
        }
    }
}
