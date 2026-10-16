#include "Assets/Scripts/Terrain/NoiseSimplex.hlsl"
#include "Assets/Scripts/Terrain/NoiseTerrainGenerator.hlsl"

float GetPlanetHeight(float3 pos){
    return LayeredNoise(pos, _Octaves, _Frequency, _Amplitude);
}