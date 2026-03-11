using UnityEngine;

public class StartupItems : MonoBehaviour
{
    [Header("Give these items on game start")]
    public ItemData[] items;          // 把“陈末日记”ItemData拖进来
    public bool onlyOnce = true;      // 防止重复添加（可选）

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
    }
}
