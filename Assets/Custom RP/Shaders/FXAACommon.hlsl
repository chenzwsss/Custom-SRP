#ifndef CUSTOM_FXAA_COMMON_INCLUDED
#define CUSTOM_FXAA_COMMON_INCLUDED

#include "../ShaderLibrary/Common.hlsl"

#define FXAA_SPAN_MAX   (8.0)
#define FXAA_REDUCE_MUL (1.0 / 8.0)
#define FXAA_REDUCE_MIN (1.0 / 128.0)

#define TEXTURE2D_X(textureName)                                  TEXTURE2D(textureName)
#define SAMPLE_TEXTURE2D_X(textureName, samplerName, coord2)      SAMPLE_TEXTURE2D(textureName, samplerName, coord2)
#define LOAD_TEXTURE2D_X(textureName, unCoord2)                   LOAD_TEXTURE2D(textureName, unCoord2)

half3 FXAAFetch(float2 coords, float2 offset, TEXTURE2D_X(inputTexture))
{
    float2 uv = coords + offset;
    return SAMPLE_TEXTURE2D_X(inputTexture, sampler_linear_clamp, uv).xyz;
}

half3 FXAALoad(int2 icoords, int idx, int idy, float4 sourceSize, TEXTURE2D_X(inputTexture))
{
    #if SHADER_API_GLES
        float2 uv = (icoords + int2(idx, idy)) * sourceSize.zw;
        return SAMPLE_TEXTURE2D_X(inputTexture, sampler_PointClamp, uv).xyz;
    #else
        return LOAD_TEXTURE2D_X(inputTexture, clamp(icoords + int2(idx, idy), 0, sourceSize.xy - 1.0)).xyz;
    #endif
}

half3 ApplyFXAA(half3 color, float2 positionNDC, int2 positionSS, float4 sourceSize, TEXTURE2D_X(inputTexture))
{
    // Edge detection
    half3 rgbNW = FXAALoad(positionSS, -1, -1, sourceSize, inputTexture);
    half3 rgbNE = FXAALoad(positionSS,  1, -1, sourceSize, inputTexture);
    half3 rgbSW = FXAALoad(positionSS, -1,  1, sourceSize, inputTexture);
    half3 rgbSE = FXAALoad(positionSS,  1,  1, sourceSize, inputTexture);

    rgbNW = saturate(rgbNW);
    rgbNE = saturate(rgbNE);
    rgbSW = saturate(rgbSW);
    rgbSE = saturate(rgbSE);
    color = saturate(color);

    half lumaNW = Luminance(rgbNW);
    half lumaNE = Luminance(rgbNE);
    half lumaSW = Luminance(rgbSW);
    half lumaSE = Luminance(rgbSE);
    half lumaM = Luminance(color);

    float2 dir;
    dir.x = -((lumaNW + lumaNE) - (lumaSW + lumaSE));
    dir.y = ((lumaNW + lumaSW) - (lumaNE + lumaSE));

    half lumaSum = lumaNW + lumaNE + lumaSW + lumaSE;
    float dirReduce = max(lumaSum * (0.25 * FXAA_REDUCE_MUL), FXAA_REDUCE_MIN);
    float rcpDirMin = rcp(min(abs(dir.x), abs(dir.y)) + dirReduce);

    dir = min((FXAA_SPAN_MAX).xx, max((-FXAA_SPAN_MAX).xx, dir * rcpDirMin)) * sourceSize.zw;

    // Blur
    half3 rgb03 = FXAAFetch(positionNDC, dir * (0.0 / 3.0 - 0.5), inputTexture);
    half3 rgb13 = FXAAFetch(positionNDC, dir * (1.0 / 3.0 - 0.5), inputTexture);
    half3 rgb23 = FXAAFetch(positionNDC, dir * (2.0 / 3.0 - 0.5), inputTexture);
    half3 rgb33 = FXAAFetch(positionNDC, dir * (3.0 / 3.0 - 0.5), inputTexture);

    rgb03 = saturate(rgb03);
    rgb13 = saturate(rgb13);
    rgb23 = saturate(rgb23);
    rgb33 = saturate(rgb33);

    half3 rgbA = 0.5 * (rgb13 + rgb23);
    half3 rgbB = rgbA * 0.5 + 0.25 * (rgb03 + rgb33);

    half lumaB = Luminance(rgbB);

    half lumaMin = Min3(lumaM, lumaNW, Min3(lumaNE, lumaSW, lumaSE));
    half lumaMax = Max3(lumaM, lumaNW, Max3(lumaNE, lumaSW, lumaSE));

    return ((lumaB < lumaMin) || (lumaB > lumaMax)) ? rgbA : rgbB;
}

#endif
