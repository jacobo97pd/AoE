Shader "Emberfield/Meshy Ground"
{
    // Painted ground layers for the Meshy look. Four layers of one biome live in a Texture2DArray (albedo, height in
    // alpha) tiled in world space. AmberBiome writes how much of layers 1-3 each vertex wants (UV1); layer 0 takes
    // the rest. Layers meet by height, so a path's pebbles poke through grass instead of fading in like fog.
    // Brightness still follows the procedural terrain colour: its luminance, relative to the layers' own, scales the
    // painted albedo, so the lighting grade tuned for the flat ground holds and its large light and shade patches stay.
    // With _LANDS (a map whose starting lands wear different cultures) up to three biomes' layer arrays are bound, each
    // vertex carries its share of every biome (UV2 xyz) and how close it lies to lava (UV2 w), and a pixel samples only
    // the biomes it has a share of, so the ground away from a border costs one biome's four reads as before.
    Properties
    {
        _Layers("Layers", 2DArray) = "" {}
        _Tiling("Tiling per metre", Float) = .2
        _HeightBlend("Height blend", Range(.02, .5)) = .15
        _TintStrength("Follow terrain brightness", Range(0, 1)) = .6
        _Exposure("Exposure", Float) = 1
        _Saturation("Saturation", Range(0, 1.5)) = .72
        _MeanLuminance("Layer mean luminance", Vector) = (.2, .2, .2, .2)
        _Layers1("Second biome layers", 2DArray) = "" {}
        _Layers2("Third biome layers", 2DArray) = "" {}
        _MeanLuminance1("Second biome mean luminance", Vector) = (.2, .2, .2, .2)
        _MeanLuminance2("Third biome mean luminance", Vector) = (.2, .2, .2, .2)
        _LandExposure("Exposure per biome", Vector) = (1, 1, 1, 0)
        _LandSaturation("Saturation per biome", Vector) = (.72, .72, .72, 0)
        _LandPath("Path layer share per biome", Vector) = (1, 1, 1, 0)
        _LandColor0("Colour of the first biome", Color) = (1, 1, 1, 1)
        _LandColor1("Colour of the second biome", Color) = (1, 1, 1, 1)
        _LandColor2("Colour of the third biome", Color) = (1, 1, 1, 1)
        _LavaGlow("Glow cast by lava", Color) = (1, .36, .1, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_local _ _LANDS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Tiling, _HeightBlend, _TintStrength, _Exposure, _Saturation;
                float4 _MeanLuminance, _MeanLuminance1, _MeanLuminance2, _LandExposure, _LandSaturation, _LandPath;
                float4 _LandColor0, _LandColor1, _LandColor2, _LavaGlow;
            CBUFFER_END
            TEXTURE2D_ARRAY(_Layers); SAMPLER(sampler_Layers);
            #if defined(_LANDS)
            TEXTURE2D_ARRAY(_Layers1); TEXTURE2D_ARRAY(_Layers2);
            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float4 weights : TEXCOORD1; float4 lands : TEXCOORD2; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 world : TEXCOORD0; half4 color : COLOR; half3 weights : TEXCOORD1; half fogFactor : TEXCOORD2; half4 lands : TEXCOORD3; };
            #else
            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float4 weights : TEXCOORD1; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 world : TEXCOORD0; half4 color : COLOR; half3 weights : TEXCOORD1; half fogFactor : TEXCOORD2; };
            #endif
            Varyings Vert(Attributes input)
            {
                Varyings output; output.world = TransformObjectToWorld(input.positionOS.xyz); output.positionCS = TransformWorldToHClip(output.world);
                output.color = input.color; output.weights = input.weights.xyz; output.fogFactor = ComputeFogFactor(output.positionCS.z);
                #if defined(_LANDS)
                output.lands = input.lands;
                #endif
                return output;
            }
            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Noise(float2 p) { float2 i = floor(p), f = frac(p); f = f * f * (3 - 2 * f); return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + 1), f.x), f.y); }
            #if defined(_LANDS)
            // The same four reads as below, with explicit gradients so that a biome can be skipped inside a branch. Returns
            // the blended albedo and, in alpha, the blend's mean luminance; top is the tallest wanted layer, which the
            // biomes meet by.
            half4 Paint(TEXTURE2D_ARRAY_PARAM(layers, layerSampler), float2 uv, float2 dx, float2 dy, half3 w, float4 mean, out half top)
            {
                float2x2 r1 = float2x2(.96, -.28, .28, .96) * 1.11, r2 = float2x2(.87, .5, -.5, .87) * .93, r3 = float2x2(.71, -.71, .71, .71) * 1.23;
                half4 l0 = SAMPLE_TEXTURE2D_ARRAY_GRAD(layers, layerSampler, uv, 0, dx, dy);
                half4 l1 = SAMPLE_TEXTURE2D_ARRAY_GRAD(layers, layerSampler, mul(r1, uv), 1, mul(r1, dx), mul(r1, dy));
                half4 l2 = SAMPLE_TEXTURE2D_ARRAY_GRAD(layers, layerSampler, mul(r2, uv), 2, mul(r2, dx), mul(r2, dy));
                half4 l3 = SAMPLE_TEXTURE2D_ARRAY_GRAD(layers, layerSampler, mul(r3, uv), 3, mul(r3, dx), mul(r3, dy));
                half4 want = half4(saturate(1 - (w.x + w.y + w.z)), w.x, w.y, w.z);
                half4 height = want * (half4(l0.a, l1.a, l2.a, l3.a) + .35);
                top = max(max(height.x, height.y), max(height.z, height.w));
                half4 blend = max(height - (top - _HeightBlend), 0) * step(.001, want);
                blend /= max(dot(blend, 1), 1e-4);
                return half4(l0.rgb * blend.x + l1.rgb * blend.y + l2.rgb * blend.z + l3.rgb * blend.w, dot(blend, mean));
            }
            half3 Finish(half4 painted, half terrainLuminance, half exposure, half saturation, half3 colour)
            {
                half follow = clamp(terrainLuminance / max(painted.a, .02), .55, 3.5);
                half3 albedo = painted.rgb * lerp(exposure, follow, _TintStrength) * .88;
                albedo = lerp(dot(albedo, half3(.2126, .7152, .0722)).xxx, albedo, saturation);
                return albedo * colour;
            }
            #endif
            half4 Frag(Varyings input) : SV_Target
            {
                #if defined(_LANDS)
                float2 uv = input.world.xz * _Tiling, dx = ddx(uv), dy = ddy(uv);
                half3 w = saturate(input.weights);
                half terrainLuminance = dot(input.color.rgb, half3(.2126, .7152, .0722));
                half3 share = saturate(input.lands.xyz);
                // A border wanders with the ground's relief instead of following the smooth vertex ramp: noise nudges the
                // shares where two biomes overlap, then they meet by height like the layers inside one biome.
                half wander = (Noise(input.world.xz * .37 + 3.3) - .5) * 2;
                share = saturate(share + share * (1 - share) * half3(wander, -wander, wander * .6) * 1.4);
                half3 albedoA = 0, albedoB = 0, albedoC = 0, tops = 0;
                half top = 0;
                // A biome can keep its path layer (layer 2) quieter: the volcanic cinders under a settlement hid its orcs.
                [branch] if (share.x > .002) { albedoA = Finish(Paint(TEXTURE2D_ARRAY_ARGS(_Layers, sampler_Layers), uv, dx, dy, w * half3(1, _LandPath.x, 1), _MeanLuminance, top), terrainLuminance, _LandExposure.x, _LandSaturation.x, _LandColor0.rgb); tops.x = top; }
                [branch] if (share.y > .002) { albedoB = Finish(Paint(TEXTURE2D_ARRAY_ARGS(_Layers1, sampler_Layers), uv, dx, dy, w * half3(1, _LandPath.y, 1), _MeanLuminance1, top), terrainLuminance, _LandExposure.y, _LandSaturation.y, _LandColor1.rgb); tops.y = top; }
                [branch] if (share.z > .002) { albedoC = Finish(Paint(TEXTURE2D_ARRAY_ARGS(_Layers2, sampler_Layers), uv, dx, dy, w * half3(1, _LandPath.z, 1), _MeanLuminance2, top), terrainLuminance, _LandExposure.z, _LandSaturation.z, _LandColor2.rgb); tops.z = top; }
                half3 lift = share * (tops + .35);
                half highest = max(lift.x, max(lift.y, lift.z));
                half3 mix = max(lift - (highest - _HeightBlend * 1.5), 0) * step(.002, share);
                mix /= max(dot(mix, 1), 1e-4);
                half3 albedo = albedoA * mix.x + albedoB * mix.y + albedoC * mix.z;
                albedo *= .95 + Noise(input.world.xz * .58 + 8.1) * .08;
                albedo *= lerp(1, .72, input.color.a);
                Light landSun = GetMainLight(TransformWorldToShadowCoord(input.world));
                half3 landAmbient = max(SampleSH(half3(0, 1, 0)), half3(.23, .28, .29));
                half landDiffuse = saturate(landSun.direction.y) * landSun.shadowAttenuation;
                half3 lit = albedo * (landAmbient + landSun.color * landDiffuse * .8);
                // Ground beside lava takes its light: strongest at the bank, breathing slowly with the flow.
                half heat = input.lands.w * input.lands.w * (.85 + .15 * sin(_Time.y * 1.7 + input.world.x * .9 + input.world.z * .6));
                lit += _LavaGlow.rgb * heat * .22;
                return half4(MixFog(lit, input.fogFactor), 1);
                #else
                float2 uv = input.world.xz * _Tiling;
                // Each layer is read at a slightly different angle and scale, so their repeats never line up.
                half4 l0 = SAMPLE_TEXTURE2D_ARRAY(_Layers, sampler_Layers, uv, 0);
                half4 l1 = SAMPLE_TEXTURE2D_ARRAY(_Layers, sampler_Layers, float2(uv.x * .96 - uv.y * .28, uv.x * .28 + uv.y * .96) * 1.11, 1);
                half4 l2 = SAMPLE_TEXTURE2D_ARRAY(_Layers, sampler_Layers, float2(uv.x * .87 + uv.y * .5, -uv.x * .5 + uv.y * .87) * .93, 2);
                half4 l3 = SAMPLE_TEXTURE2D_ARRAY(_Layers, sampler_Layers, float2(uv.x * .71 - uv.y * .71, uv.x * .71 + uv.y * .71) * 1.23, 3);
                half3 w = saturate(input.weights);
                half4 want = half4(saturate(1 - (w.x + w.y + w.z)), w.x, w.y, w.z);
                half4 height = want * (half4(l0.a, l1.a, l2.a, l3.a) + .35);
                half top = max(max(height.x, height.y), max(height.z, height.w));
                half4 blend = max(height - (top - _HeightBlend), 0) * step(.001, want);
                blend /= max(dot(blend, 1), 1e-4);
                half3 albedo = l0.rgb * blend.x + l1.rgb * blend.y + l2.rgb * blend.z + l3.rgb * blend.w;
                half layerLuminance = dot(blend, _MeanLuminance);
                half terrainLuminance = dot(input.color.rgb, half3(.2126, .7152, .0722));
                half follow = clamp(terrainLuminance / max(layerLuminance, .02), .55, 3.5);
                albedo *= lerp(_Exposure, follow, _TintStrength) * .88;
                // Painted layers are far more saturated than the flat ground was; pulled back, they sit under the units.
                albedo = lerp(dot(albedo, half3(.2126, .7152, .0722)).xxx, albedo, _Saturation);
                float patches = Noise(input.world.xz * .58 + 8.1);
                albedo *= .95 + patches * .08;
                albedo *= lerp(1, .72, input.color.a);
                Light sun = GetMainLight(TransformWorldToShadowCoord(input.world));
                half3 ambient = max(SampleSH(half3(0, 1, 0)), half3(.23, .28, .29));
                half diffuse = saturate(sun.direction.y) * sun.shadowAttenuation;
                return half4(MixFog(albedo * (ambient + sun.color * diffuse * .8), input.fogFactor), 1);
                #endif
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
