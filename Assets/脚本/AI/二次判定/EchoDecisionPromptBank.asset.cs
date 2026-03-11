using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Echo/DecisionPromptBank")]
public class EchoDecisionPromptBank : ScriptableObject
{
    [TextArea(5, 20)]
    public string commonHeader;

    [Serializable]
    public class Entry
    {
        public EchoStage stage;
        public int subState;
        [TextArea(5, 30)]
        public string prompt;
    }

    public List<Entry> entries = new List<Entry>();

    public string Get(EchoStage stage, int subState)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e.stage == stage && e.subState == subState)
                return e.prompt;
        }
        return null;
    }
}
