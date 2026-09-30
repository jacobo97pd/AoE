Shader "Emberfield/Meshy Lava"
{
    // Lava for the water cells of a volcanic land (tools/art/make_lava_maps.py). The molten colour scrolls at two scales;
    // a crust of cooled plates drifts over it at half the speed and closes in toward the banks, where the flow is slow.
    // The molten part is emissive above the bloom threshold, so the global bloom makes it glow; the crust takes the
    // sun like ground. Vertex red is the same shore depth as the water sheet (0 at the bank, 1 in open lava) and vertex
    // green marks a ford, which crusts over so that the crossing reads as solid ground with glowing seams.
    Properties
    {
        _LavaMap("Molten colour (alpha: heat)", 2D) = "black" {}
        _CrustMap("Crust (R: distance to a crack, G: plate value, B: grain)", 2D) = "white" {}
        _CrustColor("Crust", Color) = (.18, .15, .14, 1)
        _Bank("Bank", Color) = (.36, .23, .19, 1)
        _Rim("Rim glow", Color) = (1, .42, .12, 1)
        _Heat("Molten emission", Float) = 2.6
        _RimHeat("Rim emission", Float) = 1.8
        _Flow("Flow (xy: first scale, zw: second)", Vector) = (.018, .011, -.009, .014)
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
                float4 _CrustColor, _Bank, _Rim, _Flow;
                float _Heat, _RimHeat;
            CBUFFER_END
            TEXTURE2D(_LavaMap); SAMPLER(sampler_LavaMap);
            TEXTURE2D(_CrustMap); SAMPLER(sampler_CrustMap);
            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 world : TEXCOORD0; half2 shore : TEXCOORD1; half fog : TEXCOORD2; };
            Varyings Vert(Attributes i)
            {
                Varyings o; o.world = TransformObjectToWorld(i.positionOS.xyz); o.positionCS = TransformWorldToHClip(o.world);
                o.shore = i.color.rg; o.fog = ComputeFogFactor(o.positionCS.z); return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float t = _Time.y;
                float2 p = i.world.xz;
                // Molten colour at two scales and headings, so the flow never marches in step.
                half4 near = SAMPLE_TEXTURE2D(_LavaMap, sampler_LavaMap, p / 7.0 + t * _Flow.xy);
                half4 far = SAMPLE_TEXTURE2D(_LavaMap, sampler_LavaMap, p.yx / 17.0 + t * _Flow.zw + .37);
                half4 lava = lerp(near, far, .42);
                // The crust drifts at half the speed of the melt beneath it.
                half3 crustMap = SAMPLE_TEXTURE2D(_CrustMap, sampler_CrustMap, p / 5.5 + t * _Flow.xy * .5).rgb;
                half depth = saturate(i.shore.r), ford = saturate(i.shore.g);
                // Plates close in toward the bank; open lava keeps wide molten seams. A ford is crust all over but its cracks.
                half threshold = lerp(.14, .58, smoothstep(0, .8, depth));
                threshold = lerp(threshold, .015, ford);
                half crust = smoothstep(threshold, threshold + .08, crustMap.r);
                half3 crustColour = _CrustColor.rgb * (.75 + .5 * crustMap.g) * (.8 + .4 * crustMap.b);
                half bank = 1 - smoothstep(0, .45, depth);
                crustColour = lerp(crustColour, _Bank.rgb * (.8 + .4 * crustMap.b), bank * (1 - ford) * .7);
                half3 albedo = lerp(lava.rgb * .35, crustColour, crust);
                Light sun = GetMainLight(TransformWorldToShadowCoord(i.world));
                half3 ambient = max(SampleSH(half3(0, 1, 0)), half3(.23, .28, .29));
                half3 lit = albedo * (ambient + sun.color * saturate(sun.direction.y) * sun.shadowAttenuation * .8);
                // Molten lava glows by its own heat; a plate's edge glows where it meets the melt.
                half molten = 1 - crust;
                half rim = smoothstep(threshold - .09, threshold + .02, crustMap.r) * molten;
                half pulse = .88 + .12 * sin(t * 1.3 + p.x * .7 - p.y * .45);
                half3 emission = lava.rgb * lava.a * molten * _Heat * pulse + _Rim.rgb * rim * _RimHeat;
                return half4(MixFog(lit + emission, i.fog), 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
