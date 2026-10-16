using System.Collections.Generic;
using UnityEngine;

public class QTMeshHandler : MonoBehaviour {
    [Header("MeshProperties")]
    public int maxBranch;
    public int meshSize;
    public Material terrainMat;
    public float skirtHeight;
    public GameObject terrainChunk;
    [Header("Trigger Properties")]
    public Transform player;
    public AnimationCurve rangeByBranch;
    public float minDstToAccountHeight; //LOD does not account for player height above chunks up until this threshold. Then LOD is simply based on distance to chunk
    public bool trigger;
    public bool reset;
    public bool useNoise;
    public int orientation;
    [Header("Planet Properties")]
    public bool spherify;
    public Vector3 playerSpawnDirection;
    public Vector3Int planetCenter;
    public float surfaceGravity;
    [Header("Terrain Properties")]
    public int octaves;
    public int gpuOctaves;
    public float frequency;
    public float amplitude;
    public ComputeShader physicsCompute;
    [Header("Terrain Texture Properties")]
    public TextureArrayBuilder flatDiffuseTextures;
    [Header("Debug Properties")]
    public bool constantUpdate;
    [Range(0f, 1f)]
    public float inflate;
    public int worldScale;
    public float planetRadius;
    [Header("LOD Optimization")]
    public int physicsBranchThreshold;

    List<QTChunk> selectedChunks = new List<QTChunk>();
    List<QTChunk> updatingChunks = new List<QTChunk>();
    QTChunk[] baseChunks;

    List<GPUChunkData>[] gpuChunks = new List<GPUChunkData>[6] {
        new List<GPUChunkData>(), new List<GPUChunkData>(), new List<GPUChunkData>(),
        new List<GPUChunkData>(), new List<GPUChunkData>(), new List<GPUChunkData>()
    };

    List<TerrainMeshGenerator> terrainGens = new List<TerrainMeshGenerator>();

        class PendingSwap {
        public List<QTChunk> waitFor;      // new chunks that must finish generating
        public List<GameObject> toDestroy; // old objects to remove once they're done
        }
    List<PendingSwap> pendingSwaps = new List<PendingSwap>();

    void Start() {
        worldScale = Mathf.RoundToInt(Mathf.Pow(2, maxBranch));
        planetRadius = worldScale * Mathf.Sqrt(3f) / 2f;

        if(playerSpawnDirection.magnitude >= 0.01f) {
            Vector3 center = -planetRadius * playerSpawnDirection.normalized;
            planetCenter = new Vector3Int(Mathf.RoundToInt(center.x), Mathf.RoundToInt(center.y), Mathf.RoundToInt(center.z));
            player.GetComponent<Rigidbody>().position = playerSpawnDirection.normalized * 600f;
        }
        terrainMat.SetVector("_PlanetCenter", new Vector3(planetCenter.x, planetCenter.y, planetCenter.z));
        terrainMat.SetFloat("_MeshSize", meshSize);

        GetComponent<GPUChunkRenderer>().Initialize(worldScale, meshSize);

        if(baseChunks == null){
            int size = worldScale / 2;
            baseChunks = new QTChunk[]{
                new QTChunk(maxBranch, new Vector3Int(-1,1,-1) * size + planetCenter, Vector3.zero, 0, null, null, null),
                new QTChunk(maxBranch, new Vector3Int(1,1,-1) * size + planetCenter, Vector3.zero, 1, null, null, null),
                new QTChunk(maxBranch, new Vector3Int(1,-1,-1) * size + planetCenter, Vector3.zero, 2, null, null, null),
                new QTChunk(maxBranch, new Vector3Int(-1,-1,-1) * size + planetCenter, Vector3.zero, 3, null, null, null),
                new QTChunk(maxBranch, new Vector3Int(-1,-1,-1) * size + planetCenter, Vector3.zero, 4, null, null, null),
                new QTChunk(maxBranch, new Vector3Int(-1,1,1) * size + planetCenter, Vector3.zero, 5, null, null, null),
            };
        }
        foreach(QTChunk bc in baseChunks){
            CreateChunkObj(bc);
        }
    }

    void Update() {
        if (trigger) {
            trigger = false;
            CreateQTChunks();
        }
        if (reset) {
            reset = false;
            selectedChunks.Clear();
        }

        //For debug only
        if (constantUpdate) {
            List<TerrainMeshGenerator> toRemove = new List<TerrainMeshGenerator>();
            foreach(TerrainMeshGenerator tmg in terrainGens) {
                if(tmg != null){
                    tmg.inflate = inflate;
                    tmg.GenerateColliderCompute();
                } else {
                    toRemove.Add(tmg);
                }
            }
            foreach(TerrainMeshGenerator tmg in toRemove) {
                terrainGens.Remove(tmg);
            }
        }

        ResolvePendingSwaps();
    }

    Vector3Int GetCoordShift(int orientation, int child) {
        Vector3Int[,] coordShifts = new Vector3Int[6,4] {
            { new Vector3Int(0,0,0), new Vector3Int(0,0,1), new Vector3Int(1,0,1), new Vector3Int(1,0,0) },
            { new Vector3Int(0,0,0), new Vector3Int(0,0,1), new Vector3Int(0,-1,1), new Vector3Int(0,-1,0) },
            { new Vector3Int(0,0,0), new Vector3Int(0,0,1), new Vector3Int(-1,0,1), new Vector3Int(-1,0,0) },
            { new Vector3Int(0,0,0), new Vector3Int(0,0,1), new Vector3Int(0,1,1), new Vector3Int(0,1,0) },
            { new Vector3Int(0,0,0), new Vector3Int(0,1,0), new Vector3Int(1,1,0), new Vector3Int(1,0,0) },
            { new Vector3Int(0,0,0), new Vector3Int(0,-1,0), new Vector3Int(1,-1,0), new Vector3Int(1,0,0) },
        };

        return coordShifts[orientation, child];
    }

    public void CreateQTChunks() {
        selectedChunks.Clear();
        updatingChunks.Clear();
        for(int i = 0; i < 6; i++){
            gpuChunks[i].Clear();
        }

        selectedChunks.AddRange(baseChunks);
        List<QTChunk> toAdd = new List<QTChunk>();
        
        for(int j = 0; j < maxBranch + 1; j++){
            for(int i = 0; i < selectedChunks.Count; i++){
                QTChunk chunk = selectedChunks[i];
                
                int scale = Mathf.RoundToInt(Mathf.Pow(2, chunk.branch));
                Vector3 playerPosOnSphere = (player.position / meshSize - planetCenter).normalized * planetRadius + planetCenter;
                float dstToCenter = Vector3.Distance(player.position / meshSize, GetChunkCenter(chunk));

                dstToCenter = (dstToCenter < minDstToAccountHeight) ? Vector3.Distance(playerPosOnSphere, GetChunkCenter(chunk)) / scale : dstToCenter / scale;
                
                if(dstToCenter <= rangeByBranch.Evaluate((float)chunk.branch / maxBranch) && chunk.branch > 0) {
                    scale = Mathf.RoundToInt(Mathf.Pow(2, chunk.branch - 1));
                    if(chunk.children == null) {
                        chunk.children = new QTChunk[4];
                        for(int c = 0; c < 4; c++) {
                            chunk.children[c] = new QTChunk(chunk.branch - 1, chunk.coord + GetCoordShift(chunk.orientation, c) * scale, chunk.offset, chunk.orientation, null, chunk, null);
                            Vector3 relativeCoord = chunk.children[c].coord - planetCenter;
                            chunk.children[c].offset = relativeCoord.normalized * planetRadius - relativeCoord;
                        }

                        if(chunk.obj != null) {
                            pendingSwaps.Add(new PendingSwap{
                                waitFor = new List<QTChunk>(chunk.children),
                                toDestroy = new List<GameObject>{ chunk.obj }
                            });
                            chunk.obj = null;
                        }
                    }

                    toAdd.Add(chunk.children[0]);
                    toAdd.Add(chunk.children[1]);
                    toAdd.Add(chunk.children[2]);
                    toAdd.Add(chunk.children[3]);
                } else { 
                    //LEAF NODE
                    if(chunk.children != null) {                    
                        List<GameObject> oldObjs = new List<GameObject>();
                        CollectSubtreeObjects(chunk, oldObjs);
                        chunk.children = null;

                        if(oldObjs.Count > 0) {
                            pendingSwaps.Add(new PendingSwap {
                                waitFor = new List<QTChunk> { chunk },
                                toDestroy = oldObjs
                            });
                        }
                    }

                    if(chunk.branch <= physicsBranchThreshold) {
                        if (chunk.obj == null && !updatingChunks.Contains(chunk)) {
                            updatingChunks.Add(chunk);
                        }
                    } else {
                        GPUChunkData data = new GPUChunkData {
                            coord = chunk.coord,
                            size = scale,
                            faceId = chunk.orientation
                        };
                        gpuChunks[chunk.orientation].Add(data);

                        if(chunk.obj != null) {
                            DestroyChunkObj(chunk.obj);
                            chunk.obj = null;
                        }
                    }
                }

                void CollectSubtreeObjects(QTChunk chunk, List<GameObject> collected) {
                    // 1. Recurse into children first, same as before
                    if (chunk.children != null) {
                        for (int i = 0; i < 4; i++) {
                            if (chunk.children[i] != null) {
                                CollectSubtreeObjects(chunk.children[i], collected);
                            }
                        }
                    }

                    // 2. Instead of destroying this chunk's object, collect it
                    if (chunk.obj != null) {
                        collected.Add(chunk.obj);
                        chunk.obj = null; // chunk no longer owns it — the PendingSwap does
                    }
                }

            }
            selectedChunks.Clear();
            selectedChunks.AddRange(toAdd);
            toAdd.Clear();
        }
        
        foreach(QTChunk c in updatingChunks) {
            CreateChunkObj(c);
        }

        GetComponent<GPUChunkRenderer>().RenderChunks(gpuChunks);
    }

    Vector3 GetChunkCenter(QTChunk chunk) {
        int scale = Mathf.RoundToInt(Mathf.Pow(2, chunk.branch));
        //center of the squareChunk CHUNKCOORD REFERENCE so no * meshSize
        Vector3 faceCenterC = chunk.coord + (Vector3)GetCoordShift(chunk.orientation, 2) * scale * 0.5f; //index is 2 as that is the Vector2.one shift for a given orientation
        //center of spherified chunk
        Vector3 faceCenterS = (faceCenterC - planetCenter).normalized * planetRadius + planetCenter;

        Vector3 center = (Mathf.RoundToInt(inflate) == 0) ? faceCenterC : faceCenterS;

        return center;
    }

    void CreateChunkObj(QTChunk chunk) {
        if(chunk.children == null){
            DestroyChunkObj(chunk.obj);

            GameObject terrainChunk = new GameObject("QTChunk" + chunk.branch + (chunk.children == null) + chunk.orientation);
            TerrainMeshGenerator terrainGen = terrainChunk.AddComponent<TerrainMeshGenerator>();
            terrainChunk.AddComponent<MeshFilter>();
            terrainChunk.AddComponent<MeshRenderer>();
            terrainChunk.AddComponent<MeshCollider>();

            terrainGen.meshHandler = this;

            terrainGen.coord = chunk.coord;
            terrainGen.offset = chunk.offset;
            terrainGen.orientation = chunk.orientation;
            terrainGen.branch = chunk.branch;

            terrainGen.GenerateColliderCompute();

            if (!terrainGens.Contains(terrainGen)) {
                terrainGens.Add(terrainGen);
            }

            terrainChunk.transform.parent = transform;
            
            chunk.obj = terrainChunk;
        } else {
            DestroyChunkObj(chunk.obj);
            chunk.obj = null;
        }
    }

    void ResolvePendingSwaps() {
        for(int i = pendingSwaps.Count - 1; i >= 0; i--) {
            if(pendingSwaps[i].waitFor.TrueForAll(ChunkIsReady)) {
                foreach(GameObject go in pendingSwaps[i].toDestroy) {
                    if(go != null) DestroyChunkObj(go);
                }
                pendingSwaps.RemoveAt(i);
            }
        }
    }

    bool ChunkIsReady(QTChunk c) {
        // GPU-rendered chunks appear the same frame RenderChunks runs — always "ready"
        if(c.branch > physicsBranchThreshold) return true;
        // it split again before finishing — its own swap will cover it
        if(c.children != null) return true;
        // physics chunk: ready once the async readback finished
        return c.obj != null && c.obj.GetComponent<TerrainMeshGenerator>().meshGenerationComplete;
    }

    void DestroyChunkObj(GameObject obj) {
        if(obj != null) obj.GetComponent<TerrainMeshGenerator>().OnDestroy();
    }

    
}

public class QTChunk {
    public int branch;
    public Vector3Int coord;
    public Vector3 offset; //chunk offset from coord; offset optimizes transform position by setting it to the sphere position (not cube)
    public int orientation; //0,1,2,3,4,5 - up, right, down, left, for, back
    public QTChunk[] children;
    public QTChunk parent;
    public GameObject obj;
    public QTChunk(int branch, Vector3Int coord, Vector3 offset, int orientation, QTChunk[] children, QTChunk parent, GameObject obj) {
        this.branch = branch;
        this.coord = coord;
        this.offset = offset;
        this.orientation = orientation;
        this.children = children;
        this.parent = parent;
        this.obj = obj;
    }
}
