using UnityEngine;

namespace Base
{
    /// <summary>
    /// Counts games in UserData from GameStateManager: Play (a new game, not a resume or revive) → totalPlay, Win →
    /// totalWin and winStreak, Lose → totalLose and loseStreak. adInterOnPlay, InAppReview and analytics read them.
    /// A game that counts by itself turns this off before the first game:
    ///   [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    ///   static void Setup() => GameCounters.IsEnabled = false;
    /// </summary>
    public static class GameCounters
    {
        /// <summary>Base counts games (default). Off: the game increments the counters itself.</summary>
        public static bool IsEnabled { get; set; } = true;

        /// <summary>Adds the state change to the counters. False when it is not a counted change.</summary>
        internal static bool Count(UserDataBase user, GameState current, GameState last)
        {
            switch (current)
            {
                case GameState.Play:
                    if (last == GameState.Pause || last == GameState.ReviveContinue || last == GameState.ReviveCheckPoint)
                        return false;   // resuming or reviving is the same game
                    user.totalPlay++;
                    return true;
                case GameState.Win:
                    user.totalWin++;
                    user.winStreak++;
                    return true;
                case GameState.Lose:
                    user.totalLose++;
                    user.loseStreak++;
                    return true;
                default:
                    return false;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            GameStateManager.OnGameStateChanged -= OnGameStateChanged;
            GameStateManager.OnGameStateChanged += OnGameStateChanged;
        }

        private static void OnGameStateChanged(GameState current, GameState last, object data)
        {
            if (IsEnabled && DataManager.IsLoaded && DataManager.UserData != null)
                Count(DataManager.UserData, current, last);
        }

        // Statics survive Enter Play Mode without domain reload (DataManager resets its own the same way).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetStatics() => IsEnabled = true;
    }
}
