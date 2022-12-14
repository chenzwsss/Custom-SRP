using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class Lighting
{
    const string bufferName = "Lighting";

    CommandBuffer cmd = new CommandBuffer
    {
        name = bufferName
    };

    const int maxDirLightCount = 4, maxOtherLightCount = 64;

    static string lightsPerObjectKeyword = "_LIGHTS_PER_OBJECT";

    // directional lights
    static int dirLightCountId = Shader.PropertyToID("_DirectionalLightCount"),
        dirLightColorsId = Shader.PropertyToID("_DirectionalLightColors"),
        dirLightDirectionsAndMasksId = Shader.PropertyToID("_DirectionalLightDirectionsAndMasks"),
        dirLightShadowDataId = Shader.PropertyToID("_DirectionalLightShadowData");

    static Vector4[] dirLightColors = new Vector4[maxDirLightCount],
        dirLightDirectionsAndMasks = new Vector4[maxDirLightCount],
        dirLightShadowData = new Vector4[maxDirLightCount];

    // other lights
    static int otherLightCountId = Shader.PropertyToID("_OtherLightCount"),
       otherLightColorsId = Shader.PropertyToID("_OtherLightColors"),
       otherLightPositionsId = Shader.PropertyToID("_OtherLightPositions"),
       otherLightDirectionsAndMasksId = Shader.PropertyToID("_OtherLightDirectionsAndMasks"),
       otherLightSpotAnglesId = Shader.PropertyToID("_OtherLightSpotAngles"),
       otherLightShadowDataId = Shader.PropertyToID("_OtherLightShadowData");

    static Vector4[] otherLightColors = new Vector4[maxOtherLightCount],
        otherLightPositions = new Vector4[maxOtherLightCount],
        otherLightDirectionsAndMasks = new Vector4[maxOtherLightCount],
        otherLightSpotAngles = new Vector4[maxOtherLightCount],
        otherLightShadowData = new Vector4[maxOtherLightCount];

    CullingResults cullingResults;

    private Shadows shadows = new Shadows();

    public void Setup(ScriptableRenderContext context, CullingResults cullingResults, ShadowSettings shadowSettings, bool useLightsPerObject,
        int renderingLayerMask)
    {
        this.cullingResults = cullingResults;

        cmd.BeginSample(bufferName);

        shadows.Setup(context, cullingResults, shadowSettings);

        SetupLights(useLightsPerObject, renderingLayerMask);

        shadows.Render();

        cmd.EndSample(bufferName);

        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();
    }

    void SetupLights(bool useLightsPerObject, int renderingLayerMask)
    {
        NativeArray<int> indexMap = useLightsPerObject ? cullingResults.GetLightIndexMap(Allocator.Temp) : default;
        NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;

        int dirLightCount = 0, otherLightCount = 0;
        int i;
        for (i = 0; i < visibleLights.Length; i++)
        {
            int newIndex = -1;
            VisibleLight visibleLight = visibleLights[i];
            Light light = visibleLight.light;

            if ((light.renderingLayerMask & renderingLayerMask) != 0)
            {
                switch (visibleLight.lightType)
                {
                    case LightType.Directional:
                        if (dirLightCount < maxDirLightCount)
                        {
                            SetupDirectionalLight(dirLightCount++, i, ref visibleLight, light);
                        }
                        break;
                    case LightType.Point:
                        if (otherLightCount < maxOtherLightCount)
                        {
                            newIndex = otherLightCount;
                            SetupPointLight(otherLightCount++, i, ref visibleLight, light);
                        }
                        break;
                    case LightType.Spot:
                        if (otherLightCount < maxOtherLightCount)
                        {
                            newIndex = otherLightCount;
                            SetupSpotLight(otherLightCount++, i, ref visibleLight, light);
                        }
                        break;
                }
            }

            if (useLightsPerObject)
                indexMap[i] = newIndex;
        }

        if (useLightsPerObject)
        {
            for (; i < indexMap.Length; i++)
                indexMap[i] = -1;
            cullingResults.SetLightIndexMap(indexMap);
            indexMap.Dispose();
            Shader.EnableKeyword(lightsPerObjectKeyword);
        }
        else
        {
            Shader.DisableKeyword(lightsPerObjectKeyword);
        }

        cmd.SetGlobalInt(dirLightCountId, dirLightCount);
        if (dirLightCount > 0)
        {
            cmd.SetGlobalVectorArray(dirLightColorsId, dirLightColors);
            cmd.SetGlobalVectorArray(dirLightDirectionsAndMasksId, dirLightDirectionsAndMasks);
            cmd.SetGlobalVectorArray(dirLightShadowDataId, dirLightShadowData);
        }

        cmd.SetGlobalInt(otherLightCountId, otherLightCount);
        if (otherLightCount > 0)
        {
            cmd.SetGlobalVectorArray(otherLightColorsId, otherLightColors);
            cmd.SetGlobalVectorArray(otherLightPositionsId, otherLightPositions);
            cmd.SetGlobalVectorArray(otherLightDirectionsAndMasksId, otherLightDirectionsAndMasks);
            cmd.SetGlobalVectorArray(otherLightSpotAnglesId, otherLightSpotAngles);
            cmd.SetGlobalVectorArray(otherLightShadowDataId, otherLightShadowData);
        }
    }

    void SetupDirectionalLight(int index, int visibleIndex, ref VisibleLight visibleLight, Light light)
    {
        dirLightColors[index] = visibleLight.finalColor;
        Vector4 dirAndMask = -visibleLight.localToWorldMatrix.GetColumn(2);
        dirAndMask.w = light.renderingLayerMask.ReinterpretAsFloat();
        dirLightDirectionsAndMasks[index] = -visibleLight.localToWorldMatrix.GetColumn(2);

        dirLightShadowData[index] = shadows.ReserveDirectionalShadows(light, visibleIndex);
    }

    void SetupPointLight(int index, int visibleIndex, ref VisibleLight visibleLight, Light light)
    {
        otherLightColors[index] = visibleLight.finalColor;

        Vector4 position = visibleLight.localToWorldMatrix.GetColumn(3);
        position.w = 1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
        otherLightPositions[index] = position;

        otherLightSpotAngles[index] = new Vector4(0f, 1f);

        Vector4 dirAndmask = Vector4.zero;
        dirAndmask.w = light.renderingLayerMask.ReinterpretAsFloat();
        otherLightDirectionsAndMasks[index] = dirAndmask;

        otherLightShadowData[index] = shadows.ReserveOtherShadows(light, visibleIndex);
    }

    void SetupSpotLight(int index, int visibleIndex, ref VisibleLight visibleLight, Light light)
    {
        otherLightColors[index] = visibleLight.finalColor;

        Vector4 position = visibleLight.localToWorldMatrix.GetColumn(3);
        position.w = 1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
        otherLightPositions[index] = position;

        Vector4 dirAndMask = -visibleLight.localToWorldMatrix.GetColumn(2);
        dirAndMask.w = light.renderingLayerMask.ReinterpretAsFloat();
        otherLightDirectionsAndMasks[index] = dirAndMask;


        float innerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * light.innerSpotAngle);
        float outerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * light.spotAngle);
        float angleRangeInv = 1f / Mathf.Max(innerCos - outerCos, 0.001f);
        otherLightSpotAngles[index] = new Vector4(angleRangeInv, -outerCos * angleRangeInv);

        otherLightShadowData[index] = shadows.ReserveOtherShadows(light, visibleIndex);
    }

    public void Cleanup()
    {
        shadows.Cleanup();
    }
}
