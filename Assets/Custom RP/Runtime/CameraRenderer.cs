using UnityEngine;
using UnityEngine.Rendering;

public partial class CameraRenderer
{
    ScriptableRenderContext context;

    Camera camera;

    const string bufferName = "Render Camera Czw";

    // use context.DrawSkybox to draw the skybox
    // via a separate command buffer to draw the other geometry
    CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };

    CullingResults cullingResults;

    static ShaderTagId unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit"),
        litShaderTagId = new ShaderTagId("CustomLit");

    Lighting lighting = new Lighting();

    PostFXStack postFXStack = new PostFXStack();

    static int frameBufferId = Shader.PropertyToID("_CameraFrameBuffer");

    bool useHDR;

    static CameraSettings defaultCameraSettings = new CameraSettings();

    void Setup()
    {
        // apply camera's properties to the context
        context.SetupCameraProperties(camera);

        CameraClearFlags flags = camera.clearFlags;

        if (postFXStack.IsActive)
        {
            if (flags > CameraClearFlags.Color)
                flags = CameraClearFlags.Color;
            buffer.GetTemporaryRT(frameBufferId, camera.pixelWidth, camera.pixelHeight, 32, FilterMode.Bilinear, useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);
            buffer.SetRenderTarget(frameBufferId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        }

        // clear camera's render target
        buffer.ClearRenderTarget(flags <= CameraClearFlags.Depth, flags == CameraClearFlags.Color,
            flags == CameraClearFlags.Color ? camera.backgroundColor.linear : Color.clear);

        // begin inject profiler event (show up in profiler & frame debugger)
        buffer.BeginSample(SampleName);

        ExecuteBuffer();
    }

    public void Render(ScriptableRenderContext context, Camera camera, bool allowHDR, bool useDynamicBatching, bool useGPUInstancing, bool useLightsPerObject, ShadowSettings shadowSettings,
        PostFXSettings postFXSettings, int colorLUTResolution)
    {
        this.context = context;
        this.camera = camera;

        var crpCamera = camera.GetComponent<CustomRenderPipelineCamera>();
        CameraSettings cameraSettings = crpCamera ? crpCamera.Settings : defaultCameraSettings;

        if (cameraSettings.overridePostFX)
        {
            postFXSettings = cameraSettings.postFXSettings;
        }

        PrepareBuffer();
        // draw UI in scene window
        PrepareForSceneWindow();

        if (!Cull(shadowSettings.maxDistance))
        {
            return;
        }

        useHDR = allowHDR && camera.allowHDR;

        buffer.BeginSample(SampleName);
        ExecuteBuffer();

        // setup lighting data
        lighting.Setup(context, cullingResults, shadowSettings, useLightsPerObject, cameraSettings.renderingLayerMask);

        // setup post process
        postFXStack.Setup(context, camera, postFXSettings, useHDR, colorLUTResolution, cameraSettings.finalBlendMode);

        buffer.EndSample(SampleName);

        Setup();

        DrawVisibleGeometry(useDynamicBatching, useGPUInstancing, useLightsPerObject, cameraSettings.renderingLayerMask);

        DrawUnsupportedShaders();

        // draw gizmos in scene window
        DrawGizmosBeforeFX();

        // post process
        if (postFXStack.IsActive)
        {
            postFXStack.Render(frameBufferId);
        }

        // draw gizmos in scene window
        DrawGizmosAfterFX();

        Cleanup();

        // submit the queued work for execution
        Submit();
    }

    void DrawVisibleGeometry(bool useDynamicBatching, bool useGPUInstancing, bool useLightsPerObject, int renderingLayerMask)
    {
        PerObjectData lightsPerObjectData = useLightsPerObject ? PerObjectData.LightData | PerObjectData.LightIndices : PerObjectData.None;

        // draw opaque
        var sortingSettings = new SortingSettings(camera)
        {
            criteria = SortingCriteria.CommonOpaque
        };
        var drawingSettings = new DrawingSettings(unlitShaderTagId, sortingSettings)
        {
            enableDynamicBatching = useDynamicBatching,
            enableInstancing = useGPUInstancing,
            // perObjectData: Set what kind of per-object data to setup during rendering.
            perObjectData = PerObjectData.ReflectionProbes | PerObjectData.Lightmaps | PerObjectData.ShadowMask |
                            PerObjectData.LightProbe | PerObjectData.OcclusionProbe |
                            PerObjectData.LightProbeProxyVolume |
                            PerObjectData.OcclusionProbeProxyVolume | lightsPerObjectData
        };
        drawingSettings.SetShaderPassName(1, litShaderTagId);

        var filteringSettings = new FilteringSettings(RenderQueueRange.opaque, renderingLayerMask: (uint)renderingLayerMask);
        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);

        // draw skybox
        context.DrawSkybox(camera);

        // draw transparent
        sortingSettings.criteria = SortingCriteria.CommonTransparent;
        drawingSettings.sortingSettings = sortingSettings;
        filteringSettings.renderQueueRange = RenderQueueRange.transparent;
        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
    }

    void Submit()
    {
        // end inject profiler event (show up in profiler & frame debugger)
        buffer.EndSample(SampleName);

        ExecuteBuffer();

        context.Submit();
    }

    void ExecuteBuffer()
    {
        // execute the buffer
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    bool Cull(float maxShadowDistance)
    {
        if (camera.TryGetCullingParameters(out ScriptableCullingParameters p))
        {
            p.shadowDistance = Mathf.Min(maxShadowDistance, camera.farClipPlane);
            cullingResults = context.Cull(ref p);
            return true;
        }

        return false;
    }

    void Cleanup()
    {
        lighting.Cleanup();

        if (postFXStack.IsActive)
            buffer.ReleaseTemporaryRT(frameBufferId);
    }
}
