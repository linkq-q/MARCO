using UnityEngine;
using System.Collections.Generic;

public class PlayerLoreState : MonoBehaviour
{
    public static PlayerLoreState Instance { get; private set; }

    [SerializeField] List<LoreRef> acquired = new(); // 存储玩家已获得的所有语料
    HashSet<string> acquiredIds = new(); // 用于去重，确保不会重复记录

    void Awake()
    {
        // 确保唯一实例
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 初始化 acquiredIds（确保一致性）
        foreach (var lore in acquired) acquiredIds.Add(lore.id);
    }

    // 添加语料
    public bool AddLore(string id, string text)
    {
        if (string.IsNullOrEmpty(id)) return false;
        if (acquiredIds.Contains(id)) return false;

        acquiredIds.Add(id);
        acquired.Add(new LoreRef { id = id, text = text });
        return true;
    }

    // 获取所有语料
    public IReadOnlyList<LoreRef> GetAllLore() => acquired;
}
