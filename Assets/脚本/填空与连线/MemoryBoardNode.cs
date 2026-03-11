// MemoryBoardNode.cs
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class MemoryBoardNode : MonoBehaviour, IPointerClickHandler
{
    [Header("Refs")]
    public Image icon;                    // 节点图（可为空：则尝试用自己身上的 Image）
    public TextMeshProUGUI labelTMP;      // ✅ 节点文字（可为空：会自动找子物体 TMP）
    public MemoryBoardController board;   // 侦探板控制器（必须）

    [Header("Visual")]
    public Color normalColor = Color.white;
    public Color confirmedColor = new Color(0.85f, 0.95f, 1f, 1f);
    public Color errorColor = Color.red;

    [HideInInspector] public bool isConfirmed;

    void Awake()
    {
        if (!icon) icon = GetComponent<Image>();
        if (!labelTMP) labelTMP = GetComponentInChildren<TextMeshProUGUI>(true);
        SetNormal();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (board) board.OnNodeClicked(this);
    }

    public void SetLabel(string s)
    {
        if (!labelTMP) labelTMP = GetComponentInChildren<TextMeshProUGUI>(true);
        if (labelTMP) labelTMP.text = s ?? "";
    }

    public void SetNormal()
    {
        if (!icon) return;
        icon.color = normalColor;
    }

    public void SetConfirmed()
    {
        isConfirmed = true;
        if (!icon) return;
        icon.color = confirmedColor;
    }

    public void SetUnconfirmed()
    {
        isConfirmed = false;
        SetNormal();
    }

    public void FlashError(float t)
    {
        if (!gameObject.activeInHierarchy) return;
        StopAllCoroutines();
        StartCoroutine(CoFlash(t));
    }

    System.Collections.IEnumerator CoFlash(float t)
    {
        if (icon) icon.color = errorColor;
        yield return new WaitForSeconds(t);
        if (isConfirmed) SetConfirmed();
        else SetNormal();
    }
}