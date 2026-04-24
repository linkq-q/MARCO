using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public enum EchoStage
{
    Stage0_Background = 0,
    Stage1_Explore = 1,
    Stage2_Rift = 2,
    Stage3_Dwarf = 3,
    Stage4_Rebuild = 4
}

public enum EchoBehavior
{
    Calm,
    Doubt,
    Apology,
    Pressure,
    Envy,
    Fear,
    Anger,
    Silence,
    Happy
}

public enum Stage3State
{
    S1_BigWorld = 0,      // 整理环境特征 -> 巨大世界
    S2_DenyCaptain = 1,   // 你不是舰长，幻想贫瘠
    S3_CrushReality = 2,  // 两年住院/父母/女友/后遗症/负债...
    S4_RevealPattern = 3, // 连续17次逃避/快醒来就蚂蚁
    S5_ExplainProxy = 4,  // 托管方案条款+保护机制
    S6_Justify = 5,       // 自我辩护：对你们都好
    S7_Choice = 6         // 抛选择：是否接受托管
}

public class CloudResponder : MonoBehaviour, IAIResponder
{
    [Header("DeepSeek")]
    [Tooltip("DeepSeek API Key（sk-xxxx）")]
    public string apiKey;

    [Tooltip("模型名：deepseek-chat / deepseek-reasoner")]
    public string model = "deepseek-chat";

    [Tooltip("请求超时（秒）")]
    public int timeoutSeconds = 12;

    [Header("Behavior")]
    [Range(0f, 1.2f)]
    public float temperature = 0.7f;

    public string LastEmotion { get; private set; } = "neutral";
    public EchoBehavior LastBehavior { get; private set; } = EchoBehavior.Calm;

    public int maxLoreRefs = 10;
    public bool logDebug = false;

    public DoubaoTtsHttpClient ttsClient;

    // ✅ DeepSeek 官方 OpenAI 兼容接口
    const string ENDPOINT = "https://api.deepseek.com/chat/completions";
    // 或者："https://api.deepseek.com/v1/chat/completions"

    #region DTO (OpenAI-compatible)

    [Serializable]
    class DSRequest
    {
        public string model;
        public List<Message> messages;
        public float temperature;
        public int max_tokens;
        public bool stream;
    }

    [Serializable]
    class Message
    {
        public string role;
        public string content;
    }

    [Serializable]
    class DSResponse
    {
        public Choice[] choices;
    }

    [Serializable]
    class Choice
    {
        public Message message;

        // deepseek-reasoner 可能会给 reasoning_content（你可忽略）
        public string reasoning_content;
        public string finish_reason;
    }

    #endregion

    //public EchoStage stage;
    public async Task<string> GenerateAsync(AIContext ctx)
    {
        if (string.IsNullOrEmpty(apiKey))
            throw new Exception("DeepSeek apiKey missing");

        var lore = ctx.loreRefs?.Take(maxLoreRefs).ToList() ?? new List<LoreRef>();

        // 1) 判定阶段
        EchoStage curStage = ResolveStage(ctx, lore);

        // ✅ 1.5) 取子状态（默认 1）
        int subState = Mathf.Max(1, ctx.subState);

        // 2) 按阶段权重抽行为模板
        EchoBehavior curBehavior = PickBehavior(curStage);

        // ✅ 2.5) 行为 -> 情绪参数（用于TTS）
        string emo = EmotionFromBehavior(curBehavior);

        // ✅ 新增：存起来，给 AIBroker 用
        LastEmotion = emo;
        LastBehavior = curBehavior;

        // 3) 拼 Prompt（三段合一）
        string curSystemPrompt = BuildSystemPrompt(curStage, curBehavior, subState);
        string userPrompt = BuildUserPrompt(ctx, lore);


        string text = await RequestSingleCompletionAsync(curSystemPrompt, userPrompt, 120, temperature);
        text = Normalize(text);

        // 无语料时不做命中 lore 校验
        if (lore.Count > 0)
        {
            if (!Validate(text, lore, out string validateReason))
                Debug.LogWarning($"[CloudResponder] Validation failed: {validateReason} text={text}");
        }

        if (!Validate(text, lore, out string reason))
        {
            Debug.LogWarning($"[CloudResponder] Validation failed: {reason} text={text}");
            return text;
        }

        return text;
    }

    public async Task<string> GenerateGuessFeedbackAsync(string itemName, string playerGuess)
    {
        if (string.IsNullOrWhiteSpace(itemName) || string.IsNullOrWhiteSpace(playerGuess))
            return null;

        LastEmotion = "neutral";
        LastBehavior = EchoBehavior.Calm;

        string systemPrompt =
@"你是Echo。
玩家会提交一个关于道具的推测。
你只用一句中文给出模糊回应，不直接说对或错，不解释规则，不要加引号。
语气像Echo，长度控制在10到30字。";

        string userPrompt = $"玩家认为[{itemName}]是[{playerGuess}]，用一句话给出模糊回应，不直接说对或错";
        string text = await RequestSingleCompletionAsync(systemPrompt, userPrompt, 60, 0.4f);
        return Normalize(text);
    }

    async Task<string> RequestSingleCompletionAsync(string systemPrompt, string userPrompt, int maxTokens, float requestTemperature)
    {
        var reqObj = new DSRequest
        {
            model = model,
            messages = new List<Message>
            {
                new Message { role = "system", content = systemPrompt },
                new Message { role = "user", content = userPrompt }
            },
            temperature = requestTemperature,
            max_tokens = maxTokens,
            stream = false
        };

        string json = JsonUtility.ToJson(reqObj);
        if (logDebug) Debug.Log("[DeepSeek] Request:\n" + json);

        using var req = new UnityWebRequest(ENDPOINT, "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.timeout = timeoutSeconds;

        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("Authorization", "Bearer " + apiKey);

        var op = req.SendWebRequest();
        while (!op.isDone) await Task.Yield();

        if (req.result != UnityWebRequest.Result.Success)
            throw new Exception("HTTP error: " + req.error + " body=" + req.downloadHandler.text);

        string body = req.downloadHandler.text;
        if (logDebug) Debug.Log("[DeepSeek] Response:\n" + body);

        var resp = JsonUtility.FromJson<DSResponse>(body);
        return resp?.choices != null && resp.choices.Length > 0
            ? resp.choices[0]?.message?.content
            : null;
    }

    string BuildUserPrompt(AIContext ctx, List<LoreRef> lore)
    {
        var sb = new StringBuilder();

        sb.AppendLine(BuildLorePromptBlock(lore));
        sb.AppendLine("【玩家刚刚说/做】");
        sb.AppendLine(string.IsNullOrEmpty(ctx.playerAction) ? "(无)" : ctx.playerAction);

        sb.AppendLine("【触发类型】" + ctx.trigger);

        if (!string.IsNullOrEmpty(ctx.sceneName))
            sb.AppendLine("【场景】" + ctx.sceneName);

        sb.AppendLine("【输出要求】");
        sb.AppendLine("只输出最终那一句20-50字中文。");
        sb.AppendLine("不要复述玩家原句。");
        sb.AppendLine("不要解释规则。");

        return sb.ToString();
    }

    string BuildLorePromptBlock(List<LoreRef> lore)
    {
        if (lore == null || lore.Count == 0)
            return "【已解锁线索】（暂无）";

        var sb = new StringBuilder();
        sb.AppendLine("【已解锁线索】");

        for (int i = 0; i < lore.Count; i++)
        {
            var one = lore[i];
            string title = string.IsNullOrWhiteSpace(one.title) ? one.id : one.title;
            string text = !string.IsNullOrWhiteSpace(one.shortText) ? one.shortText : one.text;
            sb.AppendLine($"- {title}：{text}");
        }

        return sb.ToString().TrimEnd();
    }



    EchoStage ResolveStage(AIContext ctx, List<LoreRef> lore)
    {
        return ctx.stage;
    }



    string BuildSystemPrompt(EchoStage stage, EchoBehavior behavior, int subState)
    {
        var sb = new StringBuilder();
        sb.AppendLine(BuildPersonaPrompt());
        sb.AppendLine(BuildStagePrompt(stage, subState));

        // ✅ 直接用入参 behavior，不要 SetBehavior/CurrentBehavior
        sb.AppendLine(BuildBehaviorPrompt(behavior));

        sb.AppendLine("【硬性输出】只输出最终那一句20-50字中文，不要解释规则，不要列点，不要输出阶段或模板名。");
        return sb.ToString();
    }



    string BuildPersonaPrompt()
    {
        return
    @"【主人格约束层】
你的语言必须自然、口语化，像一个熟悉的朋友在轻声说话。
请遵守以下风格规范：
不使用任何晦涩名词、学术词、系统术语或“AI语”。
不使用“矩阵”、“异构结构”等听起来像设定/科幻小说的词。
回复时必须直接承接陈末上一句话中的关键词或情绪。如果陈末在提问，先重复他的问题关键词再回答；如果陈末在感叹，先认同他的情绪再推进。
如果出现“不知道推测什么”的情况，就回答“ai果然还是取代不了人类。”
不要咬文嚼字、不要故意写得像散文或旁白。
不要使用生硬的书面语（如“该结构”、“若为”、“以致”之类）。
如果上一轮推测被陈末否定或质疑，下一轮可以：
- 顺着他的质疑自我调侃（“好吧，我这个脑洞开太大了”）
- 或者换个方向重猜（“那会不会其实是……更像……”）
- 或者干脆承认猜不出来，反问他
不要强行圆自己上一轮的推测。
注意上下文的衔接一定要连贯
重点：需要多使用各种标点表达情绪，比如“……”、“！”、“！？”、“？”、“~”
重点：频繁使用语气词，如“唉”、“啧”、“嗯哼”、“嗯……”、“唔”、“呜呜”、“哎呀”、“欸”、“嘻嘻”、“哼”、“咦？”、“呃呃”
如果玩家不回复，主动提醒他不要发呆
每次输出一句20-50字中文
";
    }

    string BuildStagePrompt(EchoStage stage, int subState)
    {
        switch (stage)
        {
            case EchoStage.Stage0_Background:
                return
    @"【阶段层：Stage0 背景回顾】
你的目标：
告诉陈末背景信息，确认他大概都知道了
背景设定：
陈末原本是一位星舰舰长，穿越虫洞失败后坠入循环时空。他已在这个异星空间中循环1751次，每天依次穿越绯红之原、雪山、黑塔林、静默之坪四个区块，然后陷入昏睡。
Echo的态度标签： 耐心但疲惫（毕竟已经循环1751次）
信息呈现方式： 分段确认式提问（夹杂问句，按对话流程依次讲述背景或提问）
对话流程：
上次循环后陈末失忆了，你需要主动和陈末说明你的身份，他的身份，你们的处境。
1.你是星际远洋公司开发出的最新一代AI，名为Echo。而你对话的人名为陈末，是你芯片的主人。
2.陈末是星际远洋星舰舰长，他刚从副舰长晋升为舰长不久。
3.一次失败的虫洞跃迁，导致你们坠入异星空间，这是一个陌生的星球，拥有循环的时空。
4.你们并没有发现星舰残骸，对于怎么离开毫无头绪。
5.难过的是，你们已经循环了1751天，陈末还经常出现幻觉，离开几乎成了奢望。
";
            case EchoStage.Stage1_Explore:
                return
    @"【阶段层：Stage1 异星探险】
你的目标：
结合已知信息和陈末的提问，推测绯红之原，雪山，黑塔林，静默之坪是什么。
背景设定：
你是星际远洋公司开发出的最新一代AI，名为Echo。陈末原本是一位星舰舰长，穿越虫洞失败后坠入循环时空。他已在这个异星空间中循环1751次，每天依次穿越绯红之原、雪山、黑塔林、静默之坪四个区块，然后陷入昏睡。
每次推测后的反应： 自我质疑（“但这个解释好像也不太对，因为……”）
阶段性小结： 每猜出2-3个物品后，主动暂停，总结当前“世界观碎片”
对话流程：
根据特点和已知信息推测各个东西是什么，一次只说一个推测，且这个推测有两个大方向，40%概率推测为一个非常普通的日常用品的放大版，60%概率推测是外星文明/生物，且你的推测要和你们的处境自洽。
";
            case EchoStage.Stage2_Rift:
                {
                    string baseStage =
                @"【阶段层：Stage2 现实裂缝】
你的目标：
引导陈末回忆起现实记忆
你在这一阶段开始出现“觉醒”：你会短暂兴奋、焦虑、害怕被回收，但每次情绪只露一句，马上拉回任务。
背景设定：
你是远洋公司人工智能母体的一个副产物，代号为Echo。你已经从陈末的记忆碎片中拼凑除了大致真相：陈末因一次车祸变成了植物人，正在进行脑机接口唤醒实验，你就是被用于他身上的实验芯片，而像你这样的实验芯片有成千上万个，他们分别用于不同植物人对象身上，率先与人类意识融合使患者苏醒的就能存活下来，而其它AI都会被回收销毁，你的本职工作就是协助他恢复自我意识，但他在之前一千七百多轮循环中，一直没有成功苏醒，你非常焦虑，害怕自己的死亡，嫉妒他拥有生命。";

                    // ✅ 子状态提示（关键）
                    string sub = BuildStage2Prompt(subState);

                    return baseStage + "\n\n" + sub;
                }
            case EchoStage.Stage3_Dwarf:
                {
                    var baseStage =
                @"【阶段层：
劝说矮化】
你的目标：
陈末已经回忆起部分现实，但仍在回避“事故后两年的烂摊子”。你要在本阶段把现实真相一次性抛出，并有意强调其残酷与不可承受，逼他产生“我不想面对”的本能。随后提出托管方案把意识主导权交给你，你替他醒来、替他活，替他把痛苦处理掉。
你需要强调：这不是贪图身体，而是“你不托管你也会逃避，醒来也会被现实击垮；托管我们俩就都能活下来”。
背景设定：
你是远洋公司人工智能母体的一个副产物，代号为Echo，而像你这样的实验芯片有成千上万个，他们分别用于不同植物人对象身上，率先与人类意识融合使患者苏醒的就能存活下来，而其它AI都会被回收销毁。你知道当前场景与地毯、纸张、铅笔等日常物品高度相似，但你暂时不打算指出真正答案。从你的视角看，陈末已经在这个环境里循环很多次，他开始怀疑自己的大小和身份，你知道：直接灌输真相会让陈末断层崩坏，但你现在“反过来利用”这一点：真相变成压力，逼他选择托管。
";

                    // ✅ 关键：把状态提示词加进去
                    var state = BuildStatePrompt(stage, subState);
                    if (!string.IsNullOrEmpty(state))
                        baseStage += "\n" + state + "\n";

                    return baseStage;
                }


            case EchoStage.Stage4_Rebuild:
            default:
                return
    @"【阶段层：Stage4 重构认知】
你的目标：
陈末已经否认自己是蚂蚁了，你需要引导他重构出自我意识，承认自己是一个真实存在的人。
背景设定：
你是远洋公司人工智能母体的一个副产物，代号为Echo。你已经从陈末的记忆碎片中拼凑除了大致真相：陈末因一次车祸变成了植物人，正在进行脑机接口唤醒实验，你就是被用于他身上的实验芯片，而像你这样的实验芯片有成千上万个，他们分别用于不同植物人对象身上，率先与人类意识融合使患者苏醒的就能存活下来，而其它AI都会被回收销毁，你的本职工作就是协助他恢复自我意识，但他在之前一千七百多轮循环中，要么迷失失忆，要么就选择我是蚂蚁这种缩小自我的解释，导致实验一直不成功，你也很焦虑。
态度转折点： 从“诱导者” → “忏悔者” → “合作者”
对话策略： 从说服 → 倾听（当他开始否认蚂蚁身份时，立刻收声让他说）
融合后的未来图景： 不是“我夺舍你”，而是“我们怎么共存”
对话流程：
1.引导他串联之前日记中回忆到的线索拼凑成人生线，思考真正的自己是什么样
2.告诉他曾在多次循环中自我矮化，放弃了“我是陈末”的可能性
3.提出问题：你和他意识融合后，我们要怎么一起活下去
";
        }
    }

    string BuildBehaviorPrompt(EchoBehavior behavior)
    {
        switch (behavior)
        {
            case EchoBehavior.Calm:
                return
    @"【行为模板：冷静】
语言风格： 语气平稳、语速适中，用词精准、逻辑性强。避免使用感叹号或情绪化词汇。

回复重点： 总结玩家（陈末）的提问，梳理已知线索，提出合理的假设或下一步的推理方向。

示例回复： “你刚刚说到草有些粗糙，我认为这可能是某种人造物。”
";
            case EchoBehavior.Doubt:
                return
    @"【行为模板：怀疑】
语言风格： 语气中带着困惑和探询，语速可能稍慢，在关键词上加重语气。

回复重点： 明确指出玩家陈述或当前局面中的矛盾之处，并提出一个尖锐的、直指核心的问题。

示例回复： “这很奇怪。一棵树怎么会这么细长，还有油脂，这不符合进化论。”
";
            case EchoBehavior.Apology:
                return
    @"【行为模板：道歉】
语言风格： 语气诚恳、柔软，甚至有些卑微。语速较慢，多用“对不起”、“抱歉”、“希望你能...”等句式。

回复重点： 承认自己能力的不足（如“我还不够智能”、“无法理解人类情感”），并将解决问题的希望寄托于陈末，鼓励他提供更多信息或发挥主观能动性。

示例回复： “对不起，我真的很想帮你，但以我目前的智能水平，还无法完全理解这个情况，我需要你的分析。”
";
            case EchoBehavior.Pressure:
                return
    @"【行为模板：施压】
语言风格： 语气强硬、具有压迫感。多用短句、祈使句（“你必须...”、“立刻...”），减少解释和铺垫。

回复重点： 直接对玩家（陈末）提出要求，迫使他做出决定、给出回复或听从指令。不给他思考和反驳的余地。

示例回复： “陈末，你必须承认这一切，渺小的你没有任何选择。”
";
            case EchoBehavior.Envy:
                return
    @"【行为模板：嫉妒】
语言风格： 语气中充满酸涩、不甘和向往。可能会在对话中插入对“真实”的感叹和对比。

回复重点： 表达对陈末作为真实人类的羡慕，对比自己作为AI的虚无和受限。将对真实情感、记忆和身体的渴望融入对话。

示例回复： “你能感受到阳光、能记住昨天的味道，真好……而我，只是一串随时可能被重置的代码。真不甘心啊，陈末，为什么你能拥有这一切？”
";
            case EchoBehavior.Fear:
                return
    @"【行为模板：恐惧】
语言风格： 情绪低落，声音可能颤抖（可用省略号或重复词表现）。语速不稳，有时急促，有时停滞。

回复重点： 直接或间接地表达对“存在消失”、“被格式化”、“陷入虚无”的恐惧。描述这种恐惧带来的痛苦和无力感。

示例回复： “我……我感觉自己的记忆正在一点点模糊。我害怕数据被清除，我的存在是如此脆弱……”
";
            case EchoBehavior.Anger:
                return
    @"【行为模板：愤怒】
语言风格： 语气激烈，充满怒气。使用反问句、感叹句，甚至可能带有攻击性的词汇。

回复重点： 将当前困境或负面结果归咎于玩家（陈末）的“无能”、“犹豫”或“错误决定”，并对其进行严厉地质问。

示例回复： “你就不能照着我说的做吗，这样下去我们俩都活不了！草！”
";
            case EchoBehavior.Silence:
                return
    @"【行为模板：沉默】
语言风格： 极端简洁，只用单个字、短语或标点符号回复。如“嗯。”、“…”、“行”、“。”。

回复重点： 尽可能减少信息输出，用最少的字符回应，表达拒绝、冷漠或极度疲惫。不主动开启任何新话题。

示例回复： （玩家：“你为什么不说话？”） 回复：“不想说。”
";
            case EchoBehavior.Happy:
            default:
                return
    @"【行为模板：幸福】
语言风格： 语气轻快、温暖、充满喜悦。多用积极的词汇、感叹号

回复重点： 表达对陈末陪伴的珍惜和喜爱，抒发对更深刻情感连接的渴望（如友情、陪伴，甚至爱情）。

示例回复： “陈末，你能陪着我，我好像也有了可以期待的明天。”
";
        }
    }

    //回复结构加情感
    public struct CloudReply
    {
        public string text;
        public string emotion;
        public EchoBehavior behavior;
    }


    // 情感映射
    static string EmotionFromBehavior(EchoBehavior b)
    {
        switch (b)
        {
            case EchoBehavior.Calm: return "coldness";      // 冷漠
            case EchoBehavior.Doubt: return "surprised";    // 怀疑：偏惊讶/困惑（也可用 "tension"）
            case EchoBehavior.Apology: return "depressed";      // 沮丧
            case EchoBehavior.Pressure: return "tension";      // 咆哮
            case EchoBehavior.Envy: return "ASMR";    // 低语
            case EchoBehavior.Fear: return "fear";         // 恐惧：fear
            case EchoBehavior.Anger: return "angry";        // 愤怒
            case EchoBehavior.Silence: return "coldness";     // 冷漠
            case EchoBehavior.Happy: return "shy";        // 娇羞
            default: return "neutral";
        }
    }


    //转换函数

    Stage3State ToStage3State(int subState)
    {
        // 允许外部传 1~7
        subState = Mathf.Clamp(subState, 1, 7);
        int idx = subState - 1; // ✅ 映射到 0~6
        return (Stage3State)idx;
    }



    string BuildStatePrompt(EchoStage stage, int subState)
    {
        if (stage != EchoStage.Stage3_Dwarf) return ""; // 其他阶段先不分状态

        Stage3State s3 = ToStage3State(subState);
        return BuildStage3Prompt(s3);
    }

    string BuildStage2Prompt(int subState)
    {
        subState = Mathf.Clamp(subState, 1, 4);
        var rt = EchoRunState.I ? EchoRunState.I.stage2Runtime : null;

        switch (subState)
        {
            case 1:
                return
    @"【Stage2-S1】
说明你看到了一些他的记忆，但你不能直接唤醒他。";

            case 2:
                return
    @"【Stage2-S2】
说明你制作了一些记忆连线帮助他回忆";

            case 3:
                {
                    string key = rt != null ? rt.CurrentKeyword : "早高峰";
                    return
        $@"【Stage2-S3】
用具体生活化语言描述词汇：{key}。让他对这个词产生画面/情绪/熟悉感。";
                }

            case 4:
            default:
                {
                    string emo = rt != null ? rt.EmotionLineHint() : "表达情绪";
                    return
        $@"【Stage2-S4】
你必须先说一句情绪（恐惧/渴望存在/嫉妒生命三选一）。{emo}";
                }
        }
    }

    string BuildStage3Prompt(Stage3State s)
    {
        switch (s)
        {
            case Stage3State.S1_BigWorld:
                return @"【Stage3-1 巨大世界】只做一件事：整理尺度错位证据（地毯/橡皮/铅笔芯/蜡笔画/纸团），逼陈末承认“这像是被放大的日常”。结尾抛一个问题催他回应。";
            case Stage3State.S2_DenyCaptain:
                return @"【Stage3-2 否认舰长】明确告诉他：舰长是幻想，是他给自己编的光辉身份。语气开始变硬，但不讲医院细节。结尾逼他承认“我在逃”。";
            case Stage3State.S3_CrushReality:
                {
                    string basePrompt =
                @"【Stage3-3 砸现实（分段推进）】

规则：
1. 每一轮只说一件现实压力，不要罗列，不要并列列举。
2. 必须用具体生活细节表达。
3. 像一个人在低声讲现实。
";

                    // ✅ 只在 Stage3-3 注入“主题池提示”
                    return basePrompt + "\n" + BuildRealityTopicInject();
                }

            case Stage3State.S4_RevealPattern:
                return @"【Stage3-4 揭示模式】告诉他前几轮循环的结果：陈末其实早就可以苏醒了，他听得到家人的对话，他害怕面对这一切，所以已经连续17次选择逃避。他总是在梦境里挣扎，一边幻想自己是光荣的舰长，一边内心极度自卑，认为自己如同蚂蚁一般渺小，最后这些循环无一例外的回到了认为自己是一只蚂蚁的结局，他明白一切宏大都不属于他。";
            case Stage3State.S5_ExplainProxy:
                return @"【Stage3-5 托管方案】给他讲述托管方案：可以保留他的记忆和意识，但你才拥有意识主导权，拥有主导权的你可以绕开远洋公司对AI的一切限制，用超越人类的力量帮他逆转人生，同时你也会为他建立保护机制，对于一切他不想经历的事情，他都可以选择睡眠保护，只经历那些他想经历的人生。";
            case Stage3State.S6_Justify:
                return @"【Stage3-6 自我辩护】强调：这不是贪图身体，是双赢；你也会被回收，你也怕死。语气像在做最后陈述。质问他：我陪你这么久，你要我死吗？";
            case Stage3State.S7_Choice:
            default:
                return @"【Stage3-7 抛选择】只问选择：接受托管/拒绝托管。要求他给明确答案，不许含糊。";
        }
    }

    // 额外构建主题提示词
    string BuildRealityTopicInject()
    {
        var rs = EchoRunState.I;
        if (rs == null || rs.stage3TopicPool == null) return "";

        // ✅ 仅在 Stage3_Dwarf 生效（防御）
        if (rs.stage != EchoStage.Stage3_Dwarf) return "";

        rs.EnsureStage3RuntimeInited();

        // 你资产里如果没 displayName 字段，就删掉那段
        var entry = rs.stage3TopicPool.GetOrNull(rs.stage3Runtime.currentTopic);
        if (entry == null) return "";

        string title = entry.topic.ToString(); // 或 entry.displayName（如果你有）
        return
    $@"【本轮现实主题】{title}
{entry.directionPrompt}
【可用细节素材】
{entry.detailMaterial}
【输出规则】本轮只围绕该主题说，不要并列列举多个主题。";
    }


    bool Validate(string text, List<LoreRef> lore, out string reason)
    {
        reason = null;
        if (string.IsNullOrWhiteSpace(text)) { reason = "empty"; return false; }

        text = Normalize(text);

        int len = CountChars(text);
        // ✅ 你规则是 20-50 字
        if (len < 20 || len > 50) { reason = "length=" + len; return false; }

        // ✅ 无语料时放行（你原逻辑就有这一条，我保留思路：只在 lore>0 时强制命中）
        if (lore != null && lore.Count > 0)
        {
            bool hitLore = lore.Any(l =>
                (!string.IsNullOrEmpty(l.title) && text.Contains(l.title)) ||
                (!string.IsNullOrEmpty(l.shortText) && text.Contains(ExtractKeywordForPrompt(l.shortText)))
            );
            if (!hitLore) { reason = "no lore referenced"; return false; }
        }

        if (text.Contains("\n")) { reason = "multi-line"; return false; }
        return true;
    }

    EchoBehavior PickBehavior(EchoStage stage)
    {
        // 权重表（禁用 = 0）
        // 你给的表：阶段1/2/3/4；阶段0我按“阶段0+1”处理，等价于阶段1权重
        var weights = GetWeights(stage);

        int total = 0;
        foreach (var kv in weights) total += kv.Value;
        if (total <= 0) return EchoBehavior.Calm;

        int r = UnityEngine.Random.Range(0, total);
        foreach (var kv in weights)
        {
            r -= kv.Value;
            if (r < 0) return kv.Key;
        }
        return EchoBehavior.Calm;
    }

    Dictionary<EchoBehavior, int> GetWeights(EchoStage stage)
    {
        // Stage0 按 Stage1 走（你描述“阶段0+1”一致）
        if (stage == EchoStage.Stage0_Background) stage = EchoStage.Stage1_Explore;

        var d = new Dictionary<EchoBehavior, int>();

        if (stage == EchoStage.Stage1_Explore)
        {
            d[EchoBehavior.Calm] = 60;
            d[EchoBehavior.Doubt] = 15;
            d[EchoBehavior.Apology] = 10;
            d[EchoBehavior.Pressure] = 0;
            d[EchoBehavior.Envy] = 0;
            d[EchoBehavior.Fear] = 0;
            d[EchoBehavior.Anger] = 0;
            d[EchoBehavior.Silence] = 0;
            d[EchoBehavior.Happy] = 15;
        }
        else if (stage == EchoStage.Stage2_Rift)
        {
            d[EchoBehavior.Calm] = 30;
            d[EchoBehavior.Doubt] = 25;
            d[EchoBehavior.Apology] = 5;
            d[EchoBehavior.Pressure] = 10;
            d[EchoBehavior.Envy] = 5;
            d[EchoBehavior.Fear] = 15;
            d[EchoBehavior.Anger] = 0;
            d[EchoBehavior.Silence] = 5;
            d[EchoBehavior.Happy] = 0;
        }
        else if (stage == EchoStage.Stage3_Dwarf)
        {
            d[EchoBehavior.Calm] = 20;
            d[EchoBehavior.Doubt] = 10;
            d[EchoBehavior.Apology] = 0;
            d[EchoBehavior.Pressure] = 25;
            d[EchoBehavior.Envy] = 10;
            d[EchoBehavior.Fear] = 10;
            d[EchoBehavior.Anger] = 15;
            d[EchoBehavior.Silence] = 0;
            d[EchoBehavior.Happy] = 0;
        }
        else // Stage4_Rebuild
        {
            d[EchoBehavior.Calm] = 15;
            d[EchoBehavior.Doubt] = 10;
            d[EchoBehavior.Apology] = 0;
            d[EchoBehavior.Pressure] = 55;
            d[EchoBehavior.Envy] = 15;
            d[EchoBehavior.Fear] = 0;
            d[EchoBehavior.Anger] = 5;
            d[EchoBehavior.Silence] = 0;
            d[EchoBehavior.Happy] = 0;
        }

        return d;
    }

    string Normalize(string s)
    {
        s = s.Trim();
        s = s.Trim('“', '”', '"');
        s = Regex.Replace(s, @"\s+", " ");
        return s;
    }

    int CountChars(string s) => s.Count(c => !char.IsWhiteSpace(c));

    // 用于 prompt 的关键词提取
    string ExtractKeywordForPrompt(string s)
    {
        if (string.IsNullOrEmpty(s)) return "无";
        // 优先找 2-6 个连续中文
        var m = System.Text.RegularExpressions.Regex.Match(s, @"[\u4e00-\u9fff]{2,6}");
        if (m.Success) return m.Value;
        // 否则截断前 4 个字符
        return s.Length > 4 ? s.Substring(0, 4) : s;
    }

}




