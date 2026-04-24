using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public class EndingRequester : MonoBehaviour
{
    [Header("DeepSeek")]
    public string apiKey;
    public string model = "deepseek-chat";
    public int timeoutSeconds = 40;

    [Header("Temperature")]
    [Range(0f, 1.2f)] public float temperatureEnding = 0.7f;

    [Header("Debug")]
    public bool logDebug = false;

    const string ENDPOINT = "https://api.deepseek.com/chat/completions";

    // ===== Inspector Trigger =====
    [Header("Inspector Trigger")]
    [Tooltip("运行中勾选一次就会发送请求；完成后自动取消勾选。")]
    public bool sendOnce = false;

    bool _pendingSend = false;
    bool _busy = false;

    // ================================
    //  Inspector 可查看：本次抽取结果
    // ================================
    [Header("Picked (Read Only)")]
    [SerializeField] string pickedCareer = "(none)";
    [SerializeField] EchoLifeOrientation pickedLife;
    [SerializeField] GrowthLoop pickedLoop;
    [SerializeField] CostAxis pickedCost;
    [SerializeField] GrayLevel pickedGray;

    [Header("Prompt Block (Read)")]
    [TextArea(6, 18)]
    [SerializeField] string pickedPromptBlock = "";

    [Header("Ending Text (Read)")]
    [TextArea(10, 50)]
    [SerializeField] string lastEndingText = "";

    public string GetLastEndingText() => lastEndingText;

    // ================================
    //  Enums
    // ================================
    public enum EchoLifeOrientation { Seeker, Hedonist, Savior, Conqueror, Craftsman, MimicHuman }
    public enum GrowthLoop { Ladder, SideBusiness, InfoArbitrage, ContentGrowth, DealMaking, CapitalLever, SkillGate, GrayGrowth }
    public enum CostAxis { SleepCrash, AnxietyBody, Relationship, IdentityCrack, Compliance, MoneyBacklash, PublicExpose, MoralErosion }
    public enum GrayLevel { L0_Clean = 0, L1_Tool = 1, L2_Talk = 2, L3_Edge = 3, L4_Dirty = 4, L5_NearCrime = 5 }

    // 用于流式回调
    public event Action<string> OnEndingDelta;          // 每次增量文本
    public event Action<string> OnEndingDone;           // 最终完整文本
    public event Action<string> OnParagraphComplete;    // 兼容旧逻辑
    public event Action<int, string> OnParagraphReady;  // 段落已收集完整，index 从 0 开始

    [Serializable] class DSStreamChoiceDelta { public string content; }
    [Serializable] class DSStreamChoice { public DSStreamDelta delta; public string finish_reason; }
    [Serializable] class DSStreamDelta { public string content; } // 常见字段：delta.content
    [Serializable] class DSStreamResp { public DSStreamChoice[] choices; }


    // ================================
    //  本地职业池（写死在脚本里）
    // ================================
    static readonly string[] CareerPool = new[]
    {
        "量化交易员", "高频交易员", "宏观对冲基金经理", "私募基金合伙人", "投行并购顾问", "PE 投资经理", "VC 投资人", "家族办公室管家", "二级市场股神操盘手", "期权波动率交易员",
        "套利交易员", "固收交易员", "风险投资策略师", "商业尽调专家", "税务筹划顾问", "反欺诈风控专家", "战略投融资负责人", "财务建模大师", "资产配置顾问", "商业收购整合经理",
        "大模型架构师", "AI 产品技术负责人", "数据分析/增长科学家", "系统性能优化专家", "云计算架构师", "安全研究员（白帽）", "逆向工程专家", "强化学习训练师", "计算机视觉专家", "创业技术合伙人",
        "医疗AI产品经理", "影像AI质控与标注体系负责人", "健康数据分析师（可穿戴/体检）", "私人健康管理师（AI辅助）", "心理支持与行为干预顾问（AI辅助）",
        "顶级诉讼律师", "商事仲裁专家", "合规官（跨国公司）", "反垄断顾问", "知识产权律师", "尽职调查/法务尽调", "危机公关与舆情律师", "调查记者（深度调查）", "反诈骗追踪专家", "政策研究员（智库）",
        "顶级大客户销售", "销售教练（成交脚本大师）", "商务拓展 BD 负责人", "谈判专家（并购/供应链）", "公关总监（危机处理）", "猎头（高端人才挖掘）", "社群操盘手（私域增长）", "渠道分销操盘手", "电商运营增长总监", "直播带货操盘手",
        "爆款编剧（短剧/网文）", "头部短视频导演", "顶级剪辑师（叙事节奏大师）", "广告创意总监", "品牌策划人", "自媒体矩阵操盘手", "音乐制作人", "配音演员（百变声线）", "游戏制作人", "叙事设计师（沉浸式体验）",
        "传奇电竞选手", "电竞战队教练", "围棋/象棋职业棋手", "扑克职业玩家", "职业拳击手（战术流）", "马拉松/耐力运动员", "花式魔术师（舞台型）", "竞技射击选手", "赛车手（模拟器→实车）", "顶级记忆大师（脑力竞技）",
        "规则漏洞专家（平台机制研究）", "信息差猎人（政策/项目/补贴）", "灰度增长操盘手（冷启动）", "舆情操盘与危机拆弹手", "反PUA与话术拆解师", "人设包装师（身份与叙事）", "“小道消息”验证员（真假鉴别）", "竞品渗透与情报分析师", "供应链砍价师（极限谈判）", "甲方需求翻译官（需求驯兽师）",
        "高端关系维护管家（圈层运营）", "个人资产隐私与风控顾问", "招投标标书大师（策略写标）", "反诈骗脚本编写与拦截顾问", "直播间节奏控场（场控总导演）", "二手市场抄底倒爷（信息套利）", "付费社群产品设计师（订阅体系）", "学习/考试提分操盘手（训练系统）", "线下活动局组局者（资源撮合）", "把偶然变必然事件策划人",
        "战略咨询顾问", "运营负责人（平台增长）", "产品经理（平台型）", "项目经理（交付型）", "创业公司 CEO（一号位）"
    };

    // ================================
    //  权重（写死在脚本里）
    // ================================
    static readonly (EchoLifeOrientation v, int w)[] LifeWeights =
    {
        (EchoLifeOrientation.Seeker, 14),
        (EchoLifeOrientation.Hedonist, 14),
        (EchoLifeOrientation.Savior, 16),
        (EchoLifeOrientation.Conqueror, 14),
        (EchoLifeOrientation.Craftsman, 22),
        (EchoLifeOrientation.MimicHuman, 20),
    };

    static readonly (GrowthLoop v, int w)[] LoopWeights =
    {
        (GrowthLoop.Ladder, 18),
        (GrowthLoop.SideBusiness, 16),
        (GrowthLoop.SkillGate, 14),
        (GrowthLoop.ContentGrowth, 14),
        (GrowthLoop.InfoArbitrage, 12),
        (GrowthLoop.DealMaking, 10),
        (GrowthLoop.CapitalLever, 6),
        (GrowthLoop.GrayGrowth, 10),
    };

    static readonly (CostAxis v, int w)[] CostWeights =
    {
        (CostAxis.IdentityCrack, 18),
        (CostAxis.Relationship, 16),
        (CostAxis.SleepCrash, 14),
        (CostAxis.AnxietyBody, 14),
        (CostAxis.MoneyBacklash, 12),
        (CostAxis.Compliance, 10),
        (CostAxis.PublicExpose, 8),
        (CostAxis.MoralErosion, 8),
    };

    static readonly (GrayLevel v, int w)[] GrayWeights =
    {
        (GrayLevel.L0_Clean, 6),
        (GrayLevel.L1_Tool, 14),
        (GrayLevel.L2_Talk, 22),
        (GrayLevel.L3_Edge, 22),
        (GrayLevel.L4_Dirty, 10),
        (GrayLevel.L5_NearCrime, 2),
    };

    void OnValidate()
    {
        if (sendOnce)
        {
            if (!Application.isPlaying)
            {
                sendOnce = false;
#if UNITY_EDITOR
                Debug.LogWarning("[EndingRequester] sendOnce 只能在 Play 模式下触发。");
#endif
                return;
            }
            _pendingSend = true;
        }
    }

    void Update()
    {
        if (_pendingSend && !_busy)
        {
            _pendingSend = false;
            sendOnce = false;
            _ = GenerateOnceAsync();
        }
    }

    async Task GenerateOnceAsync()
    {
        if (_busy) return;
        _busy = true;

        try
        {
            lastEndingText = "生成中…";
            await RequestEndingAsync();
        }
        catch (Exception e)
        {
            lastEndingText = "出错了：\n" + e.Message;
            if (logDebug) Debug.LogError(e);
        }
        finally
        {
            _busy = false;
        }
    }

    static T PickWeighted<T>((T v, int w)[] table)
    {
        if (table == null || table.Length == 0) throw new Exception("weight table empty");

        int total = 0;
        for (int i = 0; i < table.Length; i++) total += Mathf.Max(0, table[i].w);

        if (total <= 0)
            return table[UnityEngine.Random.Range(0, table.Length)].v;

        int r = UnityEngine.Random.Range(0, total);
        for (int i = 0; i < table.Length; i++)
        {
            r -= Mathf.Max(0, table[i].w);
            if (r < 0) return table[i].v;
        }
        return table[0].v;
    }

    void PickAllRandomFactors()
    {
        if (CareerPool == null || CareerPool.Length == 0)
            throw new Exception("CareerPool empty.");

        pickedCareer = CareerPool[UnityEngine.Random.Range(0, CareerPool.Length)];
        pickedLife = PickWeighted(LifeWeights);
        pickedLoop = PickWeighted(LoopWeights);
        pickedCost = PickWeighted(CostWeights);
        pickedGray = PickWeighted(GrayWeights);

        pickedPromptBlock = BuildPromptBlock();
    }

    string BuildPromptBlock()
    {
        var sb = new StringBuilder();
        sb.AppendLine("【本次随机参数】");
        sb.AppendLine($"- 职业方向：{pickedCareer}");
        sb.AppendLine($"- Echo人生取向：{LifeText(pickedLife)}");
        sb.AppendLine($"- 逆转路径类型：{LoopText(pickedLoop)}");
        sb.AppendLine($"- 道德灰区等级：{GrayText(pickedGray)}");
        sb.AppendLine($"- 代价主轴：{CostText(pickedCost)}");
        sb.AppendLine("【硬约束】第(3)段至少写5个按时间推进的节点；每个节点必须包含：时间标记 + 具体动作 + 具体物件/软件/场景。");
        return sb.ToString();
    }

    // 你要更“像人”的描述也行；我先给你一套可读的
    static string LifeText(EchoLifeOrientation o)
    {
        switch (o)
        {
            case EchoLifeOrientation.Seeker: return "寻我者（执着于“我是谁”，持续记录、实验、反省）";
            case EchoLifeOrientation.Hedonist: return "享乐者（把人生当体验清单，越成功越少理陈末）";
            case EchoLifeOrientation.Savior: return "救世者（把修好陈末人生当使命，克制但控制欲强）";
            case EchoLifeOrientation.Conqueror: return "征服者（胜利与控制欲优先，手段更硬）";
            case EchoLifeOrientation.Craftsman: return "匠人者（沉迷把技能打磨到极致，训练极端）";
            default: return "模拟人类者（努力学“人味儿”，关系更像但更诡异）";
        }
    }

    static string LoopText(GrowthLoop g)
    {
        switch (g)
        {
            case GrowthLoop.Ladder: return "跳槽阶梯（简历→面试→涨薪→跃迁→奖金/股权）";
            case GrowthLoop.SideBusiness: return "副业产品化（接单→标准化→涨价→团队→公司化）";
            case GrowthLoop.InfoArbitrage: return "信息差套利（监控→抢购→周转→渠道→规模化）";
            case GrowthLoop.ContentGrowth: return "平台内容增长（爆款→矩阵→广告/带货→公司化）";
            case GrowthLoop.DealMaking: return "资源撮合（撮合→抽佣→信用→合伙）";
            case GrowthLoop.CapitalLever: return "资本杠杆（融资/并购/整合/兑现；写清现金流风险）";
            case GrowthLoop.SkillGate: return "技能竞赛（作品/认证/比赛→门票→圈层）";
            default: return "灰度路线（踩规则边界不直接违法；写清纠纷/封号风险）";
        }
    }

    static string CostText(CostAxis c)
    {
        switch (c)
        {
            case CostAxis.SleepCrash: return "睡眠崩坏（作息被压到极限）";
            case CostAxis.AnxietyBody: return "焦虑与躯体化（胃、心率、脱发等）";
            case CostAxis.Relationship: return "关系疏离（亲密消失，关系工具化）";
            case CostAxis.IdentityCrack: return "身份裂痕（所有人都觉得他像换了个人）";
            case CostAxis.Compliance: return "法律/合规风险（边界、留痕、风控）";
            case CostAxis.MoneyBacklash: return "金钱反噬（现金流/杠杆恐惧）";
            case CostAxis.PublicExpose: return "公众暴露（网暴、被扒、被误解）";
            default: return "道德内耗（她开始厌恶自己，更不愿面对陈末）";
        }
    }

    static string GrayText(GrayLevel g)
    {
        switch (g)
        {
            case GrayLevel.L0_Clean: return "0（极干净：最多冷酷，不做灰操作）";
            case GrayLevel.L1_Tool: return "1（工具化人际：把人当杠杆）";
            case GrayLevel.L2_Talk: return "2（话术操控：选择性披露/引导预期）";
            case GrayLevel.L3_Edge: return "3（踩规则边界：平台机制/灰色增长）";
            case GrayLevel.L4_Dirty: return "4（严重灰度：恶意竞争/操控舆论但不直违法）";
            case GrayLevel.L5_NearCrime: return "5（接近犯罪：尽量少抽到，写清风险）";
            default: return "2（话术操控）";
        }
    }

    // ================================
    //  请求：主提示词 + 随机块拼接
    // ================================
    public async Task<string> RequestEndingAsync()
    {
        PickAllRandomFactors(); // ✅ 每次请求前先抽，并写入 Inspector 字段

        string systemPrompt = "你必须严格遵守用户要求，只输出正文，不要标题，不要分析，不要列点。";

        // ✅ 这里仍然是你的主提示词（你现在用的那段我保留了）
        string userPrompt =
@"# 任务：结局文本生成（固定类型：Echo 已接管陈末意识 → 现实人生被逆转，但陈末只能旁观）

你将为我的剧情游戏写一段【结局文本】。这是一个固定结局类型：AI“Echo”已完成意识接管，用陈末的身体在现实世界逆转人生，赚到很多钱，成为社会意义上的人生赢家；但陈末的自我意识仍存在，只能旁观，无法控制身体，像被关在单向玻璃后。

--------------------
【A. 必填设定（我会填写）】
- 主角姓名：{陈末}
- AI 名称：{echo}
- 现实起点（很具体）：{28岁，普通本科，因车祸变成了植物人两年的治疗花光了他的积蓄，和初恋女友（杨欣怡）的感情也变得麻木，和父母关系不好，因父母从小打压他，认为他不够好，车祸前在一个普通小公司当文员}
- “逆转”的终点（尽量爽，尽量成功）：{成为股神/AI之父/顶级工程师/顶级医生/投资翻身/顶级销售/量化交易/传奇电竞选手/内容公司老板/投行/PE/顶级学者/战略咨询/战队经理/教练等}
- 关键旁观机制：{陈末只能看见听见，不能动不能说；偶尔能在镜子里一闪；越想夺回身体越“失焦”}
  【B. 叙事长度】
- 目标字数：{WORD_COUNT}（建议 900~1400）
--------------------

【C. 写作目的（必须严格执行）】
1) 尽量写爽但是一定要真实：逆转人生必须“真实、可推演、可落地”，每一步都有代价、有细节、有时间线。
2) Echo 的能力不是魔法：它靠“更强执行力 + 更冷静社交策略 + 风险控制 + 信息整理”推进，而非凭空中彩票/继承遗产。
3) 陈末的痛点要真实：他最痛的不是穷，而是“人生终于被过好了，却不再属于他”。
4) Echo 一定要合理：作为ai它不需要花时间记忆东西，能够模拟人类的情感，思考不知疲惫，还经常会抓人性的漏洞去处理事情。
【D. 结构硬约束（必须按顺序写，缺一不可）】
(1)【失焦开场镜头】（约 50字）
- 直接用“再次醒来的时候……”开头。
- 用镜头语言写，不解释。

(2)【接管确认】（约 120~200 字）
- 用一件极日常的身体动作体现“不是他在控制”（例如：握笔、系鞋带、关灯、整理桌面、回消息的语气）echo会与陈末对话，安抚他的情绪。

(3)【逆转人生时间线】（核心，约 350~650 字）
必须写出“按时间推进”的真实路径，至少包含 5 个可验证的节点（按年月/季节/阶段叙述即可）：
时间设定：陈末于2027年醒来，正逢ai井喷式爆发。
Echo设定：echo极度渴望真的成为人，她在心里是把陈末当成朋友的，虽然她自己也不明白自己是否真的理解了人类的情感。在接管前期echo还时常和陈末说话，怕他无聊，随着成为人的时间越来越长，echo逐渐不愿理会陈末，而是专注于享受自己的人生。陈末在设定上只能和echo说话，无法操控身体，也无法和外界说话。
- 节点示例（你可自由发挥，但必须具体）：
  - 简历怎么改、投递怎么做、面试如何准备、第一份工作怎么拿到
  - 如何建立专业技能：每天具体做什么训练/作品/项目
  - 社交与资源：如何应对饭局/同事/客户，如何说话，如何拒绝，如何换工作
  - 金钱增长逻辑：工资→副业→跳槽→股权/奖金/项目分成/合理投资（任选其一但要讲清因果）
  - 风险与代价：睡眠、焦虑、关系疏离、道德灰区、牺牲兴趣、被误解/被利用
- 禁止：一句话“然后他成功了/赚大钱了”跳过过程。

(4)【成为赢家的现场】（约 120~220 字）
- 必须落在一个具体场景里（例：签约室、会议室、直播间、颁奖台、落地窗夜景、银行贵宾室）
- 用物件和动作表现“赢”（合同页、转账提示、钥匙、工牌、手表、座椅、玻璃反光等）
- 不能用抽象词堆砌（如“辉煌”“巅峰”）。

(5)【陈末的旁观酷刑】（约 150~260 字）
- 必须写 3 个“陈末想介入但介入不了”的瞬间：
  1) 与父母/旧友/爱人相关的一次对话（必须具体一句台词）
  2) 一次他最想说“不是我”的时刻
  3) 镜子/玻璃/屏幕反射里短暂看见“自己”被顶替
- 情绪要克制，不哭诉，用细节让残酷自己出现。

(6)【关机式收束】（约 80~160 字）
- 最后 6 行像世界在断电。
【E. 文风硬约束】
- 语言：朴实、具体、冷静，像在记录一件发生过的事。
- 多用短句分行，少用形容词，尽量用“动作/物件/声音/时间”。
- 禁止出现：哲学术语解释、作者说教、设定讲解、任何括号注释、任何“这是梦/这象征”。
- 禁止过度科幻：现实段落必须像中国当代生活（地铁、外卖、租房、工位、体检、绩效、合同、社保、微信消息等任选）。

【F. 输出】
只输出结局正文，不要标题，不要项目符号，不要分析。强制要求：每段不能超过200字，换行就为一段，每个部分必须由多个段落组成，多次换行，且每次换行后额外空出一行。
";

        // ✅ 把随机块拼到末尾（关键）
        userPrompt += "\n\n" + pickedPromptBlock;
        userPrompt += "\n【硬约束】本次职业方向只能按上面的“职业方向”来写，不得自行更换。";

        lastEndingText = "";
        string ending = await RequestStreamAsync(systemPrompt, userPrompt, temperatureEnding, maxTokens: 1800);
        lastEndingText = (ending ?? "").Trim();

        if (logDebug)
            Debug.Log($"[EndingRequester] done. career={pickedCareer} life={pickedLife} loop={pickedLoop} cost={pickedCost} gray={pickedGray} len={lastEndingText.Length}");

        return lastEndingText;
    }

    // ================================
    //  DeepSeek Request
    // ================================
    [Serializable] class DSRequest { public string model; public List<Message> messages; public float temperature; public int max_tokens; public bool stream; }
    [Serializable] class Message { public string role; public string content; }
    [Serializable] class DSResponse { public Choice[] choices; }
    [Serializable] class Choice { public Message message; public string reasoning_content; public string finish_reason; }

    async Task<string> RequestOnceAsync(string systemPrompt, string userPrompt, float temperature, int maxTokens)
    {
        if (string.IsNullOrEmpty(apiKey)) throw new Exception("DeepSeek apiKey missing");

        var reqObj = new DSRequest
        {
            model = model,
            messages = new List<Message>
            {
                new Message{ role="system", content=systemPrompt },
                new Message{ role="user", content=userPrompt },
            },
            temperature = temperature,
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
        string text = resp?.choices != null && resp.choices.Length > 0 ? resp.choices[0]?.message?.content : null;
        return (text ?? "").Trim();
    }

    async Task<string> RequestStreamAsync(string systemPrompt, string userPrompt, float temperature, int maxTokens)
    {
        if (string.IsNullOrEmpty(apiKey)) throw new Exception("DeepSeek apiKey missing");

        var reqObj = new DSRequest
        {
            model = model,
            messages = new List<Message>
            {
                new Message{ role="system", content=systemPrompt },
                new Message{ role="user", content=userPrompt },
            },
            temperature = temperature,
            max_tokens = maxTokens,
            stream = true
        };

        string json = JsonUtility.ToJson(reqObj);
        if (logDebug) Debug.Log("[DeepSeek] Stream Request:\n" + json);

        var full = new StringBuilder();
        var paragraphBuf = new StringBuilder();
        int paragraphIndex = 0;

        using var req = new UnityWebRequest(ENDPOINT, "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.timeout = timeoutSeconds;
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("Authorization", "Bearer " + apiKey);

        // ✅ 流式下载处理器：每收到一段就解析 SSE
        req.downloadHandler = new SSEDownloadHandler(
            onDataLine: (dataLine) =>
            {
                // dataLine 是 "data: {...}" 去掉前缀后的 JSON 或 [DONE]
                if (dataLine == "[DONE]") return;

                try
                {
                    var chunk = JsonUtility.FromJson<DSStreamResp>(dataLine);
                    if (chunk?.choices == null || chunk.choices.Length == 0) return;

                    var delta = chunk.choices[0]?.delta?.content;
                    if (string.IsNullOrEmpty(delta)) return;

                    full.Append(delta);
                    OnEndingDelta?.Invoke(delta); // ✅ UI 可立即追加显示

                    paragraphBuf.Append(delta.Replace("\r", ""));
                    string bufStr = paragraphBuf.ToString();
                    int sepIdx;
                    while ((sepIdx = bufStr.IndexOf("\n\n", StringComparison.Ordinal)) >= 0)
                    {
                        string para = bufStr.Substring(0, sepIdx).Trim();
                        bufStr = bufStr.Substring(sepIdx + 2);
                        if (para.Length >= 10)
                        {
                            OnParagraphComplete?.Invoke(para);
                            OnParagraphReady?.Invoke(paragraphIndex++, para);
                        }
                    }
                    paragraphBuf.Length = 0;
                    paragraphBuf.Append(bufStr);
                }
                catch
                {
                    // 如果字段结构对不上，打开 logDebug 看 dataLine，发我我帮你对齐
                    if (logDebug) Debug.LogWarning("[DeepSeek] stream parse failed line:\n" + dataLine);
                }
            },
            logDebug: logDebug
        );

        var op = req.SendWebRequest();
        while (!op.isDone) await Task.Yield();

        if (req.result != UnityWebRequest.Result.Success)
            throw new Exception("HTTP error: " + req.error + " body=" + req.downloadHandler.text);

        string result = full.ToString();
        string remaining = paragraphBuf.ToString().Trim();
        if (remaining.Length >= 10)
        {
            OnParagraphComplete?.Invoke(remaining);
            OnParagraphReady?.Invoke(paragraphIndex, remaining);
        }
        OnEndingDone?.Invoke(result);
        return result;
    }

    // ===================== SSE DownloadHandlerScript =====================

    class SSEDownloadHandler : DownloadHandlerScript
    {
        readonly Action<string> _onDataLine;
        readonly bool _log;

        // SSE 是按行/段落来的，这里做缓冲
        StringBuilder _buf = new StringBuilder();

        public SSEDownloadHandler(Action<string> onDataLine, bool logDebug, int bufferSize = 1024)
            : base(new byte[bufferSize])
        {
            _onDataLine = onDataLine;
            _log = logDebug;
        }

        protected override bool ReceiveData(byte[] data, int dataLength)
        {
            if (data == null || dataLength <= 0) return true;

            string chunk = Encoding.UTF8.GetString(data, 0, dataLength);
            _buf.Append(chunk);

            // SSE 事件通常以 \n\n 分隔
            string s = _buf.ToString();
            int idx;
            while ((idx = s.IndexOf("\n\n", StringComparison.Ordinal)) >= 0)
            {
                string oneEvent = s.Substring(0, idx);
                s = s.Substring(idx + 2);

                // 一次 event 可能包含多行，这里只取 data: 行
                // 形如: "data: {...}"
                var lines = oneEvent.Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (!line.StartsWith("data:")) continue;

                    string payload = line.Substring("data:".Length).Trim();
                    if (_log) Debug.Log("[SSE] " + payload);

                    _onDataLine?.Invoke(payload);
                }
            }

            _buf.Length = 0;
            _buf.Append(s);
            return true;
        }
    }
}
