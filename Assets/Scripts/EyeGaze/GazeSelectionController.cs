using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 注视选择核心：取世界射线 → Raycast 命中 GazeInteractable → 高亮 + dwell 停留计时 → 满时触发 Select。
/// 按 GazeActionType 分发：
///   BuyProduct      → 弹出确认面板（记录 pending 商品）
///   ConfirmPurchase → 真正购买并计入购物车
///   CancelPurchase  → 关闭面板
/// 两段注视流程：先注视商品弹面板，再把视线移到面板的确认按钮停留确认。
/// </summary>
public class GazeSelectionController : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("拖 XREyeGazeInputSource 组件")]
    [SerializeField] private MonoBehaviour gazeInputSourceBehaviour;

    [Header("Selection")]
    [SerializeField] private float maxRayDistance = 30f;
    [SerializeField] private float dwellDuration = 1.5f;             // 停留触发时长
    [SerializeField] private LayerMask interactableLayerMask = ~0;

    [Header("UI / Refs")]
    [Tooltip("准星圆环（Image Type=Filled），注视时按 0→1 填充")]
    [SerializeField] private Image gazeProgressImage;
    [SerializeField] private PurchasePanelController purchasePanel;
    [SerializeField] private ShoppingCartManager cart;

    [Header("Events")]
    [SerializeField] private UnityEvent<float> onProgressChanged;
    [SerializeField] private UnityEvent<GazeInteractable> onTargetSelected;

    private IGazeInputSource gazeInputSource;
    private GazeInteractable currentTarget;
    private float dwellTimer;
    private bool selectedCurrentTarget;

    /// <summary>当前注视命中的可交互物体（供其它系统读取，如调试）。</summary>
    public GazeInteractable CurrentTarget => currentTarget;

    private void Awake()
    {
        gazeInputSource = gazeInputSourceBehaviour as IGazeInputSource;
        if (gazeInputSource == null) gazeInputSource = GetComponent<IGazeInputSource>();
        SetProgress(0f);
    }

    private void OnEnable() => gazeInputSource?.StartTracking();
    private void OnDisable() { gazeInputSource?.StopTracking(); ClearCurrentTarget(); }

    private void Update()
    {
        if (gazeInputSource == null || !gazeInputSource.TryGetGazeRay(out Ray gazeRay))
        {
            ClearCurrentTarget();
            return;
        }

        if (Physics.Raycast(gazeRay, out RaycastHit hit, maxRayDistance,
                            interactableLayerMask, QueryTriggerInteraction.Collide))
        {
            // GetComponentInParent：碰撞体可以在子物体上
            GazeInteractable target = hit.collider.GetComponentInParent<GazeInteractable>();
            if (target != null) { HandleTarget(target); return; }
        }
        ClearCurrentTarget();
    }

    private void HandleTarget(GazeInteractable target)
    {
        if (target != currentTarget) SwitchTarget(target);
        if (selectedCurrentTarget) { SetProgress(1f); return; } // 已选中，不重复触发

        dwellTimer += Time.deltaTime;
        SetProgress(Mathf.Clamp01(dwellTimer / dwellDuration));
        if (dwellTimer >= dwellDuration) SelectCurrentTarget();
    }

    private void SwitchTarget(GazeInteractable newTarget)
    {
        if (currentTarget != null) currentTarget.SetFocused(false);
        currentTarget = newTarget;
        dwellTimer = 0f; selectedCurrentTarget = false; SetProgress(0f);
        if (currentTarget != null) currentTarget.SetFocused(true);
    }

    private void SelectCurrentTarget()
    {
        if (currentTarget == null) return;
        selectedCurrentTarget = true;
        currentTarget.Select();
        onTargetSelected?.Invoke(currentTarget);

        switch (currentTarget.Type)
        {
            case GazeInteractable.GazeActionType.BuyProduct:
                if (purchasePanel != null) purchasePanel.ShowProduct(currentTarget);
                break;

            case GazeInteractable.GazeActionType.ConfirmPurchase:
                if (purchasePanel != null && purchasePanel.CanConfirm)
                {
                    ProductInfo info = purchasePanel.PendingInfo;
                    if (purchasePanel.ConfirmPurchase() && cart != null && info != null)
                        cart.AddItem(info.category, info.DisplayNameOrFallback);
                }
                // CanConfirm 为 false 仅发生在"购买成功"短冷却期间，此次确认忽略
                break;

            case GazeInteractable.GazeActionType.CancelPurchase:
                if (purchasePanel != null) purchasePanel.Hide();
                break;
        }
    }

    private void ClearCurrentTarget()
    {
        if (currentTarget != null) { currentTarget.SetFocused(false); currentTarget = null; }
        dwellTimer = 0f; selectedCurrentTarget = false; SetProgress(0f);
    }

    private void SetProgress(float progress)
    {
        if (gazeProgressImage != null)
        {
            gazeProgressImage.fillAmount = progress;
            gazeProgressImage.enabled = progress > 0f && progress < 1f;
        }
        onProgressChanged?.Invoke(progress);
    }
}
