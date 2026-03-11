using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class VirtualCursorUIInput : MonoBehaviour
{
    [Header("Virtual Cursor")]
    public RectTransform virtualCursor;

    [Header("Enable")]
    public bool enableUIInput = true;

    [Header("Buttons")]
    public KeyCode clickKey = KeyCode.Mouse0;

    [Header("Scroll")]
    public float scrollSpeed = 1200f;
    public KeyCode scrollUpKey = KeyCode.PageUp;
    public KeyCode scrollDownKey = KeyCode.PageDown;

    [Header("Drag")]
    public float dragThreshold = 8f;

    PointerEventData _ped;
    readonly List<RaycastResult> _hits = new List<RaycastResult>();

    GameObject _currentOver;
    GameObject _pressGO;
    GameObject _dragGO;

    Vector2 _pressPos;
    bool _dragging;

    Vector2 _lastScreenPos;
    bool _hasLast;

    void Awake()
    {
        if (EventSystem.current == null)
            Debug.LogError("[VirtualCursorUIInput] 场景里没有 EventSystem！");

        _ped = new PointerEventData(EventSystem.current);
        _ped.button = PointerEventData.InputButton.Left;
    }

    void Update()
    {
        if (!enableUIInput) return;
        if (EventSystem.current == null) return;
        if (virtualCursor == null) return;

        // ✅ 关键：把虚拟鼠标 RectTransform 的世界坐标转换成“屏幕坐标像素”
        Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(null, virtualCursor.position);

        _ped.Reset();
        _ped.position = screenPos;
        _ped.delta = _hasLast ? (screenPos - _lastScreenPos) : Vector2.zero;
        _lastScreenPos = screenPos;
        _hasLast = true;

        _hits.Clear();
        EventSystem.current.RaycastAll(_ped, _hits);

        GameObject topGO = _hits.Count > 0 ? _hits[0].gameObject : null;
        _ped.pointerCurrentRaycast = _hits.Count > 0 ? _hits[0] : default;

        HandleHover(topGO);
        HandleScroll(topGO);
        HandleClickAndDrag(topGO);
    }

    void HandleHover(GameObject topGO)
    {
        if (_currentOver == topGO) return;

        if (_currentOver != null)
            ExecuteEvents.Execute(_currentOver, _ped, ExecuteEvents.pointerExitHandler);

        _currentOver = topGO;

        if (_currentOver != null)
            ExecuteEvents.Execute(_currentOver, _ped, ExecuteEvents.pointerEnterHandler);
    }

    void HandleScroll(GameObject topGO)
    {
        if (topGO == null) return;

        Vector2 scrollDelta = Vector2.zero;
        float wheel = Input.mouseScrollDelta.y;
        if (Mathf.Abs(wheel) > 0.001f) scrollDelta.y = wheel * scrollSpeed;

        if (Input.GetKey(scrollUpKey)) scrollDelta.y += scrollSpeed * Time.unscaledDeltaTime;
        if (Input.GetKey(scrollDownKey)) scrollDelta.y -= scrollSpeed * Time.unscaledDeltaTime;

        if (scrollDelta.sqrMagnitude < 0.001f) return;

        _ped.scrollDelta = scrollDelta;

        var scrollHandler = ExecuteEvents.GetEventHandler<IScrollHandler>(topGO);
        if (scrollHandler != null)
            ExecuteEvents.Execute(scrollHandler, _ped, ExecuteEvents.scrollHandler);

        _ped.scrollDelta = Vector2.zero;
    }

    void HandleClickAndDrag(GameObject topGO)
    {
        bool down = Input.GetKeyDown(clickKey);
        bool held = Input.GetKey(clickKey);
        bool up = Input.GetKeyUp(clickKey);

        if (down)
        {
            _dragging = false;
            _pressPos = _ped.position;
            _ped.pressPosition = _pressPos;
            _ped.pointerPressRaycast = _ped.pointerCurrentRaycast;

            _pressGO = null;
            _dragGO = null;

            if (topGO != null)
            {
                _pressGO = ExecuteEvents.ExecuteHierarchy(topGO, _ped, ExecuteEvents.pointerDownHandler);
                if (_pressGO == null)
                    _pressGO = ExecuteEvents.GetEventHandler<IPointerClickHandler>(topGO);

                _dragGO = ExecuteEvents.GetEventHandler<IDragHandler>(topGO);
            }

            _ped.pointerPress = _pressGO;
            _ped.rawPointerPress = topGO;
            _ped.pointerDrag = _dragGO;

            if (_dragGO != null)
                ExecuteEvents.Execute(_dragGO, _ped, ExecuteEvents.initializePotentialDrag);
        }

        if (held && _dragGO != null)
        {
            float dist = Vector2.Distance(_pressPos, _ped.position);
            if (!_dragging && dist >= dragThreshold)
            {
                _dragging = true;
                ExecuteEvents.Execute(_dragGO, _ped, ExecuteEvents.beginDragHandler);
            }

            if (_dragging)
                ExecuteEvents.Execute(_dragGO, _ped, ExecuteEvents.dragHandler);
        }

        if (up)
        {
            if (_pressGO != null)
                ExecuteEvents.Execute(_pressGO, _ped, ExecuteEvents.pointerUpHandler);

            if (_dragging && _dragGO != null)
                ExecuteEvents.Execute(_dragGO, _ped, ExecuteEvents.endDragHandler);

            if (!_dragging && _pressGO != null)
            {
                var clickHandler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(topGO);
                if (clickHandler == _pressGO)
                    ExecuteEvents.Execute(_pressGO, _ped, ExecuteEvents.pointerClickHandler);
            }

            _ped.pointerPress = null;
            _ped.rawPointerPress = null;
            _ped.pointerDrag = null;

            _pressGO = null;
            _dragGO = null;
            _dragging = false;
        }
    }
}
