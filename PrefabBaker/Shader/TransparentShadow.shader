Shader "PrefabBaker/Transparent Shadow"
{
    Properties
    {
        [HDR]_Clip("Color", color) = (1, 1, 1, 1)
        // ObsoleteProperties
        [HideInInspector] _MainTex("BaseMap", 2D) = "white" {}
    }
    
    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent"
        }

        LOD 100

        Blend SrcAlpha OneMinusSrcAlpha
        
        Pass
        {
            Tags{ "LightMode"="UniversalForward" }

            HLSLPROGRAM

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _Clip;
            CBUFFER_END

            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_instancing
                        
            struct VertexInput
            {
                float4 vertex : POSITION;
                float2 texcoord0 : TEXCOORD0;
                float2 texcoord1 : TEXCOORD1;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct VertexOutput
            {
                float4 pos : SV_POSITION;
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float3 normal : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            VertexOutput vert (VertexInput v)
            {
                VertexOutput o = (VertexOutput)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v,o);
                o.uv0 = v.texcoord0;
                OUTPUT_LIGHTMAP_UV(v.texcoord1, unity_LightmapST, o.uv1);
                o.normal = TransformObjectToWorldNormal(v.normal);
                o.pos = TransformObjectToHClip(v.vertex.xyz);
                return o;
            }
            
            float4 frag(VertexOutput i) : COLOR
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float4 mainColor;
                #if LIGHTMAP_ON
                    mainColor.a = saturate(_Clip.r - SampleLightmap(i.uv1, i.normal).r);
                #else
                    mainColor.a = 1;
                #endif
                mainColor.rgb = 0;
                return mainColor;
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}