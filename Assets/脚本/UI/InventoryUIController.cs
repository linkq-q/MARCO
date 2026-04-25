//背包控制，管理背包开关，条目生成
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System;


public class InventoryUIController : MonoBehaviour
{
    [Header("Root")]
    public GameObject root;                 // 面板整体根节点
    public Button exitButton;               // 左上退出按钮

    [Header("Left List")]
    public RectTransform itemContent;       // ScrollView_Items/Viewport/Content
    public ScrollRect itemScrollRect;       // 左侧ScrollRect
    public GameObject itemRowPrefab;    // 左侧条目Prefab

    [Header("Right Header")]
    public Image iconImage;                 // 右侧图标
    public TextMeshProUGUI nameText;        // 右侧名称
    public TextMeshProUGUI guessPromptText;   // 显示：经过推测你认为这是_______ / 或已填答案
    public TMP_InputField guessInput;         // 玩家填空输入框
    public TextMeshProUGUI guessResponseText; // Echo 对推测的即时回应


    [Header("Right Body")]
    public GameObject panelLogs;      // 旧：道具语料面板
    public GameObject panelDiary;     // ✅ 新：日记面板根节点（你做的“日记面板”）

    [Header("Diary")]
    public string diaryItemId = "diary";  // ✅ 你的日记 ItemData.id（填真实的）
    public DiaryUI diaryUI;              // ✅ 你做的 DiaryUI 脚本（挂在日记面板上）


    [Header("Logs Scroll")]
    public RectTransform logsContent;       // ScrollView_Logs/Viewport/Content
    public ScrollRect logsScrollRect;       // 语料ScrollRect

    [Tooltip("一条语料的预制体（推荐：背景框Image + 子物体TMP文本）。")]
    public GameObject logLinePrefab;     

    [Header("Input")]
    public KeyCode toggleKey = KeyCode.Tab;

    [Header("Refs - Player Control")]
    public FirstPersonControllerSimple firstPerson;  // 拖你的玩家身上的 FirstPersonControllerSimple

    [Header("Diary Guess Prompt")]
    [TextArea(2, 6)]
    public string diaryGuessPromptText = "（这里填你想显示的日记提示语）";

    [Serializable]
    public class GuessAnswerEntry
    {
        public string itemId;                 // 对应 ItemData.id
        public List<string> acceptableAnswers; // 可接受答案（同义词）
    }

    [Header("Guess Answers")]
    public List<GuessAnswerEntry> guessAnswers = new List<GuessAnswerEntry>();

    [Header("Guess Colors")]
    public Color correctColor = Color.white;
    public Color wrongColor = Color.red;

    [Header("Guess Wrong Behavior")]
    public float wrongKeepSeconds = 2f;     // 红色保持时间
    public bool clearWrongAnswer = true;    // 错误后是否清空答案（建议 true）

    [Header("Guess Feedback")]
    public string guessResponseLoadingText = "Echo在想……";
    public string guessResponseFallbackText = "嗯……这个方向不算离谱，但还差一点。";

    public GameObject panelMemoryShard;     // ✅ 新：记忆碎片面板根节点
    public MemoryShardUI memoryShardUI;     // ✅ 新：显示文本的 UI 脚本（挂在面板上）

    [Header("Memory Shard Prompt (Template)")]
    [TextArea(2, 6)]
    public string memoryGuessPromptTemplate = "（这是一段记忆碎片）";

    string _readingKeyActive = null;

    public ReadStayTracker readStayTracker;

    bool endingLocked;

    public InteractableClickSystem clickSystem;

    public StoryTaskManager storyTaskManager;

    // 运行时缓存生成出来的左侧UI条目
    readonly List<InventoryItemRowUI> rowUIs = new List<InventoryItemRowUI>();

    bool isOpen;
    float _lastNorm = -1;
    int _guessFeedbackSerial;

    void Start()
    {
        //检查引用是否齐全
        if (!ValidateRefs())
        {
            enabled = false;
            return;
        }

        root.SetActive(false);
        isOpen = false;

        if (exitButton) exitButton.onClick.AddListener(Close);
        if (guessInput) guessInput.onEndEdit.AddListener(OnGuessSubmitted);



        //确保单例存在
        if (InventoryManager.Instance != null)
        {
            //订阅
            InventoryManager.Instance.OnInventoryChanged += RebuildLeftList;
            InventoryManager.Instance.OnSelectedChanged += RefreshRightPanel;
        }

    }

    void OnDestroy()
    {
        if (InventoryManager.Instance == null) return;
        //取消订阅
        InventoryManager.Instance.OnInventoryChanged -= RebuildLeftList;
        InventoryManager.Instance.OnSelectedChanged -= RefreshRightPanel;
    }

    void Update()
    {
        if (endingLocked) return;
        if (Input.GetKeyDown(toggleKey))
        {
            if (isOpen) Close();
            else Open();
        }
    }


    public void Open()
    {
        isOpen = true;
        root.SetActive(true);
        FindFirstObjectByType<IdleDetector>()?.SetPaused(true);//暂停发呆计时

        // 记录打开背包行为
        storyTaskManager.NotifyPanelOpened();
        if (firstPerson != null)
        {
            firstPerson.SetCursorLock(false);
            firstPerson.allowCursorToggle = false;
        }



        // 触发背包语料（废弃）
        SnarkRouter.I?.Arm();                  // 开局静默解锁
        SnarkRouter.I?.Say(SnarkType.Inventory);


        RebuildLeftList();
        RefreshRightPanel(InventoryManager.Instance != null ? InventoryManager.Instance.Selected : null);
        StartCoroutine(FixLogsAfterOpen());
        if (clickSystem) clickSystem.enableWorldClick = false;

    }



    IEnumerator FixLogsAfterOpen()
    {
        yield return null; // 等 ScrollRect OnEnable
        yield return null; // 等布局/滚动归位
        Canvas.ForceUpdateCanvases();

        if (logsScrollRect)
        {
            logsScrollRect.velocity = Vector2.zero;
            logsScrollRect.verticalNormalizedPosition = 0f; // 要底部就 0
        }
    }

    IEnumerator PinLogsForAFewFrames()
    {
        for (int i = 0; i < 6; i++)
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            logsScrollRect.velocity = Vector2.zero;
            logsScrollRect.verticalNormalizedPosition = 0f;
        }
    }


    public void Close()
    { 
    
        EndReadStay();

        //关闭UI并恢复时间
        isOpen = false;
        root.SetActive(false);
        if (firstPerson != null)
        {
            firstPerson.allowCursorToggle = true;
            firstPerson.SetCursorLock(true);
        }



        FindFirstObjectByType<IdleDetector>()?.SetPaused(false);

        if (clickSystem) clickSystem.enableWorldClick = true;
    }

    void RebuildLeftList()
    {
        if (InventoryManager.Instance == null) return;

        // 清空旧条目
        for (int i = 0; i < rowUIs.Count; i++)
        {
            if (rowUIs[i]) Destroy(rowUIs[i].gameObject);
        }
        rowUIs.Clear();

        //对当前已经拾取的道具进行初始化
        var owned = InventoryManager.Instance.Owned;
        for (int i = 0; i < owned.Count; i++)
        {
            var rt = owned[i];
            var go = Instantiate(itemRowPrefab, itemContent);
            var row = go.GetComponent<InventoryItemRowUI>();
            if (row == null)
            {
                Debug.LogError("[InventoryUIController] Item Row Prefab 上找不到 InventoryItemRowUI 组件！请检查道具条目模板 Prefab。", go);
                Destroy(go);
                continue;
            }

            rowUIs.Add(row);
            row.Bind(rt);


        }

        //新拾取自动滚到最底，显示最新道具
        if (owned.Count > 0 && itemScrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            itemScrollRect.verticalNormalizedPosition = 0f; // 0=底部，1=顶部
        }
    }

void RefreshRightPanel(InventoryManager.ItemRuntime rt)
{
        if (!isOpen || root == null || !root.activeInHierarchy)
        {
            EndReadStay();
            return;
        }

        // ✅ 统一重置：避免面板状态残留
        if (panelLogs) panelLogs.SetActive(false);
        if (panelDiary) panelDiary.SetActive(false);
        if (panelMemoryShard) panelMemoryShard.SetActive(false);

        // 没选中/没数据
        if (rt == null || rt.data == null)
    {
        if (iconImage) iconImage.sprite = null;
        if (nameText) nameText.text = "";

            if (guessPromptText) guessPromptText.text = InventoryManager.GuessTemplate;
            if (guessInput) guessInput.SetTextWithoutNotify("");
            if (guessResponseText)
            {
                guessResponseText.text = "";
                guessResponseText.gameObject.SetActive(false);
            }


            if (panelLogs) panelLogs.SetActive(true);
        if (panelDiary) panelDiary.SetActive(false);

        ClearLogs();

        // ✅ 没内容就别 Begin
        return;
    }

    if (iconImage) iconImage.sprite = rt.data.icon;
    if (nameText) nameText.text = rt.data.displayName;

        //日记分支
        bool isDiary = (!string.IsNullOrEmpty(diaryItemId) && rt.data.id == diaryItemId);

        if (isDiary)
        {
            if (panelDiary) panelDiary.SetActive(true);

            if (guessPromptText)
            {
                guessPromptText.gameObject.SetActive(true);
                guessPromptText.text = diaryGuessPromptText;   // ✅ 来自 Inspector，可修改
            }

            if (guessInput)
            {
                guessInput.gameObject.SetActive(false);        // ✅ 日记仍不允许填空
                guessInput.SetTextWithoutNotify("");            // 可选：顺便清空
            }

            // ✅ 关键：日记强制设置key
            if (readStayTracker) readStayTracker.contextKey = "diary_main";

            BeginReadStayForCurrentPanel(); // 或者你也可以直接 readStayTracker.Begin()

            return;
        }

        //记忆碎片分支
        if (rt.data.kind == ItemKind.MemoryShard)
        {
            // 面板开关
            if (panelMemoryShard) panelMemoryShard.SetActive(true);

            // 右侧提示语（ItemData 可覆盖，否则用全局默认）
            if (guessPromptText)
            {
                guessPromptText.gameObject.SetActive(true);
                guessPromptText.color = correctColor;

                string prompt = !string.IsNullOrWhiteSpace(rt.data.memoryPromptOverride)
                    ? rt.data.memoryPromptOverride
                    : memoryGuessPromptTemplate; // 你InventoryUIController里已有的全局模板字段

                guessPromptText.text = prompt;
            }

            // 输入框隐藏（不填空）
            if (guessInput)
            {
                guessInput.gameObject.SetActive(false);
                guessInput.SetTextWithoutNotify("");
            }

            // ✅ 显示内容：优先预制体，其次文本
            if (memoryShardUI != null)
            {
                if (rt.data.memoryContentPrefab != null)
                {
                    memoryShardUI.ShowPrefab(rt.data.memoryContentPrefab, rt.data, scrollToTop: true);
                }
                else
                {
                    // 如果你现在的 MemoryShardUI 是“预制体版”，这里有两种选择：
                    // 1) 你做一个“通用文本预制体”，把文本塞进去（推荐）
                    // 2) 给 MemoryShardUI 再加一个 ShowTextFallback（我下面给你）
                    memoryShardUI.ShowTextFallback(rt.data.memoryFixedText, scrollToTop: true);
                }
            }

            // 阅读计时 key
            if (readStayTracker) readStayTracker.contextKey = "mem_" + rt.data.id;
            BeginReadStayForCurrentPanel();

            return;
        }

        // 普通道具语料分支
        bool hasAnswer = !string.IsNullOrWhiteSpace(rt.guessAnswer);

        if (guessPromptText)
        {
            guessPromptText.gameObject.SetActive(true);
            guessPromptText.text = InventoryManager.Instance.GetGuessPromptForSelected();

            // 每次刷新都重新算一次颜色（保证一致）
            bool correct = IsGuessCorrect(rt.data.id, rt.guessAnswer);
            guessPromptText.color = hasAnswer ? (correct ? correctColor : wrongColor) : correctColor;
        }

        if (guessInput)
        {
            // 没填过才显示输入框
            guessInput.gameObject.SetActive(!hasAnswer);
            guessInput.SetTextWithoutNotify(""); // 你要提交后必隐藏，所以这里别回填
        }

        if (panelLogs) panelLogs.SetActive(true);

    // ✅ 设置当前阅读key（用于60s冷却里“不同道具不重叠”的全局规则你已经在SanSystem里做成全局冷却了）
    if (readStayTracker) readStayTracker.contextKey = "item_" + rt.data.id;


        BuildLogs(rt.logs);

    // ✅ 切到道具：Begin（key 会是 item_xxx）
    BeginReadStayForCurrentPanel();
}

    Coroutine _coScroll;

    void BuildLogs(List<string> logs)
    {
        //清空旧语料，实例化语料后填入文本
        ClearLogs();
        if (logs == null || logLinePrefab == null) return;

        for (int i = 0; i < logs.Count; i++)
        {
            var go = Instantiate(logLinePrefab, logsContent);
            var tmp = go.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp) tmp.text = logs[i];
        }

        //只保留最后一次滚动协程，避免多次刷新互相打架
        if (_coScroll != null) StopCoroutine(_coScroll);
        _coScroll = StartCoroutine(ScrollLogsStable(toBottom: true));
    }

    System.Collections.IEnumerator ScrollLogsStable(bool toBottom)
    {
        // 先等一帧，让 LayoutGroup/SizeFitter 把尺寸算出来
        yield return null;

        // 强制重建布局
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(logsContent);

        // 再等到本帧结束，确保 ScrollRect 内部也更新完
        yield return new WaitForEndOfFrame();

        if (!logsScrollRect) yield break;

        // 清掉惯性速度
        logsScrollRect.velocity = Vector2.zero;

        // 最后钉位置
        logsScrollRect.verticalNormalizedPosition = toBottom ? 0f : 1f;

        //保险再钉一遍
        yield return null;
        logsScrollRect.velocity = Vector2.zero;
        logsScrollRect.verticalNormalizedPosition = toBottom ? 0f : 1f;

        _coScroll = null;
    }


    System.Collections.IEnumerator FixLogsScrollNextFrame(bool toBottom)
    {
        yield return null;                  // 下一帧
        Canvas.ForceUpdateCanvases();       // 再强制刷新一次
        if (logsScrollRect)
            logsScrollRect.verticalNormalizedPosition = toBottom ? 0f : 1f;
    }


    void ClearLogs()
    {
        if (logsContent == null) return;

        for (int i = logsContent.childCount - 1; i >= 0; i--)
            Destroy(logsContent.GetChild(i).gameObject);
    }

    //引用不全禁用脚本
    bool ValidateRefs()
    {
        bool ok = true;

        void Check(object obj, string fieldName)
        {
            if (obj == null)
            {
                Debug.LogError($"[InventoryUIController] Missing reference: {fieldName}", this);
                ok = false;
            }
        }

        //根
        Check(root, nameof(root));
        Check(exitButton, nameof(exitButton));

        //左列
        Check(itemContent, nameof(itemContent));
        Check(itemScrollRect, nameof(itemScrollRect));
        Check(itemRowPrefab, nameof(itemRowPrefab));

        //右上
        Check(iconImage, nameof(iconImage));
        Check(nameText, nameof(nameText));
        Check(guessPromptText, nameof(guessPromptText));
        Check(guessInput, nameof(guessInput));


        //语料刷新
        Check(panelLogs, nameof(panelLogs));
        Check(logsContent, nameof(logsContent));
        Check(logsScrollRect, nameof(logsScrollRect));
        Check(logLinePrefab, nameof(logLinePrefab)); // ✅ 现在这里可以拖“背景框预制体”了
                                                     // 记忆碎片面板
        Check(panelMemoryShard, nameof(panelMemoryShard));
        Check(memoryShardUI, nameof(memoryShardUI));

        return ok;
    }

    void BeginReadStayForCurrentPanel()
    {
        if (!readStayTracker) return;
        if (!isOpen || root == null || !root.activeInHierarchy)
        {
            EndReadStay();
            return;
        }

        var sel = InventoryManager.Instance != null ? InventoryManager.Instance.Selected : null;

        bool isDiarySel = (sel != null && sel.data != null &&
                           !string.IsNullOrEmpty(diaryItemId) &&
                           sel.data.id == diaryItemId);

        bool isMemSel = (sel != null && sel.data != null &&
                         sel.data.kind == ItemKind.MemoryShard);

        string key =
            isDiarySel ? "diary_main" :
            isMemSel ? ("mem_" + sel.data.id) :
            (sel != null && sel.data != null ? ("item_" + sel.data.id) : "item_none");

        if (_readingKeyActive == key) return;

        if (_readingKeyActive != null)
            readStayTracker.End();

        readStayTracker.contextKey = key;
        _readingKeyActive = key;
        readStayTracker.Begin();
    }


    void EndReadStay()
    {
        if (!readStayTracker) return;
        if (_readingKeyActive == null) return;

        // ✅ 现在奖励在 8s 到达时自动触发，这里只负责停表
        readStayTracker.End();

        _readingKeyActive = null;
    }

    void OnGuessInputChanged(string text)
    {
        if (InventoryManager.Instance == null) return;

        // 把玩家输入写回 InventoryManager（会触发 OnSelectedChanged / OnInventoryChanged）
        InventoryManager.Instance.SetGuessAnswer(text);

        // 顺手刷新 prompt 文案（不依赖事件也行）
        if (guessPromptText)
            guessPromptText.text = InventoryManager.Instance.GetGuessPromptForSelected();
        if (guessInput)
        {
            guessInput.gameObject.SetActive(false);
            guessInput.SetTextWithoutNotify("");
        }



    }
    void OnGuessSubmitted(string text)
    {
        if (InventoryManager.Instance == null) return;

        var sel = InventoryManager.Instance.Selected;
        if (sel == null || sel.data == null) return;

        string answer = (text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(answer)) return;

        RequestGuessFeedback(sel, answer);

        // 先判定对错（不要先写入，否则错了就把输入锁死）
        bool correct = IsGuessCorrect(sel.data.id, answer);

        bool alreadyHadAnswer = !string.IsNullOrWhiteSpace(sel.guessAnswer);

        if (correct)
        {
            // ✅ 首次正确：加SAN + toast（避免重复刷）
            if (!alreadyHadAnswer)
            {
                var sanSys = FindFirstObjectByType<SanSystem>();
                if (sanSys != null)
                {
                    sanSys.ApplyPlayerAction(SanSystem.PlayerActionType.SubmitValidHypothesis);
                }
                ToastSpawner.Instance?.Show("推测成立：San +2");
            }

            InventoryManager.Instance.SetGuessAnswer(answer);
}
        else
        {
            // ❌ 错误：不写入（或写入后立刻清掉），先红色提示几秒，再恢复可填写
            if (guessPromptText)
            {
                // 这里显示玩家填的内容更直观；你也可以继续用模板
                guessPromptText.text = "经过推测你认为这是" + answer;
                guessPromptText.color = wrongColor;
            }

            if (guessInput)
            {
                // 错误时不要立刻隐藏，让玩家看到自己输入了什么（也可隐藏，看你习惯）
                // 我这里选择隐藏输入框，等待几秒后重新打开，避免玩家手滑继续输入导致状态乱
                guessInput.gameObject.SetActive(false);
            }

            // 启动恢复流程（只恢复当前这个道具）
            StopCoroutine(nameof(CoRecoverWrongGuess));
            StartCoroutine(CoRecoverWrongGuess(sel.data.id));
        }
    }

    IEnumerator CoRecoverWrongGuess(string itemId)
    {
        // 保持红色一段时间
        yield return new WaitForSeconds(wrongKeepSeconds);

        // 期间可能玩家切换了选中道具 / 关闭背包 / 进入结局锁定
        if (endingLocked) yield break;
        if (!isOpen) yield break;

        var mgr = InventoryManager.Instance;
        if (mgr == null) yield break;

        var sel = mgr.Selected;
        if (sel == null || sel.data == null) yield break;

        // 只对“仍然是同一个道具”生效
        if (sel.data.id != itemId) yield break;

        // 清空错误答案，恢复到“_______”
        if (clearWrongAnswer)
        {
            // 这会触发 OnSelectedChanged → RefreshRightPanel，自动把输入框打开（hasAnswer=false）
            mgr.SetGuessAnswer("");
        }
        else
        {
            // 不清答案的话，必须手动把输入框再打开，否则 hasAnswer=true 会继续锁住
            if (guessPromptText)
            {
                guessPromptText.text = InventoryManager.GuessTemplate;
                guessPromptText.color = correctColor;
            }

            if (guessInput)
            {
                guessInput.SetTextWithoutNotify("");
                guessInput.gameObject.SetActive(true);
                guessInput.ActivateInputField();
            }
        }
    }

    async void RequestGuessFeedback(InventoryManager.ItemRuntime rt, string answer)
    {
        if (rt == null || rt.data == null) return;

        int serial = ++_guessFeedbackSerial;

        string feedback = null;
        try
        {
            if (AIBroker.Instance != null)
                feedback = await AIBroker.Instance.GenerateGuessFeedbackAsync(rt.data.displayName, answer);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[InventoryUIController] Guess feedback failed: " + e.Message);
        }

        if (serial != _guessFeedbackSerial) return;

        rt.guessFeedback = string.IsNullOrWhiteSpace(feedback)
            ? guessResponseFallbackText
            : feedback.Trim();

        AIBroker.Instance?.PlayTtsLine(rt.guessFeedback, "neutral");
    }

    public bool IsGuessCorrect(string itemId, string answer)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return false;
        if (string.IsNullOrWhiteSpace(answer)) return false;

        string a = Normalize(answer);

        for (int i = 0; i < guessAnswers.Count; i++)
        {
            var e = guessAnswers[i];
            if (e == null) continue;
            if (e.itemId != itemId) continue;

            if (e.acceptableAnswers == null) return false;

            for (int k = 0; k < e.acceptableAnswers.Count; k++)
            {
                string ok = Normalize(e.acceptableAnswers[k]);
                if (!string.IsNullOrEmpty(ok) && ok == a)
                    return true;
            }
            return false;
        }

        // 找不到配置：默认判错（你也可以改成默认判对/不变色）
        return false;
    }

    string Normalize(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Trim().Replace(" ", "").Replace("　", "").ToLowerInvariant();
    }

    public void OnEndingLock()
    {
        endingLocked = true;

        // 如果当前开着，走你原本 Close()，把 readStay / clickSystem / cursor 都收干净
        if (isOpen) Close();

        // 彻底禁用脚本 Update（可选，但很稳）
        enabled = false;

        // 如果你结局需要鼠标可点“结局按钮”，这里建议解锁鼠标：
        if (firstPerson != null)
        {
            firstPerson.allowCursorToggle = false;
            firstPerson.SetCursorLock(false);
        }

        // 保险：关掉背包面板根
        if (root) root.SetActive(false);
    }

    public void OnEndingUnlock()
    {
        endingLocked = false;
        enabled = true;
    }

    // 计数
    public int GetCorrectGuessCount()
    {
        if (InventoryManager.Instance == null) return 0;

        int count = 0;
        var owned = InventoryManager.Instance.Owned;
        for (int i = 0; i < owned.Count; i++)
        {
            var rt = owned[i];
            if (rt == null || rt.data == null) continue;
            if (string.IsNullOrWhiteSpace(rt.guessAnswer)) continue;

            if (IsGuessCorrect(rt.data.id, rt.guessAnswer))
                count++;
        }
        return count;
    }
}
