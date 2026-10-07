using Base;
using Base.Ads;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The full ad test popup, opened by the "Test All Ads" button of AdsDebugPanel (Debug Mode column).
/// Its own overlay canvas (same scaler as the Debug canvas) draws the frame: dark card, header (top-left: drag handle
/// to move the popup; top-right icons: minimize / expand / close), log, sections. Every button is a ButtonTestAd and every choice a DropdownAdFlow (base prefabs, normal size).
/// One screen, no tabs: status, flows, every placement (Load / Show / Destroy), tools with Force Ads.
/// </summary>
public class AdsTestAllPanel : MonoBehaviour
{
    // Above the Debug column canvas (32000). TMP opens dropdown lists at 30000, so LateUpdate lifts them above the popup.
    private const int SortingOrder = 32100;
    // Part of the screen kept free at the bottom: the banner strip plus the AdMob native ad validator popup of test ads.
    private const float BottomReserve = 0.22f;
    private static readonly Vector2 Cell = new Vector2(192, 40);   // base prefab 160 x 40, 120% wide so the labels fit
    private const float Space = 10;
    private const int Columns = 4;
    private const float LogHeight = 250;
    private static readonly Vector2 IconCell = new Vector2(56, 40);   // header icon buttons (ButtonTestAd, square-ish)

    private static readonly Color Bg = Hex("0E1319", 0.97f);
    private static readonly Color Card = Hex("19212A");
    private static readonly Color TextMain = Hex("E8EDF2");
    private static readonly Color TextSub = Hex("8A97A6");
    private static readonly Color LogColor = Hex("A7F3C4");

    private GameObject buttonPrefab, dropdownPrefab;
    private TMP_FontAsset font;
    private GameObject canvasGo, panel;
    private RectTransform content, grid;
    private LayoutElement scrollSize;
    private GameObject logGo, scrollGo, dragGo;
    private ContentSizeFitter panelFitter;
    private bool minimized, expanded;
    private Vector2 compactPosition = new Vector2(0, -Space);   // where the compact popup was dragged to
    private TMP_Text logText, title;
    private readonly List<string> logLines = new List<string>();
    private readonly List<Action> refreshers = new List<Action>();
    private int placementIndex;
    private float nextRefresh;
    private bool relayoutNextFrame;
    private static Sprite rounded;

    private static AdConfig Config => AdConfig.Instance;
    private static UserData User => DataManager.UserData;

    #region Lifecycle
    /// <summary>Called by AdsDebugPanel once, before the first TogglePanel.</summary>
    public void Init(GameObject button, GameObject dropdown)
    {
        buttonPrefab = button;
        dropdownPrefab = dropdown;
        font = buttonPrefab.GetComponentInChildren<TMP_Text>(true).font;   // admobFont SDF
        BuildPanel();
        panel.SetActive(false);
        AdZativeSDK.OnEvent += OnPluginEvent;
        AdBase.OnAnyStateChanged += OnStateChanged;
    }

    private void OnDestroy()
    {
        AdZativeSDK.OnEvent -= OnPluginEvent;
        AdBase.OnAnyStateChanged -= OnStateChanged;
        if (canvasGo != null) Destroy(canvasGo);
    }

    private void LateUpdate()
    {
        if (panel != null && panel.activeSelf) LiftDropdownLists();
        if (!relayoutNextFrame) return;
        relayoutNextFrame = false;
        Relayout();
    }

    /// <summary>An open dropdown list (and its click-outside blocker) must draw above the popup, not at TMP's 30000.</summary>
    private void LiftDropdownLists()
    {
        foreach (Transform child in canvasGo.transform)
            if (child.name == "Blocker" && child.TryGetComponent<Canvas>(out var blocker))
                blocker.sortingOrder = SortingOrder + 1;
        foreach (var dropdown in panel.GetComponentsInChildren<TMP_Dropdown>())
        {
            var list = dropdown.transform.Find("Dropdown List");
            if (list != null && list.TryGetComponent<Canvas>(out var canvas))
                canvas.sortingOrder = SortingOrder + 2;
        }
    }

    private void Update()
    {
        if (panel == null || !panel.activeSelf || Time.unscaledTime < nextRefresh)
            return;
        nextRefresh = Time.unscaledTime + 1f;
        Refresh();
    }

    public void TogglePanel()
    {
        panel.SetActive(!panel.activeSelf);
        if (panel.activeSelf)
        {
            BuildContent();
            ApplyWindowState();
        }
    }
    #endregion

    #region Frame
    private void BuildPanel()
    {
        // Root overlay canvas with the Debug canvas scaler: the base prefabs keep the size they have in the Debug column.
        var debugCanvas = GetComponentInParent<Canvas>().rootCanvas;
        canvasGo = new GameObject("AdsTestAllCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        DontDestroyOnLoad(canvasGo);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        var src = debugCanvas.GetComponent<CanvasScaler>();
        if (src != null)
        {
            scaler.uiScaleMode = src.uiScaleMode;
            scaler.referenceResolution = src.referenceResolution;
            scaler.screenMatchMode = src.screenMatchMode;
            scaler.matchWidthOrHeight = src.matchWidthOrHeight;
            scaler.scaleFactor = src.scaleFactor;
            scaler.referencePixelsPerUnit = src.referencePixelsPerUnit;
        }

        panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasGo.transform, false);
        AddImage(panel, Bg);
        panelFitter = panel.AddComponent<ContentSizeFitter>();

        var vl = panel.AddComponent<VerticalLayoutGroup>();
        vl.padding = new RectOffset(16, 16, 12, 16);
        vl.spacing = Space;
        vl.childControlWidth = vl.childControlHeight = true;
        vl.childForceExpandWidth = true;
        vl.childForceExpandHeight = false;

        // Header: drag handle at the top left, title, [minimize][expand][close] at the top right.
        var header = new GameObject("Header", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        header.transform.SetParent(panel.transform, false);
        Fixed(header, Cell.y);
        var hl = header.GetComponent<HorizontalLayoutGroup>();
        hl.spacing = Space;
        hl.childControlWidth = hl.childControlHeight = true;
        hl.childForceExpandWidth = false;
        hl.childForceExpandHeight = true;
        dragGo = IconButton(header.transform, "Drag", DrawGrip, () => { });
        dragGo.AddComponent<DragHandle>().owner = this;
        title = NewText(header.transform, "", 28, TextMain, TextAlignmentOptions.Midline);
        title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        IconButton(header.transform, "Minimize", DrawMinimize, ToggleMinimize);
        IconButton(header.transform, "Expand", DrawExpand, ToggleExpand);
        IconButton(header.transform, "Close", DrawClose, TogglePanel);

        // Log (reading area at the top, away from the thumb).
        var log = logGo = new GameObject("Log", typeof(RectTransform), typeof(LayoutElement));
        log.transform.SetParent(panel.transform, false);
        AddImage(log, Card);
        Fixed(log, LogHeight);
        logText = NewText(log.transform, "", 24, LogColor, TextAlignmentOptions.TopLeft);
        var lrt = (RectTransform)logText.transform;
        lrt.offsetMin = new Vector2(12, 8);
        lrt.offsetMax = new Vector2(-12, -8);

        // Scrollable content: sections of text cards and grids of base prefabs.
        var scroll = scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(RectMask2D), typeof(ScrollRect), typeof(LayoutElement));
        scroll.transform.SetParent(panel.transform, false);
        scrollSize = scroll.GetComponent<LayoutElement>();
        scrollSize.flexibleHeight = 0;
        var sr = scroll.GetComponent<ScrollRect>();
        sr.horizontal = false;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.scrollSensitivity = 30;
        content = NewRect("Content", scroll.transform);
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0.5f, 1);
        content.sizeDelta = Vector2.zero;
        var cl = content.gameObject.AddComponent<VerticalLayoutGroup>();
        cl.spacing = Space;
        cl.childControlWidth = cl.childControlHeight = true;
        cl.childForceExpandWidth = true;
        cl.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        sr.content = content;
        ApplyWindowState();
    }

    #region Window: minimize / expand / drag
    /// <summary>Compact (as big as its controls, movable), minimized (header only, movable) or expanded (fills the screen above the banner).</summary>
    private void ApplyWindowState()
    {
        var prt = (RectTransform)panel.transform;
        float top = Screen.height > 0 ? Screen.safeArea.yMax / Screen.height : 1f;
        logGo.SetActive(!minimized);
        scrollGo.SetActive(!minimized);
        dragGo.GetComponent<Button>().interactable = !expanded;
        if (expanded && !minimized)
        {
            panelFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            prt.anchorMin = new Vector2(0, BottomReserve);
            prt.anchorMax = new Vector2(1, top);
            prt.pivot = new Vector2(0.5f, 1);
            prt.offsetMin = new Vector2(Space, 0);
            prt.offsetMax = new Vector2(-Space, -Space);
            scrollSize.flexibleHeight = 1;
            scrollSize.preferredHeight = -1;
        }
        else
        {
            panelFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, top);
            prt.pivot = new Vector2(0.5f, 1);
            prt.sizeDelta = new Vector2(Columns * Cell.x + (Columns - 1) * Space + 32, 0);
            prt.anchoredPosition = compactPosition;
            scrollSize.flexibleHeight = 0;
        }
        if (panel.activeSelf)
        {
            Relayout();
            relayoutNextFrame = true;
        }
    }

    private void ToggleMinimize()
    {
        minimized = !minimized;
        ApplyWindowState();
    }

    private void ToggleExpand()
    {
        if (minimized)
            minimized = false;     // expand from the minimized bar = open again
        else
            expanded = !expanded;
        ApplyWindowState();
    }

    /// <summary>Moves the compact / minimized popup; the header always stays on screen.</summary>
    private void Drag(Vector2 screenDelta)
    {
        if (expanded && !minimized)
            return;
        var prt = (RectTransform)panel.transform;
        var canvasRect = ((RectTransform)canvasGo.transform).rect;
        float scale = canvasGo.GetComponent<Canvas>().scaleFactor;
        Vector2 p = prt.anchoredPosition + screenDelta / (scale > 0 ? scale : 1f);
        // anchor = top centre of the safe area, pivot = top centre of the popup
        float anchorY = canvasRect.height * (prt.anchorMin.y - 0.5f);
        float halfW = prt.rect.width / 2, header = Cell.y + 24;
        p.x = Mathf.Clamp(p.x, -canvasRect.width / 2 + halfW, canvasRect.width / 2 - halfW);
        p.y = Mathf.Clamp(p.y, -canvasRect.height / 2 - anchorY + header, canvasRect.height / 2 - anchorY);
        prt.anchoredPosition = compactPosition = p;
    }

    /// <summary>Top-left handle of the header: drag it to move the popup.</summary>
    private sealed class DragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        public AdsTestAllPanel owner;
        public void OnBeginDrag(PointerEventData e) { }
        public void OnDrag(PointerEventData e) => owner.Drag(e.delta);
    }

    /// <summary>ButtonTestAd with a drawn icon instead of text (the font has no ✕ / □ / – glyphs).</summary>
    private GameObject IconButton(Transform parent, string name, Action<RectTransform, Color> draw, Action onClick)
    {
        var go = AddButton(parent, () => "", onClick);
        go.name = name;
        var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
        le.preferredWidth = le.minWidth = IconCell.x;
        le.preferredHeight = le.minHeight = IconCell.y;
        le.flexibleWidth = 0;
        var icon = NewRect("Icon", go.transform);
        icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 0.5f);
        icon.sizeDelta = new Vector2(24, 24);
        draw(icon, go.GetComponentInChildren<TMP_Text>(true).color);
        return go;
    }

    private static void Bar(RectTransform parent, Vector2 position, Vector2 size, Color color, float angle = 0)
    {
        var bar = new GameObject("Bar", typeof(RectTransform), typeof(Image));
        bar.transform.SetParent(parent, false);
        var rt = (RectTransform)bar.transform;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
        rt.localEulerAngles = new Vector3(0, 0, angle);
        var img = bar.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
    }

    private static void DrawMinimize(RectTransform r, Color c) => Bar(r, new Vector2(0, -8), new Vector2(20, 4), c);

    private static void DrawExpand(RectTransform r, Color c)
    {
        Bar(r, new Vector2(0, 9), new Vector2(22, 4), c);
        Bar(r, new Vector2(0, -9), new Vector2(22, 4), c);
        Bar(r, new Vector2(-9, 0), new Vector2(4, 22), c);
        Bar(r, new Vector2(9, 0), new Vector2(4, 22), c);
    }

    private static void DrawClose(RectTransform r, Color c)
    {
        Bar(r, Vector2.zero, new Vector2(26, 4), c, 45);
        Bar(r, Vector2.zero, new Vector2(26, 4), c, -45);
    }

    private static void DrawGrip(RectTransform r, Color c)
    {
        Bar(r, new Vector2(0, 7), new Vector2(24, 3), c);
        Bar(r, new Vector2(0, 0), new Vector2(24, 3), c);
        Bar(r, new Vector2(0, -7), new Vector2(24, 3), c);
    }
    #endregion

    /// <summary>One screen, no tabs: status, flows, every placement, tools.</summary>
    private void BuildContent()
    {
        foreach (Transform child in content) Destroy(child.gameObject);
        content.DetachChildren();
        grid = null;
        refreshers.Clear();
        title.text = "Ads Debug";

        InfoCard(StatusText);

        Section("Flows");
        var c = Config;
        AddFlow("Inter", () => c.adInterFlow, v => c.adInterFlow = v);
        AddFlow("Reward", () => c.adRewardFlow, v => c.adRewardFlow = v);
        AddFlow("AppOpen", () => c.adAppOpenFlow, v => c.adAppOpenFlow = v);
        AddFlow("Banner", () => c.adBannerFlow, v => c.adBannerFlow = v);

        // Every PlacementType, also the ones this scene does not have (their buttons say so).
        Section("Placement");
        var types = ((PlacementType[])Enum.GetValues(typeof(PlacementType))).Where(t => t != PlacementType.None).ToList();
        placementIndex = Mathf.Clamp(placementIndex, 0, types.Count - 1);
        AdBase P() => Find(types[placementIndex]);
        void Run(Func<AdBase, System.Collections.IEnumerator> call)
        {
            var p = P();
            if (p == null) { Print($"{types[placementIndex]}: not in this scene"); return; }
            if (!p.gameObject.activeSelf) p.gameObject.SetActive(true);
            p.StartCoroutine(call(p));
        }
        AddDropdown(types.Select(t => t.ToString().Replace("AdZative", "Native").Replace("Default", "")).ToList(), () => placementIndex, i => placementIndex = i);
        AddButton(() => "Load", () => Run(p => p.IELoadAd(Cb(p.placement.ToString()), "debug", Config.adLoadTimeout, ShowLoadingMode.Toast)));
        AddButton(() => "Show", () => Run(p => p.IELoadShowAd(Cb(p.placement.ToString()), "debug", Config.adLoadTimeout, ShowLoadingMode.Toast)));
        AddButton(() => "Destroy", () => { var p = P(); if (p != null) p.DestroyAd(); });

        Section("Tools");
        AddButton(() => $"Force Ads: {(AdsManager.ForceAds ? "ON" : "OFF")}", () =>
        {
            AdsManager.ForceAds = !AdsManager.ForceAds;
            Print(AdsManager.ForceAds ? "Force Ads ON: every ad call skips the clock, min plays, review, VIP / Remove Ads, ForceReward" : "Force Ads OFF");
        });
        AddButton(() => $"Test IDs: {(Config.useIdTest ? "ON" : "OFF")}", () => Config.useIdTest = !Config.useIdTest);
        AddButton(() => "Ad Inspector", AdsManager.OpenAdInspector);
        AddButton(() => $"VIP: {(User.isVIP ? "ON" : "OFF")}", () => User.isVIP = !User.isVIP);
        AddButton(() => $"Remove Ads: {(User.isRemovedAds ? "ON" : "OFF")}", () => User.isRemovedAds = !User.isRemovedAds);

        content.anchoredPosition = Vector2.zero;
        Refresh();
        Relayout();
        relayoutNextFrame = true;   // once more after the layout groups settled
    }

    /// <summary>
    /// Left-aligned TMP texts created under layout groups keep the mesh of their first (default-size) rect; rebuild the
    /// layout, then the text meshes, so they sit in their final cards / cells.
    /// </summary>
    private void Relayout()
    {
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        // The scroll area is as tall as its content, up to the space above the banner / validator strip.
        var canvasRect = (RectTransform)canvasGo.transform;
        var prt = (RectTransform)panel.transform;
        float max = canvasRect.rect.height * (prt.anchorMax.y - BottomReserve) - Cell.y - LogHeight - 28 - 3 * Space;
        if (!expanded)
            scrollSize.preferredHeight = Mathf.Min(LayoutUtility.GetPreferredHeight(content), max);
        LayoutRebuilder.ForceRebuildLayoutImmediate(prt);
        foreach (var t in content.GetComponentsInChildren<TMP_Text>(true))
            t.ForceMeshUpdate(true);
    }

    private void Refresh()
    {
        foreach (var r in refreshers) r();
        foreach (var label in panel.GetComponentsInChildren<ButtonLabel>(true)) label.Refresh();
    }
    #endregion

    #region Building blocks
    /// <summary>Small grey heading; the controls added after it go into a new 4-column grid.</summary>
    private void Section(string text)
    {
        var t = NewText(content, text.ToUpperInvariant(), 22, TextSub, TextAlignmentOptions.BottomLeft);
        t.gameObject.AddComponent<LayoutElement>().preferredHeight = 38;
        t.margin = new Vector4(4, 0, 0, 2);
        grid = null;
    }

    /// <summary>A card whose text is rebuilt on every refresh; its height follows the text.</summary>
    private void InfoCard(Func<string> text)
    {
        var card = new GameObject("Card", typeof(RectTransform), typeof(VerticalLayoutGroup));
        card.transform.SetParent(content, false);
        AddImage(card, Card);
        var vl = card.GetComponent<VerticalLayoutGroup>();
        vl.padding = new RectOffset(14, 14, 10, 10);
        vl.childControlWidth = vl.childControlHeight = true;
        vl.childForceExpandWidth = vl.childForceExpandHeight = true;
        var t = NewText(card.transform, text(), 24, TextMain, TextAlignmentOptions.TopLeft);
        t.overflowMode = TextOverflowModes.Overflow;
        refreshers.Add(() => t.text = text());
        grid = null;
    }

    /// <summary>Grid of base prefabs (192 x 40 cells), 4 per row.</summary>
    private RectTransform Grid()
    {
        if (grid != null) return grid;
        grid = NewRect("Grid", content);
        var g = grid.gameObject.AddComponent<GridLayoutGroup>();
        g.cellSize = Cell;
        g.spacing = new Vector2(Space, Space);
        g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        g.constraintCount = Columns;
        return grid;
    }

    private GameObject AddButton(Transform parent, Func<string> label, Action onClick)
    {
        var go = Instantiate(buttonPrefab, parent, false);
        var text = go.GetComponentInChildren<TMP_Text>(true);
        text.maskable = true;   // the prefab label is not maskable: it would draw outside the scroll area
        text.RecalculateClipping();
        text.RecalculateMasking();
        Fit(text);
        go.AddComponent<ButtonLabel>().Set(text, label);
        var button = go.GetComponent<Button>();
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() =>
        {
            try { onClick(); }
            catch (Exception ex) { Print("ERROR " + ex.Message); Debug.LogException(ex); }
            Refresh();
        });
        return go;
    }

    private void AddButton(Func<string> label, Action onClick) => AddButton(Grid(), label, onClick);

    private TMP_Dropdown AddDropdown(List<string> options, Func<int> get, Action<int> set)
    {
        var dropdown = Instantiate(dropdownPrefab, Grid(), false).GetComponent<TMP_Dropdown>();
        Fit(dropdown.captionText);
        Fit(dropdown.itemText);
        dropdown.onValueChanged.RemoveAllListeners();
        dropdown.ClearOptions();
        dropdown.AddOptions(options);
        // The prefab list is 3 options tall (flows): make it fit every option, and clip its labels to the list.
        dropdown.template.sizeDelta = new Vector2(dropdown.template.sizeDelta.x, Cell.y * options.Count);
        dropdown.itemText.maskable = true;
        dropdown.SetValueWithoutNotify(get());
        dropdown.onValueChanged.AddListener(i =>
        {
            set(i);
            Refresh();
        });
        refreshers.Add(() => { if (dropdown != null) dropdown.SetValueWithoutNotify(get()); });
        return dropdown;
    }

    /// <summary>Flow dropdown: option index = AdFlow value (OnlyNative, OnlyDefault, Both).</summary>
    private void AddFlow(string name, Func<AdFlow> get, Action<AdFlow> set)
    {
        AddDropdown(new List<string> { $"{name}: Native", $"{name}: AdMob", $"{name}: Both" }, () => (int)get(), i =>
        {
            set((AdFlow)i);
            Print($"{name} flow = {(AdFlow)i}");
        });
    }

    /// <summary>Long labels shrink to fit the cell instead of spilling out of it.</summary>
    private static void Fit(TMP_Text text)
    {
        if (text == null) return;
        text.enableWordWrapping = false;
        text.fontSizeMax = text.fontSize;
        text.fontSizeMin = 10;
        text.enableAutoSizing = true;
    }

    /// <summary>Keeps a prefab at its Debug column size inside a layout group.</summary>
    private static void FixedSize(GameObject go)
    {
        var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
        le.preferredWidth = le.minWidth = Cell.x;
        le.preferredHeight = le.minHeight = Cell.y;
    }

    /// <summary>A fixed-height row: the base prefabs inside must not make it stretch.</summary>
    private static void Fixed(GameObject go, float height)
    {
        var le = go.GetComponent<LayoutElement>();
        le.preferredHeight = le.minHeight = height;
        le.flexibleHeight = 0;
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private TMP_Text NewText(Transform parent, string text, float size, Color color, TextAlignmentOptions align)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var t = go.GetComponent<TextMeshProUGUI>();
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.enableWordWrapping = true;
        t.overflowMode = TextOverflowModes.Ellipsis;
        t.raycastTarget = false;
        return t;
    }

    private static void AddImage(GameObject go, Color color)
    {
        var img = go.GetComponent<Image>() ?? go.AddComponent<Image>();
        img.sprite = Rounded();
        img.type = Image.Type.Sliced;
        img.color = color;
    }

    /// <summary>White rounded rectangle (radius 16 px), 9-sliced so every size keeps round corners.</summary>
    private static Sprite Rounded()
    {
        if (rounded != null) return rounded;
        const int size = 48, r = 16;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(0, Mathf.Max(r - x - 0.5f, x + 0.5f - (size - r)));
                float dy = Mathf.Max(0, Mathf.Max(r - y - 0.5f, y + 0.5f - (size - r)));
                float a = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        tex.SetPixels32(px);
        tex.Apply();
        rounded = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
        return rounded;
    }

    private static Color Hex(string hex, float alpha = 1f)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out var c);
        c.a = alpha;
        return c;
    }

    #endregion

    #region Status
    private string StatusText()
    {
        var c = Config;
        var u = User;
        if (c == null || u == null) return "DataManager not ready";
        float left = AdsManager.SecondsUntilAdAllowed;
        bool review = InAppReview.Instance != null && InAppReview.Instance.isTimeToShow;
        int ratio = c.adInterVsRewardRatio;
        // Same rule as AdsManager.ForceReward, which writes a log line on every read.
        bool forceNext = ratio > 0 && u.adInterstitial / ratio > u.adRewarded + u.adRewardSkipped;
        string force = ratio <= 0 ? "off" : forceNext ? "next inter" : $"in {ratio - u.adInterstitial % ratio} inter";
        var sb = new StringBuilder();
        string inter = u.totalPlay < c.adInterOnPlay ? $"<color=#EF4444>needs {c.adInterOnPlay - u.totalPlay} more play(s)</color>"
            : left <= 0 ? "<color=#22C55E>READY</color>" : $"{left:0}s left";
        if (AdsManager.ForceAds) inter = "<color=#F59E0B>FORCE ADS ON</color>";
        sb.AppendLine($"Inter: {inter}   gap {AdsManager.TimePlayToShowAds:0}s   plays {u.totalPlay}/{c.adInterOnPlay}" +
                      (review ? "   <color=#EF4444>blocked by InAppReview</color>" : ""));
        sb.AppendLine($"SDK: AdMob {AdsManager.IsInitialized}   Native {AdZativeSDK.IsInitialized}   UMP {UMP.CanRequestAds}   Test IDs {AdsManager.UseIdTest}");
        sb.AppendLine($"Counters: inter {u.adInterstitial}   reward {u.adRewarded}   skipped {u.adRewardSkipped}   ForceReward {force}");
        // The placement picked in the dropdown.
        var type = (PlacementType[])Enum.GetValues(typeof(PlacementType));
        var picked = type.Where(t => t != PlacementType.None).ElementAtOrDefault(placementIndex);
        var ad = Find(picked);
        string adState = ad == null ? "<color=#EF4444>not in this scene</color>" : ad.isCanShow ? "<color=#22C55E>READY</color>" : ad.state.ToString();
        sb.Append($"{picked}: {adState}{(ad != null && !string.IsNullOrEmpty(ad.LastError) ? "   " + ad.LastError : "")}");
        return sb.ToString();
    }
    #endregion

    #region Actions
    /// <summary>Placements of AdsManager plus scene placements outside it (e.g. the banner under CanvasBase).</summary>
    private static List<AdBase> AllPlacements()
    {
        var list = new List<AdBase>(AdsManager.Placements.Values.Where(a => a != null));
        foreach (var a in FindObjectsOfType<AdBase>(true))
            if (!list.Contains(a)) list.Add(a);
        return list.OrderBy(a => a.placement.ToString()).ToList();
    }

    private static AdBase Find(PlacementType type) => AllPlacements().FirstOrDefault(a => a.placement == type);

    private Action<AdType, AdState> Cb(string name) => (type, state) => Print($"{name}: {type} {state}");
    #endregion

    #region Log
    private static readonly HashSet<string> LoggedEvents = new HashSet<string>
    {
        "unit_loaded", "unit_failed", "shown", "impression", "paid", "clicked", "closed", "show_failed",
        "replaced", "video_gate", "reward_earned", "reward_skipped", "pod_next"
    };

    private void OnPluginEvent(AdZativeEvent e)
    {
        if (!LoggedEvents.Contains(e.Name)) return;
        // The banner reloads by itself every 30 s: only its failures are logged, or it pushes the tested ad out of the log.
        bool failed = e.Name == "unit_failed" || e.Name == "show_failed";
        if (!failed && e.SlotId != null && e.SlotId.IndexOf("Banner", StringComparison.OrdinalIgnoreCase) >= 0) return;
        var p = e.Payload;
        string extra = e.Name == "paid" ? $" {p.value_micros} micros {p.currency}"
            : failed ? $" {p.message}{p.reason}"
            : !string.IsNullOrEmpty(p?.network) ? $" {p.network}" : "";
        Print($"[{e.SlotId}] {e.Name}{extra}");
    }

    private void OnStateChanged(AdBase ad, AdState state)
    {
        if (state == AdState.None) return;
        bool failed = state == AdState.LoadNotAvailable || state == AdState.ShowFailed;
        if (ad.type == AdType.Banner && !failed) return;
        Print($"{ad.placement} -> {state}{(failed ? " " + ad.LastError : "")}");
    }

    private void Print(string line)
    {
        Debug.Log("[AdsTestAll] " + line);
        logLines.Insert(0, $"{DateTime.Now:HH:mm:ss} {line}");
        if (logLines.Count > 200) logLines.RemoveRange(200, logLines.Count - 200);
        if (logText != null) logText.text = string.Join("\n", logLines.Take(7));
    }
    #endregion

    /// <summary>Keeps a prefab button's label in sync with its state (ON/OFF, READY, values).</summary>
    private sealed class ButtonLabel : MonoBehaviour
    {
        private TMP_Text text;
        private Func<string> label;

        public void Set(TMP_Text t, Func<string> l)
        {
            text = t;
            label = l;
            Refresh();
        }

        public void Refresh()
        {
            if (text != null) text.text = label();
        }
    }
}
