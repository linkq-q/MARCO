using UnityEngine;
using TMPro;

public class MemoryBoardTextBinder : MonoBehaviour
{
    [Header("Scenario")]
    public MemoryBoardScenario scenario;

    [Header("Node Text Refs (size=6)")]
    public TextMeshProUGUI[] nodeTMPS = new TextMeshProUGUI[6];

    [ContextMenu("Apply Scenario Now")]
    public void ApplyScenario()
    {
        if (!scenario)
        {
            Debug.LogWarning("[MemoryBoardTextBinder] scenario is null", this);
            return;
        }

        if (scenario.nodeTexts == null || scenario.nodeTexts.Length != 6)
        {
            Debug.LogError("[MemoryBoardTextBinder] scenario.nodeTexts 必须长度=6", this);
            return;
        }

        if (nodeTMPS == null || nodeTMPS.Length != 6)
        {
            Debug.LogError("[MemoryBoardTextBinder] nodeTMPS 必须长度=6", this);
            return;
        }

        for (int i = 0; i < 6; i++)
        {
            if (!nodeTMPS[i]) continue;
            nodeTMPS[i].text = scenario.nodeTexts[i] ?? "";
        }
    }

    void Start()
    {
        // 自动应用一次
        ApplyScenario();
    }

    // 运行时切换情景用
    public void SetScenario(MemoryBoardScenario s)
    {
        scenario = s;
        ApplyScenario();
    }
}