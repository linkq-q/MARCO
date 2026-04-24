using UnityEngine;

[RequireComponent(typeof(Collider))]
public class UIHintTriggerZone : MonoBehaviour
{
    [Header("Mode")]
    public bool useHighlightPreset = true;
    public string customHintId = "custom_hint";
    [TextArea(1, 3)]
    public string customHintText = "靠近高亮物体 E 获取线索";
    public KeyCode dismissKey = KeyCode.E;
    public float delay = 0f;

    [Header("Player Detect")]
    public string playerTag = "Player";
    public bool detectFirstPersonController = true;

    bool _triggered;

    void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (_triggered) return;
        if (!IsPlayer(other)) return;

        _triggered = true;

        if (useHighlightPreset)
        {
            UIHintManager.I?.NotifyFirstHighlightEntered();
            return;
        }

        UIHintManager.I?.ShowHintOnce(customHintId, customHintText, dismissKey, delay);
    }

    bool IsPlayer(Collider other)
    {
        if (other == null) return false;

        if (detectFirstPersonController && other.GetComponentInParent<FirstPersonControllerSimple>() != null)
            return true;

        return !string.IsNullOrWhiteSpace(playerTag) && other.CompareTag(playerTag);
    }
}
