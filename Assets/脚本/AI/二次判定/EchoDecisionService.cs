using System.Threading.Tasks;
using UnityEngine;

public class EchoDecisionService : MonoBehaviour
{
    [Header("Refs")]
    public CloudResponderJudge judge;     // ✅ 拖 Inspector
    public SanSystem sanSystem;           // ✅ 新增：拖 Inspector（场景里那个SanSystem）

    [Header("Behavior -> San")]
    [Tooltip("当 advance>0 或 tag=push/accept 时，视为一次有效交互（用于“三次解锁引导句”）并视为推进（停止自然衰减）")]
    public bool treatAdvanceAsProgress = true;

    [Tooltip("调用 SanSystem.NotifyProgress 的持续时间，<=0 用 SanSystem.progressActiveWindow")]
    public float progressSeconds = -1f;

    [Header("Debug")]
    public bool logDebug = false;

    public async Task<DecisionResult> DecideAsync(EchoStage stage, int subState, string injectedBlock)
    {
        if (judge == null) return null;

        // ✅ 直接让 CloudResponderJudge 负责：header + 每状态prompt 的拼接
        string jsonLine = await judge.JudgeAsync(stage, subState, injectedBlock);
        if (string.IsNullOrWhiteSpace(jsonLine)) return null;

        DecisionResult dr = null;
        try
        {
            dr = JsonUtility.FromJson<DecisionResult>(jsonLine);
        }
        catch
        {
            Debug.LogWarning("[Judge] JSON parse failed: " + jsonLine);
            return null;
        }

        // ✅ 在这里把 AI 判定接入真正的 San 逻辑
        if (sanSystem != null && dr != null)
        {
            sanSystem.ApplyDecisionResult(dr);

            // 可选：如果你希望“判定为推进”时延长推进窗口
            if (treatAdvanceAsProgress)
            {
                bool consideredProgress =
                    dr.advance > 0 ||
                    string.Equals(dr.tag, "push", System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(dr.tag, "accept", System.StringComparison.OrdinalIgnoreCase);

                if (consideredProgress)
                    sanSystem.NotifyProgress(progressSeconds);
            }
        }
        else
        {
            if (logDebug)
                Debug.LogWarning("[EchoDecisionService] sanSystem not set, DecisionResult applied nowhere.");
        }

        if (logDebug && dr != null)
            Debug.Log($"[EchoDecisionService] Decision tag={dr.tag} adv={dr.advance} sanDelta={dr.sanDelta} note={dr.note}");

        return dr;
    }
}
