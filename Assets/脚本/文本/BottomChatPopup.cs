using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class BottomChatPopup : MonoBehaviour
{
    public enum State { Hidden, AIText, PlayerText, InputMode }

    [Header("Refs")]
    public CanvasGroup rootGroup;          // 拖：整个底部弹窗（背景+文字）的 CanvasGroup（必须是共同父节点）
    public GameObject aiTextRoot;
    public TextMeshProUGUI aiText;
    public GameObject playerTextRoot;
    public TextMeshProUGUI playerText;
    public GameObject inputRoot;
    public TMP_InputField inputField;

    [Header("Hotkeys")]
    public KeyCode openKey = KeyCode.LeftShift;
    public KeyCode closeKey = KeyCode.Escape;
    public bool shiftIsToggle = true;

    [Header("Timing")]
    public float showSeconds = 2.0f;

    [Tooltip("隐藏时淡出时间（0=立刻隐藏）。用它保证背景和文字同步消失")]
    public float fadeOutSeconds = 0.15f;

    [Header("AI Binding")]
    public bool autoBindToAIBroker = true;
    public bool pauseAutoTalkWhileTyping = true;
    public bool debugSubmitLog = true;

    [Tooltip("AI 文本显示滞后：先等这段时间再把文字弹出来（用于等语音/tts）")]
    public float aiShowDelaySeconds = 0.0f;

    public Action<string> OnPlayerSubmit;

    public GameObject thinkingRoot;

    public StoryTaskManager storyTaskManager;

    State state = State.Hidden;
    Coroutine hideCo;
    Coroutine fadeCo;

    bool _boundToBroker;
    Coroutine _bindCo;
    Coroutine aiDelayCo;

    bool endingLocked;


    void Awake()
    {
        // 初始：全部隐藏
        SetAllRoots(false, false, false);
        SetVisibleInstant(false);

        if (aiText) aiText.text = "";
        if (playerText) playerText.text = "";
    }

    void OnEnable()
    {
        if (autoBindToAIBroker)
        {
            if (_bindCo != null) StopCoroutine(_bindCo);
            _bindCo = StartCoroutine(BindToBrokerWhenReady());
        }
    }

    void OnDisable()
    {
        if (_bindCo != null)
        {
            StopCoroutine(_bindCo);
            _bindCo = null;
        }

        if (pauseAutoTalkWhileTyping && AIBroker.Instance != null)
            AIBroker.Instance.SetAutoTalkPaused(false);
    }

    // 思考ui
    public void SetThinking(bool on)
    {
        if (thinkingRoot)
            thinkingRoot.SetActive(on);
    }


    IEnumerator BindToBrokerWhenReady()
    {
        while (AIBroker.Instance == null)
            yield return null;

        if (_boundToBroker) yield break;

        OnPlayerSubmit += AIBroker.Instance.OnUserSend;
        _boundToBroker = true;

        if (debugSubmitLog)
            Debug.Log("[BottomChatPopup] Bound: OnPlayerSubmit -> AIBroker.OnUserSend");
    }

    void Update() => HandleHotkey();

    void HandleHotkey()
    {
        if (endingLocked) return;
        if (shiftIsToggle)
        {
            if (Input.GetKeyDown(openKey))
            {
                if (state == State.InputMode) CloseInput();
                else OpenInput();
            }
        }
        else
        {
            if (Input.GetKeyDown(openKey)) OpenInput();
            if (state == State.InputMode && Input.GetKeyUp(openKey)) CloseInput();
        }

        if (state == State.InputMode && Input.GetKeyDown(closeKey))
            CloseInput();
    }

    // ======= 对外：显示AI =======
    public void ShowAI(string msg)
    {
        StopHideAndFade();

        if (aiDelayCo != null) { StopCoroutine(aiDelayCo); aiDelayCo = null; }

        aiDelayCo = StartCoroutine(CoShowAIDelayed(msg));

    }
    //延迟
    IEnumerator CoShowAIDelayed(string msg)
    {
        if (aiShowDelaySeconds > 0f)
            yield return new WaitForSecondsRealtime(aiShowDelaySeconds);

        SetThinking(false);

        state = State.AIText;

        SetVisibleInstant(true);
        SetAllRoots(ai: true, player: false, input: false);

        SetTextWithOptionalTypewriter(aiText, msg);

        hideCo = StartCoroutine(HideAfter(showSeconds));
        aiDelayCo = null;
    }


    // ======= 对外：显示玩家一句 =======
    public void ShowPlayerOnce(string msg)
    {
        StopHideAndFade();

        state = State.PlayerText;

        SetVisibleInstant(true);
        SetAllRoots(ai: false, player: true, input: false);

        SafeSetText(playerText, msg);

        hideCo = StartCoroutine(HideAfter(showSeconds));
    }

    // ======= 输入态 =======
    void OpenInput()
    {
        StopHideAndFade();

        state = State.InputMode;

        SetVisibleInstant(true);
        SetAllRoots(ai: false, player: false, input: true);

        if (pauseAutoTalkWhileTyping && AIBroker.Instance != null)
            AIBroker.Instance.SetAutoTalkPaused(true);

        if (rootGroup)
        {
            rootGroup.blocksRaycasts = true;
            rootGroup.interactable = true;
        }

        if (!EventSystem.current)
            Debug.LogError("[BottomChatPopup] No EventSystem in scene! UI input won't work.");

        if (inputField)
        {
            inputField.onSubmit.RemoveListener(OnSubmit);
            inputField.onSubmit.AddListener(OnSubmit);

            inputField.text = "";
            inputField.interactable = true;
            inputField.enabled = true;
            inputField.gameObject.SetActive(true);

            EventSystem.current?.SetSelectedGameObject(inputField.gameObject);
            inputField.Select();
            inputField.ActivateInputField();
        }
        else
        {
            Debug.LogError("[BottomChatPopup] inputField is NULL");
        }
    }

    void CloseInput()
    {
        if (inputField)
            inputField.onSubmit.RemoveListener(OnSubmit);

        if (pauseAutoTalkWhileTyping && AIBroker.Instance != null)
            AIBroker.Instance.SetAutoTalkPaused(false);

        // ✅ 关键：不要直接 SetAllRoots(false...) 立刻让文字消失
        // 统一走 FadeOut，保证背景和文字同步
        state = State.Hidden;
        StartFadeOutAndHide();
    }

    void OnSubmit(string _)
    {
        string msg = inputField ? inputField.text.Trim() : "";
        if (string.IsNullOrEmpty(msg)) return;

        if (inputField) inputField.text = "";

        ShowPlayerOnce(msg);

        if (debugSubmitLog)
        {
            int handlers = OnPlayerSubmit?.GetInvocationList()?.Length ?? 0;
            Debug.Log($"[BottomChatPopup] Submit player msg='{msg}' handlers={handlers} boundToBroker={_boundToBroker} broker={(AIBroker.Instance ? "OK" : "NULL")}");
        }

        try
        {
            OnPlayerSubmit?.Invoke(msg);

            if (debugSubmitLog)
                Debug.Log("[BottomChatPopup] OnPlayerSubmit Invoke DONE");
        }
        catch (Exception e)
        {
            Debug.LogError("[BottomChatPopup] OnPlayerSubmit Invoke ERROR: " + e);
        }
    }

    IEnumerator HideAfter(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        state = State.Hidden;
        StartFadeOutAndHide();
        hideCo = null;
    }

    void StopHideAndFade()
    {
        if (hideCo != null)
        {
            StopCoroutine(hideCo);
            hideCo = null;
        }

        if (fadeCo != null)
        {
            StopCoroutine(fadeCo);
            fadeCo = null;
        }

        // 如果之前淡出了，把 alpha 拉回 1（显示时必须这样）
        if (rootGroup) rootGroup.alpha = 1f;

        // 输入态外应不可交互
        if (rootGroup)
        {
            bool interact = state == State.InputMode;
            rootGroup.blocksRaycasts = interact;
            rootGroup.interactable = interact;
        }

        // ✅ 停掉打字机（防止淡出/隐藏时还在改 maxVisibleCharacters）
        StopTypewriterForHide(aiText);
        StopTypewriterForHide(playerText);
    }

    void StartFadeOutAndHide()
    {
        // 先禁用输入交互
        if (rootGroup)
        {
            rootGroup.blocksRaycasts = false;
            rootGroup.interactable = false;
        }

        // ✅ 停掉打字机，避免“闪/不同步”
        StopTypewriterForHide(aiText);
        StopTypewriterForHide(playerText);

        if (fadeOutSeconds <= 0f || !rootGroup)
        {
            // 立刻隐藏
            SetAllRoots(false, false, false);
            SetVisibleInstant(false);
            return;
        }

        fadeCo = StartCoroutine(FadeOutThenHide(fadeOutSeconds));
    }

    IEnumerator FadeOutThenHide(float seconds)
    {
        float start = rootGroup.alpha;
        float t = 0f;

        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / seconds);
            rootGroup.alpha = Mathf.Lerp(start, 0f, k);
            yield return null;
        }

        rootGroup.alpha = 0f;

        // ✅ 淡出完成后再关 root，保证背景和文字同步消失
        SetAllRoots(false, false, false);
        SetVisibleInstant(false);

        fadeCo = null;
    }

    void SetVisibleInstant(bool v)
    {
        if (!rootGroup) return;

        rootGroup.alpha = v ? 1f : 0f;

        bool interact = v && state == State.InputMode;
        rootGroup.blocksRaycasts = interact;
        rootGroup.interactable = interact;
    }

    void SetAllRoots(bool ai, bool player, bool input)
    {
        if (aiTextRoot) aiTextRoot.SetActive(ai);
        if (playerTextRoot) playerTextRoot.SetActive(player);
        if (inputRoot) inputRoot.SetActive(input);
    }

    static void SafeSetText(TextMeshProUGUI t, string msg)
    {
        if (!t) return;

        t.gameObject.SetActive(true);
        t.text = msg;

        t.maxVisibleCharacters = int.MaxValue;
        t.ForceMeshUpdate();

        var c = t.color;
        if (c.a < 0.01f) { c.a = 1f; t.color = c; }
    }

    void SetTextWithOptionalTypewriter(TextMeshProUGUI t, string msg)
    {
        if (!t) return;

        t.gameObject.SetActive(true);
        t.text = msg;

        // ✅ 关键：无论是否打字机，都在这里登记 AI 发言
        if (storyTaskManager != null && !string.IsNullOrWhiteSpace(msg))
            storyTaskManager.RegisterAIDialogue(msg);

        var tw = t.GetComponent<TypewriterText>();
        if (tw != null)
        {
            // ✅ 如果之前为了隐藏临时禁用了，这里重新启用
            if (!tw.enabled) tw.enabled = true;

            // 确保 tw.text 指向正确对象
            if (tw.text != t) tw.text = t;

            // 立即隐藏（防止上一句残留）
            t.maxVisibleCharacters = 0;
            t.ForceMeshUpdate();

            tw.Play();
            return;
        }

        // 没有打字机：直接显示全文
        t.maxVisibleCharacters = int.MaxValue;
        t.ForceMeshUpdate();

        var c = t.color;
        if (c.a < 0.01f) { c.a = 1f; t.color = c; }
    }


    static void StopTypewriterForHide(TextMeshProUGUI t)
    {
        if (!t) return;
        var tw = t.GetComponent<TypewriterText>();
        if (tw != null && tw.enabled)
        {
            // ✅ 目的：立刻停掉打字机更新，避免淡出期间继续改 maxVisibleCharacters
            tw.enabled = false;
        }
    }

    public void OnEndingLock()
    {
        endingLocked = true;

        // 停协程
        if (hideCo != null) { StopCoroutine(hideCo); hideCo = null; }
        if (fadeCo != null) { StopCoroutine(fadeCo); fadeCo = null; }
        if (aiDelayCo != null) { StopCoroutine(aiDelayCo); aiDelayCo = null; }
        if (_bindCo != null) { StopCoroutine(_bindCo); _bindCo = null; }

        // 解绑提交（防止结局仍能发消息）
        if (_boundToBroker && AIBroker.Instance != null)
        {
            OnPlayerSubmit -= AIBroker.Instance.OnUserSend;
            _boundToBroker = false;
        }

        // 退出输入态：保证 AutoTalk 不被“卡在暂停”
        if (pauseAutoTalkWhileTyping && AIBroker.Instance != null)
            AIBroker.Instance.SetAutoTalkPaused(false);

        // 关闭输入监听
        if (inputField)
        {
            inputField.onSubmit.RemoveListener(OnSubmit);
            inputField.DeactivateInputField();
            inputField.interactable = false;
        }

        // 立刻隐藏视觉
        state = State.Hidden;
        SetThinking(false);
        SetAllRoots(false, false, false);
        SetVisibleInstant(false);
    }

    public void OnEndingUnlock()
    {
        endingLocked = false;
    }

}
