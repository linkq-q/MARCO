using System;
using UnityEngine;

[Serializable]
public struct LoreRef
{
    public string id;

    [Tooltip("语料标题（新系统）")]
    public string title;

    [TextArea(1, 3)]
    [Tooltip("语料短描述（新系统）")]
    public string shortText;

    [TextArea(1, 3)]
    public string rawText;


    public string text
    {
        get
        {
            if (!string.IsNullOrEmpty(rawText)) return rawText;
            if (!string.IsNullOrEmpty(shortText)) return shortText;
            return title;
        }
        set
        {
            rawText = value;
        }
    }

}
