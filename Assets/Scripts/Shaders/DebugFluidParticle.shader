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
            #pragma target 4.5
            #pragma require geometry
            #pragma enable_d3d11_debug_symbols

            #pragma vertex vert
            #pragma geometry geom
            #pragma fragment frag

            #include "UnityCG.cginc"

            #define A_UAV_READONLY
            #include "../Physics/FluidData.cginc"
            #undef A_UAV_READONLY

            struct appdata
            {
                uint instance_id : SV_InstanceID;
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
                const ParticlePositionIndexed positionIndexed = GetFluidParticlePosition(v.instance_id, pingpong);
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

            void frag(g2f i, out float4 color : SV_Target, out float depth : SV_Depth)
            {
                const float2 xy = i.uv - 0.5;
                const float sdf = max(0.5 - length(xy), 0.0);
                color = float4(sdf * 0.5 + 0.5, 0.0, 0.0, sdf > 0.0);

                depth = sdf > 0.0 ? i.vertex.z : 0.0;
            }
            ENDCG
        }
    }
}
