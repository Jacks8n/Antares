Shader "SDF/RayMarching"
{
    Properties
    {
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }

        Pass
        {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct v2f {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler scene_volume;

            v2f vert(uint vertID : SV_VertexID) {
                float2 uvs[3] = { { 0, 0 }, { 2, 0 }, { 0, 2 } };
                float4 verts[3] = { { 0, 0, 0, 1}, { 2, 0, 0, 1 }, { 0, 2, 0, 1 } };

                v2f o;
                o.vertex = verts[vertID];
                o.uv = uvs[vertID];
                return o;
            }

            float4 gather_sdf(float3 o, float3 d) {
                float t;
                for (uint i = 0; i < 32; i++) {
                    t = tex3D(scene_volume, o);

                    if (abs(t) < .001)
                        break;
                    o = d * t;
                }
                return float4(o, t);
            }

            float4 sample_sdf(float3 o, float3 d) {
                return 0;
            }

            float4 frag(v2f i) : SV_Target
            {
                return float4(1, 1, 1, 0);
            }
            ENDCG
        }
    }
}
