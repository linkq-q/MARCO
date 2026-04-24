using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine.SceneManagement;

// AI 控制中心：
// ✅ 不再由“打开背包/拾取”等玩家行为触发 AI。
// ✅ 改为：每隔 N 秒自动说一句；玩家发送文本则立刻回复。
// ✅ 玩家输入时：暂停自动发言计时 + 屏蔽自动发言的弹窗（避免打断输入）
public class AIBroker : MonoBehaviour
{
    [Header("Refs (必须拖)")]
    public CloudResponder cloudResponder;
    public BottomChatPopup bottomChat;

    public static AIBroker Instance { get; private set; }

    [Header("Auto Talk")]
    public bool autoTalkEnabled = true;
    public float autoTalkInterval = 5f;
    public AITrigger autoTrigger = AITrigger.Idle;

    [Header("Debug")]
    public bool logDebug = false;

    [Header("Debug Stage Override")]
    public bool debugForceStage = false; // ✅ 默认关掉
    public EchoStage debugStage = EchoStage.Stage0_Background;
    public int debugSubState = 1;

    [Header("TTS")]
    public DoubaoTtsHttpClient ttsClient;   // 拖：场景里的 DoubaoTtsHttpClient

    bool _debugInjected = false;

    public CloudResponderJudge cloudJudge;

    [Header("Story Task")]
    public StoryTaskManager storyTaskManager; // 拖到场景里的StoryTaskManager

    [Header("Stage Switch By Task")]
    public int stage1TriggerTaskIndex = 4;
    public EchoStage stageWhenEnterTask4 = EchoStage.Stage1_Explore; // 你自己选实际枚举
    public int subStateWhenEnterTask4 = 1;

    [Header("Task 5 Preset Intro")]
    public int taskIndexForLink5Intro = 5;
    [TextArea(1, 3)]
    public string link5IntroLine = "你有没有试着把你的想法写下来？";
    public float link5IntroDelay = 0.2f;

    [Header("Stage2 Link Trigger")]
    public bool triggerStage2LinkByReplyKeywords = true;
    public string[] stage2LinkKeywords = new[]
    {
        "连线", "记忆", "帮你回忆", "串起来", "找到联系", "线索", "拼起来"
    };


    int _turnId = 0;

    bool endingLocked;
    bool _finalChoicePopupShown = false;

    Coroutine autoCo;

    bool busy;                 // 防止并发请求
    bool pendingUserRequest;   // 用户刚发消息时，优先处理
    bool _autoPaused;          // ✅ 输入时暂停
    bool _introShown;
    string _lastAILine;

    public EchoStage stage;
    public int subState;


    // ✅ 防止“输入期间，自动发言的网络请求返回了”仍然弹窗
    // 如果当前在输入态，自动发言结果先缓存，等输入结束再显示（可选）
    string _pendingAutoLine;

    string _pendingAutoEmotion = "neutral";
    bool _link5IntroQueuedOrPlayed;



    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnEnable()
    {
        if (storyTaskManager != null)
            storyTaskManager.OnTaskEntered += OnTaskEntered;

        StartAutoLoop();
    }

    void OnDisable()
    {
        if (storyTaskManager != null)
            storyTaskManager.OnTaskEntered -= OnTaskEntered;

        StopAutoLoop();
    }

    // 立即生效
    void OnTaskEntered(int taskIndex)
    {
        Debug.Log($"[AIBroker] OnTaskEntered taskIndex={taskIndex} need={stage1TriggerTaskIndex}");
        TryQueueTask5Intro(taskIndex);
        if (taskIndex != stage1TriggerTaskIndex) return;

        // ✅ 切阶段：写到 EchoRunState
        if (EchoRunState.I != null)
        {
            EchoRunState.I.ForceSet(stageWhenEnterTask4, subStateWhenEnterTask4, "TaskEntered:" + taskIndex);
        }
        else
        {
            // 兜底：如果你某些场景没有 RunState，就写到本地字段
            stage = stageWhenEnterTask4;
            subState = Mathf.Max(1, subStateWhenEnterTask4);
        }

        if (logDebug)
            Debug.Log($"[AIBroker] Task{taskIndex} entered -> Switch stage={stageWhenEnterTask4} sub={subStateWhenEnterTask4}");
    }


    //第一次调用ai
    public async void RequestFirstLine()
    {
        if (endingLocked) return;
        if (busy) return;
        if (cloudResponder == null || bottomChat == null)
        {
            Debug.LogError("[AIBroker] missing refs: cloudResponder/bottomChat");
            return;
        }

        busy = true;

        try
        {
            var ctx = new AIContext
            {
                trigger = AITrigger.EventNode,
                sceneName = SceneManager.GetActiveScene().name,
                playerAction = "（开局测试）",
                loreRefs = new List<LoreRef>(),

                // 指定阶段 + 子状态
                stage = EchoRunState.I ? EchoRunState.I.stage : stage,
                subState = EchoRunState.I ? EchoRunState.I.subState : subState
            };

            string line = await cloudResponder.GenerateAsync(ctx);

            if (!string.IsNullOrWhiteSpace(line))
            {
                _lastAILine = line;
                bottomChat.ShowAI(line);
                storyTaskManager?.RegisterAIDialogue(line);


                AISessionState.I?.SetAIReply(line);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("[AIBroker] First call failed: " + e);
        }
        finally
        {
            busy = false;
        }
    }

    void Start()
    {
        StartAutoLoop();
    }


    void StartAutoLoop()
    {
        if (!autoTalkEnabled) return;
        if (autoCo != null) StopCoroutine(autoCo);
        autoCo = StartCoroutine(AutoTalkLoop());
    }

    void StopAutoLoop()
    {
        if (autoCo != null)
        {
            StopCoroutine(autoCo);
            autoCo = null;
        }
    }

    IEnumerator AutoTalkLoop()
    {
        float interval = Mathf.Max(0.2f, autoTalkInterval);

        // ✅ 用“自己计时”而不是 WaitForSecondsRealtime：这样才能真正“暂停计时”
        float acc = 0f;

        while (true)
        {
            yield return null; // 每帧推进

            if (!autoTalkEnabled) { acc = 0f; continue; }
            if (_autoPaused) continue;                 // ✅ 输入时暂停（不累加 acc）
            if (busy) continue;
            if (pendingUserRequest) continue;

            acc += Time.unscaledDeltaTime;
            if (acc < interval) continue;

            acc = 0f;
            RequestAutoTalkOnce();
        }
    }

    //保存情绪
    public string CurrentEmotion { get; private set; } = "ASMR";
    public EchoBehavior CurrentBehavior { get; private set; } = EchoBehavior.Calm;

    void SetMood(EchoBehavior b, string emo)
    {
        CurrentBehavior = b;
        CurrentEmotion = string.IsNullOrEmpty(emo) ? "ASMR" : emo;
    }


    // ===== 自动发言 =====
    public void RequestAutoTalkOnce()
    {
        if (endingLocked) return;
        if (!cloudResponder || !bottomChat) return;

        var ctx = BuildBaseContext(autoTrigger, "自动发言");
        _ = RequestAndShow(ctx, isAutoTalk: true, turnId: 0);

    }

    // ===== 玩家发文本立刻回复 =====
    public void OnUserSend(string userText)
    {
        if (endingLocked) return;
        if (string.IsNullOrWhiteSpace(userText)) return;
        if (!cloudResponder || !bottomChat) return;

        // 1) 写入会话状态
        if (AISessionState.I != null)
            AISessionState.I.SetPlayerInput(userText);


        // 2) 计算 SAN 变化
        int delta = (AISessionState.I != null) ? SanRules.EvaluateDelta(userText) : 0;
        if (AISessionState.I != null)
            AISessionState.I.AddSan(delta);

        // 3) 组装注入块（每次请求都会带上）
        string injected = BuildInjectedBlock(userText);

        pendingUserRequest = true;

        int myTurn = ++_turnId;


        var ctx = BuildBaseContext(AITrigger.EventNode, injected);
        _ = RequestAndShow(ctx, isAutoTalk: false, turnId: myTurn);


    }

    string BuildInjectedBlock(string userText)
    {
        int san = AISessionState.I ? AISessionState.I.san : 85;
        string pinned = AISessionState.I ? AISessionState.I.BuildSavedBlock(6) : "（无）";
        string recent = AISessionState.I ? AISessionState.I.BuildRecentTurnsBlock() : "（无）";


        return
    $@"【UserState】：SAN={san}
{recent}
【PinnedNotes】：{pinned}
【UserInput】：{userText}";
    }


    AIContext BuildBaseContext(AITrigger trigger, string playerAction)
    {
        EnsureDebugInjectedOnce(); // 只在你手动开 debugForceStage 时生效一次

        // ✅ 每次请求前：提交 pending
        if (EchoRunState.I != null)
            EchoRunState.I.ApplyPendingIfAny("AIBroker.BuildBaseContext");

        var ctx = new AIContext
        {
            trigger = trigger,
            sceneName = SceneManager.GetActiveScene().name,
            playerAction = playerAction,
        };

        // ✅ 阶段来源：RunState 优先，否则 AIBroker 本地字段兜底
        EchoStage s;
        int sub;

        if (EchoRunState.I != null)
        {
            s = EchoRunState.I.stage;
            sub = EchoRunState.I.subState;
        }
        else
        {
            s = stage;
            sub = subState;
        }

        // ✅ debug 强制覆盖（仅用于你手动测试）
        if (debugForceStage)
        {
            s = debugStage;
            sub = debugSubState;
        }

        ctx.stage = s;
        ctx.subState = Mathf.Max(1, sub);

        if (InventoryManager.Instance != null)
            ctx.loreRefs = new List<LoreRef>(InventoryManager.Instance.GetUnlockedLoreRefs());
        else if (PlayerLoreState.Instance != null)
            ctx.loreRefs = new List<LoreRef>(PlayerLoreState.Instance.GetAllLore());
        else
            ctx.loreRefs = new List<LoreRef>();

        return ctx;
    }
    // 一次性注入调试阶段
    void EnsureDebugInjectedOnce()
    {
        if (!debugForceStage) return;
        if (_debugInjected) return;

        if (EchoRunState.I != null)
            EchoRunState.I.ForceSet(debugStage, debugSubState, "DebugInjectOnce");
        else
        {
            stage = debugStage;
            subState = Mathf.Max(1, debugSubState);
        }

        _debugInjected = true;
        // 注意：这里不要自动关 debugForceStage，
        // 因为 BuildBaseContext 里还会用 debugForceStage 做“覆盖 prompt”
    }

    void TryQueueTask5Intro(int taskIndex)
    {
        if (taskIndex != taskIndexForLink5Intro) return;
        if (_link5IntroQueuedOrPlayed) return;
        if (string.IsNullOrWhiteSpace(link5IntroLine)) return;

        _link5IntroQueuedOrPlayed = true;
        StartCoroutine(CoPresentPresetLineWhenReady(link5IntroLine, "neutral", link5IntroDelay));
    }

    IEnumerator CoPresentPresetLineWhenReady(string line, string emotion, float delaySeconds)
    {
        if (delaySeconds > 0f)
            yield return new WaitForSecondsRealtime(delaySeconds);

        while (busy && !endingLocked)
            yield return null;

        if (endingLocked) yield break;
        PresentPresetLine(line, emotion);
    }

    public void PresentPresetLine(string line, string emotion = "neutral")
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        if (bottomChat == null) return;

        _lastAILine = line;
        bottomChat.ShowAI(line);
        storyTaskManager?.RegisterAIDialogue(line);
        AISessionState.I?.SetAIReply(line);
        PlayTtsLine(line, emotion);
    }

    public async System.Threading.Tasks.Task<string> GenerateGuessFeedbackAsync(string itemName, string playerGuess)
    {
        if (cloudResponder == null) return null;
        return await cloudResponder.GenerateGuessFeedbackAsync(itemName, playerGuess);
    }

    public void PlayTtsLine(string originalText, string emotion = "neutral")
    {
        if (ttsClient == null) return;

        string ttsText = FilterTtsText(originalText);
        if (string.IsNullOrWhiteSpace(ttsText)) return;

        ttsClient.Speak(ttsText, emotion);
    }

    static string FilterTtsText(string originalText)
    {
        if (string.IsNullOrWhiteSpace(originalText)) return string.Empty;

        string ttsText = Regex.Replace(originalText, @"（[^）]*）", "").Trim();
        ttsText = Regex.Replace(ttsText, @"\([^)]*\)", "").Trim();
        return ttsText;
    }


    async System.Threading.Tasks.Task RequestAndShow(AIContext ctx, bool isAutoTalk, int turnId)
    {
        if (endingLocked) return;
        if (busy) return;

        //玩家发完文本开始显示思考状态
        bottomChat?.SetThinking(true);

        busy = true;

        try
        {
            if (logDebug)
                Debug.Log($"[AIBroker] Request start isAuto={isAutoTalk} paused={_autoPaused} trigger={ctx.trigger} action={ctx.playerAction}");

            string line = await cloudResponder.GenerateAsync(ctx);
            if (string.IsNullOrWhiteSpace(line)) return;

            // ✅ 从 CloudResponder 取出“本次请求对应的情绪”
            string emo = (!string.IsNullOrEmpty(cloudResponder.LastEmotion)) ? cloudResponder.LastEmotion : "neutral";

            // ✅ 玩家在输入态时，禁止自动发言打断：缓存文本 + 情绪
            if (isAutoTalk && _autoPaused)
            {
                _pendingAutoLine = line;
                _pendingAutoEmotion = emo;
                if (logDebug) Debug.Log("[AIBroker] AutoTalk result buffered (typing).");
                return;
            }

            _lastAILine = line;
            bottomChat.ShowAI(line);
            storyTaskManager?.RegisterAIDialogue(line);

            Debug.Log($"[JudgeDBG] isAuto={isAutoTalk} cloudJudge={(cloudJudge ? "OK" : "NULL")} runState={(EchoRunState.I ? "OK" : "NULL")}");

            // 只对玩家输入（isAutoTalk=false）做判定
            if (!isAutoTalk && cloudJudge != null && EchoRunState.I != null)
            {
                var rs = EchoRunState.I;
                EchoStage curStage = rs.stage;
                int curSub = rs.subState;

                string injected = ctx.playerAction;

                Debug.Log($"[Judge] kickoff turn={turnId} stage={curStage} sub={curSub}");
                _ = RunJudgeBackground(turnId, curStage, curSub, injected);
            }



            if (AISessionState.I != null)
                AISessionState.I.SetAIReply(line);

            // ✅ 统一由 AIBroker 播 TTS（避免 CloudResponder 里也播导致重复）
            PlayTtsLine(line, emo);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[AIBroker] Request failed: " + e.Message);
        }
        finally
        {
            busy = false;
            pendingUserRequest = false;
        }
    }

    // 判定
    async System.Threading.Tasks.Task RunJudgeBackground(int turnId, EchoStage stage, int subState, string injected)
    {
        try
        {
            string json = await cloudJudge.JudgeAsync(stage, subState, injected);
            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.LogWarning("[Judge] empty json");
                return;
            }

            // 过期丢弃
            if (turnId != _turnId)
            {
                Debug.LogWarning($"[Judge] drop expired turn. got={turnId} cur={_turnId}");
                return;
            }

            DecisionResult d;
            try { d = JsonUtility.FromJson<DecisionResult>(json); }
            catch
            {
                Debug.LogWarning("[Judge] JSON parse failed: " + json);
                return;
            }

            // ✅ 解析日志（无论 logDebug 开没开都建议输出一条，方便你定位）
            Debug.Log($"[JudgeParsed] turn={turnId} stage={stage} sub={subState} adv={d.advance} tag={d.tag} sanDelta={d.sanDelta} note={d.note}");

            var rs = EchoRunState.I;

            // ✅ 用“参数 stage/subState”判断主题池，避免 rs.stage/subState 被 pending/debug 覆盖
            bool useTopicPool = (rs != null
                                 && rs.stage3TopicPool != null
                                 && stage == EchoStage.Stage3_Dwarf
                                 && subState == 3);

            // ===== Stage3-S7: 判定出明确选择后 -> 弹确认框（不推进） =====
            bool handledS7 = false;

            if (stage == EchoStage.Stage3_Dwarf && subState == 7)
            {
                bool decided = (d.advance == 1) &&
                               !string.IsNullOrEmpty(d.tag) &&
                               (d.tag.Equals("accept", StringComparison.OrdinalIgnoreCase) ||
                                d.tag.Equals("reject", StringComparison.OrdinalIgnoreCase));

                Debug.Log($"[S7Check] decided={decided} adv={d.advance} tag={d.tag} popupShown={_finalChoicePopupShown}");

                if (decided)
                {
                    handledS7 = true;

                    if (!_finalChoicePopupShown)
                    {
                        _finalChoicePopupShown = true;

                        Debug.Log("[S7] decided -> try call EndingManager.RequestFinalConfirmFromJudge");
                        Debug.Log("[S7] EndingManager.I=" + (EndingManager.I ? "OK" : "NULL"));

                        if (EndingManager.I != null)
                        {
                            // 顺手输出 popup 引用是否绑上（不影响编译）
                            Debug.Log("[S7] EndingManager.choicePopup=" + (EndingManager.I.choicePopup ? "OK" : "NULL"));
                            EndingManager.I.RequestFinalConfirmFromJudge(d.tag);
                            Debug.Log("[S7] RequestFinalConfirmFromJudge CALLED");
                        }
                        else
                        {
                            Debug.LogError("[S7] EndingManager.I is NULL -> cannot show popup.");
                        }
                    }
                    else
                    {
                        Debug.Log("[S7] popup already shown -> skip");
                    }
                }
            }

            // ===== 推进逻辑：S7 decided 时不推进 =====
            if (!handledS7)
            {
                if (useTopicPool)
                {
                    rs.EnsureStage3RuntimeInited();

                    // 玩家没有追问细节：扣“最低轮数”
                    if (!d.detailFollow)
                    {
                        rs.stage3Runtime.remainingTurns--;
                        if (rs.stage3Runtime.remainingTurns <= 0)
                            rs.stage3Runtime.PickNext(rs.stage3TopicPool);
                    }

                    // 覆盖满 N 个主题才允许推进
                    if (rs.stage3Runtime.coveredCount >= rs.stage3NeedCoverCount)
                        ApplyAdvanceToPending(stage, subState, d.advance);
                    else
                        ApplyAdvanceToPending(stage, subState, 0); // 强制不推进
                }
                else
                {
                    ApplyAdvanceToPending(stage, subState, d.advance);
                }
            }
            else
            {
                Debug.Log("[S7] handledS7=true -> skip ApplyAdvanceToPending");
            }

            // ===== SAN / takeover 等仍然执行（避免 S7 return 导致状态不更新） =====
            var sanSys = FindFirstObjectByType<SanSystem>();
            if (sanSys != null)
            {
                sanSys.ApplyDecisionResult(d);

                bool validHyp =
                    (!string.IsNullOrEmpty(d.tag) &&
                        (d.tag.Equals("accept", StringComparison.OrdinalIgnoreCase) ||
                         d.tag.Equals("valid_hypothesis", StringComparison.OrdinalIgnoreCase) ||
                         d.tag.Equals("hypothesis", StringComparison.OrdinalIgnoreCase))) ||
                    (!string.IsNullOrEmpty(d.note) &&
                        (d.note.Contains("成立") || d.note.Contains("有效")));

                if (validHyp)
                {
                    sanSys.ApplyPlayerAction(SanSystem.PlayerActionType.SubmitValidHypothesis);
                    ToastSpawner.Instance?.Show("推测成立：San +2");
                }
                else
                {
                    if (!string.IsNullOrEmpty(d.tag) &&
                        d.tag.Equals("reject", StringComparison.OrdinalIgnoreCase))
                    {
                        sanSys.takeover?.OnInferenceFail();
                        sanSys.takeover?.NotifyProgress();
                    }
                }
            }
            else
            {
                Debug.LogWarning("[Judge] SanSystem not found in scene.");
            }

            if (logDebug)
                Debug.Log($"[Judge] turn={turnId} adv={d.advance} tag={d.tag} san={d.sanDelta} note={d.note}");
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Judge] failed: " + e);
        }
    }


    public string GetLastAI() => _lastAILine;

    // ✅ BottomChatPopup 在输入开始/结束时调用这个
    public void SetAutoTalkPaused(bool paused)
    {
        _autoPaused = paused;

        if (logDebug)
            Debug.Log("[AIBroker] AutoTalkPaused=" + paused);

        // ✅ 输入结束：如果刚才缓存过自动发言，就在这时显示（不打断输入）
        if (!paused && !string.IsNullOrWhiteSpace(_pendingAutoLine))
        {
            string line = _pendingAutoLine;
            _pendingAutoLine = null;

            bottomChat?.ShowAI(line);
            storyTaskManager?.RegisterAIDialogue(line);
            AISessionState.I?.SetAIReply(line);
            PlayTtsLine(line, _pendingAutoEmotion);
            _pendingAutoEmotion = "neutral";

            if (logDebug) Debug.Log("[AIBroker] Buffered AutoTalk shown after typing.");
        }
    }

    //暂时保留
    static class SanRules
    {
        public static int EvaluateDelta(string playerText)
        {
            if (string.IsNullOrWhiteSpace(playerText)) return 0;
            string s = playerText.Trim();

            if (s.Length <= 2) return -1;

            if (ContainsAny(s, "不对", "异常", "循环", "假的", "醒来", "梦", "现实", "崩坏"))
                return +2;

            if (ContainsAny(s, "操", "烦死", "滚", "受不了", "何意味", "妈的"))
                return -3;

            return 0;
        }

        static bool ContainsAny(string s, params string[] keys)
        {
            foreach (var k in keys)
                if (s.Contains(k)) return true;
            return false;
        }
    }

    void ApplyAdvanceToPending(EchoStage curStage, int curSub, int advance)
    {
        if (EchoRunState.I == null) return;
        advance = Mathf.Clamp(advance, -1, 1);

        // ✅ Stage2 用专用状态机
        if (curStage == EchoStage.Stage2_Rift)
        {
            ApplyAdvanceStage2(curSub, advance);
            return;
        }

        // 其他阶段：原逻辑
        int nextSub = Mathf.Max(1, curSub + advance);
        EchoRunState.I.SetPending(curStage, nextSub, "JudgeAdvance");
    }

    // stage2循环组
    void ApplyAdvanceStage2(int curSub, int advance)
    {
        var rs = EchoRunState.I;
        if (rs == null) return;

        curSub = Mathf.Clamp(curSub, 1, 4);
        var rt = rs.stage2Runtime ?? (rs.stage2Runtime = new Stage2RealityRuntime());

        // ===== 当前在 S4：玩家回应情绪 -> 回到 resumeSub，并清零计数 =====
        if (curSub == 4)
        {
            int back = Mathf.Clamp(rt.resumeSub, 1, 3);
            rs.SetPending(EchoStage.Stage2_Rift, back, "Stage2:S4->Resume");
            return;
        }

        // ===== 当前在 S1/S2/S3：advance=1 才推进，否则停留在当前状态 =====
        int nextBase = curSub;
        if (advance > 0)
        {
            nextBase = (curSub % 3) + 1;

            if (curSub == 3)
                rt.StepKeyword();
        }

        rt.RegisterBaseDialogueTurn();

        if (rt.ShouldInsertS4())
        {
            rt.PickEmotion();
            rt.resumeSub = Mathf.Clamp(nextBase, 1, 3);
            rt.ResetS4Counter();
            rs.SetPending(EchoStage.Stage2_Rift, 4, "Stage2:InsertS4");
            return;
        }

        rs.SetPending(EchoStage.Stage2_Rift, nextBase, advance > 0 ? "Stage2:Advance" : "Stage2:Hold");
    }

    public void OnEndingLock()
    {
        endingLocked = true;

        // 停自动发言
        autoTalkEnabled = false;
        _autoPaused = true;

        StopAutoLoop();

        // 禁止 pending 队列影响结局
        pendingUserRequest = false;
        _pendingAutoLine = null;

        // 关闭思考UI（避免结局卡“thinking”）
        bottomChat?.SetThinking(false);

        // 取消订阅任务事件（避免结局仍切阶段）
        if (storyTaskManager != null)
            storyTaskManager.OnTaskEntered -= OnTaskEntered;
    }

    public void OnEndingUnlock()
    {
        endingLocked = false;
        _autoPaused = false;

        // 如果你需要恢复自动发言：
        // autoTalkEnabled = true; StartAutoLoop();
    }
    public void ResetFinalChoicePopupGate()
    {
        _finalChoicePopupShown = false;
    }
}
