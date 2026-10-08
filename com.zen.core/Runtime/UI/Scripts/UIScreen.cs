/// <summary>
/// A full screen (Home, Shop, Gameplay HUD...). One screen is shown at a time: UIManager.ShowScreen&lt;T&gt;() hides the
/// current one and keeps a history for UIManager.Back(). Put the screen in the scene (it registers itself) or as a
/// prefab in Resources named like the class (loaded on first use). Inherit and override the UIView hooks.
/// </summary>
public abstract class UIScreen : UIView
{
    protected override void Awake()
    {
        base.Awake();
        UIManager.Instance.RegisterScreen(this);
    }

    protected virtual void OnDestroy()
    {
        if (UIManager.HasInstance)
            UIManager.Instance.UnregisterScreen(this);
    }
}
