using System;
using System.Collections.Generic;
using UnityEngine;

[Obsolete("RunMemory is deprecated. Recent dialogue context in AISessionState replaces hypothesis memory.")]
public class RunMemory : MonoBehaviour
{
    public static RunMemory I { get; private set; }

    [TextArea(3, 10)] public string runSeedNote = "本局开始：记忆为空。";

    readonly List<string> facts = new();

    void Awake()
    {
        if (I != null) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
        ClearForNewRun();
    }

    public void ClearForNewRun()
    {
        facts.Clear();
        facts.Add(runSeedNote);
    }

    public void AddFact(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return;
        facts.Add(s.Trim());
    }

    public void AddHypothesis(string s)
    {
        // Deprecated on purpose.
    }

    public List<string> GetRecentFacts(int n = 30)
    {
        int count = Mathf.Clamp(n, 0, facts.Count);
        return count <= 0 ? new List<string>() : facts.GetRange(Mathf.Max(0, facts.Count - count), count);
    }

    public List<string> GetRecentHypotheses(int n = 12)
    {
        return new List<string>();
    }
}
