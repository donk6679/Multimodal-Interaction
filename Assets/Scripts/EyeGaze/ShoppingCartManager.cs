using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 购物车 / 结算系统：实时统计已购商品的名称与数量，刷新固定的结算 UI（满足竞赛"结算界面"要求）。
/// 文本形如：
///   咖啡 x2
///   瓶装水 x1
/// </summary>
public class ShoppingCartManager : MonoBehaviour
{
    [Header("Checkout UI")]
    [Tooltip("显示购物车清单的 TMP 文本（固定在场景某处的结算面板上）")]
    [SerializeField] private TMP_Text cartText;
    [Tooltip("购物车为空时显示的占位文字")]
    [SerializeField] private string emptyHint = "购物车为空";

    [Header("Events")]
    [Tooltip("每次购物车变化后回调（参数=商品总件数）")]
    [SerializeField] private UnityEvent<int> onCartChanged;

    // 按品类计数
    private readonly Dictionary<ProductCategory, int> counts = new Dictionary<ProductCategory, int>();
    // 品类 → 显示名（以最后一次写入为准）
    private readonly Dictionary<ProductCategory, string> displayNames = new Dictionary<ProductCategory, string>();
    // 维持加入顺序，结算清单按购买先后排列
    private readonly List<ProductCategory> order = new List<ProductCategory>();

    private void Start() => RefreshUI();

    /// <summary>购买一件商品：对应品类 +1 并刷新结算 UI。</summary>
    public void AddItem(ProductCategory category, string displayName)
    {
        if (!counts.ContainsKey(category))
        {
            counts[category] = 0;
            order.Add(category);
        }
        counts[category]++;
        displayNames[category] = string.IsNullOrEmpty(displayName) ? category.ToString() : displayName;
        RefreshUI();
    }

    /// <summary>移除一件商品（备用，可用于纠正错选）。减到 0 时从清单移除。</summary>
    public void RemoveItem(ProductCategory category)
    {
        if (!counts.ContainsKey(category)) return;
        counts[category]--;
        if (counts[category] <= 0)
        {
            counts.Remove(category);
            displayNames.Remove(category);
            order.Remove(category);
        }
        RefreshUI();
    }

    /// <summary>清空购物车（备用）。</summary>
    public void Clear()
    {
        counts.Clear();
        displayNames.Clear();
        order.Clear();
        RefreshUI();
    }

    public int GetCount(ProductCategory category) => counts.TryGetValue(category, out int c) ? c : 0;

    public int TotalItems
    {
        get
        {
            int total = 0;
            foreach (var kv in counts) total += kv.Value;
            return total;
        }
    }

    private void RefreshUI()
    {
        int total = TotalItems;
        if (cartText != null)
        {
            if (order.Count == 0)
            {
                cartText.text = emptyHint;
            }
            else
            {
                StringBuilder sb = new StringBuilder();
                foreach (ProductCategory cat in order)
                {
                    string name = displayNames.TryGetValue(cat, out string n) ? n : cat.ToString();
                    sb.AppendLine($"{name} x{counts[cat]}");
                }
                cartText.text = sb.ToString().TrimEnd();
            }
        }
        onCartChanged?.Invoke(total);
    }
}
