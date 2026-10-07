using Base;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(UIAnim))]
public class PopupBase<T> : MonoBehaviour where T : PopupBase<T>
{
    [SerializeField] protected UIAnim anim;
    [SerializeField] protected Button closeButton;
    [SerializeField] protected bool destroyOnHide = true;
    private static T instance;

    private void OnValidate()
    {
        if (anim == null)
            TryGetComponent(out anim);
        if (closeButton == null)
            closeButton = GetComponentInChildren<Button>();
        name = typeof(T).Name;
    }

    private void Awake()
    {
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(() => anim.Hide(null, true, destroyOnHide));
        }
    }

    public static void Show(bool destroyOnHide = true)
    {
        if (instance == null)
        {
            instance = Instantiate(Resources.Load<T>($"{typeof(T).Name}"), UIAnimManager.RootTransform);
            instance.anim.playAnimAtStart = true;
            return;
        }

        if (instance == null)
        {
            Debug.LogError($"Failed to load {typeof(T).Name} from Resources.");
            return;
        }

        instance.destroyOnHide = destroyOnHide;
        instance.anim.Show();
    }

    public static void Hide()
    {
        instance?.anim.Hide(() =>
        {
            if (instance != null && instance.destroyOnHide)
            {
                Destroy(instance.gameObject, 0.5f);
                instance = null;
            }
        });
    }

    private void OnDestroy()
    {
        instance = null;
    }
}
