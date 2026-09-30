Shader "Emberfield/Meshy Water"
{
    // The painted-realism water that sits with the Meshy art. Same sheet and same shore depth (vertex red: 0 at the
    // bank, 1 in open water) as Alpha Shallow Water, which stays as the fallback. No scene depth or opaque texture:
    // mobile renders without them, so everything the eye reads as depth comes from the authored shore distance.
    Properties
    {
        _NormalMap("Ripple normals", 2D) = "bump" {}
        _FoamMap("Foam lace", 2D) = "black" {}
        _Shallow("Shallow", Color) = (.37, .56, .52, 1)
        _Deep("Deep", Color) = (.12, .29, .31, 1)
        _Foam("Foam", Color) = (.90, .93, .90, 1)
        _WetBand("Wet bank", Color) = (.43, .41, .32, 1)
        _Horizon("Sky reflection", Color) = (.72, .80, .84, 1)
        _Ripple("Ripple strength", Range(0, 1)) = .55
    }
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
            CBUFFER_START(UnityPerMaterial)
                float4 _Shallow, _Deep, _Foam, _WetBand, _Horizon;
                float _Ripple;
            CBUFFER_END
            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
            TEXTURE2D(_FoamMap); SAMPLER(sampler_FoamMap);
            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 world : TEXCOORD0; half depth : TEXCOORD1; half fog : TEXCOORD2; };
            Varyings Vert(Attributes i)
            {
                Varyings o; o.world = TransformObjectToWorld(i.positionOS.xyz); o.positionCS = TransformWorldToHClip(o.world);
                o.depth = i.color.r; o.fog = ComputeFogFactor(o.positionCS.z); return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float t = _Time.y;
                // Two ripple layers at unrelated scales and headings, so the pattern never visibly repeats or marches.
                half3 a = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, i.world.xz / 6.0 + t * float2(.021, .013)));
                half3 b = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, i.world.zx / 11.0 + t * float2(-.012, .017)));
                half2 slope = (a.xy + b.xy) * _Ripple;
                half3 n = normalize(half3(slope.x, 1, slope.y));
                half depth = saturate(i.depth);
                half edge = saturate(1 - depth * 1.55);
                half3 colour = lerp(_Shallow.rgb, _Deep.rgb, smoothstep(0, 1, depth));
                // Sky in the ripples: stronger where the surface tilts away from the view.
                half3 view = GetWorldSpaceNormalizeViewDir(i.world);
                half fresnel = pow(1 - saturate(dot(n, view)), 3);
                colour = lerp(colour, _Horizon.rgb, fresnel * .45);
                Light sun = GetMainLight(TransformWorldToShadowCoord(i.world));
                half glint = pow(saturate(dot(reflect(-sun.direction, n), view)), 90);
                // Wet bank, then foam lace that rolls toward the shore in slow bands.
                colour = lerp(colour, _WetBand.rgb, smoothstep(.80, 1, edge) * .55);
                half lace = SAMPLE_TEXTURE2D(_FoamMap, sampler_FoamMap, i.world.xz / 3.2 + slope * .15 + t * float2(.01, .008)).r;
                half bands = sin(depth * 18 - t * 1.4) * .5 + .5;
                half foam = smoothstep(.45, 1, edge) * saturate(lace * 1.2 + bands * .35 - .25);
                colour = lerp(colour, _Foam.rgb, foam * .85);
                colour *= .68 + sun.shadowAttenuation * .32;
                colour += sun.color * glint * .55 * sun.shadowAttenuation * (1 - foam);
                return half4(MixFog(colour, i.fog), 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
