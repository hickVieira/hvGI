Shader "hickv/SHPreview"
{
    Properties
    {
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }

        Pass
        {
            HLSLPROGRAM
            #pragma target 4.0

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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

            float3 _y0;
            float3 _y1;
            float3 _y2;
            float3 _y3;
            float3 _y4;
            float3 _y5;
            float3 _y6;
            float3 _y7;
            float3 _y8;

            struct Attributes
            {
                float3 positionOS 	: POSITION;
                float3 normalOS 	: NORMAL;
                float4 tangentOS 	: TANGENT;
            };

            struct Varyings
            {
                float4 positionCS 	: SV_POSITION;
                float3 normalWS 	: TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = vertexInput.positionCS;
                output.normalWS = normalInput.normalWS;

                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float3 v = input.normalWS.xyz;
                v = normalize(v);
                float3 result =
                _y0 * Y0(v) +
                _y1 * Y1(v) +
                _y2 * Y2(v) +
                _y3 * Y3(v) +
                _y4 * Y4(v) +
                _y5 * Y5(v) +
                _y6 * Y6(v) +
                _y7 * Y7(v) +
                _y8 * Y8(v);

                return float4(result, 1);
            }
            ENDHLSL
        }
    }
}
