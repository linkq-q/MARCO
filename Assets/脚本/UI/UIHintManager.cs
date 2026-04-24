using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class UIHintManager : MonoBehaviour
{
    public static UIHintManager I { get; private set; }

    [Header("Refs")]
    public CanvasGroup hintGroup;
    public TextMeshProUGUI hintText;
    public Animator breathAnimator;
    public StoryTaskManager storyTaskManager;

    [Header("Preset Hints")]
    public string task4Hint = "TAB 查看面板";
    public float task4HintDelay = 1f;
    public string highlightHint = "靠近高亮物体 E 获取线索";
    public string wakeDialogueHint = "空格 唤醒对话";
    public float wakeDialogueHintDelay = 3f;

    [Header("Visual")]
    public float fadeSeconds = 0.2f;
    public float fallbackMinAlpha = 0.3f;
    public float fallbackMaxAlpha = 1.0f;
    public float fallbackBreathPeriod = 2f;

    readonly HashSet<string> _shownIds = new HashSet<string>();
    Coroutine _showCo;
    Coroutine _fadeCo;
    KeyCode _dismissKey;
    bool _visible;
    float _fallbackBreathTime;

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;

        EnsureRuntimeUi();
        HideImmediate();
    }

    void OnEnable()
    {
        BindStoryTaskManager();
    }

    void Start()
    {
        BindStoryTaskManager();
        ShowHintOnce("wake_dialogue", wakeDialogueHint, KeyCode.Space, wakeDialogueHintDelay);
    }

    void OnDisable()
    {
        if (storyTaskManager != null)
            storyTaskManager.OnTaskEntered -= OnTaskEntered;
    }

    void Update()
    {
        if (_visible && Input.GetKeyDown(_dismissKey))
            HideHint();

        if (_visible && breathAnimator == null && hintGroup != null)
        {
            _fallbackBreathTime += Time.unscaledDeltaTime;
            float period = Mathf.Max(0.01f, fallbackBreathPeriod);
            float t = Mathf.PingPong(_fallbackBreathTime, period * 0.5f) / (period * 0.5f);
            hintGroup.alpha = Mathf.Lerp(fallbackMinAlpha, fallbackMaxAlpha, t);
        }
    }

    public void ShowHint(string text, KeyCode dismissKey, float delay = 0f)
    {
        ShowHintInternal(text, dismissKey, delay);
    }

    public void ShowHintOnce(string id, string text, KeyCode dismissKey, float delay = 0f)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            ShowHintInternal(text, dismissKey, delay);
            return;
        }

        if (_shownIds.Contains(id)) return;
        _shownIds.Add(id);
        ShowHintInternal(text, dismissKey, delay);
    }

    public void NotifyFirstHighlightEntered()
    {
        ShowHintOnce("highlight_interact", highlightHint, KeyCode.E, 0f);
    }

    public void HideHint()
    {
        if (_fadeCo != null)
            StopCoroutine(_fadeCo);

        _fadeCo = StartCoroutine(CoFadeOut());
    }

    void OnTaskEntered(int taskIndex)
    {
        if (taskIndex == 4)
            ShowHintOnce("task4_panel", task4Hint, KeyCode.Tab, task4HintDelay);
    }

    void ShowHintInternal(string text, KeyCode dismissKey, float delay)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        EnsureRuntimeUi();

        if (_showCo != null)
            StopCoroutine(_showCo);

        if (_fadeCo != null)
        {
            StopCoroutine(_fadeCo);
            _fadeCo = null;
        }

        _showCo = StartCoroutine(CoShow(text, dismissKey, delay));
    }

    IEnumerator CoShow(string text, KeyCode dismissKey, float delay)
    {
        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);

        if (hintText != null)
            hintText.text = text;

        _dismissKey = dismissKey;
        _visible = true;
        _fallbackBreathTime = 0f;

        if (hintGroup != null)
        {
            hintGroup.gameObject.SetActive(true);
            hintGroup.alpha = breathAnimator != null ? 1f : fallbackMinAlpha;
        }

        if (breathAnimator != null)
        {
            breathAnimator.enabled = true;
            breathAnimator.Play(0, 0, 0f);
        }

        _showCo = null;
    }

    IEnumerator CoFadeOut()
    {
        _visible = false;

        if (breathAnimator != null)
            breathAnimator.enabled = false;

        if (hintGroup == null)
        {
            _fadeCo = null;
            yield break;
        }

        float start = hintGroup.alpha;
        float time = 0f;
        float duration = Mathf.Max(0.01f, fadeSeconds);

        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            hintGroup.alpha = Mathf.Lerp(start, 0f, time / duration);
            yield return null;
        }

        hintGroup.alpha = 0f;
        hintGroup.gameObject.SetActive(false);
        _fadeCo = null;
    }

    void HideImmediate()
    {
        _visible = false;

        if (breathAnimator != null)
            breathAnimator.enabled = false;

        if (hintGroup != null)
        {
            hintGroup.alpha = 0f;
            hintGroup.gameObject.SetActive(false);
        }

        if (hintText != null)
            hintText.text = "";
    }

    void BindStoryTaskManager()
    {
        if (storyTaskManager == null)
            storyTaskManager = FindFirstObjectByType<StoryTaskManager>();

        if (storyTaskManager == null) return;

        storyTaskManager.OnTaskEntered -= OnTaskEntered;
        storyTaskManager.OnTaskEntered += OnTaskEntered;
    }

    void EnsureRuntimeUi()
    {
        if (hintGroup != null && hintText != null) return;

        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = FindFirstObjectByType<Canvas>();

        if (canvas == null) return;

        if (hintGroup == null)
        {
            var root = new GameObject("UIHintRuntime", typeof(RectTransform), typeof(CanvasRenderer), typeof(CanvasGroup));
            root.transform.SetParent(canvas.transform, false);

            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 40f);
            rect.sizeDelta = new Vector2(900f, 120f);

            hintGroup = root.GetComponent<CanvasGroup>();
        }

        if (hintText == null && hintGroup != null)
        {
            var textGo = new GameObject("HintText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(hintGroup.transform, false);

            var rect = textGo.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            hintText = textGo.GetComponent<TextMeshProUGUI>();
            hintText.alignment = TextAlignmentOptions.Center;
            hintText.enableWordWrapping = false;
            hintText.fontSize = 30f;
            hintText.color = Color.white;
            hintText.raycastTarget = false;
        }
    }
}
