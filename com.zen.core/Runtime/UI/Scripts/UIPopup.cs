using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// A popup over the screens. Every active popup is on UIManager's popup stack (top = last opened), so
/// UIManager.Back() / ClosePopup() close the top one. The back key closes it by default (override OnBack to change),
/// and so does a tap on its dim background (PopupBackground of Popup_Base) while closeOnTapOutside is on.
/// UIPopupBase&lt;T&gt; adds the Resources-loaded singleton (UIPopupX.Show()).
/// </summary>
public abstract class UIPopup : UIView
{
    [Tooltip("A tap on the dim background (PopupBackground, outside the content) does what the back key does.")]
    [SerializeField] protected bool closeOnTapOutside = true;

    protected override void Awake()
    {
        base.Awake();
        var background = transform.Find("PopupBackground");
        if (closeOnTapOutside && background != null)
            background.gameObject.AddComponent<TapOutside>().popup = this;
    }

    protected virtual void OnEnable()
    {
        UIManager.Instance.RegisterPopup(this);
    }

    protected virtual void OnDisable()
    {
        if (UIManager.HasInstance)
            UIManager.Instance.UnregisterPopup(this);
    }

    public override bool OnBack()
    {
        Close();
        return true;
    }

    /// <summary>
    /// On the popup's background: a tap outside the content goes to OnBack, once the popup is shown. The panes under
    /// ContentAnim may not catch raycasts (Raycast Target off), so a tap inside one of them is not "outside".
    /// </summary>
    private sealed class TapOutside : MonoBehaviour, IPointerClickHandler
    {
        public UIPopup popup;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!popup.IsVisible)
                return;
            var content = popup.transform.Find("ContentAnim");
            if (content != null)
            {
                foreach (RectTransform pane in content)
                    if (pane.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(pane,
                            eventData.position, eventData.pressEventCamera))
                        return;
            }
            popup.OnBack();
        }
    }
}
