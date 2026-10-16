using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;

public class PhysicsChunkBuilder : MonoBehaviour {
    public ComputeShader physicsCompute;
    public Material mat;
    MeshFilter meshFilter;
    MeshRenderer meshRenderer;
    MeshCollider meshCollider;

    public int meshSize = 16;
    public Vector3 chunkCenter;
    public int chunkSize;
    public float planetRadius;
    public Vector3 planetCenter;

    public int octaves;
    public float frequency;
    public float amplitude;

    ComputeBuffer vertexBuffer;

    public void GenerateCollider() {
        int vertCount = meshSize * meshSize;

        vertexBuffer = new ComputeBuffer(vertCount, 12);

        int kernel = physicsCompute.FindKernel("CSMain");
        physicsCompute.SetBuffer(kernel, "_Vertices", vertexBuffer);

        physicsCompute.SetInt("_MeshSize", meshSize);
        physicsCompute.SetVector("_ChunkCenter", chunkCenter);
        physicsCompute.SetInt("_ChunkSize", chunkSize);
        physicsCompute.SetFloat("_PlanetRadius", planetRadius);
        physicsCompute.SetVector("_PlanetCenter", planetCenter);
        
        physicsCompute.SetInt("_Octaves", octaves);
        physicsCompute.SetFloat("_Frequency", frequency);
        physicsCompute.SetFloat("_Amplitude", amplitude);

        int threadGroups = Mathf.CeilToInt(meshSize / 8f);
        physicsCompute.Dispatch(kernel, threadGroups, threadGroups, 1);

        AsyncGPUReadback.Request(vertexBuffer, OnCompleteReadback);
    }

    void OnCompleteReadback(AsyncGPUReadbackRequest request) {
        if (request.hasError) {
            Debug.LogError("GPU Readback failed.");
            vertexBuffer.Release();
            return;
        }

        NativeArray<Vector3> gpuVerts = request.GetData<Vector3>();

        int i = 0;
        int[] triangles = new int[meshSize * meshSize * 6];
        for(int y = 0; y < meshSize; y++) {
            for(int x = 0; x < meshSize; x++) {
                triangles[i + 0] = x + 0 + (y + 0) * (meshSize + 1);
                triangles[i + 1] = x + 0 + (y + 1) * (meshSize + 1);
                triangles[i + 2] = x + 1 + (y + 0) * (meshSize + 1);
                triangles[i + 3] = x + 0 + (y + 1) * (meshSize + 1);
                triangles[i + 4] = x + 1 + (y + 1) * (meshSize + 1);
                triangles[i + 5] = x + 1 + (y + 0) * (meshSize + 1);
                i += 6;
            }
        }

        if(meshFilter == null) meshFilter = GetComponent<MeshFilter>();
        if(meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
        if(meshCollider == null) meshCollider = GetComponent<MeshCollider>();

        Mesh collisionMesh = new Mesh();
        collisionMesh.SetVertices(gpuVerts);
        collisionMesh.triangles = triangles;

        meshFilter.mesh = collisionMesh;
        meshRenderer.material = mat;
        meshCollider.sharedMesh = collisionMesh;
    }
}//This did not work :/, I just went back to the one I had before and adjusted it to use the compute shader
