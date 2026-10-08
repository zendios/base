/// <summary>
/// A popup over the screens. Every active popup is on UIManager's popup stack (top = last opened), so
/// UIManager.Back() / ClosePopup() close the top one. The back key closes it by default (override OnBack to change).
/// UIPopupBase&lt;T&gt; adds the Resources-loaded singleton (UIPopupX.Show()).
/// </summary>
public abstract class UIPopup : UIView
{
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
}
