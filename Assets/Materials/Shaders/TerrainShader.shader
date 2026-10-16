Shader "Custom/TerrainShader"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        _SlopeThreshold("Slope Threshold", Float) = 1.0
        _SlopeFade("Slope Fade", Float) = 1.0
        _SlopePow("Slope Power", Float) = 1.0
        _MeshSize("Mesh Size", Float) = 1.0
        _PlanetCenter("Planet Center", Vector) = (0, 0, 0, 0)
        _MacroDiffuseArray("Flat Texture", 2DArray) = "white" {}
        _MacroNormalArray("Flat Normal", 2DArray) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD0;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D_ARRAY(_MacroDiffuseArray);
            SAMPLER(sampler_MacroDiffuseArray);
            TEXTURE2D_ARRAY(_MacroNormalArray);
            SAMPLER(sampler_MacroNormalArray);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _BaseMap_ST;
                float3 _PlanetCenter;
                float _MeshSize;
                float _SlopeThreshold;
                float _SlopeFade;
                float _SlopePow;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);

                OUT.positionCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);

                return OUT;
            }

            #include "BaseTerrainGraphics.hlsl"

            float4 frag(Varyings IN) : SV_Target
            {
                float3 sphereNormal = normalize(IN.positionWS / _MeshSize - _PlanetCenter);
                TextureProperties texProperties = GetTextureProperties(IN.positionWS / _MeshSize, IN.normalWS);

                float4 color = texProperties.color;
                float3 normal = IN.normalWS;
                
                float lighting = saturate(dot(normal, GetMainLight().direction) + 1.0 / 2.0);

                return color * lighting;
            }
            ENDHLSL
        }

        

        // 2. DEPTH ONLY (For Scene Depth Debugger)
        Pass {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag // Keep this...

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes IN) {
                Varyings OUT;
                float3 worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                // Apply your curvature math here!
                OUT.positionCS = TransformWorldToHClip(worldPos);
                return OUT;
            }

            // ...But you MUST define this function for the pragma above to work
            half4 frag(Varyings IN) : SV_Target { 
                return 0; // The GPU ignores this because of ColorMask 0, but the compiler needs it.
            }
            ENDHLSL
        }

        // 3. DEPTH NORMALS (For Normals Debugger)
        Pass {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };

            Varyings vert(Attributes IN) {
                Varyings OUT;
                float3 worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                // Apply Curvature here too!
                OUT.positionCS = TransformWorldToHClip(worldPos);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target {
                float3 normal = normalize(IN.normalWS);
                return float4(normal, 0); 
            }
            ENDHLSL
        }
    }
}
