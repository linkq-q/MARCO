using UnityEngine;

/// <summary>
/// 全局点击检测：
/// 鼠标点击 -> Raycast -> 找到命中物体父链上的 InteractableEventSource 并触发
/// </summary>
public class InteractableClickSystem : MonoBehaviour
{
    public Camera targetCamera;
    public int mouseButton = 0;

    [Tooltip("中文注释：建议只勾 Interactable 层，避免天空盒/大物体挡住")]
    public LayerMask raycastMask = ~0;

    public bool debugLog = true;
    public bool enableWorldClick = true;


    void Awake()
    {
        if (targetCamera == null) targetCamera = Camera.main;
        if (debugLog) Debug.Log("[ICS] Awake OK");
    }

    void Update()
    {
        if (!Input.GetMouseButtonDown(mouseButton)) return;
        if (!enableWorldClick) return;   // ✅ UI开时直接屏蔽世界点击

        if (debugLog) Debug.Log("[ICS] MouseDown");

        if (targetCamera == null)
        {
            if (debugLog) Debug.LogWarning("[ICS] targetCamera is null");
            return;
        }

        Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, raycastMask.value))
        {
            // 先检查 hit.collider 是否为 null
            if (hit.collider != null)
            {
                if (debugLog) Debug.Log($"[ICS] Hit={hit.collider.name}, layer={LayerMask.LayerToName(hit.collider.gameObject.layer)}");

                var src = hit.collider.GetComponent<InteractableEventSource>();
                var srcInParent = hit.collider.GetComponentInParent<InteractableEventSource>();

                if (debugLog)
                {
                    Debug.Log($"[ICS] SourceOnHit={(src ? src.name : "null")} | SourceInParent={(srcInParent ? srcInParent.name : "null")}");
                }

                // 中文注释：优先触发命中对象自身，其次触发父链上的事件源
                if (src != null) { src.TryTriggerFromExternal(); return; }
                if (srcInParent != null) { srcInParent.TryTriggerFromExternal(); return; }

                if (debugLog) Debug.LogWarning("[ICS] No InteractableEventSource found on hit object or its parents.");
            }
            else
            {
                if (debugLog) Debug.LogWarning("[ICS] Raycast hit a null collider.");
            }
            return;
        }


        if (debugLog) Debug.Log("[ICS] Raycast miss");
    }
}
