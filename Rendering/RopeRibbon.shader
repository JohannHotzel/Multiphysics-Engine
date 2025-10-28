// XPBD/RopeRibbon Robust
Shader "XPBD/RopeRibbon"
{
    Properties {
        _BaseColor ("Color", Color) = (1,1,1,1)
        _Radius ("World Radius", Float) = 0.03
        _PixelWidth ("Screen Width (px)", Float) = 0.0 // 0 = aus
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalRenderPipeline" "Queue"="Geometry" }
        Cull Off
        ZWrite On
        ZTest LEqual
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Particle { float3 positionP; float3 positionX; float3 positionPredicted; float3 velocity; float m; float w; float radius; };

            StructuredBuffer<Particle> _Particles;
            float4 _BaseColor;
            int _StartIndex;
            int _Count;
            float _Radius;
            float _PixelWidth; // 0 = aus

            struct Attributes {
                uint vertexID : SV_VertexID;
                float2 uv     : TEXCOORD0;
            };
            struct Varyings {
                float4 posCS : SV_Position;
                float2 uv    : TEXCOORD0;
            };

            float3 LoadPos(int logicalIndex)
            {
                logicalIndex = clamp(logicalIndex, 0, _Count - 1);
                return _Particles[_StartIndex + logicalIndex].positionX;
            }

            float4 OffsetWorldToClip(float3 worldPos, float3 worldOffset)
            {
                float4 p0 = TransformWorldToHClip(worldPos);
                float4 p1 = TransformWorldToHClip(worldPos + worldOffset);
                return p1 - p0; 
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                int logical = IN.vertexID / 2;
                int side    = IN.vertexID % 2; // 0 = -N, 1 = +N

                float3 p  = LoadPos(logical);
                float3 pr = LoadPos(logical - 1);
                float3 nx = LoadPos(logical + 1);

                float3 T = normalize((nx - pr) * 0.5);           
                float3 V = GetWorldSpaceViewDir(p);            
                float  eps = 1e-3;


                float3 N = cross(T, V);
                float nLen = length(N);
                if (nLen < eps)
                {
                    float3 camRight = UNITY_MATRIX_I_V[0].xyz; 
                    N = cross(T, camRight);
                    nLen = max(length(N), eps);
                }
                N /= nLen;

                float3 offsetWS;
                if (_PixelWidth > 0.0)
                {

                    float4 dc = OffsetWorldToClip(p, N);             
                    float2 dcN = dc.xy / max(abs(dc.w), 1e-6);      
                    float2 ndcToPix = 0.5 * float2(_ScreenParams.x, _ScreenParams.y);
                    float pixPerMeter = length(dcN * ndcToPix);
                    float meters = (_PixelWidth / max(pixPerMeter, 1e-4));
                    offsetWS = N * meters * (side == 0 ? -1 : 1);
                }
                else
                {
                    offsetWS = N * _Radius * (side == 0 ? -1 : 1);
                }

                float3 worldPos = p + offsetWS;
                OUT.posCS = TransformWorldToHClip(worldPos);
                OUT.uv    = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return _BaseColor;
            }
            ENDHLSL
        }
    }
}
