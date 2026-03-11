using System;
using UnityEngine;
using UnityEngine.UI;

public class EndingChoicePopup : MonoBehaviour
{
    [Header("Refs")]
    public CanvasGroup group;          // 可选：弹窗整体 CanvasGroup（你若不用可不填）
    public Button acceptButton;
    public Button rejectButton;

    [Header("Behavior")]
    public bool blockRaycastsWhenShown = true;

    public Action<bool> OnDecision; // true=accept, false=reject

    bool _wired;

    void Awake()
    {
        WireOnce();
        Hide(); // 默认隐藏
    }

    void WireOnce()
    {
        if (_wired) return;
        _wired = true;

        if (acceptButton) acceptButton.onClick.AddListener(() => Decide(true));
        if (rejectButton) rejectButton.onClick.AddListener(() => Decide(false));
    }

    /// <summary>
    /// 只负责“显示预制体”：不改任何文本。
    /// </summary>
    public void Show()
    {
        WireOnce();

        gameObject.SetActive(true);

        // ✅ 关键：Hide() 关过 blocksRaycasts，这里必须打开，否则会穿透点到后面
        if (group)
        {
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true; // 或 blockRaycastsWhenShown
        }
    }

    public void Hide()
    {
        if (group)
        {
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }
        gameObject.SetActive(false);
    }

    void Decide(bool accepted)
    {
        var cb = OnDecision;
        OnDecision = null; // 防止重复触发/叠加
        cb?.Invoke(accepted);
    }
}