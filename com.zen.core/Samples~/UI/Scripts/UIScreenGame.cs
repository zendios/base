using System.Collections;
using System.Globalization;
using Base;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The in-game screen of the sample (Resources/UIScreenGame; GameFlow shows it at Load): the 3-2-1 countdown at Ready, then the HUD (mode,
/// level, score / distance, pause button). The controls are hidden at WaitWin / WaitLose (end animation, revive offer)
/// and while loading. Back key: pause / resume.
/// </summary>
public class UIScreenGame : UIScreen
{
    [SerializeField] private GameFlow flow;
    [Tooltip("The HUD and its buttons: shown only while playing or paused.")]
    [SerializeField] private GameObject controls;
    [SerializeField] private TMP_Text countdownText;
    [SerializeField] private TMP_Text infoText;
    [SerializeField] private Button pauseButton;

    private static readonly WaitForSecondsRealtime Second = new WaitForSecondsRealtime(1f);
    private int shownProgress = -1;

    protected override void Awake()
    {
        base.Awake();
        if (flow == null)
            flow = FindAnyObjectByType<GameFlow>();   // the screen is loaded from Resources: the scene's GameFlow
        pauseButton.onClick.AddListener(flow.TogglePause);
    }

    private void OnEnable() => GameStateManager.OnGameStateChanged += OnGameStateChanged;

    private void OnDisable() => GameStateManager.OnGameStateChanged -= OnGameStateChanged;

    // The text changes only when the value does: no string per frame.
    private void Update()
    {
        if (!controls.activeSelf || flow.Progress == shownProgress)
            return;
        shownProgress = flow.Progress;
        string amount = shownProgress.ToString("N0", CultureInfo.InvariantCulture);
        infoText.text = flow.CurrentMode == GameFlow.Mode.Casual
            ? "Level " + flow.Level + "   Score " + amount
            : "Race " + flow.Level + "   " + amount + " m";
    }

    protected override void OnShow()
    {
        controls.SetActive(false);
        countdownText.gameObject.SetActive(false);
    }

    public override bool OnBack()
    {
        flow.TogglePause();
        return true;   // never leaves the game by the back key
    }

    private void OnGameStateChanged(GameState current, GameState last, object data)
    {
        switch (current)
        {
            case GameState.Ready:
                StopAllCoroutines();
                StartCoroutine(Countdown());
                break;
            case GameState.Play:
            case GameState.Pause:
                shownProgress = -1;
                controls.SetActive(true);
                break;
            default:
                controls.SetActive(false);   // Load, Init, WaitWin, WaitLose, Win, Lose...
                break;
        }
    }

    private IEnumerator Countdown()
    {
        countdownText.gameObject.SetActive(true);
        for (int i = 3; i > 0; i--)
        {
            countdownText.text = i.ToString();
            yield return Second;
        }
        countdownText.gameObject.SetActive(false);
        GameStateManager.Play();
    }
}
