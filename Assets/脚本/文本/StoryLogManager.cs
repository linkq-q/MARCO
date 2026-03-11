using System.Collections.Generic;
using UnityEngine;

public class StoryLogManager : MonoBehaviour
{
    public static StoryLogManager Instance { get; private set; }

    readonly List<string> storyLines = new List<string>();
    public IReadOnlyList<string> StoryLines => storyLines;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void AppendStoryLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        storyLines.Add(line);
    }

    public List<string> GetAllStoryLines()
    {
        return new List<string>(storyLines);
    }
}
