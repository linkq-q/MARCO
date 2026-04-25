using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIHintManager : MonoBehaviour
{
    public static UIHintManager I { get; private set; }

    [Header("Fixed Hint GameObjects (Inspector拖拽赋值 → UI/拾取提示/)")]
    public CanvasGroup hintGroup_TAB;       // HintText_TAB
    public CanvasGroup hintGroup_Interact;  // HintText_Interact
    public CanvasGroup hintGroup_Space;     // HintText_Space

    [Header("Timing")]
    public float task4HintDelay = 1f;
    public float wakeDialogueHintDelay = 3f;

    [Header("Visual")]
    public float fadeSeconds = 0.2f;

    [Header("Refs")]
    public StoryTaskManager storyTaskManager;

    readonly HashSet<string> _shownIds = new HashSet<string>();
    Coroutine _coTAB, _coInteract, _coSpace;

    bool _firstDialogueOccurred;
    bool _spaceHintDismissed;

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        HideAllImmediate();
    }

    void OnEnable() => BindStoryTaskManager();

    void Start()
    {
        BindStoryTaskManager();
        ShowSpaceOnce("wake_dialogue", wakeDialogueHintDelay);
    }

    void OnDisable()
    {
        if (storyTaskManager != null)
            storyTaskManager.OnTaskEntered -= OnTaskEntered;
    }

    void Update()
    {
        TryDismiss(hintGroup_TAB, ref _coTAB, KeyCode.Tab);
        TryDismissSpace();
    }

    // ========================
    // 外部调用接口
    // ========================

    public void SetFirstDialogueOccurred()
    {
        _firstDialogueOccurred = true;
    }

    public void NotifyFirstHighlightEntered()
    {
        if (_coInteract != null) StopCoroutine(_coInteract);
        _coInteract = StartCoroutine(CoFadeOut(hintGroup_Interact));
    }

    public void HideHint()
    {
        StartFadeOut(hintGroup_TAB, ref _coTAB);
        StartFadeOut(hintGroup_Interact, ref _coInteract);
        StartFadeOut(hintGroup_Space, ref _coSpace);
    }

    public void ShowHint(string text, KeyCode dismissKey, float delay = 0f)
    {
        ShowInteractOnce("hint_generic_" + text.GetHashCode(), delay);
    }

    public void ShowHintOnce(string id, string text, KeyCode dismissKey, float delay = 0f)
    {
        if (id == "task4_panel")
            ShowTABOnce(id, delay);
        else if (id == "highlight_interact")
            ShowInteractOnce(id, delay);
        else if (id == "wake_dialogue")
            ShowSpaceOnce(id, delay);
        else
            ShowInteractOnce(id, delay);
    }

    // ========================
    // 内部：按类型显示一次
    // ========================

    void OnTaskEntered(int taskIndex)
    {
        if (taskIndex == 4)
            ShowTABOnce("task4_panel", task4HintDelay);
    }

    void ShowTABOnce(string id, float delay)
    {
        if (_shownIds.Contains(id)) return;
        _shownIds.Add(id);
        StartFadeIn(hintGroup_TAB, ref _coTAB, delay);
    }

    void ShowInteractOnce(string id, float delay)
    {
        if (_shownIds.Contains(id)) return;
        _shownIds.Add(id);
        StartFadeIn(hintGroup_Interact, ref _coInteract, delay);
    }

    void ShowSpaceOnce(string id, float delay)
    {
        if (_shownIds.Contains(id)) return;
        _shownIds.Add(id);
        StartFadeIn(hintGroup_Space, ref _coSpace, delay);
    }

    // ========================
    // 内部：Space提示消失后延迟开放Interact权限
    // ========================

    void TryDismissSpace()
    {
        if (hintGroup_Space == null || !(hintGroup_Space.alpha > 0.01f)) return;
        if (Input.GetKeyDown(KeyCode.Space))
        {
            StartFadeOut(hintGroup_Space, ref _coSpace);
            StartCoroutine(EnableInteractAfterDelay(1f));
        }
    }

    IEnumerator EnableInteractAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        _spaceHintDismissed = true;
        ShowInteractOnce("highlight_interact", 0f);
    }

    // ========================
    // 内部：淡入 / 淡出 / 立即隐藏
    // ========================

    void TryDismiss(CanvasGroup cg, ref Coroutine field, KeyCode key)
    {
        if (cg == null || !(cg.alpha > 0.01f)) return;
        if (Input.GetKeyDown(key))
            StartFadeOut(cg, ref field);
    }

    void StartFadeIn(CanvasGroup cg, ref Coroutine field, float delay)
    {
        if (cg == null) return;
        if (field != null) StopCoroutine(field);
        cg.gameObject.SetActive(true);
        field = StartCoroutine(CoFadeIn(cg, delay));
    }

    void StartFadeOut(CanvasGroup cg, ref Coroutine field)
    {
        if (cg == null || cg.alpha < 0.01f) return;
        if (field != null) StopCoroutine(field);
        field = StartCoroutine(CoFadeOut(cg));
    }

    IEnumerator CoFadeIn(CanvasGroup cg, float delay)
    {
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
        float t = 0f;
        float dur = Mathf.Max(0.01f, fadeSeconds);
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = t / dur;
            yield return null;
        }
        cg.alpha = 1f;
    }

    IEnumerator CoFadeOut(CanvasGroup cg)
    {
        float start = cg.alpha;
        float t = 0f;
        float dur = Mathf.Max(0.01f, fadeSeconds);
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(start, 0f, t / dur);
            yield return null;
        }
        cg.alpha = 0f;
        cg.gameObject.SetActive(false);
    }

    void HideAllImmediate()
    {
        HideImmediate(hintGroup_TAB);
        HideImmediate(hintGroup_Interact);
        HideImmediate(hintGroup_Space);
    }

    void HideImmediate(CanvasGroup cg)
    {
        if (cg == null) return;
        cg.alpha = 0f;
        cg.gameObject.SetActive(false);
    }

    void BindStoryTaskManager()
    {
        if (storyTaskManager == null)
            storyTaskManager = FindFirstObjectByType<StoryTaskManager>();
        if (storyTaskManager == null) return;
        storyTaskManager.OnTaskEntered -= OnTaskEntered;
        storyTaskManager.OnTaskEntered += OnTaskEntered;
    }
}