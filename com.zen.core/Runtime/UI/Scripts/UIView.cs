using System;
using UnityEngine;

/// <summary>
/// Base of every UI view (UIScreen, UIPopup): shown and hidden with its UIAnim. Override the hooks to add behaviour:
/// OnShow (before the show animation), OnShown (after it), OnHide (before the hide animation), OnHidden (after it),
/// OnBack (the device / Escape back key reached this view; return true when handled).
/// </summary>
[RequireComponent(typeof(UIAnim))]
public abstract class UIView : MonoBehaviour
{
    [SerializeField] protected UIAnim anim;

    public UIAnim Anim => anim;

    /// <summary>Showing or shown.</summary>
    public bool IsVisible => anim != null && gameObject.activeInHierarchy
                             && (anim.status == UIAnimStatus.IsShow || anim.status == UIAnimStatus.IsShowing);

    /// <summary>Destroy the view when its hide animation ends.</summary>
    protected virtual bool DestroyOnClose => false;

    protected virtual void OnValidate()
    {
        if (anim == null)
            TryGetComponent(out anim);
    }

    protected virtual void Awake()
    {
        if (anim == null)
            TryGetComponent(out anim);
    }

    /// <summary>Shows the view (UIAnim), then onShown.</summary>
    public virtual void Open(Action onShown = null)
    {
        OnShow();
        anim.Show(null, () =>
        {
            OnShown();
            onShown?.Invoke();
        });
    }

    /// <summary>For a view just instantiated: its UIAnim shows it on Start (playAnimAtStart); the hooks still run.</summary>
    public void OpenAtStart()
    {
        anim.playAnimAtStart = true;
        OnShow();
        StartCoroutine(WaitShown());
    }

    private System.Collections.IEnumerator WaitShown()
    {
        // stops by itself when the view is hidden (deactivated) before the animation ends
        yield return new WaitUntil(() => anim == null || anim.status == UIAnimStatus.IsShow);
        if (anim != null)
            OnShown();
    }

    /// <summary>Hides the view (UIAnim), then onHidden; destroys it when DestroyOnClose.</summary>
    public virtual void Close(Action onHidden = null)
    {
        OnHide();
        anim.Hide(() =>
        {
            OnHidden();
            onHidden?.Invoke();
        }, false, DestroyOnClose);
    }

    /// <summary>The back key reached this view. True = handled (UIManager stops there).</summary>
    public virtual bool OnBack() => false;

    protected virtual void OnShow() { }
    protected virtual void OnShown() { }
    protected virtual void OnHide() { }
    protected virtual void OnHidden() { }
}
