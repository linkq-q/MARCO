using TMPro;
using UnityEngine;

public class DiaryEntryItemUI : MonoBehaviour
{
    [Header("UI")]
    public TextMeshProUGUI typeText;   // 顶部：DAY/类型标签（可选）
    public TextMeshProUGUI bodyText;   // 正文

    /// <summary>
    /// bind 并显示（不使用打字机，直接显示全文）
    /// </summary>
    public void Bind(DiaryEntry e)
    {
        if (e == null) return;

        // 标签
        if (typeText != null)
        {
            if (e.kind == DiaryEntryKind.Log)
                typeText.text = $"DAY {e.dayIndex}";
            else
                typeText.text = $"";
        }

        // 正文
        if (bodyText != null)
            bodyText.text = e.text ?? "";
    }

    static string ToCn(GuideType t)
    {
        switch (t)
        {
            case GuideType.AccidentRecord: return "事故记录";
            case GuideType.FamilyVoice: return "亲人声音";
            case GuideType.RealityMemory: return "现实记忆";
            case GuideType.LoopHint: return "循环提示";
            case GuideType.EchoPrevLine: return "Echo前轮台词";
            default: return t.ToString();
        }
    }
}
