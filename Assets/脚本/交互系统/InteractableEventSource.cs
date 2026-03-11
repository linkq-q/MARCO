using System.Collections;
using UnityEngine;

/// <summary>
/// 场景中的可交互事件源（由 InteractableClickSystem 命中后触发）
/// - 每一轮每个物体只允许触发一次
/// - 支持：先显示单句（可多行）-> 再弹二选一
/// </summary>
public class InteractableEventSource : MonoBehaviour
{
    [Header("ID")]
    public string interactId;                 // 中文注释：必须和 EventPoolData 里的 interactableId 完全一致（大小写/空格都要一致）

    [Header("Data")]
    public ItemData relatedItem;              // 中文注释：触发后会自动入库，并把文本记录到该道具
    public InteractionTopUI topUI;            // 中文注释：顶部提示UI（单句/二选一）

    [Header("Behavior")]
    public bool debugLog = true;

    [Header("Debug / Test")]
    public bool debugUnlimitedClicks = false;   // 测试：不限点击次数


    bool hasTriggered;                        // 中文注释：本轮是否已触发

    /// <summary>
    /// 由点击系统调用：确保本轮只触发一次
    /// </summary>
    public void TriggerOnce()
    {
        if (!debugUnlimitedClicks && hasTriggered) return;

        hasTriggered = true;

        if (debugLog) Debug.Log($"[IES] TriggerOnce: {name}, interactId={interactId}");

        // 中文注释：基本依赖检查
        if (string.IsNullOrWhiteSpace(interactId))
        {
            if (debugLog) Debug.LogWarning("[IES] interactId is empty.");
            return;
        }
        if (relatedItem == null)
        {
            if (debugLog) Debug.LogWarning("[IES] relatedItem is null.");
            return;
        }
        if (InventoryManager.Instance == null)
        {
            if (debugLog) Debug.LogWarning("[IES] InventoryManager.Instance is null.");
            return;
        }
        if (EventPicker.Instance == null)
        {
            if (debugLog) Debug.LogWarning("[IES] EventPicker.Instance is null.");
            return;
        }
        if (topUI == null)
        {
            if (debugLog) Debug.LogWarning("[IES] topUI is null. Cannot show UI.");
            return;
        }

        // 中文注释：从事件池抽取一条事件
        var entry = EventPicker.Instance.PickForInteractable(interactId);
        if (entry == null)
        {
            if (debugLog) Debug.LogWarning($"[IES] No event entry found for {interactId}");
            return;
        }

        // 中文注释：支持“BinaryChoice 但同时写了 singleLine”的情况
        // 顺序：先显示 singleLine（如果有）-> 再显示 choice（如果是 BinaryChoice）
        if (!string.IsNullOrWhiteSpace(entry.singleLine))
        {
            // 先显示单句（可多行）
            topUI.ShowSingleLine(entry.singleLine);

            if (entry.type == EventType.BinaryChoice)
            {
                // 中文注释：等单句自动消失后，再弹二选一（用真实时间，不受 Time.timeScale 影响）
                StartCoroutine(ShowChoiceAfterDelay(entry, topUI.autoHideSeconds));
                return;
            }
            else
            {
                // 中文注释：仅单句：直接把整段文本记录到道具
                InventoryManager.Instance.AppendLogToItem(relatedItem, entry.singleLine);
                return;
            }
        }

        // 中文注释：没有 singleLine，直接走 type
        if (entry.type == EventType.SingleLine)
        {
            if (!string.IsNullOrWhiteSpace(entry.singleLine))
            {
                topUI.ShowSingleLine(entry.singleLine);
                InventoryManager.Instance.AppendLogToItem(relatedItem, entry.singleLine);
            }
            return;
        }

        if (entry.type == EventType.BinaryChoice)
        {
            ShowBinaryChoice(entry);
            return;
        }
    }

    IEnumerator ShowChoiceAfterDelay(EventPoolData.EventEntry entry, float seconds)
    {
        // 中文注释：避免 seconds=0 导致一闪而过，给个极小保底
        float wait = Mathf.Max(0.05f, seconds);
        yield return new WaitForSecondsRealtime(wait);
        ShowBinaryChoice(entry);
    }

    void ShowBinaryChoice(EventPoolData.EventEntry entry)
    {
        string aText = entry.optionA != null ? entry.optionA.text : "";
        string bText = entry.optionB != null ? entry.optionB.text : "";

        string aLog = entry.optionA != null && !string.IsNullOrWhiteSpace(entry.optionA.logTextOverride)
            ? entry.optionA.logTextOverride
            : aText;

        string bLog = entry.optionB != null && !string.IsNullOrWhiteSpace(entry.optionB.logTextOverride)
            ? entry.optionB.logTextOverride
            : bText;

        topUI.ShowBinaryChoice(
            aText,
            bText,
            () =>
            {
                if (debugLog) Debug.Log($"[IES] Choose A ({entry.optionA.tag})");

                if (!string.IsNullOrWhiteSpace(aLog))
                    InventoryManager.Instance.AppendLogToItem(relatedItem, aLog);

                // 中文注释：这里预留给“影响下一轮循环地块”
                // LoopStateManager.Instance.ApplyChoice(interactId, entry.optionA.stateKey, entry.optionA.tag);
            },
            () =>
            {
                if (debugLog) Debug.Log($"[IES] Choose B ({entry.optionB.tag})");

                if (!string.IsNullOrWhiteSpace(bLog))
                    InventoryManager.Instance.AppendLogToItem(relatedItem, bLog);

                // LoopStateManager.Instance.ApplyChoice(interactId, entry.optionB.stateKey, entry.optionB.tag);
            }
        );
    }

    /// <summary>
    /// 新一轮循环开始时调用，重置触发状态
    /// </summary>
    public void ResetForNewLoop()
    {
        hasTriggered = false;
    }
    /// <summary>
    /// 中文注释：给 InteractableClickSystem 调用的入口（兼容旧代码）
    /// </summary>
    public void TryTriggerFromExternal(GameObject hitObject = null)
    {
        // 中文注释：你可以在这里做额外校验（比如必须点到自己/子物体）；
        // 目前直接触发即可，因为 ClickSystem 已经完成了 Raycast 命中判定。
        TriggerOnce();
    }

}
