using UnityEngine;

public class LorePicker : MonoBehaviour
{
    public static LorePicker Instance { get; private set; }

    [Header("Database")]
    public EventPoolData eventPool;  // 使用 EventPoolData 替代 LoreDatabase

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // 使用事件池来获取数据
    public string PickOne(ItemData item)
    {
        if (eventPool == null || item == null) return null;

        // 获取与道具相关的事件池
        var pool = eventPool.GetPool(item);
        if (pool == null || pool.entries == null || pool.entries.Count == 0) return null;

        // 简单随机，尚未使用权重
        int idx = Random.Range(0, pool.entries.Count);
        return pool.entries[idx].text;  // 获取事件文本
    }

    // 使用加权随机选择事件
    public string PickOneWeighted(ItemData item)
    {
        if (eventPool == null || item == null) return null;

        // 获取与道具相关的事件池
        var pool = eventPool.GetPool(item);
        if (pool == null || pool.entries == null || pool.entries.Count == 0) return null;

        // 计算总权重
        int total = 0;
        for (int i = 0; i < pool.entries.Count; i++)
            total += Mathf.Max(1, pool.entries[i].weight);

        // 随机抽取
        int r = Random.Range(0, total);
        for (int i = 0; i < pool.entries.Count; i++)
        {
            int w = Mathf.Max(1, pool.entries[i].weight);
            if (r < w) return pool.entries[i].text;  // 获取加权后的事件文本
            r -= w;
        }

        return pool.entries[pool.entries.Count - 1].text;
    }
}
