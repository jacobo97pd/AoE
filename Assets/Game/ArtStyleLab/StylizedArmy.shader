Shader "Emberfield/Stylized Army PBR"
{
    Properties
    {
        _BaseMap("Shared broad surface atlas", 2D) = "white" {}
        _NormalMap("Optional authored normal", 2D) = "bump" {}
        _NormalStrength("Normal strength", Range(0,1)) = 0
        _TeamMask("Team mask multiplier", 2D) = "white" {}
        _TeamColor("Player color", Color) = (.08,.29,.78,1)
        _FactionTint("Cosmetic surface tint", Color) = (1,1,1,1)
        _Metallic("Metallic multiplier", Range(0,1)) = 1
        _Smoothness("Smoothness multiplier", Range(0,1)) = 1
        _RimStrength("Soft readability rim", Range(0,.3)) = .055
        _Selection("Selection highlight", Range(0,1)) = 0
        _DamageFlash("Damage feedback", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST, _TeamColor, _FactionTint;
            float _NormalStrength, _Metallic, _Smoothness, _RimStrength, _Selection, _DamageFlash;
        CBUFFER_END
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
        TEXTURE2D(_TeamMask); SAMPLER(sampler_TeamMask);
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct Attributes
            {
                float4 positionOS:POSITION; float3 normalOS:NORMAL; float4 tangentOS:TANGENT;
                half4 color:COLOR; float2 uv:TEXCOORD0; float2 surface:TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0;
                half3 normalWS:TEXCOORD1; half4 tangentWS:TEXCOORD2;
                half4 color:COLOR; float2 uv:TEXCOORD3; half2 surface:TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                Varyings o; UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz);
                VertexNormalInputs n=GetVertexNormalInputs(v.normalOS,v.tangentOS);
                o.positionCS=p.positionCS; o.positionWS=p.positionWS;
                o.normalWS=n.normalWS; o.tangentWS=half4(n.tangentWS,v.tangentOS.w*GetOddNegativeScale());
                o.color=v.color; o.uv=TRANSFORM_TEX(v.uv,_BaseMap); o.surface=v.surface;
                return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                half3 n=normalize(i.normalWS), view=GetWorldSpaceNormalizeViewDir(i.positionWS);
                if(_NormalStrength>.001)
                {
                    half3 normalTS=UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap,sampler_NormalMap,i.uv),_NormalStrength);
                    half3 t=normalize(i.tangentWS.xyz), b=cross(n,t)*i.tangentWS.w;
                    n=normalize(TransformTangentToWorld(normalTS,half3x3(t,b,n)));
                }
                half mask=saturate(i.color.a)*SAMPLE_TEXTURE2D(_TeamMask,sampler_TeamMask,i.uv).r;
                half shade=lerp(.76,1.08,saturate(dot(i.color.rgb,half3(.2126,.7152,.0722))));
                half3 base=i.color.rgb*_FactionTint.rgb;
                base=lerp(base,_TeamColor.rgb*shade,mask)*SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb;
                SurfaceData s=(SurfaceData)0;
                s.albedo=base; s.metallic=saturate(i.surface.x*_Metallic);
                s.smoothness=clamp(i.surface.y*_Smoothness,.08,.86); s.occlusion=1; s.alpha=1; s.normalTS=half3(0,0,1);
                InputData d=(InputData)0;
                d.positionWS=i.positionWS; d.normalWS=n; d.viewDirectionWS=view;
                d.shadowCoord=TransformWorldToShadowCoord(i.positionWS);
                d.bakedGI=max(SampleSH(n),lerp(half3(.12,.14,.16),half3(.30,.35,.42),n.y*.5+.5));
                d.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS); d.shadowMask=1;
                half4 result=UniversalFragmentPBR(d,s);
                // Broad cool environment response keeps metal readable before probe baking.
                result.rgb+=base*s.metallic*d.bakedGI*.40;
                half rim=pow(1-saturate(dot(n,view)),3);
                result.rgb+=rim*(_RimStrength+_Selection*.22)*lerp(half3(.50,.67,.83),half3(.75,.85,1),_Selection);
                result.rgb=lerp(result.rgb,half3(1,.56,.25),_DamageFlash*.65);
                return result;
            }
            ENDHLSL
        }
        UsePass "Emberfield/Faceted Team/ShadowCaster"
        UsePass "Emberfield/Faceted Team/DepthOnly"
    }
    Fallback Off
}
