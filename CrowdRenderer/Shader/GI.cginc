#ifndef UNITY_FT_GI_INCLUDE
#define UNITY_FT_GI_INCLUDE
#include "UnityImageBasedLighting.cginc"
inline fixed4 calculateAmbientLight(half3 normalWorld){
			fixed3 ambient = unity_AmbientSky.rgb*0.75;
			fixed3 worldUp=fixed3(0,1,0);
			float skyGroundDotMul=2.5;
			float minEqueatorMix =0.5;
			float equatorColorBlur=0.33;
			float upDot = dot(normalWorld,worldUp);
			float adjustedDot = upDot*skyGroundDotMul;
			fixed3 skyGroundColor=lerp(unity_AmbientGround,unity_AmbientSky,saturate((adjustedDot+1.0)*0.5));
			float equatorBright=saturate(dot(unity_AmbientEquator.rgb,unity_AmbientEquator.rgb));
			fixed3 equatorBlurredColor= lerp(unity_AmbientEquator,saturate(unity_AmbientEquator+unity_AmbientGround+unity_AmbientSky),equatorBright*equatorColorBlur);
			float smoothDot=pow(abs(upDot),1);
			fixed3 equatorColor=lerp(equatorBlurredColor,unity_AmbientGround,smoothDot)*step(upDot,0)+lerp(equatorBlurredColor,unity_AmbientSky,smoothDot)*step(0,upDot);
			return fixed4(lerp(skyGroundColor,equatorColor,saturate(equatorBright+minEqueatorMix))*0.75,1);
}

inline half3 FT_Unity_GlossyEnvironment (UNITY_ARGS_TEXCUBE(tex), half4 hdr,half roughness,half3 viewReflDir)
{
    half perceptualRoughness = roughness*(1.7 - 0.7*roughness);
    half mip = perceptualRoughnessToMipmapLevel(perceptualRoughness);
    half4 rgbm = UNITY_SAMPLE_TEXCUBE_LOD(tex, viewReflDir, mip);
    return DecodeHDR(rgbm, hdr);
}

inline half3 FT_ShadeSHPerVertex(half3 normal,half3 ambient)
{
    #ifdef UNITY_COLORSPACE_GAMMA
        ambient=GammaToLinearSpace(ambient);
    #endif
    ambient+=SHEvalLinearL2(half4(normal,1));//LPPV & Light Probe Group
    return ambient;
}
half3 FT_ShadeSHPerPixel(half3 normal,half3 ambient,float3 worldPos)
{
    half3 ambient_contrib=0.0;
//  ambient_contrib=SHEvalLinearL0L1(half4(normal,1.0));
    #if UNITY_LIGHT_PROBE_PROXY_VOLUME
        if (unity_ProbeVolumeParams.x == 1.0)
            ambient_contrib = SHEvalLinearL0L1_SampleProbeVolume (half4(normal, 1.0), worldPos);
        else
            ambient_contrib = SHEvalLinearL0L1 (half4(normal, 1.0));
    #else
            ambient_contrib = SHEvalLinearL0L1 (half4(normal, 1.0));
    #endif
    ambient=max(half3(0,0,0),ambient+ambient_contrib); 
    #ifdef UNITY_COLORSPACE_GAMMA
        ambient=LinearToGammaSpace(ambient);
    #endif
    return ambient;
} 


half FT_LerpOneTo(half b, half t)
{
    half oneMinusT = 1 - t;
    return oneMinusT + b * t;
}

#endif //UNITY_FT_GI_INCLUDE