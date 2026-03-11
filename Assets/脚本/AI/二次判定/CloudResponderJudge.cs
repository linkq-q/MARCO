using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public class CloudResponderJudge : MonoBehaviour
{
    const string ENDPOINT = "https://api.deepseek.com/chat/completions"; // 如果你项目里用的是别的地址，改这里

    [Header("Auth")]
    [TextArea(1, 2)] public string apiKey;
    public string model = "deepseek-chat";

    [Header("Judge Prompt")]
    [TextArea(5, 30)] public string commonJudgeHeader; // 公用：你是状态判定器...JSON字段说明...只输出一行JSON...
    public EchoDecisionPromptBank promptBank;           // 每状态提示词表（推荐ScriptableObject）
    public int maxTokens = 120;
    [Range(0f, 1.5f)] public float temperature = 0.1f;

    [Header("Net")]
    public int timeoutSeconds = 20;

    [Header("Debug")]
    public bool logDebug = false;

    /// <summary>
    /// 判定：返回单行 JSON（advance/tag/sanDelta/note）
    /// system = commonJudgeHeader + statePrompt
    /// user   = injectedBlock（BuildInjectedBlock的那坨）
    /// </summary>
    public async Task<string> JudgeAsync(EchoStage stage, int subState, string injectedBlock)
    {
        if (string.IsNullOrEmpty(apiKey))
            throw new Exception("DeepSeek apiKey missing (Judge)");

        if (promptBank == null)
            throw new Exception("promptBank missing (Judge)");

        subState = Mathf.Max(1, subState);

        string statePrompt = promptBank.Get(stage, subState);
        var rs = EchoRunState.I;
        if (stage == EchoStage.Stage2_Rift && subState == 3 && rs != null && rs.stage2Runtime != null)
        {
            statePrompt = statePrompt.Replace("{TARGET_KEY}", rs.stage2Runtime.CurrentKeyword);
        }

        if (string.IsNullOrWhiteSpace(statePrompt))
            throw new Exception($"No judge prompt for stage={stage} subState={subState}");

        string sys = BuildJudgeSystemPrompt(commonJudgeHeader, statePrompt);
        string usr = injectedBlock ?? "";

        var reqObj = new DSRequest
        {
            model = model,
            messages = new List<Message>
            {
                new Message { role = "system", content = sys },
                new Message { role = "user", content = usr }
            },
            temperature = temperature,
            max_tokens = maxTokens,
            stream = false
        };

        string json = JsonUtility.ToJson(reqObj);
        if (logDebug) Debug.Log("[DeepSeek-Judge] Request:\n" + json);

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
        if (logDebug) Debug.Log("[DeepSeek-Judge] Response:\n" + body);

        var resp = JsonUtility.FromJson<DSResponse>(body);
        string text = (resp != null && resp.choices != null && resp.choices.Length > 0)
            ? resp.choices[0]?.message?.content
            : null;

        text = NormalizeJudgeJson(text);

        // 最后一道保险：必须单行
        if (!string.IsNullOrEmpty(text))
            text = text.Replace("\r", "").Replace("\n", "");

        return text;
    }

    static string BuildJudgeSystemPrompt(string commonHeader, string statePrompt)
    {
        if (string.IsNullOrWhiteSpace(commonHeader)) return statePrompt ?? "";
        if (string.IsNullOrWhiteSpace(statePrompt)) return commonHeader ?? "";
        return commonHeader.Trim() + "\n\n" + statePrompt.Trim();
    }

    /// <summary>
    /// 清洗：
    /// - 去掉 ```json ``` 包裹
    /// - 去掉首尾空白
    /// - 去掉多余换行
    /// </summary>
    static string NormalizeJudgeJson(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Trim();

        // 去代码块
        if (s.StartsWith("```"))
        {
            int firstNl = s.IndexOf('\n');
            if (firstNl >= 0) s = s.Substring(firstNl + 1);
            int lastFence = s.LastIndexOf("```", StringComparison.Ordinal);
            if (lastFence >= 0) s = s.Substring(0, lastFence);
        }

        s = s.Trim();
        s = s.Replace("\r", "").Replace("\n", "");

        // 有些模型会在前后加解释，尽量截取第一段 {...}
        int l = s.IndexOf('{');
        int r = s.LastIndexOf('}');
        if (l >= 0 && r > l)
            s = s.Substring(l, r - l + 1);

        return s.Trim();
    }

    // ===== DeepSeek JSON structs（如果你项目里已有同名类型，删掉这里） =====
    [Serializable]
    class DSRequest
    {
        public string model;
        public List<Message> messages;
        public float temperature = 0.1f;
        public int max_tokens = 120;
        public bool stream = false;
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
        public RespMessage message;
    }

    [Serializable]
    class RespMessage
    {
        public string content;
    }
}
