using System;
using System.Collections.Generic;
using UnityEngine;

public enum Stage2EmotionType { Fear, Exist, Envy }

[Serializable]
public class Stage2RealityRuntime
{
    [Header("S4 insert rule")]
    public int progressCount = 0;     // S1/S2/S3 推进成功计数
    public int resumeSub = 1;         // 插入S4后回到哪个子状态（1..3）

    [Header("S3 keyword")]
    public List<string> keywords = new List<string> { "早高峰", "迟到罚款", "生日", "结婚", "毕业证" };
    public int keywordIndex = 0;
    public string CurrentKeyword => (keywords != null && keywords.Count > 0)
        ? keywords[Mathf.Clamp(keywordIndex, 0, keywords.Count - 1)]
        : "早高峰";

    [Header("S4 emotion")]
    public Stage2EmotionType currentEmotion = Stage2EmotionType.Exist;

    public void ResetAll()
    {
        progressCount = 0;
        resumeSub = 1;
        keywordIndex = 0;
        currentEmotion = Stage2EmotionType.Exist;
    }

    public void StepKeyword()
    {
        if (keywords == null || keywords.Count == 0) return;
        keywordIndex = (keywordIndex + 1) % keywords.Count; // 先循环版；你要“不放回”可后面再换成洗牌池
    }

    public void PickEmotion()
    {
        // 30% fear, 40% exist, 30% envy
        int r = UnityEngine.Random.Range(0, 100);
        if (r < 30) currentEmotion = Stage2EmotionType.Fear;
        else if (r < 70) currentEmotion = Stage2EmotionType.Exist;
        else currentEmotion = Stage2EmotionType.Envy;
    }

    public string EmotionLineHint()
    {
        return currentEmotion switch
        {
            Stage2EmotionType.Fear => "（情绪：恐惧被回收，只说一句就拉回任务）",
            Stage2EmotionType.Exist => "（情绪：渴望存在，只说一句就拉回任务）",
            _ => "（情绪：嫉妒真实生命，只说一句就拉回任务）",
        };
    }
}