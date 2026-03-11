using TMPro;
using UnityEngine;

public class MemoryShardPrefabBinder : MonoBehaviour
{
    [Header("Bind Target")]
    public TextMeshProUGUI textTMP;

    [Tooltip("是否同时把标题也改掉（可选）")]
    public TextMeshProUGUI titleTMP;

    public void Bind(ItemData data)
    {
        if (data == null) return;

        if (titleTMP) titleTMP.text = data.displayName ?? "";

        if (textTMP)
            textTMP.text = data.memoryFixedText ?? "";
    }
}