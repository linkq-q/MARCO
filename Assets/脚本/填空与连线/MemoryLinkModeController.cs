using UnityEngine;

public class MemoryLinkModeController : MonoBehaviour
{
    [Header("Refs")]
    public GameObject boardRootGO;               // 记忆连线UI根（MemoryBoardRoot）
    public MemoryBoardController board;          // 你的连线控制器
    public ModeDisableManager disableManager;    // 上面的禁用管理器

    [Header("Auto")]
    public bool resetPuzzleOnEnter = true;
    public bool autoExitOnComplete = true;


    void Awake()
    {
        if (boardRootGO) boardRootGO.SetActive(false);
    }

    public void Enter()
    {
        if (boardRootGO) boardRootGO.SetActive(true);
        if (disableManager) disableManager.Enter();
        if (resetPuzzleOnEnter && board) board.ResetPuzzle();
    }

    public void Exit()
    {
        if (disableManager) disableManager.Exit();
        if (boardRootGO) boardRootGO.SetActive(false);
    }

    // 给 UnityEvent 用：完成时调用
    public void OnPuzzleCompleted()
    {
        if (autoExitOnComplete) Exit();
    }

    public KeyCode testKey = KeyCode.M;

    void Update()
    {
        if (Input.GetKeyDown(testKey))
        {
            if (boardRootGO && boardRootGO.activeSelf) Exit();
            else Enter();
        }
    }
}