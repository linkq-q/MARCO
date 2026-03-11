using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class EndingLockdownManager : MonoBehaviour
{
    public static EndingLockdownManager I { get; private set; }

    [Serializable]
    public class Target
    {
        public enum Mode { SetActiveFalse, DisableBehaviour, CallLockable }
        public string note;
        public Mode mode = Mode.DisableBehaviour;

        public GameObject go;
        public Behaviour behaviour;
        public MonoBehaviour lockable; // 必须实现 IEndingLockable

        [HideInInspector] public bool prevGoActive;
        [HideInInspector] public bool prevBehaviourEnabled;
    }

    [Header("Targets")]
    public List<Target> targets = new();

    [Header("Optional: UI Input Kill Switch")]
    public bool disableEventSystem = true;
    bool _prevEventSystemEnabled;

    bool _locked;

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        // DontDestroyOnLoad(gameObject); // 需要跨场景再开
    }

    public void Lock(bool keepEventSystemEnabled = false)
    {
        if (_locked) return;
        _locked = true;

        // EventSystem 一刀切（可选）
        var es = EventSystem.current;
        if (!keepEventSystemEnabled && disableEventSystem && es != null)
        {
            _prevEventSystemEnabled = es.enabled;
            es.enabled = false;
            es.SetSelectedGameObject(null);
        }

        foreach (var t in targets)
        {
            if (t == null) continue;

            switch (t.mode)
            {
                case Target.Mode.SetActiveFalse:
                    if (t.go)
                    {
                        t.prevGoActive = t.go.activeSelf;
                        t.go.SetActive(false);
                    }
                    break;

                case Target.Mode.DisableBehaviour:
                    if (t.behaviour)
                    {
                        t.prevBehaviourEnabled = t.behaviour.enabled;
                        t.behaviour.enabled = false;
                    }
                    break;

                case Target.Mode.CallLockable:
                    if (t.lockable is IEndingLockable il)
                        il.OnEndingLock();
                    break;
            }
        }
    }

    public void Unlock()
    {
        if (!_locked) return;
        _locked = false;

        var es = EventSystem.current;
        if (disableEventSystem && es != null)
            es.enabled = _prevEventSystemEnabled;

        foreach (var t in targets)
        {
            if (t == null) continue;

            switch (t.mode)
            {
                case Target.Mode.SetActiveFalse:
                    if (t.go) t.go.SetActive(t.prevGoActive);
                    break;

                case Target.Mode.DisableBehaviour:
                    if (t.behaviour) t.behaviour.enabled = t.prevBehaviourEnabled;
                    break;

                case Target.Mode.CallLockable:
                    if (t.lockable is IEndingLockable il)
                        il.OnEndingUnlock();
                    break;
            }
        }
    }
    public void SetEventSystemEnabled(bool enabled)
    {
        var es = EventSystem.current;
        if (es != null) es.enabled = enabled;
    }

    // 只恢复被禁用的某些目标（按 note 关键字匹配）
    public void UnlockByNoteContains(string key)
    {
        if (!_locked) return;
        if (string.IsNullOrEmpty(key)) return;

        foreach (var t in targets)
        {
            if (t == null) continue;
            if (string.IsNullOrEmpty(t.note)) continue;
            if (!t.note.Contains(key, StringComparison.OrdinalIgnoreCase)) continue;

            switch (t.mode)
            {
                case Target.Mode.SetActiveFalse:
                    if (t.go) t.go.SetActive(t.prevGoActive);
                    break;

                case Target.Mode.DisableBehaviour:
                    if (t.behaviour) t.behaviour.enabled = t.prevBehaviourEnabled;
                    break;

                case Target.Mode.CallLockable:
                    if (t.lockable is IEndingLockable il) il.OnEndingUnlock();
                    break;
            }
        }

        // 这里不改 _locked，表示仍处于“总体锁定”状态
    }
}