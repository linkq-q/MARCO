using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 通过 RendererData(UniversalRendererData/ForwardRendererData) 运行时开关 Renderer Features。
/// 兼容 renderer.rendererFeatures 不可访问的 URP 版本。
/// </summary>
public class URPRendererFeatureToggler_ByRendererData : MonoBehaviour
{
    [Header("Renderer Data Asset")]
    [Tooltip("把你相机正在使用的 Renderer Data 资产拖进来（包含 GaussianBlurFeature/DarkFilterFeature 的那个）")]
    public ScriptableRendererData rendererData;

    [Header("Feature Names (match 'Name' in Renderer Feature inspector)")]
    public string blurFeatureName = "GaussianBlurFeature";
    public string darkFeatureName = "DarkFilterFeature";

    [Header("Debug")]
    public bool logDebug = false;

    ScriptableRendererFeature _blur;
    ScriptableRendererFeature _dark;

    void Awake()
    {
        Cache();
        SetEndingFX(false); // ✅ 每次进入Play先关掉
    }

    public void SetEndingFX(bool on)
    {
        if ((_blur == null && _dark == null)) Cache();

        if (_blur != null) _blur.SetActive(on);
        else if (logDebug) Debug.LogWarning($"[FeatureToggler] Blur feature not found: {blurFeatureName}");

        if (_dark != null) _dark.SetActive(on);
        else if (logDebug) Debug.LogWarning($"[FeatureToggler] Dark feature not found: {darkFeatureName}");
    }

    public void SetBlur(bool on)
    {
        if (_blur == null) Cache();
        if (_blur != null) _blur.SetActive(on);
    }

    public void SetDark(bool on)
    {
        if (_dark == null) Cache();
        if (_dark != null) _dark.SetActive(on);
    }

    void Cache()
    {
        if (!rendererData)
        {
            Debug.LogError("[FeatureToggler] rendererData is NULL. 请把包含这些 Feature 的 Renderer Data 资产拖进来。", this);
            return;
        }

        // ✅ 关键：用 RendererData 的 rendererFeatures（这个在 URP 版本里通常是可访问的）
        var list = rendererData.rendererFeatures;
        _blur = FindByName(list, blurFeatureName);
        _dark = FindByName(list, darkFeatureName);

        if (logDebug)
            Debug.Log($"[FeatureToggler] Cache done. blur={(_blur ? _blur.name : "NULL")}, dark={(_dark ? _dark.name : "NULL")}", this);
    }

    static ScriptableRendererFeature FindByName(System.Collections.Generic.List<ScriptableRendererFeature> list, string n)
    {
        if (list == null || string.IsNullOrEmpty(n)) return null;
        for (int i = 0; i < list.Count; i++)
        {
            var f = list[i];
            if (!f) continue;
            if (string.Equals(f.name, n, StringComparison.Ordinal))
                return f;
        }
        return null;
    }
}