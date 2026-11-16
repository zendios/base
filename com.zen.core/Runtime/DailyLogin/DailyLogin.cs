using System;
using System.Collections.Generic;
using Base.Ads;
using UnityEngine;

namespace Base
{
    /// <summary>
    /// Daily login reward: one reward per calendar day (device time) along the days of DailyLoginDatas, starting again
    /// after the last day; moving the device clock back opens nothing. Progress is saved in the save file (section
    /// "daily_login", registered by itself). Needs a DailyLoginDatas in DataManager.itemDatas; without it, or with Remote
    /// Config dailyLogin_enabled = false, IsAvailable is false. The UI is the Racing sample (UIPopupDailyLogin).
    ///   if (DailyLogin.CanClaim) UIPopupDailyLogin.Show();
    ///   DailyLogin.Claim(claimButton.transform);          // or ClaimDouble: after a rewarded ad
    /// </summary>
    public static class DailyLogin
    {
        [Serializable]
        private class Data
        {
            public int lastClaimDay;   // yyyyMMdd of the last claim, 0 = never
            public int dayIndex;       // index in Datas.list of the next reward
            public int cycle;          // how many times the calendar was completed
        }

        private class Section : ISaveSection
        {
            public string SaveKey => "daily_login";

            public string ToJson() => JsonUtility.ToJson(data);

            public void FromJson(string json) => JsonUtility.FromJsonOverwrite(json, data);
        }

        private const string RemoteEnabledKey = "dailyLogin_enabled";

        private static Data data = new Data();
        private static readonly Section section = new Section();
        private static DailyLoginDatas datasForTests;
        private static bool enabled = true;

        /// <summary>A reward was granted: day index (0-based), the reward, doubled by an ad or not.</summary>
        public static event Action<int, DailyLoginData, bool> OnClaimed;

        /// <summary>The DailyLoginDatas of DataManager.itemDatas, or null.</summary>
        public static DailyLoginDatas Datas => datasForTests != null ? datasForTests : DataManager.Get<DailyLoginDatas>();

        /// <summary>There is a calendar and Remote Config has not turned the feature off.</summary>
        public static bool IsAvailable => enabled && Datas != null && Datas.list.Count > 0;

        /// <summary>Index in Datas.list of the next reward (the one to claim today when CanClaim).</summary>
        public static int DayIndex => data.dayIndex;

        /// <summary>How many times the calendar was completed.</summary>
        public static int Cycle => data.cycle;

        /// <summary>No reward was claimed today yet.</summary>
        public static bool CanClaim => CanClaimOn(DateTime.Today);

        /// <summary>Index of the reward claimed today, or -1.</summary>
        public static int ClaimedToday => ClaimedTodayOn(DateTime.Today);

        /// <summary>The day of the next reward (today's when CanClaim), or null when not available.</summary>
        public static DailyLoginData NextDay => IsAvailable ? Datas.list[data.dayIndex % Datas.list.Count] : null;

        /// <summary>Today's reward can be claimed doubled for a rewarded ad: adDouble on and a coin or booster reward.</summary>
        public static bool CanClaimDouble => CanClaimDoubleOn(DateTime.Today);

        /// <summary>Days of the current calendar already claimed: the cells before the next one (all of them right
        /// after the last day's claim).</summary>
        public static int ClaimedCount
        {
            get
            {
                if (!IsAvailable)
                    return 0;
                if (data.dayIndex == 0 && ClaimedToday == Datas.list.Count - 1)
                    return Datas.list.Count;
                return data.dayIndex;
            }
        }

        /// <summary>Grants today's reward (Rewards.Grant, with the fly FX from "from") and saves. False when nothing
        /// can be claimed.</summary>
        public static bool Claim(Transform from) => Grant(from, false);

        /// <summary>Shows a rewarded ad, then grants today's reward twice. When the ad does not show, nothing is
        /// claimed (the player can still Claim). Does nothing unless CanClaimDouble.</summary>
        public static void ClaimDouble(Transform from)
        {
            if (!CanClaimDouble)
                return;
            AdsManager.ShowReward((type, state) =>
            {
                if (state == AdState.ShowSuccess)
                    Grant(from, true);
                else
                    AdToast.ShowNotice("No ad right now, claim the normal reward.");
            }, "daily_login_x2");
        }

        /// <summary>Moves the last claim to another day (a debug tool: yesterday makes today claimable again).</summary>
        public static void SetLastClaim(DateTime day) => data.lastClaimDay = DayKey(day);

        // Only a later day opens a claim: moving the device clock back and forth must not grant a reward each time.
        internal static bool CanClaimOn(DateTime today) => IsAvailable && DayKey(today) > data.lastClaimDay;

        internal static bool CanClaimDoubleOn(DateTime today) =>
            CanClaimOn(today) && Datas.adDouble && NextDay.reward.CanDouble;

        internal static int ClaimedTodayOn(DateTime today)
        {
            if (!IsAvailable || data.lastClaimDay != DayKey(today))
                return -1;
            int count = Datas.list.Count;
            return (data.dayIndex - 1 + count) % count;
        }

        /// <summary>Marks today claimed and returns the index of the reward, or -1. A day missed with resetOnMiss
        /// starts the calendar again.</summary>
        internal static int ClaimOn(DateTime today)
        {
            if (!CanClaimOn(today))
                return -1;
            int count = Datas.list.Count;
            if (Datas.resetOnMiss && data.lastClaimDay != 0 && data.lastClaimDay < DayKey(today.AddDays(-1)))
                data.dayIndex = 0;
            int index = data.dayIndex % count;
            data.lastClaimDay = DayKey(today);
            data.dayIndex = index + 1;
            if (data.dayIndex >= count)
            {
                data.dayIndex = 0;
                data.cycle++;
            }
            return index;
        }

        internal static int DayKey(DateTime date) => date.Year * 10000 + date.Month * 100 + date.Day;

        internal static void SetDatasForTests(DailyLoginDatas value) => datasForTests = value;

        private static bool Grant(Transform from, bool doubled)
        {
            int index = ClaimOn(DateTime.Today);
            if (index < 0)
                return false;
            var day = Datas.list[index];
            int times = doubled ? 2 : 1;
            if (!Rewards.Grant(day.reward, "daily_login", from, times) && DataManager.IsLoaded)
                DataManager.Save(true);   // a bad reward id still uses up the day: keep the claim
            AnalyticsManager.LogEvent("daily_login_claim", new Dictionary<string, object>
            {
                { "day", index + 1 }, { "cycle", data.cycle }, { "type", day.reward.type.ToString() },
                { "id", day.reward.id ?? string.Empty }, { "amount", day.reward.amount * times }, { "doubled", doubled },
            });
            OnClaimed?.Invoke(index, day, doubled);
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            DataManager.RegisterSection(section);
            RemoteConfigManager.OnCollectDefaults -= AddRemoteDefaults;
            RemoteConfigManager.OnCollectDefaults += AddRemoteDefaults;
            RemoteConfigManager.OnFetched -= ApplyRemote;
            RemoteConfigManager.OnFetched += ApplyRemote;
        }

        private static void AddRemoteDefaults(Dictionary<string, object> defaults) => defaults[RemoteEnabledKey] = true;

        private static void ApplyRemote() => enabled = RemoteConfigManager.GetBool(RemoteEnabledKey, true);

        // Statics survive Enter Play Mode without domain reload (DataManager resets its own the same way).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetStatics()
        {
            data = new Data();
            datasForTests = null;
            enabled = true;
            OnClaimed = null;
        }
    }
}
