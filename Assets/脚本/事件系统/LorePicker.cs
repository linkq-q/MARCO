using System.Collections.Generic;
using UnityEngine;

public class LorePicker : MonoBehaviour
{
    public static LorePicker Instance { get; private set; }

    [Header("Database")]
    public EventPoolData eventPool;  // ʹ�� EventPoolData ��� LoreDatabase

    readonly Dictionary<ItemData, List<int>> _usedIndices = new Dictionary<ItemData, List<int>>();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // 不放回抽取：每条语料用完一轮才会重置
    public string PickOne(ItemData item)
    {
        if (eventPool == null || item == null) return null;

        var pool = eventPool.GetPool(item);
        if (pool == null || pool.entries == null || pool.entries.Count == 0) return null;

        if (!_usedIndices.TryGetValue(item, out var used))
        {
            used = new List<int>();
            _usedIndices[item] = used;
        }

        var available = new List<int>(pool.entries.Count);
        for (int i = 0; i < pool.entries.Count; i++)
            if (!used.Contains(i)) available.Add(i);

        if (available.Count == 0)
        {
            used.Clear();
            for (int i = 0; i < pool.entries.Count; i++) available.Add(i);
        }

        int idx = available[Random.Range(0, available.Count)];
        used.Add(idx);
        return pool.entries[idx].text;
    }

    // ʹ�ü�Ȩ���ѡ���¼�
    public string PickOneWeighted(ItemData item)
    {
        if (eventPool == null || item == null) return null;

        // ��ȡ�������ص��¼���
        var pool = eventPool.GetPool(item);
        if (pool == null || pool.entries == null || pool.entries.Count == 0) return null;

        // ������Ȩ��
        int total = 0;
        for (int i = 0; i < pool.entries.Count; i++)
            total += Mathf.Max(1, pool.entries[i].weight);

        // �����ȡ
        int r = Random.Range(0, total);
        for (int i = 0; i < pool.entries.Count; i++)
        {
            int w = Mathf.Max(1, pool.entries[i].weight);
            if (r < w) return pool.entries[i].text;  // ��ȡ��Ȩ����¼��ı�
            r -= w;
        }

        return pool.entries[pool.entries.Count - 1].text;
    }
}
