using System;

[Serializable]
public class DecisionResult
{
    public int advance;     // -1/0/1
    public string tag;      // push/avoid/confuse/idle/anger/accept/reject (或你兼容resist)
    public int sanDelta;    // -5..+5
    public string note;     // <=10字
    public bool detailFollow; // 玩家是否在追问细节
}
