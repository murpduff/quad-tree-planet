using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

public class PlanetTerrainGeneration : MonoBehaviour {
    public int textureSize;
    public Cubemap planetaryTerrainTexture;
    public Material planetMat;
    public bool update;
    public ComputeShader planetTerrainCompute;
    public bool[] includeFace;

    [Header("Planetary Terrain Properties")]
    public int octaves;
    public float frequency;
    public float amplitude;

    void OnValidate() {
        if (update) {
            update = false;
            GenerateTexturesCompute();
            print("done");
        }
    }

    void GenerateTexturesCompute() {
        planetaryTerrainTexture = new Cubemap(textureSize, TextureFormat.RGBA32, false) {
            filterMode = FilterMode.Point
        };
        for(int i = 0; i < 6; i++){
            if(includeFace[i]) {
                int count = textureSize * textureSize;
                ComputeBuffer textureBuffer = new ComputeBuffer(count, sizeof(float) * 4);

                int kernel = planetTerrainCompute.FindKernel("CSMain");
                planetTerrainCompute.SetBuffer(kernel, "_Result", textureBuffer);

                planetTerrainCompute.SetInt("_FaceID", i);
                planetTerrainCompute.SetInt("_FaceSize", textureSize);

                planetTerrainCompute.SetInt("_Octaves", octaves);
                planetTerrainCompute.SetFloat("_Frequency", frequency);
                planetTerrainCompute.SetFloat("_Amplitude", amplitude);

                int threadGroups = Mathf.CeilToInt(textureSize / 8f);
                planetTerrainCompute.Dispatch(kernel, threadGroups, threadGroups, 1);
                
                int captured = i;
                AsyncGPUReadback.Request(textureBuffer, req => CreateTextureArray(req, captured));
            }
        }
    }

    public void CreateTextureArray(AsyncGPUReadbackRequest request, int faceID) {
        if(this == null) return;
        
        NativeArray<float4> colors = request.GetData<float4>();

        CubemapFace face = GetFace(faceID);
        for(int y = 0; y < textureSize; y++) {
            for(int x = 0; x < textureSize; x++) {
                float4 colorVal = colors[x + y * textureSize];

                Color color = new Color(colorVal.x, colorVal.y, colorVal.z, colorVal.w);
                planetaryTerrainTexture.SetPixel(face, x, y, color);
            }
        }

        planetaryTerrainTexture.Apply();
        planetMat.SetTexture("_Cubemap", planetaryTerrainTexture);
    }

    CubemapFace GetFace(int i) {
        if(i == 0) return CubemapFace.PositiveY;
        if(i == 1) return CubemapFace.PositiveX;
        if(i == 2) return CubemapFace.NegativeY;
        if(i == 3) return CubemapFace.NegativeX;
        if(i == 4) return CubemapFace.NegativeZ;
        if(i == 5) return CubemapFace.PositiveZ;
        else return CubemapFace.Unknown;
    }
}
