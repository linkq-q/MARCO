using System;
using UnityEngine;

public static class JudgeResultApplier
{
    // 你可以把“有效推测”的判定规则放这里，避免散落在各处
    public static bool IsValidHypothesis(DecisionResult dr)
    {
        if (dr == null) return false;

        // ✅ 最稳的规则：tag 明确是 hypothesis / valid / accept
        if (!string.IsNullOrEmpty(dr.tag))
        {
            var t = dr.tag.Trim().ToLowerInvariant();
            if (t == "hypothesis" || t == "valid_hypothesis" || t == "valid" || t == "accept")
                return true;
        }

        // 兜底：note 含关键字（可按你的 judge prompt 输出习惯调整）
        if (!string.IsNullOrEmpty(dr.note))
        {
            var n = dr.note.ToLowerInvariant();
            if (n.Contains("有效推测") || n.Contains("推测成立") || n.Contains("valid hypothesis"))
                return true;
        }

        return false;
    }

    public static DecisionResult Parse(string judgeJson)
    {
        if (string.IsNullOrWhiteSpace(judgeJson)) return null;
        try
        {
            return JsonUtility.FromJson<DecisionResult>(judgeJson);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 在你“拿到 judgeJson”之后调用这一句即可：
    /// JudgeResultApplier.Apply(sanSystem, judgeJson);
    /// </summary>
    public static void Apply(SanSystem san, string judgeJson)
    {
        if (!san) return;

        var dr = Parse(judgeJson);
        if (dr == null) return;

        // 先走你现有的 AI 对接（sanDelta + advance -> 交互计数）
        san.ApplyDecisionResult(dr);

        // ✅ “提交有效推测”额外加成：San+2（你的需求）
        if (IsValidHypothesis(dr))
        {
            san.ApplyPlayerAction(SanSystem.PlayerActionType.SubmitValidHypothesis);
            ToastSpawner.Instance?.Show("推测成立：San +2");
        }
    }
}
