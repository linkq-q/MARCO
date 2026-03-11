using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChoiceUI : MonoBehaviour
{
    public static ChoiceUI Instance { get; private set; }

    public GameObject root;
    public TextMeshProUGUI optionAText;
    public TextMeshProUGUI optionBText;
    public Button optionAButton;
    public Button optionBButton;
    public Button closeButton; // 可选：允许玩家关闭不选

    Action onChooseA;
    Action onChooseB;

    void Awake()
    {
        Instance = this;
        root.SetActive(false);

        optionAButton.onClick.AddListener(() => { root.SetActive(false); onChooseA?.Invoke(); ClearCallbacks(); });
        optionBButton.onClick.AddListener(() => { root.SetActive(false); onChooseB?.Invoke(); ClearCallbacks(); });

        if (closeButton != null)
            closeButton.onClick.AddListener(() => { root.SetActive(false); ClearCallbacks(); });
    }

    public void Open(string a, string b, Action onChooseA, Action onChooseB)
    {
        this.onChooseA = onChooseA;
        this.onChooseB = onChooseB;

        optionAText.text = a;
        optionBText.text = b;

        root.SetActive(true);
    }

    void ClearCallbacks()
    {
        onChooseA = null;
        onChooseB = null;
    }
}
