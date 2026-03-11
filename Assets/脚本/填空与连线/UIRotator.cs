using UnityEngine;

public class UIRotator : MonoBehaviour
{
    [Tooltip("角速度：度/秒")]
    public float speed = 18f;

    [Tooltip("是否不受 Time.timeScale 影响（UI 常用 true）")]
    public bool useUnscaledTime = true;

    [Tooltip("顺时针")]
    public bool clockwise = true;

    void Update()
    {
        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float dir = clockwise ? -1f : 1f;
        transform.Rotate(0f, 0f, dir * speed * dt);
    }
}