using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 眼动购物系统的一键搭建工具（仅编辑器）。菜单：Tools/眼动购物。
/// 帮你自动创建并连好：GazeManager（眼动数据源+注视控制器）、确认面板、购物车面板、准星，
/// 以及批量给选中的商品加 GazeInteractable + Collider。把手工拖拽降到最低。
/// </summary>
public static class EyeGazeSetup
{
    private const string Root = "Tools/眼动购物/";

    // ============ ① 一键创建整套系统 ============
    [MenuItem(Root + "① 创建眼动购物系统(GazeManager+面板+购物车+准星)")]
    public static void CreateSystem()
    {
        Camera cam = Camera.main;
        if (cam == null) cam = Object.FindObjectOfType<Camera>();
        Transform xrOrigin = FindXrOrigin(cam);

        // ---- GazeManager ----
        GameObject gazeManager = new GameObject("GazeManager");
        Undo.RegisterCreatedObjectUndo(gazeManager, "Create EyeGaze System");
        var inputSource = gazeManager.AddComponent<XREyeGazeInputSource>();
        var controller = gazeManager.AddComponent<GazeSelectionController>();

        // ---- 世界空间 Canvas（放相机前方） ----
        GameObject canvasGo = new GameObject("EyeGazeUI", typeof(Canvas));
        Undo.RegisterCreatedObjectUndo(canvasGo, "Create EyeGaze System");
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var canvasRt = canvas.GetComponent<RectTransform>();
        canvasRt.sizeDelta = new Vector2(1000, 700);
        canvasRt.localScale = Vector3.one * 0.0015f; // 约 1.5m x 1.05m
        if (cam != null)
        {
            canvasRt.position = cam.transform.position + cam.transform.forward * 2f;
            canvasRt.rotation = Quaternion.LookRotation(canvasRt.position - cam.transform.position);
        }

        // ---- 确认面板 ----
        var panelCtrl = BuildPurchasePanel(canvasRt, out GameObject panelRoot,
            out TMP_Text title, out TMP_Text desc, out TMP_Text status,
            out GameObject confirmBtn, out Collider confirmCol, out CanvasGroup confirmCg);

        // ---- 购物车面板 ----
        var cartMgr = BuildCartPanel(canvasRt, out TMP_Text cartText);

        // ---- 准星 ----
        Image reticle = BuildReticle(canvasRt);

        // ---- 连线：XREyeGazeInputSource ----
        var soInput = new SerializedObject(inputSource);
        SetRef(soInput, "fallbackCamera", cam);
        SetRef(soInput, "xrOriginTransform", xrOrigin);
        soInput.ApplyModifiedPropertiesWithoutUndo();

        // ---- 连线：GazeSelectionController ----
        var soCtrl = new SerializedObject(controller);
        SetRef(soCtrl, "gazeInputSourceBehaviour", inputSource);
        SetRef(soCtrl, "purchasePanel", panelCtrl);
        SetRef(soCtrl, "cart", cartMgr);
        SetRef(soCtrl, "gazeProgressImage", reticle);
        soCtrl.ApplyModifiedPropertiesWithoutUndo();

        // ---- 连线：PurchasePanelController ----
        var soPanel = new SerializedObject(panelCtrl);
        SetRef(soPanel, "panelRoot", panelRoot);
        SetRef(soPanel, "titleText", title);
        SetRef(soPanel, "descriptionText", desc);
        SetRef(soPanel, "statusText", status);
        SetRef(soPanel, "confirmButtonRoot", confirmBtn);
        SetRef(soPanel, "confirmButtonCollider", confirmCol);
        SetRef(soPanel, "confirmButtonCanvasGroup", confirmCg);
        soPanel.ApplyModifiedPropertiesWithoutUndo();

        // 面板初始隐藏（运行时也会自动隐藏，这里编辑器下先关掉方便观察）
        panelRoot.SetActive(false);

        Selection.activeGameObject = gazeManager;
        Debug.Log($"[眼动购物] 系统已创建。Camera={(cam ? cam.name : "未找到")}, " +
                  $"XR Origin={(xrOrigin ? xrOrigin.name : "未找到")}。" +
                  "请把场景里的商品选中后执行菜单②，并点一次菜单③修复中文字体。");
    }

    // ============ ② 批量给选中商品加交互 ============
    [MenuItem(Root + "② 给选中物体添加商品交互(GazeInteractable+Collider)")]
    public static void TagSelectedProducts()
    {
        var sel = Selection.gameObjects;
        if (sel == null || sel.Length == 0)
        {
            EditorUtility.DisplayDialog("眼动购物", "请先在 Hierarchy 里选中一个或多个商品物体。", "好");
            return;
        }

        int count = 0;
        foreach (var go in sel)
        {
            EnsureCollidersOnMeshes(go); // 先补碰撞体，避免随后 Reset() 再加一个多余的 BoxCollider
            if (go.GetComponent<GazeInteractable>() == null)
            {
                Undo.AddComponent<GazeInteractable>(go); // Reset() 会自动填 Renderer/scaleTarget
                count++;
            }
        }
        Debug.Log($"[眼动购物] 已为 {count} 个物体添加 GazeInteractable（共处理 {sel.Length} 个）。" +
                  "记得在每个物体的 Inspector 里设置 ProductInfo 的 Category 与 DisplayName。");
    }

    [MenuItem(Root + "② 给选中物体添加商品交互(GazeInteractable+Collider)", true)]
    public static bool TagSelectedProductsValidate() => Selection.gameObjects.Length > 0;

    // ============ ③ 修复中文字体 ============
    [MenuItem(Root + "③ 修复中文字体(导入黑体并设为TMP回退)")]
    public static void FixChineseFont()
    {
        const string fontsDir = "Assets/Fonts";
        const string destTtf = fontsDir + "/SimHei.ttf";
        const string fontAssetPath = fontsDir + "/SimHei SDF.asset";

        if (!Directory.Exists(fontsDir)) Directory.CreateDirectory(fontsDir);

        // 1) 把系统黑体复制进工程（打包到 PICO 需要字体在工程内）
        if (!File.Exists(destTtf))
        {
            string[] sysCandidates =
            {
                @"C:\Windows\Fonts\simhei.ttf",
                @"C:\Windows\Fonts\msyh.ttc",
                @"C:\Windows\Fonts\msyhbd.ttc",
                @"C:\Windows\Fonts\simsun.ttc"
            };
            string src = null;
            foreach (var c in sysCandidates) { if (File.Exists(c)) { src = c; break; } }
            if (src == null)
            {
                EditorUtility.DisplayDialog("眼动购物",
                    "未在 C:\\Windows\\Fonts 找到中文字体。请手动拖一个中文 .ttf 到 Assets/Fonts/ 后重试，" +
                    "或用 Window > TextMeshPro > Font Asset Creator 自建。", "好");
                return;
            }
            File.Copy(src, destTtf, true);
            AssetDatabase.ImportAsset(destTtf, ImportAssetOptions.ForceSynchronousImport);
        }

        Font font = AssetDatabase.LoadAssetAtPath<Font>(destTtf);
        if (font == null)
        {
            EditorUtility.DisplayDialog("眼动购物", "字体导入失败，请检查 " + destTtf, "好");
            return;
        }

        // 2) 创建/复用 TMP 动态字体资产（动态模式：用到的字形按需写入图集）
        TMP_FontAsset tmpFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontAssetPath);
        if (tmpFont == null)
        {
            tmpFont = TMP_FontAsset.CreateFontAsset(font); // 默认即 Dynamic
            AssetDatabase.CreateAsset(tmpFont, fontAssetPath);

            // 把材质和图集纹理作为子资产保存，否则资产会丢引用
            if (tmpFont.material != null)
            {
                tmpFont.material.name = "SimHei SDF Material";
                AssetDatabase.AddObjectToAsset(tmpFont.material, tmpFont);
            }
            if (tmpFont.atlasTextures != null)
            {
                foreach (var tex in tmpFont.atlasTextures)
                    if (tex != null) AssetDatabase.AddObjectToAsset(tex, tmpFont);
            }
            EditorUtility.SetDirty(tmpFont);
            AssetDatabase.SaveAssets();
        }

        // 3) 加入 TMP 默认字体的 fallback —— 全工程的中文都会自动回退渲染
        var def = TMP_Settings.defaultFontAsset;
        if (def != null)
        {
            if (def.fallbackFontAssetTable == null)
                def.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>();
            if (!def.fallbackFontAssetTable.Contains(tmpFont))
            {
                def.fallbackFontAssetTable.Add(tmpFont);
                EditorUtility.SetDirty(def);
            }
        }

        // 4) 双保险：直接把字体赋给 EyeGazeUI 下的所有 TMP 文本
        var ui = GameObject.Find("EyeGazeUI");
        if (ui != null)
        {
            foreach (var t in ui.GetComponentsInChildren<TMP_Text>(true))
                t.font = tmpFont;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[眼动购物] 中文字体已修复：{fontAssetPath} 已生成并设为 TMP 回退。" +
                  (def == null ? "（注意：未找到 TMP 默认字体，已直接赋给面板文本）" : ""));
    }

    // ===================== 构建辅助 =====================

    private static PurchasePanelController BuildPurchasePanel(RectTransform parent,
        out GameObject panelRoot, out TMP_Text title, out TMP_Text desc, out TMP_Text status,
        out GameObject confirmBtn, out Collider confirmCol, out CanvasGroup confirmCg)
    {
        panelRoot = NewUI("PurchasePanel", parent);
        var prt = panelRoot.GetComponent<RectTransform>();
        prt.sizeDelta = new Vector2(560, 640);
        prt.anchoredPosition = new Vector2(-220, 0);
        AddBackground(panelRoot, new Color(0f, 0f, 0f, 0.7f));
        var panelCtrl = panelRoot.AddComponent<PurchasePanelController>();

        title = AddText("Title", prt, new Vector2(0, 250), new Vector2(520, 90), 48, "商品名称", TextAlignmentOptions.Center);
        desc = AddText("Desc", prt, new Vector2(0, 170), new Vector2(520, 60), 32, "", TextAlignmentOptions.Center);
        status = AddText("Status", prt, new Vector2(0, 60), new Vector2(520, 120), 34, "注视下方按钮确认购买", TextAlignmentOptions.Center);

        // 确认按钮
        confirmBtn = AddButton("ConfirmButton", prt, new Vector2(0, -110), new Vector2(420, 130),
            new Color(0.15f, 0.6f, 0.2f, 0.95f), "确认购买", out confirmCol, out confirmCg);
        // 取消按钮
        GameObject cancelBtn = AddButton("CancelButton", prt, new Vector2(0, -260), new Vector2(420, 110),
            new Color(0.6f, 0.2f, 0.2f, 0.95f), "取消", out _, out _);

        // 给两个按钮配上 GazeInteractable 的动作类型
        SetGazeButtonType(confirmBtn, GazeInteractable.GazeActionType.ConfirmPurchase);
        SetGazeButtonType(cancelBtn, GazeInteractable.GazeActionType.CancelPurchase);

        return panelCtrl;
    }

    private static ShoppingCartManager BuildCartPanel(RectTransform parent, out TMP_Text cartText)
    {
        GameObject cartPanel = NewUI("CartPanel", parent);
        var crt = cartPanel.GetComponent<RectTransform>();
        crt.sizeDelta = new Vector2(360, 640);
        crt.anchoredPosition = new Vector2(320, 0);
        AddBackground(cartPanel, new Color(0f, 0f, 0f, 0.6f));

        AddText("CartTitle", crt, new Vector2(0, 270), new Vector2(340, 70), 40, "购物车", TextAlignmentOptions.Center);
        cartText = AddText("CartList", crt, new Vector2(0, -20), new Vector2(320, 520), 34, "购物车为空", TextAlignmentOptions.TopLeft);

        var cartMgr = cartPanel.AddComponent<ShoppingCartManager>();
        var so = new SerializedObject(cartMgr);
        SetRef(so, "cartText", cartText);
        so.ApplyModifiedPropertiesWithoutUndo();
        return cartMgr;
    }

    private static Image BuildReticle(RectTransform parent)
    {
        GameObject go = NewUI("Reticle", parent);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(48, 48);
        rt.anchoredPosition = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.color = new Color(1f, 1f, 0.2f, 0.9f);
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Radial360;
        img.fillAmount = 0f;
        img.enabled = false;
        return img;
    }

    // ---- 小工具 ----

    private static GameObject NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Create EyeGaze System");
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void AddBackground(GameObject go, Color color)
    {
        var img = go.AddComponent<Image>();
        img.color = color;
    }

    private static TMP_Text AddText(string name, Transform parent, Vector2 pos, Vector2 size,
        float fontSize, string text, TextAlignmentOptions align)
    {
        GameObject go = NewUI(name, parent);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = fontSize;
        t.alignment = align;
        t.color = Color.white;
        t.enableWordWrapping = true;
        return t;
    }

    private static GameObject AddButton(string name, Transform parent, Vector2 pos, Vector2 size,
        Color color, string label, out Collider col, out CanvasGroup cg)
    {
        GameObject go = NewUI(name, parent);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        var img = go.AddComponent<Image>();
        img.color = color;
        cg = go.AddComponent<CanvasGroup>();

        // 世界空间 UI 的注视命中靠 3D BoxCollider（注视用 Physics.Raycast）
        var box = go.AddComponent<BoxCollider>();
        box.size = new Vector3(size.x, size.y, 10f);
        col = box;

        go.AddComponent<GazeInteractable>();

        AddText(name + "_Label", rt, Vector2.zero, size, 40, label, TextAlignmentOptions.Center);
        return go;
    }

    /// <summary>设置按钮上 GazeInteractable 的动作类型（私有字段 actionType）。</summary>
    private static void SetGazeButtonType(GameObject buttonGo, GazeInteractable.GazeActionType type)
    {
        var gi = buttonGo.GetComponent<GazeInteractable>();
        if (gi == null) return;
        var so = new SerializedObject(gi);
        var prop = so.FindProperty("actionType");
        if (prop != null) prop.enumValueIndex = (int)type;
        // 按钮自身的高亮 Renderer 清空（UI 用 Image，不需要 MaterialPropertyBlock 高亮）
        var rends = so.FindProperty("highlightRenderers");
        if (rends != null) rends.arraySize = 0;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureCollidersOnMeshes(GameObject root)
    {
        // 给每个带 MeshFilter 且没有 Collider 的子物体补 MeshCollider，保证注视射线能打中真实网格
        var filters = root.GetComponentsInChildren<MeshFilter>();
        foreach (var mf in filters)
        {
            if (mf.GetComponent<Collider>() != null) continue;
            var mc = Undo.AddComponent<MeshCollider>(mf.gameObject);
            if (mf.sharedMesh != null) mc.sharedMesh = mf.sharedMesh;
        }
        if (filters.Length == 0 && root.GetComponentInChildren<Collider>() == null)
            Undo.AddComponent<BoxCollider>(root);
    }

    private static void SetRef(SerializedObject so, string field, Object value)
    {
        var p = so.FindProperty(field);
        if (p != null) p.objectReferenceValue = value;
        else Debug.LogWarning($"[眼动购物] 找不到字段 {field}，请手动连线。");
    }

    private static Transform FindXrOrigin(Camera cam)
    {
        // 优先按名字找 XR Origin，找不到就退到相机的根
        foreach (var t in Object.FindObjectsOfType<Transform>())
        {
            string n = t.name.ToLower();
            if (n.Contains("xr origin") || n.Contains("xrorigin") || n.Contains("xr rig"))
                return t;
        }
        if (cam != null) return cam.transform.root;
        return null;
    }
}
