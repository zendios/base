using System;
using System.Collections;
using Base.Ads;

/// <summary>
/// The interstitial of a game end, then the next step: shown only when AdConfig.adInterOnComplete is on, and only when
/// AdsManager says it is time (gap since the last ad, Remove Ads, games played...). The next step runs once the ad is
/// over (closed, failed or not shown), never while it is on screen.
///   GameEndAd.Show("game_end", () => GameStateManager.Main());
/// </summary>
public static class GameEndAd
{
    public static void Show(string where, Action then)
    {
        var config = AdConfig.Instance;
        if (config == null || !config.adInterOnComplete || !AdsManager.InstanceExists)
        {
            then();
            return;
        }
        UnityMainThreadDispatcher.Enqueue(ShowThen(where, then));
    }

    // IEShowInter ends when the interstitial is closed (or failed, or was not time): its callback also reports
    // LoadAvailable / ShowSuccess on the way, too early to go on.
    private static IEnumerator ShowThen(string where, Action then)
    {
        yield return AdsManager.IEShowInter(null, where);
        then();
    }
}
