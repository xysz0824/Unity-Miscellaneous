Shader "Mobile Friend/GeneralPlanarShadow"
{
    Properties
    {
        // ObsoleteProperties
        [HideInInspector] _MainTex("BaseMap", 2D) = "white" {}
        [HideInInspector] _Color("Base Color", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType"="TransparentCutout" }
        LOD 100

        Pass
        {
            Name "PlanarShadowCaster"
            Tags { "LightMode" = "PlanarShadowCaster" }

            ZTest LEqual
            ZWrite Off
            BlendOp RevSub
            Blend One One
            Cull[_Cull]

            Stencil
            {
                Ref [_StencilRef]
                Comp NotEqual
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile DIRECITONAL_PLANAR_SHADOW POINT_PLANAR_SHADOW SPOT_PLANAR_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
            #include "GeneralPlanarShadow.hlsl"

            ENDHLSL
        }

        Pass
        {
            Name "PlanarShadowCasterAlphaTest"
            Tags { "LightMode" = "PlanarShadowCasterAlphaTest" }

            ZTest LEqual
            ZWrite Off
            BlendOp RevSub
            Blend One One
            Cull[_Cull]

            Stencil
            {
                Ref [_StencilRef]
                Comp NotEqual
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex vertAlphaTest
            #pragma fragment fragAlphaTest
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_instancing
            #pragma multi_compile DIRECITONAL_PLANAR_SHADOW POINT_PLANAR_SHADOW SPOT_PLANAR_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
            #include "GeneralPlanarShadow.hlsl"

            ENDHLSL
        }

        Pass
        {
            Name "StencilClear"
            ZTest Off
            ZWrite Off
            Blend Zero One
            ColorMask 0

            Stencil
            {
                Ref [_StencilRef]
                Comp Equal
                Pass Zero
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
