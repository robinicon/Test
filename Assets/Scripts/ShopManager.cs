using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// Drives the whole beach-shop UI. The static interface lives in the ShopUI
// prefab (Canvas + ShopPanel + all fixed parts); this component is placed on
// that prefab and references its parts through serialized fields. At runtime it
// dynamically instantiates the two *repeated* elements — currency pills and shop
// items — from their own prefabs, then runs the open/close + refresh state
// machine, the countdown timer, and random restocking. No procedural UI building.
[System.Serializable]
public struct ShopArt
{
    // Only the sprites consumed at runtime (by ShopItem). The panel / top bar /
    // title / pill backgrounds are baked directly into the ShopUI prefab by the
    // prefab builder, so they don't need to live here.
    //  - cellNormal / cellEmpty : item card backgrounds
    //  - priceBg                : the NEAR-WHITE strip behind each item's price
    //                             (top-bar currency pills intentionally use a
    //                             separate dark pill baked into CurrencyPill.prefab)
    public Sprite cellNormal, cellEmpty, priceBg;
}

[AddComponentMenu("Shop/ShopManager")]
public class ShopManager : MonoBehaviour
{
    [Header("Art (auto-loaded from Resources/ShopArt)")]
    public ShopArt art;

    [Header("Layout")]
    // The reference artwork shows a 3-column grid (measured on the "Trades"
    // mock: card columns centred at roughly x=76 / 195 / 324 of 390 px).
    public int columns = 3;
    public int rows = 4;
    public float panelWidth = 740f;
    public float panelHeight = 1440f;
    public float cellGap = 16f;

    [Header("Timing")]
    public float refreshSeconds = 30f;
    public float openDuration = 0.35f;
    public float enterStagger = 0.06f;
    public float criticalTime = 10f;

    [Header("Catalog")]
    public List<ItemDef> catalog = new List<ItemDef>();

    [Header("Currencies (top bar)")]
    public List<CurrencyDef> currencies = new List<CurrencyDef>();

    [Header("Prefabs (loaded from Resources/Prefabs)")]
    public GameObject currencyPillPrefab;
    public GameObject shopItemPrefab;

    // Wired inside the ShopUI prefab.
    [SerializeField] private RectTransform panel, gridRect, currencyRow;
    [SerializeField] private Text timerText;
    [SerializeField] private CanvasGroup panelCG, flashCG;
    [SerializeField] private Image flash;

    private List<ShopItem> items = new List<ShopItem>();
    private List<CurrencyPill> currencyPills = new List<CurrencyPill>();
    private List<int> currencyAmounts = new List<int>();
    private float timeLeft;
    private bool isOpen, refreshing;

    // ---- art file names (under Resources/ShopArt) ----
    const string CELL = "cartoon_icon_guestbackground02_blue";
    const string CELL_EMPTY = "cartoon_icon_guestbackground02_empty_blue";
    const string PRICEBG = "price_pill_offwhite";
    const string TIMERBG = "timer_plate_navy";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Object.FindObjectOfType<ShopManager>() != null) return;
        var prefab = Resources.Load<GameObject>("Prefabs/ShopUI");
        if (prefab == null) { Debug.LogError("[Shop] Missing Resources/Prefabs/ShopUI — run Shop/Build Prefabs."); return; }
        var go = Instantiate(prefab);
        Object.DontDestroyOnLoad(go);
        if (FindObjectOfType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    void Awake()
    {
        if (currencyPillPrefab == null) currencyPillPrefab = Resources.Load<GameObject>("Prefabs/CurrencyPill");
        if (shopItemPrefab == null) shopItemPrefab = Resources.Load<GameObject>("Prefabs/ShopItem");
        LoadArt();
        BuildCatalog();
        BuildCurrencies();
        BuildGrid();

        // The Flash overlay covers the whole panel and sits on top of the grid.
        // Make it transparent to raycasts so item clicks actually reach the
        // cards underneath (otherwise every click is swallowed by the invisible
        // flash and nothing on the grid is clickable).
        if (flash != null) { flash.raycastTarget = false; if (flashCG != null) flashCG.blocksRaycasts = false; }

        // Runtime safeguard: enforce the correct 9-slice rendering and hide the
        // decorative elements that are not part of the reference layout. This keeps
        // the visuals correct even before "Shop > Build Prefabs" regenerates the
        // prefab from the updated builder.
        EnforceRuntimeVisuals();

        timeLeft = refreshSeconds;
        OpenShop();
    }

    void Update()
    {
        if (!isOpen || refreshing) return;
        timeLeft -= Time.deltaTime;
        if (timeLeft <= 0f)
        {
            timeLeft = 0f;
            refreshing = true;            // guard: exactly one refresh cycle
            StartCoroutine(RefreshRoutine());
        }
        UpdateTimer();
    }

    // ---------- art / data ----------
    Sprite Art(string n) => Resources.Load<Sprite>("ShopArt/" + n);
    void LoadArt()
    {
        // Only the per-item assets are resolved at runtime; the static UI
        // (panel, top bar, title, pill backgrounds) is baked into the prefab.
        art.cellNormal = Art(CELL);
        art.cellEmpty = Art(CELL_EMPTY);
        art.priceBg = Art(PRICEBG);
    }

    void BuildCatalog()
    {
        if (catalog == null || catalog.Count == 0) catalog = ShopCatalog.DefaultCatalog();
        for (int i = 0; i < catalog.Count; i++)
        {
            var d = catalog[i];
            d.icon = Art(d.spriteName);
            if (d.icon == null) Debug.LogWarning("[Shop] missing art for " + d.spriteName);
            catalog[i] = d;
        }
    }

    // ---------- dynamic instantiation ----------
    void BuildCurrencies()
    {
        if (currencies == null || currencies.Count == 0)
            currencies = CurrencyCatalog.DefaultCurrencies();
        currencyPills.Clear();
        currencyAmounts.Clear();

        if (currencyPillPrefab == null) { Debug.LogError("[Shop] currencyPillPrefab missing."); return; }
        for (int i = 0; i < currencies.Count; i++)
        {
            var c = currencies[i];
            c.icon = Art(c.iconName);
            currencies[i] = c;

            var go = Instantiate(currencyPillPrefab, currencyRow);
            var pill = go.GetComponent<CurrencyPill>();
            pill.Init(c.icon, c.startAmount);
            currencyPills.Add(pill);
            currencyAmounts.Add(c.startAmount);
        }
    }

    // Called by ShopItem when an item is purchased: deduct the cost from the
    // matching currency and refresh its amount label (with a small pop).
    public void Spend(int currencyIndex, int amount)
    {
        if (currencyIndex < 0 || currencyIndex >= currencyAmounts.Count) return;
        int after = Mathf.Max(0, currencyAmounts[currencyIndex] - amount);
        currencyAmounts[currencyIndex] = after;
        if (currencyIndex < currencyPills.Count)
            currencyPills[currencyIndex].SetAmount(after);
    }

    void BuildGrid()
    {
        if (shopItemPrefab == null) { Debug.LogError("[Shop] shopItemPrefab missing."); return; }
        var grid = gridRect.GetComponent<GridLayoutGroup>();
        // Geometry mirrors the reference artwork, measured on the 390px-wide
        // "Trades" mock and scaled to the 740px design width (k = 1.897):
        //   card  97px -> 184px wide (asset aspect 188x272 -> 266px tall)
        //   gap   52px ->  99px between columns
        //   left  27px ->  51px outer margin
        float margin = 51f;
        float cellGapX = 99f;
        float innerW = panelWidth - margin * 2f;
        float cellW = (innerW - (columns - 1) * cellGapX) / columns;
        float cellH = cellW * (272f / 188f);
        grid.cellSize = new Vector2(cellW, cellH);
        grid.spacing = new Vector2(cellGapX, cellGap);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.padding = new RectOffset(0, 0, 0, 0);

        int count = columns * rows;
        items.Clear();
        for (int i = 0; i < count; i++)
        {
            var d = catalog[Random.Range(0, catalog.Count)];
            var go = Instantiate(shopItemPrefab, gridRect);
            var item = go.GetComponent<ShopItem>();
            item.Init(d, art, this, cellW, cellH);
            items.Add(item);
        }
    }

    // ---------- runtime visual enforcement ----------
    // Applies the 9-slice rendering and removes non-reference decorations at
    // runtime, so the look is correct regardless of whether the prefab was just
    // regenerated by "Shop > Build Prefabs".
    void EnforceRuntimeVisuals()
    {
        var panelImg = panel.GetComponent<Image>();
        if (panelImg != null) panelImg.type = Image.Type.Sliced;

        // The countdown plate in the reference is a DEEP-NAVY panel, not a grey
        // box. The art pack only ships a translucent black bar, and tinting a
        // black sprite is a no-op (black x any colour stays black) - so the
        // plate uses its own baked navy sprite and keeps a white tint.
        var timerBg = panel.Find("TimerBg");
        if (timerBg != null)
        {
            var im = timerBg.GetComponent<Image>();
            if (im != null)
            {
                var navy = Art(TIMERBG);
                if (navy != null) im.sprite = navy;
                im.type = Image.Type.Sliced;
                im.color = Color.white;
            }
        }

        SetSlicedIfPresent("TitleWood");
        SetSlicedIfPresent("TitleYellow");

        HideIfPresent("CloseButton");
        HideIfPresent("InfoButton");
        HideIfPresent("Rope");
    }

    void SetSlicedIfPresent(string childName)
    {
        var t = panel.Find(childName);
        if (t == null) return;
        var im = t.GetComponent<Image>();
        if (im != null) im.type = Image.Type.Sliced;
    }

    void HideIfPresent(string childName)
    {
        var t = panel.Find(childName);
        if (t != null) t.gameObject.SetActive(false);
    }

    // ---------- state machine ----------
    void OpenShop()
    {
        isOpen = false; refreshing = false;
        panelCG.alpha = 0f;
        panel.localScale = Vector3.one * 0.92f;
        this.Tween(openDuration, e =>
        {
            panelCG.alpha = e;
            panel.localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, e);
        }, Ease.OutCubic, 0f, () =>
        {
            isOpen = true;
            for (int i = 0; i < items.Count; i++)
                items[i].PlayEnter(i * enterStagger, openDuration);
        });
    }

    // Note: the close button is intentionally removed — it is not part of the
    // shop reference layout. The panel stays open; the refresh loop re-stocks items.

    IEnumerator RefreshRoutine()
    {
        refreshing = true;
        yield return StartCoroutine(FlashRoutine());
        Restock();
        timeLeft = refreshSeconds;
        refreshing = false;
        isOpen = true;
        for (int i = 0; i < items.Count; i++)
            items[i].PlayEnter(i * enterStagger, openDuration);
    }

    void Restock()
    {
        foreach (var it in items)
            it.ResetItem(catalog[Random.Range(0, catalog.Count)]);
    }

    IEnumerator FlashRoutine()
    {
        float dur = 0.5f;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / dur;
            flashCG.alpha = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * 0.85f;
            yield return null;
        }
        flashCG.alpha = 0f;
    }

    void UpdateTimer()
    {
        int s = Mathf.CeilToInt(timeLeft);
        int m = s / 60, sec = s % 60;
        timerText.text = string.Format("{0:00}:{1:00}", m, sec);
        // The reference artwork shows the countdown in plain white. Keep white
        // as the normal state and only escalate to red in the final seconds
        // (a gameplay affordance, not a static layout detail).
        if (timeLeft <= criticalTime)
        {
            float k = 1f - (timeLeft / criticalTime);
            timerText.color = Color.Lerp(Color.white, Color.red, k);
            timerText.transform.localScale = Vector3.one * (0.9f + 0.12f * (0.5f + 0.5f * Mathf.Sin(Time.time * 8f)));
        }
        else
        {
            timerText.color = Color.white;
            timerText.transform.localScale = Vector3.one;
        }
    }
}
