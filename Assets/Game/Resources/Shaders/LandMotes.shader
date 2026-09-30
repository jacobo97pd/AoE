Shader "Emberfield/Land Motes"
{
    // Embers and thin smoke over lava and a volcano, with no particle system and no work on the CPU: LandMotes bakes
    // one quad per mote at its source, with its phase, speed, heading and size in UV1, and this shader flies it. Each
    // mote rises for its life, drifts downwind, wobbles, faces the camera and fades in and out, then starts again from
    // its source. Embers add light above the bloom threshold; smoke is a soft translucent puff that grows as it thins.
    Properties
    {
        _Color("Colour (HDR for embers)", Color) = (1, .45, .12, 1)
        _Cool("Colour as it cools", Color) = (.6, .12, .04, 1)
        _Life("Life in seconds", Float) = 3.5
        _Rise("Rise in metres", Float) = 3
        _Drift("Drift in metres (xz)", Vector) = (.8, 0, .35, 0)
        _Size("Size in metres", Float) = .08
        _Grow("Growth over life", Float) = 0
        _Softness("Edge softness", Range(.05, 1)) = .5
        [Enum(UnityEngine.Rendering.BlendMode)] _Source("Source blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _Destination("Destination blend", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Blend [_Source] [_Destination]
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Color, _Cool, _Drift;
                float _Life, _Rise, _Size, _Grow, _Softness, _Source, _Destination;
            CBUFFER_END
            // uv: the quad's corner (-1..1). uv1: phase, speed, heading in turns, size scale.
            struct Attributes { float4 positionOS : POSITION; float2 corner : TEXCOORD0; float4 mote : TEXCOORD1; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 corner : TEXCOORD0; half2 life : TEXCOORD1; half fog : TEXCOORD2; };
            Varyings Vert(Attributes i)
            {
                Varyings o;
                float age = frac(_Time.y / (_Life * lerp(.75, 1.25, i.mote.y)) + i.mote.x);
                float heading = i.mote.z * 6.2832;
                float3 origin = TransformObjectToWorld(i.positionOS.xyz);
                float3 world = origin + float3(0, _Rise * age * lerp(.7, 1.3, i.mote.y), 0)
                    + float3(_Drift.x, 0, _Drift.z) * age * age
                    + float3(cos(heading), 0, sin(heading)) * (.25 + .5 * age) * age * (.6 + _Grow * .6)
                    + float3(sin(age * 9 + heading * 3), 0, cos(age * 7 + heading * 2)) * .12 * age;
                float size = _Size * i.mote.w * (1 + _Grow * age);
                float3 view = TransformWorldToView(world) + float3(i.corner * size, 0);
                o.positionCS = TransformWViewToHClip(view);
                o.corner = i.corner;
                // Fade in quickly, out slowly: a mote appears as a spark and dies away.
                o.life = half2(age, smoothstep(0, .12, age) * (1 - smoothstep(.55, 1, age)));
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                half distance = length(i.corner);
                half body = 1 - smoothstep(1 - _Softness, 1, distance);
                half3 colour = lerp(_Color.rgb, _Cool.rgb, i.life.x);
                half alpha = body * i.life.y * _Color.a;
                // An additive ember scales its light; a blended puff keeps its colour and thins by alpha.
                // Haze fades an ember's light out and pulls a puff toward the haze's colour.
                half additive = step(_Destination, 1.5) * step(.5, _Source);
                return additive > .5 ? half4(MixFogColor(colour, half3(0, 0, 0), i.fog) * alpha, alpha) : half4(MixFog(colour, i.fog), alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
