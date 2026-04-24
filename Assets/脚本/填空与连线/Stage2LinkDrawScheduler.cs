using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stage2 连线抽取调度器：
/// - 进入二阶段后：每隔固定时间触发一次“连线事件”
/// - 抽取不放回：每个 Scenario 只会被触发一次（直到你手动 ResetPool/重新进入二阶段）
/// - 进入连线面板后：暂停抽取计时；面板关闭后再继续计时
///
/// 用法：
/// 1) 把该脚本挂到一个常驻物体（或StageManager物体）
/// 2) 在 Inspector 里绑定：board、boardPanelRoot、scenarioPool、intervalSeconds
/// 3) 当你进入二阶段时调用 EnterStage2()（从你的 StageManager/任务推进处调用）
///    退出二阶段时调用 ExitStage2()
/// </summary>
public class Stage2LinkDrawScheduler : MonoBehaviour
{
    [Header("Stage2 Control")]
    [Tooltip("外部进入二阶段时调用 EnterStage2()。离开二阶段时调用 ExitStage2()。")]
    public bool debugAutoEnterOnPlay = false;

    [Header("Interval")]
    [Tooltip("每次抽取触发的间隔秒数（建议用 unscaled，避免暂停影响）")]
    public float intervalSeconds = 45f;

    [Tooltip("改为由对话关键词手动触发时，关闭时间循环，只保留不放回抽取。")]
    public bool triggerByDialogueOnly = true;

    [Tooltip("首次进入二阶段后，是否立刻抽一次（不等待 interval）")]
    public bool triggerImmediatelyOnEnter = false;

    [Header("MemoryBoard")]
    [Tooltip("你的连线板控制器（MemoryBoardController）")]
    public MemoryBoardController board;

    [Tooltip("连线面板根节点（用于判断是否正在连线中；也用于打开/关闭）。不填则用 board.gameObject")]
    public GameObject boardPanelRoot;

    [Header("Scenario Pool (No-Replace)")]
    [Tooltip("二阶段可抽取的连线 Scenario 列表（不放回）")]
    public List<MemoryBoardScenario> scenarioPool = new List<MemoryBoardScenario>();

    [Tooltip("抽取前是否随机洗牌（推荐 true）")]
    public bool shuffleOnEnter = true;

    [Header("Debug")]
    public bool logDebug = false;

    // ===== runtime =====
    bool _stage2Active;
    Coroutine _co;
    readonly List<MemoryBoardScenario> _bag = new List<MemoryBoardScenario>();
    int _bagIndex = 0;
    bool _isTriggering;

    void Awake()
    {
        if (!board && logDebug) Debug.LogWarning("[Stage2LinkDrawScheduler] board 未绑定。", this);
        if (!boardPanelRoot && board) boardPanelRoot = board.gameObject;
    }

    void Start()
    {
        if (debugAutoEnterOnPlay) EnterStage2();
    }

    /// <summary>外部：进入二阶段时调用</summary>
    public void EnterStage2()
    {
        if (_stage2Active) return;
        _stage2Active = true;

        RefillBag();

        if (!triggerByDialogueOnly)
        {
            if (_co != null) StopCoroutine(_co);
            _co = StartCoroutine(CoLoop());
        }

        if (logDebug) Debug.Log("[Stage2LinkDrawScheduler] EnterStage2", this);
    }

    /// <summary>外部：离开二阶段时调用</summary>
    public void ExitStage2()
    {
        _stage2Active = false;
        _isTriggering = false;

        if (_co != null) StopCoroutine(_co);
        _co = null;

        if (logDebug) Debug.Log("[Stage2LinkDrawScheduler] ExitStage2", this);
    }

    /// <summary>
    /// 手动重置“不放回袋子”，允许再次抽取（例如你想在二阶段内循环第二轮）。
    /// </summary>
    [ContextMenu("Reset Pool Bag")]
    public void ResetPoolBag()
    {
        RefillBag();
        if (logDebug) Debug.Log("[Stage2LinkDrawScheduler] ResetPoolBag", this);
    }

    public void TryTriggerNextScenario()
    {
        if (!_stage2Active) return;
        if (_isTriggering) return;
        if (IsBoardOpen()) return;

        StartCoroutine(CoTriggerOnceManual());
    }

    void RefillBag()
    {
        _bag.Clear();
        _bagIndex = 0;

        // 过滤 null
        for (int i = 0; i < scenarioPool.Count; i++)
        {
            var s = scenarioPool[i];
            if (s) _bag.Add(s);
        }

        if (shuffleOnEnter) Shuffle(_bag);

        if (logDebug) Debug.Log($"[Stage2LinkDrawScheduler] Bag ready count={_bag.Count}", this);
    }

    IEnumerator CoLoop()
    {
        // 可选：立即触发一次
        if (triggerImmediatelyOnEnter)
        {
            yield return WaitUntilBoardClosed();
            yield return TriggerOne();
        }

        while (_stage2Active)
        {
            // 关键：如果面板正在打开（正在连线），暂停计时直到关闭
            yield return WaitUntilBoardClosed();

            // 计时（unscaled）
            float t = 0f;
            while (_stage2Active && t < intervalSeconds)
            {
                // 面板被打开了：暂停计时，等它关掉后继续（不丢进度）
                if (IsBoardOpen())
                {
                    if (logDebug) Debug.Log("[Stage2LinkDrawScheduler] Board opened -> pause timer", this);
                    yield return WaitUntilBoardClosed();
                    continue;
                }

                t += Time.unscaledDeltaTime;
                yield return null;
            }

            if (!_stage2Active) yield break;

            // 触发一次
            yield return TriggerOne();
        }
    }

    IEnumerator TriggerOne()
    {
        if (board == null)
        {
            if (logDebug) Debug.LogWarning("[Stage2LinkDrawScheduler] board==null, skip trigger.", this);
            yield break;
        }

        // 不放回：袋子空了就停止（或你也可以选择自动 refill）
        if (_bagIndex >= _bag.Count)
        {
            if (logDebug) Debug.Log("[Stage2LinkDrawScheduler] Bag exhausted. Stop triggering (no-replace).", this);
            yield break;
        }

        var scenario = _bag[_bagIndex];
        _bagIndex++;

        if (!scenario)
            yield break;

        if (logDebug) Debug.Log($"[Stage2LinkDrawScheduler] Trigger scenario: {scenario.name}", this);

        // ✅ 先设置 scenario + 重置，再打开面板，确保面板显示内容与数据索引一致
        board.SetScenario(scenario);
        board.ResetPuzzle();
        OpenBoardPanel();

        // 等面板关闭（你的 MemoryBoardController 成功后会 SetActive(false) 时，这里就会继续）
        yield return WaitUntilBoardClosed();
    }

    void OpenBoardPanel()
    {
        var root = boardPanelRoot ? boardPanelRoot : (board ? board.gameObject : null);
        if (root && !root.activeSelf) root.SetActive(true);
    }

    bool IsBoardOpen()
    {
        var root = boardPanelRoot ? boardPanelRoot : (board ? board.gameObject : null);
        return root && root.activeInHierarchy;
    }

    IEnumerator WaitUntilBoardClosed()
    {
        // 如果根节点为空，当作“永远关闭”
        var root = boardPanelRoot ? boardPanelRoot : (board ? board.gameObject : null);
        if (!root) yield break;

        while (_stage2Active && root.activeInHierarchy)
            yield return null;
    }

    static void Shuffle<T>(List<T> list)
    {
        // Fisher–Yates
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    IEnumerator CoTriggerOnceManual()
    {
        _isTriggering = true;
        yield return TriggerOne();
        _isTriggering = false;
    }
}
