using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Lore Pool Data", fileName = "LorePool_")]
public class LorePoolData : ScriptableObject
{
    [Header("Binding")]
    public ItemData item; // which item this pool belongs to

    [Serializable]
    public class LoreEntry
    {
        [TextArea(2, 6)]
        public string text;

        [Range(1, 100)]
        public int weight = 10; // optional: rarity / frequency
    }

    [Header("Entries")]
    public List<LoreEntry> entries = new List<LoreEntry>();

    // =========================
    // Runtime API
    // =========================

    /// <summary>
    /// 从语料池中按权重随机取一条文本
    /// 注意：这是“抽取语料”，不负责“本轮只触发一次”的限制（那个在交互脚本里做）
    /// </summary>
    public string PickOne()
    {
        if (entries == null || entries.Count == 0) return null;

        int totalWeight = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            int w = Mathf.Max(0, entries[i].weight);
            totalWeight += w;
        }

        // 中文注释：所有权重都为0时，退化成等概率/取第一条
        if (totalWeight <= 0)
        {
            return entries[0].text;
        }

        int roll = UnityEngine.Random.Range(0, totalWeight);
        for (int i = 0; i < entries.Count; i++)
        {
            roll -= Mathf.Max(0, entries[i].weight);
            if (roll < 0)
                return entries[i].text;
        }

        // 理论不会走到这里，兜底
        return entries[entries.Count - 1].text;
    }
}
