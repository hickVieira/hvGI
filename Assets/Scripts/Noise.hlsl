#ifndef HICKV_NOISE
    #define HICKV_NOISE

    #include "Utility.hlsl"

    // https://stackoverflow.com/questions/4200224/random-noise-functions-for-glsl
    float gold_noise(in float2 xy, in float seed)
    {
        return frac(tan(distance(xy*GOLDEN_RATIO, xy)*seed)*xy.x);
    }

    float rand(float2 uv)
    {
        float a = 12.9898;
        float b = 78.233;
        float c = 43758.5453;
        float dt= dot(uv.xy ,float2(a,b));
        float sn= fmod(dt, PI);
        return frac(sin(sn) * c);
    }

    float randTime(float2 uv)
    {
        float a = 12.9898;
        float b = 78.233;
        float c = 43758.5453;
        float dt= dot(uv.xy, float2(a,b)) * _Time.x;
        float sn= fmod(dt, PI);
        return frac(sin(sn) * c);
    }

    // https://www.gamedev.net/articles/programming/graphics/contact-hardening-soft-shadows-made-fast-r4906/
    float InterleavedGradientNoiseSimple(float2 position_screen)
    {
        float3 magic = float3(0.06711056f, 0.00583715f, 52.9829189f);
        return frac(magic.z * frac(dot(position_screen, magic.xy)));
    }

    float ScreenSpaceDither(float2 vScreenPos)
    {
        // Iestyn's RGB dither (7 asm instructions) from Portal 2 X360, slightly modified for VR
        //float3 vDither = float3( dot( float2( 171.0, 231.0 ), vScreenPos.xy + iTime ) );
        float vDither = dot( float2( 171.0, 231.0 ), vScreenPos.xy );
        return frac( vDither / 91.111 );
        //vDither.rgb = frac( vDither.rgb / float3( 103.0, 71.0, 97.0 ) );
        //return vDither.rgb / 255.0; //note: looks better without 0.375...

        //note: not sure why the 0.5-offset is there...
        //vDither.rgb = frac( vDither.rgb / float3( 103.0, 71.0, 97.0 ) ) - float3( 0.5, 0.5, 0.5 );
        //return (vDither.rgb / 255.0) * 0.375;
    }

    float2 VogelDiskSample(int sampleIndex, int samplesCount, float phi)
    {
        float GoldenAngle = 2.4;

        float r = sqrt(sampleIndex + 0.5f) / sqrt(samplesCount);
        float theta = sampleIndex * GoldenAngle + phi;

        float sine, cosine;
        sincos(theta, sine, cosine);

        return float2(r * cosine, r * sine);
    }

    // from The Books of Shaders
    // https://thebookofshaders.com/13/
    float random2d(float2 coord)
    {
        return frac(sin(dot(coord.xy, float2(12.9898, 78.233))) * 43758.5453);
    }

    float random2dTime(float2 coord)
    {
        return frac(sin(dot(coord.xy, float2(12.9898, 78.233)) * fmod(_Time.y, 100)) * 43758.5453);
    }

    // Based on Morgan McGuire @morgan3d
    // https://www.shadertoy.com/view/4dS3Wd
    float noise2d (float2 st)
    {
        float2 i = floor(st);
        float2 f = frac(st);

        // Four corners in 2D of a tile
        float a = random2dTime(i);
        float b = random2dTime(i + float2(1.0, 0.0));
        float c = random2dTime(i + float2(0.0, 1.0));
        float d = random2dTime(i + float2(1.0, 1.0));

        float2 u = f * f * (3.0 - 2.0 * f);

        return lerp(a, b, u.x) +
        (c - a)* u.y * (1.0 - u.x) +
        (d - b) * u.x * u.y;
    }

    // Based on Morgan McGuire @morgan3d
    // https://www.shadertoy.com/view/4dS3Wd
    float noise2dTime(float2 st)
    {
        float2 i = floor(st);
        float2 f = frac(st);

        // Four corners in 2D of a tile
        float a = random2d(i);
        float b = random2d(i + float2(1.0, 0.0));
        float c = random2d(i + float2(0.0, 1.0));
        float d = random2d(i + float2(1.0, 1.0));

        float2 u = f * f * (3.0 - 2.0 * f);

        return lerp(a, b, u.x) +
        (c - a)* u.y * (1.0 - u.x) +
        (d - b) * u.x * u.y;
    }

    // Inigo Quilez noise
    // https://www.shadertoy.com/view/4sfGzS
    float hash(float3 p)  // replace this by something better
    {
        p  = frac( p*0.3183099+.1 );
        p *= 17.0;
        return frac( p.x*p.y*p.z*(p.x+p.y+p.z) );
    }

    float noise3d(float3 x)
    {
        float3 i = floor(x);
        float3 f = frac(x);
        f = f*f*(3.0-2.0*f);
        
        return lerp(lerp(lerp( hash(i+float3(0,0,0)), 
        hash(i+float3(1,0,0)),f.x),
        lerp( hash(i+float3(0,1,0)), 
        hash(i+float3(1,1,0)),f.x),f.y),
        lerp(lerp( hash(i+float3(0,0,1)), 
        hash(i+float3(1,0,1)),f.x),
        lerp( hash(i+float3(0,1,1)), 
        hash(i+float3(1,1,1)),f.x),f.y),f.z);
    }

    float fbm2d(float2 st, int octaves, float amplitude, float lacunarity=0.5, float freq=2)
    {
        float outValue = 0;
        // Loop of octaves
        for (int i = 0; i < octaves; i++) {
            outValue += amplitude * noise2d(st);
            st *= freq;
            amplitude *= -lacunarity;
        }
        return outValue;
    }

    float fbm3d(float3 pos, int octaves, float amplitude, float lacunarity=0.5, float freq=2)
    {
        float outValue = 0;
        // Loop of octaves
        for (int i = 0; i < octaves; i++) {
            outValue += amplitude * noise3d(pos);
            pos *= freq;
            amplitude *= -lacunarity;
        }
        return outValue;
    }
#endif