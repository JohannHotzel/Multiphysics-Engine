Shader "Xpbd/ClothGPUPos"
{
    Properties{
        _BaseColor("Color", Color) = (1,1,1,1)
        _Cull("Cull Mode (0:Back 1:Front 2:Off)", Float) = 2
    }
    SubShader{
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Cull [_Cull]

        Pass{
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 5.0
            #include "UnityCG.cginc"

            struct Particle {
                float3 positionP;
                float3 positionX;
                float3 positionPredicted;
                float3 velocity;
                float  m;
                float  w;
                float  radius;
            };

            StructuredBuffer<Particle> _Particles;
            int _StartIndex;
            int _HasNormals; //  GPU-Normals 
            float4 _BaseColor;

            struct appdata {
                uint   vertexID : SV_VertexID;
                float2 uv       : TEXCOORD0; 
            };

            struct v2f {
                float4 posCS : SV_POSITION;
                float2 uv    : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                uint i = _StartIndex + v.vertexID;

                float3 posWS = _Particles[i].positionX;

                o.posCS = mul(UNITY_MATRIX_VP, float4(posWS, 1));
                o.uv    = v.uv;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                // unlit; Lighting with GPU-Normals ergänzen
                return _BaseColor;
            }
            ENDHLSL
        }
    }
}
