Shader "Unlit/DebugParticle"
{
    Properties { }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma geometr geom
            #pragma fragment frag
            #pragma target 4.5

            #include "UnityCG.cginc"
            #include "../Physics/FluidData.cginc"

            struct appdata
            {
                uint particle_id : SV_VertexID;
            };

            struct v2g
            {
                float3 center : TEXCOORD0;
            };

            struct g2f
            {
                float2 uv : TEXCOORD0;
                float3 wpos : TEXCOORD1;
                float4 vertex : SV_POSITION;
            };

            CBUFFER_START(DebugFluidParticleParameters)
                float3 CameraPosition;

                float Padding;

                // magnitude: radius of particle
                float3 ParticleUp;
            CBUFFER_END

            v2g vert(appdata v)
            {
                v2g o;

                const bool pingpong = GetFluidParticlePositionPingPongFlag();
                const ParticlePositionIndexed positionIndexed = GetFluidParticlePosition(v.particle_id, pingpong);
                o.center = positionIndexed.Position;

                return o;
            }

            [maxvertexcount(6)]
            void geom(point v2g i[1], inout TriangleStream<g2f> triangles)
            {
                const float3 p = i[0].center;
                const float3 v = normalize(i[0].center - CameraPosition);
                const float3 right = cross(v, ParticleUp);

                g2f o;

                o.wpos = p + right + ParticleUp;
                o.vertex = UnityWorldToClipPos(o.wpos);
                o.uv = float2(1.0, 1.0);
                triangles.Append(o);

                o.wpos = p - right + ParticleUp;
                o.vertex = UnityWorldToClipPos(o.wpos);
                o.uv = float2(0.0, 1.0);
                triangles.Append(o);

                o.wpos = p + right - ParticleUp;
                o.vertex = UnityWorldToClipPos(o.wpos);
                o.uv = float2(1.0, 0.0);
                triangles.Append(o);
                triangles.RestartStrip();

                triangles.Append(o);

                o.wpos = p - right + ParticleUp;
                o.vertex = UnityWorldToClipPos(o.wpos);
                o.uv = float2(0.0, 1.0);
                triangles.Append(o);
                
                o.wpos = p - right - ParticleUp;
                o.vertex = UnityWorldToClipPos(o.wpos);
                o.uv = float2(0.0, 0.0);
                triangles.Append(o);
                triangles.RestartStrip();
            }

            float4 frag(g2f i) : SV_Target
            {
                const float sdf = max(0.5 - length(i.uv - 0.5), 0.0);
                return float4(sdf, 0.0, 0.0, sdf > 0.0);
            }
            ENDCG
        }
    }
}
