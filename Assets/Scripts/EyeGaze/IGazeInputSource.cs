using UnityEngine;

/// <summary>
/// 眼动数据来源的抽象接口：把"眼动数据从哪来"和"注视选择逻辑"解耦。
/// 真机走 PICO 眼动 API，编辑器用鼠标/相机模拟，业务逻辑（射线检测、停留、面板）完全不用改。
/// </summary>
public interface IGazeInputSource
{
    bool IsEyeTrackingSupported { get; }
    bool IsEyeTrackingActive { get; }
    void StartTracking();
    void StopTracking();

    /// <summary>拿到世界空间注视射线。返回 false 表示当前帧无有效数据。</summary>
    bool TryGetGazeRay(out Ray gazeRay);
}
