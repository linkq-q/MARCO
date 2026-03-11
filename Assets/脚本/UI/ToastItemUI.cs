using System.Collections;
using TMPro;
using UnityEngine;

public class ToastItemUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TextMeshProUGUI text;

    [Header("Timing")]
    [SerializeField] private float inDuration = 0.2f;
    [SerializeField] private float stayDuration = 4.0f;
    [SerializeField] private float outDuration = 0.25f;

    [Header("Slide")]
    [SerializeField] private float slidePixels = 80f; // 从右侧滑入的距离

    RectTransform _rt;
    Vector2 _baseAnchoredPos;
    Coroutine _co;

    void Awake()
    {
        _rt = GetComponent<RectTransform>();
        if (!canvasGroup) canvasGroup = GetComponent<CanvasGroup>();
        _baseAnchoredPos = _rt.anchoredPosition;
    }

    public void Play(string msg)
    {
        if (text) text.text = msg;

        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(CoPlay());
    }

    IEnumerator CoPlay()
    {
        // 初始：右侧偏移 + 透明
        canvasGroup.alpha = 0f;
        _rt.anchoredPosition = _baseAnchoredPos + new Vector2(slidePixels, 0);

        // 淡入 + 滑入
        yield return FadeMove(0f, 2f,
            _baseAnchoredPos + new Vector2(slidePixels, 0),
            _baseAnchoredPos,
            inDuration);

        // 停留
        yield return new WaitForSeconds(stayDuration);

        // 淡出 + 轻微滑出
        yield return FadeMove(1f, 0f,
            _baseAnchoredPos,
            _baseAnchoredPos + new Vector2(slidePixels * 0.35f, 0),
            outDuration);

        Destroy(gameObject);
    }

    IEnumerator FadeMove(float a0, float a1, Vector2 p0, Vector2 p1, float dur)
    {
        if (dur <= 0.001f)
        {
            canvasGroup.alpha = a1;
            _rt.anchoredPosition = p1;
            yield break;
        }

        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime; // ✅ UI通常用unscaled，避免你暂停游戏时不播
            float k = Mathf.Clamp01(t / dur);

            // 简单 ease-out
            float e = 1f - Mathf.Pow(1f - k, 3f);

            canvasGroup.alpha = Mathf.Lerp(a0, a1, e);
            _rt.anchoredPosition = Vector2.Lerp(p0, p1, e);
            yield return null;
        }

        canvasGroup.alpha = a1;
        _rt.anchoredPosition = p1;
    }

    public void SetBasePosition(Vector2 anchoredPos)
    {
        _baseAnchoredPos = anchoredPos;
        if (_rt != null) _rt.anchoredPosition = anchoredPos;
    }

}
