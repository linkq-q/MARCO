using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class FamilyVoiceScheduleConfig
{
    [Header("Which type is family voice")]
    public GuideType familyType = GuideType.FamilyVoice;

    [Header("Guarantee")]
    [Tooltip("每 N 次刷新，至少出现 1 次亲人声音（N>=1）")]
    public int guaranteeEveryNRefresh = 4;

    [Header("Stage Progression")]
    [Tooltip("按刷新次数推进阶段：每 M 次刷新阶段+1（M>=1）。例如 M=6：0-5为stage0，6-11为stage1...")]
    public int stageAdvanceEveryMRefresh = 6;

    [Tooltip("是否在 stage 内优先抽 order 小的（更像时间顺序）")]
    public bool preferOrderWithinStage = true;

    [Range(0f, 0.3f)]
    [Tooltip("非保底时，亲人声音也有机会自然出现（仍限制在当前stage）。0=只靠保底出现")]
    public float naturalFamilyChance = 0.12f;
}

/// <summary>
/// ✅ 需求：
/// - 非亲人：混抽（不分阶段）
/// - 亲人声音：只允许当前stage进入候选
/// - 按刷新次数推进stage（可调）
/// - 每N次刷新必出一次亲人声音
/// - 已抽过的永不再抽（used）
/// </summary>
public class GuidePicker
{
    private readonly HashSet<string> used = new HashSet<string>();

    // ✅ 调度状态（需要持久化的话，你可以把这几个值存档）
    private int refreshCount = 0;          // 总刷新次数（每次Pick算一次）
    private int sinceLastFamily = 0;       // 距离上次亲人声音已经过去多少次刷新

    public void ResetUsed()
    {
        used.Clear();
        refreshCount = 0;
        sinceLastFamily = 0;
    }

    public int RemainingCount(GuidePool pool)
    {
        if (!pool || pool.lines == null) return 0;
        return pool.lines.Count(x => x != null && !string.IsNullOrEmpty(x.id) && !used.Contains(x.id));
    }

    /// <summary>
    /// ✅ 主入口：按你的规则抽取
    /// </summary>
    public GuideLine PickWithFamilySchedule(GuidePool pool, FamilyVoiceScheduleConfig cfg, int dayIndex)
    {
        if (!pool || pool.lines == null || pool.lines.Count == 0) return null;
        if (cfg == null) cfg = new FamilyVoiceScheduleConfig();

        // ✅ 1) 阶段：按 dayIndex 推进（确定性，不受调用次数污染）
        int m = Mathf.Max(1, cfg.stageAdvanceEveryMRefresh);
        int currentStage = Mathf.Clamp((dayIndex - 1) / m, 0, 3);

        // ✅ 2) “刷新计数”仍用于保底节奏（每N次必出亲人）
        //     这里的 refreshCount 应该代表“触发一次guide插入的次数”，而不是函数被乱调用的次数
        refreshCount++;

        bool mustFamily = cfg.guaranteeEveryNRefresh <= 1
                          || sinceLastFamily >= (cfg.guaranteeEveryNRefresh - 1);

        // ✅ 当前阶段亲人候选
        var familyCandidates = GetCandidates(pool, x => x.type == cfg.familyType && x.stage == currentStage);

        // 若必须出亲人但本阶段抽干了：向后找 stage 1/2/3
        if (mustFamily && familyCandidates.Count == 0)
        {
            int s = currentStage;
            while (s < 3 && familyCandidates.Count == 0)
            {
                s++;
                familyCandidates = GetCandidates(pool, x => x.type == cfg.familyType && x.stage == s);
            }
            if (familyCandidates.Count > 0)
                currentStage = s;
        }

        // ✅ 3) 保底：必出亲人（只从亲人候选抽）
        if (mustFamily && familyCandidates.Count > 0)
        {
            var pick = PickFromCandidates(familyCandidates, cfg.preferOrderWithinStage);
            MarkUsedAndAdvanceCounters(pick, cfg.familyType);
            return pick;
        }

        // ✅ 4) 非保底：可选“自然出现亲人”（仍然只从亲人候选抽，不参与混抽）
        bool tryNaturalFamily = cfg.naturalFamilyChance > 0f
                                && familyCandidates.Count > 0
                                && UnityEngine.Random.value < cfg.naturalFamilyChance;

        if (tryNaturalFamily)
        {
            var pick = PickFromCandidates(familyCandidates, cfg.preferOrderWithinStage);
            MarkUsedAndAdvanceCounters(pick, cfg.familyType);
            return pick;
        }

        // ✅ 5) 否则：混抽 —— 彻底排除亲人声音（满足“亲人不能混抽”）
        var mixedCandidates = GetCandidates(pool, x => x.type != cfg.familyType);

        if (mixedCandidates.Count == 0) return null;

        var mixedPick = PickFromCandidates(mixedCandidates, preferOrder: false);
        MarkUsedAndAdvanceCounters(mixedPick, cfg.familyType);
        return mixedPick;
    }


    // ---------- helpers ----------

    private int GetStageByRefreshCount(int refreshCount, int advanceEveryM)
    {
        advanceEveryM = Mathf.Max(1, advanceEveryM);
        int stage = (refreshCount - 1) / advanceEveryM; // 1..M => stage0
        return Mathf.Clamp(stage, 0, 3);
    }

    private List<GuideLine> GetCandidates(GuidePool pool, Func<GuideLine, bool> predicate)
    {
        return pool.lines
            .Where(x => x != null
                        && !string.IsNullOrEmpty(x.id)
                        && !used.Contains(x.id)
                        && predicate(x))
            .ToList();
    }

    private GuideLine PickFromCandidates(List<GuideLine> candidates, bool preferOrder)
    {
        if (candidates == null || candidates.Count == 0) return null;

        // ✅ 同stage内可选：更偏向 order 小的（更像时间顺序，但仍有随机）
        if (preferOrder)
        {
            // 做一个“顺序偏置”：按 order 分组，先从最小 order 里抽；抽空了再到下一个
            int minOrder = candidates.Min(x => x.order);
            var firstBand = candidates.Where(x => x.order == minOrder).ToList();
            if (firstBand.Count > 0)
                return PickWeighted(firstBand);

            // 兜底
        }

        return PickWeighted(candidates);
    }

    private GuideLine PickWeighted(List<GuideLine> candidates)
    {
        int total = candidates.Sum(x => Mathf.Max(1, x.weight));
        int r = UnityEngine.Random.Range(0, total);

        foreach (var c in candidates)
        {
            r -= Mathf.Max(1, c.weight);
            if (r < 0) return c;
        }

        return candidates[UnityEngine.Random.Range(0, candidates.Count)];
    }

    private void MarkUsedAndAdvanceCounters(GuideLine pick, GuideType familyType)
    {
        if (pick == null) return;

        used.Add(pick.id);

        if (pick.type == familyType)
            sinceLastFamily = 0;
        else
            sinceLastFamily++;
    }
}
