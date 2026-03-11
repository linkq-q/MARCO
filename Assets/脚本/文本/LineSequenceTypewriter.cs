using UnityEngine;
using TMPro;
using System.Collections;
using System;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class LineSequenceTypewriterWithChoice : MonoBehaviour
{
    [Header("References")]
    public TextMeshProUGUI text;

    [Header("Lines (one line per entry)")]
    [TextArea(1, 6)]
    public string[] lines;

    [Header("Timing")]
    public float charInterval = 0.05f;
    public float linePause = 0.6f;
    public bool playOnEnable = true;

    [Header("Input")]
    public bool clickToSkipOrNext = true;

    // ===== Black Mask Hook (B方案：Image+Animator) =====
    [Header("Black Mask (optional)")]
    public BlackMaskTrigger blackMask;

    [Tooltip("按行号触发（0-based）。例如 Element 5 就填 5。")]
    public int[] triggerMaskAtLineIndices;

    [Tooltip("按关键字触发：文本包含任意关键字就触发。留空则不使用。")]
    public string[] triggerMaskKeywords;

    [Tooltip("同一行只触发一次（推荐开）")]
    public bool triggerOncePerLine = true;

    HashSet<int> _triggeredLineSet = new HashSet<int>();

    // ===== Choice Hook =====
    public event Action<string[], int[]> OnChoiceRequested;

    int _index = 0;
    bool _isTyping = false;
    bool _waitingChoice = false;
    Coroutine _routine;

    void OnEnable()
    {
        if (playOnEnable) PlayFromStart();
    }

    void Update()
    {
        if (!clickToSkipOrNext) return;

        if (Input.GetMouseButtonDown(0))
        {
            if (_isTyping) ShowCurrentLineInstant();
            else if (!_waitingChoice) PlayNextLine();
        }
    }

    public void PlayFromStart()
    {
        _index = 0;
        _triggeredLineSet.Clear();
        StartCurrent();
    }

    public void PlayNextLine()
    {
        _index++;
        StartCurrent();
    }

    public void JumpToLine(int lineIndex)
    {
        _waitingChoice = false;
        _index = Mathf.Clamp(lineIndex, 0, lines.Length - 1);
        StartCurrent();
    }

    void StartCurrent()
    {
        if (!text) return;
        if (lines == null || lines.Length == 0)
        {
            text.text = "";
            return;
        }
        if (_index >= lines.Length)
        {
            _isTyping = false;
            return;
        }

        // 先判断是否是“选择标记行”
        if (TryParseChoiceLine(lines[_index], out var optionTexts, out var jumpTargets))
        {
            _waitingChoice = true;
            _isTyping = false;

            text.text = "";
            text.maxVisibleCharacters = 999999;

            OnChoiceRequested?.Invoke(optionTexts, jumpTargets);
            return;
        }

        // ===== 在“本行开始显示”时触发全屏黑遮罩 =====
        TryTriggerBlackMask(_index, lines[_index]);

        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(TypeLineAndAutoAdvance(lines[_index]));
    }

    void TryTriggerBlackMask(int lineIndex, string lineText)
    {
        if (!blackMask) return;

        if (triggerOncePerLine && _triggeredLineSet.Contains(lineIndex))
            return;

        bool hit = false;

        // 方式1：按行号触发
        if (triggerMaskAtLineIndices != null && triggerMaskAtLineIndices.Length > 0)
        {
            for (int i = 0; i < triggerMaskAtLineIndices.Length; i++)
            {
                if (triggerMaskAtLineIndices[i] == lineIndex)
                {
                    hit = true;
                    break;
                }
            }
        }

        // 方式2：按关键字触发
        if (!hit && triggerMaskKeywords != null && triggerMaskKeywords.Length > 0 && !string.IsNullOrEmpty(lineText))
        {
            for (int i = 0; i < triggerMaskKeywords.Length; i++)
            {
                string kw = triggerMaskKeywords[i];
                if (!string.IsNullOrEmpty(kw) && lineText.Contains(kw))
                {
                    hit = true;
                    break;
                }
            }
        }

        if (hit)
        {
            blackMask.ShowBlack();

            if (triggerOncePerLine) _triggeredLineSet.Add(lineIndex);
        }
    }

    IEnumerator TypeLineAndAutoAdvance(string line)
    {
        _waitingChoice = false;
        _isTyping = true;

        text.text = line;
        text.maxVisibleCharacters = 0;

        text.ForceMeshUpdate();
        int totalChars = text.textInfo.characterCount;

        for (int i = 0; i <= totalChars; i++)
        {
            text.maxVisibleCharacters = i;
            yield return new WaitForSeconds(charInterval);
        }

        _isTyping = false;

        yield return new WaitForSeconds(linePause);

        _index++;
        if (_index < lines.Length)
            StartCurrent();
    }

    public void ShowCurrentLineInstant()
    {
        if (!text) return;

        if (_routine != null) StopCoroutine(_routine);

        text.ForceMeshUpdate();
        text.maxVisibleCharacters = text.textInfo.characterCount;

        _isTyping = false;
    }

    // ===== Choice Line Format =====
    // 【CHOICE】选项A=12|选项B=20|选项C=30
    bool TryParseChoiceLine(string raw, out string[] optionTexts, out int[] jumpTargets)
    {
        optionTexts = null;
        jumpTargets = null;

        if (string.IsNullOrEmpty(raw)) return false;
        raw = raw.Trim();

        if (!raw.StartsWith("【CHOICE】")) return false;

        string body = raw.Substring("【CHOICE】".Length).Trim();
        if (string.IsNullOrEmpty(body)) return false;

        string[] parts = body.Split('|');
        if (parts.Length == 0) return false;

        optionTexts = new string[parts.Length];
        jumpTargets = new int[parts.Length];

        for (int i = 0; i < parts.Length; i++)
        {
            string p = parts[i];
            int eq = p.LastIndexOf('=');
            if (eq <= 0 || eq >= p.Length - 1) return false;

            string optText = p.Substring(0, eq).Trim();
            string num = p.Substring(eq + 1).Trim();

            if (!int.TryParse(num, out int target)) return false;

            optionTexts[i] = optText;
            jumpTargets[i] = target;
        }
        return true;
    }
}
