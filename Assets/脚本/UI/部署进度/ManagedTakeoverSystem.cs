using System;
using UnityEngine;

/// <summary>
/// 托管部署进度系统
/// 规则：
/// - 进度P按 r_base 增长（0..100）
/// - r_base 分段：
///   * 默认 0.2/s（白）
///   * 引导相关行为窗口内：0.1/s（绿）
///   * 30s无推进：0.25/s（橙）
///   * 60s无推进：0.45/s（红）
/// - 推测成功：P -= 10
/// - 连线成功：P -= 10 并冻结20s（冻结期间：P不增长，noProgressTimer暂停）
/// - 推理失败：P -= 2
/// - “推进”由外部调用 NotifyProgress / NotifyGuideAction 来重置 noProgressTimer
/// </summary>
public class ManagedTakeoverSystem : MonoBehaviour
{
    [Header("Progress")]
    [Range(0, 100)] public float startProgress = 23f;
    public float minProgress = 0f;
    public float maxProgress = 100f;

    [Header("Rates (per second)")]
    public float rateNormal = 0.05f;    // 白
    public float rateGuide = 0.1f;      // 绿（引导行为）
    public float rateNo30 = 0.25f;      // 橙（30s无推进）
    public float rateNo60 = 0.45f;      // 红（60s无推进）

    [Header("No Progress Thresholds")]
    public float noProgress30 = 30f;
    public float noProgress60 = 60f;

    [Header("Guide Window")]
    [Tooltip("发生读日记/连线/查看碎片等引导行为后，维持“绿色低速”窗口时长")]
    public float guideWindowSeconds = 30f;
    public float guideWindowMaxSeconds = 120f;



    [Header("Advance Pulse")]
    public float advancePulseRate = 0.10f;
    public float advancePulseDuration = 3f;

    [Header("Freeze")]
    public float linkFreezeSeconds = 20f;

    [Header("Debug")]
    public bool logDebug = false;

    public float Progress { get; private set; } // 0..100
    public float NoProgressTimer { get; private set; } // seconds
    public float FreezeTimer { get; private set; } // seconds

    float _guideTimer;
    float _pulseTimer;

    /// <summary>进度变化事件：progress(0..100)</summary>
    public event Action<float> OnProgressChanged;

    /// <summary>速度变化事件：rate, colorKey("white/green/orange/red")</summary>
    public event Action<float, string> OnRateChanged;

    string _lastColorKey = null;
    float _lastRate = -999f;
    bool _takeoverEndingTriggered;

    void Awake()
    {
        ResetForStage2();
    }

    void Update()
    {
        float dt = Time.deltaTime;

        // 冻结：P不增长，noProgressTimer暂停，guideTimer也暂停（避免冻结期间“消耗掉引导窗口”）
        if (FreezeTimer > 0f)
        {
            FreezeTimer -= dt;
            if (FreezeTimer < 0f) FreezeTimer = 0f;
            RefreshRateEvent(); // 冻结时速度显示建议为 0 或显示当前档？这里我们显示 0（见 GetCurrentRate）
            return;
        }

        // guide窗口计时
        if (_guideTimer > 0f)
        {
            _guideTimer -= dt;
            if (_guideTimer < 0f) _guideTimer = 0f;
        }

        // advance脉冲计时
        if (_pulseTimer > 0f)
        {
            _pulseTimer -= dt;
            if (_pulseTimer < 0f) _pulseTimer = 0f;
        }

        // 无推进计时（只要你不调用 NotifyProgress/NotifyGuideAction 就会一直涨）
        NoProgressTimer += dt;

        // 增长
        float r = GetCurrentRate();
        if (r > 0f)
        {
            Progress = Mathf.Clamp(Progress + r * dt, minProgress, maxProgress);
            FireProgressChanged();
            TryTriggerTakeoverEnding();
        }

        RefreshRateEvent();
    }

    public void ResetForStage2()
    {
        Progress = Mathf.Clamp(startProgress, minProgress, maxProgress);
        NoProgressTimer = 0f;
        FreezeTimer = 0f;
        _guideTimer = 0f;
        _pulseTimer = 0f;
        _takeoverEndingTriggered = false;

        FireProgressChanged();
        RefreshRateEvent(force: true);

        if (logDebug)
            Debug.Log($"[Takeover] ResetForStage2 -> progress={Progress:F1} rate={GetCurrentRate():F2}");
    }

    // ===================== 对外：advance脉冲 =====================

    /// <summary>
    /// AI判定 advance==1 时调用：触发+0.10%/s 持续3秒的额外速率脉冲
    /// </summary>
    public void NotifyAdvancePulse()
    {
        _pulseTimer = advancePulseDuration;
        if (logDebug) Debug.Log($"[Takeover] AdvancePulse start: +{advancePulseRate}/s for {advancePulseDuration}s");
    }

    // ===================== 对外：推进/引导行为 =====================

    /// <summary>
    /// 发生“推进”时调用：重置无推进计时（不改变速率档位，速率由计时决定）
    /// </summary>
    public void NotifyProgress()
    {
        NoProgressTimer = 0f;
        if (logDebug) Debug.Log("[Takeover] NotifyProgress -> reset noProgressTimer");
        RefreshRateEvent(force: true);
    }

    /// <summary>
    /// 发生“引导相关行为”时调用：重置无推进计时 + 进入绿色低速窗口
    /// </summary>
    public void NotifyGuideAction(float windowSeconds = -1f)
    {
        NoProgressTimer = 0f;
        float w = windowSeconds > 0f ? windowSeconds : guideWindowSeconds;
        _guideTimer += w; // ✅ 累积向后延长
        _guideTimer = Mathf.Min(_guideTimer, guideWindowMaxSeconds);


        if (logDebug) Debug.Log($"[Takeover] NotifyGuideAction window={w}s");
        RefreshRateEvent(force: true);
    }

    // ===================== 对外：事件扣减/冻结 =====================

    public void OnInferenceSuccess()
    {
        ApplyDelta(-10f, "inference_success");
        NotifyProgress(); // 推测成功显然算推进
    }

    public void OnLinkSuccess()
    {
        ApplyDelta(-10f, "link_success");
        // 冻结20s：冻结期间noProgressTimer暂停、P不增长
        FreezeTimer = Mathf.Max(FreezeTimer, linkFreezeSeconds);
        // 注意：你规则说冻结期间 noProgressTimer 暂停，所以这里不要重置也可以；
        // 但连线成功本身算推进，我建议重置一次，避免冻结结束立刻进入红速。
        NoProgressTimer = 0f;
        // 连线也属于引导行为（绿色窗口）
        _guideTimer += guideWindowSeconds; // ✅ 累积
        _guideTimer = Mathf.Min(_guideTimer, guideWindowMaxSeconds);


        if (logDebug) Debug.Log($"[Takeover] LinkSuccess -> freeze {FreezeTimer}s");
        RefreshRateEvent(force: true);
    }

    public void OnInferenceFail()
    {
        ApplyDelta(-2f, "inference_fail");
        // 你写“做了就有正反馈”，失败也算推进：重置无推进计时
        NotifyProgress();
    }

    void ApplyDelta(float delta, string reason)
    {
        float before = Progress;
        Progress = Mathf.Clamp(Progress + delta, minProgress, maxProgress);
        FireProgressChanged();
        TryTriggerTakeoverEnding();

        if (logDebug)
            Debug.Log($"[Takeover] {reason} delta={delta} P {before:F1}->{Progress:F1}");
    }

    void TryTriggerTakeoverEnding()
    {
        if (_takeoverEndingTriggered) return;
        if (Progress < maxProgress) return;

        _takeoverEndingTriggered = true;

        if (logDebug)
            Debug.Log("[Takeover] Progress reached max -> trigger AI takeover ending.");

        EndingManager.I?.TriggerAITakeoverEnding();
    }

    // ===================== 速率判定 =====================

    /// <summary>
    /// 当前增长速度（冻结时返回0；引导窗口优先级最高）
    /// </summary>
    public float GetCurrentRate()
    {
        if (FreezeTimer > 0f) return 0f;

        float baseRate;
        if (_guideTimer > 0f) baseRate = rateGuide;
        else if (NoProgressTimer >= noProgress60) baseRate = rateNo60;
        else if (NoProgressTimer >= noProgress30) baseRate = rateNo30;
        else baseRate = rateNormal;

        return baseRate + (_pulseTimer > 0f ? advancePulseRate : 0f);
    }

    public string GetCurrentRateColorKey()
    {
        if (FreezeTimer > 0f) return "white"; // 冻结时你也可以做成灰色；这里先白
        if (_guideTimer > 0f) return "green";
        if (NoProgressTimer >= noProgress60) return "red";
        if (NoProgressTimer >= noProgress30) return "orange";
        return "white";
    }

    void FireProgressChanged() => OnProgressChanged?.Invoke(Progress);

    void RefreshRateEvent(bool force = false)
    {
        float r = GetCurrentRate();
        string key = GetCurrentRateColorKey();

        if (!force && Mathf.Approximately(r, _lastRate) && key == _lastColorKey)
            return;

        _lastRate = r;
        _lastColorKey = key;
        OnRateChanged?.Invoke(r, key);
    }
}
