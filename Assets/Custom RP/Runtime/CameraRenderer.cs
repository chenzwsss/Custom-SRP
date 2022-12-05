using UnityEngine;
using UnityEngine.Rendering;

public partial class CameraRenderer
{
    ScriptableRenderContext context;

    Camera camera;

    const string bufferName = "Render Camera Czw";

    public const float renderScaleMin = 0.1f, renderScaleMax = 2f;

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

    static int bufferSizeId = Shader.PropertyToID("_CameraBufferSize"),
        colorAttachmentId = Shader.PropertyToID("_CameraColorAttachment"),
        depthAttachmentId = Shader.PropertyToID("_CameraDepthAttachment"),
        colorTextureId = Shader.PropertyToID("_CameraColorTexture"),
        depthTextureId = Shader.PropertyToID("_CameraDepthTexture"),
        sourceTextureId = Shader.PropertyToID("_SourceTexture"),
        srcBlendId = Shader.PropertyToID("_CameraSrcBlend"),
        dstBlendId = Shader.PropertyToID("_CameraDstBlend");

    bool useHDR, useScaledRendering;

    static CameraSettings defaultCameraSettings = new CameraSettings();

    bool useColorTexture, useDepthTexture, useIntermediateBuffer;

    Material material;

    Texture2D missingTexture;

    static bool copyTextureSupported = SystemInfo.copyTextureSupport > CopyTextureSupport.None;

    Vector2Int bufferSize;

    public CameraRenderer(Shader shader)
    {
        material = CoreUtils.CreateEngineMaterial(shader);
        missingTexture = new Texture2D(1, 1)
        {
            hideFlags = HideFlags.HideAndDontSave,
            name = "Missing"
        };
        missingTexture.SetPixel(0, 0, Color.white * 0.5f);
        missingTexture.Apply(true, true);
    }

    public void Dispose()
    {
        CoreUtils.Destroy(material);
        CoreUtils.Destroy(missingTexture);
    }

    void Setup()
    {
        // apply camera's properties to the context
        context.SetupCameraProperties(camera);

        CameraClearFlags flags = camera.clearFlags;

        useIntermediateBuffer = useScaledRendering || useColorTexture || useDepthTexture || postFXStack.IsActive;
        if (useIntermediateBuffer)
        {
            if (flags > CameraClearFlags.Color)
            {
                flags = CameraClearFlags.Color;
            }
            buffer.GetTemporaryRT(colorAttachmentId, bufferSize.x, bufferSize.y, 0, FilterMode.Bilinear, useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);
            buffer.GetTemporaryRT(depthAttachmentId, bufferSize.x, bufferSize.y, 32, FilterMode.Point, RenderTextureFormat.Depth);
            buffer.SetRenderTarget(colorAttachmentId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
                depthAttachmentId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        }

        // clear camera's render target
        buffer.ClearRenderTarget(flags <= CameraClearFlags.Depth, flags == CameraClearFlags.Color,
            flags == CameraClearFlags.Color ? camera.backgroundColor.linear : Color.clear);

        // begin inject profiler event (show up in profiler & frame debugger)
        buffer.BeginSample(SampleName);

        buffer.SetGlobalTexture(colorTextureId, missingTexture);
        buffer.SetGlobalTexture(depthTextureId, missingTexture);

        ExecuteBuffer();
    }

    public void Render(ScriptableRenderContext context, Camera camera, CameraBufferSettings bufferSettings, bool useDynamicBatching, bool useGPUInstancing, bool useLightsPerObject, ShadowSettings shadowSettings,
        PostFXSettings postFXSettings, int colorLUTResolution)
    {
        this.context = context;
        this.camera = camera;

        var crpCamera = camera.GetComponent<CustomRenderPipelineCamera>();
        CameraSettings cameraSettings = crpCamera ? crpCamera.Settings : defaultCameraSettings;

        if (camera.cameraType == CameraType.Reflection)
        {
            useColorTexture = bufferSettings.copyColorReflection;
            useDepthTexture = bufferSettings.copyDepthReflections;
        }
        else
        {
            useColorTexture = bufferSettings.copyColor && cameraSettings.copyColor;
            useDepthTexture = bufferSettings.copyDepth && cameraSettings.copyDepth;
        }

        if (cameraSettings.overridePostFX)
        {
            postFXSettings = cameraSettings.postFXSettings;
        }

        float renderScale = cameraSettings.GetRenderScale(bufferSettings.renderScale);
        useScaledRendering = renderScale < 0.99f || renderScale > 1.01f;

        PrepareBuffer();
        // draw UI in scene window
        PrepareForSceneWindow();

        if (!Cull(shadowSettings.maxDistance))
        {
            return;
        }

        useHDR = bufferSettings.allowHDR && camera.allowHDR;

        if (useScaledRendering)
        {
            renderScale = Mathf.Clamp(renderScale, renderScaleMin, renderScaleMax);
            bufferSize.x = (int)(camera.pixelWidth * renderScale);
            bufferSize.y = (int)(camera.pixelHeight * renderScale);
        }
        else
        {
            bufferSize.x = camera.pixelWidth;
            bufferSize.y = camera.pixelHeight;
        }

        buffer.BeginSample(SampleName);

        buffer.SetGlobalVector(bufferSizeId, new Vector4(1f / bufferSize.x, 1f / bufferSize.y, bufferSize.x, bufferSize.y));

        ExecuteBuffer();

        // setup lighting data
        lighting.Setup(context, cullingResults, shadowSettings, useLightsPerObject, cameraSettings.renderingLayerMask);

        // setup post process
        postFXStack.Setup(context, camera, bufferSize, postFXSettings, useHDR, colorLUTResolution, cameraSettings.finalBlendMode, bufferSettings.bicubicRescaling);

        buffer.EndSample(SampleName);

        Setup();

        DrawVisibleGeometry(useDynamicBatching, useGPUInstancing, useLightsPerObject, cameraSettings.renderingLayerMask);

        DrawUnsupportedShaders();

        // draw gizmos in scene window
        DrawGizmosBeforeFX();

        // post process
        if (postFXStack.IsActive)
        {
            postFXStack.Render(colorAttachmentId);
        }
        else if (useIntermediateBuffer)
        {
            DrawFinal(cameraSettings.finalBlendMode);
            ExecuteBuffer();
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

        if (useColorTexture || useDepthTexture)
        {
            CopyAttachments();
        }

        // draw transparent
        sortingSettings.criteria = SortingCriteria.CommonTransparent;
        drawingSettings.sortingSettings = sortingSettings;
        filteringSettings.renderQueueRange = RenderQueueRange.transparent;
        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
    }

    void Draw(RenderTargetIdentifier from, RenderTargetIdentifier to, bool isDepth = false)
    {
        buffer.SetGlobalTexture(sourceTextureId, from);
        buffer.SetRenderTarget(to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        buffer.DrawProcedural(Matrix4x4.identity, material, isDepth ? 1 : 0, MeshTopology.Triangles, 3);
    }

    void DrawFinal(CameraSettings.FinalBlendMode finalBlendMode)
    {
        buffer.SetGlobalFloat(srcBlendId, (float)finalBlendMode.source);
        buffer.SetGlobalFloat(dstBlendId, (float)finalBlendMode.destination);
        buffer.SetGlobalTexture(sourceTextureId, colorAttachmentId);
        buffer.SetRenderTarget(BuiltinRenderTextureType.CameraTarget,
            finalBlendMode.destination == BlendMode.Zero ? RenderBufferLoadAction.DontCare : RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);
        buffer.SetViewport(camera.pixelRect);
        buffer.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3);
        buffer.SetGlobalFloat(srcBlendId, 1f);
        buffer.SetGlobalFloat(dstBlendId, 0f);
    }

    void CopyAttachments()
    {
        if (useColorTexture)
        {
            buffer.GetTemporaryRT(colorTextureId, bufferSize.x, bufferSize.y, 0, FilterMode.Bilinear, useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);
            if (copyTextureSupported)
            {
                buffer.CopyTexture(colorAttachmentId, colorTextureId);
            }
            else
            {
                Draw(colorAttachmentId, colorTextureId);
            }
        }
        if (useDepthTexture)
        {
            buffer.GetTemporaryRT(depthTextureId, bufferSize.x, bufferSize.y, 32, FilterMode.Point, RenderTextureFormat.Depth);
            if (copyTextureSupported)
            {
                buffer.CopyTexture(depthAttachmentId, depthTextureId);
            }
            else
            {
                Draw(depthAttachmentId, depthTextureId, true);
            }
        }

        if (!copyTextureSupported)
        {
            buffer.SetRenderTarget(colorAttachmentId, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store,
                depthAttachmentId, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);
        }
        ExecuteBuffer();
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

        if (useIntermediateBuffer)
        {
            buffer.ReleaseTemporaryRT(colorAttachmentId);
            buffer.ReleaseTemporaryRT(depthAttachmentId);
            if (useColorTexture)
            {
                buffer.ReleaseTemporaryRT(colorTextureId);
            }
            if (useDepthTexture)
            {
                buffer.ReleaseTemporaryRT(depthTextureId);
            }
        }
    }
}
