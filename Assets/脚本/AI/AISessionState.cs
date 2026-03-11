using System;
using System.Collections.Generic;
using UnityEngine;

public class AISessionState : MonoBehaviour
{
    public static AISessionState I { get; private set; }

    [Header("Core Memory")]
    [TextArea(2, 6)]
    public string playerLastInput;      // 【PlayerLastInput】
    [TextArea(2, 6)]
    public string aiRecentMemory;       // 【AIRecentMemory】

    [Header("SAN")]
    [Range(0, 100)]
    public int san = 85;                // 【UserState】默认 san
    public int sanMin = 0;
    public int sanMax = 100;

    [Header("Init / Default State")]
    [TextArea(2, 6)]
    public string defaultIntro = "陈末苏醒了。你需要先给他解释当前的状况，以及你是什么。";

    [Header("Saved Evidence (Player Pinned)")]
    public List<EvidenceRef> saved = new List<EvidenceRef>();

    void Awake()
    {
        if (I != null) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);

        // 初始默认
        if (string.IsNullOrWhiteSpace(playerLastInput))
            playerLastInput = "（沉默）";

        if (string.IsNullOrWhiteSpace(aiRecentMemory))
            aiRecentMemory = defaultIntro;
    }

    public void SetPlayerInput(string txt)
    {
        playerLastInput = string.IsNullOrWhiteSpace(txt) ? "（沉默）" : txt.Trim();
    }

    public void SetAIReply(string txt)
    {
        aiRecentMemory = string.IsNullOrWhiteSpace(txt) ? aiRecentMemory : txt.Trim();
    }

    public void AddSan(int delta)
    {
        san = Mathf.Clamp(san + delta, sanMin, sanMax);
    }

    public void AddSaved(EvidenceRef ev)
    {
        if (ev == null) return;
        if (string.IsNullOrWhiteSpace(ev.text)) return;

        // 去重：同文本就不重复存（你也可以改成按 id）
        if (saved.Exists(x => x != null && x.text == ev.text)) return;

        saved.Add(ev);
    }

    public string BuildSavedBlock(int max = 6)
    {
        if (saved == null || saved.Count == 0) return "（无）";
        int n = Mathf.Clamp(max, 1, 30);

        // 最近的优先
        int start = Mathf.Max(0, saved.Count - n);
        var lines = new List<string>();
        for (int i = saved.Count - 1; i >= start; i--)
        {
            var s = saved[i];
            if (s == null) continue;
            lines.Add("- " + s.text);
        }
        lines.Reverse();
        return string.Join("\n", lines);
    }
}
