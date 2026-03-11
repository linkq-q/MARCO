using UnityEngine;

public class BlackMaskTrigger : MonoBehaviour
{
    [Header("Assign BlackMask Animator")]
    public Animator maskAnimator;

    [Header("Animator Bool Name")]
    public string boolName = "ShowBlack";

    [Header("Idle State Name (must match Animator state name)")]
    public string idleStateName = "透明";

    void Awake()
    {
        ForceIdleAndDisable();
    }

    void OnEnable()
    {
        ForceIdleAndDisable();
    }

    void ForceIdleAndDisable()
    {
        if (!maskAnimator) return;

        // 核心：开场强制 false，杜绝自动进入
        maskAnimator.SetBool(boolName, false);

        // 强制回到透明状态
        if (!string.IsNullOrEmpty(idleStateName))
        {
            maskAnimator.Play(idleStateName, 0, 0f);
            maskAnimator.Update(0f);
        }
    }

    // 对外：显示黑屏（淡入后保持）
    public void ShowBlack()
    {
        if (!maskAnimator) return;
        maskAnimator.SetBool(boolName, true);
    }

    // 可选：立刻回透明（调试用）
    public void HideInstant()
    {
        ForceIdleAndDisable();
    }
}
