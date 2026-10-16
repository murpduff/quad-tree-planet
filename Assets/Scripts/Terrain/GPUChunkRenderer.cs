using System.Collections.Generic;
using UnityEngine;

public class GPUChunkRenderer : MonoBehaviour {
    public Mesh[] baseGridMeshes = new Mesh[6];
    public Material gpuTerrainMat;
    public bool[] isolateOrientation = new bool[6];
    public QTMeshHandler meshHandler;
    float worldScale;
    int meshSize;
    Vector3 planetCenter;
    float skirtHeight;
    ComputeBuffer[] faceBuffers = new ComputeBuffer[6];
    int[] activeCounts = new int[6];
    float planetRadius;
    MaterialPropertyBlock propertyBlock;

    public void Initialize(int worldScale, int meshSize) {
        meshHandler = GetComponent<QTMeshHandler>();
        this.worldScale = worldScale;
        this.meshSize = meshSize;
        planetCenter = FindAnyObjectByType<QTMeshHandler>().planetCenter;
        skirtHeight = meshHandler.skirtHeight;

        planetRadius = worldScale * Mathf.Sqrt(3f) / 2f;
        GenerateBaseGridMeshes();
    }

    void GenerateBaseGridMeshes() {
        for(int o = 0; o < 6; o++) {
            Mesh mesh = new Mesh();
            Vector3[] verts = new Vector3[(meshSize + 2) * (meshSize + 2)];
            int[] triangles = new int[(meshSize + 1) * (meshSize + 1) * 6];

            int i = 0;
            for(int y = 0; y <= meshSize + 1; y++) {
                for(int x = 0; x <= meshSize + 1; x++) {
                    Vector3 vert = TerrainMeshGenerator.GetVertOrientation(x, y, o);
                    verts[i] = vert;
                    i++;
                }
            }

            i = 0;
            for(int y = 0; y < meshSize + 1; y++) {
                for(int x = 0; x < meshSize + 1; x++) {
                    triangles[i + 0] = x + 0 + (y + 0) * (meshSize + 2);
                    triangles[i + 1] = x + 0 + (y + 1) * (meshSize + 2);
                    triangles[i + 2] = x + 1 + (y + 0) * (meshSize + 2);
                    triangles[i + 3] = x + 0 + (y + 1) * (meshSize + 2);
                    triangles[i + 4] = x + 1 + (y + 1) * (meshSize + 2);
                    triangles[i + 5] = x + 1 + (y + 0) * (meshSize + 2);
                    i += 6;
                }
            }

            mesh.vertices = verts;
            mesh.triangles = triangles;
            baseGridMeshes[o] = mesh;
        }
    }

    public void RenderChunks(List<GPUChunkData>[] newChunkData) {
        for(int i = 0; i < 6; i++) {
            int count = newChunkData[i].Count;
            activeCounts[i] = count;

            if(count == 0) {
                if(faceBuffers[i] != null) {
                    faceBuffers[i].Release();
                    faceBuffers[i] = null;
                }
                continue;
            }

            if(faceBuffers[i] == null || faceBuffers[i].count != count) {
                if(faceBuffers[i] != null) faceBuffers[i].Release();
                faceBuffers[i] = new ComputeBuffer(count, 20);
            }

            faceBuffers[i].SetData(newChunkData[i].ToArray());
        }

    }

    void Update() {
        if(worldScale == 0) worldScale = GetComponent<QTMeshHandler>().worldScale;

        if(propertyBlock == null) propertyBlock = new MaterialPropertyBlock();

        Vector3 boundsScale = Vector3.one * (planetRadius * 2 * meshSize + 1000f);
        Bounds planetBounds = new Bounds(planetCenter * meshSize, boundsScale);

        for(int i = 0; i < 6; i++) {
            if(!isolateOrientation[i]) continue;

            propertyBlock.Clear();
            
            propertyBlock.SetBuffer("_ChunkBuffer", faceBuffers[i]);
            propertyBlock.SetVector("_PlanetCenter", planetCenter);
            propertyBlock.SetFloat("_PlanetRadius", planetRadius);
            propertyBlock.SetFloat("_MeshSize", meshSize);
            propertyBlock.SetFloat("_SkirtHeight", skirtHeight);

            propertyBlock.SetInt("_Octaves", meshHandler.gpuOctaves);
            propertyBlock.SetFloat("_Frequency", meshHandler.frequency);
            propertyBlock.SetFloat("_Amplitude", meshHandler.amplitude);

            
            if(activeCounts[i] > 0) Graphics.DrawMeshInstancedProcedural(baseGridMeshes[i], 0, gpuTerrainMat, planetBounds, activeCounts[i], propertyBlock);
        }
    }

    void OnDestroy() {
        for(int i = 0; i < 6; i++){
            if(faceBuffers[i] != null) {
                faceBuffers[i].Release();
            }
        }
    }
}

public struct GPUChunkData {
    public Vector3 coord;
    public float size;
    public int faceId;
}
