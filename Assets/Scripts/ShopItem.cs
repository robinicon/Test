using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

// One shop card. Lives as the ShopItem prefab: its visual hierarchy (background,
// icon, name, price pill, sold-out overlay) is authored in the prefab, and this
// component wires those children via serialized references. It owns the card
// animations: staggered entrance, hover lift, and the "Sold Out" transition
// (swaps to the empty cell background + elastic "SOLD OUT" stamp + dimmed icon),
// plus the parabolic buy fly-out.
[RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
public class ShopItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public ItemDef def;
    public bool soldOut;

    // Wired inside the ShopItem prefab.
    [SerializeField] private Image bg, iconImg, pricePill, priceIcon, soldOverlay;
    [SerializeField] private Text nameTxt, priceTxt, soldTxt;
    [SerializeField] private CanvasGroup soldCG;

    private RectTransform rt;
    private CanvasGroup cg;
    private Coroutine scaleTween, enterTween;
    private bool clickEnabled = true;
    private ShopManager manager;
    private Sprite normalBg, emptyBg;
    private readonly Color iconBase = Color.white;

    [Header("Fly-out (purchase parabola)")]
    public float flyUpSpeed = 700f;      // upward launch speed (px/s, at 1280 reference height); peak height ~half of 1000
    public float flyGravity = 2400f;     // gravity (px/s^2)
    public float flyDrift = 160f;        // random horizontal drift range (px/s)
    public float flySpin = 260f;         // random spin speed (deg/s)

    // Called once right after the prefab is instantiated, with the data and the
    // shared art (cell backgrounds + the near-white price-strip background) plus the manager.
    public void Init(ItemDef d, ShopArt art, ShopManager mgr, float cellW, float cellH)
    {
        manager = mgr;
        normalBg = art.cellNormal;
        emptyBg = art.cellEmpty;
        rt = GetComponent<RectTransform>();
        cg = GetComponent<CanvasGroup>();

        bg.sprite = art.cellNormal;
        bg.type = Image.Type.Simple; bg.preserveAspect = false;

        // Price strip uses its OWN near-white plate (per the shop reference the
        // item price tag is a very light blue-white strip with a DARK BROWN
        // number, while the top-bar currency pills are charcoal — so they
        // intentionally do NOT share a background). The sprite is a 9-slice so
        // the corners stay crisp at any strip width.
        pricePill.sprite = art.priceBg;
        pricePill.type = Image.Type.Sliced; pricePill.preserveAspect = false;
        pricePill.color = Color.white;

        // Dark brown number on the light strip (sampled from the reference).
        priceTxt.color = new Color(91f / 255f, 54f / 255f, 33f / 255f, 1f);

        ApplyLayout(cellW, cellH);
        SetData(d);
    }

    // Positions/sizes every child relative to the grid cell. Safe to call again
    // (e.g. if the cell size changes) — it only touches layout, not data.
    public void ApplyLayout(float cellW, float cellH)
    {
        bg.rectTransform.anchorMin = Vector2.zero; bg.rectTransform.anchorMax = Vector2.one;
        bg.rectTransform.offsetMin = Vector2.zero; bg.rectTransform.offsetMax = Vector2.zero;

        iconImg.rectTransform.anchorMin = iconImg.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        iconImg.rectTransform.anchoredPosition = new Vector2(0f, cellH * 0.14f);
        iconImg.rectTransform.sizeDelta = new Vector2(cellW * 0.62f, cellW * 0.62f);
        iconImg.preserveAspect = true;

        nameTxt.rectTransform.anchorMin = nameTxt.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        nameTxt.rectTransform.anchoredPosition = new Vector2(0f, -cellH * 0.20f);
        nameTxt.rectTransform.sizeDelta = new Vector2(cellW * 0.92f, 26f);

        // price strip at the bottom: near-white plate + currency gem (left) + price (right).
        // Proportions measured on the reference artwork: the strip spans 96%
        // of the card width and is ~20% of the card width in height.
        float pillW = cellW * 0.96f;
        float pillH = cellW * 0.196f;
        pricePill.rectTransform.anchorMin = pricePill.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        pricePill.rectTransform.anchoredPosition = new Vector2(0f, -cellH * 0.36f);
        pricePill.rectTransform.sizeDelta = new Vector2(pillW, pillH);

        float pIcon = pillH * 0.8f;
        priceIcon.rectTransform.SetParent(pricePill.rectTransform, false);
        priceIcon.rectTransform.anchorMin = priceIcon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        priceIcon.rectTransform.sizeDelta = new Vector2(pIcon, pIcon);
        priceIcon.rectTransform.anchoredPosition = new Vector2(pIcon / 2f + 10f, 0f);
        priceIcon.preserveAspect = true;

        priceTxt.rectTransform.SetParent(pricePill.rectTransform, false);
        priceTxt.rectTransform.anchorMin = priceTxt.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        float numW = pillW - (pIcon + 10f) - 12f;
        priceTxt.rectTransform.sizeDelta = new Vector2(numW, 34f);
        priceTxt.rectTransform.anchoredPosition = new Vector2(pIcon + 10f + numW / 2f, 0f);
        priceTxt.alignment = TextAnchor.MiddleLeft;

        // Sold-out state per the reference artwork: the card swaps to the EMPTY cell
        // background (light blue), the price strip disappears, and a white
        // "Sold Out" label with a dark-brown outline appears in the middle.
        // The reference does NOT use a dark scrim over the card.
        soldOverlay.rectTransform.anchorMin = Vector2.zero; soldOverlay.rectTransform.anchorMax = Vector2.one;
        soldOverlay.rectTransform.offsetMin = Vector2.zero; soldOverlay.rectTransform.offsetMax = Vector2.zero;
        soldOverlay.color = new Color(0f, 0f, 0f, 0f);   // transparent scrim
        soldCG.alpha = 0f;
        soldOverlay.gameObject.SetActive(false);

        soldTxt.rectTransform.anchorMin = soldTxt.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        soldTxt.rectTransform.sizeDelta = new Vector2(cellW * 0.9f, 40f);
        soldTxt.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 0f);
    }

    public void SetData(ItemDef d)
    {
        def = d;
        nameTxt.text = d.displayName;
        priceTxt.text = d.price.ToString();
        iconImg.sprite = d.icon;
        iconImg.enabled = d.icon != null;
        if (manager != null && manager.currencies != null &&
            d.currencyIndex >= 0 && d.currencyIndex < manager.currencies.Count)
            priceIcon.sprite = manager.currencies[d.currencyIndex].icon;
    }

    public void ResetItem(ItemDef d)
    {
        soldOut = false;
        clickEnabled = true;
        bg.sprite = normalBg;
        iconImg.color = iconBase;
        soldOverlay.gameObject.SetActive(false);
        soldCG.alpha = 0f;
        if (pricePill != null) pricePill.gameObject.SetActive(true);
        cg.alpha = 1f;
        rt.localScale = Vector3.one;
        if (enterTween != null) StopCoroutine(enterTween);
        if (scaleTween != null) StopCoroutine(scaleTween);
        SetData(d);
    }

    public void PlayEnter(float delay, float duration)
    {
        cg.alpha = 0f;
        rt.localScale = Vector3.one * 0.8f;
        if (enterTween != null) StopCoroutine(enterTween);
        enterTween = this.Tween(duration, e =>
        {
            cg.alpha = e;
            rt.localScale = Vector3.one * Mathf.Lerp(0.8f, 1f, e);
        }, Ease.OutBack, delay);
    }

    public void PlaySoldOut()
    {
        if (soldOut || !clickEnabled) return;
        soldOut = true;
        clickEnabled = false;
        bg.sprite = emptyBg;

        // pay with the matching currency: reduce its amount by this item's price
        if (manager != null) manager.Spend(def.currencyIndex, def.price);

        // launch the item icon on a parabolic arc off-screen (purchase feedback)
        FlyIconOut();

        // the price strip is gone on a sold-out card (per the reference artwork)
        if (pricePill != null) pricePill.gameObject.SetActive(false);

        // white "Sold Out" label with a dark-brown outline, popping in with an
        // elastic scale. No dark scrim: the reference keeps the card readable.
        if (scaleTween != null) StopCoroutine(scaleTween);
        soldOverlay.gameObject.SetActive(true);
        soldTxt.color = Color.white;
        var ol = soldTxt.GetComponent<Outline>();
        if (ol != null) ol.effectColor = new Color(91f / 255f, 54f / 255f, 33f / 255f);
        this.Tween(0.2f, e => soldCG.alpha = 1f * e, Ease.Linear, 0f, () =>
        {
            soldTxt.transform.localScale = Vector3.one * 1.6f;
            this.Tween(0.5f, e => soldTxt.transform.localScale = Vector3.one * Mathf.Lerp(1.6f, 1f, e), Ease.OutElastic);
        });
    }

    // Clones the item icon, parents it to the Canvas root (so it is not clipped
    // by the panel), and animates it along a parabola: launched up, then falls
    // under gravity until it leaves the bottom of the screen.
    void FlyIconOut()
    {
        if (def.icon == null) return;
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        var fly = new GameObject("FlyIcon", typeof(RectTransform), typeof(Image));
        fly.transform.SetParent(canvas.transform, false);
        fly.transform.SetAsLastSibling();                 // render on top of everything
        var fi = fly.GetComponent<Image>();
        fi.sprite = def.icon;
        fi.type = Image.Type.Simple;
        fi.preserveAspect = true;
        var frt = fly.GetComponent<RectTransform>();
        frt.sizeDelta = iconImg.rectTransform.sizeDelta;
        frt.position = iconImg.rectTransform.position;    // screen-space overlay coords
        iconImg.enabled = false;                          // hide the original; the clone flies out
        StartCoroutine(FlyRoutine(frt));
    }

    IEnumerator FlyRoutine(RectTransform rt)
    {
        float s = Screen.height / 1280f;                  // resolution-independent scaling
        Vector2 pos = rt.position;
        float vy = flyUpSpeed * s;                        // upward launch (px/s)
        float g = flyGravity * s;                         // gravity (px/s^2)
        float vx = Random.Range(-flyDrift, flyDrift) * s; // slight random horizontal drift
        float spin = Random.Range(-flySpin, flySpin);
        float bottom = -300f * s;                         // off the bottom edge
        while (pos.y > bottom)
        {
            float dt = Time.deltaTime;
            vy -= g * dt;                                 // gravity pulls it back down
            pos.x += vx * dt;
            pos.y += vy * dt;
            rt.position = pos;
            rt.rotation = Quaternion.Euler(0f, 0f, rt.rotation.eulerAngles.z + spin * dt);
            yield return null;
        }
        Destroy(rt.gameObject);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!clickEnabled || soldOut) return;
        PlaySoldOut();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (soldOut || !clickEnabled) return;
        float s0 = rt.localScale.x;
        if (scaleTween != null) StopCoroutine(scaleTween);
        scaleTween = this.Tween(0.12f, e => rt.localScale = Vector3.one * Mathf.Lerp(s0, 1.06f, e), Ease.OutQuad);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        float s0 = rt.localScale.x;
        if (scaleTween != null) StopCoroutine(scaleTween);
        scaleTween = this.Tween(0.12f, e => rt.localScale = Vector3.one * Mathf.Lerp(s0, 1f, e), Ease.OutQuad);
    }
}
