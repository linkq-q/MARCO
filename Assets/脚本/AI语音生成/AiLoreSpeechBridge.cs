using System.Text;
using UnityEngine;

public class AiLoreSpeechBridge : MonoBehaviour
{
    public DoubaoTtsHttpClient tts;

    [Header("Lore Build")]
    [Tooltip("最多取最近多少条语料来口播")]
    public int maxLines = 8;

    [Tooltip("单次口播最大字符数（避免太长导致延迟、也避免接口限制）")]
    public int maxChars = 200;

    /// <summary>
    /// 示例：从你自己的“当前生存语料”里取文本，然后转语音。
    /// 你需要把 GetCurrentRunLoreLines() 替换成你项目里的真实数据来源。
    /// </summary>
    public void SpeakCurrentLore()
    {
        if (!tts)
        {
            Debug.LogError("[LoreSpeech] tts not assigned.");
            return;
        }

        var lines = GetCurrentRunLoreLines(); // TODO：替换成你的 RunMemory/Inventory logs
        string text = BuildSpeakText(lines, maxLines, maxChars);

        tts.Speak(text);
    }

    // ===== 你要改的部分：从你项目里拿“当前生存语料” =====
    // 这里我先用假数据示例：你自行替换
    System.Collections.Generic.List<string> GetCurrentRunLoreLines()
    {
        // 例：如果你有 RunMemory.I.GetRecentFacts(30) 之类，直接把它们转 string list
        return new System.Collections.Generic.List<string>
        {
            "你刚刚进入了新的区块。",
            "地面有反常的震颤。",
            "你收集到的线索指向同一个结论：这里不是异星。",
            "下一步建议：检查背包里的那枚金属碎片。"
        };
    }

    // ===== 通用：把多条语料拼成一段易读口播 =====
    static string BuildSpeakText(System.Collections.Generic.List<string> lines, int maxLines, int maxChars)
    {
        if (lines == null || lines.Count == 0) return "没有新的语料。";

        var sb = new StringBuilder(256);

        // 只取最后 N 条
        int start = Mathf.Max(0, lines.Count - maxLines);
        for (int i = start; i < lines.Count; i++)
        {
            var s = lines[i];
            if (string.IsNullOrWhiteSpace(s)) continue;

            // 简单清理换行
            s = s.Replace("\n", " ").Replace("\r", " ").Trim();

            // 用“、”或“。”组织，让 TTS 更自然
            if (sb.Length > 0) sb.Append("。");
            sb.Append(s);

            if (sb.Length >= maxChars) break;
        }

        // 保底句号
        if (sb.Length == 0) sb.Append("没有新的语料。");

        // 截断到 maxChars
        if (sb.Length > maxChars) sb.Length = maxChars;

        return sb.ToString();
    }
}
