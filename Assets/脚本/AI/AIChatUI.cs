using TMPro;
using UnityEngine;

public class AIChatUI : MonoBehaviour
{
    [Header("Refs (必须拖)")]
    public CanvasGroup group;        // ✅ 拖：AI对话模板(CanvasGroup)
    public TextMeshProUGUI aiText;   // ✅ 拖：ai文本(TMP)
    public TypewriterText typewriter;

    [Header("Behavior")]
    public float autoHideSeconds = 2.5f;

    float hideAt = -1f;
    bool isShowing;

    void Awake()
    {
        // 不要禁用任何 GameObject，只清文本+隐藏视觉
        if (aiText)
        {
            aiText.text = "";
            aiText.gameObject.SetActive(false);
        }
        HideVisual();
    }

    void Update()
    {
        if (!isShowing) return;

        if (autoHideSeconds > 0 && hideAt > 0 && Time.unscaledTime >= hideAt)
            HideImmediate();
    }

    public void ShowLine(string line)
    {
        if (!group || !aiText)
        {
            Debug.LogError("[AIChatUI] Missing refs: group/aiText");
            return;
        }

        // ✅ 保证自身脚本可运行（但不再依赖 SetActive(false)/true）
        if (!gameObject.activeInHierarchy)
        {
            // 如果你真的被关了，就把父链打开（保险）
            Transform p = transform;
            while (p != null)
            {
                if (!p.gameObject.activeSelf) p.gameObject.SetActive(true);
                p = p.parent;
            }
        }

        // 视觉显示
        ShowVisual();

        aiText.gameObject.SetActive(true);
        aiText.text = line;

        if (typewriter)
        {
            // typewriter 所在对象必须 active 才能协程
            if (!typewriter.gameObject.activeInHierarchy)
                typewriter.gameObject.SetActive(true);

            typewriter.text = aiText;
            typewriter.Play();
        }

        isShowing = true;

        if (autoHideSeconds > 0)
        {
            float cps = 18f;
            float typingTime = Mathf.Clamp(line.Length / cps, 0.6f, 4.5f);
            hideAt = Time.unscaledTime + typingTime + autoHideSeconds;
        }
        else hideAt = -1f;
    }

    public void HideImmediate()
    {
        hideAt = -1f;
        isShowing = false;

        if (aiText) aiText.gameObject.SetActive(false);

        // ✅ 只隐藏 CanvasGroup，不禁用 GameObject
        HideVisual();
    }

    void ShowVisual()
    {
        group.alpha = 1f;
        group.blocksRaycasts = false;
        group.interactable = false;
    }

    void HideVisual()
    {
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
    }
}
