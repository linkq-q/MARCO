using UnityEngine;

public class ManagedTakeoverDebugKeys : MonoBehaviour
{
    public ManagedTakeoverSystem sys;

    void Update()
    {
        if (sys == null) return;

        // 引导相关行为：读日记/查看碎片
        if (Input.GetKeyDown(KeyCode.Alpha1))
            sys.NotifyGuideAction();

        // 普通推进（重置无推进计时）
        if (Input.GetKeyDown(KeyCode.Alpha2))
            sys.NotifyProgress();

        // 推测成功 -10
        if (Input.GetKeyDown(KeyCode.Alpha3))
            sys.OnInferenceSuccess();

        // 连线成功 -10 + 冻结20s
        if (Input.GetKeyDown(KeyCode.Alpha4))
            sys.OnLinkSuccess();

        // 推理失败 -2
        if (Input.GetKeyDown(KeyCode.Alpha5))
            sys.OnInferenceFail();
    }
}
