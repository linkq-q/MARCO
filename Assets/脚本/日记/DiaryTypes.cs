using UnityEngine;

public enum DiaryEntryKind { Log, Guide }

public enum GuideType
{
    AccidentRecord,
    FamilyVoice,
    RealityMemory,
    LoopHint,
    EchoPrevLine
}

[System.Serializable]
public class DiaryEntry
{
    public int dayIndex;
    public string isoTime;
    public DiaryEntryKind kind;
    [TextArea(1, 10)]
    public string text;

    public GuideType guideType;
    public string guideId;
}

[System.Serializable]
public class GuideLine
{
    public string id;
    public GuideType type;
    [TextArea(1, 4)]
    public string text;
    public int weight = 1;

    public int stage; // 0-3
    public int order; // ¿ÉÑ¡

}
