using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 顶部交互提示UI（兼容版）：
/// - 单句：打开 SingleRoot
/// - 二选一：打开 ChoiceRoot
/// 注意：这里不再依赖 root 字段，避免你Inspector没绑导致不显示
/// </summary>
public class InteractionTopUI : MonoBehaviour
{
    [Header("Single Line")]
    public GameObject singleRoot;
    public TextMeshProUGUI singleText;

    [Header("Choice")]
    public GameObject choiceRoot;
    public TextMeshProUGUI optionAText;
    public TextMeshProUGUI optionBText;
    public Button optionAButton;
    public Button optionBButton;

    [Header("Behavior")]
    public float autoHideSeconds = 2.0f; // 中文注释：单句显示多久自动消失

    Action onChooseA;
    Action onChooseB;

    float hideAt;
    bool waitingChoice;

    void Awake()
    {
        // 初始全部隐藏
        if (singleRoot) singleRoot.SetActive(false);
        if (choiceRoot) choiceRoot.SetActive(false);

        if (optionAButton) optionAButton.onClick.AddListener(() => Choose(true));
        if (optionBButton) optionBButton.onClick.AddListener(() => Choose(false));
    }

    void Update()
    {
        // 单句播完自动消失（用unscaledTime，避免Time.timeScale=0时不走）
        if (!waitingChoice && singleRoot && singleRoot.activeSelf && hideAt > 0f && Time.unscaledTime >= hideAt)
        {
            Hide();
        }
    }

    public void ShowSingleLine(string text)
    {
        Debug.Log("[TopUI] ShowSingleLine called");
        //先都置空
        waitingChoice = false;
        onChooseA = null;
        onChooseB = null;
        //两个状态中选择一个开启
        if (choiceRoot) choiceRoot.SetActive(false);
        if (singleRoot) singleRoot.SetActive(true);

        if (singleText) singleText.text = text;
        //设定自动隐藏时间
        hideAt = Time.unscaledTime + Mathf.Max(0.1f, autoHideSeconds);
    }

    public void ShowBinaryChoice(string a, string b, Action chooseA, Action chooseB)
    {
        Debug.Log("[TopUI] ShowBinaryChoice called");
        waitingChoice = true;
        //回调 =「你现在先把一件事记下来，等将来某个事件发生时，再帮你去做。」
        onChooseA = chooseA;
        onChooseB = chooseB;

        hideAt = 0f;

        if (singleRoot) singleRoot.SetActive(false);
        if (choiceRoot) choiceRoot.SetActive(true);

        if (optionAText) optionAText.text = a;
        if (optionBText) optionBText.text = b;
    }
    //用户选择了其中一个选项后，调用相应的回调函数并隐藏提示框
    void Choose(bool isA)
    {
        if (!waitingChoice) { Hide(); return; }// 如果不是等待选择状态，直接隐藏，只有等待选择才执行回调

        var cb = isA ? onChooseA : onChooseB;

        Hide();
        cb?.Invoke();
    }
    //隐藏单句提示框和二选一选择框，清空回调
    public void Hide()
    {
        waitingChoice = false;//不再等待选择
        onChooseA = null;
        onChooseB = null;
        hideAt = 0f;

        if (singleRoot) singleRoot.SetActive(false);
        if (choiceRoot) choiceRoot.SetActive(false);
    }
}
