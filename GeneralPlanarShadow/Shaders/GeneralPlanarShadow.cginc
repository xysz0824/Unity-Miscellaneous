#ifndef GENERAL_PLANAR_SHADOW_CGINC
#define GENERAL_PLANAR_SHADOW_CGINC

float _PlanarShadowPlaneY;
float3 _PlanarShadowLightDir;
float3 _PlanarShadowLightPos;
float4 _PlanarShadowLightColor;
float4 _PlanarShadowLightParam;

float4 ProjectPlanarShadowVertex(float4 vertex)
{
    float4 worldPos = mul(unity_ObjectToWorld, vertex);
    float d = -_PlanarShadowPlaneY; //Give plane dot(n, q) + d = 0 when n = (0, 1, 0)
#if DIRECITONAL_PLANAR_SHADOW
    float3 l = normalize(_PlanarShadowLightDir);
    float nl = l.y;
    float4x4 mat = { 
        1,      -l.x / nl,      0,      -d * l.x / nl, 
        0,      1 - l.y / nl,   0,      -d * l.y / nl, 
        0,      -l.z / nl,      1,      -d * l.z / nl, 
        0,      0,              0,      1,
    };
    return mul(mat, worldPos);
#elif POINT_PLANAR_SHADOW || SPOT_PLANAR_SHADOW
    float3 s = _PlanarShadowLightPos;
    worldPos.y = min(worldPos.y, s.y - 0.001f);
    float ns = s.y;
    float4x4 mat = { 
        ns + d, -s.x,           0,      -d * s.x, 
        0,      ns + d - s.y,   0,      -d * s.y, 
        0,      -s.z,           ns + d, -d * s.z, 
        0,      -1,             0,      ns,
    };
    float4 pos = mul(mat, worldPos);
    pos /= pos.w;
    return pos;
#endif
}

fixed4 GetPlanarShadowLightColor(float3 worldPos)
{
    fixed4 col = 1;
#if DIRECITONAL_PLANAR_SHADOW
    col.rgb = _PlanarShadowLightColor.rgb / 2.f;
    fixed scale = saturate(-normalize(_PlanarShadowLightDir).y);
    col.rgb *= scale;
#elif POINT_PLANAR_SHADOW || SPOT_PLANAR_SHADOW
    col.rgb = _PlanarShadowLightColor.rgb / 3.f;
    fixed scale = distance(worldPos, _PlanarShadowLightPos) / _PlanarShadowLightParam.r * 1.4f;
    scale = saturate(1 - scale);
    scale *= scale;
    col.rgb *= scale;
#endif
#if SPOT_PLANAR_SHADOW
    half3 l = normalize(_PlanarShadowLightDir);
    half3 d = normalize(worldPos - _PlanarShadowLightPos);
    half t = dot(l, d);
    scale = saturate((t - _PlanarShadowLightParam.b) / (_PlanarShadowLightParam.g - _PlanarShadowLightParam.b));
    scale *= scale;
    col.rgb *= scale;
#endif
    return col;
}

#endif