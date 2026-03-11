
public static class EventLoreUtil
{
    // 获取事件文本，用于记录语料
    public static string GetRecordText(EventPoolData.EventEntry e, bool choseA, out bool valid)
    {
        valid = false;
        if (e == null) return null;
        if (!e.recordAsLore) return null;

        if (e.type == EventType.SingleLine)
        {
            var t = e.singleLine;
            if (string.IsNullOrWhiteSpace(t)) return null;
            valid = true;
            return t.Trim();
        }
        else
        {
            var opt = choseA ? e.optionA : e.optionB;
            if (opt == null) return null;

            var t = string.IsNullOrWhiteSpace(opt.logTextOverride) ? opt.text : opt.logTextOverride;
            if (string.IsNullOrWhiteSpace(t)) return null;
            valid = true;
            return t.Trim();
        }
    }
}
