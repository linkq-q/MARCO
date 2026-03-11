using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 从资产池（MemoryShardPool）中抽取记忆碎片，并加入背包
/// - 进入 Stage2 时立刻抽一次（只触发一次）
/// - 之后按 intervalSeconds 周期抽
/// </summary>
public class MemoryShardAssetPicker : MonoBehaviour
{
    public enum PickMode { Random, Sequential }

    [Header("Pool")]
    public MemoryShardPool pool;

    [Header("Timing")]
    public bool autoPick = true;
    public float intervalSeconds = 10f;
    public float startDelaySeconds = 1f;

    [Header("Stage2 Instant Pick")]
    public bool pickImmediatelyOnEnterStage2 = true;

    [Tooltip("只在该阶段生效。一般填 Stage2_Rift")]
    public EchoStage activeStage = EchoStage.Stage2_Rift;

    [Header("Rule")]
    public PickMode mode = PickMode.Random;
    public bool noRepeatUntilExhausted = true;

    [Header("After Pick")]
    public bool autoSelectPicked = true; // 目前 AddItem 内部会选中
    public bool logDebug = false;

    float _t;
    int _seq = -1;
    readonly HashSet<string> _picked = new HashSet<string>(); // 用 itemId 记录本轮抽过哪些

    bool _wasInStage2 = false;
    bool _didInstantPickThisEntry = false;

    void OnEnable()
    {
        _t = -startDelaySeconds;
        _wasInStage2 = false;
        _didInstantPickThisEntry = false;
    }

    void Update()
    {
        if (!autoPick) return;
        if (InventoryManager.Instance == null) return;
        if (pool == null || pool.shards == null || pool.shards.Count == 0) return;
        if (EchoRunState.I == null) return;

        bool inStage2 = (EchoRunState.I.stage == activeStage);

        // ✅ 进入 Stage2：立刻抽一次（只触发一次）
        if (pickImmediatelyOnEnterStage2)
        {
            if (inStage2 && !_wasInStage2)
            {
                _didInstantPickThisEntry = false; // 新一轮进入，允许触发一次
                _t = 0f; // 让后续 interval 从现在开始计
            }

            if (inStage2 && !_didInstantPickThisEntry)
            {
                _didInstantPickThisEntry = true;

                if (logDebug) Debug.Log("[MemoryShardPicker] Enter Stage2 -> PickOnce NOW");
                PickOnce();

                // ✅ 抽完后重置计时器，避免同一帧/很短时间又触发 interval
                _t = 0f;
            }

            // 离开 Stage2：重置标记，方便下次再进来触发
            if (!inStage2 && _wasInStage2)
            {
                _didInstantPickThisEntry = false;
            }
        }

        _wasInStage2 = inStage2;

        // ✅ 如果你希望“只有 Stage2 才抽”，就在这里 return
        if (!inStage2) return;

        // ✅ 周期抽取
        _t += Time.deltaTime;
        if (_t < intervalSeconds) return;
        _t = 0f;

        PickOnce();
    }

    [ContextMenu("Pick Once (Debug)")]
    public void PickOnce()
    {
        if (InventoryManager.Instance == null) return;
        if (pool == null || pool.shards == null || pool.shards.Count == 0) return;

        // 过滤掉非 MemoryShard
        List<ItemData> mems = new List<ItemData>();
        for (int i = 0; i < pool.shards.Count; i++)
        {
            var d = pool.shards[i];
            if (d == null) continue;
            if (d.kind != ItemKind.MemoryShard) continue;
            mems.Add(d);
        }
        if (mems.Count == 0) return;

        // 不重复：构建本轮可抽池
        List<ItemData> candidates = mems;
        if (noRepeatUntilExhausted)
        {
            candidates = new List<ItemData>(mems.Count);
            for (int i = 0; i < mems.Count; i++)
            {
                var d = mems[i];
                if (d == null) continue;
                if (_picked.Contains(d.id)) continue;
                candidates.Add(d);
            }
            if (candidates.Count == 0)
            {
                _picked.Clear();
                candidates = mems;
            }
        }

        ItemData chosen = null;
        if (mode == PickMode.Random)
        {
            chosen = candidates[Random.Range(0, candidates.Count)];
        }
        else
        {
            for (int step = 0; step < mems.Count; step++)
            {
                _seq = (_seq + 1) % mems.Count;
                var d = mems[_seq];
                if (d == null) continue;
                if (noRepeatUntilExhausted && _picked.Contains(d.id)) continue;
                chosen = d;
                break;
            }
            if (chosen == null) chosen = mems[0];
        }

        if (chosen == null) return;

        if (noRepeatUntilExhausted) _picked.Add(chosen.id);

        InventoryManager.Instance.AddItem(chosen);

        var rt = InventoryManager.Instance.Selected;
        if (rt == null || rt.data == null || rt.data.id != chosen.id) return;

        if (autoSelectPicked)
            InventoryManager.Instance.SelectItem(rt);

        if (logDebug) Debug.Log($"[MemoryShardPicker] Picked: {chosen.id}");
    }
}