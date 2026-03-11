using TMPro;
using UnityEngine;

public class StoryTaskUI : MonoBehaviour
{
    public TextMeshProUGUI text;

    [Header("Format")]
    public bool showStage = true;

    public void Apply(StoryTaskManager.Stage stage, int taskIndex)
    {
        if (text == null) return;

        // 你表格里的提示内容（按任务Index映射）
        string content = taskIndex switch
        {
            1 => "神经元链接1：了解你我的身份",
            2 => "神经元链接2：了解我们为什么来到这里",
            3 => "神经元链接3：了解我们所面临的困境",
            4 => "神经元链接4：翻看面板，提取有效信息和我讨论",
            5 => "神经元链接5：只有知道这些是什么，我才能帮你出去",
            6 => "神经元链接6：翻看日记，完成连线，拼凑记忆",
            7 => "神经元链接：西西弗斯",
            _ => "任务：-"
        };

        text.text = content;
    }
}
