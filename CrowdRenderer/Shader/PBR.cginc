#ifndef UNITY_FT_BRDF_INCLUDE
#define UNITY_FT_BRDF_INCLUDE
#include "AutoLight.cginc"
#include "Lighting.cginc"
#define PI 3.14
            float _FabricScatterColor;
            float _FabricScatterScale;

inline half pow4_twn (half x)
{
    return x*x*x*x;
}

inline float PerceptualRoughnessToRough(float perceptualRoughness)
{
    return perceptualRoughness*perceptualRoughness;
}

float SmoothnessToPerceptualRoughness2(float smoothness)
{
    return saturate(1-smoothness);
}


inline half Pow5_twn (half x)
{
    return x*x * x*x * x;
}

half DisneyDiffuse2(half NdotV, half NdotL, half LdotH, half perceptualRoughness)
{
    half fd90 = 0.5 + 2 * LdotH * LdotH * perceptualRoughness;
    // Two schlick fresnel term
    half lightScatter   = (1 + (fd90 - 1) * Pow5_twn(1 - NdotL));
    half viewScatter    = (1 + (fd90 - 1) * Pow5_twn(1 - NdotV));

    return lightScatter * viewScatter;
}


inline float GGXTerm2 (half NdotH, float roughness)
{
    float a2 = roughness * roughness;
    float d = (NdotH * a2 - NdotH) * NdotH + 1.0f; // 2 mad
    return 0.31f * a2 / (d * d + 0.000001f); 
                                            
}
float D_UE_GGX( float NoH,float roughness)
{
    float a2 = roughness*roughness;
    float d = ( NoH * a2 - NoH ) * NoH + 1; // 2 mad
    return a2 / ( 3.1415926f*d*d);                 // 4 mul, 1 rcp
}


inline half SmithJointGGXVisibility (half NdotL, half NdotV, half roughness)
{
#if 0
#else
 
    half a = roughness;
    half lambdaV = NdotL * (NdotV * (1 - a) + a);
    half lambdaL = NdotV * (NdotL * (1 - a) + a);

    return 0.5f / (lambdaV + lambdaL + 0.01f);
#endif
}


inline half PBR_GGX(float _Roughtness,half NoV,half NoL,half NoH,fixed4 RoughtnessMap,float _Frenes){
    _Roughtness=1-_Roughtness+0.04;
    half perceptualRoughness = SmoothnessToPerceptualRoughness2 (_Roughtness*saturate(1-RoughtnessMap.r));
	half roughness =PerceptualRoughnessToRough(perceptualRoughness);
	half a2 =roughness*roughness;
	//-------------------------------
	float d = (NoH * a2 - NoH) * NoH + 1.0f; // 2 mad
    half D= GGXTerm2 (NoH, roughness);
	half G = SmithJointGGXVisibility(NoL,NoV,roughness);
	half F= lerp(pow((1-NoV),5),1,_Frenes);
	half PBR=(D*G*F)*(3.141f);
    //PBR = sqrt(max(0.001f, PBR));
    //half diffuseTerm=DisneyDiffuse2(NoV,NoL,LoH,perceptualRoughness)*NoL;
    PBR = max(0, PBR * NoL);
	return PBR;

}

half FT_SpecularStrength(half3 specular)
{
    #if (SHADER_TARGET < 30)
        // SM2.0: instruction count limitation
        // SM2.0: simplified SpecularStrength
        return specular.r; // Red channel - because most metals are either monocrhome or with redish/yellowish tint
    #else
        return max (max (specular.r, specular.g), specular.b);
    #endif
}

half DisneyDiffuse_SSS(half NdotV, half NdotL, half LdotH, half perceptualRoughness)
{
    half fd90 = 0.5 + 2 * LdotH * LdotH * perceptualRoughness;
    // Two schlick fresnel term
    half lightScatter   = (1 + (fd90 - 1) * Pow5(1 - NdotL));
    half viewScatter    = (1 + (fd90 - 1) * Pow5(1 - NdotV));

    return lightScatter * viewScatter;
}

inline half3 FT_EnergyConservationBetweenDiffuseAndSpecular (half3 albedo, half3 specColor, out half oneMinusReflectivity)
{
    oneMinusReflectivity = 1 - FT_SpecularStrength(specColor);
    #if !UNITY_CONSERVE_ENERGY
        return albedo;
    #elif UNITY_CONSERVE_ENERGY_MONOCHROME
        return albedo * oneMinusReflectivity;
    #else
        return albedo * (half3(1,1,1) - specColor);
    #endif
}

inline half3 FT_DiffuseAndSpecularFromMetallic (half3 albedo, half metallic, out half3 specColor, out half oneMinusReflectivity)
{
    specColor = lerp (unity_ColorSpaceDielectricSpec.rgb, albedo, metallic);
    oneMinusReflectivity = OneMinusReflectivityFromMetallic(metallic);
    return albedo * oneMinusReflectivity;
}

//-------------------------------------------------------------UE4 Anisotropy Start 
float3 EncodeTangentAndAnisotropy(half3 WorldTangent, float Anisotropy)
{
float TangentXYLength = length(WorldTangent.xy);
float ZAngle = atan2(WorldTangent.y, WorldTangent.x); // -PI <= ZAngle <= PI
ZAngle = ZAngle / PI *0.5f + 0.5f; // -> 0 <= ZAngle <= 1.0
float ZSign = WorldTangent.z >= 0.0f ? 0.0f : 128.0f;
float ZSign_and_Anisotropy = (ZSign + (Anisotropy * 127.0f)) / 255.0f;
return float3(TangentXYLength, ZAngle, ZSign_and_Anisotropy);
}
void DecodeTangentAndAnisotropy(out float3 WorldTangent, out float Anisotropy, float3 CustomData)
{
    // Decompress WorldTanget and Anisotropy by polar coordination.
    float polarLength = CustomData.x;
    float polarAngle = (CustomData.y * 2.0f - 1.0f) * 3.1415926;
    float s, c;
    sincos(polarAngle, s, c);
    float2 txy = float2(c, s) * polarLength;
    float zSign = CustomData.z >= (127.5f / 255.0f) ? -1.0f : 1.0f;
    WorldTangent = float3(txy, zSign * sqrt(max(1.0f - dot(txy.xy, txy.xy), 0.0f)));

    Anisotropy = (zSign < 0.0f) ? CustomData.z - 0.5f : CustomData.z;
    Anisotropy = Anisotropy * 255.0f / 127.0f;
}


float D_GGXaniso( float RoughnessX, float RoughnessY, float NoH, float3 H, float3 X, float3 Y )
{
    float ax = RoughnessX * RoughnessX;
    float ay = RoughnessY * RoughnessY;
    float XoH = dot( X, H );
    float YoH = dot( Y, H );
    float d = XoH*XoH / (ax*ax) + YoH*YoH / (ay*ay) + NoH*NoH;
    return 1 / ( PI * ax*ay * d*d );
}

/*
float3 GGXAnisoShading( FGBufferData GBuffer, float3 LobeRoughness, float3 LobeEnergy, float3 L, float3 V, half3 N, float2 DiffSpecMask )
{
float3 H = normalize(V + L);
float NoL = saturate( dot(N, L) );
float NoV = max( dot(N, V), 1e-5 );
float NoH = saturate( dot(N, H) );
float VoH = saturate( dot(V, H) );
float3 WorldTangent;
float Anisotropy;
DecodeTangentAndAnisotropy(WorldTangent, Anisotropy, GBuffer.CustomData);
float3 B = normalize(cross(N, WorldTangent));
float3 T = normalize(cross(B, N));

// Generalized microfacet specular
float aspect = sqrt(1.0f - Anisotropy * 0.9f);
float anisoXRoughness = max(0.01f, LobeRoughness[1] / aspect);
float anisoYRoughness = max(0.01f, LobeRoughness[1] * aspect);
float D = D_GGXaniso( anisoXRoughness, anisoYRoughness, NoH, H, T, B ) * LobeEnergy[1];
float Vis = Vis_SmithJointApprox( LobeRoughness[1], NoV, NoL );
float3 F = F_Schlick( GBuffer.SpecularColor, VoH );

float3 Diffuse = Diffuse_Lambert( GBuffer.DiffuseColor );

return Diffuse * (LobeEnergy[2] * DiffSpecMask.r) + (D * Vis * DiffSpecMask.g) * F;
}
*/
float D_InvGGX(  float NoH ,float Roughness)
{
    float a = Roughness * Roughness;
    float a2 = a * a;
    float A = 4;
    float d = ( NoH - a2 * NoH ) * NoH + a2;
    return rcp( PI * (1 + A*a2) ) * ( 1 + 4 * a2*a2 / ( d*d ) );
}

float3 F_Schlick( float3 SpecularColor, float VoH )
{
    float Fc = Pow5( 1 - VoH );                 // 1 sub, 3 mul
    //return Fc + (1 - Fc) * SpecularColor;     // 1 add, 3 mad
    
    // Anything less than 2% is physically impossible and is instead considered to be shadowing
    return saturate( 50.0 * SpecularColor.g ) * Fc + (1 - Fc) * SpecularColor;
    
}
float Vis_Cloth( float NoV, float NoL )
{
    return rcp( 4 * ( NoL + NoV - NoL * NoV ) );
}
//------------------------------------------------UE Anisotropy End

half4 FT_BRDF2_Unity_PBS (half3 diffColor, half3 specColor, half oneMinusReflectivity, half smoothness,
    float3 normal, float3 viewDir,
    UnityLight light, UnityIndirect gi, half atten)
{
    float3 halfDir = Unity_SafeNormalize (float3(light.dir) + viewDir);

    half nl = saturate(dot(normal, light.dir)) * atten;
    float nh = saturate(dot(normal, halfDir));
    half nv = saturate(dot(normal, viewDir));
    float lh = saturate(dot(light.dir, halfDir));

    // Specular term
    half perceptualRoughness = SmoothnessToPerceptualRoughness (smoothness);
    half roughness = PerceptualRoughnessToRoughness(perceptualRoughness);

#if UNITY_BRDF_GGX

    // GGX Distribution multiplied by combined approximation of Visibility and Fresnel
    // See "Optimizing PBR for Mobile" from Siggraph 2015 moving mobile graphics course
    // https://community.arm.com/events/1155
    half a = roughness;
    float a2 = a*a;

    float d = nh * nh * (a2 - 1.f) + 1.00001f;
#ifdef UNITY_COLORSPACE_GAMMA
    // Tighter approximation for Gamma only rendering mode!
    // DVF = sqrt(DVF);
    // DVF = (a * sqrt(.25)) / (max(sqrt(0.1), lh)*sqrt(roughness + .5) * d);
    float specularTerm = a / (max(0.32f, lh) * (1.5f + roughness) * d);
#else
    float specularTerm = a2 / (max(0.1f, lh*lh) * (roughness + 0.5f) * (d * d) * 4);
#endif

    // on mobiles (where half actually means something) denominator have risk of overflow
    // clamp below was added specifically to "fix" that, but dx compiler (we convert bytecode to metal/gles)
    // sees that specularTerm have only non-negative terms, so it skips max(0,..) in clamp (leaving only min(100,...))
#if defined (SHADER_API_MOBILE)
    specularTerm = specularTerm - 1e-4f;
#endif

#else

    // Legacy
    half specularPower = PerceptualRoughnessToSpecPower(perceptualRoughness);
    // Modified with approximate Visibility function that takes roughness into account
    // Original ((n+1)*N.H^n) / (8*Pi * L.H^3) didn't take into account roughness
    // and produced extremely bright specular at grazing angles

    half invV = lh * lh * smoothness + perceptualRoughness * perceptualRoughness; // approx ModifiedKelemenVisibilityTerm(lh, perceptualRoughness);
    half invF = lh;

    half specularTerm = ((specularPower + 1) * pow (nh, specularPower)) / (8 * invV * invF + 1e-4h);

#ifdef UNITY_COLORSPACE_GAMMA
    specularTerm = sqrt(max(1e-4f, specularTerm));
#endif

#endif

#if defined (SHADER_API_MOBILE)
    specularTerm = clamp(specularTerm, 0.0, 100.0); // Prevent FP16 overflow on mobiles
#endif
#if defined(_SPECULARHIGHLIGHTS_OFF)
    specularTerm = 0.0;
#endif

    // surfaceReduction = Int D(NdotH) * NdotH * Id(NdotL>0) dH = 1/(realRoughness^2+1)

    // 1-0.28*x^3 as approximation for (1/(x^4+1))^(1/2.2) on the domain [0;1]
    // 1-x^3*(0.6-0.08*x)   approximation for 1/(x^4+1)
#ifdef UNITY_COLORSPACE_GAMMA
    half surfaceReduction = 0.28;
#else
    half surfaceReduction = (0.6-0.08*perceptualRoughness);
#endif

    surfaceReduction = 1.0 - roughness*perceptualRoughness*surfaceReduction;

    half grazingTerm = saturate(smoothness + (1-oneMinusReflectivity));
    half3 color =   (diffColor + specularTerm * specColor) * light.color * nl
                    + gi.diffuse * diffColor
                    + surfaceReduction * gi.specular * FresnelLerpFast (specColor, grazingTerm, nv);

    return half4(color, 1);
}

inline float FabricD(float NdotH,float roughness){
    return 0.96*pow(1-NdotH,2)+0.057;
}
inline half FabricScatterFresnelLerp(half nv,half scale){
    half t0=pow4_twn(1-nv);
    half t1=0.4*(1-nv);
    return (t1-t0)*scale+t0;
}

half4 FT_BRDF_Unity_Cloth_PBS (half3 diffColor, half3 specColor, half oneMinusReflectivity, half smoothness,
    float3 normal, float3 viewDir,
    UnityLight light, UnityIndirect gi,float atten,half BackMaxIntensity)
{
    float perceptualRoughness = SmoothnessToPerceptualRoughness (smoothness);
    float3 halfDir = Unity_SafeNormalize (float3(light.dir) + viewDir);

// NdotV should not be negative for visible pixels, but it can happen due to perspective projection and normal mapping
// In this case normal should be modified to become valid (i.e facing camera) and not cause weird artifacts.
// but this operation adds few ALU and users may not want it. Alternative is to simply take the abs of NdotV (less correct but works too).
// Following define allow to control this. Set it to 0 if ALU is critical on your platform.
// This correction is interesting for GGX with SmithJoint visibility function because artifacts are more visible in this case due to highlight edge of rough surface
// Edit: Disable this code by default for now as it is not compatible with two sided lighting used in SpeedTree.
#define UNITY_HANDLE_CORRECTLY_NEGATIVE_NDOTV 0

#if UNITY_HANDLE_CORRECTLY_NEGATIVE_NDOTV
    // The amount we shift the normal toward the view vector is defined by the dot product.
    half shiftAmount = dot(normal, viewDir);
    normal = shiftAmount < 0.0f ? normal + viewDir * (-shiftAmount + 1e-5f) : normal;
    // A re-normalization should be applied here but as the shift is small we don't do it to save ALU.
    //normal = normalize(normal);

    half nv = saturate(dot(normal, viewDir)); // TODO: this saturate should no be necessary here
#else
    half nv = abs(dot(normal, viewDir));    // This abs allow to limit artifact
#endif

    half nl = saturate(dot(normal, light.dir));
    float nh = saturate(dot(normal, halfDir));

    half lv = saturate(dot(light.dir, viewDir));
    half lh = saturate(dot(light.dir, halfDir));

    // Diffuse term
    half diffuseTerm = DisneyDiffuse(nv, nl, lh, perceptualRoughness) * nl;

    // Specular term
    // HACK: theoretically we should divide diffuseTerm by Pi and not multiply specularTerm!
    // BUT 1) that will make shader look significantly darker than Legacy ones
    // and 2) on engine side "Non-important" lights have to be divided by Pi too in cases when they are injected into ambient SH
    float roughness = PerceptualRoughnessToRoughness(perceptualRoughness);
#if UNITY_BRDF_GGX
    // GGX with roughtness to 0 would mean no specular at all, using max(roughness, 0.002) here to match HDrenderloop roughtness remapping.
    roughness = max(roughness, 0.002);
    half V = SmithJointGGXVisibilityTerm (nl, nv, roughness);
    //float D = GGXTerm (nh, roughness);
    float D=FabricD(nh,roughness);
#else
    // Legacy
    half V = SmithBeckmannVisibilityTerm (nl, nv, roughness);
    float D=FabricD(nh,roughness);
    //half D = NDFBlinnPhongNormalizedTerm (nh, PerceptualRoughnessToSpecPower(perceptualRoughness));
#endif

    half specularTerm = V*D * UNITY_PI; // Torrance-Sparrow model, Fresnel is applied later

#   ifdef UNITY_COLORSPACE_GAMMA
        specularTerm = sqrt(max(1e-4h, specularTerm));
#   endif

    // specularTerm * nl can be NaN on Metal in some cases, use max() to make sure it's a sane value
    specularTerm = max(0, specularTerm * nl);
#if defined(_SPECULARHIGHLIGHTS_OFF)
    specularTerm = 0.0;
#endif

    // surfaceReduction = Int D(NdotH) * NdotH * Id(NdotL>0) dH = 1/(roughness^2+1)
    half surfaceReduction;
#   ifdef UNITY_COLORSPACE_GAMMA
        surfaceReduction = 1.0-0.28*roughness*perceptualRoughness;      // 1-0.28*x^3 as approximation for (1/(x^4+1))^(1/2.2) on the domain [0;1]
#   else
        surfaceReduction = 1.0 / (roughness*roughness + 1.0);           // fade \in [0.5;1]
#   endif

    // To provide true Lambert lighting, we need to be able to kill specular completely.
    specularTerm *= any(specColor) ? 1.0 : 0.0;

    half grazingTerm = saturate(smoothness + (1-oneMinusReflectivity));
    half3 color =   diffColor * (gi.diffuse + light.color * diffuseTerm)
                    + specularTerm * light.color * FresnelTerm (specColor, lh)
                    //+ surfaceReduction * gi.specular * FresnelLerp (specColor, grazingTerm, nv);
                    +_FabricScatterColor*(nl*0.5+0.5)*FabricScatterFresnelLerp(nv,_FabricScatterScale);

    return half4(color, 1);
}

half4 FT_BRDF_Unity_Cloth_UEAniso_PBS (half3 diffColor, half3 specColor, half oneMinusReflectivity, half smoothness,
    float3 normal,float3 worldTangent,float3 binNormal,float Anisotropy,float Rim,float3 viewDir,
    UnityLight light, UnityIndirect gi,float atten,half BackMaxIntensity,float FabricScatterColor,float FabricScatterScale)
{
    float perceptualRoughness = SmoothnessToPerceptualRoughness (smoothness);
    float3 halfDir = Unity_SafeNormalize (float3(light.dir) + viewDir);

// NdotV should not be negative for visible pixels, but it can happen due to perspective projection and normal mapping
// In this case normal should be modified to become valid (i.e facing camera) and not cause weird artifacts.
// but this operation adds few ALU and users may not want it. Alternative is to simply take the abs of NdotV (less correct but works too).
// Following define allow to control this. Set it to 0 if ALU is critical on your platform.
// This correction is interesting for GGX with SmithJoint visibility function because artifacts are more visible in this case due to highlight edge of rough surface
// Edit: Disable this code by default for now as it is not compatible with two sided lighting used in SpeedTree.
#define UNITY_HANDLE_CORRECTLY_NEGATIVE_NDOTV 0

#if UNITY_HANDLE_CORRECTLY_NEGATIVE_NDOTV
    // The amount we shift the normal toward the view vector is defined by the dot product.
    half shiftAmount = dot(normal, viewDir);
    normal = shiftAmount < 0.0f ? normal + viewDir * (-shiftAmount + 1e-5f) : normal;
    // A re-normalization should be applied here but as the shift is small we don't do it to save ALU.
    //normal = normalize(normal);

    half nv = saturate(dot(normal, viewDir)); // TODO: this saturate should no be necessary here
#else
    half nv = abs(dot(normal, viewDir));    // This abs allow to limit artifact
#endif

    half nl = saturate(dot(normal, light.dir));
    float nh = saturate(dot(normal, halfDir));
    half lh = saturate(dot(light.dir, halfDir));
    float NoL = dot(normal, light.dir);
    float NoV = dot(normal, viewDir);
    float LoV = dot(light.dir, viewDir);
    float InvLenH = rsqrt( 2 + 2 * LoV );
    float NoH = saturate( ( NoL + NoV ) * InvLenH );
    float VoH = saturate( InvLenH + InvLenH * LoV );
    NoL = saturate(NoL);
    NoV = saturate(abs(NoV) + 1e-5);

    // Diffuse term
    half diffuseTerm = DisneyDiffuse(nv, nl, lh, perceptualRoughness) * saturate(nl);

    half aspect = sqrt(1.0f - Anisotropy * 0.99f);
    half anisoXRoughness = max(0.01f, perceptualRoughness / aspect);
    half anisoYRoughness = max(0.01f, perceptualRoughness * aspect);
    
    // Specular term
    // HACK: theoretically we should divide diffuseTerm by Pi and not multiply specularTerm!
    // BUT 1) that will make shader look significantly darker than Legacy ones
    // and 2) on engine side "Non-important" lights have to be divided by Pi too in cases when they are injected into ambient SH
    float roughness = PerceptualRoughnessToRoughness(perceptualRoughness);
#if UNITY_BRDF_GGX
    // GGX with roughtness to 0 would mean no specular at all, using max(roughness, 0.002) here to match HDrenderloop roughtness remapping.
    roughness = max(roughness, 0.002);
    //half V = SmithJointGGXVisibilityTerm (nl, nv, roughness);
    half V = Vis_Cloth(NoV,NoL);
    //float D = GGXTerm (nh, roughness);
    //float D = pow(nh,128)*0.1;
    //float D=FabricD(nh,roughness);
    float D =D_GGXaniso(anisoXRoughness,anisoYRoughness,nh,halfDir,worldTangent,binNormal)*pow(NoV,5)*4;
    float D_Inv=D_InvGGX(NoH,roughness);
    //float D=GGXTerm(NoH,roughness);
#else
    // Legacy
    //half V = SmithBeckmannVisibilityTerm (nl, nv, roughness);
    //half V = Vis_Cloth(nv,nl);
    //float D=FabricD(nh,roughness);
    //half D = NDFBlinnPhongNormalizedTerm (nh, PerceptualRoughnessToSpecPower(perceptualRoughness));
    //float D= D_GGXaniso(anisoXRoughness,anisoYRoughness,NdotH,halfDir,worldTangent,binNormal);
    //float D=D_InvGGX(roughness,nh);
#endif
    //float F = F_Schlick(specColor,VoH);
    float Fc = pow( 1 - VoH, 5);                           // 1 sub, 3 mul
    float3 F = Fc + (1 - Fc) * specColor;
    //float F = pow(NoV,5)*specColor;
    //half3 specularTerm = V*D*F; // Torrance-Sparrow model, Fresnel is applied later
    //half3 specularTerm=D*V*F;
    //float VxD= roughness > 0.99 ? 1 * FabricD (nh, roughness) : V * D;
    float VxD=  V * D;
    //float VxD= roughness < 0.1 ? V*D_Inv : V * D_Inv;
    //float VxD= 1 * FabricD (NoH, roughness) ;

    half3 specularTerm = VxD * UNITY_PI*F;//+(pow(NoV,Rim)*specColor)*Anisotropy;

#   ifdef UNITY_COLORSPACE_GAMMA
        specularTerm = sqrt(max(1e-4h, specularTerm));
#   endif

    // specularTerm * nl can be NaN on Metal in some cases, use max() to make sure it's a sane value
    specularTerm = max(0, specularTerm * nl);
#if defined(_SPECULARHIGHLIGHTS_OFF)
    specularTerm = 0.0;
#endif

    // surfaceReduction = Int D(NdotH) * NdotH * Id(NdotL>0) dH = 1/(roughness^2+1)
    half surfaceReduction;
#   ifdef UNITY_COLORSPACE_GAMMA
        surfaceReduction = 1.0-0.28*roughness*perceptualRoughness;      // 1-0.28*x^3 as approximation for (1/(x^4+1))^(1/2.2) on the domain [0;1]
#   else
        surfaceReduction = 1.0 / (roughness*roughness + 1.0);           // fade \in [0.5;1]
#   endif

    // To provide true Lambert lighting, we need to be able to kill specular completely.
    //specularTerm *= any(specColor) ? 1.0 : 0.0;

    half grazingTerm = saturate(smoothness + (1-oneMinusReflectivity));
    half3 color =   diffColor * (gi.diffuse + light.color * diffuseTerm*atten)
                    + specularTerm * light.color*atten
                    //+ surfaceReduction * gi.specular * FresnelLerp (specColor, grazingTerm, nv);
                    +FabricScatterColor*(nl*0.5+0.5)*FabricScatterFresnelLerp(nv,FabricScatterScale)
                    +surfaceReduction * gi.specular * FresnelLerp (specColor, grazingTerm, nv)
                    //+ specularTerm
                    ;

    return half4(color, 1);
}


half4 FT_BRDF1_Unity_PBS (half3 diffColor, half3 specColor, half oneMinusReflectivity, half smoothness,
    float3 normal, float3 viewDir,
    UnityLight light, UnityIndirect gi,float atten,half BackMaxIntensity)
{
    float perceptualRoughness = SmoothnessToPerceptualRoughness (smoothness);
    float3 halfDir = Unity_SafeNormalize (float3(light.dir) + viewDir);

// NdotV should not be negative for visible pixels, but it can happen due to perspective projection and normal mapping
// In this case normal should be modified to become valid (i.e facing camera) and not cause weird artifacts.
// but this operation adds few ALU and users may not want it. Alternative is to simply take the abs of NdotV (less correct but works too).
// Following define allow to control this. Set it to 0 if ALU is critical on your platform.
// This correction is interesting for GGX with SmithJoint visibility function because artifacts are more visible in this case due to highlight edge of rough surface
// Edit: Disable this code by default for now as it is not compatible with two sided lighting used in SpeedTree.
#define UNITY_HANDLE_CORRECTLY_NEGATIVE_NDOTV 0

#if UNITY_HANDLE_CORRECTLY_NEGATIVE_NDOTV
    // The amount we shift the normal toward the view vector is defined by the dot product.
    half shiftAmount = dot(normal, viewDir);
    normal = shiftAmount < 0.0f ? normal + viewDir * (-shiftAmount + 1e-5f) : normal;
    // A re-normalization should be applied here but as the shift is small we don't do it to save ALU.
    //normal = normalize(normal);

    half nv = saturate(dot(normal, viewDir)); // TODO: this saturate should no be necessary here
#else
    half nv = abs(dot(normal, viewDir));    // This abs allow to limit artifact
#endif

    half nl = saturate(dot(normal, light.dir)*atten);
    half sd=lerp(BackMaxIntensity,0,dot(light.dir,viewDir)*0.5+0.5);
    float nh = saturate(dot(normal, halfDir));

    half lv = saturate(dot(light.dir, viewDir));
    half lh = saturate(dot(light.dir, halfDir));

    // Diffuse term
    half diffuseTerm = DisneyDiffuse(nv, nl, lh, perceptualRoughness) * nl;

    // Specular term
    // HACK: theoretically we should divide diffuseTerm by Pi and not multiply specularTerm!
    // BUT 1) that will make shader look significantly darker than Legacy ones
    // and 2) on engine side "Non-important" lights have to be divided by Pi too in cases when they are injected into ambient SH
    float roughness = PerceptualRoughnessToRoughness(perceptualRoughness);
#if UNITY_BRDF_GGX
    // GGX with roughtness to 0 would mean no specular at all, using max(roughness, 0.002) here to match HDrenderloop roughtness remapping.
    roughness = max(roughness, 0.002);
    half V = SmithJointGGXVisibilityTerm (nl, nv, roughness);
    float D = GGXTerm (nh, roughness);
#else
    // Legacy
    half V = SmithBeckmannVisibilityTerm (nl, nv, roughness);
    half D = NDFBlinnPhongNormalizedTerm (nh, PerceptualRoughnessToSpecPower(perceptualRoughness));
#endif

    half specularTerm = V*D * UNITY_PI; // Torrance-Sparrow model, Fresnel is applied later

#   ifdef UNITY_COLORSPACE_GAMMA
        specularTerm = sqrt(max(1e-4h, specularTerm));
#   endif

    // specularTerm * nl can be NaN on Metal in some cases, use max() to make sure it's a sane value
    specularTerm = max(0, specularTerm * nl);
#if defined(_SPECULARHIGHLIGHTS_OFF)
    //specularTerm = 0.0;
#endif

    // surfaceReduction = Int D(NdotH) * NdotH * Id(NdotL>0) dH = 1/(roughness^2+1)
    half surfaceReduction;
#   ifdef UNITY_COLORSPACE_GAMMA
        surfaceReduction = 1.0-0.28*roughness*perceptualRoughness;      // 1-0.28*x^3 as approximation for (1/(x^4+1))^(1/2.2) on the domain [0;1]
#   else
        surfaceReduction = 1.0 / (roughness*roughness + 1.0);           // fade \in [0.5;1]
#   endif

    // To provide true Lambert lighting, we need to be able to kill specular completely.
    specularTerm *= any(specColor) ? 1.0 : 0.0;

    half grazingTerm = saturate(smoothness + (1-oneMinusReflectivity));
    half3 color =   diffColor * (gi.diffuse + light.color * diffuseTerm+sd)
                    + specularTerm * light.color * FresnelTerm (specColor, lh)
                    + surfaceReduction * gi.specular * FresnelLerp (specColor, grazingTerm, nv);

    return half4(color, 1);
}




half4 FT_BRDF_SSS_Unity_PBS (half3 diffColor, half3 specColor, half oneMinusReflectivity, half smoothness,
    float3 normal, float3 viewDir,
    UnityLight light, UnityIndirect gi,float3 lut,half BackMaxInstensity,half SpecMask)
{
    float perceptualRoughness = SmoothnessToPerceptualRoughness (smoothness);
    float3 halfDir = Unity_SafeNormalize (float3(light.dir) + viewDir);

// NdotV should not be negative for visible pixels, but it can happen due to perspective projection and normal mapping
// In this case normal should be modified to become valid (i.e facing camera) and not cause weird artifacts.
// but this operation adds few ALU and users may not want it. Alternative is to simply take the abs of NdotV (less correct but works too).
// Following define allow to control this. Set it to 0 if ALU is critical on your platform.
// This correction is interesting for GGX with SmithJoint visibility function because artifacts are more visible in this case due to highlight edge of rough surface
// Edit: Disable this code by default for now as it is not compatible with two sided lighting used in SpeedTree.
#define UNITY_HANDLE_CORRECTLY_NEGATIVE_NDOTV 0

#if UNITY_HANDLE_CORRECTLY_NEGATIVE_NDOTV
    // The amount we shift the normal toward the view vector is defined by the dot product.
    half shiftAmount = dot(normal, viewDir);
    normal = shiftAmount < 0.0f ? normal + viewDir * (-shiftAmount + 1e-5f) : normal;
    // A re-normalization should be applied here but as the shift is small we don't do it to save ALU.
    //normal = normalize(normal);

    half nv = saturate(dot(normal, viewDir)); // TODO: this saturate should no be necessary here
#else
    half nv = abs(dot(normal, viewDir));    // This abs allow to limit artifact
#endif

    half nl = saturate(dot(normal, light.dir));

    half sd=lerp(BackMaxInstensity,-0.1,dot(light.dir,viewDir)*0.5+0.5);
    
    float nh = saturate(dot(normal, halfDir));

    half lv = saturate(dot(light.dir, viewDir));
    half lh = saturate(dot(light.dir, halfDir));

    // Diffuse term
    half diffuseTerm = DisneyDiffuse_SSS(nv, nl, lh, perceptualRoughness) * nl;


    // Specular term
    // HACK: theoretically we should divide diffuseTerm by Pi and not multiply specularTerm!
    // BUT 1) that will make shader look significantly darker than Legacy ones
    // and 2) on engine side "Non-important" lights have to be divided by Pi too in cases when they are injected into ambient SH
    float roughness = PerceptualRoughnessToRoughness(perceptualRoughness);
#if UNITY_BRDF_GGX
    // GGX with roughtness to 0 would mean no specular at all, using max(roughness, 0.002) here to match HDrenderloop roughtness remapping.
    roughness = max(roughness, 0.002);
    half V = SmithJointGGXVisibilityTerm (nl, nv, roughness);
    float D = GGXTerm (nh, roughness);
#else
    // Legacy
    half V = SmithBeckmannVisibilityTerm (nl, nv, roughness);
    half D = NDFBlinnPhongNormalizedTerm (nh, PerceptualRoughnessToSpecPower(perceptualRoughness));
#endif

    half specularTerm = V*D * UNITY_PI; // Torrance-Sparrow model, Fresnel is applied later

#   ifdef UNITY_COLORSPACE_GAMMA
        specularTerm = sqrt(max(1e-4h, specularTerm));
#   endif

    // specularTerm * nl can be NaN on Metal in some cases, use max() to make sure it's a sane value
    specularTerm = max(0, specularTerm * nl);
#if defined(_SPECULARHIGHLIGHTS_OFF)
    specularTerm = 0.0;
#endif

    // surfaceReduction = Int D(NdotH) * NdotH * Id(NdotL>0) dH = 1/(roughness^2+1)
    half surfaceReduction;

    surfaceReduction = 1.0 / (roughness*roughness + 1.0);           // fade \in [0.5;1]


    // To provide true Lambert lighting, we need to be able to kill specular completely.
    specularTerm *= any(specColor) ? 1.0 : 0.0;

    half grazingTerm = saturate(smoothness + (1-oneMinusReflectivity));
    half3 color =   diffColor * (gi.diffuse + light.color * lut+sd)
                    + specularTerm * light.color * FresnelTerm (specColor, lh)*SpecMask
                    + surfaceReduction * gi.specular * FresnelLerp (specColor, grazingTerm, nv);

    return half4(color, 1);
}



half4 FT_BRDF1_Unity_Scene_PBS (half3 diffColor, half3 specColor, half oneMinusReflectivity, half smoothness,
    float3 normal, float3 viewDir,
    UnityLight light, UnityIndirect gi,half atten)
{
    float perceptualRoughness = SmoothnessToPerceptualRoughness (smoothness);
    float3 halfDir = Unity_SafeNormalize (float3(light.dir) + viewDir);

// NdotV should not be negative for visible pixels, but it can happen due to perspective projection and normal mapping
// In this case normal should be modified to become valid (i.e facing camera) and not cause weird artifacts.
// but this operation adds few ALU and users may not want it. Alternative is to simply take the abs of NdotV (less correct but works too).
// Following define allow to control this. Set it to 0 if ALU is critical on your platform.
// This correction is interesting for GGX with SmithJoint visibility function because artifacts are more visible in this case due to highlight edge of rough surface
// Edit: Disable this code by default for now as it is not compatible with two sided lighting used in SpeedTree.
#define UNITY_HANDLE_CORRECTLY_NEGATIVE_NDOTV 0

#if UNITY_HANDLE_CORRECTLY_NEGATIVE_NDOTV
    // The amount we shift the normal toward the view vector is defined by the dot product.
    half shiftAmount = dot(normal, viewDir);
    normal = shiftAmount < 0.0f ? normal + viewDir * (-shiftAmount + 1e-5f) : normal;
    // A re-normalization should be applied here but as the shift is small we don't do it to save ALU.
    //normal = normalize(normal);

    float nv = saturate(dot(normal, viewDir)); // TODO: this saturate should no be necessary here
#else
    half nv = abs(dot(normal, viewDir));    // This abs allow to limit artifact
#endif

    float nl = saturate(dot(normal, light.dir))*atten;
    float nh = saturate(dot(normal, halfDir));

    half lv = saturate(dot(light.dir, viewDir));
    half lh = saturate(dot(light.dir, halfDir));

    // Diffuse term
    half diffuseTerm = DisneyDiffuse(nv, nl, lh, perceptualRoughness) * nl;

    // Specular term
    // HACK: theoretically we should divide diffuseTerm by Pi and not multiply specularTerm!
    // BUT 1) that will make shader look significantly darker than Legacy ones
    // and 2) on engine side "Non-important" lights have to be divided by Pi too in cases when they are injected into ambient SH
    float roughness = PerceptualRoughnessToRoughness(perceptualRoughness);
#if UNITY_BRDF_GGX
    // GGX with roughtness to 0 would mean no specular at all, using max(roughness, 0.002) here to match HDrenderloop roughtness remapping.
    roughness = max(roughness, 0.002);
    float V = SmithJointGGXVisibilityTerm (nl, nv, roughness);
    float D = GGXTerm (nh, roughness);
#else
    // Legacy
    half V = SmithBeckmannVisibilityTerm (nl, nv, roughness);
    half D = NDFBlinnPhongNormalizedTerm (nh, PerceptualRoughnessToSpecPower(perceptualRoughness));
#endif

    float specularTerm = V*D * UNITY_PI; // Torrance-Sparrow model, Fresnel is applied later

#   ifdef UNITY_COLORSPACE_GAMMA
        specularTerm = sqrt(max(1e-4h, specularTerm));
#   endif

    // specularTerm * nl can be NaN on Metal in some cases, use max() to make sure it's a sane value
    specularTerm = max(0, specularTerm * nl);
#if defined(_SPECULARHIGHLIGHTS_OFF)
    specularTerm = 0.0;
#endif

    // surfaceReduction = Int D(NdotH) * NdotH * Id(NdotL>0) dH = 1/(roughness^2+1)
    half surfaceReduction;

    #if defined(LIGHTMAP_ON)
        surfaceReduction =1.0 / (roughness*roughness + 1.0);  
    #else
        surfaceReduction =1.0 / (roughness*roughness + 1.0);  
    #endif
    // To provide true Lambert lighting, we need to be able to kill specular completely.
    specularTerm *= any(specColor) ? 1.0 : 0.0;

    half grazingTerm = saturate(smoothness + (1-oneMinusReflectivity));
    half3 color =   diffuseTerm*diffColor* light.color
                    +diffColor * (gi.diffuse)
                    + specularTerm * light.color * FresnelTerm (specColor, lh)
                    + surfaceReduction * gi.specular * FresnelLerp (specColor, grazingTerm, nv);

    return half4(color, 1);
}




half4 FT_BRDF1_Unity_Scene_NoPBS (half3 diffColor, half3 specColor, half oneMinusReflectivity, half smoothness,
    float3 normal, float3 viewDir,
    UnityLight light, UnityIndirect gi,half atten)
{
    float perceptualRoughness = SmoothnessToPerceptualRoughness (smoothness);
    float3 halfDir = Unity_SafeNormalize (float3(light.dir) + viewDir);

// NdotV should not be negative for visible pixels, but it can happen due to perspective projection and normal mapping
// In this case normal should be modified to become valid (i.e facing camera) and not cause weird artifacts.
// but this operation adds few ALU and users may not want it. Alternative is to simply take the abs of NdotV (less correct but works too).
// Following define allow to control this. Set it to 0 if ALU is critical on your platform.
// This correction is interesting for GGX with SmithJoint visibility function because artifacts are more visible in this case due to highlight edge of rough surface
// Edit: Disable this code by default for now as it is not compatible with two sided lighting used in SpeedTree.
#define UNITY_HANDLE_CORRECTLY_NEGATIVE_NDOTV 0

#if UNITY_HANDLE_CORRECTLY_NEGATIVE_NDOTV
    // The amount we shift the normal toward the view vector is defined by the dot product.
    half shiftAmount = dot(normal, viewDir);
    normal = shiftAmount < 0.0f ? normal + viewDir * (-shiftAmount + 1e-5f) : normal;
    // A re-normalization should be applied here but as the shift is small we don't do it to save ALU.
    //normal = normalize(normal);

    float nv = saturate(dot(normal, viewDir)); // TODO: this saturate should no be necessary here
#else
    half nv = abs(dot(normal, viewDir));    // This abs allow to limit artifact
#endif

    float nl = saturate(dot(normal, light.dir))*atten;
    float nh = saturate(dot(normal, halfDir));

    half lv = saturate(dot(light.dir, viewDir));
    half lh = saturate(dot(light.dir, halfDir));

    // Diffuse term
    half diffuseTerm = DisneyDiffuse(nv, nl, lh, perceptualRoughness) * nl;

    // Specular term
    // HACK: theoretically we should divide diffuseTerm by Pi and not multiply specularTerm!
    // BUT 1) that will make shader look significantly darker than Legacy ones
    // and 2) on engine side "Non-important" lights have to be divided by Pi too in cases when they are injected into ambient SH
    float roughness = PerceptualRoughnessToRoughness(perceptualRoughness);
/*
#if UNITY_BRDF_GGX
    // GGX with roughtness to 0 would mean no specular at all, using max(roughness, 0.002) here to match HDrenderloop roughtness remapping.
    roughness = max(roughness, 0.002);
    float V = SmithJointGGXVisibilityTerm (nl, nv, roughness);
    float D = GGXTerm (nh, roughness);
#else
    // Legacy
    half V = SmithBeckmannVisibilityTerm (nl, nv, roughness);
    half D = NDFBlinnPhongNormalizedTerm (nh, PerceptualRoughnessToSpecPower(perceptualRoughness));
#endif

    float specularTerm = V*D * UNITY_PI; // Torrance-Sparrow model, Fresnel is applied later

#   ifdef UNITY_COLORSPACE_GAMMA
        specularTerm = sqrt(max(1e-4h, specularTerm));
#   endif

    // specularTerm * nl can be NaN on Metal in some cases, use max() to make sure it's a sane value
    specularTerm = max(0, specularTerm * nl);
#if defined(_SPECULARHIGHLIGHTS_OFF)
    specularTerm = 0.0;
#endif
*/
    // surfaceReduction = Int D(NdotH) * NdotH * Id(NdotL>0) dH = 1/(roughness^2+1)
    half surfaceReduction;
/*
    #if defined(LIGHTMAP_ON)
        surfaceReduction =1.0 / (roughness*roughness + 1.0);  
    #else
        surfaceReduction =1.0 / (roughness*roughness + 1.0);  
    #endif
*/
    // To provide true Lambert lighting, we need to be able to kill specular completely.
    surfaceReduction =1.0 / (roughness*roughness + 1.0); 
    //float specularTerm *= any(specColor) ? 1.0 : 0.0;

    half grazingTerm = saturate(smoothness + (1-oneMinusReflectivity));
    half3 color =   diffuseTerm*diffColor* light.color
                    +diffColor * (gi.diffuse)
                    + light.color * FresnelTerm (specColor, lh)
                    + surfaceReduction * gi.specular * FresnelLerp (specColor, grazingTerm, nv);

    return half4(color, 1);
}



#endif  //UNITY_FT_BRDF_INCLUDE