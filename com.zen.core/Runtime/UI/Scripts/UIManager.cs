using System;
using System.Collections.Generic;
using System.Linq;
using Base;
using UnityEngine;

/// <summary>
/// One entry point for the game's UI: screens (one at a time, with a history), popups (a stack), toasts and the
/// message popup, plus the back key (Android back / Escape).
///   UIManager.ShowScreen&lt;HomeScreen&gt;();  UIManager.Back();
///   UIManager.ShowPopup&lt;UIPopupIAP&gt;();     UIManager.ClosePopup();  UIManager.CloseAllPopups();
///   UIManager.Message("Error", "No connection", onConfirm: Retry);   UIManager.Toast("Saved");
/// Put a UIManager (or a subclass) in the scene to change its settings or behaviour: every step is a virtual method.
/// Without one, the first call creates a default UIManager.
/// </summary>
public class UIManager : MonoBehaviour
{
    private static UIManager instance;

    [Tooltip("Android back / Escape calls Back().")]
    [SerializeField] protected bool handleBackKey = true;

    protected readonly List<UIScreen> screens = new List<UIScreen>();   // registered (scene or loaded)
    protected readonly List<UIScreen> history = new List<UIScreen>();   // last = current screen
    protected readonly List<UIPopup> popups = new List<UIPopup>();      // last = top popup

    /// <summary>The current screen changed (null = none).</summary>
    public static event Action<UIScreen> OnScreenChanged;
    /// <summary>A popup opened (true) or closed (false).</summary>
    public static event Action<UIPopup, bool> OnPopupChanged;
    /// <summary>Back with nothing to close and no previous screen (e.g. ask "Quit the game?").</summary>
    public static event Action OnBackAtRoot;

    public static bool HasInstance => instance != null;

    public static UIManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<UIManager>();
                if (instance == null)
                    instance = new GameObject(nameof(UIManager)).AddComponent<UIManager>();
            }
            return instance;
        }
    }

    // ---- static API ----

    public static T ShowScreen<T>(bool addToHistory = true) where T : UIScreen => Instance.OpenScreen<T>(addToHistory);
    public static bool Back() => Instance.HandleBack();
    public static T ShowPopup<T>(bool destroyOnHide = true) where T : UIPopupBase<T> => UIPopupBase<T>.Show(destroyOnHide);
    public static void ClosePopup() => Instance.CloseTopPopup();
    public static void CloseAllPopups() => Instance.CloseEveryPopup();
    public static void Toast(string message) => Instance.ShowToast(message);

    public static UIPopupMessage Message(string title, string message, Action onConfirm = null, string confirmLabel = "OK",
        Action onCancel = null, string cancelLabel = null) =>
        Instance.ShowMessage(title, message, onConfirm, confirmLabel, onCancel, cancelLabel);

    public UIScreen CurrentScreen => history.LastOrDefault(s => s != null);
    public UIPopup TopPopup => popups.LastOrDefault(p => p != null);
    public bool HasPopup => TopPopup != null;
    public IReadOnlyList<UIPopup> Popups => popups;

    // ---- lifecycle ----

    protected virtual void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }
        instance = this;
    }

    protected virtual void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    protected virtual void Update()
    {
        if (handleBackKey && BackPressed())
            HandleBack();
    }

    /// <summary>Android back (Input System maps it to Escape) or Escape.</summary>
    protected virtual bool BackPressed()
    {
#if ENABLE_INPUT_SYSTEM
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    // ---- screens ----

    public virtual void RegisterScreen(UIScreen screen)
    {
        if (screen == null || screens.Contains(screen))
            return;
        screens.Add(screen);
        // the scene's start screen (its UIAnim shows it at start) becomes the current one
        if (history.Count == 0 && screen.Anim != null && screen.Anim.playAnimAtStart)
        {
            history.Add(screen);
            OnScreenChanged?.Invoke(screen);
        }
    }

    public virtual void UnregisterScreen(UIScreen screen)
    {
        screens.Remove(screen);
        bool wasCurrent = CurrentScreen == screen;
        history.RemoveAll(s => s == screen || s == null);
        if (wasCurrent)
            OnScreenChanged?.Invoke(CurrentScreen);
    }

    /// <summary>Hides the current screen and shows T (registered, else loaded); addToHistory = false replaces the history.</summary>
    public virtual T OpenScreen<T>(bool addToHistory = true) where T : UIScreen
    {
        var screen = FindScreen<T>() ?? LoadScreen<T>();
        if (screen == null)
        {
            Debug.LogError($"[UIManager] no screen {typeof(T).Name} in the scene or in Resources.");
            return null;
        }
        SwitchTo(screen, addToHistory);
        return screen;
    }

    protected virtual T FindScreen<T>() where T : UIScreen => screens.OfType<T>().FirstOrDefault(s => s != null);

    /// <summary>Resources/{class name} prefab under the UI root. Override for Addressables etc.</summary>
    protected virtual T LoadScreen<T>() where T : UIScreen
    {
        var prefab = Resources.Load<T>(typeof(T).Name);
        if (prefab == null)
            return null;
        var screen = Instantiate(prefab, UIAnimManager.RootTransform);
        screen.name = typeof(T).Name;
        RegisterScreen(screen);
        return screen;
    }

    protected virtual void SwitchTo(UIScreen screen, bool addToHistory)
    {
        var current = CurrentScreen;
        if (current == screen)
            return;
        if (current != null)
            current.Close();
        if (!addToHistory)
            history.Clear();
        history.Remove(screen);
        history.Add(screen);
        screen.Open();
        OnScreenChanged?.Invoke(screen);
    }

    /// <summary>Goes back to the previous screen; false when there is none.</summary>
    public virtual bool BackScreen()
    {
        history.RemoveAll(s => s == null);
        if (history.Count < 2)
            return false;
        var current = history[history.Count - 1];
        history.RemoveAt(history.Count - 1);
        current.Close();
        var previous = history[history.Count - 1];
        previous.Open();
        OnScreenChanged?.Invoke(previous);
        return true;
    }

    // ---- popups ----

    public virtual void RegisterPopup(UIPopup popup)
    {
        if (popup == null) return;
        popups.Remove(popup);
        popups.Add(popup);
        OnPopupChanged?.Invoke(popup, true);
    }

    public virtual void UnregisterPopup(UIPopup popup)
    {
        if (popups.Remove(popup))
            OnPopupChanged?.Invoke(popup, false);
        popups.RemoveAll(p => p == null);
    }

    public virtual void CloseTopPopup()
    {
        var top = TopPopup;
        if (top != null)
            top.Close();
    }

    public virtual void CloseEveryPopup()
    {
        foreach (var popup in popups.Where(p => p != null).ToList())
            popup.Close();
    }

    // ---- back ----

    /// <summary>Top popup first, then the current screen's OnBack, then the previous screen; else OnBackAtRoot.</summary>
    public virtual bool HandleBack()
    {
        var top = TopPopup;
        if (top != null && top.IsVisible)
            return top.OnBack();
        var screen = CurrentScreen;
        if (screen != null && screen.OnBack())
            return true;
        if (BackScreen())
            return true;
        OnBackAtRoot?.Invoke();
        return false;
    }

    // ---- toast / message ----

    /// <summary>Short notice (ZenToast: AdToast when it is there, else the log). Override for another toast.</summary>
    public virtual void ShowToast(string message) => ZenToast.ShowNotice(message);

    /// <summary>The UIPopupMessage popup. Override for another message popup.</summary>
    public virtual UIPopupMessage ShowMessage(string title, string message, Action onConfirm, string confirmLabel, Action onCancel, string cancelLabel) =>
        UIPopupMessage.Show(title, message, onConfirm, confirmLabel, onCancel, cancelLabel);
}
