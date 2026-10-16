#include "Assets/Scripts/Terrain/NoiseSimplex.hlsl"
#include "Assets/Scripts/Terrain/NoiseTerrainGenerator.hlsl"
#include "Assets/Scripts/Terrain/ReorientNormalTBN.hlsl"

struct TextureProperties{
    float4 color;
    float3 normal;
};

float4 SampleTriplanar(TEXTURE2D_ARRAY_PARAM(texArray, sampler_texArray), float3 worldPos, float3 worldNormal, float blend, float tiling, float index)
{
    float3 triW = abs(worldNormal);
    triW /= triW.x + triW.y + triW.z;
    float4 colX = SAMPLE_TEXTURE2D_ARRAY(texArray, sampler_texArray, worldPos.zy * tiling, index);
    float4 colY = SAMPLE_TEXTURE2D_ARRAY(texArray, sampler_texArray, worldPos.xz * tiling, index);
    float4 colZ = SAMPLE_TEXTURE2D_ARRAY(texArray, sampler_texArray, worldPos.xy * tiling, index);

    return colX * triW.x + colY * triW.y + colZ * triW.z;
}

TextureProperties GetTextureProperties(float3 pos, float3 normal){
    TextureProperties output;

    float3 sphereNormal = normalize(pos - _PlanetCenter);

    float3 up = abs(sphereNormal.y) < 0.999 ? float3(0.0, 1.0, 0.0) : float3(1.0, 0.0, 0.0);
    float3 tangent = normalize(cross(up, sphereNormal));

    float3x3 sphereTBN = BuildTBN(sphereNormal, tangent);
    float3x3 planarTBN = BuildTBN(float3(0,1,0), float3(1,0,0));
    float3 planarNormal = ReorientNormalTBN(normal, sphereTBN, planarTBN);

    float4 flatMacroDif = SampleTriplanar(_MacroDiffuseArray, sampler_MacroDiffuseArray, pos, normal, 1.0, 1.0, 0);
    float4 flatMacroNorm = SampleTriplanar(_MacroNormalArray, sampler_MacroNormalArray, pos, normal, 1.0, 1.0, 0);
    float4 slopeMacroDif = SampleTriplanar(_MacroDiffuseArray, sampler_MacroDiffuseArray, pos, normal, 1.0, 1.0, 1);
    float4 slopeMacroNorm = SampleTriplanar(_MacroNormalArray, sampler_MacroNormalArray, pos, normal, 1.0, 1.0, 1);

    float t = pow(saturate((planarNormal.y - (_SlopeThreshold - 1/(2 * _SlopeFade))) * _SlopeFade), _SlopePow);

    float4 outColor = lerp(slopeMacroDif, flatMacroDif, t);
    // outColor = float4(planarNormal, 1.0);
    float4 blendNormalTex = lerp(slopeMacroNorm, flatMacroNorm, t);
    
    output.color = outColor;
    output.normal = normal;
    return output;
}
