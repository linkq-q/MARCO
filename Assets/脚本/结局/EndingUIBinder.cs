using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class EndingUIBinder : MonoBehaviour
{
    [Header("Refs")]
    public EndingRequester requester;
    public GameObject uiRoot;

    [Header("Display Root")]
    [Tooltip("Current ending display container. A plain full-screen RectTransform is enough; no ScrollRect needed.")]
    public RectTransform content;
    [FormerlySerializedAs("scroll")]
    [Tooltip("Legacy only. No longer used by the ending layout.")]
    public ScrollRect scroll;

    [Header("Paragraph Display")]
    public GameObject paragraphBlockPrefab;

    [Header("Legacy Fallback")]
    [Tooltip("Imported from old AITakeoverEndingFlow. If no paragraphBlockPrefab is set, this text prefab will still display ending text.")]
    public GameObject fallbackTextPrefab;
    [Tooltip("Legacy only. Kept for inspector compatibility.")]
    public bool scrollToTopOnStart = true;

    [Header("Waiting Placeholder")]
    public GameObject waitingTextGO;

    [Header("Controls")]
    public Button generateButton;
    public GameObject loadingGO;

    [Header("Inspector Trigger")]
    public bool generateOnce;

    [Header("Options")]
    public bool clearBeforeShow = true;

    [Header("Typewriter")]
    [Range(0f, 0.2f)] public float charDelay = 0.04f;

    [Header("Paragraph Timing")]
    [Tooltip("Seconds to hold the completed paragraph on screen before switching to the next one.")]
    [Range(0f, 10f)] public float pauseAfterParagraphSeconds = 2f;

    [Header("Image Generation")]
    public EndingImageRequester imageRequester;
    public bool enableImageGeneration = true;
    [Range(1, 5)] public int imageEveryNParagraphs = 1;

    [Header("Debug")]
    public bool logDebug;

    bool _busy;
    bool _pendingGenerate;
    bool _processingQueue;
    bool _streamDone;
    bool _destroyed;
    int _paragraphCount;
    GameObject _currentVisual;
    RectTransform _runtimeStage;

    readonly Queue<ParagraphJob> _paragraphQueue = new Queue<ParagraphJob>();

    void Awake()
    {
        ImportLegacyBindings(FindLegacyFlow());

        if (loadingGO) loadingGO.SetActive(false);
        if (!requester) requester = FindFirstObjectByType<EndingRequester>();

        if (generateButton)
        {
            generateButton.onClick.RemoveListener(OnClickGenerate);
            generateButton.onClick.AddListener(OnClickGenerate);
        }

        SetWaitingVisible(false);
    }

    void OnDestroy()
    {
        _destroyed = true;

        if (generateButton)
            generateButton.onClick.RemoveListener(OnClickGenerate);

        UnwireStreamEvents();
    }

    void OnValidate()
    {
        ImportLegacyBindings(FindLegacyFlow());

        if (!generateOnce) return;

        if (!Application.isPlaying)
        {
            generateOnce = false;
#if UNITY_EDITOR
            Debug.LogWarning("[EndingUIBinder] generateOnce only works in Play Mode.");
#endif
            return;
        }

        _pendingGenerate = true;
    }

    void Update()
    {
        if (_pendingGenerate && !_busy)
        {
            _pendingGenerate = false;
            generateOnce = false;
            _ = GenerateFlowAsync();
        }
    }

    public void ImportLegacyBindings(AITakeoverEndingFlow legacy)
    {
        if (!legacy) return;

        if (!requester) requester = legacy.requester;
        if (!uiRoot) uiRoot = legacy.uiRoot;
        if (!content) content = legacy.content;
        if (!loadingGO) loadingGO = legacy.loadingGO;

        clearBeforeShow = legacy.clearBeforeShow;
        scrollToTopOnStart = legacy.scrollToTop;

        if (!content && uiRoot)
            content = uiRoot.GetComponent<RectTransform>();

        if (!fallbackTextPrefab) fallbackTextPrefab = legacy.endingTextPrefab;

        if (!paragraphBlockPrefab && legacy.endingTextPrefab)
        {
            if (legacy.endingTextPrefab.GetComponent<EndingParagraphBlock>() ||
                legacy.endingTextPrefab.GetComponentInChildren<EndingParagraphBlock>(true))
            {
                paragraphBlockPrefab = legacy.endingTextPrefab;
            }
        }
    }

    AITakeoverEndingFlow FindLegacyFlow()
    {
        var local = GetComponent<AITakeoverEndingFlow>();
        if (local) return local;

        var all = FindObjectsByType<AITakeoverEndingFlow>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (all == null || all.Length == 0) return null;

        foreach (var candidate in all)
        {
            if (!candidate) continue;

            if (requester && candidate.requester == requester)
                return candidate;

            if (content && candidate.content == content)
                return candidate;

            if (uiRoot && candidate.uiRoot == uiRoot)
                return candidate;
        }

        return all[0];
    }

    public void PlayTakeoverEnding()
    {
        _ = GenerateFlowAsync();
    }

    void OnClickGenerate()
    {
        _ = GenerateFlowAsync();
    }

    async Task GenerateFlowAsync()
    {
        if (_busy) return;

        _busy = true;
        _streamDone = false;
        SetBusyUI(true);

        try
        {
            ImportLegacyBindings(FindLegacyFlow());
            ValidateRefs();

            if (uiRoot) uiRoot.SetActive(true);

            PrepareRun(clearBeforeShow);
            WireStreamEvents();
            await requester.RequestEndingAsync();
        }
        catch (Exception e)
        {
            Debug.LogError(e, this);
            SetWaitingVisible(false);
            SetBusyUI(false);
            _busy = false;
        }
        finally
        {
            UnwireStreamEvents();
            FinishRunIfComplete();
        }
    }

    void ValidateRefs()
    {
        ResolveParagraphPrefab();
        ResolveDisplayContainer();
        EnsureRuntimeStage();

        if (!requester) throw new Exception("EndingRequester reference is missing.");
        if (!content) throw new Exception("Content reference is missing.");
    }

    RectTransform ResolveDisplayContainer()
    {
        if (content) return content;

        if (uiRoot)
        {
            content = uiRoot.GetComponent<RectTransform>();
            if (content) return content;
        }

        return null;
    }

    GameObject ResolveParagraphPrefab()
    {
        if (paragraphBlockPrefab) return paragraphBlockPrefab;
        if (fallbackTextPrefab) return fallbackTextPrefab;

        var legacy = FindLegacyFlow();
        if (legacy && legacy.endingTextPrefab)
        {
            fallbackTextPrefab = legacy.endingTextPrefab;

            if (legacy.endingTextPrefab.GetComponent<EndingParagraphBlock>() ||
                legacy.endingTextPrefab.GetComponentInChildren<EndingParagraphBlock>(true))
            {
                paragraphBlockPrefab = legacy.endingTextPrefab;
                return paragraphBlockPrefab;
            }

            return fallbackTextPrefab;
        }

        return null;
    }

    void PrepareRun(bool clear)
    {
        ResolveDisplayContainer();
        EnsureRuntimeStage();

        if (clear)
            ClearCurrent();

        _paragraphQueue.Clear();
        _processingQueue = false;
        _paragraphCount = 0;
        _streamDone = false;
        SetWaitingVisible(true);

        if (content)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }
    }

    void WireStreamEvents()
    {
        UnwireStreamEvents();
        requester.OnParagraphReady += OnParagraphReady;
        requester.OnEndingDone += OnStreamDone;
    }

    void UnwireStreamEvents()
    {
        if (!requester) return;

        requester.OnParagraphReady -= OnParagraphReady;
        requester.OnEndingDone -= OnStreamDone;
    }

    void OnParagraphReady(int index, string text)
    {
        _paragraphCount++;

        bool doImage = enableImageGeneration
            && imageRequester != null
            && (_paragraphCount % Mathf.Max(1, imageEveryNParagraphs) == 0);

        Task<Texture2D> imageTask = doImage
            ? imageRequester.GenerateImageAsync(text)
            : Task.FromResult<Texture2D>(null);

        _paragraphQueue.Enqueue(new ParagraphJob(text, imageTask));

        if (!_processingQueue)
            _ = ProcessQueueAsync();
    }

    async Task ProcessQueueAsync()
    {
        if (_processingQueue) return;
        _processingQueue = true;

        try
        {
            while (_paragraphQueue.Count > 0)
            {
                if (_destroyed || this == null) return;

                ParagraphJob job = _paragraphQueue.Dequeue();

                if (_currentVisual == null)
                    SetWaitingVisible(true);

                Texture2D tex = null;
                try
                {
                    tex = await job.ImageTask;
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[EndingUIBinder] image task exception: " + e.Message, this);
                }

                if (_destroyed || this == null) return;

                SetWaitingVisible(false);
                GameObject previousVisual = _currentVisual;
                _currentVisual = null;

                bool typewriterDone = false;
                CreateParagraphVisual(job.Text, tex, () => typewriterDone = true);

                if (previousVisual != null)
                    _ = FadeOutVisualAsync(previousVisual);

                while (!typewriterDone)
                {
                    if (_destroyed || this == null) return;
                    await Task.Yield();
                }

                if (pauseAfterParagraphSeconds > 0f)
                {
                    await WaitRealtimeAsync(pauseAfterParagraphSeconds);
                    if (_destroyed || this == null) return;
                }
            }
        }
        finally
        {
            _processingQueue = false;

            if (_streamDone && _paragraphQueue.Count == 0)
                SetWaitingVisible(false);

            FinishRunIfComplete();
        }
    }

    void CreateParagraphVisual(string text, Texture2D tex, Action onComplete)
    {
        GameObject resolvedPrefab = ResolveParagraphPrefab();

        if (paragraphBlockPrefab && !ShouldUseRuntimeParagraphBlock(paragraphBlockPrefab))
        {
            if (TryCreatePrefabParagraphBlock(paragraphBlockPrefab, text, tex, onComplete))
                return;
        }

        if (resolvedPrefab && !ShouldUseRuntimeParagraphBlock(resolvedPrefab))
        {
            if (TryCreatePrefabParagraphBlock(resolvedPrefab, text, tex, onComplete))
                return;
        }

        if (resolvedPrefab)
        {
            CreateRuntimeParagraphBlock(text, tex, resolvedPrefab, onComplete);
            return;
        }

        CreateDefaultTextVisual(text, onComplete);
    }

    bool ShouldUseRuntimeParagraphBlock(GameObject prefab)
    {
        if (!prefab) return false;

        if (prefab.GetComponent<RectTransform>() == null)
            return true;

        var nestedCanvas = prefab.GetComponentInChildren<Canvas>(true);
        if (nestedCanvas != null && nestedCanvas.gameObject != prefab)
            return true;

        return false;
    }

    bool TryCreatePrefabParagraphBlock(GameObject prefab, string text, Texture2D tex, Action onComplete)
    {
        if (!prefab) return false;

        var parent = GetDisplayParent();
        if (!parent) return false;

        var go = Instantiate(prefab, parent, false);
        _currentVisual = go;
        go.name = prefab.name;
        go.SetActive(true);

        if (go.transform is RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        var block = go.GetComponent<EndingParagraphBlock>() ??
                    go.GetComponentInChildren<EndingParagraphBlock>(true);
        if (!block)
        {
            Destroy(go);
            _currentVisual = null;
            return false;
        }

        block.charDelay = charDelay;
        if (tex != null)
            block.ShowImage(tex);
        else if (block.imageCanvasGroup)
            block.imageCanvasGroup.alpha = 0f;

        block.StartTypewriter(text, onComplete);
        return true;
    }

    void CreateRuntimeParagraphBlock(string text, Texture2D tex, GameObject styleSourcePrefab, Action onComplete)
    {
        var parent = GetDisplayParent();
        var root = new GameObject("EndingParagraphRuntime", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup), typeof(EndingParagraphBlock));
        root.transform.SetParent(parent, false);
        _currentVisual = root;

        var rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.anchoredPosition = Vector2.zero;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        var rootImage = root.GetComponent<Image>();
        rootImage.color = Color.black;
        rootImage.raycastTarget = false;

        var rootCanvasGroup = root.GetComponent<CanvasGroup>();
        rootCanvasGroup.alpha = 1f;

        var movieGO = new GameObject("MovieFrame", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage), typeof(CanvasGroup));
        movieGO.transform.SetParent(root.transform, false);

        var movieRect = movieGO.GetComponent<RectTransform>();
        movieRect.anchorMin = Vector2.zero;
        movieRect.anchorMax = Vector2.one;
        movieRect.pivot = new Vector2(0.5f, 0.5f);
        movieRect.anchoredPosition = Vector2.zero;
        movieRect.offsetMin = new Vector2(0f, 0f);
        movieRect.offsetMax = new Vector2(0f, 0f);

        var rawImage = movieGO.GetComponent<RawImage>();
        rawImage.color = Color.white;
        rawImage.raycastTarget = false;

        var imageCanvasGroup = movieGO.GetComponent<CanvasGroup>();
        imageCanvasGroup.alpha = 0f;

        var vignetteGO = new GameObject("VignetteOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        vignetteGO.transform.SetParent(movieGO.transform, false);

        var vignetteRect = vignetteGO.GetComponent<RectTransform>();
        vignetteRect.anchorMin = Vector2.zero;
        vignetteRect.anchorMax = Vector2.one;
        vignetteRect.pivot = new Vector2(0.5f, 0.5f);
        vignetteRect.offsetMin = Vector2.zero;
        vignetteRect.offsetMax = Vector2.zero;

        var vignetteImage = vignetteGO.GetComponent<Image>();
        vignetteImage.raycastTarget = false;

        var gradientGO = new GameObject("BottomGradient", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        gradientGO.transform.SetParent(movieGO.transform, false);

        var gradientRect = gradientGO.GetComponent<RectTransform>();
        gradientRect.anchorMin = new Vector2(0f, 0f);
        gradientRect.anchorMax = new Vector2(1f, 0.45f);
        gradientRect.pivot = new Vector2(0.5f, 0f);
        gradientRect.offsetMin = Vector2.zero;
        gradientRect.offsetMax = Vector2.zero;

        var gradientImage = gradientGO.GetComponent<Image>();
        gradientImage.raycastTarget = false;

        var bodyGO = new GameObject("BodyText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        bodyGO.transform.SetParent(gradientGO.transform, false);

        var bodyRect = bodyGO.GetComponent<RectTransform>();
        bodyRect.anchorMin = Vector2.zero;
        bodyRect.anchorMax = Vector2.one;
        bodyRect.pivot = new Vector2(0.5f, 0f);
        bodyRect.offsetMin = Vector2.zero;
        bodyRect.offsetMax = Vector2.zero;

        var bodyTMP = bodyGO.GetComponent<TextMeshProUGUI>();
        ApplyTextStyle(bodyTMP, styleSourcePrefab);

        var block = root.GetComponent<EndingParagraphBlock>();
        block.bodyTMP = bodyTMP;
        block.movieFrame = rawImage;
        block.bottomGradient = gradientImage;
        block.vignetteOverlay = vignetteImage;
        block.imageCanvasGroup = imageCanvasGroup;
        block.charDelay = charDelay;

        CopyImageStyle(block, styleSourcePrefab);

        if (tex != null)
        {
            block.ShowImage(tex);
        }
        else
        {
            movieGO.SetActive(false);

            var fullTextRect = bodyGO.GetComponent<RectTransform>();
            fullTextRect.SetParent(root.transform, false);
            fullTextRect.anchorMin = new Vector2(0f, 0f);
            fullTextRect.anchorMax = new Vector2(1f, 1f);
            fullTextRect.pivot = new Vector2(0.5f, 0.5f);
            fullTextRect.offsetMin = new Vector2(64f, 64f);
            fullTextRect.offsetMax = new Vector2(-64f, -64f);
        }

        block.StartTypewriter(text, onComplete);
    }

    void ApplyTextStyle(TextMeshProUGUI target, GameObject styleSourcePrefab)
    {
        if (!target) return;

        TextMeshProUGUI source = null;
        if (styleSourcePrefab)
        {
            var sourceBlock = styleSourcePrefab.GetComponent<EndingParagraphBlock>() ??
                              styleSourcePrefab.GetComponentInChildren<EndingParagraphBlock>(true);
            if (sourceBlock && sourceBlock.bodyTMP)
                source = sourceBlock.bodyTMP;

            if (!source)
                source = styleSourcePrefab.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        if (source)
        {
            target.font = source.font;
            target.fontSharedMaterial = source.fontSharedMaterial;
            target.fontSize = source.fontSize;
            target.color = Color.white;
            target.alignment = TextAlignmentOptions.BottomLeft;
            target.enableWordWrapping = source.enableWordWrapping;
            target.lineSpacing = Mathf.Max(4f, source.lineSpacing);
            target.characterSpacing = source.characterSpacing;
        }
        else
        {
            target.fontSize = 44f;
            target.color = Color.white;
            target.alignment = TextAlignmentOptions.BottomLeft;
            target.enableWordWrapping = true;
            target.lineSpacing = 4f;
        }

        target.text = string.Empty;
        target.raycastTarget = false;
        target.overflowMode = TextOverflowModes.Overflow;
        target.margin = new Vector4(40f, 20f, 40f, 24f);
    }

    void CopyImageStyle(EndingParagraphBlock targetBlock, GameObject styleSourcePrefab)
    {
        if (targetBlock == null || !styleSourcePrefab) return;

        var sourceBlock = styleSourcePrefab.GetComponent<EndingParagraphBlock>() ??
                          styleSourcePrefab.GetComponentInChildren<EndingParagraphBlock>(true);
        if (!sourceBlock) return;

        targetBlock.blurMaterial = sourceBlock.blurMaterial;
        targetBlock.useBlurredTextureFallback = sourceBlock.useBlurredTextureFallback;
        targetBlock.blurDownsample = sourceBlock.blurDownsample;
        targetBlock.blurIterations = sourceBlock.blurIterations;
        targetBlock.blurRadius = sourceBlock.blurRadius;
    }

    void CreateDefaultTextVisual(string text, Action onComplete)
    {
        var go = new GameObject("EndingParagraphText", typeof(RectTransform), typeof(LayoutElement));
        go.transform.SetParent(GetDisplayParent(), false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = Vector2.zero;

        var layout = go.GetComponent<LayoutElement>();
        layout.flexibleWidth = 1f;
        layout.minHeight = 80f;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text ?? string.Empty;
        tmp.fontSize = 48f;
        tmp.color = Color.white;
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.margin = new Vector4(24f, 12f, 24f, 12f);

        onComplete?.Invoke();

        if (logDebug)
            Debug.LogWarning("[EndingUIBinder] No paragraph prefab configured. Using runtime TMP fallback.", this);
    }

    void OnStreamDone(string full)
    {
        _streamDone = true;
        if (logDebug) Debug.Log("[EndingUIBinder] stream done.", this);
        FinishRunIfComplete();
    }

    void FinishRunIfComplete()
    {
        if (!_streamDone || _processingQueue || _paragraphQueue.Count > 0)
            return;

        SetWaitingVisible(false);
        SetBusyUI(false);
        _busy = false;
    }

    void SetBusyUI(bool busy)
    {
        if (generateButton) generateButton.interactable = !busy;
        if (loadingGO) loadingGO.SetActive(busy);
    }

    void SetWaitingVisible(bool visible)
    {
        if (waitingTextGO)
            waitingTextGO.SetActive(visible);
    }

    public void ClearCurrent()
    {
        ResolveDisplayContainer();
        EnsureRuntimeStage();

        _paragraphQueue.Clear();
        _processingQueue = false;
        _paragraphCount = 0;
        SetWaitingVisible(false);

        if (_currentVisual)
        {
            Destroy(_currentVisual);
            _currentVisual = null;
        }

        var displayParent = GetDisplayParent();
        if (!displayParent) return;
        for (int i = displayParent.childCount - 1; i >= 0; i--)
        {
            Transform child = displayParent.GetChild(i);
            Destroy(child.gameObject);
        }
    }

    RectTransform GetDisplayParent()
    {
        EnsureRuntimeStage();
        return _runtimeStage;
    }

    void EnsureRuntimeStage()
    {
        if (_runtimeStage) return;

        RectTransform host = GetComponent<RectTransform>();
        if (!host) return;

        Transform existing = host.Find("RuntimeStage");
        if (existing is RectTransform existingRect)
        {
            _runtimeStage = existingRect;
            ConfigureRuntimeStage(_runtimeStage);
            return;
        }

        var go = new GameObject("RuntimeStage", typeof(RectTransform));
        go.transform.SetParent(host, false);
        _runtimeStage = go.GetComponent<RectTransform>();
        ConfigureRuntimeStage(_runtimeStage);
    }

    static void ConfigureRuntimeStage(RectTransform stage)
    {
        if (!stage) return;

        stage.anchorMin = Vector2.zero;
        stage.anchorMax = Vector2.one;
        stage.pivot = new Vector2(0.5f, 0.5f);
        stage.anchoredPosition = Vector2.zero;
        stage.offsetMin = Vector2.zero;
        stage.offsetMax = Vector2.zero;
        stage.localScale = Vector3.one;
    }

    async Task FadeOutCurrentVisualAsync()
    {
        var current = _currentVisual;
        _currentVisual = null;
        await FadeOutVisualAsync(current);
    }

    async Task FadeOutVisualAsync(GameObject target)
    {
        if (!target) return;

        var canvasGroup = target.GetComponent<CanvasGroup>();
        if (!canvasGroup)
            canvasGroup = target.AddComponent<CanvasGroup>();

        const float duration = 0.5f;
        float elapsed = 0f;
        float startAlpha = canvasGroup.alpha;

        while (elapsed < duration)
        {
            if (!target) return;

            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, Mathf.Clamp01(elapsed / duration));
            await Task.Yield();
        }

        if (target)
            Destroy(target);
    }

    async Task WaitRealtimeAsync(float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            if (_destroyed || this == null) return;
            elapsed += Time.unscaledDeltaTime;
            await Task.Yield();
        }
    }

    sealed class ParagraphJob
    {
        public readonly string Text;
        public readonly Task<Texture2D> ImageTask;

        public ParagraphJob(string text, Task<Texture2D> imageTask)
        {
            Text = text;
            ImageTask = imageTask;
        }
    }
}
