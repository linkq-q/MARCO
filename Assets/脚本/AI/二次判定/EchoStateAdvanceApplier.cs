using UnityEngine;

public static class EchoStateAdvanceApplier
{
    public static void ApplyDecisionToPending(EchoStage curStage, int curSub, DecisionResult d)
    {
        if (d == null || EchoRunState.I == null) return;

        int nextSub = Mathf.Max(1, curSub + Mathf.Clamp(d.advance, -1, 1));
        EchoStage nextStage = curStage;

        // TODO：这里按你的阶段结构写“边界推进”
        // 例：Stage2 子状态到达 3 且 advance=1 -> 进入 Stage3 子状态 1
        // if (curStage == EchoStage.Stage2 && nextSub > 3) { nextStage = EchoStage.Stage3; nextSub = 1; }

        EchoRunState.I.SetPending(nextStage, nextSub, "EchoStateAdvanceApplier");
    }
}
