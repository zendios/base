using System;
using System.Collections.Generic;
using Base;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The sample's flow test panel, styled like the Test All Ads panel (AdsTestAllPanel): its own overlay canvas, a dark
/// card that moves by its header, minimize, ButtonTestAd buttons, a log of the state changes. Always shown in the sample
/// scene (no Debug Mode needed): State, Play, Mode, Win, Lose, Pause / Resume, Next, Restart, Home; a button that does
/// not fit the current state is not interactable. Remove it (the component) in a game.
/// </summary>
public class GameFlowTestPanel : MonoBehaviour
{
    // Above the game UI and the ad canvases (31000), under the Debug column (32000) and Test All Ads (32100).
    private const int SortingOrder = 31500;
    private const int Columns = 3;
    private const float Space = 8;
    private const int LogLines = 4;
    private static readonly Vector2 Cell = new Vector2(160, 40);   // ButtonTestAd size
    private static readonly Color Bg = new Color32(14, 19, 25, 235);
    private static readonly Color CardColor = new Color32(25, 33, 42, 255);
    private static readonly Color TextMain = new Color32(232, 237, 242, 255);
    private static readonly Color LogColor = new Color32(167, 243, 196, 255);

    [Tooltip("Base/Ads/Prefabs/ButtonTestAd.prefab")]
    [SerializeField] private GameObject buttonPrefab;
    [SerializeField] private GameFlow flow;

    private readonly List<(Button button, TMP_Text text, Func<string> label, Func<bool> canUse)> buttons =
        new List<(Button, TMP_Text, Func<string>, Func<bool>)>();
    private readonly List<string> log = new List<string>();
    private GameObject canvasGo, body;
    private RectTransform panel;
    private TMP_Text title, logText;
    private TMP_FontAsset font;

    private static GameState State => GameStateManager.CurrentState;

    private void Start()
    {
        font = buttonPrefab.GetComponentInChildren<TMP_Text>(true).font;
        BuildPanel();
        Refresh();
    }

    private void OnEnable()
    {
        GameStateManager.OnGameStateChanged += OnGameStateChanged;
        flow.OnModeChanged += OnModeChanged;
    }

    private void OnDisable()
    {
        GameStateManager.OnGameStateChanged -= OnGameStateChanged;
        flow.OnModeChanged -= OnModeChanged;
    }

    private void OnDestroy()
    {
        if (canvasGo != null)
            Destroy(canvasGo);
    }

    private void OnGameStateChanged(GameState current, GameState last, object data)
    {
        log.Insert(0, $"{DateTime.Now:HH:mm:ss}  {last} > {current}");
        if (log.Count > LogLines)
            log.RemoveAt(LogLines);
        Refresh();
    }

    private void OnModeChanged(GameFlow.Mode mode) => Refresh();

    private void BuildPanel()
    {
        canvasGo = new GameObject("GameFlowTestCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = Screen.width > Screen.height ? 1f : 0f;

        // Card at the bottom left, above the banner, off the popups' centre; drag the header to move it.
        panel = NewRect("Panel", canvasGo.transform);
        panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.zero;
        panel.anchoredPosition = new Vector2(16, 140);
        AddImage(panel.gameObject, Bg);
        var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 10, 12);
        layout.spacing = Space;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        var fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Header: title with the state (drag handle), minimize button.
        var header = NewRect("Header", panel);
        var headerLayout = header.gameObject.AddComponent<HorizontalLayoutGroup>();
        headerLayout.spacing = Space;
        headerLayout.childControlWidth = headerLayout.childControlHeight = true;
        headerLayout.childForceExpandWidth = false;
        headerLayout.childForceExpandHeight = true;
        header.gameObject.AddComponent<LayoutElement>().preferredHeight = Cell.y;
        title = NewText(header, 26, TextMain, TextAlignmentOptions.MidlineLeft);
        title.raycastTarget = true;   // the drag handle
        title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        title.gameObject.AddComponent<DragHandle>().panel = panel;
        var minimize = AddButton(header, () => body.activeSelf ? "-" : "+", () => true, () => body.SetActive(!body.activeSelf));
        var minimizeSize = minimize.AddComponent<LayoutElement>();
        minimizeSize.preferredWidth = minimizeSize.minWidth = Cell.y + 16;

        body = NewRect("Body", panel).gameObject;
        var bodyLayout = body.AddComponent<VerticalLayoutGroup>();
        bodyLayout.spacing = Space;
        bodyLayout.childControlWidth = bodyLayout.childControlHeight = true;
        bodyLayout.childForceExpandWidth = true;
        bodyLayout.childForceExpandHeight = false;

        var grid = NewRect("Buttons", body.transform);
        var gridLayout = grid.gameObject.AddComponent<GridLayoutGroup>();
        gridLayout.cellSize = Cell;
        gridLayout.spacing = new Vector2(Space, Space);
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = Columns;
        AddButton(grid, () => "Play", () => State == GameState.None || State == GameState.Main, flow.StartGame);
        AddButton(grid, () => "Mode: " + flow.CurrentMode, () => State == GameState.None || State == GameState.Main,
            flow.ToggleMode);
        AddButton(grid, () => State == GameState.Pause ? "Resume" : "Pause",
            () => State == GameState.Play || State == GameState.Pause, flow.TogglePause);
        AddButton(grid, () => "Win", () => State == GameState.Play, flow.Win);
        AddButton(grid, () => "Lose", () => State == GameState.Play, flow.Lose);
        AddButton(grid, () => "Next", () => State == GameState.Win, flow.Next);
        AddButton(grid, () => "Restart", () => State == GameState.Win || State == GameState.Lose, flow.Restart);
        AddButton(grid, () => "Home", () => State != GameState.None && State != GameState.Main, Home);

        var logCard = NewRect("Log", body.transform);
        AddImage(logCard.gameObject, CardColor);
        logCard.gameObject.AddComponent<LayoutElement>().preferredHeight = LogLines * 26 + 16;
        logText = NewText(logCard, 22, LogColor, TextAlignmentOptions.TopLeft);
        logText.rectTransform.offsetMin = new Vector2(10, 8);
        logText.rectTransform.offsetMax = new Vector2(-10, -8);
    }

    // Leaves the game from any state, as the pause popup's Home does.
    private static void Home()
    {
        UIManager.CloseAllPopups();
        GameStateManager.Main();
    }

    private void Refresh()
    {
        if (title == null)
            return;
        title.text = $"GAME FLOW   <color=#A7F3C4>{State}</color>";
        logText.text = string.Join("\n", log);
        foreach (var entry in buttons)
        {
            entry.text.text = entry.label();
            entry.button.interactable = entry.canUse();
        }
    }

    private GameObject AddButton(Transform parent, Func<string> label, Func<bool> canUse, Action onClick)
    {
        var go = Instantiate(buttonPrefab, parent, false);
        var button = go.GetComponent<Button>();
        var text = go.GetComponentInChildren<TMP_Text>(true);
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() =>
        {
            onClick();
            Refresh();
        });
        buttons.Add((button, text, label, canUse));
        return go;
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private TMP_Text NewText(Transform parent, float size, Color color, TextAlignmentOptions align)
    {
        var rect = NewRect("Text", parent);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = size;
        text.color = color;
        text.alignment = align;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }

    private static void AddImage(GameObject go, Color color)
    {
        var image = go.AddComponent<Image>();
        image.color = color;
    }

    /// <summary>The header moves the panel; it stays inside the screen.</summary>
    private sealed class DragHandle : MonoBehaviour, IDragHandler
    {
        public RectTransform panel;

        public void OnDrag(PointerEventData eventData)
        {
            var canvas = panel.GetComponentInParent<Canvas>();
            var area = ((RectTransform)canvas.transform).rect;
            var position = panel.anchoredPosition + eventData.delta / canvas.scaleFactor;
            position.x = Mathf.Clamp(position.x, 0, area.width - panel.rect.width);
            position.y = Mathf.Clamp(position.y, 0, area.height - panel.rect.height);
            panel.anchoredPosition = position;
        }
    }
}
