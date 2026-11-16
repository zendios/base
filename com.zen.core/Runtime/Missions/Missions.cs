using System;
using Base.Ads;
using UnityEngine;

namespace Base
{
    /// <summary>
    /// The one place the game reports what the player did: it moves daily missions and achievements listening to that
    /// event. Base reports play / win / lose (GameStateManager), watch_ad (a rewarded ad shown), earn_soft (soft currency
    /// gained) and unlock_item (an item reward); the game reports its own keys (the eventKey of a MissionData).
    ///   Missions.Report("collect_star", stars);
    /// </summary>
    public static class Missions
    {
        public const string EventPlay = "play";
        public const string EventWin = "win";
        public const string EventLose = "lose";
        public const string EventWatchAd = "watch_ad";
        public const string EventEarnSoft = "earn_soft";
        public const string EventUnlockItem = "unlock_item";

        private static int lastSoft;

        /// <summary>A daily mission (false) or an achievement (true) just reached its target, even in the middle of a game:
        /// show a toast, play a sound. The reward is claimed later from its popup.</summary>
        public static event Action<MissionData, bool> OnCompleted;

        /// <summary>Adds amount to every daily mission and achievement listening to eventKey.</summary>
        public static void Report(string eventKey, int amount = 1)
        {
            if (amount <= 0 || string.IsNullOrEmpty(eventKey))
                return;
            DailyMissions.Add(eventKey, amount);
            Achievements.Add(eventKey, amount);
        }

        internal static void RaiseCompleted(MissionData mission, bool isAchievement) => OnCompleted?.Invoke(mission, isAchievement);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            DataManager.OnLoaded -= OnDataLoaded;
            DataManager.OnLoaded += OnDataLoaded;
            GameStateManager.OnGameStateChanged -= OnGameStateChanged;
            GameStateManager.OnGameStateChanged += OnGameStateChanged;
            AdBase.OnAnyStateChanged -= OnAdStateChanged;
            AdBase.OnAnyStateChanged += OnAdStateChanged;
            UserDataBase.OnSoftCurrencyChanged -= OnSoftCurrencyChanged;
            UserDataBase.OnSoftCurrencyChanged += OnSoftCurrencyChanged;
        }

        private static void OnDataLoaded() => lastSoft = CurrencyManager.TotalSoft;

        private static void OnGameStateChanged(GameState current, GameState last, object stateData)
        {
            switch (current)
            {
                case GameState.Play:
                    if (last != GameState.Pause && last != GameState.ReviveContinue && last != GameState.ReviveCheckPoint)
                        Report(EventPlay);   // resuming or reviving is the same game
                    break;
                case GameState.Win:
                    Report(EventWin);
                    break;
                case GameState.Lose:
                    Report(EventLose);
                    break;
                default:
                    break;
            }
        }

        private static void OnAdStateChanged(AdBase ad, AdState state)
        {
            if (state == AdState.ShowSuccess && (ad.type == AdType.Reward || ad.type == AdType.RewardInter))
                Report(EventWatchAd);
        }

        // The delegate gives the size of the change, not its sign: compare with the last total.
        private static void OnSoftCurrencyChanged(long changed, long total)
        {
            if (!DataManager.IsLoaded)
                return;
            if (total > lastSoft)
                Report(EventEarnSoft, (int)(total - lastSoft));
            lastSoft = (int)total;
        }

        // Statics survive Enter Play Mode without domain reload (DataManager resets its own the same way).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetStatics()
        {
            lastSoft = 0;
            OnCompleted = null;
        }
    }
}
