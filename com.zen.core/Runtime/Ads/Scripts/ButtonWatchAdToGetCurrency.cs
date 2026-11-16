using Base;
using Base.Ads;
using System;
using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Watch an ad, get Soft currency" button (rule 4.4 of Ads.md): always shown, locked while the ad runs, paid on
/// ShowSuccess only (AdsManager.SoftCurrencyByReward). Optional limits, both kept in PlayerPrefs: a cooldown after each
/// reward and a cap per day. timeText shows "FREE" when the player may watch, "mm:ss" during the cooldown and
/// "Tomorrow" once the cap of today is reached. rewardText shows "+500".
/// </summary>
[RequireComponent(typeof(Button))]
public class ButtonWatchAdToGetCurrency : MonoBehaviour
{
    [SerializeField] private TMP_Text rewardText;
    [SerializeField] private TMP_Text timeText;
    [Tooltip("Minutes to wait after a reward. 0 = no cooldown.")]
    [SerializeField, Min(0)] private int cooldownMinutes = 30;
    [Tooltip("Rewards per day (the player's local day). 0 = no cap.")]
    [SerializeField, Min(0)] private int maxPerDay = 5;

    private const string ReadyLabel = "Free";
    private const string CappedLabel = "Tomorrow";

    private Button button;
    private bool isWatching;
    private Coroutine tick;

    // PlayerPrefs, not the save: losing them only gives a free reward back. Keyed by the object name.
    private string DayKey => $"watchad_{name}_day";
    private string CountKey => $"watchad_{name}_count";
    private string NextKey => $"watchad_{name}_next";

    private static string Today => DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

    private bool IsCapped => maxPerDay > 0 && PlayerPrefs.GetString(DayKey) == Today && PlayerPrefs.GetInt(CountKey) >= maxPerDay;

    private double SecondsToWait =>
        long.TryParse(PlayerPrefs.GetString(NextKey), out long ticks)
            ? Math.Max(0, (new DateTime(ticks, DateTimeKind.Utc) - DateTime.UtcNow).TotalSeconds)
            : 0;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(Watch);
    }

    private void OnEnable()
    {
        if (rewardText != null)
            rewardText.text = "+" + AdsManager.SoftCurrencyByReward.ToString("N0");
        Refresh();
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
        // Edit mode: show the ready state so the layout can be judged (the real amount needs the Ads config).
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null || Application.isPlaying)
                return;
            if (rewardText != null)
                rewardText.text = "+500";
            if (timeText != null)
                timeText.text = cooldownMinutes > 0 || maxPerDay > 0 ? ReadyLabel : "";
        };
#endif
    }

    private void Refresh()
    {
        bool blocked = IsCapped || SecondsToWait > 0;
        button.interactable = !blocked && !isWatching;
        UpdateTimeText();
        if (!IsCapped && SecondsToWait > 0 && tick == null && isActiveAndEnabled)
            tick = StartCoroutine(IETick());
    }

    private IEnumerator IETick()
    {
        while (!IsCapped && SecondsToWait > 0)
        {
            UpdateTimeText();
            yield return new WaitForSecondsRealtime(1f);
        }
        tick = null;
        Refresh();
    }

    private void UpdateTimeText()
    {
        if (timeText == null)
            return;
        if (IsCapped)
            timeText.text = CappedLabel;
        else if (SecondsToWait > 0)
            timeText.text = TimeSpan.FromSeconds(Math.Ceiling(SecondsToWait)).ToString(@"mm\:ss");
        else
            timeText.text = cooldownMinutes > 0 || maxPerDay > 0 ? ReadyLabel : "";
    }

    private void Watch()
    {
        button.interactable = false;   // no second tap until the ad result
        isWatching = true;
        int reward = AdsManager.SoftCurrencyByReward;
        AdsManager.ShowReward((type, state) =>
        {
            isWatching = false;
            bool success = state == AdState.ShowSuccess;
            if (success)
                SaveReward();
            if (this == null)   // the popup closed during the ad: the reward still counts, only the fly effect is lost
            {
                if (success)
                    CurrencyManager.AddSoft(reward, "watch_ad", null, null, false);
                return;
            }
            if (success)
                CurrencyManager.AddSoft(reward, name, transform);
            Refresh();
        }, name);
    }

    private void SaveReward()
    {
        int count = PlayerPrefs.GetString(DayKey) == Today ? PlayerPrefs.GetInt(CountKey) : 0;
        PlayerPrefs.SetString(DayKey, Today);
        PlayerPrefs.SetInt(CountKey, count + 1);
        PlayerPrefs.SetString(NextKey, DateTime.UtcNow.AddMinutes(cooldownMinutes).Ticks.ToString());
    }
}
