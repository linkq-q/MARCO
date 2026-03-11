using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MemoryShardUI : MonoBehaviour
{
    [Header("Container")]
    [Tooltip("ScrollRect/Viewport/Content")]
    public RectTransform content;

    [Tooltip("可选：用于滚动到顶部")]
    public ScrollRect scroll;

    [Header("Text Fallback (optional)")]
    [Tooltip("当未配置 memoryContentPrefab 时，用这个“通用文本预制体”显示 memoryFixedText。预制体里需要有 TextMeshProUGUI。")]
    public GameObject textBlockPrefab;

    GameObject _current;

    public void ShowPrefab(GameObject prefab, ItemData data, bool scrollToTop = true)
    {
        if (!content)
        {
            Debug.LogError("[MemoryShardUI] content is NULL.", this);
            return;
        }

        ClearCurrent();

        if (!prefab)
        {
            Debug.LogWarning("[MemoryShardUI] ShowPrefab called with NULL prefab.", this);
            return;
        }

        _current = Instantiate(prefab, content);
        _current.SetActive(true);

        // ✅ 关键：查找 Binder，把 ItemData 的文本写进 prefab
        var binder = _current.GetComponentInChildren<MemoryShardPrefabBinder>(true);
        if (binder != null)
            binder.Bind(data);

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        if (scroll && scrollToTop)
        {
            scroll.velocity = Vector2.zero;
            scroll.verticalNormalizedPosition = 1f;
        }
    }

    // ✅ 文本兜底：实例化一个通用文本块，把文本写进去
    public void ShowTextFallback(string text, bool scrollToTop = true)
    {
        if (!content)
        {
            Debug.LogError("[MemoryShardUI] content is NULL.", this);
            return;
        }

        ClearCurrent();

        if (!textBlockPrefab)
        {
            Debug.LogWarning("[MemoryShardUI] textBlockPrefab not set, cannot show text fallback.", this);
            return;
        }

        _current = Instantiate(textBlockPrefab, content);
        _current.SetActive(true);

        var tmp = _current.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp) tmp.text = text ?? "";
        else Debug.LogWarning("[MemoryShardUI] textBlockPrefab missing TextMeshProUGUI.", _current);

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        if (scroll && scrollToTop)
        {
            scroll.velocity = Vector2.zero;
            scroll.verticalNormalizedPosition = 1f;
        }
    }

    public void ClearCurrent()
    {
        if (!content) return;

        if (_current)
        {
            Destroy(_current);
            _current = null;
        }

        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);
    }
}