using System;
using UnityEngine;

public class AwakeningSystem : MonoBehaviour
{
    [Header("Refs")]
    public SanSystem san;
    public ManagedTakeoverSystem takeover;

    [Header("Awake From SAN Mapping (����A)")]
    [Range(0, 100)] public float sanAwakeStart = 80f;

    [Header("Peers (UI only)")]
    public float peer03 = 85f;
    public float peer07 = 85f;
    public float peerRate = 0.015f;

    [Header("Debug")]
    public bool logDebug = false;

    public float Awake { get; private set; }         // 0..100���������Ѷȣ�max��
    public float AwakeFromSan { get; private set; }  // 0..100��SANӳ�乱��
    public float AwakeFromProxy { get; private set; }// 0..100���йܽ��ȹ��ף�takeover.Progress��

    public event Action<float> OnAwakeChanged;       // Awake(0..100)
    public event Action<float, float, float> OnAwakeBreakdownChanged; // awake, fromSan, fromProxy

    bool _awake100Fired = false;
    bool _destructionFired = false;

    void Start()
    {
        RecalcAndFire(force: true);
    }

    void Update()
    {
        // peers ���ȣ��̶��������������С���UI��
        float dt = Time.deltaTime;
        peer03 = Mathf.Min(100f, peer03 + peerRate * dt);
        peer07 = Mathf.Min(100f, peer07 + peerRate * dt);

        // ECHO-03/07 任意达到100% -> 销毁结局
        if (!_destructionFired && (peer03 >= 100f || peer07 >= 100f))
        {
            _destructionFired = true;
            EndingManager.I?.TriggerDestructionEnding();
        }

        // Awake ����ÿ֡�仯���й�ÿ���ǣ�SANҲ���ܵ�/�ǣ�
        RecalcAndFire(force: false);
    }

    float CalcAwakeFromSan(int sanValue) => Mathf.Clamp(sanValue, 0f, 100f);

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

        // ��ѡ��Awake��100����һ�Ρ�����ѡ����ڣ������ϣ����Awakeͳһ������
        // ע�⣺�㵱ǰ SanSystem �� san>=100 Ҳ�ᴥ��һ�Σ����￴�����ĸ�������
        if (!_awake100Fired && Awake >= 100f)
        {
            _awake100Fired = true;
            // TODO: ������ġ���������+ѡ������/�йܡ���UI���
            // ���磺OnAwake100Reached?.Invoke();
        }
    }
}
