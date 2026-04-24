using UnityEngine;

public class Stage2LinkModeBridge : MonoBehaviour
{
    [Header("Refs")]
    public Stage2LinkDrawScheduler scheduler;
    public GameObject boardPanelRoot;
    public ModeDisableManager disableManager;

    bool _modeEntered;
    bool _schedulerStarted;

    void OnEnable()
    {
        BindRunState();
        SyncStage2State();
    }

    void Update()
    {
        SyncBoardMode();
    }

    void OnDisable()
    {
        if (EchoRunState.I != null)
            EchoRunState.I.OnStageChanged -= OnRunStageChanged;

        if (_modeEntered && disableManager != null)
            disableManager.Exit();

        scheduler?.ExitStage2();
        _modeEntered = false;
        _schedulerStarted = false;
    }

    public void BeginStage2()
    {
        if (_schedulerStarted) return;
        _schedulerStarted = true;

        if (scheduler != null)
            scheduler.EnterStage2();
    }

    public void TryTriggerNextScenarioFromDialogue()
    {
        if (scheduler == null) return;
        BeginStage2();
        scheduler.TryTriggerNextScenario();
    }

    void StopStage2()
    {
        scheduler?.ExitStage2();
        _schedulerStarted = false;
    }

    void SyncBoardMode()
    {
        var root = boardPanelRoot != null
            ? boardPanelRoot
            : (scheduler != null ? scheduler.boardPanelRoot : null);

        bool isOpen = root != null && root.activeInHierarchy;

        if (isOpen && !_modeEntered)
        {
            if (disableManager != null)
                disableManager.Enter();

            _modeEntered = true;
            return;
        }

        if (!isOpen && _modeEntered)
        {
            if (disableManager != null)
                disableManager.Exit();

            _modeEntered = false;
        }
    }

    void BindRunState()
    {
        if (EchoRunState.I == null) return;

        EchoRunState.I.OnStageChanged -= OnRunStageChanged;
        EchoRunState.I.OnStageChanged += OnRunStageChanged;
    }

    void SyncStage2State()
    {
        if (EchoRunState.I == null) return;

        if (EchoRunState.I.stage == EchoStage.Stage2_Rift)
            BeginStage2();
        else
            StopStage2();
    }

    void OnRunStageChanged(EchoStage stage, int subState, string reason)
    {
        if (stage == EchoStage.Stage2_Rift)
            BeginStage2();
        else
            StopStage2();
    }
}
