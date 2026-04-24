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
}
