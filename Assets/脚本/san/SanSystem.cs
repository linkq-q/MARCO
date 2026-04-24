using System;
using UnityEngine;

/// <summary>
/// SAN 核心逻辑：
/// - 初始 65，范围 0..100
/// - 仅“无推进”时按 -0.05/s 衰减（推进时不衰减）
/// - san>85 时：每净增长 2 点触发一次引导句（事件）
/// - san<30 触发迷失结局（事件）
/// - san>=100 触发终局（事件）
/// - 每累计 3 次“有效交互行为”解锁一条引导线索句（事件）
/// - 对接 AI 判定 DecisionResult.sanDelta
/// </summary>
public class SanSystem : MonoBehaviour
{
    [Header("Config")]
    [Range(0, 100)] public int startSan = 65;
    public int minSan = 0;
    public int maxSan = 100;

    [Tooltip("仅无推进时衰减（每秒）")]
    public float decayPerSecond = 0.05f;

    [Tooltip("推进判定的“活跃窗口”（秒）。触发推进行为后，在该窗口内不衰减。")]
    public float progressActiveWindow = 2f;

    [Header("Guide Triggers")]
    [Tooltip("san > 此阈值时，每净增长2点触发一次引导句")]
    public int highSanThreshold = 85;

    [Tooltip("每累计几次有效交互，解锁一条引导线索句")]
    public int validInteractionPerGuide = 3;

    [Header("Debug")]
    public bool logDebug = false;

    [Header("Takeover")]
    public ManagedTakeoverSystem takeover;


    // 当前值
    public int San { get; private set; }

    // ========== Events（留接口给剧情/结局/高亮引导句） ==========
    /// <summary>san变化：newSan, delta, reason</summary>
    public event Action<int, int, string> OnSanChanged;

    /// <summary>高san引导句触发：triggerIndex（从1开始）, sanAtTrigger, reason</summary>
    public event Action<int, int, string> OnHighSanGuideRequested;

    /// <summary>每3次有效交互引导句触发：triggerIndex（从1开始）, totalValidInteractions</summary>
    public event Action<int, int> OnInteractionGuideRequested;

    /// <summary>迷失结局请求</summary>
    public event Action OnLostEndingRequested;

    /// <summary>终局：补齐线索 + 选择醒来/AI接管</summary>
    public event Action OnFullClueAndChoiceRequested;

    // ========== internal state ==========
    float _progressTimer;                // >0 视为推进中（不衰减）
    bool _lostFired;
    bool _fullFired;

    // “san>85 每+2触发一次引导句”的计数
    int _highSanGuideCount;
    int _highSanNetGainAccumulator;      // 在>阈值期间累计“净增长”（只算正向AddSan）

    // “每3次有效交互触发一次引导线索句”
    int _validInteractionCount;
    int _interactionGuideCount;

    void Awake()
    {
        San = Mathf.Clamp(startSan, minSan, maxSan);
        FireSanChanged(0, "init");
    }

    void Update()
    {
        // 推进窗口计时
        if (_progressTimer > 0f)
            _progressTimer -= Time.deltaTime;

        // 仅无推进时衰减
        if (!IsProgressing())
        {
            if (San > minSan)
            {
                float dec = decayPerSecond * Time.deltaTime;
                // 这里用 float 累积会更精确，但 UI/阈值用 int 更直观；我们用“缓慢扣整点”的方式：
                // 简化：把衰减累计到1点再扣（避免每帧整数抖动）
                AccumulateDecay(dec);
            }
        }

        // 结局触发检查（只触发一次）
        CheckEndings();
    }

    // ---------- 衰减累积（避免每帧扣0点） ----------
    float _decayAcc;
    void AccumulateDecay(float dec)
    {
        _decayAcc += dec;
        if (_decayAcc >= 1f)
        {
            int d = Mathf.FloorToInt(_decayAcc);
            _decayAcc -= d;
            AddSan(-d, "decay");
        }
    }

    public bool IsProgressing() => _progressTimer > 0f;

    /// <summary>
    /// 外部调用：发生推进行为（读日记、连线、查看碎片、有效推测等）时调用一次，
    /// 使得在一段时间内不衰减。
    /// </summary>
    public void NotifyProgress(float activeSeconds = -1f)
    {
        float sec = (activeSeconds > 0f) ? activeSeconds : progressActiveWindow;
        _progressTimer = Mathf.Max(_progressTimer, sec);
        if (logDebug) Debug.Log($"[SanSystem] NotifyProgress {sec}s");
    }

    /// <summary>
    /// 外部统一入口：修改 SAN
    /// </summary>
    public void AddSan(int delta, string reason = null)
    {
        if (delta == 0) return;

        int before = San;
        int after = Mathf.Clamp(before + delta, minSan, maxSan);
        San = after;

        // 正向增长相关：触发引导句累积
        if (delta > 0)
        {
            HandlePositiveGain(delta, reason);
        }

        FireSanChanged(after - before, reason);

        if (logDebug)
            Debug.Log($"[SanSystem] SAN {before} -> {after} (d={after - before}) reason={reason}");

        // 变更后检查结局
        CheckEndings();
    }

    void HandlePositiveGain(int positiveDelta, string reason)
    {
        // 仅当“当前san > 85”时才计入“每+2触发一次”
        // 注意：你写的是“san值大于85时每增长2点触发一次引导句”
        if (San > highSanThreshold)
        {
            _highSanNetGainAccumulator += positiveDelta;

            while (_highSanNetGainAccumulator >= 2)
            {
                _highSanNetGainAccumulator -= 2;
                _highSanGuideCount++;
                OnHighSanGuideRequested?.Invoke(_highSanGuideCount, San, reason);
                if (logDebug)
                    Debug.Log($"[SanSystem] HighSAN guide #{_highSanGuideCount} san={San} reason={reason}");
            }
        }
        else
        {
            // 不在高san区间时，累计清零（避免跨阈值时奇怪地补触发）
            _highSanNetGainAccumulator = 0;
        }
    }

    void CheckEndings()
    {
        if (!_lostFired && San < 30)
        {
            _lostFired = true;
            OnLostEndingRequested?.Invoke();
            if (logDebug) Debug.Log("[SanSystem] Lost ending requested (san<30)");
        }

        if (!_fullFired && San >= 100)
        {
            _fullFired = true;
            OnFullClueAndChoiceRequested?.Invoke();
            if (logDebug) Debug.Log("[SanSystem] Full clue + choice requested (san>=100)");
        }
    }

    void FireSanChanged(int delta, string reason)
    {
        OnSanChanged?.Invoke(San, delta, reason);
    }

    // =====================================================================
    // 你要的“行为表”接入口
    // =====================================================================

    public enum PlayerActionType
    {
        EmotionalWords,       // 情绪化用语 -3
        OffStoryDialogue,     // 偏离剧情对话 -2
        LinkSuccess,          // 连线成功 +3
        ReadDiaryOver8s,      // 读日记/看道具信息>8s +2 (60s内仅触发一次，全局不重叠)
        SubmitValidHypothesis // 提交有效推测 +2
    }

    float _readDiaryCooldownUntil;

    /// <summary>
    /// 由你自己的系统在“玩家行为发生”时调用（不依赖AI判定）。
    /// 会自动触发 NotifyProgress，并计入“三次有效交互”。
    /// </summary>
    public void ApplyPlayerAction(PlayerActionType action, string contextId = null)
    {
        switch (action)
        {
            case PlayerActionType.EmotionalWords:
                AddSan(-3, "emotional");
                // 这种算不算推进？你描述“没有意义”一般不算推进，所以不NotifyProgress。
                break;

            case PlayerActionType.OffStoryDialogue:
                AddSan(-2, "offstory");
                break;

            case PlayerActionType.LinkSuccess:
                AddSan(+3, "link_success");
                NotifyProgress();
                RegisterValidInteraction();

                takeover?.OnLinkSuccess();          // ✅ 连线成功：-10 + 冻结
                takeover?.NotifyGuideAction();      // ✅ 连线属于引导行为（绿窗）
                break;

            case PlayerActionType.SubmitValidHypothesis:
                AddSan(+2, "valid_hypothesis");
                NotifyProgress();
                RegisterValidInteraction();

                takeover?.OnInferenceSuccess();     // ✅ 推测成功：-10
                break;

            case PlayerActionType.ReadDiaryOver8s:
                if (Time.time >= _readDiaryCooldownUntil)
                {
                    AddSan(+2, "read_diary_8s");
                    _readDiaryCooldownUntil = Time.time + 60f;

                    NotifyProgress();
                    RegisterValidInteraction();

                    takeover?.NotifyGuideAction();  // ✅ 阅读属于引导行为（绿窗）
                }
                else
                {
                    // 冷却内：仍算引导行为/推进（避免进入橙红）
                    NotifyProgress();
                    takeover?.NotifyGuideAction();
                }
                break;

        }
    }

    void RegisterValidInteraction()
    {
        _validInteractionCount++;

        if (_validInteractionCount % validInteractionPerGuide == 0)
        {
            _interactionGuideCount++;
            OnInteractionGuideRequested?.Invoke(_interactionGuideCount, _validInteractionCount);

            ToastSpawner.Instance?.Show("线索解锁 +1");

            if (logDebug)
                Debug.Log($"[SanSystem] Interaction guide #{_interactionGuideCount} totalValid={_validInteractionCount}");
        }
    }


    // =====================================================================
    // 对接 AI 判定结构 DecisionResult（你发的那个）
    // =====================================================================

    /// <summary>
    /// AI 判定接入口：直接把 DecisionResult 丢进来即可。
    /// sanDelta 会进入 AddSan；advance/tag/note 你后续要用可在reason里携带或再扩展事件。
    /// </summary>
    public void ApplyDecisionResult(DecisionResult dr, string rawPlayerText = null)
    {
        if (dr == null) return;

        // 1) 应用 san 变化（核心）
        if (dr.sanDelta != 0)
            AddSan(dr.sanDelta, $"ai:{dr.tag}:{dr.note}");

        // 2) “推进”判定：你可以把 advance>0 或 tag=push 视为推进
        bool consideredProgress =
            dr.advance > 0 ||
            string.Equals(dr.tag, "push", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(dr.tag, "accept", StringComparison.OrdinalIgnoreCase);

        if (consideredProgress)
        {
            NotifyProgress();
            RegisterValidInteraction(); // ✅ 你要的”每累计三次有效交互行为解锁引导句”

            if (dr.advance == 1)
                takeover?.NotifyAdvancePulse();
        }

        // 3) 负向/无意义行为：不推进（让衰减继续发生）
        // dr.advance<=0 或 tag=avoid/confuse/anger/idle 等，不做 NotifyProgress
    }
}
