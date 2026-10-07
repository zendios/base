#if USE_FIREBASE
using Firebase.Analytics;
#endif
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Base
{
    /// <summary>Firebase as the analytics and Remote Config backend of every Zen package (set before the first scene).</summary>
    internal class FirebaseZenBackend : IZenAnalytics, IZenRemote
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            var backend = new FirebaseZenBackend();
            ZenAnalytics.Backend = backend;
            ZenRemote.Backend = backend;
        }

        public bool IsReady => FirebaseManager.AnalyticStatus == FirebaseStatus.Initialized;

        public void LogEvent(string eventName, Dictionary<string, object> parameters) => FirebaseManager.LogEvent(eventName, parameters);

        public void LogEventExact(string eventName, Dictionary<string, object> parameters)
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

        public void SetUser(string property, object value) => FirebaseManager.SetUser(property, value);

        public void LogIAP(string screen, string productId, IAPStatus status, string localizedPrice, string isoCurrencyCode) =>
            FirebaseManager.LogIAP(screen, productId, status, localizedPrice, isoCurrencyCode);

        public void LogResourceEarn(int value, string currencyName, CurrencyType currencyType, string placement) =>
            FirebaseManager.LogResourceEarn(value, currencyName, currencyType, placement);

        public void LogResourceSpend(int value, string currencyName, CurrencyType currencyType, string placement) =>
            FirebaseManager.LogResourceSpend(value, currencyName, currencyType, placement);

        public IEnumerator Init(Dictionary<string, object> defaults) => FirebaseManager.DoCheckStatus(defaults);

        public IEnumerator Fetch(Action<bool> done)
        {
            yield return FirebaseManager.DoFetchRemoteData(status =>
            {
                bool ok = status == FirebaseStatus.Success;
                done(ok);
                if (ok)
                    FirebaseManager.LogStatus();
            });
        }

        public string GetString(string key, string defaultValue) => FirebaseManager.RemoteGetValueString(key, defaultValue);
        public int GetInt(string key, int defaultValue) => FirebaseManager.RemoteGetValueInt(key, defaultValue);
        public bool GetBool(string key, bool defaultValue) => FirebaseManager.RemoteGetValueBoolean(key, defaultValue);
        public float GetFloat(string key, float defaultValue) => FirebaseManager.RemoteGetValueFloat(key, defaultValue);
    }
}
