using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A popup with one instance, loaded from Resources/{class name} on the first Show: UIPopupX.Show() / PopupX.Hide().
/// Inherit (class UIPopupX : UIPopupBase&lt;UIPopupX&gt;) and override the UIView hooks; closeButton closes it.
/// </summary>
public class UIPopupBase<T> : UIPopup where T : UIPopupBase<T>
{
    [SerializeField] protected Button closeButton;
    [SerializeField] protected bool destroyOnHide = true;
    private static T instance;

    /// <summary>The open (or hidden but kept) instance, or null.</summary>
    public static T Instance => instance;

    protected override bool DestroyOnClose => destroyOnHide;

    protected override void OnValidate()
    {
        base.OnValidate();
        if (closeButton == null)
            closeButton = GetComponentInChildren<Button>();
        name = typeof(T).Name;
    }

    protected override void Awake()
    {
        base.Awake();
        if (closeButton != null)
            closeButton.onClick.AddListener(() => Close());
    }

    /// <summary>Shows the popup (Resources/{type name} prefab, made on the first call) and returns it; null when the
    /// prefab is missing.</summary>
    public static T Show(bool destroyOnHide = true)
    {
        if (instance == null)
        {
            var prefab = Resources.Load<T>(typeof(T).Name);
            if (prefab == null)
            {
                Debug.LogError($"Failed to load {typeof(T).Name} from Resources.");
                return null;
            }
            instance = Instantiate(prefab, UIAnimManager.RootTransform);
            instance.destroyOnHide = destroyOnHide;
            instance.OpenAtStart();
            return instance;
        }

        instance.destroyOnHide = destroyOnHide;
        instance.Open();
        return instance;
    }

    public static void Hide()
    {
        if (instance != null)
            instance.Close();
    }

    protected virtual void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }
}
