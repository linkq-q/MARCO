using System;
using UnityEngine;

public class AwakeningSystem : MonoBehaviour
{
    [Header("Refs")]
    public SanSystem san;
    public ManagedTakeoverSystem takeover;

    [Header("Awake From SAN Mapping (方案A)")]
    [Range(0, 100)] public float sanAwakeStart = 80f;

    [Header("Peers (UI only)")]
    public float peer03 = 65f;
    public float peer07 = 59f;
    public float peerRate = 0.12f;

    [Header("Debug")]
    public bool logDebug = false;

    public float Awake { get; private set; }         // 0..100，最终苏醒度（max）
    public float AwakeFromSan { get; private set; }  // 0..100，SAN映射贡献
    public float AwakeFromProxy { get; private set; }// 0..100，托管进度贡献（takeover.Progress）

    public event Action<float> OnAwakeChanged;       // Awake(0..100)
    public event Action<float, float, float> OnAwakeBreakdownChanged; // awake, fromSan, fromProxy

    bool _awake100Fired = false;

    void Start()
    {
        RecalcAndFire(force: true);
    }

    void Update()
    {
        // peers 进度（固定增长，用于你的小组件UI）
        float dt = Time.deltaTime;
        peer03 = Mathf.Min(100f, peer03 + peerRate * dt);
        peer07 = Mathf.Min(100f, peer07 + peerRate * dt);

        // Awake 可能每帧变化（托管每秒涨，SAN也可能掉/涨）
        RecalcAndFire(force: false);
    }

    float CalcAwakeFromSan(int sanValue)
    {
        // 方案A：阈值线性映射
        float t = Mathf.InverseLerp(sanAwakeStart, 100f, sanValue); // <=start =>0, 100=>1
        return Mathf.Clamp01(t) * 100f;
    }

    void RecalcAndFire(bool force)
    {
        if (!san || !takeover) return;

        AwakeFromSan = CalcAwakeFromSan(san.San);
        AwakeFromProxy = takeover.Progress;
        float newAwake = Mathf.Max(AwakeFromSan, AwakeFromProxy);

        if (!force && Mathf.Approximately(newAwake, Awake)) return;

        Awake = newAwake;
        OnAwakeChanged?.Invoke(Awake);
        OnAwakeBreakdownChanged?.Invoke(Awake, AwakeFromSan, AwakeFromProxy);

        if (logDebug)
            Debug.Log($"[Awake] awake={Awake:F1} fromSan={AwakeFromSan:F1} fromProxy={AwakeFromProxy:F1} san={san.San} proxy={takeover.Progress:F1}");

        // 可选：Awake到100触发一次“苏醒选择”入口（如果你希望由Awake统一触发）
        // 注意：你当前 SanSystem 里 san>=100 也会触发一次，这里看你想哪个做主。
        if (!_awake100Fired && Awake >= 100f)
        {
            _awake100Fired = true;
            // TODO: 触发你的“补齐线索+选择醒来/托管”的UI入口
            // 比如：OnAwake100Reached?.Invoke();
        }
    }
}
