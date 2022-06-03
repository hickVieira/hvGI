Shader "hickv/SDFProbe"
{
    Properties
    {
        [MainTexture] _MainTex("MainTex", 2D) = "black" {} 
    }
    SubShader
    {
        // occlusion accumulate
        Pass
        {
            Blend one zero
            ZWrite off
            Cull back

            HLSLPROGRAM
            #pragma target 4.5

            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment

            #pragma shader_feature_local_fragment _IS_SPHERE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/Scripts/Raymarch.hlsl"

            // sdf shape data
            float4x4 _BoxMatrix;
            float3 _BoxSize;
            float2 _BoxRadius;
            float _BoxIntensity;

            TEXTURE2D(_WorldSpacePosition);
            TEXTURE2D(_MainTex);
            SAMPLER(SamplerState_Point_Clamp);

            struct Attributes
            {
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float2 uv               : TEXCOORD0;
                float4 positionCS       : SV_POSITION;
            };

            Varyings LitPassVertex(Attributes input)
            {
                Varyings output;

                output.uv = input.uv;

                // get clipPos for calculating view ray
                float2 cp = float2(input.uv * 2 - 1);

                output.positionCS = float4(cp.x, -cp.y, 1, 1);

                return output;
            }

            float LitPassFragment(Varyings input) : SV_Target
            {
                // float depthRaw = SampleRawDepth(input.uv);
                // float3 positionWS = ProjectNDCtoWS(input.uv, depthRaw);

                float4 worldPosDepth = SAMPLE_TEXTURE2D_LOD(_WorldSpacePosition, SamplerState_Point_Clamp, input.uv, 0);
                float depthRaw = worldPosDepth.a;

                UNITY_BRANCH
                if (depthRaw >= 1) return 0;

                float oldSample = SAMPLE_TEXTURE2D_LOD(_MainTex, SamplerState_Point_Clamp, input.uv, 0).r;

                float3 positionWS = worldPosDepth.xyz;
                
                #if _IS_SPHERE
                    float3 spherePos = float3(_BoxMatrix[0].w, _BoxMatrix[1].w, _BoxMatrix[2].w);
                    float sdf = _BoxIntensity * saturate(1 - sdSphere(positionWS + spherePos, _BoxRadius.x) / _BoxRadius.y);
                #else
                    float3 localP = mul(_BoxMatrix, float4(positionWS, 1.0));
                    float sdf = _BoxIntensity * saturate(1 - sdRoundBox(localP, _BoxSize, _BoxRadius.x) / _BoxRadius.y);
                #endif

                return max(sdf, oldSample);
            }
            ENDHLSL
        }

        // occlusion merge
        Pass
        {
            Blend one zero
            ZWrite off
            Cull back

            HLSLPROGRAM
            #pragma target 4.5

            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityGBuffer.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/Utils/Deferred.hlsl"

            TEXTURE2D_X_HALF(_GBuffer0);
            TEXTURE2D_X_HALF(_GBuffer3Temp);
            TEXTURE2D(_MainTex);
            SAMPLER(SamplerState_Point_Clamp);

            struct Attributes
            {
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float2 uv               : TEXCOORD0;
                float4 positionCS       : SV_POSITION;
            };

            Varyings LitPassVertex(Attributes input)
            {
                Varyings output;

                output.uv = input.uv;

                // get clipPos for calculating view ray
                float2 cp = float2(input.uv * 2 - 1);

                output.positionCS = float4(cp.x, -cp.y, 1, 1);

                return output;
            }

            float4 LitPassFragment(Varyings input) : SV_Target
            {
                float occlusion = 1 - SAMPLE_TEXTURE2D_LOD(_MainTex, SamplerState_Point_Clamp, input.uv, 0).r;
                float4 gbuffer3 = SAMPLE_TEXTURE2D_X_LOD(_GBuffer3Temp, SamplerState_Point_Clamp, input.uv, 0);

                return gbuffer3 * occlusion;
            }
            ENDHLSL
        }

        // light accumulate
        Pass
        {
            Blend one zero
            ZWrite off
            Cull back

            HLSLPROGRAM
            #pragma target 4.5

            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment

            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma shader_feature_local_fragment _IS_SPHERE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
            #include "Assets/Scripts/Raymarch.hlsl"

            #define PI 3.14159265358
            #define Y0(v) (1.0 / 2.0) * sqrt(1.0 / PI)
            #define Y1(v) sqrt(3.0 / (4.0 * PI)) * v.z
            #define Y2(v) sqrt(3.0 / (4.0 * PI)) * v.y
            #define Y3(v) sqrt(3.0 / (4.0 * PI)) * v.x
            #define Y4(v) 1.0 / 2.0 * sqrt(15.0 / PI) * v.x * v.z
            #define Y5(v) 1.0 / 2.0 * sqrt(15.0 / PI) * v.z * v.y
            #define Y6(v) 1.0 / 4.0 * sqrt(5.0 / PI) * (-v.x * v.x - v.z * v.z + 2 * v.y * v.y)
            #define Y7(v) 1.0 / 2.0 * sqrt(15.0 / PI) * v.y * v.x
            #define Y8(v) 1.0 / 4.0 * sqrt(15.0 / PI) * (v.x * v.x - v.z * v.z)

            // sh data
            float3 _y0;
            float3 _y1;
            float3 _y2;
            float3 _y3;
            float3 _y4;
            float3 _y5;
            float3 _y6;
            float3 _y7;
            float3 _y8;

            // sdf shape data
            float4x4 _BoxMatrix;
            float3 _BoxSize;
            float2 _BoxRadius;
            float _BoxIntensity;

            TEXTURE2D(_WorldSpacePosition);
            TEXTURE2D(_MainTex);
            SAMPLER(SamplerState_Point_Clamp);

            struct Attributes
            {
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float2 uv               : TEXCOORD0;
                float4 positionCS       : SV_POSITION;
            };

            Varyings LitPassVertex(Attributes input)
            {
                Varyings output;

                output.uv = input.uv;

                // get clipPos for calculating view ray
                float2 cp = float2(input.uv * 2 - 1);

                output.positionCS = float4(cp.x, -cp.y, 1, 1);

                return output;
            }

            float3 SHL2Color(float3 normalWS)
            {
                return
                _y0 * Y0(normalWS) +
                _y1 * Y1(normalWS) +
                _y2 * Y2(normalWS) +
                _y3 * Y3(normalWS) +
                _y4 * Y4(normalWS) +
                _y5 * Y5(normalWS) +
                _y6 * Y6(normalWS) +
                _y7 * Y7(normalWS) +
                _y8 * Y8(normalWS);
            }

            float4 LitPassFragment(Varyings input) : SV_Target
            {
                // float depthRaw = SampleRawDepth(input.uv);
                // float3 positionWS = ProjectNDCtoWS(input.uv, depthRaw);

                float4 worldPosDepth = SAMPLE_TEXTURE2D_LOD(_WorldSpacePosition, SamplerState_Point_Clamp, input.uv, 0);
                float depthRaw = worldPosDepth.a;

                UNITY_BRANCH
                if (depthRaw >= 1) return 0;

                float3 normalWS = SampleSceneNormals(input.uv);
                float4 oldSample = SAMPLE_TEXTURE2D_LOD(_MainTex, SamplerState_Point_Clamp, input.uv, 0);

                float3 positionWS = worldPosDepth.xyz;

                #if _IS_SPHERE
                    float3 spherePos = float3(_BoxMatrix[0].w, _BoxMatrix[1].w, _BoxMatrix[2].w);
                    float sdf = _BoxIntensity * saturate(1 - sdSphere(positionWS + spherePos, _BoxRadius.x) / _BoxRadius.y);
                #else
                    float3 localP = mul(_BoxMatrix, float4(positionWS, 1.0));
                    float sdf = _BoxIntensity * saturate(1 - sdRoundBox(localP, _BoxSize, _BoxRadius.x) / _BoxRadius.y);
                #endif

                float3 sh = SHL2Color(normalWS);

                return float4(max(oldSample, sh * sdf), 1);
            }
            ENDHLSL
        }

        // light merge
        Pass
        {
            Blend one one
            ZWrite off
            Cull back

            HLSLPROGRAM
            #pragma target 4.5

            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityGBuffer.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/Utils/Deferred.hlsl"

            TEXTURE2D_X_HALF(_GBuffer0);
            TEXTURE2D(_MainTex);
            SAMPLER(SamplerState_Point_Clamp);

            struct Attributes
            {
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float2 uv               : TEXCOORD0;
                float4 positionCS       : SV_POSITION;
            };

            Varyings LitPassVertex(Attributes input)
            {
                Varyings output;

                output.uv = input.uv;

                // get clipPos for calculating view ray
                float2 cp = float2(input.uv * 2 - 1);

                output.positionCS = float4(cp.x, -cp.y, 1, 1);

                return output;
            }

            float4 LitPassFragment(Varyings input) : SV_Target
            {
                float3 light = SAMPLE_TEXTURE2D_LOD(_MainTex, SamplerState_Point_Clamp, input.uv, 0).rgb;
                float4 gbuffer0 = SAMPLE_TEXTURE2D_X_LOD(_GBuffer0, SamplerState_Point_Clamp, input.uv, 0);

                return float4(gbuffer0.rgb * light, gbuffer0.a);
            }
            ENDHLSL
        }
    }
}
