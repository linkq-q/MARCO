using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public class DiaryCloudResponder : MonoBehaviour
{
    [Header("DeepSeek")]
    public string apiKey;
    public string model = "deepseek-chat";
    public int timeoutSeconds = 12;

    [Header("Diary Generation")]
    [Range(0f, 1.2f)]
    public float temperature = 0.7f;

    [Tooltip("日记专用 System Prompt（你的新提示词放这里）")]
    [TextArea(6, 30)]
    public string diarySystemPrompt;

    [Tooltip("日记专用 User Prompt 模板（可选，用{day}替换）")]
    [TextArea(4, 20)]
    public string diaryUserTemplate = "写第{day}天的日记，100-180字，只输出正文。";

    [Tooltip("日记输出 token 上限（长文本建议 250~450）")]
    public int maxTokens = 350;

    public bool logDebug = false;

    const string ENDPOINT = "https://api.deepseek.com/chat/completions";

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
        public string finish_reason;
    }

    public async Task<string> GenerateDiaryAsync(int dayIndex, string userOverride = null)
    {
        if (string.IsNullOrEmpty(apiKey))
            throw new Exception("DeepSeek apiKey missing (DiaryCloudResponder)");

        string userPrompt = !string.IsNullOrWhiteSpace(userOverride)
            ? userOverride
            : (diaryUserTemplate ?? "").Replace("{day}", dayIndex.ToString());

        var reqObj = new DSRequest
        {
            model = model,
            messages = new List<Message>
            {
                new Message { role = "system", content = diarySystemPrompt ?? "" },
                new Message { role = "user", content = userPrompt }
            },
            temperature = temperature,
            max_tokens = maxTokens,
            stream = false
        };

        string json = JsonUtility.ToJson(reqObj);
        if (logDebug) Debug.Log("[Diary DeepSeek] Request:\n" + json);

        using var req = new UnityWebRequest(ENDPOINT, "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.timeout = timeoutSeconds;

        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("Authorization", "Bearer " + apiKey);

        var op = req.SendWebRequest();
        while (!op.isDone) await Task.Yield();

        if (req.result != UnityWebRequest.Result.Success)
            throw new Exception("Diary HTTP error: " + req.error + " body=" + req.downloadHandler.text);

        string body = req.downloadHandler.text;
        if (logDebug) Debug.Log("[Diary DeepSeek] Response:\n" + body);

        var resp = JsonUtility.FromJson<DSResponse>(body);

        string text = resp?.choices != null && resp.choices.Length > 0
            ? resp.choices[0]?.message?.content
            : null;

        return (text ?? "").Trim();
    }
}
