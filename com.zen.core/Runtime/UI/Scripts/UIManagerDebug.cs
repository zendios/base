using TMPro;
using UnityEngine;

/// <summary>
/// Test panel of UIManager.prefab, made from the Base prefabs (Canvas_Base, Button_Text, Text_Base) so it also shows
/// how to use them: each button's OnClick (set in the Inspector) calls one public method below.
/// Add a button: duplicate one under Panel, change its label and its OnClick.
/// Shown in the editor and Development builds; hidden in release builds unless showInRelease. Needs an EventSystem.
/// </summary>
public class UIManagerDebug : MonoBehaviour
{
    [SerializeField] private bool showInRelease = false;
    [Tooltip("The debug canvas, hidden in release builds.")]
    [SerializeField] private GameObject canvas;
    [Tooltip("Current screen and popup count.")]
    [SerializeField] private TMP_Text statusText;
    [Tooltip("Hidden and shown by the UI button.")]
    [SerializeField] private GameObject[] foldable;

    private int count;

    private bool Visible => Application.isEditor || Debug.isDebugBuild || showInRelease;

    protected virtual void Awake()
    {
        if (!Visible && canvas != null)
            canvas.SetActive(false);
    }

    protected virtual void OnEnable()
    {
        UIManager.OnScreenChanged += OnScreenChanged;
        UIManager.OnPopupChanged += OnPopupChanged;
    }

    protected virtual void OnDisable()
    {
        UIManager.OnScreenChanged -= OnScreenChanged;
        UIManager.OnPopupChanged -= OnPopupChanged;
    }

    protected virtual void Start() => RefreshStatus();

    private void OnScreenChanged(UIScreen screen) => RefreshStatus();
    private void OnPopupChanged(UIPopup popup, bool opened) => RefreshStatus();

    protected virtual void RefreshStatus()
    {
        if (statusText == null)
            return;
        var manager = UIManager.Instance;
        statusText.text = $"Screen: {(manager.CurrentScreen != null ? manager.CurrentScreen.name : "-")}   Popups: {manager.Popups.Count}";
    }

    // ---- button OnClick targets ----

    public virtual void TogglePanel()
    {
        if (foldable == null || foldable.Length == 0)
            return;
        bool show = !foldable[0].activeSelf;
        foreach (var item in foldable)
            if (item != null)
                item.SetActive(show);
    }

    public virtual void ShowToast() => UIManager.Toast($"Toast #{++count}");

    public virtual void ShowMessage() => UIManager.Message("Notice", "A message with one button.");

    public virtual void ShowConfirm() =>
        UIManager.Message("Confirm", "Do it?",
            onConfirm: () => UIManager.Toast("Confirmed"), confirmLabel: "Yes",
            onCancel: () => UIManager.Toast("Cancelled"), cancelLabel: "No");

    public virtual void ShowPopupIAP() => UIManager.ShowPopup<UIPopupIAP>();

    public virtual void ClosePopup() => UIManager.ClosePopup();

    public virtual void CloseAllPopups() => UIManager.CloseAllPopups();

    public virtual void Back() => UIManager.Toast(UIManager.Back() ? "Back: handled" : "Back: at root");
}
