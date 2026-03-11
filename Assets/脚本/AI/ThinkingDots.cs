using System.Collections;
using TMPro;
using UnityEngine;

public class ThinkingDots : MonoBehaviour
{
    public TextMeshProUGUI text;

    [Header("Timing")]
    public float interval = 0.35f;     // 每次切换周期
    public int maxDots = 3;

    [Header("Fade")]
    public float fadeOut = 0.10f;      // 消失渐隐时间
    public float fadeIn = 0.10f;      // 出现渐显时间
    [Range(0f, 1f)] public float minAlpha = 0.25f; // 淡出到的最低透明度

    Coroutine co;

    void Awake()
    {
        if (!text) text = GetComponent<TextMeshProUGUI>();
    }

    void OnEnable()
    {
        if (co != null) StopCoroutine(co);
        co = StartCoroutine(Loop());
    }

    void OnDisable()
    {
        if (co != null) StopCoroutine(co);
        co = null;

        if (text) text.text = "";
        SetAlpha(1f);
    }

    IEnumerator Loop()
    {
        int count = 0;
        SetAlpha(1f);

        while (true)
        {
            // 1) 先淡出（“消失”更柔和）
            if (fadeOut > 0f)
                yield return FadeTo(minAlpha, fadeOut);

            // 2) 切换点数
            count++;
            if (count > maxDots) count = 1;
            text.text = new string('.', count);

            // 3) 再淡入（“出现”更灵动）
            if (fadeIn > 0f)
                yield return FadeTo(1f, fadeIn);

            // 4) 等待剩余时间
            float rest = interval - fadeOut - fadeIn;
            if (rest > 0f)
                yield return new WaitForSecondsRealtime(rest);
            else
                yield return null;
        }
    }

    IEnumerator FadeTo(float target, float seconds)
    {
        float start = text ? text.alpha : 1f;
        float t = 0f;

        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / seconds);
            SetAlpha(Mathf.Lerp(start, target, k));
            yield return null;
        }
        SetAlpha(target);
    }

    void SetAlpha(float a)
    {
        if (!text) return;
        text.alpha = a;
    }
}
