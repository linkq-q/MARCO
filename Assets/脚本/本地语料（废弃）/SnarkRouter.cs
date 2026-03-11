using System.Collections.Generic;
using UnityEngine;

public enum SnarkType
{
    Generic,
    Pickup,
    Inventory,
    Idle,
    Event
}

public class SnarkRouter : MonoBehaviour
{
    public static SnarkRouter I { get; private set; }

    [Header("Data")]
    public LocalSnarkBank bank;

    [Header("Debug")]
    public bool logToConsole = true;

    [Header("AI (Optional Cloud)")]
    public bool enableCloud = true;
    public CloudResponder cloudResponder;   // 云端
    LocalResponder localResponder;           // 本地适配器
    SmartResponder smartResponder;           // 云端优先 + fallback

    [Header("Startup Silence")]
    public float startupMuteSeconds = 1.0f;
    bool armed = false;
    float enableAfter;

    [Header("UI")]
    public AIChatUI chatUI; // 底部黑底聊天（现在不用于 Pickup/Inventory）

    [Header("Toast (Right Hint)")]
    [Tooltip("拾取时只显示右侧提示，不弹底部黑底")]
    public bool pickupUseToastOnly = true;

    [Tooltip("拾取提示文案（固定）")]
    public string pickupToastText = "你捡到了新物品！";

    void Awake()
    {
        enableAfter = Time.unscaledTime + 1.0f;
        armed = false;

        if (I != null) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);

        localResponder = new LocalResponder(bank);

        if (enableCloud && cloudResponder != null)
            smartResponder = new SmartResponder(cloudResponder, localResponder, log: true);
        else
            smartResponder = new SmartResponder(null, localResponder);
    }

    /// <summary>
    /// 触发一句
    /// </summary>
    public async void Say(SnarkType type)
    {
        // ✅ 1) 开背包残留：彻底禁用
        if (type == SnarkType.Inventory)
        {
            if (logToConsole) Debug.Log("[SnarkRouter] Inventory snark ignored.");
            return;
        }

        // ✅ 2) 只保留“捡到新物品”右侧提示（不走 AI，不弹黑底）
        if (type == SnarkType.Pickup && pickupUseToastOnly)
        {
            if (ToastSpawner.Instance != null)
            {
                ToastSpawner.Instance.Show(pickupToastText);
                if (logToConsole) Debug.Log("[SnarkRouter] Pickup toast shown.");
            }
            else
            {
                Debug.LogError("[SnarkRouter] ToastSpawner.Instance is null（场景里没激活ToastSpawner）");
            }
            return;
        }

        // === 以下保持你原来逻辑（如果你还想要 Idle/Event/Generic 继续说话） ===

        if (!armed)
        {
            if (Time.unscaledTime < enableAfter) return;
        }

        if (!bank)
        {
            Debug.LogWarning("[SnarkRouter] bank is null");
            return;
        }

        var ctx = new AIContext
        {
            sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
            playerAction = type.ToString(),
            loreRefs = CollectLoreRefs(type)
        };

        string line = await smartResponder.GenerateAsync(ctx);
        if (string.IsNullOrEmpty(line)) return;

        // ⚠️ 这里会弹黑底：仅用于非 Pickup/Inventory 的类型
        if (chatUI) chatUI.ShowLine(line);

        if (logToConsole)
            Debug.Log($"[SNARK/{type}] {line}");
    }

    List<string> GetList(SnarkType type)
    {
        return type switch
        {
            SnarkType.Pickup => bank.pickupSnark,
            SnarkType.Inventory => bank.inventorySnark,
            SnarkType.Idle => bank.idleSnark,
            SnarkType.Event => bank.eventSnark,
            _ => bank.genericSnark,
        };
    }

    List<LoreRef> CollectLoreRefs(SnarkType type)
    {
        var list = GetList(type);
        var lore = new List<LoreRef>();

        for (int i = 0; i < Mathf.Min(3, list.Count); i++)
        {
            lore.Add(new LoreRef
            {
                id = $"{type}_{i}",
                title = type.ToString(),
                shortText = list[i]
            });
        }
        return lore;
    }

    public void Arm()
    {
        armed = true;
    }
}
