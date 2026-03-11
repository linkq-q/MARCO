using UnityEngine;
using TMPro;
using UnityEngine.Events;
using System.Collections;

/// <summary>
/// 根据“推测正确数 / 连线成功数”自动切换阶段：
/// - Stage1_Explore：正确数 >= stage2_needCorrectAtLeast -> Stage2_Rift
/// - Stage2_Rift：连线数 >= stage3_needLinksAtLeast 且 正确数 >= stage3_needCorrectAtLeast -> Stage3_Dwarf
///
/// 说明：
/// - 阶段真值源：EchoRunState.I.stage / subState
/// - 提供 debug 日志（同帧 + 下一帧验证），用于排查“切了又被覆盖”
/// - 注意：脚本会在 InventoryChanged 时触发计算；连线成功需外部调用 NotifyLinkSolvedOnce()
/// </summary>
public class GuessStageGate_Auto : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("拖场景里真正使用的 InventoryUIController（不是 EventSystem）")]
    public InventoryUIController inventoryUI;

    [Tooltip("可选：显示正确计数的 TMP 文本")]
    public TextMeshProUGUI correctCountTMP;

    [Header("Stage Targets")]
    public EchoStage stage1 = EchoStage.Stage1_Explore;
    public EchoStage stage2 = EchoStage.Stage2_Rift;
    public EchoStage stage3 = EchoStage.Stage3_Dwarf;

    [Header("Conditions")]
    [Tooltip("进入 Stage1 后：正确数 >= 该值则进入 Stage2")]
    public int stage2_needCorrectAtLeast = 1;

    [Tooltip("进入 Stage2 后：连线完成次数 >=2 且 正确数 >=4 则进入 Stage3")]
    public int stage3_needLinksAtLeast = 2;
    public int stage3_needCorrectAtLeast = 4;

    [Header("Events")]
    [Tooltip("进入 Stage2 时触发（用来初始化/开启 Stage2 UI & Systems）")]
    public UnityEvent OnEnterStage2;

    [Tooltip("进入 Stage3 时触发")]
    public UnityEvent OnEnterStage3;

    [Header("Debug")]
    public bool logGate = true;

    int _correctCount = -1;
    int _linkSolvedCount = 0;

    bool _stage2Triggered = false;
    bool _stage3Triggered = false;

    Coroutine _verifyCo;

    void OnEnable()
    {
        // 订阅：注意 InventoryManager 可能比 Gate 晚初始化
        StartCoroutine(CoBindInventoryWhenReady());
    }

    void OnDisable()
    {
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged -= RecalcAndCheck;

        if (_verifyCo != null)
        {
            StopCoroutine(_verifyCo);
            _verifyCo = null;
        }
    }

    IEnumerator CoBindInventoryWhenReady()
    {
        while (InventoryManager.Instance == null)
            yield return null;

        InventoryManager.Instance.OnInventoryChanged -= RecalcAndCheck;
        InventoryManager.Instance.OnInventoryChanged += RecalcAndCheck;

        RecalcAndCheck();
    }

    void Start()
    {
        RecalcAndCheck();
    }

    /// <summary>
    /// 外部：每次“连线成功一次”时调用（MemoryBoardController 成功分支里调用）
    /// </summary>
    public void NotifyLinkSolvedOnce()
    {
        _linkSolvedCount++;
        if (logGate) Debug.Log($"[Gate] NotifyLinkSolvedOnce -> links={_linkSolvedCount}");
        RecalcAndCheck();
    }

    void RecalcAndCheck()
    {
        if (EchoRunState.I == null)
        {
            if (logGate) Debug.LogWarning("[Gate] EchoRunState.I is NULL");
            return;
        }

        // 1) correct count
        int now = (inventoryUI != null) ? inventoryUI.GetCorrectGuessCount() : 0;

        if (logGate)
            Debug.Log($"[Gate] tick stage={EchoRunState.I.stage} now={now} prev={_correctCount} " +
                      $"need2={stage2_needCorrectAtLeast} trig2={_stage2Triggered} links={_linkSolvedCount}");

        if (inventoryUI == null && logGate)
            Debug.LogWarning("[Gate] inventoryUI is NULL -> now forced to 0");

        if (now != _correctCount)
        {
            _correctCount = now;

            if (correctCountTMP != null)
                correctCountTMP.text = $"已正确匹配：{_correctCount}";
        }

        var curStage = EchoRunState.I.stage;

        // ===== Stage1 -> Stage2 =====
        if (curStage == stage1)
        {
            // 回到 Stage1：允许再次触发 Stage3
            _stage3Triggered = false;

            bool canEnter2 = (_correctCount >= stage2_needCorrectAtLeast);
            if (logGate) Debug.Log($"[Gate] check Stage1->2 canEnter2={canEnter2}");

            if (!_stage2Triggered && canEnter2)
            {
                ForceSetStage(stage2, 1);

                // 同帧验证
                if (logGate) Debug.Log($"[Gate] after ForceSetStage (same frame) stage={EchoRunState.I.stage}");

                // 下一帧验证：抓“被覆盖”
                StartVerify("S1->S2");

                // 只有确认切到 Stage2 才锁，避免“被覆盖后卡死”
                if (EchoRunState.I.stage == stage2)
                    _stage2Triggered = true;

                OnEnterStage2?.Invoke();
                ToastSpawner.Instance?.Show("进入阶段2");
            }
        }
        else
        {
            // 离开 Stage1：允许未来回到 Stage1 再触发
            _stage2Triggered = false;
        }

        // 重新读一次（避免调试误会）
        curStage = EchoRunState.I.stage;

        // ===== Stage2 -> Stage3 =====
        if (curStage == stage2)
        {
            bool canEnter3 = (_linkSolvedCount >= stage3_needLinksAtLeast) &&
                             (_correctCount >= stage3_needCorrectAtLeast);

            if (logGate)
                Debug.Log($"[Gate] check Stage2->3 canEnter3={canEnter3} " +
                          $"needLinks={stage3_needLinksAtLeast} needCorrect={stage3_needCorrectAtLeast}");

            if (!_stage3Triggered && canEnter3)
            {
                ForceSetStage(stage3, 1);

                if (logGate) Debug.Log($"[Gate] after ForceSetStage (same frame) stage={EchoRunState.I.stage}");

                StartVerify("S2->S3");

                if (EchoRunState.I.stage == stage3)
                    _stage3Triggered = true;

                OnEnterStage3?.Invoke();
                ToastSpawner.Instance?.Show("进入阶段3");
            }
        }
        else
        {
            _stage3Triggered = false;
        }
    }

    void StartVerify(string tag)
    {
        if (_verifyCo != null) StopCoroutine(_verifyCo);
        _verifyCo = StartCoroutine(CoVerifyStageNextFrame(tag));
    }

    IEnumerator CoVerifyStageNextFrame(string tag)
    {
        yield return null;
        if (EchoRunState.I == null) yield break;

        Debug.Log($"[Gate][VerifyNextFrame] {tag} stage={EchoRunState.I.stage} sub={EchoRunState.I.subState} pending={EchoRunState.I.hasPending}");
        _verifyCo = null;
    }

    void ForceSetStage(EchoStage s, int sub)
    {
        if (EchoRunState.I == null) return;
        EchoRunState.I.ForceSet(s, sub, "Gate");
    }
}