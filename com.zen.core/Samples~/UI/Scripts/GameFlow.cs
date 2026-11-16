using System;
using System.Collections;
using System.Globalization;
using Base;
using UnityEngine;

/// <summary>
/// The game of the UI sample: no gameplay, only the GameState flow a real game goes through, driven by the debug
/// buttons of GameFlowTestPanel. Swap it for your gameplay and keep telling GameStateManager:
///   Home Play → Load → Init → Ready (UIScreenGame counts 3-2-1) → Play
///   Win  → WaitWin (end animation, controls hidden) → Win (UIPopupResult)
///   Lose → WaitLose (UIPopupRevive while revives are left, else Lose) → ReviveContinue → Play, or Lose (UIPopupResult)
///   Pause ↔ Play; Next / Restart → Load; Result closed → Main (GameEndAd: interstitial)
/// It also switches the screens: Load shows UIScreenGame, Main shows UIScreenMain.
/// </summary>
public class GameFlow : MonoBehaviour
{
    public enum Mode { Casual, Racing }

    [SerializeField] private Mode mode = Mode.Casual;
    [Tooltip("Seconds of the fake loading (Load) and level building (Init).")]
    [SerializeField, Min(0f)] private float loadSeconds = 0.5f;
    [SerializeField, Min(0f)] private float initSeconds = 0.3f;
    [Tooltip("Seconds of the end animation (WaitWin) before the result.")]
    [SerializeField, Min(0f)] private float winSeconds = 1f;
    [SerializeField, Min(0)] private int revivesPerGame = 1;
    [Tooltip("Progress per second while playing: score (Casual) or metres (Racing).")]
    [SerializeField, Min(1f)] private float progressPerSecond = 40f;
    [Tooltip("Coins for places 1, 2, 3, 4... (Racing).")]
    [SerializeField] private int[] placeCoins = { 100, 60, 40, 20 };
    [Tooltip("SoundManager plays them at Win / Lose (with a vibration).")]
    [SerializeField] private AudioClip winSound;
    [SerializeField] private AudioClip loseSound;

    private GameResult result = new GameResult();
    private float progress;
    private bool running;
    private int level = 1;

    /// <summary>The sample's mode changed (the debug button).</summary>
    public event Action<Mode> OnModeChanged;

    public Mode CurrentMode => mode;

    /// <summary>Score or metres of the game in progress.</summary>
    public int Progress => Mathf.FloorToInt(progress);

    public int Level => level;

    private void OnEnable() => GameStateManager.OnGameStateChanged += OnGameStateChanged;

    private void OnDisable() => GameStateManager.OnGameStateChanged -= OnGameStateChanged;

    private void Update()
    {
        if (running)
            progress += progressPerSecond * Time.deltaTime;
    }

    public void ToggleMode()
    {
        mode = mode == Mode.Casual ? Mode.Racing : Mode.Casual;
        OnModeChanged?.Invoke(mode);
    }

    /// <summary>Home Play button: the game starts loading.</summary>
    public void StartGame() => GameStateManager.Load();

    /// <summary>The player won: end animation, then the result.</summary>
    public void Win()
    {
        if (GameStateManager.CurrentState != GameState.Play)
            return;
        Build(true);
        GameStateManager.WaitWin(result);
        StartCoroutine(After(winSeconds, () => GameStateManager.Win(result)));
    }

    /// <summary>The player failed: a revive offer (rewarded ad) while revives are left, else the game ends.</summary>
    public void Lose()
    {
        if (GameStateManager.CurrentState != GameState.Play)
            return;
        Build(false);
        result.revivesLeft = Mathf.Max(0, revivesPerGame - result.revives);
        GameStateManager.WaitLose(result);
        if (result.revivesLeft > 0)
            UIPopupRevive.ShowFor(result);
        else
            UnityMainThreadDispatcher.Enqueue(() => GameStateManager.Lose(result));   // after every listener saw WaitLose
    }

    public void TogglePause()
    {
        if (GameStateManager.CurrentState == GameState.Play)
            GameStateManager.Pause();
        else if (GameStateManager.CurrentState == GameState.Pause)
            GameStateManager.Play();   // straight back, not through Resume: Missions would count a new game
    }

    /// <summary>After a win: the next level.</summary>
    public void Next()
    {
        if (GameStateManager.CurrentState != GameState.Win)
            return;
        level++;
        GameStateManager.Next(result);
        UIManager.CloseAllPopups();
    }

    /// <summary>After a win or a loss: the same level again.</summary>
    public void Restart()
    {
        var state = GameStateManager.CurrentState;
        if (state != GameState.Win && state != GameState.Lose)
            return;
        GameStateManager.Restart(result);
        UIManager.CloseAllPopups();
    }

    private void OnGameStateChanged(GameState current, GameState last, object data)
    {
        running = current == GameState.Play;
        switch (current)
        {
            case GameState.Main:
                StopAllCoroutines();
                UIManager.ShowScreen<UIScreenMain>(false);
                break;
            case GameState.Load:
                UIManager.ShowScreen<UIScreenGame>(false);
                StartCoroutine(After(loadSeconds, () => GameStateManager.Init()));
                break;
            case GameState.Init:
                progress = 0f;
                result = new GameResult();
                StartCoroutine(After(initSeconds, () => GameStateManager.Ready()));
                break;
            case GameState.ReviveContinue:
            case GameState.ReviveCheckPoint:
                result.revives++;
                UnityMainThreadDispatcher.Enqueue(() => GameStateManager.Play());   // after every listener saw Revive
                break;
            case GameState.Pause:
                UIPopupPause.Show();
                break;
            case GameState.Play:
                if (last == GameState.Pause)
                    UIPopupPause.Hide();   // resumed by the HUD button, the debug button or the back key
                break;
            case GameState.Win:
            case GameState.Lose:
                bool isWin = current == GameState.Win;
                SoundManager.Play(isWin ? winSound : loseSound);
                HapticManager.PlayImpact(isWin ? HapticManager.ImpactStyle.Medium : HapticManager.ImpactStyle.Heavy);
                UIPopupResult.ShowFor(data as GameResult ?? result);
                break;
            case GameState.Next:
            case GameState.Restart:
                UnityMainThreadDispatcher.Enqueue(() => GameStateManager.Load());
                break;
            default:
                break;   // Ready: UIScreenGame counts down and calls Play
        }
    }

    // The result of the mode: what a casual level or a race would report.
    private void Build(bool isWin)
    {
        int value = Progress;
        string amount = value.ToString("N0", CultureInfo.InvariantCulture);
        int revives = result.revives;
        result = new GameResult { isWin = isWin, revives = revives };
        if (mode == Mode.Casual)
        {
            result.stars = isWin ? (value >= 300 ? 3 : value >= 150 ? 2 : 1) : 0;
            result.title = isWin ? "LEVEL COMPLETE" : "LEVEL FAILED";
            result.description = "Level " + level;
            result.progress = "Score " + amount;
            result.Add("Level " + level, isWin ? "Complete" : "Failed", isWin ? 100 : 20);
            if (isWin)
                result.Add("Stars", result.stars + " / 3", result.stars * 20);
            result.Add("Score", amount, value / 10);
        }
        else
        {
            int place = isWin ? 1 : 0;
            int picked = value / 20;
            result.stars = isWin ? 3 : 0;
            result.title = isWin ? "1ST PLACE!" : "CRASHED";
            result.description = "Race " + level;
            result.progress = amount + " m";
            result.Add("Place", isWin ? "1st" : "Crashed", place > 0 ? placeCoins[Mathf.Min(place, placeCoins.Length) - 1] : 0);
            result.Add("Distance", amount + " m", value / 10);
            result.Add("Coins picked", "x" + picked, picked);
        }
    }

    private static IEnumerator After(float seconds, Action action)
    {
        yield return new WaitForSecondsRealtime(seconds);
        action();
    }
}
