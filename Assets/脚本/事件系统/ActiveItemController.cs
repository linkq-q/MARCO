using System;
using UnityEngine;

public class ActiveItemController : MonoBehaviour
{
    public static ActiveItemController Instance { get; private set; }

    public ItemData ActiveItem { get; private set; }

    public event Action<ItemData> OnActiveItemChanged;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void SetActive(ItemData data)
    {
        if (data == null)
        {
            Clear();
            return;
        }

        if (ActiveItem == data) return;

        ActiveItem = data;
        OnActiveItemChanged?.Invoke(ActiveItem);
    }

    public void Clear()
    {
        if (ActiveItem == null) return;

        ActiveItem = null;
        OnActiveItemChanged?.Invoke(null);
    }
}
