#ifndef HICKV_RAYMARCH
    #define HICKV_RAYMARCH
    // functions from inigo quilez (iq)
    // https://iquilezles.org/

    // SHAPES
    float sdPlane(float3 p, float3 n, float h)
    {
        // n must be normalized
        return dot(p, n) + h;
    }

    float sdSphere(float3 p, float4 spherePosRad)
    {
        return length(p - spherePosRad.xyz) - spherePosRad.w;
    }

    float sdSphere(float3 p, float radius)
    {
        return length(p) - radius;
    }

    float sdRoundBox( float3 p, float3 b, float r )
    {
        float3 q = abs(p) - b;
        return length(max(q,0.0)) + min(max(q.x,max(q.y,q.z)),0.0) - r;
    }

    float sdCapsule( float3 p, float3 a, float3 b, float r )
    {
        float3 pa = p - a, ba = b - a;
        float h = clamp( dot(pa,ba)/dot(ba,ba), 0.0, 1.0 );
        return length( pa - ba*h ) - r;
    }

    float sdCappedCone( float3 p, float h, float r1, float r2 )
    {
        float2 q = float2( length(p.xz), p.y );
        float2 k1 = float2(r2,h);
        float2 k2 = float2(r2-r1,2.0*h);
        float2 ca = float2(q.x-min(q.x,(q.y<0.0)?r1:r2), abs(q.y)-h);
        float2 cb = q - k1 + k2*clamp( dot(k1-q,k2)/dot(k2,k2), 0.0, 1.0 );
        float s = (cb.x<0.0 && ca.y<0.0) ? -1.0 : 1.0;
        return s*sqrt( min(dot(ca, ca),dot(cb, cb)) );
    }
#endif