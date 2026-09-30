Shader "Emberfield/Alpha Shallow Water"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+12" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; float2 uv:TEXCOORD1; half depth:COLOR; half fogFactor:TEXCOORD2; };
            Varyings Vert(Attributes i) { Varyings o; o.world=TransformObjectToWorld(i.positionOS.xyz); o.positionCS=TransformWorldToHClip(o.world); o.uv=i.uv; o.depth=i.color.r; o.fogFactor=ComputeFogFactor(o.positionCS.z); return o; }
            half4 Frag(Varyings i):SV_Target
            {
                // The bank is where the sheet ends, not a stripe ruled across the world: shallows, wet sand
                // and a line of foam all key off how far this water is from dry land.
                half edge = saturate(1 - i.depth*1.55);
                float ripple = sin(i.world.z*3.7 + sin(i.world.x*4.1) - _Time.y*1.45)*.5+.5;
                float fine = sin(i.world.z*11.1-i.world.x*7.3-_Time.y*2.1)*.5+.5;
                half3 shallow = half3(.26,.48,.43), deep = half3(.075,.30,.34);
                half3 color = lerp(shallow,deep,i.depth*.65) * (.91+ripple*.12);
                color = lerp(color,half3(.68,.73,.55),smoothstep(.42,1,edge)*.58);
                half surf = smoothstep(.72,1,edge) * (.35 + ripple*.5);
                color = lerp(color,half3(.88,.93,.90),surf*.55);
                color += half3(.53,.76,.70)*pow(ripple*fine,15)*.28;
                Light sun=GetMainLight(TransformWorldToShadowCoord(i.world));
                color *= .68 + sun.shadowAttenuation*.32;
                return half4(MixFog(color, i.fogFactor),1);
            }
            ENDHLSL
        }
    }
}
