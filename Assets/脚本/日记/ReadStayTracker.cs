using UnityEngine;

public class ReadStayTracker : MonoBehaviour
{
    [Header("Refs")]
    public SanSystem san;

    [Header("Key")]
    [Tooltip("用于标识当前阅读对象的key。比如 diary_main / item_stone")]
    public string contextKey = "diary_main";

    [Header("Rule")]
    public float minSeconds = 8f;

    [Header("Toast")]
    public bool showToastOnReward = true;
    public string toastText = "专注阅读：San +2";

    float _t0 = -1f;
    bool _armed;       // 是否正在计时
    bool _fired;       // 本次 Begin 之后是否已触发
    string _armedKey;  // Begin 时的 key 快照（防止中途 key 被改）

    void Awake()
    {
        if (!san) san = FindFirstObjectByType<SanSystem>();
    }

    void Update()
    {
        if (!_armed || _fired) return;

        float dur = Time.unscaledTime - _t0;
        if (dur >= minSeconds)
        {
            _fired = true;

            // ✅ 触发阅读奖励（60s冷却由 SanSystem 控制）
            if (san != null)
                san.ApplyPlayerAction(SanSystem.PlayerActionType.ReadDiaryOver8s, _armedKey);

            if (showToastOnReward)
                ToastSpawner.Instance?.Show(toastText);
        }
    }

    public void Begin()
    {
        if (!isActiveAndEnabled) return;

        _armed = true;
        _fired = false;
        _t0 = Time.unscaledTime;
        _armedKey = contextKey;
    }

    public void End()
    {
        // 结束只负责停止计时（不再结算）
        _armed = false;
        _t0 = -1f;
        _armedKey = null;
        _fired = false;
    }

    void OnDisable()
    {
        End();
    }
}
