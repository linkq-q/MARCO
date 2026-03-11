using UnityEngine;

public class CameraTrackLock : MonoBehaviour
{
    [Header("Auto Find")]
    [Tooltip("生成后用于锁定的根节点名字（MapGenerator 里通常会叫 NearRoot/MidRoot/FarRoot）")]
    public string trackRootName = "NearRoot";

    [Tooltip("每隔多少秒尝试查找一次（避免每帧Find带来开销）")]
    public float findInterval = 0.2f;

    [Header("Track")]
    [Tooltip("轨道在 TrackRoot 本地空间的方向。一般用 Vector3.right（你的列是沿X+Z斜向，但步进的主轴是X）")]
    public Vector3 trackLocalDir = Vector3.right;

    [Header("Lock Axis")]
    public bool lockX = true;
    public bool lockZ = true;

    Transform trackRoot;
    Vector3 startOffset;
    Vector3 axisWorld;

    float nextFindTime;

    void Start()
    {
        TryBind(); // 尝试一次
    }

    void Update()
    {
        // 如果还没绑定，就定时找
        if (!trackRoot && Time.time >= nextFindTime)
        {
            nextFindTime = Time.time + findInterval;
            TryBind();
        }
    }

    void LateUpdate()
    {
        if (!trackRoot) return;

        // 把相机位置锁到“trackRoot + 初始偏移”
        Vector3 target = trackRoot.position + startOffset;
        Vector3 pos = transform.position;

        if (lockX) pos.x = target.x;
        if (lockZ) pos.z = target.z;

        transform.position = pos;
    }

    void TryBind()
    {
        GameObject go = GameObject.Find(trackRootName);
        if (!go) return;

        trackRoot = go.transform;
        axisWorld = trackRoot.TransformDirection(trackLocalDir).normalized;

        // 记录相机与轨道的初始相对偏移（绑定瞬间）
        startOffset = transform.position - trackRoot.position;

        Debug.Log($"[CameraTrackLockAuto] Bound to {trackRootName}");
    }
}
