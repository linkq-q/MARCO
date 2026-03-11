using UnityEngine;

public enum ItemKind
{
    Normal,
    Diary,
    MemoryShard   // ✅ 新增：记忆碎片
}

public enum ItemInteractionAction
{
    None,
    Inspect,
    Use,
    Unlock,
    Place,
    Combine
}

[CreateAssetMenu(menuName = "Game/Item Data", fileName = "ItemData_")]
public class ItemData : ScriptableObject
{
    [Header("Basic")]
    public string id;                 // Unique ID, e.g. "black_tower_shard"
    public string displayName;
    public Sprite icon;
    public ItemKind kind = ItemKind.Normal;

    [Header("Interaction")]
    public bool isInteractable = true;

    [Tooltip("Which object this item can interact with (optional)")]
    public GameObject interactableTarget;

    public ItemInteractionAction interactionAction = ItemInteractionAction.None;

    // =========================
    // ✅ Memory Shard Content
    // =========================
    [Header("Memory Shard (Only when kind = MemoryShard)")]
    [Tooltip("右侧提示语（替换你 InventoryUIController 里的 guessPromptText）。不填则用全局默认。")]
    [TextArea(2, 6)]
    public string memoryPromptOverride;

    [Tooltip("记忆碎片要显示的固定文本（不做追加、不刷新）。")]
    [TextArea(3, 30)]
    public string memoryFixedText;

    [Tooltip("如果你想用更复杂的排版/图片/动画，就填一个UI预制体；会实例化到记忆碎片面板 Content 下。")]
    public GameObject memoryContentPrefab;

    // ✅ 新增：是否进入“随机抽取池”
    [Header("Memory Shard - Spawn Rules")]
    [Tooltip("为 true 时：该记忆碎片不会进入随机抽取/池子抽取，只能通过剧情/连线等绑定事件发放。")]
    public bool excludeFromShardPool = false;

    // ✅ 可选：方便你做“连线绑定”的标记（不是必须）
    [Tooltip("可选：该碎片是否来自连线事件（用于调试/分类展示）。")]
    public bool isLinkedShard = false;
}