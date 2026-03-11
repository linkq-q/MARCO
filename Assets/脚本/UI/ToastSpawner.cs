using System.Collections.Generic;
using UnityEngine;

public class ToastSpawner : MonoBehaviour
{
    public static ToastSpawner Instance { get; private set; }

    [Header("Refs")]
    [SerializeField] private Transform toastRoot;
    [SerializeField] private GameObject toastPrefab;

    [Header("Stack Layout")]
    [Tooltip("每条 toast 之间的垂直间距（像素）")]
    public float spacingY = 70f;

    readonly List<ToastItemUI> _alive = new List<ToastItemUI>();

    bool endingLocked;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void Show(string msg)
    {
        if (endingLocked) return;
        Debug.Log("[ToastSpawner] Show CALLED -> " + msg);

        if (!toastRoot || !toastPrefab)
        {
            Debug.LogError("[ToastSpawner] toastRoot 或 toastPrefab 没绑！ root=" + toastRoot + " prefab=" + toastPrefab);
            return;
        }

        var go = Instantiate(toastPrefab, toastRoot);

        var item = go.GetComponent<ToastItemUI>();
        if (!item)
        {
            Debug.LogError("[ToastSpawner] toastPrefab 上没有 ToastItemUI 组件！");
            Destroy(go);
            return;
        }

        // 维护alive列表（清掉已销毁的）
        _alive.RemoveAll(x => x == null);

        // ✅ 计算这条 toast 的基准位置：越新越靠下（你要“自动生成在下方”）
        var rt = go.GetComponent<RectTransform>();
        Vector2 basePos = rt.anchoredPosition;

        int index = _alive.Count; // 新的放在最下面
        Vector2 stackedBasePos = basePos + new Vector2(0f, -spacingY * index);

        item.SetBasePosition(stackedBasePos);
        _alive.Add(item);

        item.Play(msg);
    }

    [ContextMenu("TEST Toast")]
    void TestToast()
    {
        Show("测试拾取提示");
        Show("第二条提示");
        Show("第三条提示");
    }

    public void SetEndingLocked(bool locked)
    {
        endingLocked = locked;
        if (locked) ClearAll();
    }

    public void ClearAll()
    {
        _alive.RemoveAll(x => x == null);
        for (int i = 0; i < _alive.Count; i++)
        {
            if (_alive[i]) Destroy(_alive[i].gameObject);
        }
        _alive.Clear();
    }

}
