using System.Collections.Generic;
using UnityEngine;

public class EventPicker : MonoBehaviour
{
    public static EventPicker Instance { get; private set; }

    public EventPoolData pool;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // 根据景物 ID 选择事件并记录语料
    public EventPoolData.EventEntry PickForInteractable(string interactableId)
    {
        if (pool == null)
        {
            Debug.LogError("EventPoolData is not assigned.");
            return null;
        }

        if (pool.entries == null || pool.entries.Count == 0)
        {
            Debug.LogError("Event entries are empty.");
            return null;
        }

        if (string.IsNullOrWhiteSpace(interactableId))
        {
            Debug.LogError("InteractableId is null or empty.");
            return null;
        }

        Debug.Log($"Selecting event for interactable: {interactableId}");

        // 过滤出所有匹配该景物ID的事件
        List<EventPoolData.EventEntry> candidates = new List<EventPoolData.EventEntry>();
        for (int i = 0; i < pool.entries.Count; i++)
        {
            var e = pool.entries[i];

            if (e == null)
            {
                Debug.LogWarning($"EventEntry {i} is null.");
                continue;  // 跳过 null 的条目
            }

            Debug.Log($"Checking event: ID={e.loreId}, InteractableId={e.interactableId}");

            if (e.interactableId == interactableId)
            {
                candidates.Add(e);
            }
        }

        if (candidates.Count == 0)
        {
            Debug.LogError($"No events found for interactable ID: {interactableId}");
            return null;
        }

        // 按权重随机选择事件
        int total = 0;
        for (int i = 0; i < candidates.Count; i++)
        {
            if (candidates[i] == null) continue;  // 跳过 null 的项
            total += Mathf.Max(1, candidates[i].weight);
        }

        int r = Random.Range(0, total);
        for (int i = 0; i < candidates.Count; i++)
        {
            if (candidates[i] == null) continue;  // 跳过 null 的项
            int w = Mathf.Max(1, candidates[i].weight);
            if (r < w)
            {
                Debug.Log($"Selected event: {candidates[i].text}");

                bool valid;
                string recordText = EventLoreUtil.GetRecordText(candidates[i], true, out valid);
                if (valid)
                {
                    // 确保 PlayerLoreState 和 AIBroker 被正确初始化
                    if (PlayerLoreState.Instance == null)
                    {
                        Debug.LogError("PlayerLoreState.Instance is null.");
                        return null;
                    }
                    if (AIBroker.Instance == null)
                    {
                        Debug.LogError("AIBroker.Instance is null.");
                        return null;
                    }

                    PlayerLoreState.Instance.AddLore(candidates[i].loreId, recordText);
                    SnarkRouter.I?.Say(SnarkType.Pickup);
                    //AIBroker.Instance.OnLorePicked(candidates[i].loreId, recordText); // 调用 AIBroker
                }

                return candidates[i];
            }
            r -= w;
        }

        return candidates[candidates.Count - 1];
    }







}
