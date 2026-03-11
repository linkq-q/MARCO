using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class FirstPersonControllerSimple : MonoBehaviour, IEndingLockable
{
    [Header("Refs")]
    public Transform cameraRoot; // CameraRoot
    public Camera mainCamera;

    [Header("Move")]
    public float moveSpeed = 3.5f;
    public float sprintMul = 1.35f;
    public float gravity = -18f;

    [Header("Look")]
    public float mouseSensitivity = 2.0f;
    public float pitchMin = -80f;
    public float pitchMax = 80f;

    [Header("Options")]
    public bool lockCursorOnStart = true;
    public bool allowSprint = true;

    [Header("Yaw Clamp")]
    public bool clampYaw = true;
    public float yawLeftLimit = 50f;   // 向左最多转多少度
    public float yawRightLimit = 50f;  // 向右最多转多少度
    public bool yawRecentersWhenUnlocked = true; // 可选：解锁/剧情后回正

    [Header("Mirror / Invert")]
    public bool invertMoveX = false; // 左右移动镜像（A/D 反转）
    public bool invertMoveY = false; // 前后移动镜像（W/S 反转）
    public bool invertLookX = false; // 左右视角镜像（Mouse X 反转）
    public bool invertLookY = false; // 上下视角镜像（Mouse Y 反转，常见“反向Y”）

    [Header("Cursor Control")]
    [Tooltip("为避免打开背包后点一下又锁回去：背包打开时把它设为 false。")]
    public bool allowCursorToggle = true;

    bool endingLocked;
    bool _prevAllowCursorToggle;
    CursorLockMode _prevLockMode;
    bool _prevCursorVisible;

    float yawCenter;     // 中心朝向（通常是进入场景时的朝向）
    float yawOffset;     // 相对中心的偏移（被 clamp 的就是它）

    CharacterController cc;
    float pitch;
    float verticalVel;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        if (!mainCamera) mainCamera = Camera.main;
        if (!cameraRoot && mainCamera) cameraRoot = mainCamera.transform.parent;

        yawCenter = transform.eulerAngles.y;
        yawOffset = 0f;

        if (lockCursorOnStart) SetCursorLock(true);
    }

    void Update()
    {
        if (endingLocked) return;
        // （可选）保留你原来的调试逻辑，但加一个门：UI打开时禁止它乱改锁定状态
        if (allowCursorToggle)
        {
            if (Input.GetKeyDown(KeyCode.Escape)) SetCursorLock(false);
            if (Input.GetMouseButtonDown(0) && lockCursorOnStart) SetCursorLock(true);
        }

        Look();
        Move();
    }

    void Look()
    {
        if (Cursor.lockState != CursorLockMode.Locked) return;

        float mx = Input.GetAxis("Mouse X") * mouseSensitivity;
        float my = Input.GetAxis("Mouse Y") * mouseSensitivity;

        if (invertLookX) mx = -mx;
        if (invertLookY) my = -my;

        // -------- Yaw（左右）---------
        if (!clampYaw)
        {
            yawCenter += mx; // 不限制时，中心朝向也跟着走
            yawOffset = 0f;  // 避免残留偏移带来奇怪效果
        }
        else
        {
            yawOffset += mx;
            yawOffset = Mathf.Clamp(yawOffset, -yawLeftLimit, yawRightLimit);
        }

        float yawFinal = yawCenter + yawOffset;
        transform.rotation = Quaternion.Euler(0f, yawFinal, 0f);

        // -------- Pitch（上下）---------
        pitch -= my;
        pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);

        if (cameraRoot)
            cameraRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    void Move()
    {
        float h = Input.GetAxisRaw("Horizontal"); // A/D
        float v = Input.GetAxisRaw("Vertical");   // W/S

        if (invertMoveX) h = -h;
        if (invertMoveY) v = -v;

        Vector3 move = (transform.right * h + transform.forward * v).normalized;

        float spd = moveSpeed;
        if (allowSprint && Input.GetKey(KeyCode.LeftShift)) spd *= sprintMul;

        // 重力
        if (cc.isGrounded && verticalVel < 0f) verticalVel = -2f; // 贴地
        verticalVel += gravity * Time.deltaTime;

        Vector3 vel = move * spd;
        vel.y = verticalVel;

        cc.Move(vel * Time.deltaTime);
    }

    public void SetCursorLock(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    public void OnEndingLock()
    {
        endingLocked = true;

        _prevAllowCursorToggle = allowCursorToggle;
        _prevLockMode = Cursor.lockState;
        _prevCursorVisible = Cursor.visible;

        allowCursorToggle = false;

        // 结局通常要鼠标可点 UI
        SetCursorLock(false);
    }

    public void OnEndingUnlock()
    {
        endingLocked = false;
        allowCursorToggle = _prevAllowCursorToggle;

        Cursor.lockState = _prevLockMode;
        Cursor.visible = _prevCursorVisible;
    }
}
