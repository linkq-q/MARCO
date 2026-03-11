using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuActions : MonoBehaviour
{
    [Header("Scene Names")]
    public string menuSceneName = "开始场景";      // 新增：开始菜单
    public string gameSceneName = "游戏场景";
    public string metaSceneName = "局外成长场景";

    // 开始游戏
    public void StartGame()
    {
        LoadScene(gameSceneName);
    }

    // 跳转到局外成长
    public void OpenMetaScene()
    {
        LoadScene(metaSceneName);
    }

    // 返回开始菜单
    public void BackToMenu()
    {
        LoadScene(menuSceneName);
    }

    // 统一场景加载函数（避免重复代码）
    void LoadScene(string sceneName)
    {
        if (!string.IsNullOrEmpty(sceneName))
        {
            SceneManager.LoadScene(sceneName);
        }
        else
        {
            Debug.LogWarning("场景名称未设置！");
        }
    }

    // 退出游戏
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
