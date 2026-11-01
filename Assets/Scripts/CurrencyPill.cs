using UnityEngine;
using UnityEngine.UI;

// One currency chip in the top bar: dark navy pill + icon (left) + amount (right).
// Instantiated dynamically by ShopManager from the CurrencyPill prefab.
[RequireComponent(typeof(RectTransform), typeof(Image))]
public class CurrencyPill : MonoBehaviour
{
    [SerializeField] private Image bg, iconImg;
    [SerializeField] private Text amountTxt;

    public void Init(Sprite icon, int amount)
    {
        // The pill background is a 9-slice charcoal capsule; render it sliced so
        // the rounded ends stay crisp, and keep the colour white - the charcoal
        // tone lives in the sprite (sampled #333639 from the reference), and a
        // tint on top of it would only flatten it out.
        bg.type = Image.Type.Sliced;
        bg.color = Color.white;
        iconImg.sprite = icon;
        amountTxt.text = amount.ToString();
    }

    // Called by ShopManager.Spend when an item is purchased: update the number
    // with a small pop so the deduction reads clearly.
    public void SetAmount(int amount)
    {
        amountTxt.text = amount.ToString();
        this.Tween(0.2f, e =>
        {
            float s = 1f + 0.3f * Mathf.Sin(e * Mathf.PI);
            amountTxt.transform.localScale = Vector3.one * s;
        }, Ease.OutQuad, 0f, () => amountTxt.transform.localScale = Vector3.one);
    }
}
