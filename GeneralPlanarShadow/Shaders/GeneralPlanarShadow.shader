Shader "Mobile Friend/GeneralPlanarShadow"
{
    Properties
    {
        _StencilRef("Stencil Ref", int) = 128
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
            ZTest LEqual
            ZWrite Off
            BlendOp RevSub
            Blend One One

            Stencil
            {
                Ref [_StencilRef]
                Comp NotEqual
                Pass Replace
            }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile DIRECITONAL_PLANAR_SHADOW POINT_PLANAR_SHADOW SPOT_PLANAR_SHADOW

            #include "UnityCG.cginc"
            #include "GeneralPlanarShadow.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                UNITY_FOG_COORDS(2)
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                float4 worldPos = ProjectPlanarShadowVertex(v.vertex);
                o.worldPos = worldPos;
                o.vertex = UnityWorldToClipPos(worldPos);
                o.uv = v.uv;
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return GetPlanarShadowLightColor(i.worldPos);
            }
            ENDCG
        }

        Pass
        {
            Name "CLEARSTENCIL"
            ZTest Off
            ZWrite Off
            Blend Zero One

            Stencil
            {
                Ref [_StencilRef]
                Comp Equal
                Pass Zero
            }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

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
                o.vertex = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return 0;
            }
            ENDCG
        }
    }
}
