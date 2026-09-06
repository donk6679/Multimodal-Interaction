using UnityEngine;
using Invector.vCharacterController;

[RequireComponent(typeof(vThirdPersonController))]
public class VR_InvectorBridge : MonoBehaviour
{
    private vThirdPersonController cc;
    private Vector2 currentInput;

    [Header("控制模式切换")]
    [Tooltip("开启时：使用键盘 WASD 进行本地走动测试。\n关闭时：进入监听模式，等待多通道信号。")]
    public bool useKeyboardDebug = true;

    [Header("方向参考 (VR头显)")]
    [Tooltip("拖入你的 VR Main Camera。如果不填，默认使用角色自身的正前方")]
    public Transform referenceCamera;

    void Start()
    {
        cc = GetComponent<vThirdPersonController>();
        if (cc != null) cc.Init();

        if (referenceCamera == null && Camera.main != null)
        {
            referenceCamera = Camera.main.transform;
        }
    }

    void Update()
    {
        if (cc == null) return;

        // 本地键盘测试逻辑
        if (useKeyboardDebug)
        {
            float h = Input.GetAxis("Horizontal");
            float v = Input.GetAxis("Vertical");
            currentInput = new Vector2(h, v);
        }

        // 将 2D 意图转换为 Invector 需要的 3D 向量
        Vector3 invectorInput = new Vector3(currentInput.x, 0f, currentInput.y);
        cc.input = invectorInput;
        cc.ControlKeepDirection();
    }

    void FixedUpdate()
    {
        if (cc == null) return;

        // 锁定世界坐标系的方向
        Transform refTransform = referenceCamera != null ? referenceCamera : transform;
        cc.UpdateMoveDirection(refTransform);

        // 驱动底层物理与动画
        cc.UpdateMotor();
        cc.ControlLocomotionType();
        cc.ControlRotationType();
        cc.UpdateAnimator();
    }

    void OnAnimatorMove()
    {
        if (cc != null) cc.ControlAnimatorRootMotion();
    }

    // ==========================================
    // 同学们：请在你们的交互代码中调用以下接口！
    // ==========================================

    /// <summary>
    /// 接收多通道传来的移动方向指令
    /// </summary>
    /// <param name="direction">X 代表左右，Y 代表前后。例如向前走传入 new Vector2(0, 1)</param>
    public void SetMovement(Vector2 direction)
    {
        if (!useKeyboardDebug) currentInput = direction;
    }

    /// <summary>
    /// 接收停止指令
    /// </summary>
    public void StopMovement()
    {
        if (!useKeyboardDebug) currentInput = Vector2.zero;
    }
}