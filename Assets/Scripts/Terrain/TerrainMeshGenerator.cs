using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;

public class TerrainMeshGenerator : MonoBehaviour {
    struct Vertex {
        public float height;
        public Vector3 normal;
    }
    public int meshSize;
    public Vector3Int coord;
    public Vector3 offset;
    public int orientation;
    public int branch;
    public int maxBranch;
    Vector3[] vertices;
    Vector3[] normals;
    Vector2[] uvs;
    int[] triangles;
    public bool useNoise;
    public bool spherify;
    public float planetRadius;
    public Vector3Int planetCenter;
    public Material terrainMat;
    public float skirtHeight;
    Texture2D debugTex;

    public int octaves;
    public int gpuOctaves;
    public float frequency;
    public float amplitude;
    public float inflate;

    int scale;
    int worldScale;

    public bool meshGenerationComplete;

    public ComputeShader physicsCompute;
    ComputeBuffer heightBuffer;
    public QTMeshHandler meshHandler;

    void Initialize() {
        meshSize = meshHandler.meshSize;
        maxBranch = meshHandler.maxBranch;

        useNoise = meshHandler.useNoise;
        terrainMat = meshHandler.terrainMat;
        skirtHeight = meshHandler.skirtHeight;

        spherify = meshHandler.spherify;
        planetRadius = meshHandler.planetRadius;
        planetCenter = meshHandler.planetCenter;

        octaves = meshHandler.octaves;
        frequency = meshHandler.frequency;
        amplitude = meshHandler.amplitude;
        physicsCompute = meshHandler.physicsCompute;

        inflate = meshHandler.inflate;


        meshGenerationComplete = false;
        scale = Mathf.RoundToInt(Mathf.Pow(2, branch));
        worldScale = Mathf.RoundToInt(Mathf.Pow(2, maxBranch));

        debugTex = new Texture2D(meshSize, meshSize) {
            filterMode = FilterMode.Point
        };

        gpuOctaves = meshHandler.gpuOctaves;
    }

    public static Vector3 GetVertOrientation(int x, int y, int i){
        Vector3[] verts = new Vector3[6]{
            new Vector3(x, 0, y), 
            new Vector3(0, -x, y), 
            new Vector3(-x, 0, y), 
            new Vector3(0, x, y), 
            new Vector3(x, y, 0), 
            new Vector3(x, -y, 0)
        };

        return verts[i];
    }

    public void GenerateColliderCompute() {
        Initialize();

        int vertCount = (meshSize + 1) * (meshSize + 1);

        heightBuffer = new ComputeBuffer(vertCount, sizeof(float) * 4);
        //heightBuffer.SetData(new float[vertCount]);

        int kernel = physicsCompute.FindKernel("CSMain");
        physicsCompute.SetBuffer(kernel, "_Vertices", heightBuffer);

        physicsCompute.SetInt("_MeshSize", meshSize);
        physicsCompute.SetInt("_FaceID", orientation);
        physicsCompute.SetVector("_ChunkCenter", (Vector3)coord);
        physicsCompute.SetInt("_ChunkSize", scale);
        physicsCompute.SetFloat("_PlanetRadius", planetRadius);
        physicsCompute.SetVector("_PlanetCenter", (Vector3)planetCenter);
        
        physicsCompute.SetInt("_Octaves", octaves);
        physicsCompute.SetFloat("_Frequency", frequency);
        physicsCompute.SetFloat("_Amplitude", amplitude);

        int threadGroups = Mathf.CeilToInt((meshSize + 1) / 8f);
        physicsCompute.Dispatch(kernel, threadGroups, threadGroups, 1);

        AsyncGPUReadback.Request(heightBuffer, CreateMesh);
    }
    public void CreateMesh(AsyncGPUReadbackRequest request) {
        if(this == null) return;
        
        if (request.hasError) {
            Debug.LogError("GPU Readback failed.");
            heightBuffer.Release();
            return;
        }

        NativeArray<Vertex> vertData = request.GetData<Vertex>();

        SetGlobalScale(transform, Vector3.one * scale);
        transform.position = (coord + offset) * meshSize;

        vertices = new Vector3[(meshSize + 3) * (meshSize + 3)];
        normals = new Vector3[vertices.Length];
        uvs = new Vector2[vertices.Length];

        int i = 0;
        for(int y = -1; y <= meshSize + 1; y++) {
            for(int x = -1; x <= meshSize + 1; x++) {
                Vector3 vert = GetVertOrientation(x, y, orientation);

                Vector3 worldPos = GetWorldPos(x, y);
                if(x < meshSize && y < meshSize){
                    Vector3 color = (worldPos - planetCenter) / worldScale * Mathf.PerlinNoise(1.1f * x, 1.1f * y);
                    debugTex.SetPixel(x, y, new Color(color.x, color.y, color.z, 1f));
                }

                if (spherify) {
                    vert += Spherify(worldPos) * inflate - offset * meshSize / scale;
                }
                
                int lx = x;
                int ly = y;
                bool isEdge = false;

                if (lx < 0) lx = 0;
                if (ly < 0) ly = 0;
                if (lx > meshSize) lx = meshSize;
                if (ly > meshSize) ly = meshSize;
                if(lx != x || ly != y) isEdge = true;

                int index = lx + ly * (meshSize + 1);

                float height = vertData[index].height;
                Vector3 vertHeightOffset = (worldPos - planetCenter).normalized * height / scale;
                if(isEdge) vertHeightOffset += (worldPos - planetCenter).normalized * -skirtHeight;

                //print(height);

                vertices[i] = vert + vertHeightOffset;
                normals[i] = vertData[index].normal;
                uvs[i] = new Vector2(x, y) / meshSize;

                i++;
            }
        }

        debugTex.Apply();

        i = 0;
        triangles = new int[(meshSize + 2) * (meshSize + 2) * 6];
        for(int y = 0; y < meshSize + 2; y++) {
            for(int x = 0; x < meshSize + 2; x++) {
                triangles[i + 0] = x + 0 + (y + 0) * (meshSize + 3);
                triangles[i + 1] = x + 0 + (y + 1) * (meshSize + 3);
                triangles[i + 2] = x + 1 + (y + 0) * (meshSize + 3);
                triangles[i + 3] = x + 0 + (y + 1) * (meshSize + 3);
                triangles[i + 4] = x + 1 + (y + 1) * (meshSize + 3);
                triangles[i + 5] = x + 1 + (y + 0) * (meshSize + 3);
                i += 6;
            }
        }

        MeshFilter meshFilter = GetComponent<MeshFilter>();
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        MeshCollider meshCollider = GetComponent<MeshCollider>();

        Mesh mesh = new Mesh();
        meshFilter.sharedMesh = mesh;
        meshRenderer.material = terrainMat;

        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = triangles;

        meshCollider.sharedMesh = mesh;

        meshGenerationComplete = true;
    }

    Vector3 GetWorldPos(int x, int y) {
        Vector3 worldPos = GetVertOrientation(x, y, orientation) / meshSize * scale + coord;
        return worldPos;
    }

    Vector3 Spherify(Vector3 worldPos) {
        Vector3 dc = worldPos - planetCenter;

        Vector3 dh = dc.normalized * (planetRadius - dc.magnitude) * meshSize / scale;
        return dh;
    }

    public void SetGlobalScale (Transform transform, Vector3 globalScale) {
        transform.localScale = Vector3.one;
        transform.localScale = new Vector3 (globalScale.x/transform.lossyScale.x, globalScale.y/transform.lossyScale.y, globalScale.z/transform.lossyScale.z);
    }

    public void OnDestroy() {
        if(heightBuffer != null) {
            heightBuffer.Release();
            heightBuffer = null;
        }
        Destroy(gameObject);
    }

}
