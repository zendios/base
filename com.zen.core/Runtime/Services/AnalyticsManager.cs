#if USE_FIREBASE
using Firebase.Analytics;
#endif
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Base
{
    /// <summary>
    /// Analytics events of Base and the game, sent to Firebase Analytics (FirebaseManager). Without Firebase they do
    /// nothing, so a game without it still compiles and runs.
    /// </summary>
    public static class AnalyticsManager
    {
        /// <summary>True when events are recorded now (Firebase Analytics initialized).</summary>
        public static bool IsReady => FirebaseManager.AnalyticStatus == FirebaseStatus.Initialized;

        public static void LogEvent(string eventName, Dictionary<string, object> parameters = null) =>
            FirebaseManager.LogEvent(eventName, parameters);

        /// <summary>
        /// Sends the event as given: no name / value formatting, no "session" added, no readiness check (revenue
        /// events). Values: string, int / long, float / double.
        /// </summary>
        public static void LogEventExact(string eventName, Dictionary<string, object> parameters)
        {
#if USE_FIREBASE
            try
            {
                var list = (parameters ?? new Dictionary<string, object>()).Where(x => x.Key != null && x.Value != null).Select(x =>
                {
                    switch (x.Value)
                    {
                        case string s: return new Parameter(x.Key, s);
                        case int i: return new Parameter(x.Key, i);
                        case long l: return new Parameter(x.Key, l);
                        case float f: return new Parameter(x.Key, f);
                        case double d: return new Parameter(x.Key, d);
                        default: return new Parameter(x.Key, x.Value.ToString());
                    }
                }).ToArray();
                FirebaseAnalytics.LogEvent(eventName, list);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
#endif
        }

        public static void SetUser(string property, object value) => FirebaseManager.SetUser(property, value);

        public static void LogIAP(string screen, string productId, IAPStatus status, string localizedPrice, string isoCurrencyCode) =>
            FirebaseManager.LogIAP(screen, productId, status, localizedPrice, isoCurrencyCode);

        public static void LogResourceEarn(int value, string currencyName, CurrencyType currencyType, string placement) =>
            FirebaseManager.LogResourceEarn(value, currencyName, currencyType, placement);

        public static void LogResourceSpend(int value, string currencyName, CurrencyType currencyType, string placement) =>
            FirebaseManager.LogResourceSpend(value, currencyName, currencyType, placement);
    }
}
