Shader "Emberfield/Smoke"
{
    // Unlit and translucent on purpose: smoke that takes the scene's light and writes depth reads as
    // a grey boulder sitting on the roof. Colour and fade come from the instance, so every puff in the
    // settlement is one mesh and one material.
    Properties
    {
        _Color ("Tint", Color) = (.72, .74, .76, .35)
        _Fade ("Fade", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float facing : TEXCOORD0; float fogFactor : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };

            UNITY_INSTANCING_BUFFER_START(EmberfieldSmoke)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(float, _Fade)
            UNITY_INSTANCING_BUFFER_END(EmberfieldSmoke)

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                // Faces turned away from the light stay a shade deeper, which keeps the volume readable
                // without lighting the puff like a solid.
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.facing = saturate(dot(normalWS, normalize(float3(.4, 1, .25))) * .5 + .62);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float4 tint = UNITY_ACCESS_INSTANCED_PROP(EmberfieldSmoke, _Color);
                float fade = UNITY_ACCESS_INSTANCED_PROP(EmberfieldSmoke, _Fade);
                half3 color = tint.rgb * input.facing;
                color = MixFog(color, input.fogFactor);
                return half4(color, tint.a * fade);
            }
            ENDHLSL
        }
    }
}
