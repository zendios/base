using System;
using System.Collections.Generic;
using UnityEngine;

namespace Base
{
    /// <summary>Progress of one of today's missions (saved: id, value, claimed; the mission comes from DailyMissionDatas).</summary>
    [Serializable]
    public class DailyMissionProgress
    {
        public string id;
        public int value;
        public bool claimed;
        [NonSerialized] public MissionData mission;

        public bool IsDone => value >= mission.target;
        public bool CanClaim => IsDone && !claimed;
    }

    /// <summary>
    /// Daily missions: each calendar day (device time) missionsPerDay missions from the DailyMissionDatas of
    /// DataManager.itemDatas, progress and claims saved in section "daily_mission" (registered by itself) and reset on
    /// the first read of a later day (moving the clock back keeps the list). Progress comes from Missions.Report. Without
    /// the datas, or with Remote Config dailyMission_enabled = false, IsAvailable is false. The UI is the Features
    /// sample (UIPopupDailyMission).
    ///   if (DailyMissions.HasClaimable) UIPopupDailyMission.Show();
    ///   DailyMissions.Claim(progress.id, claimButton.transform);
    /// </summary>
    public static class DailyMissions
    {
        [Serializable]
        private class Data
        {
            public int dateKey;        // yyyyMMdd of the day the list is for, 0 = never
            public int rotation;       // index in Datas.list where the next day's list starts (pickRandom off)
            public List<DailyMissionProgress> missions = new List<DailyMissionProgress>();
        }

        private class Section : ISaveSection
        {
            public string SaveKey => "daily_mission";

            public string ToJson() => JsonUtility.ToJson(data);

            public void FromJson(string json)
            {
                JsonUtility.FromJsonOverwrite(json, data);
                Resolve();
            }
        }

        private const string RemoteEnabledKey = "dailyMission_enabled";

        private static Data data = new Data();
        private static readonly Section section = new Section();
        private static readonly List<DailyMissionProgress> none = new List<DailyMissionProgress>();
        private static DailyMissionDatas datasForTests;
        private static bool enabled = true;
        internal static Func<DateTime> todayProvider = () => DateTime.Today;

        /// <summary>Progress moved or a reward was claimed: refresh the list. A new day is started silently by the
        /// first read after midnight (the sample popup re-reads on its countdown).</summary>
        public static event Action OnChanged;

        /// <summary>The DailyMissionDatas of DataManager.itemDatas, or null.</summary>
        public static DailyMissionDatas Datas => datasForTests != null ? datasForTests : DataManager.Get<DailyMissionDatas>();

        /// <summary>There are missions and Remote Config has not turned the feature off.</summary>
        public static bool IsAvailable => enabled && Datas != null && Datas.list.Count > 0;

        /// <summary>Today's missions with their progress (empty when not available).</summary>
        public static IReadOnlyList<DailyMissionProgress> Today
        {
            get
            {
                if (!IsAvailable)
                    return none;
                EnsureToday();
                return data.missions;
            }
        }

        /// <summary>A mission is done and its reward not claimed yet.</summary>
        public static bool HasClaimable
        {
            get
            {
                foreach (var progress in Today)
                    if (progress.CanClaim)
                        return true;
                return false;
            }
        }

        /// <summary>Midnight (device time): the list changes then.</summary>
        public static DateTime NextReset => todayProvider().AddDays(1);

        /// <summary>Grants the reward of a done mission (Rewards.Grant, with the fly FX from "from") and saves. False
        /// when the mission is not done or already claimed.</summary>
        public static bool Claim(string id, Transform from)
        {
            var mission = MarkClaimed(id);
            if (mission == null)
                return false;
            if (!Rewards.Grant(mission.reward, "daily_mission", from))
                Save();   // a bad reward id still uses up the mission: keep the claim
            AnalyticsManager.LogEvent("daily_mission_claim", new Dictionary<string, object>
            {
                { "id", mission.id }, { "event", mission.eventKey }, { "target", mission.target },
                { "type", mission.reward.type.ToString() }, { "amount", mission.reward.amount },
            });
            OnChanged?.Invoke();
            return true;
        }

        /// <summary>Forgets today's list: the next read starts a new day (a debug tool).</summary>
        public static void ResetToday()
        {
            data.dateKey = 0;
            if (!IsAvailable)
                return;
            EnsureToday();
            Save();
            OnChanged?.Invoke();
        }

        /// <summary>Adds amount to every unclaimed mission of today listening to eventKey (from Missions.Report).</summary>
        internal static void Add(string eventKey, int amount)
        {
            bool changed = false;
            bool done = false;
            foreach (var progress in Today)
            {
                if (progress.mission.eventKey != eventKey || progress.IsDone)
                    continue;
                progress.value = Mathf.Min(progress.value + amount, progress.mission.target);
                changed = true;
                if (progress.IsDone)
                {
                    done = true;
                    Missions.RaiseCompleted(progress.mission, false);
                }
            }
            if (!changed)
                return;
            if (done)
                Save();   // a reward is waiting: not lost if the game is killed
            OnChanged?.Invoke();
        }

        /// <summary>Marks a done mission claimed and returns it, or null.</summary>
        internal static MissionData MarkClaimed(string id)
        {
            foreach (var progress in Today)
            {
                if (progress.id != id || !progress.CanClaim)
                    continue;
                progress.claimed = true;
                return progress.mission;
            }
            return null;
        }

        internal static int DayKey(DateTime date) => date.Year * 10000 + date.Month * 100 + date.Day;

        internal static void SetDatasForTests(DailyMissionDatas value) => datasForTests = value;

        internal static string ToJsonForTests() => section.ToJson();

        internal static void FromJsonForTests(string json) => section.FromJson(json);

        // Starts a new list when the saved one is for an earlier day (or none). A device clock moved back keeps the
        // saved list, so switching the date back and forth does not deal new missions. Only called when IsAvailable.
        private static void EnsureToday()
        {
            int key = DayKey(todayProvider());
            if (key <= data.dateKey && data.missions.Count > 0)
                return;
            data.dateKey = key;
            data.missions.Clear();
            var all = Datas.list;
            int count = Datas.missionsPerDay <= 0 ? all.Count : Mathf.Min(Datas.missionsPerDay, all.Count);
            if (Datas.pickRandom)
            {
                var order = new List<int>();
                for (int i = 0; i < all.Count; i++)
                    order.Add(i);
                var random = new System.Random(key);   // the same list on every device for the same day
                for (int i = 0; i < count; i++)
                {
                    int j = i + random.Next(order.Count - i);
                    (order[i], order[j]) = (order[j], order[i]);
                    data.missions.Add(new DailyMissionProgress { id = all[order[i]].id, mission = all[order[i]] });
                }
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    var mission = all[(data.rotation + i) % all.Count];
                    data.missions.Add(new DailyMissionProgress { id = mission.id, mission = mission });
                }
                data.rotation = (data.rotation + count) % all.Count;
            }
        }

        // Links the loaded progress to the datas; a mission no longer in the datas is dropped.
        private static void Resolve()
        {
            if (Datas == null)
            {
                data.missions.Clear();
                return;
            }
            for (int i = data.missions.Count - 1; i >= 0; i--)
            {
                var progress = data.missions[i];
                progress.mission = Datas.Find(progress.id);
                if (progress.mission == null)
                    data.missions.RemoveAt(i);
            }
        }

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
            todayProvider = () => DateTime.Today;
            OnChanged = null;
        }
    }
}
