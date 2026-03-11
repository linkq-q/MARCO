using UnityEngine;

public class IdleDetector : MonoBehaviour
{
    public float idleSeconds = 25f;
    public float rearmSeconds = 25f;

    bool paused;
    float lastInputTime;
    float nextAllowedTime;
    Vector3 lastMousePos;

    void Start()
    {
        lastInputTime = Time.unscaledTime;
        nextAllowedTime = 0f;
        lastMousePos = Input.mousePosition;
    }

    void Update()
    {
        if (paused) return;

        if (DetectAnyInput())
            lastInputTime = Time.unscaledTime;

        if (Time.unscaledTime < nextAllowedTime) return;

        if (Time.unscaledTime - lastInputTime >= idleSeconds)
        {
            SnarkRouter.I?.Say(SnarkType.Idle);
            nextAllowedTime = Time.unscaledTime + rearmSeconds;
            lastInputTime = Time.unscaledTime;
        }
    }

    bool DetectAnyInput()
    {
        if (Input.anyKeyDown) return true;
        if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2)) return true;
        if (Mathf.Abs(Input.mouseScrollDelta.y) > 0.01f) return true;

        Vector3 mp = Input.mousePosition;
        if ((mp - lastMousePos).sqrMagnitude > 0.5f)
        {
            lastMousePos = mp;
            return true;
        }
        return false;
    }

    public void SetPaused(bool p)
    {
        paused = p;
        if (!paused)
        {
            lastInputTime = Time.unscaledTime;
            nextAllowedTime = Time.unscaledTime + 1f;
            lastMousePos = Input.mousePosition;
        }
    }
}
