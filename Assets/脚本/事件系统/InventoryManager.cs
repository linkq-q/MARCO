using System;
using System.Collections.Generic;
using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    [Header("Lore Unlock")]
    public ItemLoreUnlockDatabase loreUnlockDatabase;

    [Serializable]
    public class ItemRuntime
    {
        public ItemData data;                       // 道具静态数据（ScriptableObject）

        // ✅ 原 takenOut（拿出/装备）改为“推测填空”
        public bool guessEnabled;                   // 是否开启“推测填空”
        public string guessAnswer;                  // 玩家填的答案（可为空）
        public string guessFeedback;                // Echo 对这次推测的即时回应

        public List<string> logs = new List<string>(); // 该道具的语料/记录
    }

    // 已拥有道具（运行时列表）
    readonly List<ItemRuntime> owned = new List<ItemRuntime>();
    public IReadOnlyList<ItemRuntime> Owned => owned;

    // 事件：道具列表发生变化（新增道具/推测状态改变/新增语料等）
    public event Action OnInventoryChanged;

    // 事件：当前选中道具变化（或需要刷新右侧详情）
    public event Action<ItemRuntime> OnSelectedChanged;

    // 当前选中
    ItemRuntime selected;
    public ItemRuntime Selected => selected;

    readonly List<LoreRef> unlockedLoreRefs = new List<LoreRef>();
    readonly HashSet<string> unlockedLoreIds = new HashSet<string>();

    readonly List<LoreRef> _injectableLoreRefs = new List<LoreRef>();
    readonly HashSet<string> _injectableLoreIds = new HashSet<string>();

    [Header("Debug（可选）")]
    public bool debugLog = false;

    // ✅ 填空展示格式（你要求的固定文案）
    public const string GuessTemplate = "经过推测你认为这是_______";

    void Awake()
    {
        // 单例
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// 判断是否已经拥有某个道具（按 ItemData.id）
    /// </summary>
    public bool HasItem(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        return owned.Exists(x => x.data != null && x.data.id == id);
    }

    /// <summary>
    /// 新增道具（如果已有则忽略）。新增后默认选中该道具，并触发库存刷新事件。
    /// </summary>
    public void AddItem(ItemData data)
    {
        if (data == null) return;
        if (string.IsNullOrWhiteSpace(data.id)) return;

        // 已有就不重复入库
        if (HasItem(data.id)) return;

        // ✅ 原 takenOut=false 改为 guessEnabled=false（默认不打开填空）
        var rt = new ItemRuntime { data = data, guessEnabled = false, guessAnswer = "", guessFeedback = "" };
        owned.Add(rt);

        if (debugLog) Debug.Log($"[Inventory] AddItem: {data.id}");
        UIHintManager.I?.NotifyFirstHighlightEntered();

        UnlockLoreForItem(data);

        // 入库后默认选中新道具
        SelectItem(rt);


        // 通知 UI 重建/刷新左侧列表
        OnInventoryChanged?.Invoke();
    }

    /// <summary>
    /// 设置当前选中道具（允许重复选择同一项，用于强制刷新右侧 UI）
    /// </summary>
    public void SelectItem(ItemRuntime rt)
    {
        selected = rt;

        if (debugLog) Debug.Log($"[Inventory] SelectItem: {rt?.data?.id}");

        OnSelectedChanged?.Invoke(selected);
    }

    /// <summary>
    /// ✅ 原“拿出来/装备中”改为“推测填空开关”
    /// - value=true：开启该道具的“经过推测你认为这是_______”填空
    /// - value=false：关闭该道具填空
    ///
    /// 说明：
    /// - 保留方法名 SetTakenOut 以避免你其他脚本引用报错
    /// - 不再做“同一时刻只能拿出一个道具”的约束（因为现在不是装备逻辑）
    /// - 不再同步 ActiveItemController（拿出表现）——属于原拿出按钮功能
    /// </summary>
    public void SetTakenOut(bool value)
    {
        if (selected == null || selected.data == null) return;

        // 如果该道具不允许交互/推测，则强制为 false
        if (!selected.data.isInteractable) value = false;

        selected.guessEnabled = value;

        if (debugLog) Debug.Log($"[Inventory] SetGuessEnabled: {selected.data.id} -> {value}");

        // 状态改变，左侧/右侧都需要刷新
        OnInventoryChanged?.Invoke();
        OnSelectedChanged?.Invoke(selected);
    }

    /// <summary>
    /// ✅ 设置当前选中道具的“填空答案”（玩家输入的内容）
    /// 不改变其他逻辑，只负责写入并触发刷新事件。
    /// </summary>
    public void SetGuessAnswer(string answer)
    {
        if (selected == null || selected.data == null) return;

        selected.guessAnswer = (answer ?? "").Trim();

        if (debugLog) Debug.Log($"[Inventory] SetGuessAnswer: {selected.data.id} -> {selected.guessAnswer}");

        OnInventoryChanged?.Invoke();
        OnSelectedChanged?.Invoke(selected);
    }

    /// <summary>
    /// ✅ 获取当前选中道具的“推测填空展示文本”
    /// - 未填：经过推测你认为这是_______
    /// - 已填：经过推测你认为这是<答案>
    /// </summary>
    public string GetGuessPromptForSelected()
    {
        if (selected == null) return GuessTemplate;
        string ans = selected.guessAnswer;
        if (string.IsNullOrWhiteSpace(ans)) return GuessTemplate;
        return "经过推测你认为这是" + ans;
    }

    /// <summary>
    /// 给“当前选中道具”追加一条语料
    /// </summary>
    public void AppendLogToSelected(string line)
    {
        if (selected == null) return;
        AppendLogToItem(selected.data, line);
    }

    /// <summary>
    /// 给指定道具追加语料：
    /// - 如果玩家还没拥有该道具，会先自动入库
    /// - 追加后触发库存刷新
    /// - 如果该道具正好是当前选中道具，则额外触发右侧刷新
    /// </summary>
    public void AppendLogToItem(ItemData item, string line)
    {
        if (item == null) return;
        if (string.IsNullOrWhiteSpace(item.id)) return;
        if (string.IsNullOrWhiteSpace(line)) return;

        var rt = owned.Find(x => x.data != null && x.data.id == item.id);
        if (rt == null)
        {
            AddItem(item);
            rt = owned.Find(x => x.data != null && x.data.id == item.id);
            if (rt == null) return;
        }

        rt.logs.Add(line);

        // ✅ 这里弹“拾取语料”Toast（每次拾取语料都会弹）
        ToastSpawner.Instance?.Show($"获得新信息：{item.displayName}");

        if (debugLog) Debug.Log($"[Inventory] AppendLog: {item.id} +1 (total {rt.logs.Count})");

        OnInventoryChanged?.Invoke();
        if (selected != null && selected.data != null && selected.data.id == item.id)
            OnSelectedChanged?.Invoke(selected);
    }

    /// <summary>
    /// ✅ 原“清空所有道具的 takenOut 状态”改为“清空所有道具的推测填空开关”
    /// （不清空答案，只关闭填写状态；更符合原来“收起/退出背包时取消拿出”的语义）
    /// </summary>
    public void ClearTakenOutAll()
    {
        for (int i = 0; i < owned.Count; i++)
            owned[i].guessEnabled = false;

        if (debugLog) Debug.Log("[Inventory] ClearGuessEnabledAll");

        OnInventoryChanged?.Invoke();
        OnSelectedChanged?.Invoke(selected);
    }

    public IReadOnlyList<LoreRef> GetUnlockedLoreRefs()
    {
        return unlockedLoreRefs;
    }

    public IReadOnlyList<LoreRef> GetInjectableLoreRefs()
    {
        return _injectableLoreRefs;
    }

    void UnlockLoreForItem(ItemData data)
    {
        if (data == null) return;

        bool shouldInject = loreUnlockDatabase == null || loreUnlockDatabase.ShouldInjectToEcho(data);

        bool addedAny = false;
        var refs = loreUnlockDatabase != null ? loreUnlockDatabase.GetLoreRefs(data) : null;
        if (refs != null)
        {
            for (int i = 0; i < refs.Count; i++)
            {
                if (AddUnlockedLore(refs[i]))
                {
                    addedAny = true;
                    if (shouldInject)
                        AddInjectableLore(unlockedLoreRefs[unlockedLoreRefs.Count - 1]);
                }
            }
        }

        if (!addedAny)
        {
            var fallback = BuildFallbackLoreRef(data);
            if (AddUnlockedLore(fallback) && shouldInject)
                AddInjectableLore(unlockedLoreRefs[unlockedLoreRefs.Count - 1]);
        }
    }

    bool AddInjectableLore(LoreRef lore)
    {
        if (string.IsNullOrWhiteSpace(lore.id)) return false;
        if (_injectableLoreIds.Contains(lore.id)) return false;
        _injectableLoreIds.Add(lore.id);
        _injectableLoreRefs.Add(lore);
        return true;
    }

    bool AddUnlockedLore(LoreRef lore)
    {
        string id = string.IsNullOrWhiteSpace(lore.id) ? Guid.NewGuid().ToString("N") : lore.id;
        if (unlockedLoreIds.Contains(id)) return false;

        lore.id = id;
        unlockedLoreIds.Add(id);
        unlockedLoreRefs.Add(lore);
        return true;
    }

    LoreRef BuildFallbackLoreRef(ItemData data)
    {
        string summary = null;

        if (!string.IsNullOrWhiteSpace(data.memoryFixedText))
            summary = data.memoryFixedText;
        else if (!string.IsNullOrWhiteSpace(data.memoryPromptOverride))
            summary = data.memoryPromptOverride;
        else
            summary = data.displayName;

        if (!string.IsNullOrWhiteSpace(summary) && summary.Length > 80)
            summary = summary.Substring(0, 80);

        return new LoreRef
        {
            id = $"item_{data.id}",
            title = data.displayName,
            shortText = summary,
            rawText = summary
        };
    }
}
