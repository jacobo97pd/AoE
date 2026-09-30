Shader "Emberfield/Meshy Prop"
{
    // Textured scenery from Meshy: trees, rocks, resource nodes, landmarks. Drawn by the thousand, so the material
    // values sit outside a UnityPerMaterial buffer on purpose: URP then GPU-instances every copy of a (mesh, material)
    // pair instead of SRP-batching one draw per object. The felling sway is an instanced property, so the one tree
    // somebody is cutting keeps its instancing while it bends; its shadow bends with it.
    Properties
    {
        _BaseMap("Albedo", 2D) = "white" {}
        _BumpMap("Normal", 2D) = "bump" {}
        _BumpScale("Normal strength", Range(0, 2)) = .7
        _MaskMap("Metallic (R), smoothness (A)", 2D) = "black" {}
        _Metallic("Metallic multiplier", Range(0, 1)) = 1
        _Smoothness("Smoothness multiplier", Range(0, 1)) = .6
        _Jitter("Tone variation between copies", Range(0, .15)) = .06
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        float4 _BaseMap_ST;
        float _BumpScale, _Metallic, _Smoothness, _Jitter;
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
        TEXTURE2D(_MaskMap); SAMPLER(sampler_MaskMap);
        UNITY_INSTANCING_BUFFER_START(EmberfieldProp)
            UNITY_DEFINE_INSTANCED_PROP(float, _Sway)
        UNITY_INSTANCING_BUFFER_END(EmberfieldProp)

        // The same bend as the procedural scenery (FacetedTeam): only above the root, two waves that never line up.
        float3 Sway(float3 world, float heightOS, float amount)
        {
            if (amount <= 0 || heightOS <= 0) return world;
            float phase = world.x * .31 + world.z * .24 + _Time.y * 1.15;
            float bend = (sin(phase) + sin(phase * 1.73 + 1.3) * .42) * amount * .022 * heightOS;
            return float3(world.x + bend, world.y, world.z + bend * .55);
        }
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
                half4 tangentWS : TEXCOORD2; float2 uv : TEXCOORD3; half fog : TEXCOORD4; half tone : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            Varyings Vert(Attributes v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                Varyings o; UNITY_TRANSFER_INSTANCE_ID(v, o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                VertexNormalInputs n = GetVertexNormalInputs(v.normalOS, v.tangentOS);
                o.positionWS = Sway(p.positionWS, v.positionOS.y, UNITY_ACCESS_INSTANCED_PROP(EmberfieldProp, _Sway));
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = n.normalWS; o.tangentWS = half4(n.tangentWS, v.tangentOS.w * GetOddNegativeScale());
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap); o.fog = ComputeFogFactor(o.positionCS.z);
                // Each copy's own spot on the map picks its shade, so a stand of one model never reads as stamped.
                float3 origin = GetObjectToWorldMatrix()._m03_m13_m23;
                o.tone = frac(sin(dot(origin.xz, float2(12.9898, 78.233))) * 43758.5453) * 2 - 1;
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half4 base = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                half4 mask = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, i.uv);
                half3 n = normalize(i.normalWS);
                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv), _BumpScale);
                half3 t = normalize(i.tangentWS.xyz), b = cross(n, t) * i.tangentWS.w;
                n = normalize(TransformTangentToWorld(normalTS, half3x3(t, b, n)));
                half3 albedo = base.rgb * (1 + i.tone * _Jitter);
                SurfaceData s = (SurfaceData)0;
                s.albedo = albedo; s.metallic = saturate(mask.r * _Metallic);
                s.smoothness = clamp(mask.a * _Smoothness, .04, .8); s.occlusion = 1; s.alpha = 1; s.normalTS = half3(0, 0, 1);
                InputData d = (InputData)0;
                d.positionWS = i.positionWS; d.normalWS = n; d.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                d.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                d.bakedGI = SampleSH(n); d.fogCoord = i.fog;
                d.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS); d.shadowMask = 1;
                half4 colour = UniversalFragmentPBR(d, s);
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
                float3 world = Sway(TransformObjectToWorld(input.positionOS.xyz), input.positionOS.y, UNITY_ACCESS_INSTANCED_PROP(EmberfieldProp, _Sway));
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
                float3 world = Sway(TransformObjectToWorld(input.positionOS.xyz), input.positionOS.y, UNITY_ACCESS_INSTANCED_PROP(EmberfieldProp, _Sway));
                Varyings o; o.positionCS = TransformWorldToHClip(world); return o;
            }
            half DepthFrag(Varyings i) : SV_Target { return i.positionCS.z; }
            ENDHLSL
        }
    }
    Fallback Off
}
