using System;
using System.Collections.Generic;
using UnityEngine;

public class AISessionState : MonoBehaviour
{
    public enum AddSavedResult
    {
        Success,
        Duplicate,
        Full,
        Invalid
    }

    [Serializable]
    public class RecentTurn
    {
        public string speaker;
        [TextArea(1, 4)] public string text;
    }

    public static AISessionState I { get; private set; }

    [Header("Core Memory")]
    [TextArea(2, 6)]
    public string playerLastInput;
    [TextArea(2, 6)]
    public string aiRecentMemory;

    [Header("SAN")]
    [Range(0, 100)]
    public int san = 85;
    public int sanMin = 0;
    public int sanMax = 100;

    [Header("Init / Default State")]
    [TextArea(2, 6)]
    public string defaultIntro = "陈末苏醒了。你需要先给他解释当前的状况，以及你是什么。";

    [Header("Saved Evidence (Player Pinned)")]
    public int savedLimit = 6;
    public List<EvidenceRef> saved = new List<EvidenceRef>();

    [Header("Recent Turns")]
    public List<RecentTurn> recentTurns = new List<RecentTurn>();

    void Awake()
    {
        if (I != null) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);

        if (string.IsNullOrWhiteSpace(playerLastInput))
            playerLastInput = "（沉默）";

        if (string.IsNullOrWhiteSpace(aiRecentMemory))
            aiRecentMemory = defaultIntro;
    }

    public void SetPlayerInput(string txt)
    {
        string normalized = string.IsNullOrWhiteSpace(txt) ? "（沉默）" : txt.Trim();
        playerLastInput = normalized;
        AddRecentTurn("玩家", normalized);
    }

    public void SetAIReply(string txt)
    {
        if (string.IsNullOrWhiteSpace(txt)) return;

        string normalized = txt.Trim();
        aiRecentMemory = normalized;
        AddRecentTurn("Echo", normalized);
    }

    public void AddSan(int delta)
    {
        san = Mathf.Clamp(san + delta, sanMin, sanMax);
    }

    public AddSavedResult AddSaved(EvidenceRef ev)
    {
        if (ev == null) return AddSavedResult.Invalid;
        if (string.IsNullOrWhiteSpace(ev.text)) return AddSavedResult.Invalid;

        ev.text = ev.text.Trim();

        if (saved.Exists(x => x != null && x.text == ev.text)) return AddSavedResult.Duplicate;
        if (saved.Count >= Mathf.Max(1, savedLimit)) return AddSavedResult.Full;

        if (string.IsNullOrEmpty(ev.id))
            ev.id = Guid.NewGuid().ToString("N");
        if (ev.unixMs <= 0)
            ev.unixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        saved.Add(ev);
        return AddSavedResult.Success;
    }

    public bool RemoveSavedAt(int index)
    {
        if (saved == null) return false;
        if (index < 0 || index >= saved.Count) return false;

        saved.RemoveAt(index);
        return true;
    }

    public int GetSavedRemainingSlots()
    {
        return Mathf.Max(0, Mathf.Max(1, savedLimit) - (saved?.Count ?? 0));
    }

    public string BuildSavedBlock(int max = 6)
    {
        if (saved == null || saved.Count == 0) return "（无）";
        int n = Mathf.Clamp(max, 1, 30);

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

    public string BuildRecentTurnsBlock()
    {
        if (recentTurns == null || recentTurns.Count == 0) return "（无）";

        var lines = new List<string>(recentTurns.Count + 1)
        {
            "【近期对话记录】"
        };

        for (int i = 0; i < recentTurns.Count; i++)
        {
            var turn = recentTurns[i];
            if (turn == null || string.IsNullOrWhiteSpace(turn.text)) continue;

            string speaker = string.IsNullOrWhiteSpace(turn.speaker) ? "未知" : turn.speaker.Trim();
            lines.Add($"{speaker}：{turn.text.Trim()}");
        }

        return lines.Count > 1 ? string.Join("\n", lines) : "（无）";
    }

    void AddRecentTurn(string speaker, string text)
    {
        if (recentTurns == null)
            recentTurns = new List<RecentTurn>();

        recentTurns.Add(new RecentTurn
        {
            speaker = speaker,
            text = text
        });

        while (recentTurns.Count > 6)
            recentTurns.RemoveAt(0);
    }
}
