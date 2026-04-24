using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class MapGenerator : MonoBehaviour
{
    [Header("Grid Size")]
    public int length = 18;
    public int farRows = 2;
    public int midRows = 2;
    public int nearRows = 2;

    public enum HexLayout { FlatTop, PointyTop }

    [Header("Hex")]
    public HexLayout layout = HexLayout.FlatTop;

    [Tooltip("手动半径（单位：世界单位）。当 useManualRadius=true 时，必定使用这个半径。")]
    public float hexRadius = 1f;

    [Tooltip("勾选后：强制使用 Hex Radius，不走 prefab 自动检测（你现在调半径没反应，就是缺这个）。")]
    public bool useManualRadius = true;

    [Tooltip("只缩放 Tile 预制体本身（不改变网格间距）。一般保持 1，用 hexRadius 调网格尺寸。")]
    public float tileScale = 1f;

    [Tooltip("整体间距缩放。1=理论紧贴；如果仍有缝，调到 0.98~0.995；如果重叠，调到 1.005~1.02")]
    [Range(0.95f, 1.05f)]
    public float spacingScale = 1f;

    [Header("Tile Prefab")]
    public GameObject tilePrefab;

    [Header("Biomes")]
    public BiomeConfig[] biomes;
    public int tilesPerBiome = 6;

    [Header("Sky (optional)")]
    public SkyApplier skyApplier;

    [Header("Root")]
    [Tooltip("不填则自动在本物体下创建 GeneratedMapRoot")]
    public Transform root;

    [Header("Options")]
    public float yHeight = 0f;
    public bool regenerateOnPlay = true;

    [Header("Editor Preview")]
    public bool previewInEditor = true;
    [Tooltip("编辑态改参数是否自动重建（大场景可关掉，手动右键 Generate）")]
    public bool autoRegenerateInEditor = true;

    [Header("Manual Placement")]
    [Tooltip("Generate/刷新时是否保留你手摆在 Manual 下的东西（建议一直开）")]
    public bool preserveManual = true;

    [Tooltip("当 length 变小，是否删除多余列（会连带删除这些列的 Manual；默认不删以免误伤）")]
    public bool pruneExtraColumns = false;

    [Header("Scatter")]
    public float propHeightOffset = 0.05f;

    [Header("Determinism")]
    [Tooltip("用于可复现随机。改这个会整体换一套地形/散布。")]
    public int worldSeed = 12345;

    [HideInInspector] public float detectedR;
    [HideInInspector] public float rowSpacingZ;         // r 方向步长（z）
    [HideInInspector] public float columnSpacingX;      // q 方向步长（x）
    [HideInInspector] public float columnTrackZOffset;  // 仅 FlatTop 用：q 对 z 的偏移（= sqrt3*R/2）

    [Header("Parallax Speeds (Per Layer)")]
    public float farSpeed = 1.5f;
    public float midSpeed = 3.0f;
    public float nearSpeed = 6.0f;

    [Header("Parallax Recycle")]
    public float recycleBehind = 60f;
    public int maxRecyclePerFrame = 8;

    [Header("Loop (Master, optional)")]
    [SerializeField] private ParallaxLoopAndSky loopMaster;

    [Header("Rendering Stability")]
    [Tooltip("给 Near/Mid/Far 三层一个极小的 Y 偏移，避免层间 Z-fighting 抖动。")]
    public bool applyBandYOffset = true;
    public float farYOffset = 0.00f;
    public float midYOffset = 0.01f;
    public float nearYOffset = 0.02f;

    // 生成的三层根名字（固定）
    const string FAR_ROOT = "FarRoot";
    const string MID_ROOT = "MidRoot";
    const string NEAR_ROOT = "NearRoot";

    // 列内四组子节点（固定）
    const string TILES_ROOT = "Tiles";
    const string MANUAL_ROOT = "Manual";
    const string AUTOPROPS_ROOT = "AutoProps";
    const string STORY_ROOT = "Story";


    const float SQRT3 = 1.7320508075688772f;

#if UNITY_EDITOR
    const double EditorGenerateDebounceSeconds = 0.25d;
    bool editorGenerateQueued;
    double lastEditorGenerateTime;
#endif

    // --------------------
    // Lifecycle
    // --------------------
    void Start()
    {
        if (Application.isPlaying)
        {
            if (regenerateOnPlay) Generate();
            BindLoopMasterAfterRootsCreated();
        }
    }

    void OnEnable()
    {
#if UNITY_EDITOR
        QueueEditorGenerate();
#endif
    }

    void OnValidate()
    {
#if UNITY_EDITOR
        QueueEditorGenerate();
#endif
    }

    void OnDisable()
    {
#if UNITY_EDITOR
        EditorApplication.delayCall -= EditorDeferredGenerate;
        editorGenerateQueued = false;
#endif
    }

#if UNITY_EDITOR
    bool ShouldAutoGenerateInEditor()
    {
        if (this == null || !isActiveAndEnabled) return false;
        if (Application.isPlaying) return false;
        if (!previewInEditor || !autoRegenerateInEditor) return false;
        if (!gameObject.scene.IsValid() || !gameObject.scene.isLoaded) return false;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return false;
        if (EditorApplication.isPlayingOrWillChangePlaymode) return false;
        return true;
    }

    void QueueEditorGenerate()
    {
        if (!ShouldAutoGenerateInEditor()) return;
        if (editorGenerateQueued) return;

        editorGenerateQueued = true;
        EditorApplication.delayCall -= EditorDeferredGenerate;
        EditorApplication.delayCall += EditorDeferredGenerate;
    }

    void EditorDeferredGenerate()
    {
        editorGenerateQueued = false;

        if (this == null) return;
        if (!ShouldAutoGenerateInEditor()) return;

        double now = EditorApplication.timeSinceStartup;
        if (now - lastEditorGenerateTime < EditorGenerateDebounceSeconds)
        {
            QueueEditorGenerate();
            return;
        }

        lastEditorGenerateTime = now;
        Generate();
    }
#endif

    void SafeDestroy(Object obj)
    {
        if (!obj) return;

        if (Application.isPlaying)
        {
            Destroy(obj);
        }
        else
        {
#if UNITY_EDITOR
            SafeDestroyImmediateDeferred(obj);
#else
            Destroy(obj);
#endif
        }
    }

#if UNITY_EDITOR
    void SafeDestroyImmediateDeferred(Object obj)
    {
        if (!obj) return;

        var captured = obj;
        EditorApplication.delayCall += () =>
        {
            if (captured == null) return;

            if (Application.isPlaying)
            {
                Destroy(captured);
                return;
            }

            DestroyImmediate(captured);
        };
    }
#endif

    [ContextMenu("Generate")]
    public void Generate()
    {
        if (!tilePrefab)
        {
            Debug.LogError("[MapGenerator] tilePrefab is null.");
            return;
        }
        if (biomes == null || biomes.Length == 0)
        {
            Debug.LogError("[MapGenerator] biomes is empty.");
            return;
        }

        EnsureRoot();

        // ====== Detect radius ======
        // 关键修复：useManualRadius=true 时，hexRadius 必定生效
        detectedR = useManualRadius ? Mathf.Max(0.0001f, hexRadius) : DetectRadiusFromPrefabWorld(tilePrefab, hexRadius);

        // ====== Layout-correct spacing ======
        float R = detectedR * Mathf.Max(0.0001f, spacingScale);

        if (layout == HexLayout.FlatTop)
        {
            // FlatTop axial(q,r):
            // x = 1.5R*q
            // z = sqrt(3)R*(r + q/2)
            columnSpacingX = 1.5f * R;
            rowSpacingZ = SQRT3 * R;
            columnTrackZOffset = 0.5f * rowSpacingZ; // = sqrt3*R/2
        }
        else // PointyTop
        {
            // PointyTop axial(q,r):
            // x = sqrt(3)R*(q + r/2)
            // z = 1.5R*r
            columnSpacingX = SQRT3 * R;
            rowSpacingZ = 1.5f * R;
            columnTrackZOffset = 0f; // PointyTop 列不应随 q 产生 z 偏移
        }

        // Ensure layer roots (do NOT nuke whole root, to preserve Manual)
        Transform farRoot = EnsureLayerRoot(FAR_ROOT, BandType.Far);
        Transform midRoot = EnsureLayerRoot(MID_ROOT, BandType.Mid);
        Transform nearRoot = EnsureLayerRoot(NEAR_ROOT, BandType.Near);

        // Add looper components (each root has its own speed) — 但最终只 Near 作为 Master
        AddOrGetLooper(farRoot, farSpeed, ScatterBand.Far);
        AddOrGetLooper(midRoot, midSpeed, ScatterBand.Mid);
        AddOrGetLooper(nearRoot, nearSpeed, ScatterBand.Near);

        int totalRows = Mathf.Max(0, farRows) + Mathf.Max(0, midRows) + Mathf.Max(0, nearRows);

        // Apply first sky for preview
        if (skyApplier && biomes[0] && biomes[0].sky)
        {
            skyApplier.skyConfig = biomes[0].sky;
            skyApplier.Apply();
        }

        // Optionally prune extra columns (dangerous for Manual)
        if (pruneExtraColumns)
        {
            PruneColumns(farRoot, length);
            PruneColumns(midRoot, length);
            PruneColumns(nearRoot, length);
        }

        // Build columns 0..length-1
        for (int q = 0; q < length; q++)
        {
            int biomeIndex = (q / Mathf.Max(1, tilesPerBiome)) % biomes.Length;
            BiomeConfig biome = biomes[biomeIndex];
            if (!biome) continue;

            Transform farCol = EnsureColumn(farRoot, q, biomeIndex);
            Transform midCol = EnsureColumn(midRoot, q, biomeIndex);
            Transform nearCol = EnsureColumn(nearRoot, q, biomeIndex);

            SetColumnLocalPos(farCol, q);
            SetColumnLocalPos(midCol, q);
            SetColumnLocalPos(nearCol, q);

            // Rebuild tiles for each band (ONLY under Tiles; keep Manual)
            RebuildTilesInColumn(farCol, totalRows, farRows, midRows, ScatterBand.Far);
            RebuildTilesInColumn(midCol, totalRows, farRows, midRows, ScatterBand.Mid);
            RebuildTilesInColumn(nearCol, totalRows, farRows, midRows, ScatterBand.Near);

            // Refresh visuals + props per band (deterministic)
            RegenerateColumn(farCol, biomeIndex, ScatterBand.Far);
            RegenerateColumn(midCol, biomeIndex, ScatterBand.Mid);
            RegenerateColumn(nearCol, biomeIndex, ScatterBand.Near);
        }

        BindLoopMasterAfterRootsCreated();
    }

    // --------------------
    // Root / Layer management
    // --------------------
    void EnsureRoot()
    {
        if (root) return;

        Transform existing = transform.Find("GeneratedMapRoot");
        if (existing)
        {
            root = existing;
            return;
        }

        var go = new GameObject("GeneratedMapRoot");
        go.transform.SetParent(transform, false);
        root = go.transform;
    }

    Transform EnsureLayerRoot(string name, BandType band)
    {
        Transform t = root.Find(name);
        if (!t)
        {
            t = new GameObject(name).transform;
            t.SetParent(root, false);
        }

        // Ensure grading exists (and apply)
        var grading = t.GetComponent<BandLayerGrading>();
        if (!grading) SetupBandGrading(t, band);
        else grading.ApplyToChildren();

        // 关键：层级 transform 本身保持干净
        t.localRotation = Quaternion.identity;
        t.localScale = Vector3.one;

        // 可选：给三层一个很小的 Y 偏移，消除层间 Z-fighting 抖动
        if (applyBandYOffset)
        {
            float y = 0f;
            if (name == FAR_ROOT) y = farYOffset;
            else if (name == MID_ROOT) y = midYOffset;
            else if (name == NEAR_ROOT) y = nearYOffset;
            t.localPosition = new Vector3(0f, y, 0f);
        }
        else
        {
            t.localPosition = Vector3.zero;
        }

        return t;
    }

    void PruneColumns(Transform layerRoot, int keepLength)
    {
        for (int i = layerRoot.childCount - 1; i >= 0; i--)
        {
            var c = layerRoot.GetChild(i);
            if (!c.name.StartsWith("Col_")) continue;

            int q;
            if (TryParseColIndex(c.name, out q))
            {
                if (q >= keepLength)
                    SafeDestroy(c.gameObject);
            }
        }
    }

    bool TryParseColIndex(string colName, out int q)
    {
        q = 0;
        int idx = colName.IndexOf('_');
        if (idx < 0 || idx == colName.Length - 1) return false;
        return int.TryParse(colName.Substring(idx + 1), out q);
    }

 
    void AddOrGetLooper(Transform layerRoot, float layerSpeed, ScatterBand band)
    {
        var looper = layerRoot.GetComponent<ParallaxLoopAndSky>();
        if (!looper) looper = layerRoot.gameObject.AddComponent<ParallaxLoopAndSky>();

        looper.mapGenerator = this;
        looper.biomes = biomes;
        looper.columnsPerBiome = tilesPerBiome;
        looper.skyApplier = skyApplier;

        looper.speed = layerSpeed;
        looper.recycleBehind = recycleBehind;
        looper.maxRecyclePerFrame = maxRecyclePerFrame;
        looper.band = band;

        // 关键修复：先全部降为非 Master，最后在 BindLoopMasterAfterRootsCreated 里只让 Near 变 Master
        looper.isMaster = false;
    }

    void BindLoopMasterAfterRootsCreated()
    {
        if (!root) return;

        Transform near = root.Find(NEAR_ROOT);
        Transform mid = root.Find(MID_ROOT);
        Transform far = root.Find(FAR_ROOT);

        if (!near) return;

        // 强制：只有 NearRoot 作为 Master
        var nearLoop = near.GetComponent<ParallaxLoopAndSky>();
        var midLoop = mid ? mid.GetComponent<ParallaxLoopAndSky>() : null;
        var farLoop = far ? far.GetComponent<ParallaxLoopAndSky>() : null;

        if (midLoop) midLoop.isMaster = false;
        if (farLoop) farLoop.isMaster = false;

        loopMaster = nearLoop ? nearLoop : loopMaster;
        if (!loopMaster) return;

        loopMaster.isMaster = true;
        loopMaster.mapGenerator = this;
        loopMaster.mainCamera = Camera.main;

        var roots = new List<Transform>(2);
        var bands = new List<ScatterBand>(2);

        if (mid) { roots.Add(mid); bands.Add(ScatterBand.Mid); }
        if (far) { roots.Add(far); bands.Add(ScatterBand.Far); }

        loopMaster.syncedRoots = roots.ToArray();
        loopMaster.syncedBands = bands.ToArray();
        loopMaster.band = ScatterBand.Near;
    }

    

    Transform EnsureChild(Transform parent, string childName)
    {
        var c = parent.Find(childName);
        if (!c)
        {
            c = new GameObject(childName).transform;
            c.SetParent(parent, false);
            c.localPosition = Vector3.zero;
            c.localRotation = Quaternion.identity;
            c.localScale = Vector3.one;
        }
        return c;
    }

    Transform EnsureColumn(Transform layerRoot, int q, int biomeIndex)
    {
        string name = $"Col_{q}";
        Transform col = layerRoot.Find(name);
        if (!col)
        {
            col = new GameObject(name).transform;
            col.SetParent(layerRoot, false);
            col.localPosition = Vector3.zero;
            col.localRotation = Quaternion.identity;
            col.localScale = Vector3.one;
        }

        // Tiles / Manual / AutoProps / Story默认关闭
        EnsureChild(col, TILES_ROOT);
        EnsureChild(col, MANUAL_ROOT);
        EnsureChild(col, AUTOPROPS_ROOT);
        var story = EnsureChild(col, STORY_ROOT);
        if (story) story.gameObject.SetActive(false);

        return col;
    }


    void SetColumnLocalPos(Transform col, int q)
    {
        float zOff = (layout == HexLayout.FlatTop) ? (columnTrackZOffset * q) : 0f;

        col.localPosition = new Vector3(
            columnSpacingX * q,
            0f,
            zOff
        );
        col.localRotation = Quaternion.identity;
        col.localScale = Vector3.one;
    }

    // Rebuild tiles under Tiles only; keep Manual untouched.
    void RebuildTilesInColumn(Transform col, int totalRows, int farRows_, int midRows_, ScatterBand band)
    {
        Transform tilesRoot = col.Find(TILES_ROOT);
        if (!tilesRoot) tilesRoot = EnsureChild(col, TILES_ROOT);

        // Clear old tiles
        for (int i = tilesRoot.childCount - 1; i >= 0; i--)
            SafeDestroy(tilesRoot.GetChild(i).gameObject);

        // Build tiles belonging to THIS band only:
        int rStart = 0, rEnd = 0;
        if (band == ScatterBand.Far)
        {
            rStart = 0; rEnd = Mathf.Max(0, farRows_);
        }
        else if (band == ScatterBand.Mid)
        {
            rStart = Mathf.Max(0, farRows_);
            rEnd = Mathf.Max(rStart, farRows_ + Mathf.Max(0, midRows_));
        }
        else
        {
            rStart = Mathf.Max(0, farRows_ + Mathf.Max(0, midRows_));
            rEnd = Mathf.Max(rStart, totalRows);
        }

        // Compute R consistently (same as Generate)
        float R = detectedR * Mathf.Max(0.0001f, spacingScale);

        for (int r = rStart; r < rEnd; r++)
        {
            GameObject tile = Instantiate(tilePrefab, tilesRoot);
            tile.name = $"Hex_r{r}";

            float xLocal = 0f;
            float zLocal = rowSpacingZ * r;

            if (layout == HexLayout.PointyTop)
            {
                xLocal = (SQRT3 * R) * 0.5f * r; // = sqrt3*R*(r/2)
            }

            tile.transform.localPosition = new Vector3(xLocal, yHeight, zLocal);
            tile.transform.localRotation = Quaternion.identity;
            tile.transform.localScale = Vector3.one * Mathf.Max(0.0001f, tileScale);
        }
    }

    // --------------------
    // Column regeneration
    // --------------------
    public void RegenerateColumn(Transform col, int biomeIndex, ScatterBand band)
    {
        if (!col) return;
        if (biomes == null || biomes.Length == 0) return;

        biomeIndex = Mathf.Clamp(biomeIndex, 0, biomes.Length - 1);
        var biome = biomes[biomeIndex];
        if (!biome) return;

        int colIndex = 0;
        var info = col.GetComponent<ColumnInfo>();
        if (info) colIndex = info.colIndex;

        int seed = ComputeSeed(worldSeed, colIndex, band);

        var oldState = Random.state;
        Random.InitState(seed);

        ApplyTileLookForBand(col, biome, band);
        RespawnPropsForBand(col, biome, band);

        Random.state = oldState;

        var grading = col.GetComponentInParent<BandLayerGrading>();
        if (grading) grading.ApplyToChildren();
    }

    int ComputeSeed(int baseSeed, int colIndex, ScatterBand band)
    {
        unchecked
        {
            int h = baseSeed;
            h = h * 31 + colIndex;
            h = h * 31 + (int)band * 1013;
            return h;
        }
    }

    void ApplyTileLookForBand(Transform col, BiomeConfig biome, ScatterBand band)
    {
        Transform tilesRoot = col.Find(TILES_ROOT);
        if (!tilesRoot) return;

        for (int i = 0; i < tilesRoot.childCount; i++)
        {
            Transform child = tilesRoot.GetChild(i);

            var mr = child.GetComponentInChildren<MeshRenderer>(true);
            var mf = child.GetComponentInChildren<MeshFilter>(true);

            if (mf && biome.meshes != null && biome.meshes.Length > 0)
                mf.sharedMesh = biome.meshes[Random.Range(0, biome.meshes.Length)];

            if (mr)
            {
                if (band == ScatterBand.Far && biome.farMat) mr.sharedMaterial = biome.farMat;
                else if (band == ScatterBand.Mid && biome.midMat) mr.sharedMaterial = biome.midMat;
                else if (band == ScatterBand.Near && biome.nearMat) mr.sharedMaterial = biome.nearMat;
            }
        }
    }

    void RespawnPropsForBand(Transform col, BiomeConfig biome, ScatterBand band)
    {
        // IMPORTANT: ONLY touch AutoProps. Never clear Manual.
        Transform propsRoot = col.Find(AUTOPROPS_ROOT);
        if (!propsRoot) propsRoot = EnsureChild(col, AUTOPROPS_ROOT);

        // Clear old auto props
        for (int i = propsRoot.childCount - 1; i >= 0; i--)
            SafeDestroy(propsRoot.GetChild(i).gameObject);

        // Gather tile anchors from Tiles only
        Transform tilesRoot = col.Find(TILES_ROOT);
        if (!tilesRoot) return;

        List<Transform> tiles = new List<Transform>(32);
        for (int i = 0; i < tilesRoot.childCount; i++)
            tiles.Add(tilesRoot.GetChild(i));

        if (tiles.Count == 0) return;

        var rules = biome.GetRulesFor(band);
        if (rules == null || rules.Length == 0) return;

        for (int ri = 0; ri < rules.Length; ri++)
        {
            var rule = rules[ri];
            if (rule == null || !rule.enabled || rule.prefab == null) continue;

            int count = rule.GetCount();
            if (count <= 0) continue;

            for (int k = 0; k < count; k++)
            {
                if (Random.value > Mathf.Clamp01(rule.probability)) continue;

                Transform anchor = tiles[Random.Range(0, tiles.Count)];
                Vector3 basePos = anchor.localPosition;

                float ox = Random.Range(-rule.randomOffsetXZ.x, rule.randomOffsetXZ.x);
                float oz = Random.Range(-rule.randomOffsetXZ.y, rule.randomOffsetXZ.y);

                Vector3 pos = basePos + new Vector3(ox, 0f, oz);
                pos.y = yHeight + propHeightOffset + rule.yOffset;

                var go = Instantiate(rule.prefab, propsRoot);
                go.transform.localPosition = pos;

                if (rule.randomYaw)
                {
                    float yaw = Random.Range(0f, 360f);
                    go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                }
                else
                {
                    go.transform.localRotation = Quaternion.identity;
                }

                float s = Random.Range(rule.randomScale.x, rule.randomScale.y);
                go.transform.localScale = Vector3.one * s;
            }
        }
    }

    // --------------------
    // Radius detection (robust)
    // --------------------
    float DetectRadiusFromPrefabWorld(GameObject prefab, float fallback)
    {
        if (!prefab) return fallback;

        GameObject tmp = null;
        try
        {
            tmp = Instantiate(prefab);
            tmp.hideFlags = HideFlags.HideAndDontSave;
            tmp.transform.position = Vector3.zero;
            tmp.transform.rotation = Quaternion.identity;
            tmp.transform.localScale = Vector3.one;

            var renderers = tmp.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0) return fallback;

            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                b.Encapsulate(renderers[i].bounds);

            float R = Mathf.Max(b.extents.x, b.extents.z);
            if (R > 1e-4f) return R;
            return fallback;
        }
        finally
        {
            if (tmp) SafeDestroy(tmp);
        }
    }

    // --------------------
    // Band grading
    // --------------------
    enum BandType { Far, Mid, Near }

    void SetupBandGrading(Transform bandRoot, BandType band)
    {
        var grading = bandRoot.GetComponent<BandLayerGrading>();
        if (!grading) grading = bandRoot.gameObject.AddComponent<BandLayerGrading>();

        switch (band)
        {
            case BandType.Near:
                grading.fogStrength = 0.05f;
                grading.saturation = 1.05f;
                grading.contrast = 1.10f;
                grading.brightness = 1.05f;
                grading.fogColor = new Color(0.70f, 0.80f, 0.90f);
                break;

            case BandType.Mid:
                grading.fogStrength = 0.20f;
                grading.saturation = 0.90f;
                grading.contrast = 0.95f;
                grading.brightness = 1.00f;
                grading.fogColor = new Color(0.65f, 0.75f, 0.85f);
                break;

            case BandType.Far:
                grading.fogStrength = 0.45f;
                grading.saturation = 0.70f;
                grading.contrast = 0.85f;
                grading.brightness = 0.95f;
                grading.fogColor = new Color(0.60f, 0.70f, 0.80f);
                break;
        }

        grading.ApplyToChildren();
    }
}
