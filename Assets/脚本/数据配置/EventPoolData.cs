using System;
using System.Collections.Generic;
using UnityEngine;

public enum RealityTag
{
    None,
    Truth,
    Illusion
}

public enum EventType
{
    SingleLine,
    BinaryChoice
}

[CreateAssetMenu(menuName = "Game/Event Pool Data", fileName = "EventPool_")]
public class EventPoolData : ScriptableObject
{
    [Serializable]
    public class ChoiceOption
    {
        [TextArea(2, 6)] public string text;

        // 二选一的标签：真相/幻觉
        public RealityTag tag = RealityTag.None;

        // 写入循环状态的key（用于影响下一轮）
        public string stateKey;

        // 你可以用这个做“真相倾向”累计（可选）
        public int deltaTruthScore = 0;

        // 记录到道具栏的文本（可选：不填则用 text）
        [TextArea(2, 6)] public string logTextOverride;
    }

    [Serializable]
    public class EventEntry
    {
        [Header("ID")]
        public string loreId;           // 例如 "E_blacktower_001"
        public bool recordAsLore = true; // 这条是否会被写进“已获取语料”

        [Header("Where")]
        public string interactableId; // 绑定到某个具体景物ID（建议必填）

        [Header("What")]
        public EventType type = EventType.SingleLine;

        [Range(1, 100)]
        public int weight = 10;

        [Header("Single Line")]
        [TextArea(2, 6)] public string singleLine;

        [Header("Binary Choice")]
        public ChoiceOption optionA;
        public ChoiceOption optionB;

        // 事件文本（用于显示）
        public string text; // 确保 EventEntry 中有这个字段
    }

    public List<EventEntry> entries = new List<EventEntry>();

    public EventPoolData GetPool(ItemData item)
    {
        // 在此处根据需要实现获取对应的事件池的逻辑，假设我们通过 ItemData 来查找相关的事件池
        return this;  // 返回当前事件池（示例，实际根据需要返回相应的池）
    }
}
