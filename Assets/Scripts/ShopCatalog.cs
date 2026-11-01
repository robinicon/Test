using System.Collections.Generic;
using UnityEngine;

// One shop item entry. `icon` is resolved at runtime from the sprite under
// Assets/Resources/ShopArt/<spriteName>. Designers can edit the catalog below
// or replace it from the inspector on ShopManager.
[System.Serializable]
public struct ItemDef
{
    public string spriteName;   // file name without extension, under ShopArt/
    public string displayName;
    public int price;           // cost in the currency referenced by currencyIndex
    public int currencyIndex;   // which of the top-bar currencies this item is paid with
    public Sprite icon;         // filled at runtime
}

// One currency shown in the top bar: background chip + icon + amount.
[System.Serializable]
public struct CurrencyDef
{
    public string iconName;     // sprite under ShopArt/
    public string name;
    public int startAmount;
    public Sprite icon;         // filled at runtime
}

public static class ShopCatalog
{
    // The 10 cartoon items shipped in Images.zip, mapped to friendly names,
    // a price, and which currency (0..3) they are purchased with.
    public static List<ItemDef> DefaultCatalog()
    {
        var list = new List<ItemDef>();
        void Add(string sprite, string name, int price, int currencyIndex)
            => list.Add(new ItemDef { spriteName = sprite, displayName = name, price = price, currencyIndex = currencyIndex });

        Add("cartoon_icon_drum_rank4_128x128",        "Drum",          15, 0);
        Add("cartoon_icon_fishingnet_rank1_128x128",  "Fishing Net",    8, 1);
        Add("cartoon_icon_ironpan_rank4_128x128",     "Iron Pan",      20, 2);
        Add("cartoon_icon_noodle_rank4_128x128",      "Noodles",       12, 3);
        Add("cartoon_icon_pearl_rank2_128x128",       "Pearl",         10, 0);
        Add("cartoon_icon_piggybank_rank3_128x128",   "Piggy Bank",    18, 1);
        Add("cartoon_icon_pumpkinchest_128x128",      "Pumpkin Chest", 35, 2);
        Add("cartoon_icon_purplebathware_128x128",    "Bath Set",      25, 3);
        Add("cartoon_icon_tofusoup_rank2",            "Tofu Soup",      5, 0);
        Add("cartoon_icon_tofusoup_rank6",            "Tofu Soup+",    14, 1);
        return list;
    }
}

public static class CurrencyCatalog
{
    // Four distinct currencies for the top bar. Icons reuse the cartoon pack.
    public static List<CurrencyDef> DefaultCurrencies()
    {
        var list = new List<CurrencyDef>();
        void Add(string icon, string name, int amt)
            => list.Add(new CurrencyDef { iconName = icon, name = name, startAmount = amt });

        Add("cartoon_icon_pearl_rank2_128x128",      "Pearl", 200);
        Add("cartoon_icon_piggybank_rank3_128x128",  "Coin",  200);
        Add("cartoon_icon_pumpkinchest_128x128",     "Chest", 120);
        Add("cartoon_icon_drum_rank4_128x128",       "Token", 150);
        return list;
    }
}
