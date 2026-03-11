using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DiaryUI : MonoBehaviour
{
    [Header("List")]
    public RectTransform content;
    public GameObject entryPrefab;

    public ScrollRect scroll;

    TextMeshProUGUI _lastLogTMP;

    public void AddEntry(DiaryEntry e)
    {
        if (!content || !entryPrefab) return;

        var go = Instantiate(entryPrefab, content);

        var view = go.GetComponent<DiaryEntryView>();
        if (view == null || view.bodyTMP == null)
        {
            Debug.LogError("[DiaryUI] entryPrefab missing DiaryEntryView or bodyTMP not assigned!", go);
            return;
        }

        view.bodyTMP.text = e.text;

        if (e.kind == DiaryEntryKind.Log)
            _lastLogTMP = view.bodyTMP;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
    }

    public void UpdateLastLogText(string newText)
    {
        if (_lastLogTMP == null)
        {
            Debug.LogWarning("[DiaryUI] UpdateLastLogText called but _lastLogTMP is NULL. Did you AddEntry(Log) first?");
            return;
        }

        _lastLogTMP.text = newText;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
    }
}
