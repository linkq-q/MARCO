using UnityEngine;

public interface IDiaryGuideRule
{
    bool ShouldInsert(int dayIndex, DiaryEntry lastLog, out GuideType type);
}

public class EveryNDaysGuideRule : IDiaryGuideRule
{
    private GuideType _type;
    private int _n;

    public EveryNDaysGuideRule(GuideType type, int n)
    {
        _type = type;
        _n = Mathf.Max(1, n);
    }

    public bool ShouldInsert(int dayIndex, DiaryEntry lastLog, out GuideType type)
    {
        type = _type;
        return dayIndex % _n == 0;
    }
}

public class RandomChanceGuideRule : IDiaryGuideRule
{
    private GuideType _type;
    private float _p;

    public RandomChanceGuideRule(GuideType type, float p)
    {
        _type = type;
        _p = Mathf.Clamp01(p);
    }

    public bool ShouldInsert(int dayIndex, DiaryEntry lastLog, out GuideType type)
    {
        type = _type;
        return Random.value < _p;
    }
}
