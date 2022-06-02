Shader "hickv/LightProbeGI"
{
    Properties
    {
    }
    SubShader
    {
        Pass
        {
            // Blend [_SrcBlend][_DstBlend]
            Blend one one
            ZWrite off
            Cull back

            HLSLPROGRAM
            #pragma target 4.5

            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment

            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityGBuffer.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/Utils/Deferred.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

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

            // tetrahedron
            float3 a;
            float3 b;
            float3 abc;
            float3 abd;
            float3 acd;
            float3 bdc;
            float totalVolume;

            // sh
            float3 sha_y0;
            float3 sha_y1;
            float3 sha_y2;
            float3 sha_y3;
            float3 sha_y4;
            float3 sha_y5;
            float3 sha_y6;
            float3 sha_y7;
            float3 sha_y8;

            float3 shb_y0;
            float3 shb_y1;
            float3 shb_y2;
            float3 shb_y3;
            float3 shb_y4;
            float3 shb_y5;
            float3 shb_y6;
            float3 shb_y7;
            float3 shb_y8;

            float3 shc_y0;
            float3 shc_y1;
            float3 shc_y2;
            float3 shc_y3;
            float3 shc_y4;
            float3 shc_y5;
            float3 shc_y6;
            float3 shc_y7;
            float3 shc_y8;

            float3 shd_y0;
            float3 shd_y1;
            float3 shd_y2;
            float3 shd_y3;
            float3 shd_y4;
            float3 shd_y5;
            float3 shd_y6;
            float3 shd_y7;
            float3 shd_y8;

            #define BIAS 0.000001
            SAMPLER(SamplerState_Point_Clamp);
            TEXTURE2D_X_HALF(_GBuffer0);
            TEXTURE2D(_WorldSpacePosition);

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

            // https://www.cdsimpson.net/2014/10/barycentric-coordinates.html
            float4 Tetrahedron_BaryCentricCoordinates(float3 p, float3 a, float3 b)
            {
                // now we can calculate volumes
                float abcpVolume = abs(dot(a - p, abc) / 6);
                float abdpVolume = abs(dot(a - p, abd) / 6);
                float acdpVolume = abs(dot(a - p, acd) / 6);
                float bdcpVolume = abs(dot(b - p, bdc) / 6);
                return float4(bdcpVolume, acdpVolume, abdpVolume, abcpVolume) / totalVolume;
            }

            float3 SHL2ColorA(float3 normalWS)
            {
                return
                sha_y0 * Y0(normalWS) +
                sha_y1 * Y1(normalWS) +
                sha_y2 * Y2(normalWS) +
                sha_y3 * Y3(normalWS) +
                sha_y4 * Y4(normalWS) +
                sha_y5 * Y5(normalWS) +
                sha_y6 * Y6(normalWS) +
                sha_y7 * Y7(normalWS) +
                sha_y8 * Y8(normalWS);
            }

            float3 SHL2ColorB(float3 normalWS)
            {
                return
                shb_y0 * Y0(normalWS) +
                shb_y1 * Y1(normalWS) +
                shb_y2 * Y2(normalWS) +
                shb_y3 * Y3(normalWS) +
                shb_y4 * Y4(normalWS) +
                shb_y5 * Y5(normalWS) +
                shb_y6 * Y6(normalWS) +
                shb_y7 * Y7(normalWS) +
                shb_y8 * Y8(normalWS);
            }

            float3 SHL2ColorC(float3 normalWS)
            {
                return
                shc_y0 * Y0(normalWS) +
                shc_y1 * Y1(normalWS) +
                shc_y2 * Y2(normalWS) +
                shc_y3 * Y3(normalWS) +
                shc_y4 * Y4(normalWS) +
                shc_y5 * Y5(normalWS) +
                shc_y6 * Y6(normalWS) +
                shc_y7 * Y7(normalWS) +
                shc_y8 * Y8(normalWS);
            }

            float3 SHL2ColorD(float3 normalWS)
            {
                return
                shd_y0 * Y0(normalWS) +
                shd_y1 * Y1(normalWS) +
                shd_y2 * Y2(normalWS) +
                shd_y3 * Y3(normalWS) +
                shd_y4 * Y4(normalWS) +
                shd_y5 * Y5(normalWS) +
                shd_y6 * Y6(normalWS) +
                shd_y7 * Y7(normalWS) +
                shd_y8 * Y8(normalWS);
            }

            float4 LitPassFragment(Varyings input) : SV_Target
            {
                // float depthRaw = SampleRawDepth(input.uv);
                // float3 positionWS = ProjectNDCtoWS(input.uv, depthRaw);

                float4 worldPosDepth = SAMPLE_TEXTURE2D_LOD(_WorldSpacePosition, SamplerState_Point_Clamp, input.uv, 0);
                float depthRaw = worldPosDepth.a;

                UNITY_BRANCH
                if(depthRaw >= 1) return 0;

                float3 positionWS = worldPosDepth.xyz;

                float4 weights = Tetrahedron_BaryCentricCoordinates(positionWS, a, b);
                float t = weights.x + weights.y + weights.z + weights.w;

                UNITY_BRANCH
                if (t > 1 + BIAS) return 0;

                float3 normalWS = SampleSceneNormals(input.uv);

                float3 color0 = SHL2ColorA(normalWS);
                float3 color1 = SHL2ColorB(normalWS);
                float3 color2 = SHL2ColorC(normalWS);
                float3 color3 = SHL2ColorD(normalWS);

                float3 irradiance = (color0 * weights.x + color1 * weights.y + color2 * weights.z + color3 * weights.w) * t;

                half4 gbuffer0 = SAMPLE_TEXTURE2D_X_LOD(_GBuffer0, SamplerState_Point_Clamp, input.uv, 0);

                return float4(irradiance, 1) * gbuffer0;
            }
            ENDHLSL
        }
    }
}
