Shader "URP/Xpbd/ClothGPUPos"
{
    Properties
    {
        _BaseColor       ("Base Color", Color) = (1,1,1,1)
        _Cull            ("Cull Mode (0:Back 1:Front 2:Off)", Float) = 2

        // Sichtbarkeits-Booster
        _AmbientBoost    ("Ambient Boost", Range(0,2)) = 0.4
        _EmissionColor   ("Emission Color", Color) = (0,0,0,0)
        _EmissionStrength("Emission Strength", Range(0,5)) = 0.0
        _ForceUnlit      ("Force Unlit (0/1)", Float) = 0.0
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalRenderPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Cull [_Cull]

        // ---------- Forward Lit ----------
        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma target 5.0
            #pragma vertex   Vert
            #pragma fragment Frag

            // URP lighting/shadows/fog toggles
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

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

            // per-draw (MaterialPropertyBlock)
            int    _StartIndex;
            int    _GridSizeX;
            int    _GridSizeY;

            float4 _BaseColor;
            float  _AmbientBoost;
            float4 _EmissionColor;
            float  _EmissionStrength;
            float  _ForceUnlit;

            struct Attributes
            {
                uint   vertexID : SV_VertexID;
                float2 uv       : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                float2 uv         : TEXCOORD0;
                float4 shadowCoord: TEXCOORD3;
                half   fogFactor  : TEXCOORD4;
            };

            float3 GetPosWS(int x, int y)
            {
                x = clamp(x, 0, _GridSizeX-1);
                y = clamp(y, 0, _GridSizeY-1);
                uint i = _StartIndex + (uint)(y * _GridSizeX + x);
                return _Particles[i].positionX;
            }

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;

                int vx = IN.vertexID % _GridSizeX;
                int vy = IN.vertexID / _GridSizeX;

                float3 p   = GetPosWS(vx, vy);
                float3 pxp = GetPosWS(vx+1, vy);
                float3 pxm = GetPosWS(vx-1, vy);
                float3 pyp = GetPosWS(vx, vy+1);
                float3 pym = GetPosWS(vx, vy-1);

                float3 dx = pxp - pxm;
                float3 dy = pyp - pym;

                // stabile, zweiseitige Normale
                float3 n = normalize(cross(dy, dx) + 1e-7);

                OUT.positionWS  = p;
                OUT.normalWS    = n;
                OUT.uv          = IN.uv;

                OUT.positionCS  = TransformWorldToHClip(p);
                OUT.shadowCoord = TransformWorldToShadowCoord(p);
                OUT.fogFactor   = ComputeFogFactor(OUT.positionCS.z);
                return OUT;
            }

            float3 ShadeMainLight(float3 nWS, float4 shadowCoord)
            {
                Light L = GetMainLight(shadowCoord);
                float NdotL = saturate(dot(nWS, L.direction));
                return L.color * (NdotL * L.distanceAttenuation * L.shadowAttenuation);
            }

            float3 ShadeAdditionalLights(float3 nWS, float3 positionWS)
            {
                float3 sum = 0;
                #if defined(_ADDITIONAL_LIGHTS)
                uint count = GetAdditionalLightsCount();
                [loop]
                for (uint i = 0; i < count; i++)
                {
                    Light l = GetAdditionalLight(i, positionWS);
                    float NdotL = saturate(dot(nWS, l.direction));
                    sum += l.color * (NdotL * l.distanceAttenuation * l.shadowAttenuation);
                }
                #endif
                return sum;
            }

            half4 Frag(Varyings IN, bool frontFace : SV_IsFrontFace) : SV_Target
            {
                // Unlit-Fallback (zum Debuggen)
                if (_ForceUnlit > 0.5)
                {
                    half3 colUnlit = _BaseColor.rgb + _EmissionColor.rgb * _EmissionStrength;
                    colUnlit = MixFog(colUnlit, IN.fogFactor);
                    return half4(colUnlit, _BaseColor.a);
                }

                // zweiseitig korrekt: Rückseiten-Normale umdrehen
                float3 n = normalize(IN.normalWS) * (frontFace ? 1.0 : -1.0);

                // Diffuse + SH-Ambient (verstärkt) + Additional Lights
                float3 diffuse = ShadeMainLight(n, IN.shadowCoord) + ShadeAdditionalLights(n, IN.positionWS);
                float3 ambient = SampleSH(n) * _AmbientBoost;

                // Emission oben drauf
                float3 emission = _EmissionColor.rgb * _EmissionStrength;

                float3 lit = _BaseColor.rgb * (diffuse + ambient) + emission;

                half4 outCol = half4(lit, _BaseColor.a);
                outCol.rgb = MixFog(outCol.rgb, IN.fogFactor);
                return outCol;
            }
            ENDHLSL
        }

        // ---------- ShadowCaster ----------
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }

            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 5.0
            #pragma vertex   VertShadow
            #pragma fragment FragShadow

            #pragma multi_compile _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

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
            int _GridSizeX;
            int _GridSizeY;

            struct Attributes { uint vertexID : SV_VertexID; };
            struct Varyings   { float4 positionCS : SV_POSITION; };

            float3 GetPosWS(int x, int y)
            {
                x = clamp(x, 0, _GridSizeX-1);
                y = clamp(y, 0, _GridSizeY-1);
                uint i = _StartIndex + (uint)(y * _GridSizeX + x);
                return _Particles[i].positionX;
            }

            Varyings VertShadow(Attributes IN)
            {
                Varyings OUT;

                int vx = IN.vertexID % _GridSizeX;
                int vy = IN.vertexID / _GridSizeX;

                float3 p   = GetPosWS(vx, vy);
                float3 pxp = GetPosWS(vx+1, vy);
                float3 pxm = GetPosWS(vx-1, vy);
                float3 pyp = GetPosWS(vx, vy+1);
                float3 pym = GetPosWS(vx, vy-1);

                float3 dx = pxp - pxm;
                float3 dy = pyp - pym;
                float3 n  = normalize(cross(dy, dx) + 1e-7);

                OUT.positionCS = GetShadowPositionHClip(p, n);
                return OUT;
            }

            half4 FragShadow() : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
