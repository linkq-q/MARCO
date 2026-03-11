using UnityEngine;

public class SkyApplier : MonoBehaviour
{
    [Header("Config")]
    public SkyColorConfig skyConfig;

    [Header("Gradient Renderer (optional)")]
    [Tooltip("拖一个用于渲染天空渐变的Renderer（建议：一个Quad/Sprite，材质用 SkyGradient.shader）")]
    public Renderer gradientRenderer;

    [Tooltip("渐变中地平线位置（0=底部，1=顶部）。一般 0.35~0.55")]
    [Range(0f, 1f)] public float horizonY = 0.45f;

    [Tooltip("是否同时把雾色/相机底色设置为Top色（兜底）")]
    public bool alsoSetFogAndCamera = true;

    // Shader property IDs（避免字符串开销）
    static readonly int TopID = Shader.PropertyToID("_TopColor");
    static readonly int HorizonID = Shader.PropertyToID("_HorizonColor");
    static readonly int BottomID = Shader.PropertyToID("_BottomColor");
    static readonly int HorizonYID = Shader.PropertyToID("_HorizonY");

    void Start()
    {
        Apply();
    }

    public void Apply()
    {
        if (skyConfig == null) return;

        // 1) 兜底：雾色 + 相机底色（单色）
        if (alsoSetFogAndCamera)
        {
            RenderSettings.fog = true;
            RenderSettings.fogColor = skyConfig.skyColor;
            if (Camera.main) Camera.main.backgroundColor = skyConfig.skyColor;
        }

        // 2) 真正渐变：把三色写进材质
        if (!gradientRenderer) return;

        // 注意：material 会实例化一份，避免改到共享材质
        var mat = gradientRenderer.material;
        if (!mat) return;

        // 兼容字段命名：如果你的 SkyColorConfig 字段不同，按你的实际字段名改这里三行
        Color top = skyConfig.skyColor;
        Color horizon = skyConfig.horizonColor;
        Color bottom = skyConfig.groundColor;

        mat.SetColor(TopID, top);
        mat.SetColor(HorizonID, horizon);
        mat.SetColor(BottomID, bottom);
        mat.SetFloat(HorizonYID, horizonY);
    }
}
