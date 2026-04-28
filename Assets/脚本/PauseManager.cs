using UnityEngine;

public class PauseManager : MonoBehaviour
{
    public static PauseManager Instance { get; private set; }
    public static bool IsPaused { get; private set; }

    [Header("UI")]
    public GameObject pauseMenuPanel;

    BottomChatPopup _chatPopup;
    CursorLockMode _previousLockMode;
    bool _previousCursorVisible;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (pauseMenuPanel) pauseMenuPanel.SetActive(false);
    }

    void Start()
    {
        _chatPopup = FindFirstObjectByType<BottomChatPopup>();
    }

    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;

        // 对话输入框打开时 Escape 交给 BottomChatPopup 处理，不暂停
        if (!IsPaused && _chatPopup != null && _chatPopup.IsInInputMode) return;

        if (IsPaused) Resume();
        else Pause();
    }

    public void Pause()
    {
        IsPaused = true;
        Time.timeScale = 0f;

        if (pauseMenuPanel) pauseMenuPanel.SetActive(true);

        _previousLockMode = Cursor.lockState;
        _previousCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Resume()
    {
        IsPaused = false;
        Time.timeScale = 1f;

        if (pauseMenuPanel) pauseMenuPanel.SetActive(false);

        Cursor.lockState = _previousLockMode;
        Cursor.visible = _previousCursorVisible;
    }

    public void QuitGame()
    {
        Time.timeScale = 1f;
        Application.Quit();
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            IsPaused = false;
        }
    }
}
