using System;
using UnityEngine;

public enum EvidenceType { Lore, AIQuote }

[Serializable]
public class EvidenceRef
{
    public string id;
    public EvidenceType type;
    [TextArea(1, 6)] public string text;

    public string scene;
    public long unixMs;
}
