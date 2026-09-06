using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 可注视物体：挂在商品 / 确认按钮 / 取消按钮上。
/// 提供高亮 + 微放大反馈，并通过 GazeActionType 标记被选中后该做什么。
/// 高亮用 MaterialPropertyBlock（内置管线 _Color），不污染共享材质、不产生材质实例。
/// </summary>
public class GazeInteractable : MonoBehaviour
{
    public enum GazeActionType
    {
        BuyProduct,       // 商品：注视选中后弹出确认面板
        ConfirmPurchase,  // 面板上的「确认购买」按钮
        CancelPurchase    // 面板上的「取消」按钮
    }

    [Header("Interaction")]
    [SerializeField] private GazeActionType actionType = GazeActionType.BuyProduct;
    [Tooltip("仅 BuyProduct 类型需要填写")]
    [SerializeField] private ProductInfo productInfo = new ProductInfo();

    [Header("Visual Feedback")]
    [SerializeField] private Transform scaleTarget;
    [SerializeField] private float focusScaleMultiplier = 1.08f;
    [SerializeField] private Renderer[] highlightRenderers;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color focusColor = new Color(1f, 0.9f, 0.2f, 1f);

    [Header("Events")]
    [SerializeField] private UnityEvent onFocusEnter;
    [SerializeField] private UnityEvent onFocusExit;
    [SerializeField] private UnityEvent onSelected;

    // URP 用 _BaseColor，内置管线用 _Color；两个都试
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private bool hasFocus;
    private Vector3 initialScale;
    private MaterialPropertyBlock propertyBlock;

    public GazeActionType Type => actionType;
    public ProductInfo Info => productInfo;

    private void Awake()
    {
        if (scaleTarget == null) scaleTarget = transform;
        initialScale = scaleTarget.localScale;
        propertyBlock = new MaterialPropertyBlock();
        ApplyHighlightColor(normalColor);
    }

#if UNITY_EDITOR
    /// <summary>
    /// 在编辑器里添加本组件（或右键 Reset）时自动调用：
    /// 没有 Collider 就自动补一个（有 MeshFilter 用 MeshCollider 贴合网格，否则用 BoxCollider），
    /// 并自动填好 scaleTarget 与高亮用的 Renderer，省去手动拖拽。
    /// </summary>
    private void Reset()
    {
        if (GetComponentInChildren<Collider>() == null)
        {
            if (GetComponent<MeshFilter>() != null)
                gameObject.AddComponent<MeshCollider>();
            else
                gameObject.AddComponent<BoxCollider>();
        }

        if (scaleTarget == null) scaleTarget = transform;

        if (highlightRenderers == null || highlightRenderers.Length == 0)
            highlightRenderers = GetComponentsInChildren<Renderer>();
    }
#endif

    public void SetFocused(bool focused)
    {
        if (hasFocus == focused) return;
        hasFocus = focused;
        scaleTarget.localScale = focused ? initialScale * focusScaleMultiplier : initialScale;
        ApplyHighlightColor(focused ? focusColor : normalColor);
        if (focused) onFocusEnter?.Invoke(); else onFocusExit?.Invoke();
    }

    public void Select() => onSelected?.Invoke();

    private void ApplyHighlightColor(Color color)
    {
        if (highlightRenderers == null || highlightRenderers.Length == 0) return;
        for (int i = 0; i < highlightRenderers.Length; i++)
        {
            Renderer r = highlightRenderers[i];
            if (r == null) continue;
            r.GetPropertyBlock(propertyBlock);            // 用 MaterialPropertyBlock 不会改共享材质
            Material m = r.sharedMaterial;
            if (m != null && m.HasProperty(BaseColorId)) propertyBlock.SetColor(BaseColorId, color);
            else                                          propertyBlock.SetColor(ColorId, color);
            r.SetPropertyBlock(propertyBlock);
        }
    }
}
