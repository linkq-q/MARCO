using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class LoreBoardUI : MonoBehaviour
{
    public static LoreBoardUI I { get; private set; }

    [Header("UI Refs")]
    public Transform content;                 // ScrollRect/Viewport/Content
    public LoreBlockUI loreBlockPrefab;       // 语料块 prefab（下面给）

    [Header("Hotkey")]
    public KeyCode pinKey = KeyCode.R;        // 你想要的按键
    public bool onlyWhenPanelOpen = true;     // 只有打开道具栏时才响应

    [Header("Rules")]
    public int maxBlocks = 30;
    public bool dedupeByText = true;

    readonly List<string> _texts = new();

    void Awake()
    {
        I = this;
    }

    void Update()
    {
        if (!Input.GetKeyDown(pinKey)) return;

        if (onlyWhenPanelOpen && !gameObject.activeInHierarchy)
            return;

        // 从 AISessionState 或 AIBroker 取“最近一句 AI”
        string text = AISessionState.I ? AISessionState.I.aiRecentMemory : null;

        // 如果你更想取 AIBroker 的 _lastAILine，就用下面这行替换
        // string text = AIBroker.Instance ? AIBroker.Instance.GetLastAI() : null;

        PinText(text);
    }

    public void PinText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        text = text.Trim();

        if (dedupeByText && _texts.Contains(text))
            return;

        // 超过上限，删最早的
        if (_texts.Count >= maxBlocks)
        {
            _texts.RemoveAt(0);
            if (content.childCount > 0)
                Destroy(content.GetChild(0).gameObject);
        }

        _texts.Add(text);

        // 生成 UI
        var block = Instantiate(loreBlockPrefab, content);
        block.SetText(text);

        // 可选：写入保存列表（你之后想把这些也注入 prompt 就很方便）
        // AISessionState.I?.AddSaved(new EvidenceRef{ ... });
    }
}
