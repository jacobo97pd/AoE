Shader "Emberfield/Overhaul Painted Surface"
{
    Properties
    {
        _SurfaceMap("Shared sixteen-surface atlas",2D)="white"{}
        _TeamColor("Owner color",Color)=(.20,.38,.64,1)
        _TextureStrength("Painted surface strength",Range(0,1))=.66
        _Metallic("Metal response",Range(0,1))=.95
        _Smoothness("Smoothness scale",Range(.1,1))=.8
        _Selection("Selection edge",Range(0,1))=0
        _Wind("Foliage movement",Range(0,1))=0
        _Emission("Luminous stone",Range(0,4))=0
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
        float4 _SurfaceMap_ST,_TeamColor;
        float _TextureStrength,_Metallic,_Smoothness,_Selection,_Wind,_Emission;
        CBUFFER_END
        TEXTURE2D(_SurfaceMap);SAMPLER(sampler_SurfaceMap);
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags {"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct Attributes
            {
                float4 positionOS:POSITION;float3 normalOS:NORMAL;half4 color:COLOR;
                float2 uv:TEXCOORD0;float2 surface:TEXCOORD1;float2 atlas:TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1;
                half4 color:COLOR;float2 uv:TEXCOORD2;half2 surface:TEXCOORD3;float tile:TEXCOORD4;half fog:TEXCOORD5;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes v)
            {
                UNITY_SETUP_INSTANCE_ID(v);Varyings o;UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 p=v.positionOS.xyz;
                p.x+=sin(_Time.y*1.1+p.y*1.4+p.z*.7)*.035*_Wind*saturate(p.y*.4);
                VertexPositionInputs position=GetVertexPositionInputs(p);
                o.positionCS=position.positionCS;o.positionWS=position.positionWS;o.normalWS=TransformObjectToWorldNormal(v.normalOS);
                o.color=v.color;o.uv=v.uv;o.surface=v.surface;o.tile=v.atlas.x;o.fog=ComputeFogFactor(position.positionCS.z);
                return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                half3 n=normalize(i.normalWS),view=GetWorldSpaceNormalizeViewDir(i.positionWS);
                float tile=clamp(round(i.tile),0,15);
                float2 cell=float2(fmod(tile,4),3-floor(tile/4));
                float2 uv=i.uv;
                float2 coordinates=(cell+lerp(.018,.982,frac(uv)))*.25;
                half3 paint=SAMPLE_TEXTURE2D_GRAD(_SurfaceMap,sampler_SurfaceMap,coordinates,ddx(uv)*.241,ddy(uv)*.241).rgb;
                half value=dot(paint,half3(.2126,.7152,.0722));
                half detail=lerp(.56,1.26,smoothstep(.035,.7,value));
                // Surface-gradient relief from the shared painted atlas, in world units.
                // Small cloth/skin variation; stronger mortar, timber and scale definition.
                float relief=(tile==4||tile==5||tile==14)?.012:(tile==2||tile==11)?.007:tile==13?.0005:.002;
                float3 dpdx=ddx(i.positionWS),dpdy=ddy(i.positionWS);
                float3 tangentX=cross(dpdy,n),tangentY=cross(n,dpdx);
                float determinant=dot(dpdx,tangentX);
                if(abs(determinant)>.00000001)
                    n=normalize(abs(determinant)*n-sign(determinant)*relief*(ddx(value)*tangentX+ddy(value)*tangentY));
                half3 base=SRGBToLinear(saturate(i.color.rgb));
                base=lerp(base,_TeamColor.rgb*saturate(.78+dot(i.color.rgb,half3(.07,.12,.04))),saturate(i.color.a));
                base*=lerp(1,detail,_TextureStrength);
                SurfaceData s=(SurfaceData)0;s.albedo=base;s.metallic=saturate(i.surface.x*_Metallic);
                s.smoothness=clamp(i.surface.y*_Smoothness+(.04*(value-.35)),.12,.65);
                s.occlusion=lerp(.83,1,saturate(detail));s.alpha=1;s.normalTS=half3(0,0,1);
                InputData d=(InputData)0;d.positionWS=i.positionWS;d.normalWS=n;d.viewDirectionWS=view;
                d.shadowCoord=TransformWorldToShadowCoord(i.positionWS);d.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);d.shadowMask=1;
                d.bakedGI=max(SampleSH(n),lerp(half3(.08,.085,.07),half3(.23,.28,.33),saturate(n.y*.5+.5)));
                half4 color=UniversalFragmentPBR(d,s);
                // Broad reflected sky response complements the scene's shared environment cubemap.
                half reflectedSky=saturate(reflect(-view,n).y*.5+.5);
                color.rgb+=base*s.metallic*lerp(half3(.08,.06,.035),half3(.22,.29,.35),reflectedSky);
                color.rgb+=_Selection*.10*pow(1-saturate(dot(n,view)),3)*half3(.7,.84,1);
                color.rgb+=base*_Emission;
                color.rgb=MixFog(color.rgb,i.fog);return color;
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags {"LightMode"="ShadowCaster"}
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            float3 _LightDirection,_LightPosition;
            struct A{float4 positionOS:POSITION;float3 normalOS:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};
            float4 ShadowVert(A i):SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(i);float3 p=i.positionOS.xyz;
                p.x+=sin(_Time.y*1.1+p.y*1.4+p.z*.7)*.035*_Wind*saturate(p.y*.4);
                float3 world=TransformObjectToWorld(p),n=TransformObjectToWorldNormal(i.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 light=normalize(_LightPosition-world);
                #else
                float3 light=_LightDirection;
                #endif
                float4 clip=TransformWorldToHClip(ApplyShadowBias(world,n,light));
                #if UNITY_REVERSED_Z
                clip.z=min(clip.z,UNITY_NEAR_CLIP_VALUE);
                #else
                clip.z=max(clip.z,UNITY_NEAR_CLIP_VALUE);
                #endif
                return clip;
            }
            half4 ShadowFrag():SV_Target{return 0;}
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags {"LightMode"="DepthOnly"}
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            struct A{float4 positionOS:POSITION;UNITY_VERTEX_INPUT_INSTANCE_ID};
            float4 DepthVert(A i):SV_POSITION
            {UNITY_SETUP_INSTANCE_ID(i);float3 p=i.positionOS.xyz;p.x+=sin(_Time.y*1.1+p.y*1.4+p.z*.7)*.035*_Wind*saturate(p.y*.4);return TransformObjectToHClip(p);}
            half4 DepthFrag(float4 p:SV_POSITION):SV_Target{return p.z;}
            ENDHLSL
        }
    }
    Fallback Off
}
