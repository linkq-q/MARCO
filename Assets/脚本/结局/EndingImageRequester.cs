using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public class EndingImageRequester : MonoBehaviour
{
    static readonly string[] PlaceKeywords =
    {
        "酒店", "地铁", "工位", "会议室", "签约室", "医院", "银行",
        "办公室", "租房", "咖啡馆", "机场", "舞台", "赛场", "车里",
        "走廊", "窗前", "楼道", "停车场"
    };

    static readonly string[] ObjectKeywords =
    {
        "合同", "手表", "玻璃", "键盘", "手机", "屏幕", "外卖",
        "烟", "咖啡", "椅子", "文件", "印章", "钥匙", "奖杯",
        "工牌", "银行卡", "笔记本"
    };

    static readonly string[] ActionKeywords =
    {
        "签", "握", "坐", "站", "靠", "盯", "看", "走", "抽", "喝"
    };

    static readonly string[] PersonKeywords =
    {
        "他", "她", "Echo", "陈末"
    };

    const string FixedPrefix = "中国当代都市，电影摄影，冷色调，35mm胶片颗粒感，自然光，";
    const string FixedSuffix = "，背影或空间构图，克制写实，无人物面部特写，无文字";
    const string ENDPOINT =
        "https://dashscope.aliyuncs.com/api/v1/services/aigc/multimodal-generation/generation";

    [Header("万相2.6 API")]
    [TextArea(1, 2)] public string apiKey;
    public string model = "wan2.6-t2i";
    public string imageSize = "1344*576";

    [Header("超时")]
    public int timeoutSeconds = 60;

    [Header("Debug")]
    public bool logDebug = false;

    public async Task<Texture2D> GenerateImageAsync(string paragraphText)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            Debug.LogWarning("[ImageReq] apiKey 未填写", this);
            return null;
        }

        string prompt = BuildPrompt(paragraphText);
        if (logDebug) Debug.Log("[ImageReq] Prompt: " + prompt, this);

        string imageUrl = await RequestImageUrlAsync(prompt);
        if (string.IsNullOrEmpty(imageUrl)) return null;

        return await DownloadTextureAsync(imageUrl);
    }

    string BuildPrompt(string paragraphText)
    {
        string text = (paragraphText ?? string.Empty).Replace("\r", string.Empty).Trim();
        string coreSentence = ExtractCoreSentence(text);
        string dynamicPrefix = BuildDynamicPrefix(text);
        return FixedPrefix + dynamicPrefix + coreSentence + FixedSuffix;
    }

    string ExtractCoreSentence(string text)
    {
        string[] sentences = SplitSentences(text);

        string matched = FindSentenceByKeywords(sentences, PlaceKeywords);
        if (!string.IsNullOrEmpty(matched)) return matched;

        matched = FindSentenceByKeywords(sentences, ObjectKeywords);
        if (!string.IsNullOrEmpty(matched)) return matched;

        for (int i = 0; i < sentences.Length; i++)
        {
            string sentence = NormalizeSentence(sentences[i], 40);
            if (string.IsNullOrEmpty(sentence)) continue;
            if (ContainsAny(sentence, ActionKeywords) && ContainsAny(sentence, PersonKeywords))
                return sentence;
        }

        return FallbackSentence(text);
    }

    string BuildDynamicPrefix(string text)
    {
        var sb = new StringBuilder();

        if (ContainsAny(text, "镜", "玻璃", "反光", "倒影"))
            sb.Append("镜面反射，双重曝光，空旷，");

        if (ContainsAny(text, "签", "合同", "工位", "会议", "谈判", "办公"))
            sb.Append("职场，商务，现代办公，");

        if (ContainsAny(text, "夜", "深夜", "凌晨", "街灯"))
            sb.Append("夜晚，");

        if (ContainsAny(text, "断电", "结束", "消失", "黑暗", "最后"))
            sb.Append("空镜，极简，");

        if (ContainsAny(text, "清晨", "醒来", "病房", "白色"))
            sb.Append("清晨，冷白色空间，");

        return sb.ToString();
    }

    async Task<string> RequestImageUrlAsync(string prompt)
    {
        string body =
            "{"
            + $"\"model\":\"{EscapeJson(model)}\","
            + "\"input\":{"
            + "\"messages\":[{"
            + "\"role\":\"user\","
            + "\"content\":[{"
            + $"\"text\":\"{EscapeJson(prompt)}\""
            + "}]"
            + "}]"
            + "},"
            + "\"parameters\":{"
            + "\"prompt_extend\":false,"
            + "\"watermark\":false,"
            + "\"n\":1,"
            + $"\"size\":\"{EscapeJson(imageSize)}\""
            + "}"
            + "}";

        using var req = new UnityWebRequest(ENDPOINT, UnityWebRequest.kHttpVerbPOST);
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.timeout = timeoutSeconds;
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("Authorization", "Bearer " + apiKey);

        var op = req.SendWebRequest();
        while (!op.isDone) await Task.Yield();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning("[ImageReq] HTTP error: " + req.error
                + "\nbody: " + req.downloadHandler.text, this);
            return null;
        }

        string responseText = req.downloadHandler.text;
        if (logDebug) Debug.Log("[ImageReq] Response: " + responseText, this);

        var resp = JsonUtility.FromJson<Wan26Resp>(responseText);
        string url = resp?.output?.choices != null && resp.output.choices.Length > 0
            ? resp.output.choices[0]?.message?.content != null
              && resp.output.choices[0].message.content.Length > 0
                ? resp.output.choices[0].message.content[0]?.image
                : null
            : null;

        if (string.IsNullOrEmpty(url))
            url = ExtractImageUrlFallback(responseText);

        if (string.IsNullOrEmpty(url))
            Debug.LogWarning("[ImageReq] 未能解析到图片URL，响应：" + responseText, this);

        return url;
    }

    async Task<Texture2D> DownloadTextureAsync(string url)
    {
        using var req = UnityWebRequestTexture.GetTexture(url);
        var op = req.SendWebRequest();
        while (!op.isDone) await Task.Yield();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning("[ImageReq] 图片下载失败: " + req.error, this);
            return null;
        }

        return DownloadHandlerTexture.GetContent(req);
    }

    static string[] SplitSentences(string text)
    {
        if (string.IsNullOrEmpty(text)) return Array.Empty<string>();

        return text.Replace("\r", string.Empty)
            .Split(new[] { "。", "！", "？", ".", "!", "?", "\n" }, StringSplitOptions.RemoveEmptyEntries);
    }

    static string FindSentenceByKeywords(string[] sentences, string[] keywords)
    {
        for (int i = 0; i < sentences.Length; i++)
        {
            string sentence = NormalizeSentence(sentences[i], 40);
            if (string.IsNullOrEmpty(sentence)) continue;
            if (ContainsAny(sentence, keywords))
                return sentence;
        }

        return null;
    }

    static string FallbackSentence(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "一个人站在城市空间里";

        string[] sentences = SplitSentences(text);
        if (sentences.Length > 0)
        {
            string first = NormalizeSentence(sentences[0], int.MaxValue);
            string second = sentences.Length > 1 ? NormalizeSentence(sentences[1], int.MaxValue) : null;

            if (!string.IsNullOrEmpty(first) && !string.IsNullOrEmpty(second))
                return first + "，" + second;

            if (!string.IsNullOrEmpty(first))
                return first;
        }

        string clipped = text.Length > 80 ? text.Substring(0, 80) : text;
        clipped = clipped.Trim('，', '。', '！', '？', ',', '.', '!', '?', ';', ':', ' ');
        return string.IsNullOrEmpty(clipped) ? "一个人站在城市空间里" : clipped;
    }

    static string NormalizeSentence(string sentence, int maxLength)
    {
        if (string.IsNullOrEmpty(sentence)) return null;

        string trimmed = sentence.Trim();
        if (maxLength > 0 && trimmed.Length > maxLength)
            trimmed = trimmed.Substring(0, maxLength);

        return trimmed.Trim('，', '。', '！', '？', ',', '.', '!', '?', ';', ':', ' ');
    }

    static bool ContainsAny(string text, params string[] keywords)
    {
        if (string.IsNullOrEmpty(text) || keywords == null) return false;

        for (int i = 0; i < keywords.Length; i++)
        {
            string keyword = keywords[i];
            if (!string.IsNullOrEmpty(keyword) && text.Contains(keyword))
                return true;
        }

        return false;
    }

    static string ExtractImageUrlFallback(string responseText)
    {
        if (string.IsNullOrEmpty(responseText)) return null;

        int imgIdx = responseText.IndexOf("\"image\":", StringComparison.Ordinal);
        if (imgIdx < 0) return null;

        int start = responseText.IndexOf("\"", imgIdx + 8, StringComparison.Ordinal);
        if (start < 0) return null;
        start += 1;

        int end = responseText.IndexOf("\"", start, StringComparison.Ordinal);
        if (end <= start) return null;

        return responseText.Substring(start, end - start);
    }

    static string EscapeJson(string s) => (s ?? string.Empty)
        .Replace("\\", "\\\\")
        .Replace("\"", "\\\"")
        .Replace("\n", "\\n")
        .Replace("\r", string.Empty);

    [Serializable] class Wan26Resp { public Wan26Output output; }
    [Serializable] class Wan26Output { public Wan26Choice[] choices; public bool finished; }
    [Serializable] class Wan26Choice { public string finish_reason; public Wan26Message message; }
    [Serializable] class Wan26Message { public string role; public Wan26Content[] content; }
    [Serializable] class Wan26Content { public string image; public string type; }
}
