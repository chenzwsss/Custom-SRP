using UnityEngine;

public partial class CustomRenderPipelineAsset
{
#if UNITY_EDITOR

    static string[] renderingLayerNames;

    static CustomRenderPipelineAsset()
    {
        renderingLayerNames = new string[31];
        for (int i = 0; i < renderingLayerNames.Length; i++) {
            renderingLayerNames[i] = "Layer " + (i + 1);
        }
    }

    // seems does not work in Unity 2021.3.15f1c1
    public override string[] renderingLayerMaskNames => renderingLayerNames;

#endif
}
