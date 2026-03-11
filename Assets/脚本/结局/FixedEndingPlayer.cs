using UnityEngine;
using TMPro;

public class FixedEndingPlayer : MonoBehaviour
{
    [Header("UI Root")]
    [Tooltip("结局覆盖层根节点（你已做好的中心文本UI的父节点）")]
    public GameObject endingRoot;

    [Header("Typewriter (your script)")]
    public LineSequenceTypewriterWithChoice typewriter;

    [Tooltip("可选：如果 typewriter.text 没绑，可在这里指定 TMP（会自动赋给 typewriter.text）")]
    public TextMeshProUGUI bodyTMP;

    [Header("Effects to Enable")]
    [Tooltip("深色滤镜呼吸、高斯模糊等脚本（enabled=true 即开始生效）")]
    public Behaviour[] enableEffects;

    [Header("Options")]
    public bool disableClickToSkipOrNext = true;
    public bool stopTypewriterOnHide = true;

    public URPRendererFeatureToggler_ByRendererData urpFx;

    bool _playing;

    void Awake()
    {
        if (endingRoot) endingRoot.SetActive(false);

        if (typewriter && !typewriter.text && bodyTMP)
            typewriter.text = bodyTMP;

        // 初始关闭效果
        if (enableEffects != null)
        {
            for (int i = 0; i < enableEffects.Length; i++)
                if (enableEffects[i]) enableEffects[i].enabled = false;
        }
    }

    public void Play(FixedEndingAsset asset)
    {
        if (!asset)
        {
            Debug.LogError("[FixedEndingPlayer] asset is NULL.", this);
            return;
        }
        if (!endingRoot)
        {
            Debug.LogError("[FixedEndingPlayer] endingRoot is NULL.", this);
            return;
        }
        if (!typewriter)
        {
            Debug.LogError("[FixedEndingPlayer] typewriter is NULL.", this);
            return;
        }

        _playing = true;

        // 1) 开UI
        endingRoot.SetActive(true);

        // 2) 开效果
        if (enableEffects != null)
        {
            for (int i = 0; i < enableEffects.Length; i++)
                if (enableEffects[i]) enableEffects[i].enabled = true;
        }

        if (urpFx) urpFx.SetEndingFX(true);

        // 3) 绑定TMP（保险）
        if (!typewriter.text && bodyTMP) typewriter.text = bodyTMP;

        // 4) 结局不需要 Choice：清空回调 & 避免等待选择卡住
        //    （你的脚本只有遇到【CHOICE】行才会进入 waitingChoice）
        //    固定结局资产里不要写【CHOICE】即可。

        // 5) 把正文拆成行数组，喂给你的打字机 lines
        typewriter.lines = SplitToLines(asset.bodyText);

        // 6) 覆盖打字机速度（可选）
        if (asset.charIntervalOverride > 0f) typewriter.charInterval = asset.charIntervalOverride;
        if (asset.linePauseOverride > 0f) typewriter.linePause = asset.linePauseOverride;

        // 7) 禁用点击跳过（可选，结局通常不希望玩家鼠标乱点跳过）
        if (disableClickToSkipOrNext) typewriter.clickToSkipOrNext = false;

        // 8) 直接开始播放
        typewriter.PlayFromStart();
    }

    public void Hide()
    {
        _playing = false;

        if (stopTypewriterOnHide && typewriter != null)
        {
            // 你的脚本没有显式 Stop()，但我们可以通过禁用组件来停止协程
            typewriter.enabled = false;
            typewriter.enabled = true; // 恢复，避免下次不能Play（也可不恢复，看你需求）
        }

        // 关效果
        if (enableEffects != null)
        {
            for (int i = 0; i < enableEffects.Length; i++)
                if (enableEffects[i]) enableEffects[i].enabled = false;
        }

        // 关UI
        if (endingRoot) endingRoot.SetActive(false);
    }

    static string[] SplitToLines(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return new string[] { "" };

        raw = raw.Replace("\r\n", "\n").Replace("\r", "\n");

        // 保留空行：空行也作为一个 entry
        return raw.Split('\n');
    }
}