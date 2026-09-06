using UnityEngine;

public class VRCameraFollow : MonoBehaviour
{
    public Transform playerTarget;

    [Header("眼高偏移")]
    public float eyeHeight = 1.65f;   // 成年人站立眼高约 1.6-1.7 米

    void LateUpdate()
    {
        if (playerTarget != null)
        {
            Vector3 targetPos = playerTarget.position;
            targetPos.y += eyeHeight;                // 在角色脚底基础上抬高
            transform.position = targetPos;
        }
    }
}