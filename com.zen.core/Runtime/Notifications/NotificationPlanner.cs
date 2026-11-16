using System;
using System.Collections.Generic;

namespace Base
{
    /// <summary>One local notification to schedule: type (a NotificationData.type), fire time, popup to open.</summary>
    public struct PlannedNotification
    {
        public string type;
        public DateTime fireTime;
        public string route;
    }

    /// <summary>
    /// The rules of the reminders, without any device call (NotificationManager schedules what it returns). Planned when
    /// the game goes to the background, so each one is a day after the last visit: today 20:00 when rewards wait,
    /// then day +1, +3 and +7 at the player's usual hour, never during 21:30–08:00, nothing after day +7.
    /// Game design: GAME_DESIGN_SAMPLE_DATAS.md section 14.
    /// </summary>
    public static class NotificationPlanner
    {
        public const string TypeDailyReady = "daily_ready";
        public const string TypeRewardsWaiting = "rewards_waiting";
        public const string TypeComebackD3 = "comeback_d3";
        public const string TypeComebackD7 = "comeback_d7";

        public const string RouteDailyLogin = "daily_login";
        public const string RouteMissions = "missions";

        public const int DefaultHour = 19;
        private const int FirstHour = 8;           // quiet hours end
        private const int LastHour = 21;           // quiet hours start at 21:30
        private const int RewardsWaitingHour = 20;
        private const int RewardsWaitingBefore = 19;
        private const int UsualHourSessions = 7;

        /// <summary>
        /// What to schedule when the game goes to the background at "now". loginWaiting: today's login reward is not
        /// claimed; claimableMissions: missions done but not claimed (lost at midnight).
        /// </summary>
        public static List<PlannedNotification> Plan(DateTime now, int usualHour, bool hasLogin, bool loginWaiting,
            bool hasMissions, int claimableMissions)
        {
            var plan = new List<PlannedNotification>();
            if (!hasLogin && !hasMissions)
                return plan;

            if ((loginWaiting || claimableMissions > 0) && now.Hour < RewardsWaitingBefore)
            {
                string waitingRoute = claimableMissions > 0 ? RouteMissions : RouteDailyLogin;
                plan.Add(Make(TypeRewardsWaiting, now.Date.AddHours(RewardsWaitingHour), waitingRoute));
            }
            string route = hasLogin ? RouteDailyLogin : RouteMissions;
            int hour = Math.Min(Math.Max(usualHour, FirstHour), LastHour);
            plan.Add(Make(TypeDailyReady, now.Date.AddDays(1).AddHours(hour), route));
            plan.Add(Make(TypeComebackD3, now.Date.AddDays(3).AddHours(hour), route));
            plan.Add(Make(TypeComebackD7, now.Date.AddDays(7).AddHours(hour), route));
            return plan;
        }

        /// <summary>Median start hour of the last sessions (at most 7), or DefaultHour without any.</summary>
        public static int UsualHour(IList<int> sessionHours)
        {
            if (sessionHours == null || sessionHours.Count == 0)
                return DefaultHour;
            var last = new List<int>();
            for (int i = Math.Max(0, sessionHours.Count - UsualHourSessions); i < sessionHours.Count; i++)
                last.Add(sessionHours[i]);
            last.Sort();
            return last[last.Count / 2];
        }

        /// <summary>
        /// The reminder offer shows after claiming day 1, 3 or 7 of the login calendar (claimedDay is 1-based), at most
        /// 3 times in all and once a day, while reminders are off and the OS has not refused for good.
        /// </summary>
        public static bool ShouldOffer(int claimedDay, int offersShown, bool offeredToday, bool remindersOn, bool osBlocked)
        {
            bool valueMoment = claimedDay == 1 || claimedDay == 3 || claimedDay == 7;
            return valueMoment && offersShown < 3 && !offeredToday && !remindersOn && !osBlocked;
        }

        private static PlannedNotification Make(string type, DateTime fireTime, string route) =>
            new PlannedNotification { type = type, fireTime = fireTime, route = route };
    }
}
