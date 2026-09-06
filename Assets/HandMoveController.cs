using UnityEngine;
using Unity.XR.PXR;

public class HandMoveController : MonoBehaviour
{
    [Header("控制设置")]
    public HandType handType = HandType.HandLeft;
    public VR_InvectorBridge invectorBridge;

    [Header("捏合阈值 (单位: 米)")]
    public float pinchThreshold = 0.045f;
    public float releaseThreshold = 0.07f;

    private bool isPinching = false;
    private HandJointLocations handJointLocations = new HandJointLocations();

    // 用于检测手部是否卡死不更新的缓存变量
    private Vector3 lastThumbPos = Vector3.zero;
    private int staticFrameCount = 0;

    void Update()
    {
        if (invectorBridge == null) return;

        bool shouldMove = false;

        // 1. 获取手部关节
        if (PXR_HandTracking.GetJointLocations(handType, ref handJointLocations))
        {
            if (handJointLocations.jointLocations != null && handJointLocations.jointLocations.Length > 20)
            {
                var thumbPose = handJointLocations.jointLocations[4].pose.Position;
                var indexPose = handJointLocations.jointLocations[9].pose.Position;

                Vector3 thumbTipPos = new Vector3(thumbPose.x, thumbPose.y, thumbPose.z);
                Vector3 indexTipPos = new Vector3(indexPose.x, indexPose.y, indexPose.z);

                // 2. 核心死锁检查：判断手部坐标是否连续几帧绝对静止
                // 真实的人手在空中是绝对不可能做到坐标每一位小数都完全静止不变的。
                // 如果连续 3 帧坐标一模一样，说明硬件已经丢失了追踪，只是 SDK 在喂缓存的死数据。
                if (thumbTipPos == lastThumbPos)
                {
                    staticFrameCount++;
                }
                else
                {
                    staticFrameCount = 0; // 手还在动，重置计数
                }

                lastThumbPos = thumbTipPos; // 记录当前帧坐标

                // 如果连续超过 3 帧完全静止，判定为“假追踪/假死状态”
                if (staticFrameCount > 3)
                {
                    isPinching = false;
                }
                else
                {
                    // 正常的指尖距离计算
                    float distance = Vector3.Distance(thumbTipPos, indexTipPos);

                    if (distance < pinchThreshold)
                    {
                        isPinching = true;
                    }
                    else if (distance > releaseThreshold)
                    {
                        isPinching = false;
                    }
                }

                shouldMove = isPinching;
            }
        }
        else
        {
            // 彻底丢失追踪
            isPinching = false;
            staticFrameCount = 0;
        }

        // 3. 终极大闸：只要没有被判定为 shouldMove，一律强制发送 Vector2.zero 刹车
        if (shouldMove)
        {
            invectorBridge.SetMovement(new Vector2(0f, 1f));
        }
        else
        {
            invectorBridge.SetMovement(Vector2.zero);
        }
    }
}