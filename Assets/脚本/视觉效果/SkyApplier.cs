using UnityEngine;

public class SkyApplier : MonoBehaviour
{
    [Header("Config")]
    public SkyColorConfig skyConfig;

    [Header("Gradient Renderer (optional)")]
    [Tooltip("Optional renderer for the sky gradient, such as a Quad or Sprite.")]
    public Renderer gradientRenderer;

    [Tooltip("Horizon position in the gradient. 0 = bottom, 1 = top.")]
    [Range(0f, 1f)] public float horizonY = 0.45f;

    [Tooltip("Also apply the top color to fog and camera background.")]
    public bool alsoSetFogAndCamera = true;

    // Cache shader property IDs to avoid repeated string lookups.
    static readonly int TopID = Shader.PropertyToID("_TopColor");
    static readonly int HorizonID = Shader.PropertyToID("_HorizonColor");
    static readonly int BottomID = Shader.PropertyToID("_BottomColor");
    static readonly int HorizonYID = Shader.PropertyToID("_HorizonY");

    MaterialPropertyBlock propertyBlock;

    void Start()
    {
        Apply();
    }

    public void Apply()
    {
        if (skyConfig == null) return;

        if (alsoSetFogAndCamera)
        {
            RenderSettings.fog = true;
            RenderSettings.fogColor = skyConfig.skyColor;
            if (Camera.main) Camera.main.backgroundColor = skyConfig.skyColor;
        }

        if (!gradientRenderer) return;

        // Avoid renderer.material in edit mode so Unity does not instantiate
        // per-renderer materials and leak them into the scene.
        if (!gradientRenderer.sharedMaterial) return;

        propertyBlock ??= new MaterialPropertyBlock();
        gradientRenderer.GetPropertyBlock(propertyBlock);

        Color top = skyConfig.skyColor;
        Color horizon = skyConfig.horizonColor;
        Color bottom = skyConfig.groundColor;

        propertyBlock.SetColor(TopID, top);
        propertyBlock.SetColor(HorizonID, horizon);
        propertyBlock.SetColor(BottomID, bottom);
        propertyBlock.SetFloat(HorizonYID, horizonY);
        gradientRenderer.SetPropertyBlock(propertyBlock);
    }
}
