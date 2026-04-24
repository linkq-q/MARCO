using UnityEngine;
using UnityEngine.UI;

public class AITakeoverEndingFlow : MonoBehaviour
{
    [Header("Refs")]
    public EndingRequester requester;
    public GameObject uiRoot;

    [Header("Content")]
    public RectTransform content;
    [Tooltip("Legacy only. No longer required by EndingUIBinder.")]
    public ScrollRect scroll;
    public GameObject endingTextPrefab;

    [Header("Optional")]
    public GameObject loadingGO;
    public bool clearBeforeShow = true;
    public bool scrollToTop = true;

    EndingUIBinder _binder;

    void Awake()
    {
        SyncBinder();
    }

    void OnValidate()
    {
        SyncBinder();
    }

    public void Play()
    {
        SyncBinder();
        if (_binder)
            _binder.PlayTakeoverEnding();
        else
            Debug.LogError("[AITakeoverEndingFlow] EndingUIBinder could not be found in the scene.", this);
    }

    void SyncBinder()
    {
        _binder = GetComponent<EndingUIBinder>();
        if (!_binder)
        {
            var all = FindObjectsByType<EndingUIBinder>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var candidate in all)
            {
                if (!candidate) continue;

                if (requester && candidate.requester == requester)
                {
                    _binder = candidate;
                    break;
                }

                if (content && candidate.content == content)
                {
                    _binder = candidate;
                    break;
                }

                if (uiRoot && candidate.uiRoot == uiRoot)
                {
                    _binder = candidate;
                    break;
                }
            }

            if (!_binder && all.Length > 0)
                _binder = all[0];
        }

        if (_binder)
            _binder.ImportLegacyBindings(this);
    }
}
