using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class EndingParagraphBlock : MonoBehaviour
{
    [Header("Injected Refs")]
    public TextMeshProUGUI bodyTMP;
    public CanvasGroup imageCanvasGroup;

    [Header("Movie Layout")]
    [FormerlySerializedAs("paragraphImage")]
    public RawImage movieFrame;
    public Image bottomGradient;
    public Image vignetteOverlay;

    [Header("Image Blur")]
    [Tooltip("Optional UI-compatible blur material. The current project fullscreen blur material is not suitable for RawImage.")]
    public Material blurMaterial;
    public bool useBlurredTextureFallback = true;
    [Range(1, 4)] public int blurDownsample = 2;
    [Range(1, 4)] public int blurIterations = 2;
    [Range(1, 3)] public int blurRadius = 1;

    [Header("Typewriter")]
    [Tooltip("Seconds between characters.")]
    public float charDelay = 0.04f;

    static Sprite s_bottomGradientSprite;
    static Sprite s_vignetteSprite;

    Coroutine _typewriterCoroutine;
    Coroutine _fadeCoroutine;
    Texture2D _runtimeTexture;

    void Awake()
    {
        CacheRefs();
        ConfigureMovieLayout();
        HideImageImmediate();
    }

    void Start()
    {
        CacheRefs();
        ConfigureMovieLayout();
    }

    void OnRectTransformDimensionsChange()
    {
        ConfigureMovieLayout();
    }

    void OnDestroy()
    {
        if (_runtimeTexture)
            Destroy(_runtimeTexture);
    }

    public void StartTypewriter(string fullText, Action onComplete = null)
    {
        CacheRefs();

        if (_typewriterCoroutine != null)
            StopCoroutine(_typewriterCoroutine);

        _typewriterCoroutine = StartCoroutine(TypewriterCoroutine(fullText ?? string.Empty, onComplete));
    }

    public void ShowImage(Texture2D tex)
    {
        CacheRefs();

        if (!movieFrame || tex == null)
        {
            HideImageImmediate();
            return;
        }

        if (_runtimeTexture)
        {
            Destroy(_runtimeTexture);
            _runtimeTexture = null;
        }

        Texture textureToUse = tex;
        if (useBlurredTextureFallback)
        {
            _runtimeTexture = CreateBlurredTexture(tex, Mathf.Max(1, blurDownsample), Mathf.Max(1, blurIterations), Mathf.Max(1, blurRadius));
            if (_runtimeTexture) textureToUse = _runtimeTexture;
        }

        movieFrame.texture = textureToUse;
        movieFrame.material = useBlurredTextureFallback ? null : blurMaterial;
        ApplyImageAspect(tex);
        ConfigureMovieLayout();

        if (_fadeCoroutine != null)
            StopCoroutine(_fadeCoroutine);

        if (imageCanvasGroup != null)
        {
            imageCanvasGroup.gameObject.SetActive(true);
            imageCanvasGroup.alpha = 0f;
            if (movieFrame) movieFrame.gameObject.SetActive(true);
            _fadeCoroutine = StartCoroutine(FadeInImage());
        }
        else
        {
            movieFrame.gameObject.SetActive(true);
        }
    }

    IEnumerator TypewriterCoroutine(string fullText, Action onComplete)
    {
        if (!bodyTMP)
        {
            onComplete?.Invoke();
            yield break;
        }

        bodyTMP.text = fullText;
        bodyTMP.maxVisibleCharacters = 0;
        bodyTMP.ForceMeshUpdate();

        if (charDelay <= 0f)
        {
            bodyTMP.maxVisibleCharacters = bodyTMP.textInfo.characterCount;
            _typewriterCoroutine = null;
            onComplete?.Invoke();
            yield break;
        }

        int total = bodyTMP.textInfo.characterCount;
        for (int i = 0; i <= total; i++)
        {
            bodyTMP.maxVisibleCharacters = i;
            yield return new WaitForSecondsRealtime(charDelay);
        }

        _typewriterCoroutine = null;
        onComplete?.Invoke();
    }

    IEnumerator FadeInImage()
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime * 2f;
            if (imageCanvasGroup != null)
                imageCanvasGroup.alpha = Mathf.Clamp01(t);
            yield return null;
        }

        if (imageCanvasGroup != null)
            imageCanvasGroup.alpha = 1f;

        _fadeCoroutine = null;
    }

    void HideImageImmediate()
    {
        CacheRefs();

        if (movieFrame)
        {
            movieFrame.texture = null;
            movieFrame.material = null;
            movieFrame.gameObject.SetActive(false);
        }

        if (imageCanvasGroup != null)
            imageCanvasGroup.alpha = 0f;
    }

    void CacheRefs()
    {
        if (!bodyTMP)
            bodyTMP = FindChildComponent<TextMeshProUGUI>("BodyText") ??
                      GetComponentInChildren<TextMeshProUGUI>(true);

        if (!movieFrame)
            movieFrame = FindChildComponent<RawImage>("MovieFrame") ??
                         FindChildComponent<RawImage>("RawImage") ??
                         GetComponentInChildren<RawImage>(true);

        if (!bottomGradient)
        {
            bottomGradient = FindChildComponent<Image>("BottomGradient");
            if (!bottomGradient)
            {
                var images = GetComponentsInChildren<Image>(true);
                for (int i = 0; i < images.Length; i++)
                {
                    if (!images[i]) continue;
                    bottomGradient = images[i];
                    break;
                }
            }
        }

        if (!vignetteOverlay)
            vignetteOverlay = FindChildComponent<Image>("VignetteOverlay");

        if (!imageCanvasGroup && movieFrame)
            imageCanvasGroup = movieFrame.GetComponent<CanvasGroup>() ??
                               movieFrame.GetComponentInParent<CanvasGroup>(true);
    }

    void ConfigureMovieLayout()
    {
        EnsureOverlayVisuals();

        if (imageCanvasGroup)
        {
            var containerRect = imageCanvasGroup.GetComponent<RectTransform>();
            if (containerRect)
            {
                containerRect.anchorMin = Vector2.zero;
                containerRect.anchorMax = Vector2.one;
                containerRect.pivot = new Vector2(0.5f, 0.5f);
                containerRect.anchoredPosition = Vector2.zero;
                containerRect.offsetMin = Vector2.zero;
                containerRect.offsetMax = Vector2.zero;
                containerRect.localScale = Vector3.one;
            }
        }

        if (movieFrame)
        {
            var fitter = movieFrame.GetComponent<AspectRatioFitter>();
            if (fitter) Destroy(fitter);

            var movieRect = movieFrame.rectTransform;
            movieRect.anchorMin = Vector2.zero;
            movieRect.anchorMax = Vector2.one;
            movieRect.pivot = new Vector2(0.5f, 0.5f);
            movieRect.anchoredPosition = Vector2.zero;
            movieRect.offsetMin = Vector2.zero;
            movieRect.offsetMax = Vector2.zero;
            movieRect.localScale = Vector3.one;
        }

        if (bottomGradient)
        {
            var gradientRect = bottomGradient.rectTransform;
            gradientRect.anchorMin = new Vector2(0f, 0f);
            gradientRect.anchorMax = new Vector2(1f, 0.45f);
            gradientRect.pivot = new Vector2(0.5f, 0f);
            gradientRect.anchoredPosition = Vector2.zero;
            gradientRect.offsetMin = Vector2.zero;
            gradientRect.offsetMax = Vector2.zero;
            gradientRect.localScale = Vector3.one;
        }

        if (bodyTMP)
        {
            var textRect = bodyTMP.rectTransform;
            textRect.anchorMin = new Vector2(0f, 0f);
            textRect.anchorMax = new Vector2(1f, 0.45f);
            textRect.pivot = new Vector2(0.5f, 0f);
            textRect.anchoredPosition = Vector2.zero;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            textRect.localScale = Vector3.one;

            bodyTMP.color = Color.white;
            bodyTMP.alignment = TextAlignmentOptions.BottomLeft;
            bodyTMP.overflowMode = TextOverflowModes.Overflow;
            bodyTMP.enableWordWrapping = true;
            bodyTMP.lineSpacing = Mathf.Clamp(bodyTMP.lineSpacing, 4f, 10f);
            bodyTMP.margin = new Vector4(40f, 20f, 40f, 24f);
        }
    }

    void EnsureOverlayVisuals()
    {
        if (bottomGradient)
        {
            if (!bottomGradient.sprite)
                bottomGradient.sprite = GetBottomGradientSprite();

            bottomGradient.type = Image.Type.Simple;
            bottomGradient.color = Color.white;
            bottomGradient.raycastTarget = false;
        }

        if (vignetteOverlay)
        {
            if (!vignetteOverlay.sprite)
                vignetteOverlay.sprite = GetVignetteSprite();

            vignetteOverlay.type = Image.Type.Simple;
            vignetteOverlay.color = new Color(0f, 0f, 0f, 0.55f);
            vignetteOverlay.raycastTarget = false;
        }
    }

    void ApplyImageAspect(Texture texture)
    {
        // Fullscreen stretch: keep default UVs and do not preserve aspect ratio.
    }

    T FindChildComponent<T>(string childName, bool includeInactive = true) where T : Component
    {
        if (string.IsNullOrEmpty(childName)) return null;

        var transforms = GetComponentsInChildren<Transform>(includeInactive);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i].name != childName) continue;
            return transforms[i].GetComponent<T>();
        }

        return null;
    }

    static Sprite GetBottomGradientSprite()
    {
        if (s_bottomGradientSprite) return s_bottomGradientSprite;

        var tex = new Texture2D(1, 64, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < tex.height; y++)
        {
            float alpha = Mathf.Clamp01((float)y / (tex.height - 1));
            tex.SetPixel(0, y, new Color(1f, 1f, 1f, alpha));
        }

        tex.Apply(false, true);
        s_bottomGradientSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        return s_bottomGradientSprite;
    }

    static Sprite GetVignetteSprite()
    {
        if (s_vignetteSprite) return s_vignetteSprite;

        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float maxDist = center.magnitude;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center) / maxDist;
                float vignette = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.75f, dist));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, vignette));
            }
        }

        tex.Apply(false, true);
        s_vignetteSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        return s_vignetteSprite;
    }

    static Texture2D CreateBlurredTexture(Texture2D source, int downsample, int iterations, int radius)
    {
        if (!source) return null;

        int targetWidth = Mathf.Max(1, source.width / downsample);
        int targetHeight = Mathf.Max(1, source.height / downsample);

        Color[] srcPixels = source.GetPixels();
        var downsampled = new Color[targetWidth * targetHeight];

        for (int y = 0; y < targetHeight; y++)
        {
            int srcY = Mathf.Min(source.height - 1, Mathf.RoundToInt((float)y / targetHeight * source.height));
            for (int x = 0; x < targetWidth; x++)
            {
                int srcX = Mathf.Min(source.width - 1, Mathf.RoundToInt((float)x / targetWidth * source.width));
                downsampled[y * targetWidth + x] = srcPixels[srcY * source.width + srcX];
            }
        }

        var work = new Color[downsampled.Length];
        Array.Copy(downsampled, work, downsampled.Length);
        var temp = new Color[downsampled.Length];

        for (int i = 0; i < iterations; i++)
        {
            BlurPass(work, temp, targetWidth, targetHeight, radius);
            var swap = work;
            work = temp;
            temp = swap;
        }

        var result = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false);
        result.filterMode = FilterMode.Bilinear;
        result.wrapMode = TextureWrapMode.Clamp;
        result.SetPixels(work);
        result.Apply(false, false);
        return result;
    }

    static void BlurPass(Color[] src, Color[] dst, int width, int height, int radius)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color sum = Color.clear;
                int count = 0;

                for (int oy = -radius; oy <= radius; oy++)
                {
                    int yy = Mathf.Clamp(y + oy, 0, height - 1);
                    for (int ox = -radius; ox <= radius; ox++)
                    {
                        int xx = Mathf.Clamp(x + ox, 0, width - 1);
                        sum += src[yy * width + xx];
                        count++;
                    }
                }

                dst[y * width + x] = sum / Mathf.Max(1, count);
            }
        }
    }
}
