using TMPro;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 商品确认面板：注视商品后弹出，显示商品信息 + 确认/取消按钮。
/// 注视确认按钮即购买成功，计入购物车。（已移除"靠近才能买"的距离判断）
/// </summary>
public class PurchasePanelController : MonoBehaviour
{
    [Header("Panel Root")]
    [SerializeField] private GameObject panelRoot;

    [Header("Text (TMP)")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [Tooltip("状态提示文字，如 \"注视下方按钮确认购买\" / \"购买成功\"")]
    [SerializeField] private TMP_Text statusText;

    [Header("Confirm Button (注视确认)")]
    [Tooltip("确认按钮根物体")]
    [SerializeField] private GameObject confirmButtonRoot;
    [Tooltip("确认按钮上的 Collider（注视射线要打中它）")]
    [SerializeField] private Collider confirmButtonCollider;
    [SerializeField] private CanvasGroup confirmButtonCanvasGroup; // 控制按钮显隐/置灰（alpha）

    [Header("Status Messages")]
    [SerializeField] private string promptInRange = "注视下方按钮确认购买";
    [SerializeField] private string promptSuccessFormat = "购买成功：{0}";
    [SerializeField] private float successHoldSeconds = 1.0f; // 显示"购买成功"并冷却的时长，防双买

    [Header("Events")]
    [SerializeField] private UnityEvent<ProductInfo> onPurchased;

    private GazeInteractable pendingProduct;
    private float successTimer = 0f; // >0 时处于"购买成功"冷却中，不可再次确认

    /// <summary>当前是否允许确认购买（有待购商品且不在"购买成功"冷却中）。GazeSelectionController 据此放行。</summary>
    public bool CanConfirm { get; private set; }

    public bool IsVisible => panelRoot != null && panelRoot.activeSelf;

    /// <summary>当前待购买商品的信息（无则 null）。供 GazeSelectionController 计入购物车。</summary>
    public ProductInfo PendingInfo => pendingProduct != null ? pendingProduct.Info : null;

    private void Awake()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    /// <summary>注视到商品时调用：记录 pending 商品并弹出面板。</summary>
    public void ShowProduct(GazeInteractable product)
    {
        if (product == null || product.Type != GazeInteractable.GazeActionType.BuyProduct) return;

        pendingProduct = product;
        successTimer = 0f;

        ProductInfo info = product.Info;
        if (titleText != null) titleText.text = info != null ? info.DisplayNameOrFallback : "";
        if (descriptionText != null)
            descriptionText.text = (info != null && info.price > 0) ? $"单价：{info.price}" : "";

        if (panelRoot != null) panelRoot.SetActive(true);
        UpdateConfirmState(); // 立刻刷新一次确认状态
    }

    private void Update()
    {
        if (!IsVisible) return;

        // 购买成功冷却：期间锁住确认，倒计时结束后恢复可购买判定
        if (successTimer > 0f)
        {
            successTimer -= Time.deltaTime;
            CanConfirm = false;
            return;
        }

        UpdateConfirmState();
    }

    /// <summary>更新确认按钮可用性。（已移除距离判断：只要有待购商品即可确认购买）</summary>
    private void UpdateConfirmState()
    {
        CanConfirm = pendingProduct != null;
        if (statusText != null) statusText.text = promptInRange;
        SetConfirmButtonEnabled(CanConfirm);
    }

    /// <summary>注视确认按钮触发：完成购买并进入短冷却。返回是否成功。</summary>
    public bool ConfirmPurchase()
    {
        if (!CanConfirm || pendingProduct == null) return false;

        ProductInfo info = pendingProduct.Info;
        if (info == null) return false;

        // 计入购物车（由 GazeSelectionController 在外部已持有 cart 时也可走那条路；这里通过事件解耦）
        onPurchased?.Invoke(info);

        if (statusText != null)
            statusText.text = string.Format(promptSuccessFormat, info.DisplayNameOrFallback);

        // 进入短冷却，防止一次停留被连续判定为多次购买
        successTimer = successHoldSeconds;
        CanConfirm = false;
        return true;
    }

    /// <summary>取消/关闭面板，清空 pending。</summary>
    public void Hide()
    {
        pendingProduct = null;
        successTimer = 0f;
        CanConfirm = false;
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    private void SetConfirmButtonEnabled(bool enabled)
    {
        if (confirmButtonCollider != null) confirmButtonCollider.enabled = enabled;
        if (confirmButtonCanvasGroup != null) confirmButtonCanvasGroup.alpha = enabled ? 1f : 0.4f;
        if (confirmButtonRoot != null && confirmButtonCanvasGroup == null)
            confirmButtonRoot.SetActive(true); // 保持可见，仅靠 collider/alpha 控制可点性
    }
}
