using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using System.IO;

// Builds the three shop prefabs from code so they are guaranteed-valid (correct
// script GUIDs and sprite references) without hand-writing YAML. Run it from the
// menu (Shop > Build Prefabs) or headless via -executeMethod ShopPrefabBuilder.BuildAll.
public static class ShopPrefabBuilder
{
    const string ART = "ShopArt/";
    const string OUT = "Assets/Resources/Prefabs/";

    const string PNL = "cartoon_icon_panel_background_beach_event";
    const string TOP = "cartoon_store_topbar_beach_blue";
    const string TITLE_WOOD = "cartoon_store_title";
    const string TITLE_YELLOW = "catoon_icon_toptitle_yellow";
    const string CELL = "cartoon_icon_guestbackground02_blue";
    const string CURPILL = "currency_pill_dark";
    const string PRICEBG = "price_pill_offwhite";
    const string TIMERBG = "timer_plate_navy";

    static Sprite S(string n) => Resources.Load<Sprite>(ART + n);

    [MenuItem("Shop/Build Prefabs")]
    public static void BuildAllMenu() { BuildAll(); AssetDatabase.SaveAssets(); }

    public static void BuildAll()
    {
        Directory.CreateDirectory(OUT);
        BuildCurrencyPill();
        BuildShopItem();
        BuildShopUI();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Shop] Prefabs built at " + OUT);
    }

    // ---------- helpers ----------
    static Image Img(string n, Sprite s, Transform parent)
    {
        var g = new GameObject(n, typeof(RectTransform), typeof(Image));
        g.transform.SetParent(parent, false);
        var img = g.GetComponent<Image>();
        img.sprite = s; img.type = Image.Type.Simple; img.preserveAspect = false;
        return img;
    }

    static Text Txt(string n, string t, int size, FontStyle style, Color color, Transform parent)
    {
        var g = new GameObject(n, typeof(RectTransform), typeof(Text));
        g.transform.SetParent(parent, false);
        var tx = g.GetComponent<Text>();
        tx.text = t; tx.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        tx.fontSize = size; tx.fontStyle = style; tx.color = color;
        tx.alignment = TextAnchor.MiddleCenter;
        var ol = g.AddComponent<Outline>(); ol.effectColor = Color.black; ol.effectDistance = new Vector2(2, 2);
        return tx;
    }

    // Same as Txt(), but with a WHITE outline instead of black. Used for the
    // price number, which is dark brown on a near-white strip and therefore
    // needs a light halo (not a black one) to stay readable.
    static Text TxtLightOutline(string n, string t, int size, FontStyle style, Color color, Transform parent)
    {
        var tx = Txt(n, t, size, style, color, parent);
        var ol = tx.GetComponent<Outline>();
        ol.effectColor = Color.white;
        ol.effectDistance = new Vector2(2, 2);
        return tx;
    }

    // ================= CurrencyPill =================
    // Layout follows the reference artwork: a thin charcoal capsule with the
    // currency gem sitting entirely OUTSIDE its LEFT end (measured on the
    // reference: gem spans x 30..48, bar spans x 48..99 — the gem touches the
    // bar's left edge but does not overlap it) and the amount centred INSIDE
    // the bar.
    static void BuildCurrencyPill()
    {
        const float barW = 100f, barH = 32f;
        const float iconSize = 36f;

        var root = new GameObject("CurrencyPill", typeof(RectTransform), typeof(Image), typeof(CurrencyPill));
        var rt = root.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(barW, barH);
        var bg = root.GetComponent<Image>();
        // The charcoal tone lives in the sprite itself (sampled #333639 from the
        // reference); tinting it would be pointless, so keep the colour white.
        bg.sprite = S(CURPILL); bg.color = Color.white;
        bg.type = Image.Type.Sliced;   // 9-slice capsule (16px radius)

        var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        icon.transform.SetParent(root.transform, false);
        var irt = icon.GetComponent<RectTransform>();
        irt.anchorMin = irt.anchorMax = new Vector2(0f, 0.5f);
        irt.sizeDelta = new Vector2(iconSize, iconSize);
        // The gem sits entirely to the LEFT of the bar, touching its left edge,
        // and is taller than the bar (measured on the reference artwork).
        irt.anchoredPosition = new Vector2(-iconSize / 2f - 1f, 0f);
        icon.GetComponent<Image>().preserveAspect = true;

        var amt = new GameObject("Amount", typeof(RectTransform), typeof(Text));
        amt.transform.SetParent(root.transform, false);
        var art = amt.GetComponent<RectTransform>();
        art.anchorMin = new Vector2(0f, 0.5f);
        art.anchorMax = new Vector2(1f, 0.5f);
        art.offsetMin = new Vector2(2f, -16f);
        art.offsetMax = new Vector2(-2f, 16f);
        var at = amt.GetComponent<Text>();
        at.text = "0"; at.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        at.fontSize = 26; at.fontStyle = FontStyle.Bold; at.color = Color.white;
        at.alignment = TextAnchor.MiddleCenter;

        var so = new SerializedObject(root.GetComponent<CurrencyPill>());
        so.FindProperty("bg").objectReferenceValue = bg;
        so.FindProperty("iconImg").objectReferenceValue = icon.GetComponent<Image>();
        so.FindProperty("amountTxt").objectReferenceValue = at;
        so.ApplyModifiedProperties();

        Save(root, OUT + "CurrencyPill.prefab");
    }

    // ================= ShopItem =================
    static void BuildShopItem()
    {
        float cw = 161f, ch = cw * (272f / 188f); // reference cell size

        var root = new GameObject("ShopItem", typeof(RectTransform), typeof(CanvasGroup), typeof(ShopItem));
        var rt = root.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        var bg = Img("Background", S(CELL), root.transform);

        var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        icon.transform.SetParent(root.transform, false);
        icon.GetComponent<Image>().preserveAspect = true;

        var name = Txt("Name", "Item", 18, FontStyle.Bold, Color.white, root.transform);

        // Price strip: the item's own NEAR-WHITE plate (a 9-slice placeholder
        // asset — see price_pill_offwhite.png, colours sampled from the
        // reference). It is intentionally NOT the same dark pill used by the
        // top-bar currency chips.
        var pricePill = Img("PricePill", S(PRICEBG), root.transform);
        pricePill.color = Color.white;
        pricePill.type = Image.Type.Sliced;   // 9-slice rounded rect (8px borders)

        var priceIcon = new GameObject("PriceIcon", typeof(RectTransform), typeof(Image));
        priceIcon.transform.SetParent(pricePill.transform, false);
        priceIcon.GetComponent<Image>().preserveAspect = true;

        // Dark brown number (sampled from the reference price strip).
        var priceTxt = TxtLightOutline("Price", "", 18, FontStyle.Bold, new Color(91f / 255f, 54f / 255f, 33f / 255f), pricePill.transform);
        priceTxt.alignment = TextAnchor.MiddleLeft;

        var sold = new GameObject("SoldOut", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        sold.transform.SetParent(root.transform, false);
        var soldImg = sold.GetComponent<Image>();
        soldImg.color = new Color(0f, 0f, 0f, 0f);   // transparent scrim (see the reference)
        var soldCG = sold.GetComponent<CanvasGroup>();

        // White "Sold Out" with a dark-brown outline (reference artwork).
        var stamp = Txt("Stamp", "Sold Out", 22, FontStyle.Bold, Color.white, sold.transform);
        var sol = stamp.gameObject.GetComponent<Outline>();
        sol.effectColor = new Color(91f / 255f, 54f / 255f, 33f / 255f, 1f);
        sol.effectDistance = new Vector2(2, 2);

        // wire refs
        var so = new SerializedObject(root.GetComponent<ShopItem>());
        so.FindProperty("bg").objectReferenceValue = bg;
        so.FindProperty("iconImg").objectReferenceValue = icon.GetComponent<Image>();
        so.FindProperty("nameTxt").objectReferenceValue = name;
        so.FindProperty("pricePill").objectReferenceValue = pricePill;
        so.FindProperty("priceIcon").objectReferenceValue = priceIcon.GetComponent<Image>();
        so.FindProperty("priceTxt").objectReferenceValue = priceTxt;
        so.FindProperty("soldOverlay").objectReferenceValue = soldImg;
        so.FindProperty("soldTxt").objectReferenceValue = stamp;
        so.FindProperty("soldCG").objectReferenceValue = soldCG;
        so.ApplyModifiedProperties();

        // preview layout so the prefab looks right in the editor
        var item = root.GetComponent<ShopItem>();
        item.ApplyLayout(cw, ch);
        // light preview sprites (overridden at runtime by Init/SetData)
        bg.sprite = S(CELL);
        icon.GetComponent<Image>().sprite = S("cartoon_icon_drum_rank4_128x128");
        priceIcon.GetComponent<Image>().sprite = S("cartoon_icon_pearl_rank2_128x128");

        Save(root, OUT + "ShopItem.prefab");
    }

    // ================= ShopUI =================
    static void BuildShopUI()
    {
        var canvasGO = new GameObject("ShopCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var cv = canvasGO.GetComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = 100;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(740, 1440);
        scaler.matchWidthOrHeight = 0.5f;
        var sm = canvasGO.AddComponent<ShopManager>();

        // panel
        var panelGO = new GameObject("ShopPanel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        panelGO.transform.SetParent(canvasGO.transform, false);
        var prt = panelGO.GetComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(740f, 1440f);
        var pimg = panelGO.GetComponent<Image>();
        // The panel art is a 9-slice sprite (see its .json: top border 1590px,
        // bottom 51px). Render as Sliced so the header/footer art stays crisp and
        // only the middle blue body stretches to fill the 1440-tall panel.
        pimg.sprite = S(PNL); pimg.type = Image.Type.Sliced; pimg.preserveAspect = false;
        var pcg = panelGO.GetComponent<CanvasGroup>();

        // top bar
        var tb = Img("TopBar", S(TOP), panelGO.transform);
        var t = tb.rectTransform; t.anchorMin = t.anchorMax = new Vector2(0.5f, 1f);
        float th = 112f * (740f / 720f);
        t.sizeDelta = new Vector2(740f, th); t.anchoredPosition = new Vector2(0f, -th / 2f);

        // title
        var wood = Img("TitleWood", S(TITLE_WOOD), panelGO.transform);
        wood.rectTransform.anchorMin = wood.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        wood.rectTransform.sizeDelta = new Vector2(220f, 68f);
        wood.rectTransform.anchoredPosition = new Vector2(0f, -175f);
        wood.type = Image.Type.Sliced;     // 9-slice plaque (58px side borders)
        var yellow = Img("TitleYellow", S(TITLE_YELLOW), panelGO.transform);
        yellow.rectTransform.anchorMin = yellow.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        yellow.rectTransform.sizeDelta = new Vector2(150f, 96f);
        yellow.rectTransform.anchoredPosition = new Vector2(0f, -179f);
        yellow.type = Image.Type.Sliced;   // 9-slice plaque (44px side borders)
        var ttext = Txt("TitleText", "Trades", 34, FontStyle.Bold, new Color(0.45f, 0.18f, 0.05f), yellow.rectTransform);
        ttext.rectTransform.anchorMin = Vector2.zero; ttext.rectTransform.anchorMax = Vector2.one;
        ttext.rectTransform.offsetMin = Vector2.zero; ttext.rectTransform.offsetMax = Vector2.zero;

        // (Close / Info buttons intentionally omitted — they are not part of the
        // shop reference layout and were not wired to any behaviour.)

        // countdown plate. Measured on the reference artwork: a DEEP-NAVY rectangle
        // (RGB 21,35,63) sitting on the panel background (38,62,124) - NOT a
        // grey box, and NOT the translucent black bar from the art pack (tinting
        // a black sprite is a no-op, which is why that bar rendered as dead
        // black). The plate carries its own baked navy sprite instead.
        var tbBg = Img("TimerBg", S(TIMERBG), panelGO.transform);
        tbBg.rectTransform.anchorMin = tbBg.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        tbBg.rectTransform.sizeDelta = new Vector2(340f, 44f);
        tbBg.rectTransform.anchoredPosition = new Vector2(0f, -240f);
        tbBg.color = Color.white;
        tbBg.type = Image.Type.Sliced;     // rounded-rect plate (6px borders)

        // "Refreshes: 7h 59m" - one centred line, all white in the reference.
        var tContent = new GameObject("TimerContent", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        tContent.transform.SetParent(tbBg.rectTransform, false);
        var tcrt = tContent.GetComponent<RectTransform>();
        tcrt.anchorMin = Vector2.zero; tcrt.anchorMax = Vector2.one;
        tcrt.offsetMin = new Vector2(12f, 0f); tcrt.offsetMax = new Vector2(-12f, 0f);
        var thlg = tContent.GetComponent<HorizontalLayoutGroup>();
        thlg.spacing = 18f; thlg.childAlignment = TextAnchor.MiddleCenter;
        thlg.childControlWidth = false; thlg.childControlHeight = false;
        thlg.childForceExpandWidth = false; thlg.childForceExpandHeight = false;

        var tlabel = Txt("TimerLabel", "Refreshes:", 20, FontStyle.Bold, Color.white, tContent.transform);
        tlabel.rectTransform.sizeDelta = new Vector2(150f, 30f);
        tlabel.alignment = TextAnchor.MiddleCenter;
        var tval = Txt("Timer", "00:00", 22, FontStyle.Bold, Color.white, tContent.transform);
        tval.rectTransform.sizeDelta = new Vector2(100f, 30f);
        tval.alignment = TextAnchor.MiddleCenter;

        // currency row (dynamic pills go here)
        var crow = new GameObject("CurrencyRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        crow.transform.SetParent(panelGO.transform, false);
        var crt = crow.GetComponent<RectTransform>();
        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 1f);
        crt.sizeDelta = new Vector2(690f, 60f);
        crt.anchoredPosition = new Vector2(0f, -70f);
        var hlg = crow.GetComponent<HorizontalLayoutGroup>();
        // The left padding reserves room for the first chip's gem, which sits
        // entirely to the LEFT of its bar (as in the reference artwork).
        hlg.spacing = 80f; hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = false; hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
        hlg.padding = new RectOffset(56, 10, 6, 6);

        // grid (dynamic items go here)
        var gridGO = new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup));
        gridGO.transform.SetParent(panelGO.transform, false);
        var grt = gridGO.GetComponent<RectTransform>();
        grt.anchorMin = new Vector2(0f, 0f); grt.anchorMax = new Vector2(1f, 1f);
        // Left/right inset 0: the grid's own 51px outer margin comes from
        // ShopManager.BuildGrid (measured from the reference artwork).
        grt.offsetMin = new Vector2(0f, 40f); grt.offsetMax = new Vector2(0f, -320f);

        // flash overlay
        var flashGO = new GameObject("Flash", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        flashGO.transform.SetParent(panelGO.transform, false);
        var frt = flashGO.GetComponent<RectTransform>();
        frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
        frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;
        var fimg = flashGO.GetComponent<Image>(); fimg.color = Color.white; fimg.type = Image.Type.Simple;
        fimg.raycastTarget = false;              // never intercept clicks for the grid below
        var fcg = flashGO.GetComponent<CanvasGroup>(); fcg.alpha = 0f;
        fcg.blocksRaycasts = false;              // transparent to pointer events

        // wire ShopManager refs
        var so = new SerializedObject(sm);
        so.FindProperty("panel").objectReferenceValue = panelGO.GetComponent<RectTransform>();
        so.FindProperty("gridRect").objectReferenceValue = gridGO.GetComponent<RectTransform>();
        so.FindProperty("currencyRow").objectReferenceValue = crow.transform;
        so.FindProperty("timerText").objectReferenceValue = tval;
        so.FindProperty("panelCG").objectReferenceValue = pcg;
        so.FindProperty("flash").objectReferenceValue = fimg;
        so.FindProperty("flashCG").objectReferenceValue = fcg;
        so.ApplyModifiedProperties();

        Save(canvasGO, OUT + "ShopUI.prefab");
    }

    static void Save(GameObject go, string path)
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) AssetDatabase.DeleteAsset(path);
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        if (prefab == null) Debug.LogError("[Shop] Failed to save prefab: " + path);
        Object.DestroyImmediate(go);
    }
}
