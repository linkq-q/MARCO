using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class LineSegmentUI : MonoBehaviour, IPointerClickHandler
{
    [HideInInspector] public MemoryBoardController board;
    [HideInInspector] public int stepIndex;      // 这条线对应主链第几步（从 1 开始）
    [HideInInspector] public bool isLast;        // 控制器会动态标记

    public Image img;                            // 线段 Image

    void Awake()
    {
        if (!img) img = GetComponent<Image>();
        // 线段要能被点到，RaycastTarget 必须开
        if (img) img.raycastTarget = true;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Right) return;
        if (board) board.OnLineRightClicked(this);
    }
}