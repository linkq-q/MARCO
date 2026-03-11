using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ManagedTakeoverHudView : MonoBehaviour
{
    [Header("Refs")]
    public ManagedTakeoverSystem sys;

    public Image fillImage;                 // Fill (Image Filled)
    public TextMeshProUGUI percentText;     // "23%"
    public TextMeshProUGUI speedText;       // "+0.20/s"

    [Header("Colors")]
    public Color white = Color.white;
    public Color green = new Color(0.3f, 1f, 0.5f, 1f);
    public Color orange = new Color(1f, 0.65f, 0.2f, 1f);
    public Color red = new Color(1f, 0.25f, 0.25f, 1f);

    void OnEnable()
    {
        if (sys != null)
        {
            sys.OnProgressChanged += OnProgressChanged;
            sys.OnRateChanged += OnRateChanged;
        }
        RefreshAll(force: true);
    }

    void OnDisable()
    {
        if (sys != null)
        {
            sys.OnProgressChanged -= OnProgressChanged;
            sys.OnRateChanged -= OnRateChanged;
        }
    }

    void OnProgressChanged(float p) => RefreshProgress();
    void OnRateChanged(float r, string key) => RefreshRate(r, key);

    void RefreshAll(bool force)
    {
        if (sys == null) return;
        RefreshProgress();
        RefreshRate(sys.GetCurrentRate(), sys.GetCurrentRateColorKey());
    }

    void RefreshProgress()
    {
        if (sys == null) return;

        float t = Mathf.InverseLerp(sys.minProgress, sys.maxProgress, sys.Progress);

        if (fillImage != null)
            fillImage.fillAmount = t;

        if (percentText != null)
            percentText.text = $"接管部署进度：{Mathf.RoundToInt(sys.Progress)}%";
    }

    void RefreshRate(float r, string key)
    {
        if (speedText == null) return;

        speedText.text = (r <= 0f) ? "冻结" : $"+{r:0.00}/s";
        speedText.color = key switch
        {
            "green" => green,
            "orange" => orange,
            "red" => red,
            _ => white
        };
    }
}
