// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'
// Upgrade NOTE: replaced '_World2Object' with 'unity_WorldToObject'

Shader "FootBall/Scene/PBRCrowdShader"
{
	Properties
	{
		_Color ("Color",Color)=(0.5,0.5,0.5,1)
		_MainTex ("Texture", 2D) = "white" {}
		_NormalScale("BumpScale",Range(0,2))=1
		_BumpMap("BumpMap",2D)="bump"{}
		_Metallic("Metallic",Range(0,1))=0
		_Glossiness("Smoothness",Range(0,1))=0.2
		_GlossMap("R=>Smoothness G=>Meta",2D)="white"{}
		_UVSplit("UV Split", Float) = 1
		_UVIndex("UV Index", Float) = 1
		[HDR]_Emission("Emission",Color)=(0,0,0,0)
		[Toggle]_EnableSkinning("Enable Skinning", Float) = 0
	}


	SubShader
	{
		Blend SrcAlpha OneMinusSrcAlpha
		Tags { "RenderType"="Opaque" "Queue" = "Geometry" "LightMode" = "ForwardBase"}
		LOD 300

		Pass
		{	
			Name "FORWARD"
		
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_fog
			#pragma target 3.0
			#pragma multi_compile_fwdbase
			#pragma multi_compile_instancing
			
            #include "UnityCG.cginc"
            #include "AutoLightSelf.cginc"
            #include "Lighting.cginc"
            #include "UnityPBSLighting.cginc"
            #include "UnityStandardBRDF.cginc"
            #include "core.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv0 : TEXCOORD0;
				float4 uv1 : TEXCOORD1;
				float4 uv2 : TEXCOORD2;
				float4 tangent: TANGENT;
				float3 normal:NORMAL;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct v2f
			{
				float2 uv0 : TEXCOORD0;
				float3 worldPos:TEXCOORD3;
				float4 pos : SV_POSITION;
				float3 normal:TEXCOORD4;
				float4 tangentDir : TEXCOORD5;
				UNITY_FOG_COORDS(6)
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			uniform sampler2D _BakedAnimationTexture;
			uniform float4 _BakedAnimationTexture_SizeInfo;

			uniform sampler2D _MainTex;
			uniform float4 _MainTex_ST;
			uniform float4 _Color;
			sampler2D _BumpMap;
			float4 _BumpMap_ST;
			float _Glossiness;
			float _Metallic;
			float _NormalScale;
			sampler2D _GlossMap;
			float4 _GlossMap_ST;
			float4 _Emission;
			float _UVSplit;
			float _UVIndex;
			float _EnableSkinning;

			UNITY_INSTANCING_BUFFER_START(Props)
				UNITY_DEFINE_INSTANCED_PROP(float, _UVIndexArray)
				UNITY_DEFINE_INSTANCED_PROP(float, _AnimationMatrixIndexArray)
			UNITY_INSTANCING_BUFFER_END(Props)

			inline float2 IndexUV(float2 uv)
			{
				float index = max(_UVIndex, UNITY_ACCESS_INSTANCED_PROP(Props, _UVIndexArray));
				return (uv + float2((index - 1.0) % _UVSplit, floor((index - 1.0) / _UVSplit))) / _UVSplit;
			}

			inline float4 PixelIndexToUV(float pixelIndex)
			{
				float row = floor(pixelIndex / _BakedAnimationTexture_SizeInfo.x);
				float col = pixelIndex - row * _BakedAnimationTexture_SizeInfo.x;
				return float4(col / _BakedAnimationTexture_SizeInfo.x, row / _BakedAnimationTexture_SizeInfo.y, 0, 0);
			}

			inline float4x4 GetMatrix(float startIndex, float boneIndex)
			{
				float pixelIndex = startIndex + boneIndex * _BakedAnimationTexture_SizeInfo.w;
				float4 row0 = tex2Dlod(_BakedAnimationTexture, PixelIndexToUV(pixelIndex));
				float4 row1 = tex2Dlod(_BakedAnimationTexture, PixelIndexToUV(pixelIndex + 1));
				float4 row2 = tex2Dlod(_BakedAnimationTexture, PixelIndexToUV(pixelIndex + 2));
				float4 row3 = float4(0, 0, 0, 1);
				return float4x4(row0, row1, row2, row3);
			}

			v2f vert (appdata v)
			{
				v2f o = (v2f)0;
				o.uv0=v.uv0;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				float4 normal = float4(v.normal, 0);
				float4 tangent = float4(v.tangent.xyz, 0);
				float4 vertex = v.vertex;
				if (_EnableSkinning >= 1)
				{
					float matrixIndex = UNITY_ACCESS_INSTANCED_PROP(Props, _AnimationMatrixIndexArray);
					matrixIndex *= _BakedAnimationTexture_SizeInfo.w;
					float4x4 mat0 = GetMatrix(matrixIndex, v.uv1.x);
					float4x4 mat1 = GetMatrix(matrixIndex, v.uv1.z);
					float weight = 1.0 / (v.uv1.y + v.uv1.w);
					normal = (mul(mat0, normal) * v.uv1.y + mul(mat1, normal) * v.uv1.w) * weight;
					tangent = (mul(mat0, tangent) * v.uv1.y + mul(mat1, tangent) * v.uv1.w) * weight;
					vertex = (mul(mat0, vertex) * v.uv1.y + mul(mat1, vertex) * v.uv1.w) * weight;
				}
				o.normal=mul(normal,(float3x3)unity_WorldToObject);
				o.tangentDir=float4(UnityObjectToWorldDir(tangent.xyz),v.tangent.w);
				o.worldPos=mul(unity_ObjectToWorld, vertex);
				o.pos = UnityObjectToClipPos(vertex);
				UNITY_TRANSFER_FOG(o,o.pos);
				COMPUTE_LIGHT_COORDS(o);
				return o;
			}
			
			fixed4 frag(v2f i) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(i);
				fixed3 worldNormal=normalize(i.normal);
				float3x3 tbn=CreateTangentToWorldPerVertex(worldNormal,i.tangentDir.xyz,i.tangentDir.w);
				half3 lightDir=normalize(_WorldSpaceLightPos0.xyz);
				half3 viewDir = normalize(_WorldSpaceCameraPos.xyz-i.worldPos.xyz);
				float3 BumpMap=UnpackNormal(tex2D(_BumpMap, IndexUV(i.uv0*_BumpMap_ST.xy+_BumpMap_ST.zw)));
				BumpMap.xy*=(_NormalScale);
				BumpMap.z=sqrt(1-saturate(BumpMap.xy*BumpMap.xy));
				fixed3 normalMap=normalize(mul(BumpMap.rgb,tbn));
				float3 R = reflect(-viewDir,normalMap);
				float3 lightColor=_LightColor0.rgb;
				half3 H =normalize(lightDir+viewDir);
				float4 SmoothnessMap=tex2D(_GlossMap, IndexUV(i.uv0*_GlossMap_ST.xy+_GlossMap_ST.zw));
				UnityLight light;
				light.ndotl=saturate(dot(normalMap,lightDir));
				UnityGIInput d;
				d.light=light;
				d.worldPos=i.worldPos.xyz;
				d.ambient = 0;
				d.light.dir=normalize(_WorldSpaceLightPos0.xyz);
				d.light.color=_LightColor0.xyz;
				float3 specularColor;
				float specularMonochrome;
				float4 _MainTex_var=tex2D(_MainTex, IndexUV(i.uv0*_MainTex_ST.xy+_MainTex_ST.zw));
				float3 diffuseColor=(_MainTex_var*_Color.rgb);
				specularColor = lerp (unity_ColorSpaceDielectricSpec.rgb,diffuseColor.rgb,(_Metallic*SmoothnessMap.g));
				specularMonochrome = OneMinusReflectivityFromMetallic(_Metallic*SmoothnessMap.g);
				diffuseColor=diffuseColor*specularMonochrome;
				specularMonochrome=1.0-specularMonochrome;
				UnityIndirect gi2;
				half3 env0=FT_Unity_GlossyEnvironment(UNITY_PASS_TEXCUBE(unity_SpecCube0),unity_SpecCube0_HDR,1-_Glossiness*SmoothnessMap.r,R);
				gi2.diffuse=ShadeSHPerPixel(normalMap, d.ambient, d.worldPos);
				gi2.specular=env0;
				float3 specular = FT_BRDF2_Unity_PBS(diffuseColor,specularColor,1-specularMonochrome,_Glossiness*SmoothnessMap.r,normalMap,viewDir,d.light,gi2,1);
				float3 finalColor=specular;
				UNITY_APPLY_FOG(i.fogCoord, finalColor);
				return fixed4(finalColor+_Emission.rgb,_MainTex_var.a);
			}
			ENDCG
		}
		UsePass "FootBall/shadowCastShader/SHADOWCASTER"
	}
	SubShader
	{
		Blend SrcAlpha OneMinusSrcAlpha
		Tags { "RenderType"="Opaque" "Queue" = "Geometry" "LightMode" = "ForwardBase"}
		LOD 100

		Pass
		{	
			Name "FORWARD_Low"
		
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_fog
			#pragma target 3.0
			#pragma multi_compile_fwdbase
			#pragma multi_compile_instancing
			#pragma fragmentoption ARB_precision_hint_fastest
            #include "UnityCG.cginc"
            #include "AutoLight.cginc"
            #include "Lighting.cginc"
            #include "UnityPBSLighting.cginc"
            #include "UnityStandardBRDF.cginc"
            #include "core.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv0 : TEXCOORD0;
				float4 uv1 : TEXCOORD1;
				float4 uv2 : TEXCOORD2;
				float3 normal:NORMAL;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct v2f
			{
				float2 uv0 : TEXCOORD0;
				float4 uv1 : TEXCOORD1;
				float4 uv2 : TEXCOORD2;
				float3 worldPos:TEXCOORD3;
				float4 pos : SV_POSITION;
				float3 normal:TEXCOORD4;
				float4 tangentDir : TEXCOORD5;
				UNITY_FOG_COORDS(6)
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			uniform sampler2D _BakedAnimationTexture;
			uniform float4 _BakedAnimationTexture_SizeInfo;

			uniform sampler2D _MainTex;
			uniform float4 _MainTex_ST;
			uniform float4 _Color;
			//sampler2D _BumpMap;
			//float4 _BumpMap_ST;
			float _Glossiness;
			//float _Metallic;
			//float _NormalScale;
			//sampler2D _GlossMap;
			//float4 _GlossMap_ST;
			float4 _Emission;
			float _UVSplit;
			float _UVIndex;
			float _EnableSkinning;

			UNITY_INSTANCING_BUFFER_START(Props)
				UNITY_DEFINE_INSTANCED_PROP(float, _UVIndexArray)
				UNITY_DEFINE_INSTANCED_PROP(float, _AnimationMatrixIndexArray)
			UNITY_INSTANCING_BUFFER_END(Props)

			inline float2 IndexUV(float2 uv)
			{
				float index = max(_UVIndex, UNITY_ACCESS_INSTANCED_PROP(Props, _UVIndexArray));
				return (uv + float2((index - 1.0) % _UVSplit, floor((index - 1.0) / _UVSplit))) / _UVSplit;
			}

			inline float4 PixelIndexToUV(float pixelIndex)
			{
				float row = floor(pixelIndex / _BakedAnimationTexture_SizeInfo.x);
				float col = pixelIndex - row * _BakedAnimationTexture_SizeInfo.x;
				return float4(col / _BakedAnimationTexture_SizeInfo.x, row / _BakedAnimationTexture_SizeInfo.y, 0, 0);
			}

			inline float4x4 GetMatrix(float startIndex, float boneIndex)
			{
				float pixelIndex = startIndex + boneIndex * _BakedAnimationTexture_SizeInfo.w;
				float4 row0 = tex2Dlod(_BakedAnimationTexture, PixelIndexToUV(pixelIndex));
				float4 row1 = tex2Dlod(_BakedAnimationTexture, PixelIndexToUV(pixelIndex + 1));
				float4 row2 = tex2Dlod(_BakedAnimationTexture, PixelIndexToUV(pixelIndex + 2));
				float4 row3 = float4(0, 0, 0, 1);
				return float4x4(row0, row1, row2, row3);
			}

			v2f vert (appdata v)
			{
				v2f o = (v2f)0;
				o.uv0=v.uv0;
				o.uv1=v.uv1;
				o.uv2=v.uv2;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				float4 normal = float4(v.normal, 0);
				float4 vertex = v.vertex;
				if (_EnableSkinning >= 1)
				{
					float matrixIndex = UNITY_ACCESS_INSTANCED_PROP(Props, _AnimationMatrixIndexArray);
					matrixIndex *= _BakedAnimationTexture_SizeInfo.w;
					float4x4 mat0 = GetMatrix(matrixIndex, v.uv1.x);
					float4x4 mat1 = GetMatrix(matrixIndex, v.uv1.z);
					float weight = 1.0 / (v.uv1.y + v.uv1.w);
					normal = (mul(mat0, normal) * v.uv1.y + mul(mat1, normal) * v.uv1.w) * weight;
					vertex = (mul(mat0, vertex) * v.uv1.y + mul(mat1, vertex) * v.uv1.w) * weight;
				}
				o.normal = mul(normal, (float3x3)unity_WorldToObject);
				o.worldPos = mul(unity_ObjectToWorld, vertex);
				o.pos = UnityObjectToClipPos(vertex);
				UNITY_TRANSFER_FOG(o,o.pos);
				COMPUTE_LIGHT_COORDS(o);
				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(i);
				fixed3 worldNormal=normalize(i.normal);
				half3 lightDir=normalize(_WorldSpaceLightPos0.xyz);
				half3 viewDir = normalize(_WorldSpaceCameraPos.xyz-i.worldPos.xyz);
				fixed3 normalMap=worldNormal;
				float3 R = reflect(-viewDir,normalMap);
				float3 lightColor=_LightColor0.rgb;
				half3 H =normalize(lightDir+viewDir);
				float viewZ =dot(_WorldSpaceCameraPos - i.worldPos, UNITY_MATRIX_V[2].xyz);
				half3 gi3=FT_ShadeSHPerPixel(normalMap,float3(0,0,0),i.worldPos);
				lightDir=_WorldSpaceLightPos0.xyz;
				half NdotL=saturate(dot(normalMap,lightDir));
				float4 _MainTex_var=tex2D(_MainTex, IndexUV(i.uv0*_MainTex_ST.xy+_MainTex_ST.zw));
				float3 diffuseColor=(_MainTex_var*_Color.rgb);
				half3 env0=FT_Unity_GlossyEnvironment(UNITY_PASS_TEXCUBE(unity_SpecCube0),unity_SpecCube0_HDR,1-_Glossiness,R);
				float3 finalColor=diffuseColor*NdotL*_LightColor0.rgb+gi3*diffuseColor*env0;
				UNITY_APPLY_FOG(i.fogCoord, finalColor);
				return fixed4(finalColor+_Emission.rgb,_MainTex_var.a);
			}
			ENDCG
		}
		UsePass "FootBall/shadowCastShader/SHADOWCASTER"
	}

}
