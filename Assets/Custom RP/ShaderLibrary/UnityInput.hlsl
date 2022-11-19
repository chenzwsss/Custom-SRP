﻿#ifndef CUSTOM_UNITY_INPUT_INCLUDED
#define CUSTOM_UNITY_INPUT_INCLUDED

CBUFFER_START(UnityPerDraw)
    float4x4 unity_ObjectToWorld;
    float4x4 unity_WorldToObject;
    float4 unity_LODFade;
    real4 unity_WorldTransformParams;

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

    float4 unity_SpecCube0_BoxMin;
    float4 unity_SpecCube0_BoxMax;
    float4 unity_SpecCube0_ProbePosition;
CBUFFER_END

float4x4 unity_MatrixVP;
float4x4 unity_MatrixV;
float4x4 glstate_matrix_projection;

float4x4 unity_MatrixPreviousM;
float4x4 unity_MatrixPreviousMI;

float3 _WorldSpaceCameraPos;

#endif
