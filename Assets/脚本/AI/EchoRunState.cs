using System;
using UnityEngine;

public class EchoRunState : MonoBehaviour
{
    public static EchoRunState I { get; private set; }

    [Header("Current")]
    public EchoStage stage = EchoStage.Stage1_Explore;
    public int subState = 1;

    [Header("Pending (apply next turn)")]
    public bool hasPending = false;
    public EchoStage nextStage;
    public int nextSubState;

    [Header("Stage2 Runtime")]
    public Stage2RealityRuntime stage2Runtime = new Stage2RealityRuntime();

    // ===== Stage3 runtime (keep old fields for compatibility) =====
    public Stage3State stage3 = Stage3State.S1_BigWorld;

    public RealityTopicPromptsAsset stage3TopicPool;
    public Stage3RealityRuntime stage3Runtime = new Stage3RealityRuntime();
    public int stage3NeedCoverCount = 4;

    public event Action<EchoStage, int, string> OnStageChanged; // (stage, sub, reason)

    void Awake()
    {
        if (I != null) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
    }

    // ✅ 单入口：立即切换（并清 pending）
    public void ForceSet(EchoStage s, int ss, string reason)
    {
        var beforeS = stage;
        var beforeSub = subState;
        bool enteringStage2 = beforeS != EchoStage.Stage2_Rift && s == EchoStage.Stage2_Rift;

        stage = s;
        subState = Mathf.Max(1, ss);
        hasPending = false;

        if (enteringStage2)
        {
            if (stage2Runtime == null)
                stage2Runtime = new Stage2RealityRuntime();

            stage2Runtime.ResetAll();

            var takeover = FindFirstObjectByType<ManagedTakeoverSystem>();
            takeover?.ResetForStage2();
        }

        Debug.Log($"[StageSet] {beforeS}/{beforeSub} -> {stage}/{subState} reason={reason}");
        OnStageChanged?.Invoke(stage, subState, reason);
    }

    // ✅ 单入口：写 pending
    public void SetPending(EchoStage s, int ss, string reason)
    {
        hasPending = true;
        nextStage = s;
        nextSubState = Mathf.Max(1, ss);
        Debug.Log($"[StagePending] -> {nextStage}/{nextSubState} reason={reason}");
    }

    // ✅ 提交 pending（由 AIBroker 每次请求前调用）
    public void ApplyPendingIfAny(string reason)
    {
        if (!hasPending) return;
        ForceSet(nextStage, nextSubState, "ApplyPending:" + reason);
    }

    // ===== Keep your old helper =====
    public void EnsureStage3RuntimeInited()
    {
        if (stage != EchoStage.Stage3_Dwarf) return;
        if (stage3TopicPool == null) return;

        if (stage3Runtime.coveredCount > 0) return;
        stage3Runtime.InitFromAsset(stage3TopicPool, stage3NeedCoverCount);
    }
}
