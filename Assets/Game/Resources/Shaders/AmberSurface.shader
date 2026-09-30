Shader "Emberfield/AmberSurface"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 world : TEXCOORD0; half3 color : COLOR; half fogFactor : TEXCOORD1; };
            Varyings Vert(Attributes input) { Varyings output; output.world = TransformObjectToWorld(input.positionOS.xyz); output.positionCS = TransformWorldToHClip(output.world); output.color = input.color.rgb; output.fogFactor = ComputeFogFactor(output.positionCS.z); return output; }
            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Noise(float2 p) { float2 i=floor(p), f=frac(p); f=f*f*(3-2*f); return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),lerp(Hash(i+float2(0,1)),Hash(i+1),f.x),f.y); }
            half4 Frag(Varyings input) : SV_Target
            {
                float grain = Noise(input.world.xz * 5.7);
                float patches = Noise(input.world.xz * .58 + 8.1);
                half3 albedo = input.color * (.93 + patches * .08 + grain * .055);
                Light sun = GetMainLight(TransformWorldToShadowCoord(input.world));
                half3 ambient = max(SampleSH(half3(0,1,0)), half3(.23,.28,.29));
                half diffuse = saturate(sun.direction.y) * sun.shadowAttenuation;
                return half4(MixFog(albedo * (ambient + sun.color * diffuse * .8), input.fogFactor), 1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
