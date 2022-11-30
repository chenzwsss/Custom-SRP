#ifndef CUSTOM_UNITY_INPUT_INCLUDED
#define CUSTOM_UNITY_INPUT_INCLUDED

CBUFFER_START(UnityPerDraw)
    float4x4 unity_ObjectToWorld;
    float4x4 unity_WorldToObject;
    float4 unity_LODFade;
    real4 unity_WorldTransformParams;

    // Light Indices block feature
    // These are set internally by the engine upon request by RendererConfiguration.
    real4 unity_LightData;  // unity_LightData.y 记录了影响当前材质的光源数量
    real4 unity_LightIndices[2];  // unity_LightIndices[2] 记录每个索引对应的具体光源 index

    float4 unity_ProbesOcclusion;

    // lightmap
    float4 unity_LightmapST;
    float4 unity_DynamicLightmapST;

    // Light Probe
    float4 unity_SHAr;
    float4 unity_SHAg;
    float4 unity_SHAb;
    float4 unity_SHBr;
    float4 unity_SHBg;
    float4 unity_SHBb;
    float4 unity_SHC;

    // Light Probe Proxy Volume
    float4 unity_ProbeVolumeParams;
    float4x4 unity_ProbeVolumeWorldToObject;
    float4 unity_ProbeVolumeSizeInv;
    float4 unity_ProbeVolumeMin;

    // Reflection Probe 0 block feature
    // HDR environment map decode instructions
    float4 unity_SpecCube0_HDR;

    float4 _ProjectionParams;
CBUFFER_END

float4x4 unity_MatrixVP;
float4x4 unity_MatrixV;
float4x4 glstate_matrix_projection;

float4x4 unity_MatrixPreviousM;
float4x4 unity_MatrixPreviousMI;

float3 _WorldSpaceCameraPos;

#endif
