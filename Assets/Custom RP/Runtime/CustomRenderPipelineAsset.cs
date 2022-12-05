using UnityEngine;
using UnityEngine.Rendering;

[System.Serializable]
public struct CameraBufferSettings
{
    public bool allowHDR;

    public bool copyColor, copyColorReflection, copyDepth, copyDepthReflections;

    [Range(CameraRenderer.renderScaleMin, CameraRenderer.renderScaleMax)]
    public float renderScale;

    public enum BicubicRescalingMode { Off, UpOnly, UpAndDown }

    public BicubicRescalingMode bicubicRescaling;
}

[CreateAssetMenu(menuName = "Rendering/Custom Render Pipeline")]
public partial class CustomRenderPipelineAsset : RenderPipelineAsset
{
    [SerializeField]
    CameraBufferSettings cameraBuffer = new CameraBufferSettings
    {
        allowHDR = true,
        renderScale = 1f
    };

    [SerializeField]
    bool useDynamicBatching = true, useGPUInstancing = true, useSRPBatcher = true, useLightsPerObject = true;

    [SerializeField]
    ShadowSettings shadows = default;

    [SerializeField]
    PostFXSettings postFXSettings = default;

    public enum ColorLUTResolution { _16 = 16, _32 = 32, _64 = 64 }

    [SerializeField]
    ColorLUTResolution colorLUTResolution = ColorLUTResolution._32;

    [SerializeField]
    Shader cameraRendererShader = default;

    protected override RenderPipeline CreatePipeline()
    {
        return new CustomRenderPipeline(cameraBuffer, useDynamicBatching, useGPUInstancing, useSRPBatcher, useLightsPerObject, shadows, postFXSettings,
            (int)colorLUTResolution, cameraRendererShader);
    }
}
