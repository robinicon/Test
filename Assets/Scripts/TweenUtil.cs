using System;
using System.Collections;
using UnityEngine;

// Tiny coroutine-based tween helper. Drives an eased 0..1 progress to a callback,
// so state-machine code stays readable. No external tween library required.
public static class TweenUtil
{
    public static Coroutine Tween(this MonoBehaviour mb, float duration, Action<float> onUpdate,
        Ease ease = Ease.Linear, float delay = 0f, Action onComplete = null)
    {
        return mb.StartCoroutine(Run(mb, duration, onUpdate, ease, delay, onComplete));
    }

    static IEnumerator Run(MonoBehaviour mb, float duration, Action<float> onUpdate,
        Ease ease, float delay, Action onComplete)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        if (duration <= 0f) { onUpdate(1f); onComplete?.Invoke(); yield break; }

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            onUpdate(Easing.Apply(ease, t));
            yield return null;
        }
        onUpdate(1f);
        onComplete?.Invoke();
    }
}
