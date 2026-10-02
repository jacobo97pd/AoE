Shader "Emberfield/Meshy Unit"
{
    // Textured characters from Meshy. The painted texture keeps its own colours; only the cloth the import marked in
    // the base map's alpha takes the owner's colour, re-lit by the painted shading so folds and trim survive the swap.
    Properties
    {
        _BaseMap("Albedo (RGB), team mask (A)", 2D) = "white" {}
        _BumpMap("Normal", 2D) = "bump" {}
        _BumpScale("Normal strength", Range(0, 2)) = .6
        _MaskMap("Metallic (R), smoothness (A)", 2D) = "black" {}
        _Metallic("Metallic multiplier", Range(0, 1)) = 1
        _Smoothness("Smoothness multiplier", Range(0, 1)) = .8
        _TeamColor("Owner colour", Color) = (.12, .56, .56, 1)
        _TeamStrength("Owner colour strength", Range(0, 1)) = 1
        _Grade("Colour grade", Color) = (1, 1, 1, 1)
        _RimStrength("Readability rim", Range(0, .4)) = .08
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST, _TeamColor, _Grade;
            float _BumpScale, _Metallic, _Smoothness, _TeamStrength, _RimStrength;
        CBUFFER_END
        // Set to 1 by the store's wardrobe preview around its own render only (CosmeticModelPreview), 0 on the
        // battlefield. A global in a buffer of its own, so every material stays SRP-batched.
        CBUFFER_START(EmberfieldStudio)
            float _EmberfieldStudio;
        CBUFFER_END
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
        TEXTURE2D(_MaskMap); SAMPLER(sampler_MaskMap);
        ENDHLSL

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct Attributes
            {
                float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half3 normalWS : TEXCOORD1;
                half4 tangentWS : TEXCOORD2; float2 uv : TEXCOORD3; half fog : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                Varyings o; UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                VertexNormalInputs n = GetVertexNormalInputs(v.normalOS, v.tangentOS);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS;
                o.normalWS = n.normalWS; o.tangentWS = half4(n.tangentWS, v.tangentOS.w * GetOddNegativeScale());
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap); o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                half4 base = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                half4 mask = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, i.uv);
                half3 n = normalize(i.normalWS);
                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv), _BumpScale);
                half3 t = normalize(i.tangentWS.xyz), b = cross(n, t) * i.tangentWS.w;
                n = normalize(TransformTangentToWorld(normalTS, half3x3(t, b, n)));

                half3 albedo = base.rgb * _Grade.rgb;
                // The painted cloth's own brightness carries its folds; the hue becomes the owner's.
                half luminance = dot(base.rgb, half3(.2126, .7152, .0722));
                half3 owner = _TeamColor.rgb * saturate(.35 + luminance * 1.9);
                albedo = lerp(albedo, owner, saturate(base.a * _TeamStrength));
                if (_EmberfieldStudio > .5)
                {
                    // The wardrobe preview: a fixed key and fill, no scene lights, shadows or fog, so a model reads the
                    // same whatever the match behind the menu is doing (the procedural art's studio, as in FacetedTeam).
                    half key = saturate(dot(n, normalize(half3(-.45, .80, -.56))));
                    half fill = saturate(dot(n, normalize(half3(.65, .30, .55))));
                    half studioRim = pow(1 - saturate(dot(n, GetWorldSpaceNormalizeViewDir(i.positionWS))), 3) * .07;
                    return half4(albedo * (half3(.78, .82, .90) + key * 1.0 + fill * .32) + studioRim, 1);
                }

                SurfaceData s = (SurfaceData)0;
                s.albedo = albedo; s.metallic = saturate(mask.r * _Metallic);
                s.smoothness = clamp(mask.a * _Smoothness, .04, .85); s.occlusion = 1; s.alpha = 1; s.normalTS = half3(0, 0, 1);
                InputData d = (InputData)0;
                half3 view = GetWorldSpaceNormalizeViewDir(i.positionWS);
                d.positionWS = i.positionWS; d.normalWS = n; d.viewDirectionWS = view;
                d.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                d.bakedGI = SampleSH(n); d.fogCoord = i.fog;
                d.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS); d.shadowMask = 1;
                half4 colour = UniversalFragmentPBR(d, s);
                // A cool rim separates dark armour and fur from dark ground at RTS distance.
                half rim = pow(1 - saturate(dot(n, view)), 3);
                colour.rgb += rim * _RimStrength * half3(.62, .72, .86);
                colour.rgb = MixFog(colour.rgb, i.fog);
                return colour;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection, _LightPosition;
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings ShadowVert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                float3 normal = TransformObjectToWorldNormal(input.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 direction = normalize(_LightPosition - world);
                #else
                    float3 direction = _LightDirection;
                #endif
                float4 clip = TransformWorldToHClip(ApplyShadowBias(world, normal, direction));
                #if UNITY_REVERSED_Z
                    clip.z = min(clip.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    clip.z = max(clip.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                Varyings o; o.positionCS = clip; return o;
            }
            half4 ShadowFrag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings DepthVert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings o; o.positionCS = TransformObjectToHClip(input.positionOS.xyz); return o;
            }
            half DepthFrag(Varyings i) : SV_Target { return i.positionCS.z; }
            ENDHLSL
        }
    }
    Fallback Off
}
