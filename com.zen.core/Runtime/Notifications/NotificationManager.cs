#if USE_NOTIFICATIONS && !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
#define NOTIFICATIONS_DEVICE
#endif
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if NOTIFICATIONS_DEVICE
using Unity.Notifications;
#if UNITY_ANDROID
using Unity.Notifications.Android;
#else
using AuthorizationStatus = Unity.Notifications.iOS.AuthorizationStatus;
using iOSNotificationCenter = Unity.Notifications.iOS.iOSNotificationCenter;
#endif
#endif

namespace Base
{
    /// <summary>
    /// Local reminders for Daily Login and Daily Missions (package com.unity.mobile.notifications; without it, or in
    /// the Editor, IsSupported is false and nothing is sent). The player turns them on from the offer shown after
    /// claiming login day 1, 3 or 7 (ShouldOffer), or from Settings. When the game goes to the background the
    /// NotificationPlanner rules are scheduled with the texts of NotificationDatas; back in the game they are cancelled.
    /// A tapped notification leaves a route ("daily_login", "missions") for the home screen: TakeRoute.
    /// State is saved in section "notifications"; Remote Config push_enabled = false turns everything off.
    ///   if (NotificationManager.ShouldOffer) showRemindRow();
    ///   StartCoroutine(NotificationManager.IETurnOn(on => ...));
    /// </summary>
    public static class NotificationManager
    {
        [Serializable]
        private class Data
        {
            public bool remindersOn;
            public int offersShown;
            public int lastOfferDay;       // yyyyMMdd
            public int osRequests;         // system dialogs shown (iOS shows 1, Android 2 at most)
            public int turn;               // picks the next text of each type
            public string lastTapped;      // data of the last tapped notification, so it is handled once
            public List<int> sessionHours = new List<int>();
        }

        private class Section : ISaveSection
        {
            public string SaveKey => "notifications";

            public string ToJson() => JsonUtility.ToJson(data);

            public void FromJson(string json) => JsonUtility.FromJsonOverwrite(json, data);
        }

        private const string RemoteEnabledKey = "push_enabled";
        private const string ChannelId = "reminders";
        private const int MaxSessionHours = 7;
        private const double NewSessionMinutes = 10;
        private const double ConvertedHours = 2;

        private static Data data = new Data();
        private static readonly Section section = new Section();
        private static bool enabled = true;
        private static string route;
        private static string openedType;
        private static string openedText;
        private static DateTime openedAt;
        private static DateTime pausedAt = DateTime.MinValue;
        private static int testSeconds;
#if NOTIFICATIONS_DEVICE
        private static bool initialized;
        private static QueryLastRespondedNotificationOp tappedQuery;
#endif

        /// <summary>The offer was answered, a setting changed or a notification was tapped: refresh the UI.</summary>
        public static event Action OnChanged;

        /// <summary>Reminders can be sent: the package is there, on a device, and Remote Config allows it. In the editor
        /// (package there) too, so the UI shows as on a device; nothing is posted there.</summary>
        public static bool IsSupported
        {
            get
            {
#if NOTIFICATIONS_DEVICE || (USE_NOTIFICATIONS && UNITY_EDITOR)
                return enabled;
#else
                return false;
#endif
            }
        }

        /// <summary>The player turned reminders on (the OS may still block them: IsAllowed).</summary>
        public static bool RemindersOn => data.remindersOn;

        /// <summary>The OS lets the game post notifications (always in the editor, which has no permission).</summary>
        public static bool IsAllowed
        {
            get
            {
#if NOTIFICATIONS_DEVICE
                Initialize();
#if UNITY_ANDROID
                return AndroidNotificationCenter.UserPermissionToPost == PermissionStatus.Allowed;
#else
                return iOSNotificationCenter.GetNotificationSettings().AuthorizationStatus == AuthorizationStatus.Authorized;
#endif
#elif USE_NOTIFICATIONS && UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>The OS will not show its dialog again: only its Settings can allow notifications.</summary>
        public static bool IsBlocked => !IsAllowed && data.osRequests >= (Application.platform == RuntimePlatform.IPhonePlayer ? 1 : 2);

        /// <summary>Show the reminder offer now: login day 1, 3 or 7 was just claimed (at most 3 offers, once a day).</summary>
        public static bool ShouldOffer => IsSupported && DailyLogin.ClaimedToday >= 0
            && NotificationPlanner.ShouldOffer(DailyLogin.ClaimedToday + 1, data.offersShown,
                data.lastOfferDay == DayKey(DateTime.Today), data.remindersOn, IsBlocked);

        /// <summary>The player said "Not now" to the offer.</summary>
        public static void Decline()
        {
            CountOffer();
            LogOffer("not_now");
            Save();
            OnChanged?.Invoke();
        }

        /// <summary>
        /// Turns reminders on: asks the OS when it can still ask, opens the app's notification settings when it no
        /// longer can (only call it from a player's tap). done(true) when notifications are allowed.
        /// </summary>
        public static IEnumerator IETurnOn(Action<bool> done)
        {
            if (ShouldOffer)
            {
                CountOffer();
                LogOffer("accept");
            }
            data.remindersOn = true;
#if NOTIFICATIONS_DEVICE
            if (!IsAllowed)
            {
                if (IsBlocked)
                {
                    NotificationCenter.OpenNotificationSettings();
                }
                else
                {
                    data.osRequests++;
                    var request = NotificationCenter.RequestPermission();
                    yield return request;
                    bool granted = request.Status == NotificationsPermissionStatus.Granted;
                    data.remindersOn = granted;   // refused: off, so a later offer (Android) can ask again
                    AnalyticsManager.LogEvent("push_permission", new Dictionary<string, object>
                    {
                        { "result", granted ? "granted" : "denied" }, { "request", data.osRequests },
                    });
                }
            }
#endif
            Save();
            OnChanged?.Invoke();
            done?.Invoke(IsAllowed);
            yield break;
        }

        /// <summary>Turns reminders off and cancels the scheduled ones.</summary>
        public static void TurnOff()
        {
            data.remindersOn = false;
            CancelAll();
            Save();
            OnChanged?.Invoke();
        }

        /// <summary>The popup a tapped notification asked for ("daily_login", "missions"), once, or null.</summary>
        public static string TakeRoute()
        {
            CheckTapped();
            string taken = route;
            route = null;
            return taken;
        }

        /// <summary>A debug tool: the next time the game goes to the background, a daily_ready (to the calendar) then a
        /// rewards_waiting (to the missions) are sent "seconds" apart instead of the real plan (reminders must be on and
        /// allowed). Two only: Android groups 4 notifications of an app, and a tapped group carries no route.</summary>
        public static void ScheduleTest(int seconds) => testSeconds = seconds;

        /// <summary>Forgets the offers, the OS requests count and the sessions (a debug tool).</summary>
        public static void ResetForDebug()
        {
            data = new Data();
            CancelAll();
            Save();
            OnChanged?.Invoke();
        }

        internal static int DayKey(DateTime date) => date.Year * 10000 + date.Month * 100 + date.Day;

        // Fills {day}, {reward} and {count}.
        internal static string Fill(string text, int day, string reward, int count) =>
            text.Replace("{day}", day.ToString()).Replace("{reward}", reward).Replace("{count}", count.ToString());

        private static void OnFocusChanged(bool focused)
        {
            if (!DataManager.IsLoaded)
                return;
            if (focused)
                OnResume();
            else
                OnPause();
        }

        private static void OnPause()
        {
            pausedAt = DateTime.Now;
            CancelAll();
            if (!IsSupported || !data.remindersOn || !IsAllowed)
            {
                if (testSeconds > 0)
                    Debug.LogWarning("[NotificationManager] test not sent: supported " + IsSupported + ", on " + data.remindersOn
                        + ", allowed " + IsAllowed);
                return;
            }
            int claimable = 0;
            foreach (var progress in DailyMissions.Today)
                if (progress.CanClaim)
                    claimable++;
            var plan = testSeconds > 0 ? TestPlan()
                : NotificationPlanner.Plan(DateTime.Now, NotificationPlanner.UsualHour(data.sessionHours),
                    DailyLogin.IsAvailable, DailyLogin.CanClaim, DailyMissions.IsAvailable, claimable);
            testSeconds = 0;
            int id = 1;
            Debug.Log("[NotificationManager] scheduled " + plan.Count + (plan.Count > 0 ? ", first " + plan[0].type + " at "
                + plan[0].fireTime.ToString("yyyy-MM-dd HH:mm:ss") : string.Empty));
            foreach (var planned in plan)
                Schedule(planned, id++);
            data.turn++;
            Save();
            AnalyticsManager.LogEvent("push_scheduled", new Dictionary<string, object>
            {
                { "count", plan.Count }, { "next_type", plan.Count > 0 ? plan[0].type : string.Empty },
                { "hour", NotificationPlanner.UsualHour(data.sessionHours) },
            });
        }

        private static void OnResume()
        {
            if ((DateTime.Now - pausedAt).TotalMinutes >= NewSessionMinutes)
                AddSessionHour();
            CancelAll();
            ReadTapped();
        }

        private static List<PlannedNotification> TestPlan()
        {
            var plan = new List<PlannedNotification>();
            foreach (var type in new[] { NotificationPlanner.TypeDailyReady, NotificationPlanner.TypeRewardsWaiting })
            {
                plan.Add(new PlannedNotification
                {
                    type = type, fireTime = DateTime.Now.AddSeconds(testSeconds * (plan.Count + 1)),
                    route = type == NotificationPlanner.TypeRewardsWaiting ? NotificationPlanner.RouteMissions
                        : NotificationPlanner.RouteDailyLogin,
                });
            }
            return plan;
        }

        private static void AddSessionHour()
        {
            data.sessionHours.Add(DateTime.Now.Hour);
            if (data.sessionHours.Count > MaxSessionHours)
                data.sessionHours.RemoveAt(0);
        }

        private static void Schedule(PlannedNotification planned, int id)
        {
#if NOTIFICATIONS_DEVICE
            var text = PickText(planned.type);
            if (text == null)
                return;
            Initialize();
            var next = DailyLogin.NextDay;
            int day = DailyLogin.DayIndex + 1;
            string reward = next != null ? Rewards.Describe(next.reward) : string.Empty;
            int count = 0;
            foreach (var progress in DailyMissions.Today)
                if (progress.CanClaim)
                    count++;
            if (DailyLogin.CanClaim)
                count++;
            var notification = new Notification
            {
                Identifier = id,
                Title = Fill(text.name, day, reward, count),
                Text = Fill(text.description, day, reward, count),
                // route|type|text id|fire time: the fire time makes each notification's data unique (handled once)
                Data = planned.route + "|" + planned.type + "|" + text.id + "|" + planned.fireTime.Ticks,
            };
            NotificationCenter.ScheduleNotification(notification, new NotificationDateTimeSchedule(planned.fireTime));
#endif
        }

        private static NotificationData PickText(string type)
        {
            var datas = DataManager.Get<NotificationDatas>();
            if (datas == null)
                return null;
            var texts = datas.list.FindAll(t => t.type == type);
            return texts.Count == 0 ? null : texts[data.turn % texts.Count];
        }

        private static void ReadTapped()
        {
#if NOTIFICATIONS_DEVICE
            Initialize();
            tappedQuery = NotificationCenter.QueryLastRespondedNotification();
#endif
            CheckTapped();
        }

        // The query answers at once on Android, a few frames later on iOS: checked again by TakeRoute.
        private static void CheckTapped()
        {
#if NOTIFICATIONS_DEVICE
            if (tappedQuery == null || tappedQuery.State == QueryLastRespondedNotificationState.Pending)
                return;
            var query = tappedQuery;
            tappedQuery = null;
            if (query.State != QueryLastRespondedNotificationState.HaveRespondedNotification)
                return;
            string tapped = query.Notification.Data;
            if (string.IsNullOrEmpty(tapped) || tapped == data.lastTapped)
                return;
            data.lastTapped = tapped;
            var parts = tapped.Split('|');
            if (parts.Length < 4)
                return;
            route = parts[0];
            Debug.Log("[NotificationManager] opened from " + parts[1] + ", route " + route);
            openedType = parts[1];
            openedText = parts[2];
            openedAt = DateTime.Now;
            var firedAt = new DateTime(long.Parse(parts[3]));
            AnalyticsManager.LogEvent("push_open", new Dictionary<string, object>
            {
                { "type", openedType }, { "text", openedText }, { "delay_min", (int)(openedAt - firedAt).TotalMinutes },
            });
            Save();
            OnChanged?.Invoke();
#endif
        }

        // A claim or a mission progress within 2 hours of opening a notification.
        private static void OnActivity()
        {
            if (openedType == null || (DateTime.Now - openedAt).TotalHours > ConvertedHours)
                return;
            AnalyticsManager.LogEvent("push_converted", new Dictionary<string, object>
            {
                { "type", openedType }, { "text", openedText },
            });
            openedType = null;
        }

        private static void CountOffer()
        {
            data.offersShown++;
            data.lastOfferDay = DayKey(DateTime.Today);
        }

        private static void LogOffer(string result) =>
            AnalyticsManager.LogEvent("push_prompt", new Dictionary<string, object>
            {
                { "day", DailyLogin.ClaimedToday + 1 }, { "result", result },
            });

        private static void CancelAll()
        {
#if NOTIFICATIONS_DEVICE
            Initialize();
            NotificationCenter.CancelAllScheduledNotifications();
            NotificationCenter.CancelAllDeliveredNotifications();
#endif
        }

        private static void Initialize()
        {
#if NOTIFICATIONS_DEVICE
            if (initialized)
                return;
            initialized = true;
            NotificationCenter.Initialize(new NotificationCenterArgs
            {
                AndroidChannelId = ChannelId,
                PresentationOptions = NotificationPresentation.Alert | NotificationPresentation.Badge
                    | NotificationPresentation.Sound,
            });
#if UNITY_ANDROID
            // Default importance (no heads-up banner): created here because NotificationCenter makes Alert channels High.
            AndroidNotificationCenter.RegisterNotificationChannel(new AndroidNotificationChannel(ChannelId, "Reminders",
                "Daily reward and mission reminders", Importance.Default));
#endif
#endif
        }

        private static void Save()
        {
            if (DataManager.IsLoaded)
                DataManager.Save(true);
        }

        private static void OnLoaded()
        {
            AddSessionHour();
            CancelAll();
            ReadTapped();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            DataManager.RegisterSection(section);
            DataManager.OnLoaded -= OnLoaded;
            DataManager.OnLoaded += OnLoaded;
            Application.focusChanged -= OnFocusChanged;
            Application.focusChanged += OnFocusChanged;
            DailyLogin.OnClaimed -= OnClaimed;
            DailyLogin.OnClaimed += OnClaimed;
            DailyMissions.OnChanged -= OnActivity;
            DailyMissions.OnChanged += OnActivity;
            RemoteConfigManager.OnCollectDefaults -= AddRemoteDefaults;
            RemoteConfigManager.OnCollectDefaults += AddRemoteDefaults;
            RemoteConfigManager.OnFetched -= ApplyRemote;
            RemoteConfigManager.OnFetched += ApplyRemote;
        }

        private static void OnClaimed(int day, DailyLoginData reward, bool doubled) => OnActivity();

        private static void AddRemoteDefaults(Dictionary<string, object> defaults) => defaults[RemoteEnabledKey] = true;

        private static void ApplyRemote()
        {
            enabled = RemoteConfigManager.GetBool(RemoteEnabledKey, true);
            if (!enabled)
                CancelAll();
        }

        // Statics survive Enter Play Mode without domain reload (DataManager resets its own the same way).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetStatics()
        {
            data = new Data();
            enabled = true;
            route = null;
            openedType = null;
            openedText = null;
            openedAt = DateTime.MinValue;
            pausedAt = DateTime.MinValue;
            testSeconds = 0;
            OnChanged = null;
        }
    }
}
