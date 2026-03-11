using System.Collections.Generic;
using UnityEngine;

public class ModeDisableManager : MonoBehaviour
{
    [Header("Disable Targets")]
    [Tooltip("进入模式时需要禁用的脚本（MonoBehaviour）")]
    public List<MonoBehaviour> disableScripts = new List<MonoBehaviour>();

    [Header("Cursor")]
    public bool unlockCursorOnEnter = true;
    public bool lockCursorOnExit = true;

    [Tooltip("如你用 FirstPersonControllerSimple，拖进来用于 SetCursorLock")]
    public FirstPersonControllerSimple firstPerson;

    // 记录进入前每个脚本的 enabled 状态，退出时恢复
    readonly Dictionary<MonoBehaviour, bool> _prev = new Dictionary<MonoBehaviour, bool>();
    bool _locked = false;

    public void Enter()
    {
        if (_locked) return;
        _locked = true;

        _prev.Clear();
        for (int i = 0; i < disableScripts.Count; i++)
        {
            var b = disableScripts[i];
            if (!b) continue;
            _prev[b] = b.enabled;
            b.enabled = false;
        }

        if (unlockCursorOnEnter)
        {
            if (firstPerson) firstPerson.SetCursorLock(false);
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
    }

    public void Exit()
    {
        if (!_locked) return;
        _locked = false;

        foreach (var kv in _prev)
        {
            if (!kv.Key) continue;
            kv.Key.enabled = kv.Value; // 恢复进入前状态
        }
        _prev.Clear();

        if (lockCursorOnExit)
        {
            if (firstPerson) firstPerson.SetCursorLock(true);
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
    }
}