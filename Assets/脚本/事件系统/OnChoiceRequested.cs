using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ChoicePanelUI : MonoBehaviour
{
    public LineSequenceTypewriterWithChoice typer;

    [Header("UI")]
    public GameObject panelRoot;
    public Button[] buttons;
    public TextMeshProUGUI[] buttonTexts;

    string[] _opts;
    int[] _targets;

    void Awake()
    {
        panelRoot.SetActive(false);
        typer.OnChoiceRequested += ShowChoices;
    }

    void ShowChoices(string[] optionTexts, int[] jumpTargets)
    {
        _opts = optionTexts;
        _targets = jumpTargets;

        panelRoot.SetActive(true);

        for (int i = 0; i < buttons.Length; i++)
        {
            bool active = i < _opts.Length;
            buttons[i].gameObject.SetActive(active);
            if (!active) continue;

            int idx = i;
            buttonTexts[i].text = _opts[i];

            buttons[i].onClick.RemoveAllListeners();
            buttons[i].onClick.AddListener(() =>
            {
                panelRoot.SetActive(false);
                typer.JumpToLine(_targets[idx]);
            });
        }
    }
}
