using System;
using System.Collections.Generic;
using UnityEngine;

namespace Base
{
    /// <summary>
    /// Achievements: lifetime goals from the AchievementDatas of DataManager.itemDatas. Progress is one lifetime counter
    /// per event key (fed by Missions.Report), so an achievement added by an update counts what the player already did.
    /// Each group shows its lowest unclaimed tier. Counters and claims are saved in section "achievement" (registered by
    /// itself). Without the datas, or with Remote Config achievement_enabled = false, IsAvailable is false. The UI is the
    /// Racing sample (UIPopupAchievements).
    ///   foreach (var achievement in Achievements.Visible) row.Set(achievement, Achievements.Progress(achievement)...);
    ///   Achievements.Claim(achievement.id, claimButton.transform);
    /// </summary>
    public static class Achievements
    {
        [Serializable]
        private class Counter
        {
            public string eventKey;
            public long value;
        }

        [Serializable]
        private class Data
        {
            public List<Counter> counters = new List<Counter>();
            public List<string> claimed = new List<string>();
        }

        private class Section : ISaveSection
        {
            public string SaveKey => "achievement";

            public string ToJson() => JsonUtility.ToJson(data);

            public void FromJson(string json) => JsonUtility.FromJsonOverwrite(json, data);
        }

        private const string RemoteEnabledKey = "achievement_enabled";

        private static Data data = new Data();
        private static readonly Section section = new Section();
        private static readonly List<MissionData> visible = new List<MissionData>();
        private static AchievementDatas datasForTests;
        private static bool enabled = true;

        /// <summary>Progress moved on an achievement or a reward was claimed: refresh the list.</summary>
        public static event Action OnChanged;

        /// <summary>The AchievementDatas of DataManager.itemDatas, or null.</summary>
        public static AchievementDatas Datas => datasForTests != null ? datasForTests : DataManager.Get<AchievementDatas>();

        /// <summary>There are achievements and Remote Config has not turned the feature off.</summary>
        public static bool IsAvailable => enabled && Datas != null && Datas.list.Count > 0;

        /// <summary>One achievement per group, in the order of the datas: the lowest unclaimed tier, or the last tier
        /// once all are claimed. Empty when not available. The list is reused: read it, do not keep it.</summary>
        public static IReadOnlyList<MissionData> Visible
        {
            get
            {
                visible.Clear();
                if (!IsAvailable)
                    return visible;
                foreach (var achievement in Datas.list)
                {
                    int shown = visible.FindIndex(a => GroupOf(a) == GroupOf(achievement));
                    if (shown < 0)
                        visible.Add(achievement);
                    else if (IsClaimed(visible[shown]) && achievement.tier > visible[shown].tier)
                        visible[shown] = achievement;
                    else if (!IsClaimed(achievement) && achievement.tier < visible[shown].tier)
                        visible[shown] = achievement;
                }
                return visible;
            }
        }

        /// <summary>An achievement is done and its reward not claimed yet.</summary>
        public static bool HasClaimable
        {
            get
            {
                if (!IsAvailable)
                    return false;
                foreach (var achievement in Datas.list)
                    if (CanClaim(achievement))
                        return true;
                return false;
            }
        }

        /// <summary>Progress toward the target, capped at the target.</summary>
        public static int Progress(MissionData achievement) =>
            (int)Math.Min(CounterOf(achievement.eventKey)?.value ?? 0, achievement.target);

        /// <summary>The reward of this achievement was claimed.</summary>
        public static bool IsClaimed(MissionData achievement) => data.claimed.Contains(achievement.id);

        /// <summary>Done and not claimed yet.</summary>
        public static bool CanClaim(MissionData achievement) =>
            !IsClaimed(achievement) && Progress(achievement) >= achievement.target;

        /// <summary>Grants the reward of a done achievement (Rewards.Grant, with the fly FX from "from") and saves.
        /// False when it is not done, already claimed or not in the datas.</summary>
        public static bool Claim(string id, Transform from)
        {
            var achievement = IsAvailable ? Datas.Find(id) : null;
            if (achievement == null || !CanClaim(achievement))
                return false;
            data.claimed.Add(id);
            if (!Rewards.Grant(achievement.reward, "achievement", from))
                Save();   // a bad reward id still uses up the achievement: keep the claim
            AnalyticsManager.LogEvent("achievement_claim", new Dictionary<string, object>
            {
                { "id", id }, { "group", GroupOf(achievement) }, { "tier", achievement.tier },
                { "type", achievement.reward.type.ToString() }, { "amount", achievement.reward.amount },
            });
            OnChanged?.Invoke();
            return true;
        }

        /// <summary>Adds amount to the lifetime counter of eventKey (from Missions.Report). Every key is counted, even
        /// with no achievement on it yet, so a later update can add one.</summary>
        internal static void Add(string eventKey, int amount)
        {
            var counter = CounterOf(eventKey);
            if (counter == null)
            {
                counter = new Counter { eventKey = eventKey };
                data.counters.Add(counter);
            }
            long before = counter.value;
            counter.value += amount;
            if (!IsAvailable)
                return;
            bool listened = false;
            bool done = false;
            foreach (var achievement in Datas.list)
            {
                if (achievement.eventKey != eventKey)
                    continue;
                listened = true;
                if (before < achievement.target && counter.value >= achievement.target)
                {
                    done = true;
                    Missions.RaiseCompleted(achievement, true);
                }
            }
            if (done)
                Save();   // a reward is waiting: not lost if the game is killed
            if (listened)
                OnChanged?.Invoke();
        }

        internal static void SetDatasForTests(AchievementDatas value) => datasForTests = value;

        internal static string ToJsonForTests() => section.ToJson();

        internal static void FromJsonForTests(string json) => section.FromJson(json);

        // An achievement without a group is a group of its own.
        private static string GroupOf(MissionData achievement) =>
            string.IsNullOrEmpty(achievement.group) ? achievement.id : achievement.group;

        private static Counter CounterOf(string eventKey) => data.counters.Find(c => c.eventKey == eventKey);

        private static void Save()
        {
            if (DataManager.IsLoaded)
                DataManager.Save(true);
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
            OnChanged = null;
        }
    }
}
