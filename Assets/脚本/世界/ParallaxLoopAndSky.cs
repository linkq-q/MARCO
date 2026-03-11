using UnityEngine;
using System.Collections.Generic;

public class ParallaxLoopAndSky : MonoBehaviour
{
    [Header("References (set by MapGenerator)")]
    public MapGenerator mapGenerator;
    public BiomeConfig[] biomes;
    public int columnsPerBiome = 6;
    public SkyApplier skyApplier;

    [Header("Band (single-root mode only)")]
    public ScatterBand band = ScatterBand.Near;

    [Header("Movement")]
    [Tooltip("正数=向右移动；负数=向左移动。相机不动，传送带原地循环。")]
    public float speed = 3f;

    // ===== 兼容旧字段（不要删，避免 MapGenerator 报 CS1061）=====
    [Header("Compatibility (do not remove)")]
    public Camera mainCamera;
    public float recycleBehind = 60f;
    public float recycleAhead = 60f;

    [Header("Recycle (Conveyor Belt Edges)")]
    [Tooltip("每帧最多回收次数，避免低帧率时一次回收太多列。")]
    public int maxRecyclePerFrame = 16;

    [Tooltip("边界缓冲（单位：步长比例）。")]
    [Range(0.1f, 1.5f)]
    public float edgePaddingInStep = 0.6f;

    [Header("Loop Reset (Interactables)")]
    public bool resetInteractablesEachBeltLoop = true; //开关：每跑一整圈就重置交互
    public bool debugLoopReset = false;

    [Header("Story Swap (Manual <-> Story)")]
    public bool enableStorySwap = true;
    public int startAfterLoop = 2;
    [Range(0f, 1f)] public float storyChance = 1f;

    // 如果为空 = 整层切换（所有列 Manual OFF / Story ON）
    // 如果填写 = 每轮随机选 1 个列号开 Story，其它列保持 Manual
    public int[] storyColumnCandidates;

    // 勾选后：只在被选中的列显示 Story，其它列 Story 强制关闭
    public bool onlyShowStoryOnSelectedColumn = true;

    // 调试
    public bool debugStorySwap = false;


    float _travelAccum;
    int _loopIndex;
    int _activeStoryColumn = -1;



    float beltTravel;     // 累计位移（本地轴向）
    int beltLoopCount;    // 传送带圈数（可选，用于debug）


    // =========================
    // Sky Switch (Selectable Gate)
    // =========================
    public enum SkyGateBand
    {
        Near,
        Mid,
        Far
    }

    [Header("Sky Switch")]
    [Tooltip("选择哪个层（Near/Mid/Far）作为天空切换的触发界限。")]
    public SkyGateBand skyGate = SkyGateBand.Near;

    [Tooltip("如果勾选：用根节点名字 NearRoot/MidRoot/FarRoot 辅助判定（容错）。不勾则仅用 syncedBands/band 判定。")]
    public bool useRootNameAsFallback = true;

    [Tooltip("调试：打印切天触发与选中 gate 的信息。")]
    public bool debugSkyGate = false;

    [Header("Master Sync Mode (Recommended)")]
    public bool isMaster = false;
    public Transform[] syncedRoots;
    public ScatterBand[] syncedBands;

    [Header("Safety")]
    public bool forceNonMasterIfNotNear = true;

    // cached spacing
    Vector3 stepLocal;
    Vector3 axisLocal;
    float stepLen;

    float lastSpacingX;
    float lastTrackZOff;
    bool cachedOnce;

    // belt edges (recorded from EDIT/Start layout, so play-mode won't shift overall position)
    bool edgesInited;
    int columnCount;
    float loopLen;

    float leftEdgeProj;   // 初始最左列投影
    float rightEdgeProj;  // 初始最右列投影

    void Awake()
    {
        if (!mainCamera) mainCamera = Camera.main;
    }

    void OnEnable()
    {
        ValidateMasterRole();
        CacheStep(true);
        InitEdgesIfNeeded(true);
    }

    void Start()
    {
        ValidateMasterRole();
        CacheStep(true);
        InitEdgesIfNeeded(true);
    }

    void ValidateMasterRole()
    {
        if (!forceNonMasterIfNotNear) return;

        bool looksLikeNear = (band == ScatterBand.Near) || (transform.name == "NearRoot");
        if (!looksLikeNear && isMaster) isMaster = false;
    }

    void CacheStep(bool force = false)
    {
        if (!mapGenerator) return;

        float sx = mapGenerator.columnSpacingX;
        float zo = mapGenerator.columnTrackZOffset;

        if (!force && cachedOnce &&
            Mathf.Abs(sx - lastSpacingX) < 1e-5f &&
            Mathf.Abs(zo - lastTrackZOff) < 1e-5f)
            return;

        lastSpacingX = sx;
        lastTrackZOff = zo;
        cachedOnce = true;

        stepLocal = new Vector3(sx, 0f, zo);
        stepLen = stepLocal.magnitude;
        axisLocal = (stepLen > 0.0001f) ? (stepLocal / stepLen) : Vector3.right;
    }

    void InitEdgesIfNeeded(bool force)
    {
        int cnt = CountColumns(transform);
        if (cnt <= 1) return;

        bool need = force || !edgesInited || cnt != columnCount;
        if (!need) return;

        columnCount = cnt;
        loopLen = stepLen * columnCount;

        // 记录“编辑态摆好的”初始边界
        GetMinMaxProj(transform, out leftEdgeProj, out rightEdgeProj);

        edgesInited = true;
    }

    void Update()
    {
        if (!mapGenerator) return;

        CacheStep();
        if (stepLen < 0.0001f) return;

        if (!isMaster) return;

        InitEdgesIfNeeded(false);

        MoveAllRootsTogether();
        RecycleByFixedEdges();

        // 每跑满一整圈，重置一次交互
        TryResetInteractablesByBeltLoop();
        TryStorySwap();


    }

    void TryStorySwap()
    {
        if (!enableStorySwap) return;
        if (!isMaster) return;
        if (loopLen <= 0.0001f) return;

        _travelAccum += Mathf.Abs(speed) * Time.deltaTime;
        if (_travelAccum < loopLen) return;

        int loops = Mathf.FloorToInt(_travelAccum / loopLen);
        _travelAccum -= loops * loopLen;
        _loopIndex += loops;

        if (_loopIndex < startAfterLoop) return;
        if (Random.value > storyChance) return;

        PickAndApplyStoryColumn();
    }

    void PickAndApplyStoryColumn()
    {
        // 先恢复上一轮
        RestoreAllToManual();

        int selectedCol = -1;

        if (storyColumnCandidates != null && storyColumnCandidates.Length > 0)
        {
            selectedCol = storyColumnCandidates[
                Random.Range(0, storyColumnCandidates.Length)
            ];
        }

        _activeStoryColumn = selectedCol;

        ApplyStoryVisibility(selectedCol);

        if (debugStorySwap)
            Debug.Log($"[StorySwap] loop={_loopIndex}, col={selectedCol}");
    }


    void RestoreAllToManual()
    {
        RestoreInRoot(transform);

        if (syncedRoots != null)
        {
            for (int i = 0; i < syncedRoots.Length; i++)
            {
                var r = syncedRoots[i];
                if (!r) continue;
                if (r == transform) continue;
                RestoreInRoot(r);
            }
        }
    }

    static void RestoreInRoot(Transform root)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            var tr = root.GetChild(i);
            if (!tr.name.StartsWith("Col_")) continue;

            var manual = tr.Find("Manual");
            var story = tr.Find("Story");

            if (manual) manual.gameObject.SetActive(true);
            if (story) story.gameObject.SetActive(false);
        }
    }


    void ApplyStoryVisibility(int storyCol)
    {
        ApplyInRoot(transform, storyCol);

        if (syncedRoots != null)
        {
            for (int i = 0; i < syncedRoots.Length; i++)
            {
                var r = syncedRoots[i];
                if (!r) continue;
                if (r == transform) continue;
                ApplyInRoot(r, storyCol);
            }
        }
    }

    void ApplyInRoot(Transform root, int storyCol)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            var tr = root.GetChild(i);
            if (!tr.name.StartsWith("Col_")) continue;

            int colIndex = ExtractColIndex(tr.name);

            var manual = tr.Find("Manual");
            var story = tr.Find("Story");

            bool showStory = (storyCol >= 0 && colIndex == storyCol);
            if (!onlyShowStoryOnSelectedColumn)
                showStory = (storyCol >= 0);

            if (manual) manual.gameObject.SetActive(!showStory);
            if (story) story.gameObject.SetActive(showStory);
        }
    }


    int ExtractColIndex(string colName)
    {
        int underscore = colName.LastIndexOf('_');
        if (underscore >= 0 &&
            int.TryParse(colName.Substring(underscore + 1), out int v))
            return v;

        return -1;
    }



    void TryResetInteractablesByBeltLoop()
    {
        if (!resetInteractablesEachBeltLoop) return;
        if (!edgesInited) return;
        if (loopLen < 0.0001f) return;

        // speed 为正/负都支持：累计“走过的距离”
        beltTravel += speed * Time.deltaTime;

        float abs = Mathf.Abs(beltTravel);
        if (abs < loopLen) return;

        // 可能低帧率一次跨过多圈
        int loops = Mathf.FloorToInt(abs / loopLen);
        beltTravel -= Mathf.Sign(beltTravel) * loopLen * loops;
        beltLoopCount += loops;

        // ✅ 只重置本传送带系统内的交互点（不扫全场，性能更稳）
        ResetInteractablesUnderBelt();

        if (debugLoopReset)
            Debug.Log($"[LoopReset] beltLoopCount={beltLoopCount} loopsAdded={loops}");
    }

    void ResetInteractablesUnderBelt()
    {
        // master 根
        ResetInRoot(transform);

        // 同步根（Near/Mid/Far）
        if (syncedRoots != null)
        {
            for (int i = 0; i < syncedRoots.Length; i++)
            {
                var r = syncedRoots[i];
                if (!r) continue;
                if (r == transform) continue;
                ResetInRoot(r);
            }
        }
    }

    static void ResetInRoot(Transform root)
    {
        var sources = root.GetComponentsInChildren<InteractableEventSource>(true);
        for (int i = 0; i < sources.Length; i++)
            sources[i].ResetForNewLoop();
    }


    // =========================
    // Move
    // =========================
    void MoveAllRootsTogether()
    {
        Vector3 deltaLocal = axisLocal * (speed * Time.deltaTime);

        MoveColumnsInRoot(transform, deltaLocal);

        if (syncedRoots != null)
        {
            for (int i = 0; i < syncedRoots.Length; i++)
            {
                var r = syncedRoots[i];
                if (!r) continue;
                if (r == transform) continue; // 防止你把自己也塞进来
                MoveColumnsInRoot(r, deltaLocal);
            }
        }
    }

    void MoveColumnsInRoot(Transform root, Vector3 deltaLocal)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            Transform col = root.GetChild(i);
            if (!col.name.StartsWith("Col_")) continue;
            col.localPosition += deltaLocal;
        }
    }

    // =========================
    // Recycle: fixed belt edges (right -> left, left -> right)
    // =========================
    void RecycleByFixedEdges()
    {
        if (!edgesInited) return;
        if (biomes == null || biomes.Length == 0) return;

        float pad = stepLen * edgePaddingInStep;
        float rightLimit = rightEdgeProj + pad;
        float leftLimit = leftEdgeProj - pad;

        int done = 0;

        // 用 colIndex 对齐 Near/Mid/Far，保证“在一行”同步回收
        var masterMap = BuildIndexMap(transform);

        while (done < maxRecyclePerFrame)
        {
            if (!FindAnyOutOfEdge(masterMap, rightLimit, leftLimit, out int oldIndex, out bool moveToLeft))
                break;

            if (!masterMap.TryGetValue(oldIndex, out var masterCol)) break;

            // 右越界 -> 回左：减一整圈长度；左越界 -> 回右：加一整圈
            int newColIndex = moveToLeft ? (oldIndex - columnCount) : (oldIndex + columnCount);
            int biomeIndex = ComputeBiomeIndex(newColIndex);

            WrapOne(masterCol.t, masterCol.info, newColIndex, biomeIndex, ScatterBand.Near, moveToLeft);

            if (syncedRoots != null && syncedBands != null)
            {
                int n = Mathf.Min(syncedRoots.Length, syncedBands.Length);
                for (int i = 0; i < n; i++)
                {
                    var root = syncedRoots[i];
                    if (!root) continue;
                    if (root == transform) continue;

                    var map = BuildIndexMap(root);
                    if (!map.TryGetValue(oldIndex, out var pack)) continue;

                    WrapOne(pack.t, pack.info, newColIndex, biomeIndex, syncedBands[i], moveToLeft);
                }
            }

            // Sky（只做一次）：由你选择的 Near/Mid/Far gate 控制
            TryApplySkyByGate(biomeIndex);

            masterMap.Remove(oldIndex);
            masterMap[newColIndex] = masterCol;

            done++;
        }
    }

    void WrapOne(Transform col, ColumnInfo info, int newColIndex, int biomeIndex, ScatterBand targetBand, bool moveToLeft)
    {
        info.colIndex = newColIndex;
        info.biomeIndex = biomeIndex;

        col.name = $"Col_{newColIndex}";
        col.localRotation = Quaternion.identity;

        col.localPosition += axisLocal * (moveToLeft ? -loopLen : loopLen);

        mapGenerator.RegenerateColumn(col, biomeIndex, targetBand);
    }

    // =========================
    // Sky Gate Logic (key fix)
    // =========================
    void TryApplySkyByGate(int biomeIndex)
    {
        if (!skyApplier) return;
        if (biomes == null || biomes.Length == 0) return;
        if (biomeIndex < 0 || biomeIndex >= biomes.Length) return;

        // 关键：不再用 “当前组件的 transform.name == NearRoot” 这种脆弱条件
        // 而是：由你选择的 gate（Near/Mid/Far）决定是否允许切天。
        if (!IsSkyGateSatisfied())
            return;

        var biome = biomes[biomeIndex];
        if (biome && biome.sky)
        {
            skyApplier.skyConfig = biome.sky;
            skyApplier.Apply();

            if (debugSkyGate)
                Debug.Log($"[SkyGate] Apply biomeIndex={biomeIndex}, gate={skyGate}, by master={name}");
        }
        else
        {
            if (debugSkyGate)
                Debug.LogWarning($"[SkyGate] biomeIndex={biomeIndex} has no sky (gate={skyGate})");
        }
    }

    bool IsSkyGateSatisfied()
    {
        // 目标：你切换 skyGate 后必须立即生效
        // 方案：只要“本传送带系统里存在被选中的 gate 根”，就允许切天。
        // 由于切天代码运行在 master 上，因此 gate 判断不能写成 “this.band == gate”。
        // 我们从 syncedRoots/syncedBands 找到 gate 根；找不到再用名字兜底；都没有则退化到单根 band。

        ScatterBand wanted = ToScatterBand(skyGate);

        // 1) 优先用 syncedRoots+syncedBands（推荐、稳定）
        if (syncedRoots != null && syncedBands != null)
        {
            int n = Mathf.Min(syncedRoots.Length, syncedBands.Length);
            for (int i = 0; i < n; i++)
            {
                if (!syncedRoots[i]) continue;
                if (syncedBands[i] == wanted)
                {
                    if (debugSkyGate)
                        Debug.Log($"[SkyGate] Found gate via syncedBands: {syncedRoots[i].name} => {syncedBands[i]}");
                    return true;
                }
            }
        }

        // 2) 兜底：按名字（可选）
        if (useRootNameAsFallback)
        {
            string gateName =
                (wanted == ScatterBand.Near) ? "NearRoot" :
                (wanted == ScatterBand.Mid) ? "MidRoot" :
                "FarRoot";

            if (transform.name == gateName)
            {
                if (debugSkyGate)
                    Debug.Log($"[SkyGate] Using fallback name: this={transform.name} is gate={gateName}");
                return true;
            }

            if (syncedRoots != null)
            {
                for (int i = 0; i < syncedRoots.Length; i++)
                {
                    var r = syncedRoots[i];
                    if (!r) continue;
                    if (r.name == gateName)
                    {
                        if (debugSkyGate)
                            Debug.Log($"[SkyGate] Found gate via fallback name: {r.name}");
                        return true;
                    }
                }
            }
        }

        // 3) 单根模式（没有 syncedRoots 时）：用自身 band 判定
        bool okSingle = (band == wanted);
        if (debugSkyGate)
            Debug.Log($"[SkyGate] SingleRoot fallback: this.band={band}, wanted={wanted}, ok={okSingle}");
        return okSingle;
    }

    static ScatterBand ToScatterBand(SkyGateBand g)
    {
        switch (g)
        {
            case SkyGateBand.Near: return ScatterBand.Near;
            case SkyGateBand.Mid: return ScatterBand.Mid;
            case SkyGateBand.Far: return ScatterBand.Far;
            default: return ScatterBand.Near;
        }
    }

    // =========================
    // Helpers
    // =========================
    int CountColumns(Transform root)
    {
        int c = 0;
        for (int i = 0; i < root.childCount; i++)
            if (root.GetChild(i).name.StartsWith("Col_")) c++;
        return c;
    }

    void GetMinMaxProj(Transform root, out float minP, out float maxP)
    {
        minP = float.PositiveInfinity;
        maxP = float.NegativeInfinity;

        for (int i = 0; i < root.childCount; i++)
        {
            var col = root.GetChild(i);
            if (!col.name.StartsWith("Col_")) continue;

            float p = Vector3.Dot(col.localPosition, axisLocal);
            if (p < minP) minP = p;
            if (p > maxP) maxP = p;
        }

        if (!float.IsFinite(minP)) minP = 0f;
        if (!float.IsFinite(maxP)) maxP = 0f;
    }

    struct ColPack { public Transform t; public ColumnInfo info; }

    Dictionary<int, ColPack> BuildIndexMap(Transform root)
    {
        var dict = new Dictionary<int, ColPack>(64);
        for (int i = 0; i < root.childCount; i++)
        {
            var col = root.GetChild(i);
            if (!col.name.StartsWith("Col_")) continue;

            var info = col.GetComponent<ColumnInfo>();
            if (!info) continue;

            dict[info.colIndex] = new ColPack { t = col, info = info };
        }
        return dict;
    }

    bool FindAnyOutOfEdge(Dictionary<int, ColPack> map, float rightLimit, float leftLimit, out int colIndex, out bool moveToLeft)
    {
        colIndex = 0;
        moveToLeft = true;

        foreach (var kv in map)
        {
            var col = kv.Value.t;
            float p = Vector3.Dot(col.localPosition, axisLocal);

            if (p > rightLimit)
            {
                colIndex = kv.Key;
                moveToLeft = true;   // 右越界 -> 回左（减 loopLen）
                return true;
            }

            if (p < leftLimit)
            {
                colIndex = kv.Key;
                moveToLeft = false;  // 左越界 -> 回右（加 loopLen）
                return true;
            }
        }
        return false;
    }

    int ComputeBiomeIndex(int colIndex)
    {
        int len = (biomes != null) ? biomes.Length : 0;
        if (len <= 0) return 0;

        int cpb = Mathf.Max(1, columnsPerBiome);
        int block = Mathf.FloorToInt(colIndex / (float)cpb);
        int m = block % len;
        if (m < 0) m += len;
        return m;
    }
}
