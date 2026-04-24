using System;
using UnityEngine;

public class EndingManager : MonoBehaviour
{
    public static EndingManager I { get; private set; }

    public enum EndingType { None, Lost, AITakeover, Awake, Destruction }

    public AITakeoverEndingFlow takeoverFlow;

    [Header("Refs")]
    public EndingLockdownManager lockdown;

    [Header("Fixed Endings (1 & 3)")]
    public FixedEndingPlayer fixedPlayer;
    public FixedEndingAsset lostAsset;
    public FixedEndingAsset awakeAsset;

    [Header("Ending 4 (Destruction)")]
    public FixedEndingAsset destructionAsset;

    [Header("Ending 2 (already done)")]
    public EndingUIBinder takeoverUIBinder;

    [Header("Stage3 Choice Popup (confirm)")]
    public EndingChoicePopup choicePopup;

    [Header("Bind SanSystem")]
    public SanSystem sanSystem;

    [Header("Options")]
    public bool lockSystemsOnEnding = true;

    [Tooltip("⚠ 若你置0时间缩放，你的打字机会停（WaitForSeconds）。固定结局建议关掉或改打字机用Realtime。")]
    public bool pauseTimeOnEnding = false;

    [Header("After Reject -> Continue Story (Stage4)")]
    [Tooltip("拒绝托管后要切到的阶段（在 Inspector 里填你的 Stage4）")]
    public EchoStage stageAfterReject = EchoStage.Stage4_Rebuild;
    public int subAfterReject = 1;

    public EndingType Current { get; private set; } = EndingType.None;

    bool _lockedOnce;
    bool _confirmShowing;

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
    }

    void OnEnable()
    {
        if (!sanSystem) sanSystem = FindFirstObjectByType<SanSystem>();
        if (sanSystem != null)
            sanSystem.OnLostEndingRequested += TriggerLostEnding;
    }

    void OnDisable()
    {
        if (sanSystem != null)
            sanSystem.OnLostEndingRequested -= TriggerLostEnding;
    }

    // ========== 结局1：迷失 ==========
    public void TriggerLostEnding()
    {
        if (Current != EndingType.None) return;
        Current = EndingType.Lost;

        EnterEndingMode(); // 真结局才硬锁

        if (!fixedPlayer || !lostAsset)
        {
            Debug.LogError("[EndingManager] fixedPlayer 或 lostAsset 未绑定。");
            return;
        }

        fixedPlayer.Play(lostAsset);
    }

    // ========== 结局2：AI接管（你已打通生成+展示） ==========
    public void TriggerAITakeoverEnding()
    {
        if (Current != EndingType.None) return;
        Current = EndingType.AITakeover;

        EnterEndingMode(); // 真结局才硬锁

        if (!takeoverUIBinder)
        {
            Debug.LogError("[EndingManager] takeoverUIBinder 未绑定（EndingUIBinder）。");
            return;
        }

        takeoverUIBinder.PlayTakeoverEnding();
    }

    // ========== 结局4：销毁（ECHO-03/07达到100%） ==========
    public void TriggerDestructionEnding()
    {
        if (Current != EndingType.None) return;
        Current = EndingType.Destruction;

        EnterEndingMode();

        if (!fixedPlayer || !destructionAsset)
        {
            Debug.LogError("[EndingManager] fixedPlayer 或 destructionAsset 未绑定。");
            return;
        }

        fixedPlayer.Play(destructionAsset);
    }

    // ========== 结局3：醒来（固定文本结局，若你仍保留这个接口） ==========
    public void TriggerAwakeEnding()
    {
        if (Current != EndingType.None) return;
        Current = EndingType.Awake;

        EnterEndingMode(); // 真结局才硬锁

        if (!fixedPlayer || !awakeAsset)
        {
            Debug.LogError("[EndingManager] fixedPlayer 或 awakeAsset 未绑定。");
            return;
        }

        fixedPlayer.Play(awakeAsset);
    }

    // ========== 统一进入结局模式（硬锁） ==========
    void EnterEndingMode()
    {
        if (_lockedOnce) return;
        _lockedOnce = true;

        if (pauseTimeOnEnding) Time.timeScale = 0f;

        if (lockSystemsOnEnding && lockdown != null)
            lockdown.Lock();
    }

    // =====================================================================
    // Stage3-S7 判定 accept/reject 后：弹“确认弹窗”（不改任何文本）
    // - 点同意按钮 => AI 主导结局（结局2）
    // - 点拒绝按钮 => 切到 Stage4 继续聊天（不进入结局，不硬锁）
    // =====================================================================
    public void RequestFinalConfirmFromJudge(string judgeTag)
    {
        if (_confirmShowing) return;
        if (Current != EndingType.None) return; // 已进入结局就不要再确认

        if (!choicePopup)
        {
            Debug.LogError("[EndingManager] choicePopup 未绑定。");
            return;
        }

        _confirmShowing = true;

        // ✅ 确认弹窗阶段不进入结局锁，否则拒绝后无法继续聊天
        if (!choicePopup.gameObject.activeSelf)
            choicePopup.gameObject.SetActive(true);


        // 弹窗阶段：像结局一样锁系统，但保留 EventSystem 以便点击
        // 弹窗阶段：像结局一样锁系统，但保留 EventSystem 以便点击
        if (lockSystemsOnEnding && lockdown != null)
            lockdown.Lock(keepEventSystemEnabled: true);

        // ✅ 不改文本：只负责让预制体显示（只调用一次）
        choicePopup.Show();

        // 防止叠加
        choicePopup.OnDecision = null;

        choicePopup.OnDecision = acceptedButton =>
        {
            choicePopup.Hide();
            _confirmShowing = false;

            if (acceptedButton)
            {
                // 同意按钮 => 进入 AI 主导结局
                TriggerAITakeoverEnding();
            }
            else
            {
                // 拒绝按钮 => 进入 Stage4 继续剧情
                if (EchoRunState.I != null)
                    EchoRunState.I.SetPending(EchoStage.Stage3_Dwarf, 7, "EndingManager");
                lockdown?.UnlockByNoteContains("chat");

                // 如果你在 AIBroker 里做了“只弹一次”的门闩，拒绝后建议重置
                AIBroker.Instance?.ResetFinalChoicePopupGate();
            }
        };
    }
}
