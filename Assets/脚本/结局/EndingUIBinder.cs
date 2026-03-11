using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EndingUIBinder : MonoBehaviour
{
    [Header("Refs")]
    public EndingRequester requester;

    [Header("Container")]
    [Tooltip("ScrollRect/Viewport/Content")]
    public RectTransform content;
    public ScrollRect scroll;

    [Header("Text Prefabs")]
    [Tooltip("Preferred paragraph prefab")]
    public GameObject endingTextPrefab;
    [Tooltip("Fallback prefab with TextMeshProUGUI")]
    public GameObject fallbackTextBlockPrefab;

    [Header("Controls")]
    public Button generateButton;
    public GameObject loadingGO;

    [Header("Inspector Trigger")]
    public bool generateOnce = false;

    [Header("Options")]
    public bool clearBeforeShow = true;
    public bool autoScrollToBottomDuringStream = true;
    public int flushEveryNDeltas = 6;

    [Header("Image Generation")]
    public EndingImageRequester imageRequester;
    public GameObject imagePrefab;
    public bool enableImageGeneration = true;
    [Range(1, 10)] public int imageEveryNParagraphs = 1;

    [Header("Debug")]
    public bool logDebug = false;

    bool _busy;
    bool _pendingGenerate;
    bool _hasReceivedStreamText;
    bool _showingLoadingPlaceholder;
    bool _isFinalizingForImageEvent;

    GameObject _current;
    TextMeshProUGUI _tmp;
    EndingEndingPrefabBinder _binder;

    int _deltaCount;
    int _paragraphIndex;
    bool _needFlush;

    readonly StringBuilder _liveParagraphBuffer = new StringBuilder();
    readonly Queue<Transform> _imageAnchorQueue = new Queue<Transform>();

    void Awake()
    {
        if (loadingGO) loadingGO.SetActive(false);
        if (!requester) requester = FindFirstObjectByType<EndingRequester>();

        if (generateButton)
        {
            generateButton.onClick.RemoveListener(OnClickGenerate);
            generateButton.onClick.AddListener(OnClickGenerate);
        }
    }

    void OnDestroy()
    {
        if (generateButton)
            generateButton.onClick.RemoveListener(OnClickGenerate);

        UnwireStreamEvents();
    }

    void OnValidate()
    {
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

        if (_needFlush)
        {
            _needFlush = false;
            FlushLayoutAndScrollToBottom();
        }
    }

    void OnClickGenerate()
    {
        _ = GenerateFlowAsync();
    }

    async Task GenerateFlowAsync()
    {
        if (_busy) return;

        _busy = true;
        SetBusyUI(true);

        try
        {
            ValidateRefs();
            PrepareStreamDisplay(clearBeforeShow);
            WireStreamEvents();

            string ending = await requester.RequestEndingAsync();
            if (logDebug) Debug.Log($"[EndingUIBinder] Stream done, len={ending?.Length ?? 0}", this);
        }
        catch (Exception e)
        {
            UnwireStreamEvents();
            ClearCurrent();
            EnsureActiveTextBlock();
            SetActiveText("Error:\n" + e.Message);
            Debug.LogError(e, this);
        }
        finally
        {
            UnwireStreamEvents();
            SetBusyUI(false);
            _busy = false;
        }
    }

    void ValidateRefs()
    {
        if (!requester) throw new Exception("EndingRequester reference is missing.");
        if (!content) throw new Exception("Content reference is missing.");
        if (!endingTextPrefab && !fallbackTextBlockPrefab)
            throw new Exception("No paragraph text prefab is configured.");
    }

    void SetBusyUI(bool busy)
    {
        if (generateButton) generateButton.interactable = !busy;
        if (loadingGO) loadingGO.SetActive(busy);
    }

    void WireStreamEvents()
    {
        UnwireStreamEvents();

        requester.OnEndingDelta += OnDelta;
        requester.OnEndingDone += OnDone;
        requester.OnParagraphComplete += OnParagraphComplete;

        _deltaCount = 0;
        _needFlush = true;

        if (logDebug) Debug.Log("[EndingUIBinder] Stream events wired.", this);
    }

    void UnwireStreamEvents()
    {
        if (!requester) return;

        requester.OnEndingDelta -= OnDelta;
        requester.OnEndingDone -= OnDone;
        requester.OnParagraphComplete -= OnParagraphComplete;
    }

    void OnDelta(string delta)
    {
        if (string.IsNullOrEmpty(delta)) return;

        if (!_hasReceivedStreamText)
        {
            _hasReceivedStreamText = true;
            _showingLoadingPlaceholder = false;
        }

        _liveParagraphBuffer.Append(delta.Replace("\r", ""));
        RefreshParagraphBlocks();

        _deltaCount++;
        if (autoScrollToBottomDuringStream && (_deltaCount % Mathf.Max(1, flushEveryNDeltas) == 0))
            _needFlush = true;
    }

    void OnDone(string full)
    {
        FinalizeRemainingParagraph();

        if (autoScrollToBottomDuringStream)
            _needFlush = true;

        if (logDebug) Debug.Log("[EndingUIBinder] OnEndingDone received.", this);
    }

    void OnParagraphComplete(string paragraphText)
    {
        _paragraphIndex++;

        Transform anchor = ConsumeParagraphAnchor(paragraphText);

        if (!enableImageGeneration) return;
        if (_paragraphIndex % Mathf.Max(1, imageEveryNParagraphs) != 0) return;
        if (!imageRequester || !imagePrefab) return;

        _ = SpawnImageSlotAsync(paragraphText, anchor);
    }

    void PrepareStreamDisplay(bool clear)
    {
        if (clear) ClearCurrent();
        ResetStreamState();

        EnsureActiveTextBlock();
        SetActiveText("Loading...");
        _showingLoadingPlaceholder = true;
    }

    void ResetStreamState()
    {
        _deltaCount = 0;
        _paragraphIndex = 0;
        _needFlush = false;
        _hasReceivedStreamText = false;
        _showingLoadingPlaceholder = false;
        _isFinalizingForImageEvent = false;
        _liveParagraphBuffer.Length = 0;
        _imageAnchorQueue.Clear();
        ReleaseActiveTextBlock();
    }

    void RefreshParagraphBlocks()
    {
        string buffer = _liveParagraphBuffer.ToString();
        int separatorIndex;

        while ((separatorIndex = buffer.IndexOf("\n\n", StringComparison.Ordinal)) >= 0)
        {
            FinalizeParagraphBlock(buffer.Substring(0, separatorIndex), enqueueForImage: true);
            buffer = buffer.Substring(separatorIndex + 2);
        }

        _liveParagraphBuffer.Length = 0;
        _liveParagraphBuffer.Append(buffer);

        if (_liveParagraphBuffer.Length > 0)
        {
            UpdateLiveParagraphPreview(_liveParagraphBuffer.ToString());
        }
        else if (_current && !_showingLoadingPlaceholder && string.IsNullOrWhiteSpace(GetCurrentText()))
        {
            DestroyCurrentLiveBlock();
        }
    }

    void FinalizeRemainingParagraph()
    {
        if (_liveParagraphBuffer.Length > 0)
        {
            FinalizeParagraphBlock(_liveParagraphBuffer.ToString(), enqueueForImage: false);
            _liveParagraphBuffer.Length = 0;
        }

        if (_current && !_showingLoadingPlaceholder && string.IsNullOrWhiteSpace(GetCurrentText()))
            DestroyCurrentLiveBlock();
    }

    void FinalizeParagraphBlock(string rawParagraph, bool enqueueForImage)
    {
        string paragraphText = NormalizeParagraphText(rawParagraph);
        if (string.IsNullOrEmpty(paragraphText))
        {
            if (_current && !_showingLoadingPlaceholder && string.IsNullOrWhiteSpace(GetCurrentText()))
                DestroyCurrentLiveBlock();
            return;
        }

        EnsureActiveTextBlock();
        SetActiveText(paragraphText);

        if (enqueueForImage && paragraphText.Length > 10)
            _imageAnchorQueue.Enqueue(_current.transform);

        if (!_isFinalizingForImageEvent)
            ReleaseActiveTextBlock();
    }

    void UpdateLiveParagraphPreview(string rawParagraph)
    {
        string previewText = NormalizeLivePreview(rawParagraph);
        if (string.IsNullOrEmpty(previewText))
            return;

        EnsureActiveTextBlock();
        SetActiveText(previewText);
    }

    void EnsureActiveTextBlock()
    {
        if (_current) return;

        GameObject prefabToUse = endingTextPrefab ? endingTextPrefab : fallbackTextBlockPrefab;
        _current = Instantiate(prefabToUse, content);
        _current.SetActive(true);

        _binder = _current.GetComponentInChildren<EndingEndingPrefabBinder>(true);
        _tmp = _current.GetComponentInChildren<TextMeshProUGUI>(true);

        if (_binder == null && _tmp == null)
            throw new Exception("Paragraph prefab is missing TextMeshProUGUI.");
    }

    void SetActiveText(string text)
    {
        if (_binder != null) _binder.Bind(text ?? string.Empty);
        else if (_tmp != null) _tmp.text = text ?? string.Empty;

        _needFlush = true;
    }

    string GetCurrentText()
    {
        return _tmp != null ? _tmp.text : string.Empty;
    }

    void ReleaseActiveTextBlock()
    {
        _current = null;
        _tmp = null;
        _binder = null;
    }

    void DestroyCurrentLiveBlock()
    {
        if (_current)
            Destroy(_current);

        ReleaseActiveTextBlock();
    }

    Transform ConsumeParagraphAnchor(string paragraphText)
    {
        if (_imageAnchorQueue.Count > 0)
            return _imageAnchorQueue.Dequeue();

        if (!_current)
            return null;

        string normalizedParagraph = NormalizeParagraphText(paragraphText);
        string liveParagraph = NormalizeParagraphText(_liveParagraphBuffer.ToString());
        string currentText = NormalizeParagraphText(GetCurrentText());
        bool matchedLiveBuffer = string.Equals(liveParagraph, normalizedParagraph, StringComparison.Ordinal);
        bool matchedCurrentText = string.Equals(currentText, normalizedParagraph, StringComparison.Ordinal);

        if (!matchedLiveBuffer && !matchedCurrentText)
            return null;

        _isFinalizingForImageEvent = true;
        try
        {
            FinalizeParagraphBlock(normalizedParagraph, enqueueForImage: false);
            if (matchedLiveBuffer)
                _liveParagraphBuffer.Length = 0;

            return _current ? _current.transform : null;
        }
        finally
        {
            _isFinalizingForImageEvent = false;
            ReleaseActiveTextBlock();
        }
    }

    async Task SpawnImageSlotAsync(string paragraphText, Transform anchor)
    {
        var slot = Instantiate(imagePrefab, content);
        if (anchor)
            slot.transform.SetSiblingIndex(anchor.GetSiblingIndex() + 1);

        slot.SetActive(true);

        var rawImage = slot.GetComponentInChildren<RawImage>(true);
        if (rawImage)
        {
            rawImage.texture = null;
            rawImage.color = new Color(1f, 1f, 1f, 0.3f);
        }

        _needFlush = true;

        try
        {
            Texture2D tex = await imageRequester.GenerateImageAsync(paragraphText);
            if (!slot) return;

            if (tex != null && rawImage != null)
            {
                rawImage.texture = tex;
                rawImage.color = Color.white;
                FlushLayoutAndScrollToBottom();
            }
            else
            {
                slot.SetActive(false);
            }
        }
        catch (Exception e)
        {
            if (slot) slot.SetActive(false);
            Debug.LogWarning("[EndingUIBinder] Image gen failed: " + e.Message, this);
        }
    }

    static string NormalizeParagraphText(string rawParagraph)
    {
        return string.IsNullOrWhiteSpace(rawParagraph) ? string.Empty : rawParagraph.Trim();
    }

    static string NormalizeLivePreview(string rawParagraph)
    {
        return string.IsNullOrEmpty(rawParagraph) ? string.Empty : rawParagraph.TrimStart('\n');
    }

    void FlushLayoutAndScrollToBottom()
    {
        if (!content) return;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        if (scroll)
        {
            scroll.velocity = Vector2.zero;
            scroll.verticalNormalizedPosition = 0f;
        }
    }

    public void ClearCurrent()
    {
        ResetStreamState();

        if (!content) return;

        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);
    }

    public void PlayTakeoverEnding()
    {
        _ = GenerateFlowAsync();
    }
}
