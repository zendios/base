using Base;
using Base.Ads;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The result of a game (GameFlow opens it at Win / Lose): title, stars, one StatsItem row per
/// GameResult.Row (Title, Detail, Coins children), the total. Claim, or "x2 with Ad" (rewarded, always shown);
/// closing claims too. Once hidden and still at Win / Lose: the interstitial of the game end (AdConfig.adInterOnComplete;
/// AdsManager decides if it is time: right after a revive skip or a rewarded x2 it is not), then GameStateManager.Main().
/// Next / Restart leave the state first, then close it (no interstitial on the way to the next game).
/// </summary>
public class UIPopupResult : UIPopupBase<UIPopupResult>
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [Tooltip("The stars, left to right: the first GameResult.stars are shown.")]
    [SerializeField] private GameObject[] stars;
    [Tooltip("The StatsItem rows: one per GameResult.Row, extra rows hidden. Each has Title, Detail and Coins texts.")]
    [SerializeField] private Transform[] rows;
    [SerializeField] private TMP_Text totalText;
    [SerializeField] private Button claimButton;
    [SerializeField] private Button doubleButton;

    private int coins;
    private bool claimed;

    /// <summary>Opens the popup for a finished game.</summary>
    public static void ShowFor(GameResult result)
    {
        var popup = Show();
        if (popup == null)
            return;
        popup.claimed = false;
        popup.coins = result.Total;
        popup.titleText.text = result.title;
        popup.descriptionText.text = result.description;
        for (int i = 0; i < popup.stars.Length; i++)
            popup.stars[i].SetActive(i < result.stars);
        for (int i = 0; i < popup.rows.Length; i++)
        {
            bool shown = i < result.rows.Count;
            popup.rows[i].gameObject.SetActive(shown);
            if (shown)
                SetRow(popup.rows[i], result.rows[i]);
        }
        popup.totalText.text = Amount(popup.coins);
        popup.claimButton.interactable = popup.doubleButton.interactable = true;
    }

    protected override void Awake()
    {
        base.Awake();
        claimButton.onClick.AddListener(() => Claim(1));
        doubleButton.onClick.AddListener(ClaimDouble);
    }

    protected override void OnHide() => Grant(1);   // closing is claiming

    protected override void OnHidden()
    {
        var state = GameStateManager.CurrentState;
        if (state == GameState.Win || state == GameState.Lose)
            GameEndAd.Show("game_end", () => GameStateManager.Main());
    }

    private void ClaimDouble()
    {
        claimButton.interactable = doubleButton.interactable = false;
        AdsManager.ShowReward((type, state) =>
        {
            if (state == AdState.ShowSuccess)
                Claim(2);
            else
                claimButton.interactable = doubleButton.interactable = true;
        }, "result_x2");
    }

    private void Claim(int times)
    {
        Grant(times);
        Close();
    }

    private void Grant(int times)
    {
        if (claimed)
            return;
        claimed = true;
        if (coins > 0)
            CurrencyManager.Add(coins * times, "game_end", CurrencyType.Soft, claimButton.transform);
    }

    private static void SetRow(Transform row, GameResult.Row data)
    {
        SetText(row, "Title", data.title);
        SetText(row, "Detail", data.detail);
        SetText(row, "Coins", Amount(data.coins));
    }

    private static void SetText(Transform row, string child, string value)
    {
        var text = row.Find(child);
        if (text != null && text.TryGetComponent(out TMP_Text tmp))
            tmp.text = value;
    }

    private static string Amount(int value) => value.ToString("N0", CultureInfo.InvariantCulture);
}
