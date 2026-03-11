using System;
using System.Collections.Generic;
using UnityEngine;

public class LoopStateManager : MonoBehaviour
{
    public static LoopStateManager Instance { get; private set; }

    public int LoopIndex { get; private set; } = 0;

    // 本轮已经触发过的“景物ID”集合：同一轮里同一个ID只能触发一次
    readonly HashSet<string> triggeredInteractablesThisLoop = new HashSet<string>();

    // 影响下一轮地块变化的状态：你可以先用最简单的 flag + score
    public int TruthScore { get; private set; } = 0;
    readonly HashSet<string> flags = new HashSet<string>();

    public event Action OnLoopAdvanced;
    public event Action OnStateChanged;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// 开始新的一轮循环：清空“本轮触发锁”
    /// 你在四地块循环回到起点时调用一次即可
    /// </summary>
    public void BeginNewLoop()
    {
        LoopIndex++;
        triggeredInteractablesThisLoop.Clear();
        OnLoopAdvanced?.Invoke();
    }

    /// <summary>
    /// 判断某个景物在“本轮”是否还能触发
    /// </summary>
    public bool CanTrigger(string interactableId)
    {
        if (string.IsNullOrWhiteSpace(interactableId)) return false;
        return !triggeredInteractablesThisLoop.Contains(interactableId);
    }

    /// <summary>
    /// 把某个景物标记为“本轮已触发”
    /// </summary>
    public void MarkTriggered(string interactableId)
    {
        if (string.IsNullOrWhiteSpace(interactableId)) return;
        triggeredInteractablesThisLoop.Add(interactableId);
    }

    /// <summary>
    /// 记录选择结果，影响下一轮（flag + score）
    /// </summary>
    public void ApplyChoice(string stateKey, int deltaTruthScore)
    {
        if (!string.IsNullOrWhiteSpace(stateKey))
            flags.Add(stateKey);

        TruthScore += deltaTruthScore;
        OnStateChanged?.Invoke();
    }

    public bool HasFlag(string stateKey) => flags.Contains(stateKey);
}
