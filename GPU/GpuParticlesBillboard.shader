Shader "Unlit/GpuParticlesBillboard"
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
            #pragma target   4.5
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:Setup

            #include "UnityCG.cginc"

            // Muss 1:1 zu deinem C#/Compute-Struct passen (ohne Padding)
            struct Particle {
                float3 positionP;
                float3 positionX;
                float3 velocity;
                float  m;
                float  w;
                float  radius;
            };

            StructuredBuffer<Particle> _ParticleBuffer;

            float4 _BaseColor;
            float  _Size;

            void Setup() {}

            struct appdata {
                float3 vertex : POSITION; // Quad-Vertex (-0.5..0.5)
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f { float4 pos : SV_POSITION; };

            v2f vert (appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                uint id = unity_InstanceID;              // Index ins Buffer

                // Render aus der vorhergesagten Position
                float3 center = _ParticleBuffer[id].positionX;

                // Kamera-Billboarding
                float3 camRight = float3(UNITY_MATRIX_I_V._m00, UNITY_MATRIX_I_V._m10, UNITY_MATRIX_I_V._m20);
                float3 camUp    = float3(UNITY_MATRIX_I_V._m01, UNITY_MATRIX_I_V._m11, UNITY_MATRIX_I_V._m21);

                float2 q = v.vertex.xy;                  // -0.5..0.5
                float3 worldPos = center + (camRight * q.x + camUp * q.y) * _Size;

                v2f o;
                o.pos = UnityWorldToClipPos(float4(worldPos, 1));
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return _BaseColor;
            }
            ENDHLSL
        }
    }
}
