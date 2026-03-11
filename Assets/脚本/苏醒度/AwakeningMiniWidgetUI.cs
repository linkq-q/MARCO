using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AwakeningMiniWidgetUI_Simple : MonoBehaviour
{
    [Header("Refs")]
    public AwakeningSystem awakening; // 提供：Awake / peer03 / peer07（会自动增长）

    [Header("Text Nodes (直接拖你四个文本节点)")]
    public TextMeshProUGUI titleText;   // 标题
    public TextMeshProUGUI selfText;    // 本机
    public TextMeshProUGUI peer1Text;   // 实验体1
    public TextMeshProUGUI peer2Text;   // 实验体2

    [Header("Optional Progress (不做也能跑)")]
    public Image selfFill;
    public Image peer1Fill;
    public Image peer2Fill;

    [Header("Display")]
    public string title = "同批实验体进度";

    [Tooltip("可用占位符：{code} {role} {percent} {rank}")]
    public string selfTemplate = "{code} / {role}：{percent}% 排名 {rank}";
    public string peerTemplate = "{code} / {role}：{percent}% 排名 {rank}";

    [Header("Identity")]
    public string selfCode = "ECHO-76";
    public string selfRole = "你的实验体";
    public string peer1Code = "ECHO-03";
    public string peer1Role = "实验体";
    public string peer2Code = "ECHO-07";
    public string peer2Role = "实验体";

    [Header("Rank Settings")]
    public int selfStartRank = 7681;
    public int selfMinRank = 1;

    public int peer1DefaultRank = 1;
    public int peer2DefaultRank = 2;

    [Header("Rank Mapping")]
    public int rankGainPerPercent = 30; // 每 1% 进步多少名

    [Tooltip("本机超过对手时额外推进名次（戏剧化奖励）")]
    public int selfRankGainPerOvertake = 250;

    [Header("Risk Popup (Prefab)")]
    public bool enableRiskPopup = true;
    public float riskThreshold = 50f;
    public float riskPopupInterval = 30f;
    public GameObject riskPopupPrefab;     // 你做好的预制体
    public Transform popupParent;          // 不填则用当前物体
    [TextArea(1, 4)]
    public string riskMessage = "当前回收风险较高，若本轮未达阈值，将转交其它模块。";

    // internal
    int _selfRank;
    int _peer1Rank;
    int _peer2Rank;

    float _lastSelfPercent = -1f;
    int _lastPlacement = -1;
    float _riskTimer;

    void Awake()
    {
        _selfRank = Mathf.Max(selfMinRank, selfStartRank);
        _peer1Rank = Mathf.Max(1, peer1DefaultRank);
        _peer2Rank = Mathf.Max(1, peer2DefaultRank);

        // 保证 1 < 2
        if (_peer1Rank == _peer2Rank) _peer2Rank = _peer1Rank + 1;
        if (_peer1Rank > _peer2Rank)
        {
            int t = _peer1Rank;
            _peer1Rank = _peer2Rank;
            _peer2Rank = t;
        }
    }

    void OnEnable()
    {
        Refresh(force: true);
    }

    void Update()
    {

        TickRank();
        Refresh(force: false);
        TickRiskPopup(Time.deltaTime);
    }

    public void RefreshNow() => Refresh(force: true);

    // ================== UI Refresh ==================

    void Refresh(bool force)
    {
        if (titleText != null) titleText.text = title;
        if (awakening == null) return;

        float selfP = awakening.Awake;
        float p1 = awakening.peer03;
        float p2 = awakening.peer07;

        if (selfText != null)
            selfText.text = FormatLine(selfTemplate, selfCode, selfRole, selfP, _selfRank);

        if (peer1Text != null)
            peer1Text.text = FormatLine(peerTemplate, peer1Code, peer1Role, p1, _peer1Rank);

        if (peer2Text != null)
            peer2Text.text = FormatLine(peerTemplate, peer2Code, peer2Role, p2, _peer2Rank);

        // optional progress fills
        if (selfFill != null) selfFill.fillAmount = Mathf.Clamp01(selfP / 100f);
        if (peer1Fill != null) peer1Fill.fillAmount = Mathf.Clamp01(p1 / 100f);
        if (peer2Fill != null) peer2Fill.fillAmount = Mathf.Clamp01(p2 / 100f);
    }

    static string FormatLine(string template, string code, string role, float percent, int rank)
    {
        int p = Mathf.Clamp(Mathf.RoundToInt(percent), 0, 100);
        return template
            .Replace("{code}", code ?? "")
            .Replace("{role}", role ?? "")
            .Replace("{percent}", p.ToString())
            .Replace("{rank}", rank.ToString());
    }

    // ================== Rank Logic ==================
    // 解决你之前“名次不动”的问题：
    // - 不要求超过peers也会推进：按Awake正向增长推进名次
    // - 超过peers时给额外推进，并让peer排名顺次变化
    void TickRank()
    {
        if (awakening == null) return;

        float selfP = awakening.Awake;

        if (_lastSelfPercent < 0f) _lastSelfPercent = selfP;

        float delta = selfP - _lastSelfPercent;
        if (delta >= 1f && rankGainPerPercent > 0)
        {
            int whole = Mathf.FloorToInt(delta);              // 只按整 1% 计算
            int gain = whole * rankGainPerPercent;
            _selfRank = Mathf.Max(selfMinRank, _selfRank - gain);

            _lastSelfPercent += whole; // 消耗掉已结算的整百分比
        }
    }

    void ApplyPeerRanksByPlacement(int placement, float p1, float p2)
    {
        if (placement == 2)
        {
            // 本机仍落后：peers保持默认 1/2
            _peer1Rank = peer1DefaultRank;
            _peer2Rank = peer2DefaultRank;
            return;
        }

        if (placement == 1)
        {
            // 本机第二：谁第一谁=1，另一=3
            bool p1First = p1 > p2;
            _peer1Rank = p1First ? 1 : 3;
            _peer2Rank = p1First ? 3 : 1;
            return;
        }

        // placement==0 本机第一：peers为2/3（按进度）
        bool p1Second = p1 >= p2;
        _peer1Rank = p1Second ? 2 : 3;
        _peer2Rank = p1Second ? 3 : 2;
    }

    // ================== Risk Popup ==================

    void TickRiskPopup(float dt)
    {
        if (!enableRiskPopup || awakening == null) return;
        if (riskPopupPrefab == null) return;

        if (awakening.Awake >= riskThreshold)
        {
            _riskTimer = 0f;
            return;
        }

        _riskTimer += dt;
        if (_riskTimer >= riskPopupInterval)
        {
            _riskTimer = 0f;
            SpawnRiskPopup();
        }
    }

    void SpawnRiskPopup()
    {
        Transform parent = popupParent != null ? popupParent : transform;
        var go = Instantiate(riskPopupPrefab, parent);

        var view = go.GetComponent<RiskPopupView>();
        if (view != null)
        {
            view.Show(riskMessage);
            return;
        }

        var tmp = go.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null) tmp.text = riskMessage;
    }
}
