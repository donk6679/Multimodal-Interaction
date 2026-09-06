using System;
using UnityEngine;

/// <summary>
/// 商品品类。对应竞赛出题的 9 种商品，作为购物车计数的 key。
/// </summary>
public enum ProductCategory
{
    MilkChocolate, // 牛奶巧克力
    Coffee,        // 咖啡
    Water,         // 瓶装水
    ToiletPaper,   // 卷纸
    EggBox,        // 盒装鸡蛋
    Sandwich,      // 三明治
    ChocolateBar,  // 巧克力棒
    Cereal,        // 谷物早餐
    Noodle         // 面条
}

/// <summary>
/// 单个商品的数据。挂在 GazeInteractable 上，购买时把 category 计入购物车。
/// 竞赛结算只需名称+数量；price 选填，仅用于面板美化。
/// </summary>
[Serializable]
public class ProductInfo
{
    [Tooltip("商品品类，购物车按此 key 计数")]
    public ProductCategory category = ProductCategory.Coffee;

    [Tooltip("面板/购物车显示用的名称，如 \"咖啡\"。留空则用品类英文名")]
    public string displayName = "";

    [Tooltip("单价（选填，仅用于面板显示）")]
    public int price = 0;

    /// <summary>取显示名，留空时回退到品类对应的中文名。</summary>
    public string DisplayNameOrFallback =>
        string.IsNullOrEmpty(displayName) ? GetChineseName(category) : displayName;

    /// <summary>品类 → 中文名映射（DisplayName 留空时使用）。</summary>
    public static string GetChineseName(ProductCategory category)
    {
        switch (category)
        {
            case ProductCategory.MilkChocolate: return "牛奶巧克力";
            case ProductCategory.Coffee:        return "咖啡";
            case ProductCategory.Water:         return "瓶装水";
            case ProductCategory.ToiletPaper:   return "卷纸";
            case ProductCategory.EggBox:        return "盒装鸡蛋";
            case ProductCategory.Sandwich:      return "三明治";
            case ProductCategory.ChocolateBar:  return "巧克力棒";
            case ProductCategory.Cereal:        return "谷物早餐";
            case ProductCategory.Noodle:        return "面条";
            default:                            return category.ToString();
        }
    }
}
