using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 任务推进管理器：按顺序推进任务1~7，并提供判定入口
/// - 阶段0：通过“AI说过的语料”判定推进（先用关键词，本地即可跑通）
/// - 阶段1：打开面板推进
/// - 阶段2/3：预留外部调用（你已有阶段判定逻辑时直接调用即可）
/// </summary>
public class StoryTaskManager : MonoBehaviour
{
    public enum Stage { Stage0, Stage1, Stage2, Stage3Plus }

    [Header("State (ReadOnly)")]
    public Stage stage = Stage.Stage0;
    [Tooltip("当前任务序号（1~7）")]
    public int taskIndex = 1;

    [Header("UI Hook (Optional)")]
    public StoryTaskUI taskUI; // 可选：拖左上角任务提示UI脚本

    [Header("Debug")]
    public bool logDebug = false;

    // ======= 事件：留接口给你后面做“任务完成动画/引导句/解锁” =======
    public event Action<Stage, int> OnTaskChanged;      // (stage, taskIndex)
    public event Action<Stage> OnStageChanged;          // stage
    public event Action<int> OnTaskCompleted;           // taskIndex

    public event Action<int> OnTaskEntered; // 进入某个任务号
    private int _currentTask = 0;

    // ======= 内部缓存：记录AI说过的语料（用于阶段0判定） =======
    readonly List<string> _aiLines = new List<string>(64);

    // ======= 可选：二次传送判定（外部Judge）接口 =======
    // 你后面如果做“二次传送判定=云端判断”，就把这个函数赋值为你自己的判定方法：
    // (key, allText) => true/false
    public Func<string, string, bool> externalJudgeSync;

    void Start()
    {
        EnsureHintManager();
        RefreshUI();
    }

    // =====================================================================
    // 外部调用入口：把 AI 的发言喂进来
    // 你在显示AI文字的那一刻调用即可（不管是自动发言还是回复玩家）
    // =====================================================================
    public void RegisterAIDialogue(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        _aiLines.Add(line.Trim());

        if (logDebug)
            Debug.Log($"[StoryTask] AI Line: {line}");

        // 阶段0的推进条件依赖AI语料
        if (stage == Stage.Stage0)
            TryAdvanceByStage0Rules();
    }

    // =====================================================================
    // 外部调用入口：玩家打开面板（阶段1任务4->5）
    // =====================================================================
    public void NotifyPanelOpened()
    {
        if (stage == Stage.Stage1 && taskIndex == 4)
        {
            CompleteTaskAndNext(5);
        }
    }

    // =====================================================================
    // 外部调用入口：阶段判定同步点（你说 5-6 与 1->2 同步；6-7 与 2->3 同步）
    // 你已有“阶段判定正在做”，只要在判定成立时调用这两个函数即可
    // =====================================================================
    public void NotifyEnterStage2()
    {
        // 阶段1 -> 阶段2 同步推进（任务5->6）
        // 放宽条件：只要还没到任务6，就直接推进，避免taskIndex未到5时漏触发
        if (taskIndex >= 6) return;
        stage = Stage.Stage2;
        taskIndex = 6;
        EnterTask(taskIndex);
        FireStageChanged();
        FireTaskChanged();
    }
    public void EnterTask(int taskIndex)
    {
        if (taskIndex == _currentTask) return;
        _currentTask = taskIndex;
        OnTaskEntered?.Invoke(_currentTask);
    }

    public void NotifyEnterStage3()
    {
        // 阶段2 -> 阶段3+ 同步推进（任务6->7）
        if (stage == Stage.Stage2 && taskIndex == 6)
        {
            stage = Stage.Stage3Plus;
            taskIndex = 7;
            FireStageChanged();
            FireTaskChanged();
        }
    }

    // =====================================================================
    // 阶段0规则：按顺序推进任务1~3（你表格里的 1-2 / 2-3 / 3-4）
    // 注意：你现在阶段0显示三个子任务，但我们用 taskIndex=1/2/3 来推进即可
    // =====================================================================
    void TryAdvanceByStage0Rules()
    {
        string all = string.Join("\n", _aiLines);

        // 任务1->2：需要两点：AI说明自己是Echo；AI说明玩家是陈末
        if (taskIndex == 1)
        {
            bool ok = Judge("s0_t1", all,
                mustContainAll: null, // 先用关键字
                containAny: new[] { "Echo", "陈末" });

            if (ok) CompleteTaskAndNext(2);
            return;
        }

        // 任务2->3：AI说明虫洞跃迁失败
        if (taskIndex == 2)
        {
            bool ok = Judge("s0_t2", all,
                mustContainAll: null,
                containAny: new[] { "跃迁", "失败", "虫洞" });

            if (ok) CompleteTaskAndNext(3);
            return;
        }

        // 任务3->阶段1：两者满足一个：循环 / 找不到星舰残骸
        if (taskIndex == 3)
        {
            bool ok = Judge("s0_t3", all,
                mustContainAll: null,
                containAny: new[] { "循环", "残骸" });

            if (ok)
            {
                stage = Stage.Stage1;
                taskIndex = 4;
                FireStageChanged();
                FireTaskChanged();

                // ✅ 新增：进入任务4
                EnterTask(taskIndex);
            }
            return;
        }
    }

    // =====================================================================
    // 判定函数：优先用 externalJudgeSync（你后面接云端），否则用本地关键词
    // =====================================================================
    bool Judge(string judgeKey, string allText, string[] mustContainAll, string[] containAny)
    {
        if (externalJudgeSync != null)
        {
            try
            {
                if (externalJudgeSync.Invoke(judgeKey, allText)) return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[StoryTask] externalJudgeSync error key={judgeKey} {e}");
            }
        }

        // 本地关键词判定（先跑通）
        if (mustContainAll != null)
        {
            foreach (var k in mustContainAll)
            {
                if (string.IsNullOrEmpty(k)) continue;
                if (!ContainsIgnoreCase(allText, k)) return false;
            }
        }

        if (containAny != null && containAny.Length > 0)
        {
            foreach (var k in containAny)
            {
                if (string.IsNullOrEmpty(k)) continue;
                if (ContainsIgnoreCase(allText, k)) return true;
            }
            return false;
        }

        return true;
    }

    static bool ContainsIgnoreCase(string s, string key)
    {
        return s != null && key != null &&
               s.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    void CompleteTaskAndNext(int nextTaskIndex)
    {
        OnTaskCompleted?.Invoke(taskIndex);

        if (logDebug)
            Debug.Log($"[StoryTask] Complete task {taskIndex} -> {nextTaskIndex}");

        taskIndex = nextTaskIndex;

        // ✅ 新增：进入新任务事件（AIBroker 监听的就是这个）
        EnterTask(taskIndex);

        FireTaskChanged();
    }

    void FireTaskChanged()
    {
        RefreshUI();
        OnTaskChanged?.Invoke(stage, taskIndex);
    }

    void FireStageChanged()
    {
        RefreshUI();
        OnStageChanged?.Invoke(stage);
        if (logDebug)
            Debug.Log($"[StoryTask] Stage changed: {stage}");
    }

    void RefreshUI()
    {
        if (taskUI != null)
            taskUI.Apply(stage, taskIndex);
    }

    void EnsureHintManager()
    {
        if (UIHintManager.I != null) return;
        Debug.LogWarning("[StoryTaskManager] UIHintManager not found in scene. 请在 Hierarchy 的 UI/拾取提示/ 下放置 UIHintManager 并赋值三个 CanvasGroup 引用。", this);
    }
}
