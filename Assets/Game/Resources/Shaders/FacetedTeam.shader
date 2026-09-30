Shader "Emberfield/Faceted Team"
{
    Properties
    {
        _TeamColor ("Team tint", Color) = (1,1,1,1)
        _CosmeticPrimary ("Cosmetic primary", Color) = (1,1,1,1)
        _CosmeticAccent ("Cosmetic ornament", Color) = (.92,.64,.23,1)
        _CosmeticBlend ("Cosmetic blend", Range(0,1)) = 0
        _CosmeticEmission ("Cosmetic glow", Range(0,1)) = 0
        _PreviewLighting ("Isolated wardrobe studio", Range(0,1)) = 0
        _Sway ("Wind sway", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            UNITY_INSTANCING_BUFFER_START(Emberfield)
                UNITY_DEFINE_INSTANCED_PROP(float4, _TeamColor)
                UNITY_DEFINE_INSTANCED_PROP(float4, _CosmeticPrimary)
                UNITY_DEFINE_INSTANCED_PROP(float4, _CosmeticAccent)
                UNITY_DEFINE_INSTANCED_PROP(float, _CosmeticBlend)
                UNITY_DEFINE_INSTANCED_PROP(float, _CosmeticEmission)
                UNITY_DEFINE_INSTANCED_PROP(float, _PreviewLighting)
                UNITY_DEFINE_INSTANCED_PROP(float, _Sway)
            UNITY_INSTANCING_BUFFER_END(Emberfield)

            // Wind. Only what is planted in the ground carries it, and only above its own root: a trunk stays
            // put while the crown leans. Two waves that never line up keep a forest from breathing in unison.
            float3 Sway(float3 world, float heightOS, float amount)
            {
                if (amount <= 0 || heightOS <= 0) return world;
                float phase = world.x * .31 + world.z * .24 + _Time.y * 1.15;
                float bend = (sin(phase) + sin(phase * 1.73 + 1.3) * .42) * amount * .022 * heightOS;
                return float3(world.x + bend, world.y, world.z + bend * .55);
            }
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 color : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionWS = Sway(position.positionWS, input.positionOS.y, UNITY_ACCESS_INSTANCED_PROP(Emberfield, _Sway));
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS); output.color = input.color;
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half3 normal = normalize(input.normalWS);
                half3 tint = UNITY_ACCESS_INSTANCED_PROP(Emberfield, _TeamColor).rgb;
                half ownership = step(.5, input.color.a) * (1 - step(1.5, input.color.a));
                half ornament = step(1.5, input.color.a);
                half blend = UNITY_ACCESS_INSTANCED_PROP(Emberfield, _CosmeticBlend);
                half3 primary = UNITY_ACCESS_INSTANCED_PROP(Emberfield, _CosmeticPrimary).rgb;
                half3 accent = UNITY_ACCESS_INSTANCED_PROP(Emberfield, _CosmeticAccent).rgb;
                half luminance = dot(input.color.rgb, half3(.2126,.7152,.0722));
                // Replace hue while preserving the recipe's light/dark material variation.
                // Mixing the source red RGB into a sapphire palette produces brown wings.
                half paletteShade = .35 + saturate(luminance) * .65;
                half3 customized = lerp(input.color.rgb, primary * paletteShade, blend);
                customized = lerp(customized, lerp(input.color.rgb, accent, blend), ornament);
                half3 albedo = lerp(customized, input.color.rgb * tint, ownership);
                half glow = UNITY_ACCESS_INSTANCED_PROP(Emberfield, _CosmeticEmission) * ornament * (1 - ownership);
                if (UNITY_ACCESS_INSTANCED_PROP(Emberfield, _PreviewLighting) > .5)
                {
                    // Per-renderer preview light: no global lights, shadow maps or volume
                    // changes can alter the live battlefield behind the wardrobe.
                    half key = saturate(dot(normal, normalize(half3(-.45, .80, -.56))));
                    half fill = saturate(dot(normal, normalize(half3(.65, .30, .55))));
                    half3 studio = half3(.76, .81, .87) + key * .78 + fill * .23;
                    half rim = pow(1 - saturate(abs(dot(normal, GetWorldSpaceNormalizeViewDir(input.positionWS)))), 3) * .055;
                    return half4(albedo * studio + rim + accent * glow, 1);
                }
                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 ambient = max(SampleSH(normal), half3(.22, .24, .25));
                half diffuse = saturate(dot(normal, light.direction)) * light.distanceAttenuation * light.shadowAttenuation;
                half3 lit = albedo * (ambient + light.color * diffuse * .8) + accent * glow;
                // Distance haze separates the far side of the battlefield from the near one. The wardrobe
                // preview above returns before this: a studio shot must not inherit the map's weather.
                return half4(MixFog(lit, input.fogFactor), 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection, _LightPosition;
            UNITY_INSTANCING_BUFFER_START(EmberfieldShadow)
                UNITY_DEFINE_INSTANCED_PROP(float, _Sway)
            UNITY_INSTANCING_BUFFER_END(EmberfieldShadow)
            // The shadow has to lean with the crown that casts it, or the trick shows on the ground.
            float3 SwayShadow(float3 world, float heightOS, float amount)
            {
                if (amount <= 0 || heightOS <= 0) return world;
                float phase = world.x * .31 + world.z * .24 + _Time.y * 1.15;
                float bend = (sin(phase) + sin(phase * 1.73 + 1.3) * .42) * amount * .022 * heightOS;
                return float3(world.x + bend, world.y, world.z + bend * .55);
            }
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings ShadowVert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 world = SwayShadow(TransformObjectToWorld(input.positionOS.xyz), input.positionOS.y,
                    UNITY_ACCESS_INSTANCED_PROP(EmberfieldShadow, _Sway));
                float3 normal = TransformObjectToWorldNormal(input.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 direction = normalize(_LightPosition - world);
                #else
                    float3 direction = _LightDirection;
                #endif
                Varyings output;
                output.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(world, normal, direction)));
                return output;
            }
            half4 ShadowFrag(Varyings input) : SV_Target { return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings DepthVert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output; output.positionCS = TransformObjectToHClip(input.positionOS.xyz); return output;
            }
            half4 DepthFrag(Varyings input) : SV_Target { return input.positionCS.z; }
            ENDHLSL
        }
    }
    Fallback Off
}
