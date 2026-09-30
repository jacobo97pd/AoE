Shader "Emberfield/Overhaul Water"
{
    Properties
    {
        _Shallow("Clear shallows",Color)=(.13,.64,.58,1)
        _Deep("Deep water",Color)=(.025,.22,.31,1)
        _Foam("Sunlit foam",Color)=(.78,.94,.86,1)
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+10"}
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
        half4 _Shallow,_Deep,_Foam;
        CBUFFER_END
        ENDHLSL
        Pass
        {
            Tags {"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct A{float4 p:POSITION;float3 n:NORMAL;float2 uv:TEXCOORD0;half4 color:COLOR;};
            struct V{float4 p:SV_POSITION;float3 world:TEXCOORD0;float3 n:TEXCOORD1;float2 uv:TEXCOORD2;half4 color:COLOR;half fog:TEXCOORD3;};
            V Vert(A i){V o;VertexPositionInputs p=GetVertexPositionInputs(i.p.xyz);o.p=p.positionCS;o.world=p.positionWS;o.n=TransformObjectToWorldNormal(i.n);o.uv=i.uv;o.color=i.color;o.fog=ComputeFogFactor(p.positionCS.z);return o;}
            half4 Frag(V i):SV_Target
            {
                float2 p=i.world.xz;float t=_Time.y;
                float wave=sin(p.x*1.8+p.y*.65+t*.9)*.45+sin(p.y*2.8-p.x*.7-t*.7)*.24+sin(p.x*5.1+p.y*3.9+t*.5)*.10;
                half3 normal=normalize(i.n+half3(cos(p.x*1.8+p.y*.65+t*.9)*.13,0,sin(p.y*2.8-p.x*.7-t*.7)*.09));
                half3 view=GetWorldSpaceNormalizeViewDir(i.world);Light sun=GetMainLight(TransformWorldToShadowCoord(i.world));
                half fresnel=pow(1-saturate(dot(normal,view)),3);
                half depth=saturate(i.color.b*.5+.12+sin(p.x*.23+p.y*.17)*.06);
                half3 color=lerp(_Shallow.rgb,_Deep.rgb,depth);
                color+=sin(p.x*3.1+p.y*2.3+wave*3+t)*sin(p.x*2.2-p.y*3.5-wave*2-t*.6)*.022;
                color=lerp(color,half3(.35,.56,.64),fresnel*.38);
                half spec=pow(saturate(dot(normal,normalize(sun.direction+view))),96);
                color+=sun.color*spec*.58;
                half ripple=smoothstep(.68,.82,wave)*.22;
                // Alpha is the authored shoreline/waterfall foam mask; opaque water avoids overdraw layers.
                half foam=saturate(i.color.a)*( .58+.32*sin(p.x*5+p.y*4-t*1.6));
                color=lerp(color,_Foam.rgb,saturate(foam+ripple));
                color*=lerp(.72,1,sun.shadowAttenuation);
                return half4(MixFog(color,i.fog),1);
            }
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
            float4 DepthVert(float4 positionOS:POSITION):SV_POSITION{return TransformObjectToHClip(positionOS.xyz);}
            half4 DepthFrag(float4 positionCS:SV_POSITION):SV_Target{return positionCS.z;}
            ENDHLSL
        }
    }
}
