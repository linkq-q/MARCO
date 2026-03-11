// MemoryBoardController.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

public class MemoryBoardController : MonoBehaviour
{
    [Header("Roots")]
    public RectTransform boardRoot;     // MemoryBoardRoot（通常就是自己）
    public RectTransform linesRoot;     // Lines 容器（必须）
    public RectTransform nodesRoot;     // Nodes 容器（可选，仅用于锁定）

    [Header("Scenario Binding")]
    public MemoryBoardScenario scenario;

    [Tooltip("按固定顺序拖入 6 个节点（index 0..5），用于映射 scenario 的文本和正确答案。")]
    public List<MemoryBoardNode> nodesByIndex = new List<MemoryBoardNode>(6);

    [Header("Config - Attempt")]
    [Tooltip("一次连线尝试需要点击的节点数（默认4）。")]
    public int nodesPerAttempt = 4;

    [Header("Config - Line")]
    public LineSegmentUI linePrefab;
    public float lineWidth = 8f;
    public Color lineNormal = new Color(0.85f, 0.95f, 1f, 1f);
    public Color lineError = Color.red;

    [Header("Fail Display")]
    [Tooltip("连完不正确：整组线标红持续多久后消失。")]
    public float failDisappearDelay = 1.5f;

    [Header("Completion")]
    public UnityEvent OnPuzzleCompleted;

    // =========================
    // ✅ Clue UI
    // =========================
    [Header("Clue UI (optional)")]
    [Tooltip("点击线索词后，将线索内容写入此文本区域（不填则不显示）。")]
    public TextMeshProUGUI clueOutputTMP;

    [Tooltip("是否在 clueOutputTMP 中追加（true=追加一段；false=替换）。")]
    public bool appendClueText = false;

    [Tooltip("追加模式下，每条线索之间的分隔符。")]
    public string clueAppendSeparator = "\n\n";

    [Header("Debug")]
    public bool logDebug = false;

    [Header("Auto Close Panel")]
    [Tooltip("连线成功后是否自动关闭连线面板")]
    public bool autoCloseOnSuccess = true;

    [Tooltip("关闭面板前的延迟（比如让 toast 看清楚一点）")]
    public float closeDelay = 0.1f;

    [Tooltip("要关闭的面板根对象。不填则默认关闭 boardRoot.gameObject")]
    public GameObject panelRootToClose;

    // ===== runtime (per attempt) =====
    MemoryBoardNode _selected = null;
    readonly List<MemoryBoardNode> _pick = new List<MemoryBoardNode>(4);
    readonly List<LineSegmentUI> _tempLines = new List<LineSegmentUI>(3);

    // ===== persistent lines (for undo, optional) =====
    readonly List<LineSegmentUI> lineStack = new List<LineSegmentUI>();

    bool completed = false;

    // 正确序列（由 Scenario 映射得到，长度=nodesPerAttempt）
    readonly List<MemoryBoardNode> _correctSequence = new List<MemoryBoardNode>(4);

    void Awake()
    {
        if (!boardRoot) boardRoot = transform as RectTransform;

        ApplyScenario(); // ✅ 自动从 Scenario 映射文本 + 正确答案
        AutoWireNodes(nodesByIndex);

        ResetBoardVisual();
    }

    void AutoWireNodes(List<MemoryBoardNode> list)
    {
        if (list == null) return;
        for (int i = 0; i < list.Count; i++)
        {
            var n = list[i];
            if (!n) continue;
            n.board = this;
        }
    }

    [ContextMenu("Apply Scenario Now")]
    public void ApplyScenario()
    {
        if (!scenario) return;

        // 1) 映射文本
        if (nodesByIndex != null && nodesByIndex.Count == 6 && scenario.nodeTexts != null && scenario.nodeTexts.Length == 6)
        {
            for (int i = 0; i < 6; i++)
            {
                var n = nodesByIndex[i];
                if (!n) continue;
                n.SetLabel(scenario.nodeTexts[i]);
            }
        }

        // 2) 映射正确答案（索引 -> Node 引用）
        _correctSequence.Clear();
        if (scenario.correctIndexSequence != null && scenario.correctIndexSequence.Length == nodesPerAttempt)
        {
            for (int i = 0; i < scenario.correctIndexSequence.Length; i++)
            {
                int idx = scenario.correctIndexSequence[i];
                if (idx < 0 || idx >= (nodesByIndex?.Count ?? 0)) continue;
                var n = nodesByIndex[idx];
                if (n) _correctSequence.Add(n);
            }
        }

        if (logDebug)
        {
            string seq = "";
            for (int i = 0; i < _correctSequence.Count; i++)
                seq += (i == 0 ? "" : " -> ") + _correctSequence[i].name;
            Debug.Log($"[MemoryBoard] ApplyScenario correctSequenceCount={_correctSequence.Count} seq={seq}", this);
        }
    }

    public void ResetPuzzle()
    {
        completed = false;

        // 清线（保留线栈也清掉）
        for (int i = lineStack.Count - 1; i >= 0; i--)
            if (lineStack[i]) Destroy(lineStack[i].gameObject);
        lineStack.Clear();

        // 清本轮
        _pick.Clear();
        _tempLines.Clear();
        _selected = null;

        ResetBoardVisual();
    }

    void ResetBoardVisual()
    {
        if (nodesByIndex != null)
        {
            for (int i = 0; i < nodesByIndex.Count; i++)
                if (nodesByIndex[i]) nodesByIndex[i].SetUnconfirmed();
        }

        MarkLineLast();
    }

    // =========================
    // ✅ Clue helpers
    // =========================
    int GetNodeIndex(MemoryBoardNode node)
    {
        if (nodesByIndex == null) return -1;
        return nodesByIndex.IndexOf(node); // 仅6个，O(n)无所谓
    }

    bool TryGetClueByNodeIndex(int nodeIndex, out string clueContent)
    {
        clueContent = null;
        if (!scenario) return false;

        var idxs = scenario.clueNodeIndices;
        var contents = scenario.clueContents;
        if (idxs == null || contents == null) return false;

        int len = Mathf.Min(idxs.Length, contents.Length);
        for (int i = 0; i < len; i++)
        {
            if (idxs[i] == nodeIndex)
            {
                clueContent = contents[i];
                return !string.IsNullOrEmpty(clueContent);
            }
        }
        return false;
    }

    void ShowClue(string clue)
    {
        if (string.IsNullOrEmpty(clue)) return;

        if (clueOutputTMP)
        {
            if (appendClueText && !string.IsNullOrEmpty(clueOutputTMP.text))
                clueOutputTMP.text = clueOutputTMP.text + clueAppendSeparator + clue;
            else
                clueOutputTMP.text = clue;
        }

        if (scenario && scenario.toastOnClueClicked && ToastSpawner.Instance != null)
        {
            var t = string.IsNullOrEmpty(scenario.clueToastText) ? "线索已记录" : scenario.clueToastText;
            ToastSpawner.Instance.Show(t);
        }
    }

    // ======== CORE: 4 nodes per attempt, always white while linking ========
    public void OnNodeClicked(MemoryBoardNode node)
    {
        if (completed) return;
        if (!node) return;

        // ✅ 线索词：点击就显示线索（不影响后续连线）
        int nodeIndex = GetNodeIndex(node);
        if (nodeIndex >= 0 && TryGetClueByNodeIndex(nodeIndex, out var clue))
        {
            ShowClue(clue);
        }

        if (_pick.Count >= nodesPerAttempt) return; // 已满，等本轮结束

        // 第一次：记录起点
        if (_pick.Count == 0)
        {
            _pick.Add(node);
            _selected = node;
            return;
        }

        // 第2~4次：先画白线（不判错、不提示）
        var a = _selected.GetComponent<RectTransform>();
        var b = node.GetComponent<RectTransform>();

        var line = SpawnLineInternal(a, b, lineNormal);
        if (line) _tempLines.Add(line);

        _pick.Add(node);
        _selected = node;

        // 点满4个：统一判定
        if (_pick.Count == nodesPerAttempt)
        {
            bool ok = IsAttemptCorrect();

            if (ok)
            {
                for (int i = 0; i < _tempLines.Count; i++)
                {
                    var l = _tempLines[i];
                    if (!l) continue;
                    lineStack.Add(l);
                }
                MarkLineLast();

                completed = true;

                // ✅ 成功 toast
                if (scenario && ToastSpawner.Instance != null && !string.IsNullOrEmpty(scenario.completedToastText))
                    ToastSpawner.Instance.Show(scenario.completedToastText);

                // ✅ 发放“连线绑定的记忆碎片”（不进抽取池）
                TryGrantLinkedShardReward();

                OnPuzzleCompleted?.Invoke();

                // ✅ 自动关闭面板
                if (autoCloseOnSuccess)
                    StartCoroutine(CoClosePanel());
            }
            else
            {
                if (logDebug) Debug.Log("[MemoryBoard] Attempt FAIL");

                var snapshot = new List<LineSegmentUI>(_tempLines);
                StartCoroutine(CoFailAndClearTempLines(snapshot));
            }

            // 结束本轮（不影响 snapshot）
            _pick.Clear();
            _tempLines.Clear();
            _selected = null;
        }
    }

    void TryGrantLinkedShardReward()
    {
        if (!scenario) return;
        var reward = scenario.linkedRewardItem;
        var logLine = scenario.rewardLogLine;

        if (!reward) return;

        // ✅ 强约束：必须是 MemoryShard（你要复用格式）
        if (reward.kind != ItemKind.MemoryShard)
            Debug.LogWarning($"[MemoryBoard] linkedRewardItem '{reward.id}' kind is not MemoryShard, but you said it should reuse shard format.");

        // ✅ 建议你在资源上勾上 excludeFromShardPool=true，确保不会被抽取到
        // 这里不强行改 ScriptableObject 值，避免你运行时污染资源；只提醒
        if (!reward.excludeFromShardPool)
            Debug.LogWarning($"[MemoryBoard] linkedRewardItem '{reward.id}' excludeFromShardPool is FALSE. 建议勾上，避免进入抽取池。");

        // 入库（不重复）
        if (InventoryManager.Instance != null)
        {
            bool alreadyHas = InventoryManager.Instance.HasItem(reward.id);
            InventoryManager.Instance.AddItem(reward);

            // 可选：新增道具 toast（你也可以不需要）
            if (!alreadyHas && ToastSpawner.Instance != null)
                ToastSpawner.Instance.Show($"获得记忆碎片：{reward.displayName}");

            // 可选：追加语料（为空就不追加）
            if (!string.IsNullOrWhiteSpace(logLine))
                InventoryManager.Instance.AppendLogToItem(reward, logLine);
        }
    }

    IEnumerator CoClosePanel()
    {
        if (closeDelay > 0.001f)
            yield return new WaitForSecondsRealtime(closeDelay);

        var root = panelRootToClose ? panelRootToClose : (boardRoot ? boardRoot.gameObject : gameObject);
        if (root) root.SetActive(false);
    }

    void TryGrantLinkedReward()
    {
        if (!scenario) return;

        var reward = scenario.linkedRewardItem;
        var logLine = scenario.rewardLogLine;

        // 1) 如果配置了奖励道具：入库（不重复）
        if (reward && InventoryManager.Instance != null)
        {
            bool alreadyHas = InventoryManager.Instance.HasItem(reward.id);

            InventoryManager.Instance.AddItem(reward); // 内部已做去重 + 默认选中
                                                       // AddItem 本身不会 toast（你的 AddItem 只 DebugLog），所以这里我们可选补一条
                                                       // 但注意：AppendLogToItem 会自己 toast “获得新信息：xxx”
            if (!alreadyHas && ToastSpawner.Instance != null)
            {
                ToastSpawner.Instance.Show($"获得道具：{reward.displayName}");
            }

            // 2) 如果还配置了语料：追加到这个奖励道具
            if (!string.IsNullOrWhiteSpace(logLine))
            {
                InventoryManager.Instance.AppendLogToItem(reward, logLine);
            }

            return;
        }

        // 兜底：如果没配置 reward，但配了 logLine（你也可能想写到“当前选中道具”里）
        if (!string.IsNullOrWhiteSpace(logLine) && InventoryManager.Instance != null)
        {
            InventoryManager.Instance.AppendLogToSelected(logLine);
            return;
        }

        // 都没配：什么都不做
    }

    bool IsAttemptCorrect()
    {
        if (_correctSequence == null || _correctSequence.Count != nodesPerAttempt) return false;
        if (_pick.Count != nodesPerAttempt) return false;

        for (int i = 0; i < nodesPerAttempt; i++)
        {
            if (_pick[i] != _correctSequence[i]) return false;
        }
        return true;
    }

    IEnumerator CoFailAndClearTempLines(List<LineSegmentUI> lines)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            var l = lines[i];
            if (l && l.img) l.img.color = lineError;
        }

        yield return new WaitForSeconds(failDisappearDelay);

        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i]) Destroy(lines[i].gameObject);
        }
    }

    // ======== LINE DRAWING ========
    LineSegmentUI SpawnLineInternal(RectTransform a, RectTransform b, Color color)
    {
        if (!linePrefab || !linesRoot || !a || !b) return null;

        var canvas = linesRoot.GetComponentInParent<Canvas>();
        Camera uiCam = null;
        if (canvas != null)
            uiCam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

        Vector3 aWorld = a.TransformPoint(a.rect.center);
        Vector3 bWorld = b.TransformPoint(b.rect.center);

        Vector2 aLocal = WorldToLocal(linesRoot, aWorld, uiCam);
        Vector2 bLocal = WorldToLocal(linesRoot, bWorld, uiCam);

        Vector2 mid = (aLocal + bLocal) * 0.5f;
        Vector2 dir = (bLocal - aLocal);
        float len = dir.magnitude;

        var line = Instantiate(linePrefab, linesRoot);
        line.board = this;

        var rt = line.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = mid;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        rt.localRotation = Quaternion.Euler(0, 0, angle);
        rt.sizeDelta = new Vector2(len, lineWidth);

        if (line.img)
        {
            line.img.color = new Color(color.r, color.g, color.b, 1f);
            line.img.raycastTarget = true;
        }

        return line;
    }

    static Vector2 WorldToLocal(RectTransform parent, Vector3 worldPos, Camera uiCam)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parent,
            RectTransformUtility.WorldToScreenPoint(uiCam, worldPos),
            uiCam,
            out var local
        );
        return local;
    }

    // ======== UNDO (optional) ========
    void MarkLineLast()
    {
        for (int i = 0; i < lineStack.Count; i++)
            if (lineStack[i]) lineStack[i].isLast = false;

        if (lineStack.Count > 0 && lineStack[^1])
            lineStack[^1].isLast = true;
    }

    public void OnLineRightClicked(LineSegmentUI line)
    {
        if (completed) return;

        if (lineStack.Count == 0 || line == null) return;

        var last = lineStack[^1];
        if (line != last) return; // 仅撤回末尾

        UndoLastStep();
    }

    void UndoLastStep()
    {
        if (lineStack.Count == 0) return;

        var lastLine = lineStack[^1];
        lineStack.RemoveAt(lineStack.Count - 1);
        if (lastLine) Destroy(lastLine.gameObject);

        MarkLineLast();
    }
}