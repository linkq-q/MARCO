using TMPro;
using UnityEngine;

public class EndingEndingPrefabBinder : MonoBehaviour
{
    [Header("UI")]
    public TextMeshProUGUI bodyTMP;

    public void Bind(string bodyText)
    {
        if (!bodyTMP)
            bodyTMP = GetComponentInChildren<TextMeshProUGUI>(true);

        if (bodyTMP)
        {
            bodyTMP.text = bodyText ?? "";
            bodyTMP.ForceMeshUpdate(true, true);
        }
        else
        {
            Debug.LogError("[EndingEndingPrefabBinder] No TextMeshProUGUI found in prefab.", this);
        }
    }
}