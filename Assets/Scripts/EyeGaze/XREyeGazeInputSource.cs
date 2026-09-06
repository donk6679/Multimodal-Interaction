using UnityEngine;
#if !UNITY_EDITOR
using Unity.XR.PXR; // PICO 官方命名空间，仅真机分支用到
#endif

/// <summary>
/// 眼动数据源实现：真机走 PICO PXR 眼动 API + 坐标矩阵转换，编辑器用鼠标/相机正前方模拟。
/// 关键修复点：旧代码用 headCamera.position + rotation*origin 转换坐标是错的；
/// 这里改用文档要求的两段矩阵相乘（头部位姿矩阵 × XR Origin 世界矩阵），人走动后射线也不会错位。
/// </summary>
public class XREyeGazeInputSource : MonoBehaviour, IGazeInputSource
{
    [Header("Fallback & Editor Simulation")]
    [SerializeField] private Camera fallbackCamera;
    [SerializeField] private bool simulateInEditor = true;
    [SerializeField] private bool useMousePointInEditor = true;

    [Header("XR Origin (必须赋值，用于坐标矩阵转换)")]
    [Tooltip("请将你的 XR Origin (VR) 最外层父物体拖到这里，不是 Main Camera")]
    [SerializeField] private Transform xrOriginTransform;

    private bool trackingStarted = false;

    public bool IsEyeTrackingSupported => true;
    public bool IsEyeTrackingActive => trackingStarted;

    private void Awake()
    {
        if (fallbackCamera == null) fallbackCamera = Camera.main;

        // 自动寻找 XR Origin（相机的父物体）作为兜底
        if (xrOriginTransform == null && fallbackCamera != null)
        {
            xrOriginTransform = fallbackCamera.transform.parent != null
                ? fallbackCamera.transform.parent
                : fallbackCamera.transform;
        }
    }

    public void StartTracking() => trackingStarted = true;
    public void StopTracking() => trackingStarted = false;

    public bool TryGetGazeRay(out Ray gazeRay)
    {
        // —— 编辑器模拟：鼠标位置 or 相机正前方 ——
        if (Application.isEditor && simulateInEditor)
        {
            if (fallbackCamera != null && useMousePointInEditor)
            {
                gazeRay = fallbackCamera.ScreenPointToRay(Input.mousePosition);
                return true;
            }
            if (fallbackCamera != null)
            {
                gazeRay = new Ray(fallbackCamera.transform.position,
                                  fallbackCamera.transform.forward);
                return true;
            }
            gazeRay = default;
            return false;
        }

        if (!trackingStarted) { gazeRay = default; return false; }

        // —— 真机：PICO 眼动 API + 坐标转换（仅非编辑器编译） ——
        #if !UNITY_EDITOR
        // 先确认合并眼数据可用（0=不可用,1=可用），无效则走兜底
        PXR_EyeTracking.GetCombinedEyePoseStatus(out uint status);
        if (status == 1)
        {
            Matrix4x4 headPoseMatrix;
            Vector3 combineEyeGazeVector;   // 方向，相机系
            Vector3 combineEyeGazeOrigin;   // 起点，相机系

            PXR_EyeTracking.GetHeadPosMatrix(out headPoseMatrix);
            PXR_EyeTracking.GetCombineEyeGazeVector(out combineEyeGazeVector);
            PXR_EyeTracking.GetCombineEyeGazePoint(out combineEyeGazeOrigin);

            // XR Origin 的世界矩阵：把追踪空间 → Unity 世界空间
            Matrix4x4 originPoseMatrix = xrOriginTransform != null
                ? xrOriginTransform.localToWorldMatrix : Matrix4x4.identity;

            // 起点：用 MultiplyPoint（含平移）
            Vector3 originWorld = originPoseMatrix.MultiplyPoint(
                                      headPoseMatrix.MultiplyPoint(combineEyeGazeOrigin));
            // 方向：用 MultiplyVector（不含平移，只旋转/缩放），不能与起点混用
            Vector3 vectorWorld = originPoseMatrix.MultiplyVector(
                                      headPoseMatrix.MultiplyVector(combineEyeGazeVector));

            if (vectorWorld.sqrMagnitude > 0.001f)   // 数据有效才用
            {
                gazeRay = new Ray(originWorld, vectorWorld.normalized);
                return true;
            }
        }
        #endif

        // —— 兜底：相机正前方 ——
        if (fallbackCamera != null)
        {
            gazeRay = new Ray(fallbackCamera.transform.position,
                              fallbackCamera.transform.forward);
            return true;
        }
        gazeRay = default;
        return false;
    }
}
