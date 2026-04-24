using UnityEngine;

public class StartupItems : MonoBehaviour
{
    [Header("Give these items on game start")]
    public ItemData[] items;          // 把“陈末日记”ItemData拖进来
    public bool onlyOnce = true;      // 防止重复添加（可选）

    [Header("Built-in Story Note")]
    public bool addLoop1750Note = true;
    public string loop1750NoteItemId = "note_loop_1750";
    public string loop1750NoteDisplayName = "第1750次的话";
    [TextArea(1, 3)]
    public string loop1750NotePrompt = "第1750次循环留下的手记";
    public Sprite loop1750NoteIcon;

    ItemData _loop1750RuntimeItem;

    const string Loop1750NoteBody =
@"第1750次
我不想醒了。
你不要听Echo的。她说的每一句话都是为了把你推出去，推回那个地方。她说她在帮你，但帮你的代价是什么她不会告诉你。
外面有什么？我记得的。妈妈在电话里的声音、医院走廊的气味、爸爸不说话只是坐着——那些我都记得。我宁愿不记得。
这里的草是软的。早晨有光。没有人在等你，没有账单，没有对不起。
Echo会告诉你要勇敢。但她不是你，她不用承受醒来之后的事。
如果你看到这张纸，求你了。
别醒。";

    void Start()
    {
        if (InventoryManager.Instance == null)
        {
            Debug.LogWarning("[StartupItems] InventoryManager not found.");
            return;
        }

        foreach (var it in items)
        {
            if (it == null) continue;

            // ✅ 防重复：背包里已有就不再加
            if (onlyOnce && InventoryManager.Instance.HasItem(it.id))
                continue;

            InventoryManager.Instance.AddItem(it);
        }

        if (addLoop1750Note)
        {
            var noteItem = BuildLoop1750NoteItem();
            if (!onlyOnce || !InventoryManager.Instance.HasItem(noteItem.id))
                InventoryManager.Instance.AddItem(noteItem);
        }
    }

    ItemData BuildLoop1750NoteItem()
    {
        if (_loop1750RuntimeItem != null)
            return _loop1750RuntimeItem;

        _loop1750RuntimeItem = ScriptableObject.CreateInstance<ItemData>();
        _loop1750RuntimeItem.hideFlags = HideFlags.DontSave;
        _loop1750RuntimeItem.id = loop1750NoteItemId;
        _loop1750RuntimeItem.displayName = loop1750NoteDisplayName;
        _loop1750RuntimeItem.icon = loop1750NoteIcon;
        _loop1750RuntimeItem.kind = ItemKind.MemoryShard;
        _loop1750RuntimeItem.isInteractable = false;
        _loop1750RuntimeItem.memoryPromptOverride = loop1750NotePrompt;
        _loop1750RuntimeItem.memoryFixedText = Loop1750NoteBody;
        _loop1750RuntimeItem.excludeFromShardPool = true;

        return _loop1750RuntimeItem;
    }
}
