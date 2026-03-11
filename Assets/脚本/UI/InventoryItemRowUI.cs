using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// 背包左侧列表条目 UI
/// - 不使用 Button
/// - 不处理选中高亮
/// - 只在 PointerDown 时切换当前道具
/// - 兼容 ScrollRect + 虚拟鼠标
/// </summary>
public class InventoryItemRowUI : MonoBehaviour, IPointerDownHandler
{
    [Header("UI")]
    public Image icon;
    public TextMeshProUGUI label;

    InventoryManager.ItemRuntime bound;

    /// <summary>
    /// 由 InventoryUIController 调用，绑定这一行代表的道具数据
    /// </summary>
    public void Bind(InventoryManager.ItemRuntime rt)
    {
        bound = rt;

        if (rt != null && rt.data != null)
        {
            icon.sprite = rt.data.icon;
            label.text = rt.data.displayName;
        }
        else
        {
            icon.sprite = null;
            label.text = "(null)";
        }
    }

    /// <summary>
    /// 点击列表项本身即切换选中道具
    /// （使用 PointerDown，避免被 ScrollRect 判定为 Drag）
    /// </summary>
    public void OnPointerDown(PointerEventData eventData)
    {
        if (bound == null) return;
        if (InventoryManager.Instance == null) return;

        InventoryManager.Instance.SelectItem(bound);
    }

    public void OnClickSelect()
    {
        if (bound == null) return;
        InventoryManager.Instance.SelectItem(bound);
    }

}
