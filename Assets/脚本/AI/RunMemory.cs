using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RunMemory : MonoBehaviour
{
    public static RunMemory I { get; private set; }

    [TextArea(3, 10)] public string runSeedNote = "本局开始：记忆为空。";

    // 事实（可直接展示/可喂给AI）
    readonly List<string> facts = new();
    // 推断（AI生成后写回）
    readonly List<string> hypotheses = new();

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
        hypotheses.Clear();
        facts.Add(runSeedNote);
    }

    public void AddFact(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return;
        facts.Add(s.Trim());
    }

    public void AddHypothesis(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return;
        hypotheses.Add(s.Trim());
    }

    // 给 prompt 用：控制长度，避免爆 prompt
    public List<string> GetRecentFacts(int n = 30) => facts.TakeLast(n).ToList();
    public List<string> GetRecentHypotheses(int n = 12) => hypotheses.TakeLast(n).ToList();
}
