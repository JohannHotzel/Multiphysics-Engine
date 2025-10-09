Shader "Hidden/XPBD/GpuConstraintLines"
{
    Properties{
        _BaseColor("Color", Color) = (0,0.8,1,1)
    }
    SubShader{
        Tags{ "Queue"="Transparent" "RenderType"="Transparent" }
        ZTest LEqual
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off

        Pass{
            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   4.5
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

            struct DistanceConstraint {
                uint i;
                uint j;
                float rest;
                float compliance;
            };

            StructuredBuffer<Particle>           _ParticleBuffer; 
            StructuredBuffer<DistanceConstraint> _Constraints;
            int   _ConstraintCount;
            float4 _BaseColor;

            struct v2f { float4 pos : SV_Position; };

            v2f vert(uint vid : SV_VertexID)
            {
                v2f o;


                uint lineIndex = vid >> 1;    // /2
                uint whichEnd  = vid & 1;     // 0 or 1
                lineIndex = min(lineIndex, (uint)max(_ConstraintCount - 1, 0));

                DistanceConstraint con = _Constraints[lineIndex];
                uint pIndex = (whichEnd == 0) ? con.i : con.j;

                float3 wp = _ParticleBuffer[pIndex].positionX;

                o.pos = mul(UNITY_MATRIX_VP, float4(wp, 1.0));
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return _BaseColor;
            }

            ENDHLSL
        }
    }
}
