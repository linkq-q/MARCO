using System.Collections;
using TMPro;
using UnityEngine;

public class TypewriterTextEnding : MonoBehaviour
{
    [Header("References")]
    public TextMeshProUGUI text;

    [Header("Typewriter Settings")]
    public float charInterval = 0.03f;
    public bool playOnEnable = false;

    Coroutine _co;
    string _full;

    void OnEnable()
    {
        if (playOnEnable && !string.IsNullOrEmpty(_full))
            Play(_full);
    }

    public void Play(string content)
    {
        if (!text) return;

        _full = content ?? "";

        if (_co != null) StopCoroutine(_co);

        text.text = _full;
        text.ForceMeshUpdate();              // ✅ 关键：刷新 characterCount
        text.maxVisibleCharacters = 0;       // ✅ 从 0 开始显示

        _co = StartCoroutine(TypeCo());
    }

    IEnumerator TypeCo()
    {
        // TMP 的“可见字符”按 textInfo.characterCount 来
        int total = text.textInfo.characterCount;

        for (int i = 0; i <= total; i++)
        {
            text.maxVisibleCharacters = i;

            // ✅ 不受 Time.timeScale 影响，UI暂停也能继续打字
            yield return new WaitForSecondsRealtime(charInterval);
        }

        _co = null;
    }
}