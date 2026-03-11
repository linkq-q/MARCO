using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DiaryController : MonoBehaviour
{
    [Header("Timing")]
    public bool autoRun = true;
    public float intervalSeconds = 60f; // 1min = 1 day
    public int startDayIndex = 1;

    [Header("AI (Diary Only)")]
    public DiaryCloudResponder diaryResponder;
    [TextArea(3, 20)]
    public string diaryPromptTemplate;

    [Header("Guide")]
    public GuidePool guidePool;

    [Header("Events / UI")]
    public DiaryUI diaryUI;
    public bool logDebug = false;

    [Header("Family Voice Schedule")]
    [SerializeField] private FamilyVoiceScheduleConfig familyCfg = new FamilyVoiceScheduleConfig();


    int _day;
    bool _busy;
    Coroutine _loop;
    GuidePicker _guidePicker = new GuidePicker();

    public List<DiaryEntry> entries = new List<DiaryEntry>();
    List<IDiaryGuideRule> _rules = new List<IDiaryGuideRule>();

    void Awake()
    {
        _day = startDayIndex;

        _rules.Clear();
        _rules.Add(new EveryNDaysGuideRule(GuideType.LoopHint, 1));          // 每天都插
        _rules.Add(new RandomChanceGuideRule(GuideType.RealityMemory, 1f)); // 100%命中
    }


    void OnEnable()
    {
        if (autoRun) StartLoop();
    }

    void OnDisable()
    {
        StopLoop();
    }

    public void StartLoop()
    {
        if (_loop != null) StopCoroutine(_loop);
        _loop = StartCoroutine(Loop());
    }

    public void StopLoop()
    {
        if (_loop != null) StopCoroutine(_loop);
        _loop = null;
    }

    IEnumerator Loop()
    {
        while (true)
        {
            yield return RequestOneDay();

            // ✅ 不受 Time.timeScale 影响：背包暂停也照样计时
            yield return new WaitForSecondsRealtime(intervalSeconds);
        }
    }

    IEnumerator RequestOneDay()
    {
        if (_busy) yield break;
        _busy = true;

        if (diaryResponder == null)
        {
            Debug.LogError("[DiaryController] diaryResponder 未绑定（请在Inspector拖入 DiaryCloudResponder）");
            _busy = false;
            yield break;
        }

        string prompt = BuildPrompt(_day);
        if (logDebug) Debug.Log($"[Diary] Request day={_day}\nPrompt={prompt}");

        string aiText = null;
        yield return StartCoroutine(GenerateCoroutine(_day, prompt, t => aiText = t));

        if (!string.IsNullOrWhiteSpace(aiText))
        {
            var logEntry = new DiaryEntry
            {
                dayIndex = _day,
                isoTime = System.DateTime.UtcNow.ToString("o"),
                kind = DiaryEntryKind.Log,
                text = aiText
            };
            AppendEntry(logEntry);

            var guide = EvaluateGuideToInsert(_day, logEntry);
            if (guide != null)
            {
                // 1) 数据层：把引导句拼接进同一天Log（不新增Guide条目）
                //    你也可以根据喜好换成 "\n\n" 或加分隔线
                logEntry.text = $"{logEntry.text}\n\n{guide.text}";

                // 2) UI层：把刚刚显示出来的那条Log也更新一下
                if (diaryUI != null)
                    diaryUI.UpdateLastLogText(logEntry.text);

                // 3) 如果你仍想记录“已抽取过的guideId”用于去重/存档，也可以存到别处
                //    比如：entriesUsedGuideIds.Add(guide.id);
            }

        }
        else
        {
            if (logDebug) Debug.LogWarning("[Diary] AI 返回为空，本次不写入条目");
        }

        _day++;
        _busy = false;
    }

    void AppendEntry(DiaryEntry e)
    {
        entries.Add(e);
        if (diaryUI) diaryUI.AddEntry(e);
    }

    string BuildPrompt(int day)
    {
        if (string.IsNullOrEmpty(diaryPromptTemplate))
            return $"生成第{day}天的日记，100-180字，偏生活化叙述。";

        return diaryPromptTemplate.Replace("{day}", day.ToString());
    }

    GuideLine EvaluateGuideToInsert(int day, DiaryEntry lastLog)
    {
        if (guidePool == null) return null;

        bool shouldInsert = false;
        foreach (var r in _rules)
        {
            if (r.ShouldInsert(day, lastLog, out _))
            {
                shouldInsert = true;
                break;
            }
        }

        if (!shouldInsert) return null;

        return _guidePicker.PickWithFamilySchedule(guidePool, familyCfg, day);

    }



    IEnumerator GenerateCoroutine(int dayIndex, string prompt, System.Action<string> onDone)
    {
        var task = diaryResponder.GenerateDiaryAsync(dayIndex, prompt);

        while (!task.IsCompleted) yield return null;

        if (task.IsFaulted)
        {
            Debug.LogError("[Diary] Generate failed: " + task.Exception);
            onDone?.Invoke(null);
            yield break;
        }

        onDone?.Invoke(task.Result);
    }
}
