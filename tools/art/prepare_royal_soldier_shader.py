"""Derive the soldier's vertex-colour aware surface from this project's pinned URP.

The forward renderer retains Unity's PBR, shadow and normal-map implementation.
Only the colour varying and its material-controlled multiplication are added.
Copyright and Unity Companion License notice travel with the derived sources.
"""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PACKAGE = next((ROOT / 'Library/PackageCache').glob('com.unity.render-pipelines.universal@*'))
OUT = ROOT / 'Assets/Game/RoyalSoldier/Shaders'
OUT.mkdir(parents=True, exist_ok=True)

def replace_one(text, before, after):
    assert text.count(before) == 1, before
    return text.replace(before, after, 1)

shader = (PACKAGE/'Shaders/Lit.shader').read_text(encoding='utf-8')
shader = replace_one(shader, 'Shader "Universal Render Pipeline/Lit"', 'Shader "RoyalSoldier/Surface"')
shader = shader.replace('#pragma target 2.0', '#pragma target 3.5')
shader = replace_one(shader, 'Properties\n    {', 'Properties\n    {\n        _VertexColorStrength("Authored colour variation", Range(0,1)) = 0')
shader = shader.replace('Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl', 'RoyalSoldierInput.hlsl')
shader = shader.replace('Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl', 'RoyalSoldierForward.hlsl')
inputs = (PACKAGE/'Shaders/LitInput.hlsl').read_text(encoding='utf-8')
inputs = replace_one(inputs, 'CBUFFER_START(UnityPerMaterial)', 'CBUFFER_START(UnityPerMaterial)\nfloat _VertexColorStrength;')
forward = (PACKAGE/'Shaders/LitForwardPass.hlsl').read_text(encoding='utf-8')
forward = replace_one(forward, 'float3 normalOS     : NORMAL;', 'float3 normalOS     : NORMAL;\n    half4 authoredColor : COLOR;')
forward = replace_one(forward, 'float2 uv                       : TEXCOORD0;', 'float2 uv                       : TEXCOORD0;\n    half4 authoredColor             : TEXCOORD11;')
forward = replace_one(forward, 'return output;', 'output.authoredColor = input.authoredColor;\n    return output;')
forward = replace_one(forward, 'InitializeStandardLitSurfaceData(input.uv, surfaceData);', 'InitializeStandardLitSurfaceData(input.uv, surfaceData);\n    surfaceData.albedo *= lerp(half3(1,1,1), input.authoredColor.rgb, _VertexColorStrength);')
notice = '// Derived from Unity URP installed in this project. See UNITY-LICENSE.md.\n'
for filename, text in [('RoyalSoldierSurface.shader', shader), ('RoyalSoldierInput.hlsl', inputs), ('RoyalSoldierForward.hlsl', forward)]:
    (OUT/filename).write_text(notice+text, encoding='utf-8')
(OUT/'UNITY-LICENSE.md').write_text((PACKAGE/'LICENSE.md').read_text(encoding='utf-8'), encoding='utf-8')
print('Prepared soldier PBR forward shader with authored vertex colours:', OUT)
