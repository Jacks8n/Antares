Shader "Antares/SDF/Shading"
{
    Properties
    {
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }

        Pass
        {
            ZTest Always
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            #define FRAMEBUFFER_INPUT(index) _UnityFBInput##index

            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            SamplerState Sampler_Clamp_Point;
            UNITY_DECLARE_FRAMEBUFFER_INPUT_FLOAT(0);
            UNITY_DECLARE_FRAMEBUFFER_INPUT_FLOAT(1);
            Texture2D<float4> SceneRM0;

            v2f vert(appdata v) {
                v2f o;
                o.vertex = v.vertex;
                o.uv = v.uv;
                return o;
            }

            float4 frag(v2f i) : SV_TARGET0
            {
                float4 col = FRAMEBUFFER_INPUT(0).Sample(Sampler_Clamp_Point, i.uv);
                float depth = FRAMEBUFFER_INPUT(1).Sample(Sampler_Clamp_Point, i.uv);
                float4 rm = SceneRM0.Sample(Sampler_Clamp_Point, i.uv);
                return rm;
            }
            ENDCG
        }
    }
}
