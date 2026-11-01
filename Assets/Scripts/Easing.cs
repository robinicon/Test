using UnityEngine;

// Easing functions + a small Ease enum so tweens can be driven declaratively.
public enum Ease
{
    Linear,
    OutQuad,
    OutCubic,
    InOutQuad,
    InOutSine,
    OutBack,
    OutElastic
}

public static class Easing
{
    public static float Linear(float t) => t;

    public static float OutQuad(float t) => 1f - (1f - t) * (1f - t);

    public static float OutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);

    public static float InOutQuad(float t) => t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;

    public static float InOutSine(float t) => -(Mathf.Cos(Mathf.PI * t) - 1f) / 2f;

    public static float OutBack(float t)
    {
        const float c1 = 1.70158f;
        float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    public static float OutElastic(float t)
    {
        if (t <= 0f) return 0f;
        if (t >= 1f) return 1f;
        const float c4 = (2f * Mathf.PI) / 3f;
        return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;
    }

    public static float Apply(Ease e, float t)
    {
        t = Mathf.Clamp01(t);
        switch (e)
        {
            case Ease.OutQuad: return OutQuad(t);
            case Ease.OutCubic: return OutCubic(t);
            case Ease.InOutQuad: return InOutQuad(t);
            case Ease.InOutSine: return InOutSine(t);
            case Ease.OutBack: return OutBack(t);
            case Ease.OutElastic: return OutElastic(t);
            default: return t;
        }
    }
}
