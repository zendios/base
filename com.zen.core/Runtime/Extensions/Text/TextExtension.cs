using DG.Tweening;
using System;
using TMPro;
using UnityEngine.UI;

public static class TextExtension
{
    public static void DONumber(this TextMeshProUGUI target, float startValue, float endValue, Action actionOnDone = null)
    {
        target.DONumber(startValue, endValue, 0.5f, "#0", actionOnDone);
    }


    public static void DONumber(this TextMeshProUGUI target, float startValue, float endValue, float duration, string format = "#0", Action actionOnDone = null)
    {
        if (target != null)
        {
            DOTween.Complete(target.GetInstanceID());
            target.DOComplete();
            target.transform.DOComplete();
            DOVirtual.Float(startValue, endValue, duration, (s) =>
            {
                if (target.IsDestroyed())
                    return;
                target.text = s.ToString(format);
            }).SetId(target.GetInstanceID()).OnComplete(() => target.transform.DOScale(1.1f, 0.125f).SetEase(Ease.InCubic)
                .OnComplete(() => target.transform.DOScale(1.0f, 0.25f).SetEase(Ease.OutCubic)
                .OnComplete(() => actionOnDone?.Invoke())));
        }
    }

    public static void DONumber(this Text target, float startValue, float endValue, Action actionOnDone = null)
    {
        target.DONumber(startValue, endValue, 0.5f, "#0", actionOnDone);
    }


    public static void DONumber(this Text target, float startValue, float endValue, float duration, string format = "#0", Action actionOnDone = null)
    {
        if (target != null)
        {
            DOTween.Complete(target.GetInstanceID());
            target.DOComplete();
            target.transform.DOComplete();
            DOVirtual.Float(startValue, endValue, duration, (s) =>
            {
                if (target.IsDestroyed())
                    return;
                target.text = s.ToString(format);
            }).SetId(target.GetInstanceID()).OnComplete(() => target.transform.DOScale(1.1f, 0.125f).SetEase(Ease.InCubic)
                .OnComplete(() => target.transform.DOScale(1.0f, 0.25f).SetEase(Ease.OutCubic)
                .OnComplete(() => actionOnDone?.Invoke())));
        }
    }
}