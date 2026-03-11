using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 背包 UI 点击失败诊断工具
/// 挂在任意 GameObject 上即可，Inspector 里绑好 scanTarget 和 virtualCursorImage
/// </summary>
public class UIRaycastDiagnostics : MonoBehaviour
{
    [Header("Targets")]
    public Transform scanTarget;         // 背包 Root 或某个道具条目
    public Image virtualCursorImage; // 虚拟鼠标的 Image 组件

    [Header("Options")]
    public bool autoFix = false;  // true = 自动修复 BlocksRaycasts/Interactable
    public bool runEverySecond = true;   // true = 每秒扫一次（调试用）

    float _timer;

    void Start() => Run();

    void Update()
    {
        if (!runEverySecond) return;
        _timer += Time.unscaledDeltaTime;
        if (_timer >= 1f) { _timer = 0f; Run(); }
    }

    [ContextMenu("Run Diagnostics Now")]
    public void Run()
    {
        if (scanTarget == null) { Debug.LogWarning("[Diagnostics] scanTarget 未赋值"); return; }

        Debug.Log($"[Diagnostics] ══════ 开始扫描：{scanTarget.name} ══════");

        // ── 1. 扫描父链 CanvasGroup ──────────────────────────────────────────
        Debug.Log("[Diagnostics] ── CanvasGroup 父链 ──");
        bool cgProblem = false;
        Transform cur = scanTarget;
        while (cur != null)
        {
            var cg = cur.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                bool bad = !cg.blocksRaycasts || !cg.interactable || cg.alpha < 0.01f;
                Debug.Log($"  [{(bad ? "⚠ 问题" : "✓")}] {cur.name}  " +
                          $"BlocksRaycasts={cg.blocksRaycasts}  Interactable={cg.interactable}  Alpha={cg.alpha:F2}");
                if (bad)
                {
                    cgProblem = true;
                    if (autoFix)
                    {
                        if (!cg.blocksRaycasts) { cg.blocksRaycasts = true; Debug.Log("    → 已修复 BlocksRaycasts=true"); }
                        if (!cg.interactable) { cg.interactable = true; Debug.Log("    → 已修复 Interactable=true"); }
                    }
                }
            }
            cur = cur.parent;
        }
        if (!cgProblem) Debug.Log("  ✓ 父链 CanvasGroup 无问题");

        // ── 2. 检查 Mask / RectMask2D（会裁剪 Raycast）──────────────────────
        Debug.Log("[Diagnostics] ── Mask 检查 ──");
        var masks = scanTarget.GetComponentsInParent<Mask>(true);
        var rectMasks = scanTarget.GetComponentsInParent<RectMask2D>(true);
        if (masks.Length == 0 && rectMasks.Length == 0)
        {
            Debug.Log("  ✓ 父链无 Mask/RectMask2D");
        }
        else
        {
            foreach (var m in masks)
                Debug.Log($"  [info] Mask 在 {m.name}，showMaskGraphic={m.showMaskGraphic}（正常，不影响点击）");
            foreach (var m in rectMasks)
                Debug.Log($"  [info] RectMask2D 在 {m.name}（正常，不影响点击，只裁剪显示）");
        }

        // ── 3. 检查虚拟鼠标 Image ────────────────────────────────────────────
        Debug.Log("[Diagnostics] ── 虚拟鼠标 Image ──");
        if (virtualCursorImage != null)
        {
            if (virtualCursorImage.raycastTarget)
                Debug.LogWarning("  ⚠ 虚拟鼠标 Image.raycastTarget = true！它会挡住下层 UI！请关闭！");
            else
                Debug.Log("  ✓ 虚拟鼠标 Image.raycastTarget 已关闭");
        }
        else Debug.LogWarning("  virtualCursorImage 未赋值，跳过");

        // ── 4. EventSystem ───────────────────────────────────────────────────
        Debug.Log("[Diagnostics] ── EventSystem ──");
        if (UnityEngine.EventSystems.EventSystem.current == null)
            Debug.LogError("  ⚠ 场景里没有 EventSystem！");
        else
            Debug.Log($"  ✓ EventSystem: {UnityEngine.EventSystems.EventSystem.current.name}");

        // ── 5. Canvas + GraphicRaycaster ────────────────────────────────────
        Debug.Log("[Diagnostics] ── Canvas & GraphicRaycaster ──");
        var canvas = scanTarget.GetComponentInParent<Canvas>(true);
        if (canvas == null)
        {
            Debug.LogError("  ⚠ scanTarget 不在任何 Canvas 下！");
        }
        else
        {
            Debug.Log($"  Canvas: {canvas.name}  renderMode={canvas.renderMode}  " +
                      $"sortOrder={canvas.sortingOrder}");
            var gr = canvas.GetComponent<GraphicRaycaster>();
            if (gr == null)
                Debug.LogError($"  ⚠ Canvas '{canvas.name}' 没有 GraphicRaycaster！");
            else
                Debug.Log($"  ✓ GraphicRaycaster 存在");
        }

        // ── 6. 扫描 scanTarget 下所有 Graphic，检查 raycastTarget ───────────
        Debug.Log("[Diagnostics] ── 子节点 Graphic.raycastTarget 统计 ──");
        var graphics = scanTarget.GetComponentsInChildren<Graphic>(true);
        int onCount = 0, offCount = 0;
        foreach (var g in graphics)
        {
            if (g.raycastTarget) onCount++;
            else offCount++;
        }
        Debug.Log($"  共 {graphics.Length} 个 Graphic：raycastTarget=ON 的 {onCount} 个，OFF 的 {offCount} 个");
        if (onCount == 0)
            Debug.LogWarning("  ⚠ 所有子 Graphic 的 raycastTarget 都关了！背包内 UI 将无法被点击！");

        Debug.Log("[Diagnostics] ══════ 扫描完毕 ══════");
    }
}