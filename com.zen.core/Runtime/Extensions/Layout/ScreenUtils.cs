using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fits a ScaleWithScreenSize canvas to any phone, tablet or foldable, portrait or landscape:
///  - Expand: the whole design area (reference resolution) is always visible; the extra space of a longer or wider
///    screen goes to the free axis, where edge-anchored UI spreads out (no more cut top / bottom on 20:9 phones).
///  - One reference resolution per orientation, switched when the device rotates (OnOrientationChanged).
///  - Tablet cap: on big screens a canvas unit may be at most tabletMaxScale times its size on a reference phone,
///    so buttons do not grow to 2-3 cm (needs a real DPI: device or Device Simulator).
/// Runs in play mode only.
/// </summary>
[RequireComponent(typeof(CanvasScaler))]
public class ScreenUtils : MonoBehaviour
{
    /// <summary>Short side of the reference phone for the tablet cap (6.1" class phone, ~65 mm).</summary>
    public const float ReferencePhoneShortSideMm = 65f;

    [SerializeField] private CanvasScaler canvasScaler;

    [Header("Design size per orientation")]
    [Tooltip("Reference resolution used while the screen is wider than tall.")]
    public Vector2 landscapeReference = new Vector2(1920, 1080);
    [Tooltip("Reference resolution used while the screen is taller than wide.")]
    public Vector2 portraitReference = new Vector2(1080, 1920);

    [Header("Tablets")]
    [Tooltip("Largest physical size of a canvas unit, relative to a ~65 mm wide phone. 1.3 = UI at most 30% bigger than on that phone. 0 = no cap.")]
    [Min(0)] public float tabletMaxScale = 1.3f;

    /// <summary>Raised with true for portrait when the screen orientation changes (and once at start).</summary>
    public static event Action<bool> OnOrientationChanged;

    /// <summary>Orientation of the last applied screen.</summary>
    public static bool IsPortrait { get; private set; }

    private Vector2Int lastScreen;

    private void Reset() => canvasScaler = GetComponent<CanvasScaler>();

    private void Awake()
    {
        if (canvasScaler == null)
            canvasScaler = GetComponent<CanvasScaler>();
        Apply();
    }

    private void Update()
    {
        if (Screen.width != lastScreen.x || Screen.height != lastScreen.y)
            Apply();
    }

    private void Apply()
    {
        lastScreen = new Vector2Int(Screen.width, Screen.height);
        if (canvasScaler == null || Screen.width <= 0 || Screen.height <= 0)
            return;

        bool portrait = Screen.height >= Screen.width;
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        canvasScaler.referenceResolution = ReferenceFor(portrait, Screen.width, Screen.height, Base.ScreenMetrics.Dpi, tabletMaxScale,
            UnityEngine.Device.Application.isMobilePlatform);

        if (portrait != IsPortrait || !orientationSent)
        {
            IsPortrait = portrait;
            orientationSent = true;
            OnOrientationChanged?.Invoke(portrait);
        }

        var canvas = GetComponent<Canvas>();
        if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera == null)
            canvas.worldCamera = Camera.main;
    }

    private static bool orientationSent;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        orientationSent = false;
        OnOrientationChanged = null;
    }

    /// <summary>
    /// Reference resolution for a screen. With Expand the canvas shows at least
    /// this size; on a tablet it is enlarged so a unit stays below tabletMaxScale × its size on the reference phone.
    /// </summary>
    public Vector2 ReferenceFor(bool portrait, int width, int height, float dpi, float maxScale, bool capTablets)
    {
        Vector2 reference = portrait ? portraitReference : landscapeReference;
        if (!capTablets || maxScale <= 0 || dpi <= 0)
            return reference;

        float scale = Mathf.Min(width / reference.x, height / reference.y);          // Expand: screen px per unit
        float mmPerUnit = scale / (dpi / 25.4f);
        float phoneMmPerUnit = ReferencePhoneShortSideMm / Mathf.Min(reference.x, reference.y);
        float ratio = mmPerUnit / (phoneMmPerUnit * maxScale);
        return ratio > 1f ? reference * ratio : reference;
    }
}
