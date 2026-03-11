using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SanHudView : MonoBehaviour
{
    [Header("Refs")]
    public SanSystem sanSystem;

    [Tooltip("san值/sanfill (Image Filled Horizontal)")]
    public Image fillImage;

    [Tooltip("san值/san值显示 (TMP)")]
    public TextMeshProUGUI valueText;

    [Header("Display")]
    public bool showAsFraction = false; // true: 65/100

    void OnEnable()
    {
        if (sanSystem != null)
            sanSystem.OnSanChanged += HandleSanChanged;

        RefreshAll();
    }

    void OnDisable()
    {
        if (sanSystem != null)
            sanSystem.OnSanChanged -= HandleSanChanged;
    }

    void HandleSanChanged(int san, int delta, string reason)
    {
        RefreshAll();
    }

    public void RefreshAll()
    {
        if (sanSystem == null) return;

        float t = Mathf.InverseLerp(sanSystem.minSan, sanSystem.maxSan, sanSystem.San);

        if (fillImage != null)
            fillImage.fillAmount = t;

        if (valueText != null)
            valueText.text = showAsFraction ? $"{sanSystem.San}/{sanSystem.maxSan}" : $"{sanSystem.San}";
    }
}
