using UnityEngine;

public class Stage2InitHub : MonoBehaviour
{
    [Header("Systems (Stage2 才启用)")]
    public AwakeningSystem awakeningSystem;
    public ManagedTakeoverSystem takeoverSystem;

    [Header("UI (Stage2 才显示)")]
    public AwakeningMiniWidgetUI_Simple awakeningWidget;
    public ManagedTakeoverHudView takeoverHud;

    [Header("Optional UI Nodes")]
    public GameObject[] enableOnStage2;

    bool _inited;

    // ✅ 由 GuessStageGate_Auto.OnEnterStage2 调用
    public void InitStage2()
    {
        if (_inited) return;
        _inited = true;

        // 1) 启用系统（如果你希望 Stage1 不跑，就在 Awake/Start 里先禁用它们）
        if (takeoverSystem) takeoverSystem.enabled = true;
        if (awakeningSystem) awakeningSystem.enabled = true;

        // 2) 显示UI
        if (awakeningWidget)
        {
            awakeningWidget.gameObject.SetActive(true);
            awakeningWidget.RefreshNow();
        }

        if (takeoverHud)
            takeoverHud.gameObject.SetActive(true);

        // 3) 其它Stage2节点
        if (enableOnStage2 != null)
        {
            for (int i = 0; i < enableOnStage2.Length; i++)
                if (enableOnStage2[i]) enableOnStage2[i].SetActive(true);
        }
    }

    // 可选：离开Stage2
    public void HideStage2()
    {
        if (awakeningWidget) awakeningWidget.gameObject.SetActive(false);
        if (takeoverHud) takeoverHud.gameObject.SetActive(false);

        if (takeoverSystem) takeoverSystem.enabled = false;
        if (awakeningSystem) awakeningSystem.enabled = false;
    }
}