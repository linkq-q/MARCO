using UnityEngine;

public static class InputMode
{
    /// <summary>UI是否打开（背包/菜单/对话等）</summary>
    public static bool UIActive { get; private set; }

    public static void SetUI(bool on)
    {
        UIActive = on;

        // 永远锁定系统鼠标：不让真实鼠标接管
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
