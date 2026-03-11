// MemoryBoardScenario.cs
using UnityEngine;

[CreateAssetMenu(menuName = "SANE/MemoryBoard Scenario", fileName = "MBScenario_")]
public class MemoryBoardScenario : ScriptableObject
{
    [Header("Display Texts (6 nodes)")]
    public string[] nodeTexts = new string[6]; // 必须长度=6

    [Header("Correct Sequence (index of nodes, length=4)")]
    [Tooltip("一次连线尝试的正确顺序：4个索引，每个范围 0..5，对应 nodesByIndex 的顺序。")]
    public int[] correctIndexSequence = new int[4];

    // =========================
    // ✅ Clue Words (2 of 6)
    // =========================
    [Header("Clues (2 nodes from the 6)")]
    [Tooltip("两条线索分别绑定到哪个节点 index（0..5）。长度建议=2。")]
    public int[] clueNodeIndices = new int[2] { 0, 1 };

    [Tooltip("对应两条线索内容（显示在指定 UI 文本区域）。长度建议=2。")]
    [TextArea(2, 8)]
    public string[] clueContents = new string[2];

    [Tooltip("点击线索词时是否弹 Toast。")]
    public bool toastOnClueClicked = false;

    [TextArea(1, 3)]
    public string clueToastText = "线索已记录";

    // =========================
    // ✅ Completion
    // =========================
    // =========================
    // ✅ Completion
    // =========================
    [Header("On Completed")]
    [Tooltip("连线成功时弹的 toast（可为空表示不弹）。")]

    [Header("On Completed")]
    [TextArea(1, 3)]
    public string completedToastText = "记忆连线已闭合";

    [Tooltip("连线成功发放的记忆碎片（ItemKind.MemoryShard），并且建议 excludeFromShardPool=true")]
    public ItemData linkedRewardItem;

    [Tooltip("可选：连线成功后追加一条语料到该碎片 logs（为空则不追加）")]
    [TextArea(2, 10)]
    public string rewardLogLine;


}