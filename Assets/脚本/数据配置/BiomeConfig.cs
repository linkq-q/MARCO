using System;
using UnityEngine;

public enum ScatterBand
{
    Far,
    Mid,
    Near
}

[Serializable]
public class PrefabSpawnRule
{
    [Header("What to spawn")]
    public string name;                 // 仅用于 Inspector 里好读
    public GameObject prefab;
    public bool enabled = true;

    [Header("How many per column")]
    [Min(0)] public int countPerColumn = 0;  // 固定数量（当 useRandomCount=false 时使用）
    public bool useRandomCount = false;
    public Vector2Int countRange = new Vector2Int(0, 0); // 随机范围（含两端）

    [Header("Placement randomization")]
    public Vector2 randomOffsetXZ = new Vector2(0.3f, 0.3f); // XZ 平面偏移
    public float yOffset = 0f;                               // 额外高度偏移（可用于略微浮起）
    public bool randomYaw = true;                            // 随机绕Y旋转
    public Vector2 randomScale = new Vector2(1f, 1f);         // 均匀缩放范围[min,max]

    [Header("Optional filtering")]
    public ScatterBand band = ScatterBand.Near;              // 规则归属层（Near/Mid/Far）
    [Range(0f, 1f)] public float probability = 1f;           // 每次尝试生成的概率（可做稀疏）

    /// <summary>返回本列本规则要刷多少个</summary>
    public int GetCount()
    {
        if (!enabled || prefab == null) return 0;

        if (useRandomCount)
        {
            int min = Mathf.Min(countRange.x, countRange.y);
            int max = Mathf.Max(countRange.x, countRange.y);
            return Mathf.Max(0, UnityEngine.Random.Range(min, max + 1));
        }

        return Mathf.Max(0, countPerColumn);
    }
}

[CreateAssetMenu(fileName = "BiomeConfig", menuName = "Config/Biome Config")]
public class BiomeConfig : ScriptableObject
{
    [Header("Tile Look")]
    public Mesh[] meshes;
    public Material farMat;
    public Material midMat;
    public Material nearMat;

    [Header("Sky")]
    public SkyColorConfig sky;

    [Header("Spawn Rules (freely add/remove in Inspector)")]
    public PrefabSpawnRule[] spawnRules;

    // -------------------- 兼容旧字段（可选保留） --------------------
    // 你如果已经在项目里用到了旧字段，不想立刻改生成器脚本，可以先保留。
    // 等你把 MapGenerator 的刷物逻辑改为读取 spawnRules 后，再删除这些旧字段。

    [Header("Legacy (optional)")]
    public bool hasTree = false;
    public GameObject treePrefab;
    public int treePerColumn = 0;

    public bool hasStone = false;
    public GameObject stonePrefab;
    public int stonePerColumn = 0;

    public Vector2 randomOffsetXZ = new Vector2(0.3f, 0.3f);

    /// <summary>
    /// 给生成器用：按层获取规则（Near/Mid/Far）
    /// </summary>
    public PrefabSpawnRule[] GetRulesFor(ScatterBand band)
    {
        if (spawnRules == null) return Array.Empty<PrefabSpawnRule>();

        // 为了避免GC，这里简单返回过滤后的新数组也行；
        // 如果你很在意GC，可以让生成器在Start时缓存。
        int n = 0;
        for (int i = 0; i < spawnRules.Length; i++)
        {
            var r = spawnRules[i];
            if (r != null && r.enabled && r.prefab != null && r.band == band) n++;
        }

        if (n == 0) return Array.Empty<PrefabSpawnRule>();

        var arr = new PrefabSpawnRule[n];
        int k = 0;
        for (int i = 0; i < spawnRules.Length; i++)
        {
            var r = spawnRules[i];
            if (r != null && r.enabled && r.prefab != null && r.band == band)
                arr[k++] = r;
        }
        return arr;
    }
}
