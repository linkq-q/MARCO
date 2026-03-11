using System.Collections;
using UnityEngine;

public class AIInferenceTicker : MonoBehaviour
{
    [Header("Refs")]
    public AIBroker ai; // 你项目里真正走云端的入口脚本（拖它）
    public float intervalSeconds = 10f; // 推理频率（8~15秒比较舒服）

    void OnEnable() => StartCoroutine(Tick());

    IEnumerator Tick()
    {
        var wait = new WaitForSecondsRealtime(intervalSeconds);
        while (true)
        {
            yield return wait;
            if (!ai) continue;
        }
    }
}
