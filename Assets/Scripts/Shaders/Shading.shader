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
            // Upgrade NOTE: excluded shader from OpenGL ES 2.0 because it uses non-square matrices
            #pragma exclude_renderers gles
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            #define FRAMEBUFFER_INPUT(index) _UnityFBInput##index

            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 wpos : TEXCOORD1;
            };

            SamplerState Sampler_Clamp_Point;
            UNITY_DECLARE_FRAMEBUFFER_INPUT_FLOAT(0);
            UNITY_DECLARE_FRAMEBUFFER_INPUT_FLOAT(1);
            Texture2D<float4> SceneRM0;
            Texture2D<float4> SceneRM1;
            float3x4 UVToScene;

            v2f vert(appdata v) {
                v2f o;
                o.vertex = v.vertex;
                o.uv = v.uv;
                o.wpos = float4(v.uv, 1.0, -1.0);
                return o;
            }

            float4 frag(v2f i) : SV_TARGET0
            {
                float4 color_rast = UNITY_READ_FRAMEBUFFER_INPUT(0, i.uv);
                float depth_rast = UNITY_READ_FRAMEBUFFER_INPUT(1, i.uv);

                float4 rm0 = SceneRM0.Sample(Sampler_Clamp_Point, i.uv);
                float4 rm1 = SceneRM1.Sample(Sampler_Clamp_Point, i.uv);

                float3 view = normalize(i.wpos);
                float3 refl = reflect(_WorldSpaceLightPos0, rm1.xyz);
                float vn = dot(view, rm1.xyz);

                float3 lum = vn * -dot(refl, view) * rm0.rgb;

                return rm0;
            }
            ENDCG
        }
    }
}
