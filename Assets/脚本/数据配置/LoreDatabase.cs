using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Lore Database", fileName = "LoreDatabase_")]
public class LoreDatabase : ScriptableObject
{
    public List<LorePoolData> pools = new List<LorePoolData>();

    public LorePoolData GetPool(ItemData item)
    {
        if (item == null) return null;

        for (int i = 0; i < pools.Count; i++)
        {
            var p = pools[i];
            if (p != null && p.item == item)
                return p;
        }
        return null;
    }
}
