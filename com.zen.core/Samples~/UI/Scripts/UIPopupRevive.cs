using Base;
using Base.Ads;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Continue?" after the player failed (GameFlow.Lose opens it at WaitLose). A countdown runs (filled image + seconds);
/// Revive is a rewarded ad, the button always shown (Ads.md 4.4), and the countdown waits while the ad plays.
/// Success → GameStateManager.Revive(); No thanks, the back key or the end of the countdown end the game: the
/// interstitial of the game end (AdConfig.adInterOnComplete; AdsManager decides if it is time), then
/// GameStateManager.Lose.
/// </summary>
public class UIPopupRevive : UIPopupBase<UIPopupRevive>
{
    [SerializeField] private Button reviveButton;
    [Tooltip("The game so far (GameResult.progress): \"350 m\", \"Score 1,200\".")]
    [SerializeField] private TMP_Text infoText;
    [Tooltip("Image of type Filled: empties with the countdown.")]
    [SerializeField] private Image countdownFill;
    [SerializeField] private TMP_Text countdownText;
    [SerializeField, Min(1)] private int countdownSeconds = 5;

    private GameResult result;
    private bool answered;
    private bool isWatching;
    private float timeLeft;
    private int shownSeconds = -1;

    /// <summary>Opens the popup for this failed game.</summary>
    public static void ShowFor(GameResult result)
    {
        var popup = Show();
        if (popup == null)
        {
            GameStateManager.Lose(result);
            return;
        }
        popup.result = result;
        popup.answered = false;
        popup.isWatching = false;
        popup.timeLeft = popup.countdownSeconds;
        popup.shownSeconds = -1;
        popup.reviveButton.interactable = true;
        popup.infoText.text = result.progress;
    }

    protected override void Awake()
    {
        base.Awake();
        reviveButton.onClick.AddListener(Revive);
    }

    private void Update()
    {
        if (answered || isWatching)
            return;
        timeLeft -= Time.unscaledDeltaTime;
        countdownFill.fillAmount = Mathf.Clamp01(timeLeft / countdownSeconds);
        int seconds = Mathf.Max(0, Mathf.CeilToInt(timeLeft));
        if (seconds != shownSeconds)
            countdownText.text = (shownSeconds = seconds).ToString();
        if (timeLeft <= 0f)
            Close();   // OnHide: Lose
    }

    protected override void OnHide()
    {
        if (answered)
            return;
        answered = true;
        GameEndAd.Show("revive_skip", () => GameStateManager.Lose(result));
    }

    private void Revive()
    {
        isWatching = true;
        reviveButton.interactable = false;
        AdsManager.ShowReward((type, state) =>
        {
            isWatching = false;
            if (state != AdState.ShowSuccess)
            {
                reviveButton.interactable = true;
                return;
            }
            answered = true;
            Close();
            GameStateManager.Revive();
        }, "revive");
    }
}
