using System;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AITakeoverEndingFlow : MonoBehaviour
{
    [Header("Refs")]
    public EndingRequester requester;     // 你的 EndingRequester
    public GameObject uiRoot;             // AI结局 UI 根节点（Canvas/Panel）

    [Header("Content")]
    public RectTransform content;         // ScrollRect/Viewport/Content
    public ScrollRect scroll;             // 可选：滚动到顶部
    public GameObject endingTextPrefab;   // 里面放 TextMeshProUGUI 或 EndingEndingPrefabBinder

    [Header("Optional")]
    public GameObject loadingGO;          // “生成中…”动画/文字（可选）
    public bool clearBeforeShow = true;
    public bool scrollToTop = true;

    bool _busy;

    public void Play()
    {
        if (_busy) return;
        _ = PlayAsync();
    }

    async Task PlayAsync()
    {
        _busy = true;

        try
        {
            if (!requester) requester = FindFirstObjectByType<EndingRequester>();
            if (!requester) throw new Exception("EndingRequester 未绑定/场景中未找到");
            if (!uiRoot) throw new Exception("uiRoot 未绑定");
            if (!content) throw new Exception("content 未绑定（ScrollRect/Viewport/Content）");
            if (!endingTextPrefab) throw new Exception("endingTextPrefab 未绑定");

            uiRoot.SetActive(true);
            if (loadingGO) loadingGO.SetActive(true);

            // 1) 先抽取参数 + 拼 prompt + 发请求（你的 requester 内部已做）
            string ending = await requester.RequestEndingAsync();
            if (string.IsNullOrWhiteSpace(ending)) ending = "(生成失败：返回为空)";

            // 2) 显示到滚动区
            if (clearBeforeShow) ClearContent();

            var go = Instantiate(endingTextPrefab, content);
            go.SetActive(true);

            // 优先 binder
            var binder = go.GetComponentInChildren<EndingEndingPrefabBinder>(true);
            if (binder != null)
            {
                binder.Bind(ending);
            }
            else
            {
                var tmp = go.GetComponentInChildren<TextMeshProUGUI>(true);
                if (tmp) tmp.text = ending;
                else Debug.LogWarning("[AITakeoverEndingFlow] Prefab 内找不到 TextMeshProUGUI / EndingEndingPrefabBinder", go);
            }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);

            if (scroll && scrollToTop)
            {
                scroll.velocity = Vector2.zero;
                scroll.verticalNormalizedPosition = 1f;
            }
        }
        catch (Exception e)
        {
            Debug.LogError("[AITakeoverEndingFlow] " + e.Message);
        }
        finally
        {
            if (loadingGO) loadingGO.SetActive(false);
            _busy = false;
        }
    }

    void ClearContent()
    {
        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);
    }
}