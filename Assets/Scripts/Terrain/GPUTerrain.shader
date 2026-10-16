Shader "Custom/GPULowResTerrainURP" {
    Properties {
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _Sphereify("Sphereify", Float) = 1
        _Octaves("Octaves", Float) = 1
        _Frequency("Frequency", Float) = 1
        _Amplitude("Amplitude", Float) = 1
    }
    SubShader {
        Tags { 
            "RenderType"="Opaque" 
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Geometry"
        }
        LOD 100

        Pass {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5 

            // Core URP library includes
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "NoiseSimplex.hlsl"
 
            struct Attributes {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
            };

            struct Varyings {
                float4 positionCS       : SV_POSITION;
                float3 positionWS       : TEXCOORD0;
                float3 positionSPHERE   : TEXCOORD1; //positionSphere
                float3 normalWS         : INTERNAL_NORMAL;
            };

            // Structure matching your C# side layout
            struct GPUChunkData {
                float3 center;
                float size;
                int faceId;
            };

            // Uniforms and Structured Buffers
            StructuredBuffer<GPUChunkData> _ChunkBuffer;
            
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
            CBUFFER_END

            float3 _PlanetCenter;
            float _PlanetRadius;
            float _MeshSize;
            float _Sphereify;
            float _SkirtHeight;
            
            int _Octaves;
            float _Frequency;
            float _Amplitude;

            #include "NoiseTerrainGenerator.hlsl"

            float3 Spherify(float3 cubeWorldPos, float chunkScale) {
                float3 dc = cubeWorldPos - _PlanetCenter;
                float3 dh = normalize(dc) * (_PlanetRadius - length(dc));
                
                return dh;
            }

            float3 NormalToSphere(float3 sphereNormal, float3 heightNormal){
                float3 up = abs(sphereNormal.y) < 0.999 ? float3(0.0, 1.0, 0.0) : float3(1.0, 0.0, 0.0);

                float3 tangent = normalize(cross(up, sphereNormal));
                float3 bitangent = cross(sphereNormal, tangent);
                float3x3 TBN = float3x3(bitangent, sphereNormal, tangent);

                return normalize(mul(heightNormal, TBN));
            }

            float GetHeight(float3 sphereWorldPos){
                float3 pos = sphereWorldPos;
                return LayeredNoise(pos, _Octaves, _Frequency, _Amplitude);
            }

            float3 GetNormal(float3 sphereWorldPos, float3 sphereNormal){
                float3 pos = sphereWorldPos;
                return GetNoiseNormal(pos, sphereNormal, _Octaves, _Frequency, _Amplitude);
            }

            Varyings vert (Attributes input, uint instanceID : SV_InstanceID) {
                Varyings output;

                _SkirtHeight = 10;

                // 1. Extract instance data from the ComputeBuffer
                GPUChunkData chunk = _ChunkBuffer[instanceID];

                float3 pos = chunk.center;
                float size = chunk.size;
                int faceId = chunk.faceId;
                
                //float3 balancedLocalPos = (input.positionOS.xyz / _MeshSize) - 0.5;
                // 2. Process local flat grid vertex positions relative to chunk data
                float3 gridPos = abs(input.positionOS.xyz);

                float lx = gridPos.x;
                float ly = gridPos.y;
                float lz = gridPos.z;

                if(lx > _MeshSize) lx = _MeshSize;
                if(ly > _MeshSize) ly = _MeshSize;
                if(lz > _MeshSize) lz = _MeshSize;

                bool isEdge = (lx != gridPos.x || ly != gridPos.y || lz != gridPos.z);

                float3 localPos = input.positionOS.xyz * size / _MeshSize; //balancedLocalPos * _MeshSize * size;
                float3 cubeFacePos = localPos + pos;

                // 3. Project to the sphere surface
                float3 sphereWorldPos = cubeFacePos + Spherify(cubeFacePos, size) * _Sphereify;
                float3 sphereNormal = normalize(sphereWorldPos - _PlanetCenter);

                float height = GetHeight(sphereWorldPos);

                float3 planetWorldPos = sphereWorldPos + sphereNormal * height;
                if(isEdge) planetWorldPos += sphereNormal * -_SkirtHeight;

                float3 normal = sphereNormal;// heightNormal;//NormalToSphere(sphereNormal, heightNormal);
                
                // 4. Transform from World Space (WS) to Homogeneous Clip Space (CS)
                // In URP instancing paths, the procedurally deformed position serves as World Space
                output.positionWS = planetWorldPos * _MeshSize;
                output.positionCS = TransformWorldToHClip(planetWorldPos * _MeshSize);
                output.positionSPHERE = sphereWorldPos;
                output.normalWS = normal;

                return output; 
            }

            half4 frag (Varyings input) : SV_Target {
                float3 color = float3(1,1,1);
                
                float3 heightNormal = GetNormal(input.positionSPHERE, input.normalWS);
                float3 normal = NormalToSphere(input.normalWS, heightNormal);

                float3 lightDir = GetMainLight().direction;
                float light = (dot(normal, lightDir) + 1 / 2.0);
                
                float height = (input.positionWS / _MeshSize - input.positionSPHERE).y;
                
                //color = normal;
                return half4(color * light, 1.0);
                //return half4(color + normal, 1.0);
            }
            ENDHLSL
        }
    }
}