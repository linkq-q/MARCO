using UnityEngine;
using TMPro;
using System.Collections;

public class TypewriterText : MonoBehaviour
{
    [Header("References")]
    public TextMeshProUGUI text;

    [Header("Typewriter Settings")]
    [Tooltip("每个字符出现的时间间隔（秒，使用不受 timeScale 影响的时间）")]
    public float charInterval = 0.1f;

    [Tooltip("是否在启用时自动播放")]
    public bool playOnEnable = true;

    [Header("Life Time (推荐交给外部UI控制)")]
    [Tooltip("⚠️ 不推荐在打字机里自动消失。这里保留字段但默认不执行自动隐藏。")]
    public float sentenceLifeTime = 0f;

    [Tooltip("若为 true，sentenceLifeTime 到点会把 maxVisibleCharacters 置 0（文字清空）。默认 false。")]
    public bool autoHide = false;

    Coroutine typingCoroutine;
    Coroutine lifeCoroutine;

    int _playToken = 0;

    void OnEnable()
    {
        if (playOnEnable) Play();
    }

    public void Play()
    {
        if (!text) return;

        _playToken++;
        StopRoutines();

        // ✅ 关键：先把可见字符清 0，避免第一帧闪全文
        text.maxVisibleCharacters = 0;

        typingCoroutine = StartCoroutine(TypeText_Unscaled(_playToken));
    }



    IEnumerator TypeText_Unscaled(int token)
    {
        // 等一帧：让 TMP/布局刷新
        yield return null;

        if (token != _playToken) yield break;

        text.ForceMeshUpdate();
        int totalChars = text.textInfo.characterCount;

        // 兜底：最多再等两帧
        int guard = 0;
        while (totalChars <= 0 && guard < 2)
        {
            yield return null;
            if (token != _playToken) yield break;

            text.ForceMeshUpdate();
            totalChars = text.textInfo.characterCount;
            guard++;
        }

        text.maxVisibleCharacters = 0;

        for (int i = 0; i <= totalChars; i++)
        {
            if (token != _playToken) yield break;

            text.maxVisibleCharacters = i;
            yield return new WaitForSecondsRealtime(charInterval);
        }

        typingCoroutine = null;

        // ✅ 自动隐藏改为可选（默认关闭）
        if (autoHide && sentenceLifeTime > 0f)
            lifeCoroutine = StartCoroutine(AutoHideAfterLife(token));
    }

    IEnumerator AutoHideAfterLife(int token)
    {
        yield return new WaitForSecondsRealtime(sentenceLifeTime);
        if (token != _playToken) yield break;

        // 只清“可见字符”，不改 alpha，不关对象
        text.maxVisibleCharacters = 0;
        lifeCoroutine = null;
    }

    public void ShowAll()
    {
        if (!text) return;

        _playToken++;
        StopRoutines();

        text.ForceMeshUpdate();
        text.maxVisibleCharacters = int.MaxValue;

        // 同样：默认不自动隐藏
        if (autoHide && sentenceLifeTime > 0f)
            lifeCoroutine = StartCoroutine(AutoHideAfterLife(_playToken));
    }

    /// <summary>停止打字机/寿命计时，不清空当前显示</summary>
    public void StopKeepVisible()
    {
        _playToken++;
        StopRoutines();
    }

    /// <summary>立刻清空可见字符（外部需要时手动调用）</summary>
    public void ClearVisible()
    {
        if (!text) return;
        _playToken++;
        StopRoutines();
        text.maxVisibleCharacters = 0;
    }

    void StopRoutines()
    {
        if (typingCoroutine != null) StopCoroutine(typingCoroutine);
        if (lifeCoroutine != null) StopCoroutine(lifeCoroutine);
        typingCoroutine = null;
        lifeCoroutine = null;
    }

    void OnDisable()
    {
        StopRoutines();
    }
}
