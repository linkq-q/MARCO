using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public class EndingImageRequester : MonoBehaviour
{
    const string SubmitEndpoint = "https://dashscope.aliyuncs.com/api/v1/services/aigc/text2image/image-synthesis";
    const string TaskEndpointPrefix = "https://dashscope.aliyuncs.com/api/v1/tasks/";

    static readonly string[] PromptStopFragments =
    {
        "they", "them", "their", "with", "from", "that", "this", "those", "these", "already",
        "start", "started", "continue", "continued", "suddenly", "because", "therefore",
        "however", "then", "while", "still"
    };

    [Header("DashScope")]
    [TextArea(1, 2)] public string apiKey;
    public string model = "wanx2.1-t2i-turbo";

    [Header("Image Params")]
    public string imageSize = "768*432";
    public int timeoutSeconds = 30;
    public int pollIntervalSeconds = 2;
    public int maxPollCount = 15;

    [Header("Debug")]
    public bool logDebug = false;

    public async Task<Texture2D> GenerateImageAsync(string paragraphText)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new Exception("DashScope apiKey is missing.");

        string prompt = BuildPrompt(paragraphText);
        string taskId = await SubmitTaskAsync(prompt);
        string imageUrl = await PollImageUrlAsync(taskId);
        return await DownloadTextureAsync(imageUrl);
    }

    string BuildPrompt(string paragraphText)
    {
        string core = ExtractPromptCore(paragraphText);
        return "\u7535\u5f71\u611f\uff0c\u73b0\u5b9e\u4e3b\u4e49\uff0c\u4e2d\u56fd\u5f53\u4ee3\u90fd\u5e02\uff0c"
            + core
            + "\uff0c\u51b7\u8272\u8c03\uff0c35mm\u80f6\u7247";
    }

    string ExtractPromptCore(string paragraphText)
    {
        if (string.IsNullOrWhiteSpace(paragraphText))
            return "\u5ba4\u5185\u7a7a\u95f4\uff0c\u4eba\u7269\u80cc\u5f71\uff0c\u57ce\u5e02\u591c\u8272";

        string cleaned = paragraphText.Replace("\r", "\n");
        cleaned = Regex.Replace(cleaned, @"\u201C[^\u201D]*\u201D|""[^""]*""|'[^']*'|\u2018[^\u2019]*\u2019", " ");
        cleaned = cleaned.Replace("\n", " ");
        cleaned = Regex.Replace(cleaned, "\\s+", " ").Trim();

        if (cleaned.Length > 80)
            cleaned = cleaned.Substring(0, 80);

        string[] parts = Regex.Split(cleaned, @"[\p{P}\p{Zs}]+");
        var keywords = new List<string>();

        for (int i = 0; i < parts.Length; i++)
        {
            string token = StripPromptNoise(parts[i]);
            if (token.Length < 2) continue;
            if (token.Length > 24) token = token.Substring(0, 24);
            if (keywords.Contains(token)) continue;

            keywords.Add(token);
            if (keywords.Count >= 5) break;
        }

        if (keywords.Count == 0)
            keywords.Add(cleaned.Length > 0 ? cleaned : "interior");

        return string.Join(", ", keywords);
    }

    string StripPromptNoise(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        string value = text.Trim();
        for (int i = 0; i < PromptStopFragments.Length; i++)
            value = value.Replace(PromptStopFragments[i], string.Empty);

        value = Regex.Replace(value, "[^\\p{L}\\p{Nd}]+", " ").Trim();
        return value;
    }

    async Task<string> SubmitTaskAsync(string prompt)
    {
        var reqObj = new DashScopeImageRequest
        {
            model = model,
            input = new DashScopeImageInput { prompt = prompt },
            parameters = new DashScopeImageParameters
            {
                size = imageSize,
                n = 1
            }
        };

        string json = JsonUtility.ToJson(reqObj);
        if (logDebug) Debug.Log("[EndingImageRequester] Submit:\n" + json, this);

        using var req = new UnityWebRequest(SubmitEndpoint, UnityWebRequest.kHttpVerbPOST);
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.timeout = timeoutSeconds;
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("Authorization", "Bearer " + apiKey);
        req.SetRequestHeader("X-DashScope-Async", "enable");

        var op = req.SendWebRequest();
        while (!op.isDone) await Task.Yield();

        if (req.result != UnityWebRequest.Result.Success)
            throw new Exception("DashScope submit failed: " + req.error + " body=" + req.downloadHandler.text);

        string body = req.downloadHandler.text;
        if (logDebug) Debug.Log("[EndingImageRequester] Submit response:\n" + body, this);

        var resp = JsonUtility.FromJson<DashScopeTaskResponse>(body);
        string taskId = resp?.output?.task_id;
        if (string.IsNullOrEmpty(taskId))
            throw new Exception("DashScope submit missing task_id: " + ExtractErrorMessage(resp, body));

        return taskId;
    }

    async Task<string> PollImageUrlAsync(string taskId)
    {
        int pollCount = Mathf.Max(1, maxPollCount);

        for (int i = 0; i < pollCount; i++)
        {
            using var req = UnityWebRequest.Get(TaskEndpointPrefix + taskId);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.timeout = timeoutSeconds;
            req.SetRequestHeader("Authorization", "Bearer " + apiKey);

            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result != UnityWebRequest.Result.Success)
                throw new Exception("DashScope poll failed: " + req.error + " body=" + req.downloadHandler.text);

            string body = req.downloadHandler.text;
            var resp = JsonUtility.FromJson<DashScopeTaskResponse>(body);
            string status = resp?.output?.task_status;

            if (logDebug) Debug.Log($"[EndingImageRequester] Poll {i + 1}/{pollCount} status={status}", this);

            if (string.Equals(status, "SUCCEEDED", StringComparison.OrdinalIgnoreCase))
            {
                string url = resp?.output?.results != null && resp.output.results.Length > 0
                    ? resp.output.results[0]?.url
                    : null;

                if (string.IsNullOrEmpty(url))
                    throw new Exception("DashScope task succeeded but no image url was returned.");

                return url;
            }

            if (string.Equals(status, "FAILED", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(status, "CANCELED", StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception("DashScope task failed: " + ExtractErrorMessage(resp, body));
            }

            if (i < pollCount - 1)
                await WaitForSecondsRealtimeAsync(Mathf.Max(1, pollIntervalSeconds));
        }

        throw new TimeoutException($"DashScope task polling timed out after {pollCount * Mathf.Max(1, pollIntervalSeconds)} seconds.");
    }

    async Task<Texture2D> DownloadTextureAsync(string url)
    {
        using var req = UnityWebRequestTexture.GetTexture(url);
        req.timeout = timeoutSeconds;

        var op = req.SendWebRequest();
        while (!op.isDone) await Task.Yield();

        if (req.result != UnityWebRequest.Result.Success)
            throw new Exception("Image download failed: " + req.error);

        return DownloadHandlerTexture.GetContent(req);
    }

    async Task WaitForSecondsRealtimeAsync(float seconds)
    {
        float endTime = Time.realtimeSinceStartup + Mathf.Max(0.1f, seconds);
        while (Time.realtimeSinceStartup < endTime)
            await Task.Yield();
    }

    static string ExtractErrorMessage(DashScopeTaskResponse resp, string rawBody)
    {
        if (!string.IsNullOrWhiteSpace(resp?.message))
            return resp.message;

        if (!string.IsNullOrWhiteSpace(resp?.code))
            return resp.code;

        return rawBody;
    }

    [Serializable]
    class DashScopeImageRequest
    {
        public string model;
        public DashScopeImageInput input;
        public DashScopeImageParameters parameters;
    }

    [Serializable]
    class DashScopeImageInput
    {
        public string prompt;
    }

    [Serializable]
    class DashScopeImageParameters
    {
        public string size;
        public int n;
    }

    [Serializable]
    class DashScopeTaskResponse
    {
        public string request_id;
        public string code;
        public string message;
        public DashScopeTaskOutput output;
    }

    [Serializable]
    class DashScopeTaskOutput
    {
        public string task_id;
        public string task_status;
        public DashScopeImageResult[] results;
    }

    [Serializable]
    class DashScopeImageResult
    {
        public string url;
    }
}
