#ifndef HICKV_PROJECTION
    #define HICKV_PROJECTION

    // OS -> WS -> VP -> CP -> NDC

    // most were taken from keijiros work

    // Inverse project UV + raw depth into the view space.
    float3 ProjectSPtoVP(float2 uv, float depthRaw)
    {
        float4 cp = float4(float3(uv, depthRaw) * 2 - 1, 1);
        float4 vp = mul(unity_CameraInvProjection, cp);
        // return float3(vp.xy, -vp.z) / vp.w;
        return float3(vp.xy, vp.z) / vp.w;
    }

    // Inverse project CP + raw depth into the view space.
    float3 ProjectCPtoVP(float2 cp, float depthRaw)
    {
        float4 cp2 = float4(float3(cp, depthRaw), 1);
        float4 vp = mul(unity_CameraInvProjection, cp2);
        // return float3(vp.xy, -vp.z) / vp.w;
        return float3(vp.xy, vp.z) / vp.w;
    }

    // Inverse project UV + raw depth into the world space.
    float3 ProjectNDCtoWS(float2 uv, float depthRaw)
    {
        // float3 vp = ProjectSPtoVP(uv, depthRaw) * float3(1,1,1);
        float3 vp = ProjectSPtoVP(uv, depthRaw) * float3(1,1,-1);
        return mul(unity_CameraToWorld, float4(vp, 1)).xyz;
    }

    // Inverse project CP + raw depth into the world space.
    float3 ProjectCPtoWS(float2 cp, float depthRaw)
    {
        // float3 vp = ProjectCPtoVP(cp, depthRaw) * float3(1,1,1);
        float3 vp = ProjectCPtoVP(cp, depthRaw) * float3(1,1,-1);
        return mul(unity_CameraToWorld, float4(vp, 1)).xyz;
    }

    // Project a view space position into the screen space.
    float3 ProjectVPtoNDC(float3 vp)
    {
        float4 cp = mul(unity_CameraProjection, float4(vp.xy, -vp.z, 1));
        return ((cp.xyz / cp.w) + 1) / 2;
    }

    // Project a view space position into the screen space.
    float4 ProjectVPtoCP(float3 vp)
    {
        float4 cp = mul(unity_CameraProjection, float4(vp.xy, -vp.z, 1));
        return cp;
    }

    // Project a world space position into the screen space.
    float3 ProjectWStoNDC(float3 ws)
    {
        float3 vp = mul(unity_WorldToCamera, float4(ws, 1)).xyz;
        return ProjectVPtoNDC(vp);
    }

    // Project a world space position into the screen space.
    float4 ProjectWStoCP(float3 ws)
    {
        float3 vp = mul(unity_WorldToCamera, float4(ws, 1)).xyz;
        return ProjectVPtoCP(vp);
    }

    // Project a world space position into the screen space.
    float4 ProjectWStoDepth(float3 ws)
    {
        float3 vp = mul(unity_WorldToCamera, float4(ws, 1)).xyz;
        return ProjectVPtoCP(vp);
    }

#endif