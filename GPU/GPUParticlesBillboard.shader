Shader "Unlit/GPUParticlesBillboard"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _Size      ("Size", Float) = 0.05
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        ZWrite On
        ZTest LEqual
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:Setup

            #include "UnityCG.cginc"

            struct Particle { float3 pos; float3 vel; };
            StructuredBuffer<Particle> _ParticleBuffer;

            float4 _BaseColor;
            float  _Size;

            void Setup() {}

            struct appdata {
                float3 vertex : POSITION;   
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f { float4 pos : SV_POSITION; };

            v2f vert (appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                uint id = unity_InstanceID;

                float3 center = _ParticleBuffer[id].pos;

                
                float3 camRight = float3(UNITY_MATRIX_I_V._m00, UNITY_MATRIX_I_V._m10, UNITY_MATRIX_I_V._m20);
                float3 camUp    = float3(UNITY_MATRIX_I_V._m01, UNITY_MATRIX_I_V._m11, UNITY_MATRIX_I_V._m21);

                float2 o = v.vertex.xy; 
                float3 worldPos = center + (camRight * o.x + camUp * o.y) * _Size;

                v2f o2;
                o2.pos = UnityWorldToClipPos(float4(worldPos, 1));
                return o2;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return _BaseColor;
            }
            ENDHLSL
        }
    }
}
