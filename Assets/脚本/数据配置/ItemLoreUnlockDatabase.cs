using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Item Lore Unlock Database", fileName = "ItemLoreUnlockDatabase_")]
public class ItemLoreUnlockDatabase : ScriptableObject
{
    [Serializable]
    public class ItemLoreEntry
    {
        public ItemData item;
        public List<LoreRef> loreRefs = new List<LoreRef>();
        [Tooltip("是否将该道具的语料注入给Echo。日记/便签/记忆碎片类型设为false。")]
        public bool injectToEcho = true;
    }

    public List<ItemLoreEntry> entries = new List<ItemLoreEntry>();

    public List<LoreRef> GetLoreRefs(ItemData item)
    {
        var result = new List<LoreRef>();
        if (item == null || entries == null) return result;

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry == null || entry.item != item || entry.loreRefs == null) continue;

            for (int j = 0; j < entry.loreRefs.Count; j++)
                result.Add(entry.loreRefs[j]);
        }

        return result;
    }

    public bool ShouldInjectToEcho(ItemData item)
    {
        if (item == null || entries == null) return true;
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry == null || entry.item != item) continue;
            return entry.injectToEcho;
        }
        return true;
    }
}
