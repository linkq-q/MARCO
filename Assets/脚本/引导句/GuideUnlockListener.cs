using UnityEngine;

public class GuideUnlockListener : MonoBehaviour
{
    public SanSystem san;

    void Awake()
    {
        if (!san) san = FindFirstObjectByType<SanSystem>();
    }

    void OnEnable()
    {
        if (san != null)
            san.OnInteractionGuideRequested += OnGuide;
    }

    void OnDisable()
    {
        if (san != null)
            san.OnInteractionGuideRequested -= OnGuide;
    }

    void OnGuide(int guideIndex, int totalValidInteractions)
    {
        Debug.Log($"[Guide] unlocked #{guideIndex} by interactions={totalValidInteractions}");

        // TODO: 这里接你的引导线索句系统
        // 例：StoryTaskManager.Instance?.UnlockGuideLine(guideIndex);
        // 或：AIBroker.Instance?.RequestGuideLine(guideIndex);
    }
}
