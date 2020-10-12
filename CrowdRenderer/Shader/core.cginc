#ifndef UNITY_FT_CORE_INCLUDE
#define UNITY_FT_CORE_INCLUDE

#include "Aniso.cginc"
#include "GI.cginc"
#include "PBR.cginc"
#include "Common.cginc"
//sampler2D _SSSLut;
float4 _SSSLut_ST;
float _ShadowAlpha;
sampler2D _sssTexMask;
float4 _sssTexMask_ST;
inline fixed4 GetSSS(float _LUTSSS,fixed3 normalMap,half3 worldPos,sampler2D _SSSLut,half3 L,half NoL){
	fixed4 lutSss=fixed4(0,0,0,0);
	
		fixed cur=saturate(_LUTSSS*(length(fwidth(normalMap)))/length(fwidth(worldPos)))/2;
		lutSss=tex2D(_SSSLut,float2(saturate(dot(normalMap,L)*0.5+0.5),cur));


	return lutSss;
}

inline fixed4 GetSSSAtten(float _LUTSSS,fixed3 normalMap,half3 worldPos,sampler2D _SSSLut,half3 L,half atten){
	fixed4 lutSss=fixed4(0,0,0,0);
	
		fixed cur=saturate(_LUTSSS*(length(fwidth(normalMap)))/length(fwidth(worldPos)))/2;
		lutSss=tex2D(_SSSLut,float2(saturate(dot(normalMap,L)*0.5+0.5)*atten,cur));


	return lutSss;
}


inline fixed4 GetSSSGlow(fixed lut,sampler2D _sssTexMask,float2 uv,float _sssSize,half NoH){
	half4 sssMastMap=tex2D(_sssTexMask,uv*_sssTexMask_ST.xy+_sssTexMask_ST.zw);
	fixed4 lutSss=lut+lut*sssMastMap.r*_sssSize*saturate(1-NoH);
	return lutSss;

}


inline fixed3  GetNormal(sampler2D _NormalMap,float2 uv,float _NormalScale,float3 tangent[3]){
	fixed3 normalMap=UnpackNormal(tex2D(_NormalMap,uv));
	normalMap.xy*=(_NormalScale);
	normalMap.z=sqrt(1-saturate(normalMap.xy*normalMap.xy));
	normalMap=normalize(half3(tangent[0]*normalMap.x+tangent[1]*normalMap.y+tangent[2]*normalMap.z));
	//normalMap=normalize(mul(normalMap,tangent[3]));
	return normalMap;
}
inline fixed3  GetNormalTwo(sampler2D _NormalMap,sampler2D _NormalStrick,float2 uv,float2 uv2,float _NormalScale,float3 tangent[3]){
	fixed3 normalMap=UnpackNormal(tex2D(_NormalMap,uv));
	fixed3 normalMap2=UnpackNormal(tex2D(_NormalStrick,uv2));
	normalMap=normalize(half3(normalMap.xy + normalMap2.xy, normalMap.z*normalMap2.z));
	normalMap.xy*=(_NormalScale);
	normalMap.z=sqrt(1-saturate(normalMap.xy*normalMap.xy));
	normalMap=normalize(half3(tangent[0]*normalMap.x+tangent[1]*normalMap.y+tangent[2]*normalMap.z));
	//normalMap=normalize(mul(normalMap,tangent[3]));
	return normalMap;
}
inline float3 SafeNormalize(float3 inVec)
{
    float dp3 = max(0.001f, dot(inVec, inVec));
    return inVec * rsqrt(dp3);
}

inline fixed4 SamplerCubeMap(samplerCUBE cube,half3 R,float Mip,float Roughness,half NoV){
	fixed4 relColor=texCUBElod(cube,half4(R,Roughness*Mip));
	fixed4 finaColor=relColor*pow((1-NoV),Roughness+2)*(1-Roughness);
	return finaColor;

}

inline half3 InvertAoSSS(float3 vLight,float3 vNormal,float fLDistortion,float3 vEye,float iLTPower,
	float fLTScale,float fLightAttenuation,float3 fLTAmbient,float fLTThickness,float3 bloodMap,float3 Bloodcolor){
/*
	half3 vLTLight=vLight+vNormal*fLDistortion;
	half fLTDot=pow(saturate(dot(vEye,-vLTLight)),iLTPower)*fLTScale;
	half3 fLT=fLightAttenuation*(fLTDot+fLTAmbient)*fLTThickness;
	//return fLT*bloodMap.r+(fLT*(1-bloodMap.r)*Bloodcolor);
	half3 outColor=fLT;
*/
	float3 H=normalize(vLight+vNormal*fLDistortion);
	float transDot=pow(saturate(dot(vEye,-H)),iLTPower)*fLTScale*fLTThickness;
	half3 lightScattering=transDot*Bloodcolor;
	return lightScattering;

}

inline float3 RGB2HSV(float3 c){
	float4 k=float4(0.0,-1.0/3.0,2.0/3.0,-1.0);
	float4 p=lerp(float4(c.bg,k.wz),float4(c.gb,k.xy),step(c.b,c.g));
	float4 q=lerp(float4(p.xyw,c.r),float4(c.r,p.yzx),step(p.x,c.r));
	float d=q.x-min(q.w,q.y);
	float e=1.0e-10;
	return float3(abs(q.z+(q.w-q.y)/(6.0*d+e)),d/(q.x+e),q.x);

}

inline float3 HSV2RGB(float3 c){
	float4 k=float4(1.0,2.0/3.0,1.0/3.0,3.0);
	float3 p=abs(frac(c.xxx+k.xyz)*6.0-k.www);
	return c.z*lerp(k.xxx,saturate(p-k.xxx),c.y);

}


float isDithered(float2 pos, float alpha) {
    pos *= _ScreenParams.xy;

    // Define a dither threshold matrix which can
    // be used to define how a 4x4 set of pixels
    // will be dithered
    float DITHER_THRESHOLDS[16] =
    {
        1.0 / 17.0,  9.0 / 17.0,  3.0 / 17.0, 11.0 / 17.0,
        13.0 / 17.0,  5.0 / 17.0, 15.0 / 17.0,  7.0 / 17.0,
        4.0 / 17.0, 12.0 / 17.0,  2.0 / 17.0, 10.0 / 17.0,
        16.0 / 17.0,  8.0 / 17.0, 14.0 / 17.0,  6.0 / 17.0
    };

    int index = (int(pos.x) % 4) * 4 + int(pos.y) %4;
    return alpha - DITHER_THRESHOLDS[index];
}
float isDithered(float2 pos, float alpha, sampler2D tex, float scale,float dis) {
    pos *= _ScreenParams.xy;

    // offset so we're centered
    pos.x -= _ScreenParams.x / 2;
    pos.y -= _ScreenParams.y / 2;
    
    // scale the texture

    pos.x =pos.x/ (scale+dis);
    pos.y =pos.y/ (scale+dis);



	// ensure that we clip if the alpha is zero by
	// subtracting a small value when alpha == 0, because
	// the clip function only clips when < 0
    return alpha - tex2D(tex, pos.xy).r - 0.0001 * (1 - ceil(alpha));
}
void ditherClip(float2 pos, float alpha) {
    clip(isDithered(pos, alpha));
}
void ditherClip(float2 pos, float alpha, sampler2D tex, float scale,float dis) {
    clip(isDithered(pos, alpha, tex, scale,dis));
}



#endif 