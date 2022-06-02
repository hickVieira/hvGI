Shader "hickv/WorldSpacePosition"
{
    Properties
    {
    }
    SubShader
    {
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Utility.hlsl"
            #include "Projection.hlsl"

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
                float depthRaw = SampleRawDepth(input.uv);

                float3 positionWS = ProjectNDCtoWS(input.uv, depthRaw);

                return float4(positionWS, depthRaw);
            }
            ENDHLSL
        }
    }
}
