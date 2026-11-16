using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Base
{
    /// <summary>
    /// Remote Config (Firebase, through FirebaseManager): the top layer, over the game's settings over Base's defaults.
    /// Each part of Base adds its keys and defaults in OnCollectDefaults and reads its values in OnFetched (GameConfig,
    /// AdConfig, native timing, daily login, missions...). Without Firebase the getters return the given defaults.
    /// </summary>
    public static class RemoteConfigManager
    {
        /// <summary>Called by Init: add your keys with their current (default) values.</summary>
        public static event Action<Dictionary<string, object>> OnCollectDefaults;

        /// <summary>Called after a fetch that activated new values: read your keys here.</summary>
        public static event Action OnFetched;

        public static IEnumerator Init()
        {
            var defaults = new Dictionary<string, object>();
            OnCollectDefaults?.Invoke(defaults);
            yield return FirebaseManager.DoCheckStatus(defaults);
        }

        /// <summary>Fetches; OnFetched runs when new values are active (also if that happens after a timeout).</summary>
        public static IEnumerator Fetch()
        {
            yield return FirebaseManager.DoFetchRemoteData(status =>
            {
                if (status != FirebaseStatus.Success)
                    return;
                if (OnFetched != null)
                {
                    foreach (Action handler in OnFetched.GetInvocationList())
                    {
                        try { handler(); }
                        catch (Exception ex) { Debug.LogException(ex); }
                    }
                }
                FirebaseManager.LogStatus();
            });
        }

        public static string GetString(string key, string defaultValue) => FirebaseManager.RemoteGetValueString(key, defaultValue);
        public static int GetInt(string key, int defaultValue) => FirebaseManager.RemoteGetValueInt(key, defaultValue);
        public static bool GetBool(string key, bool defaultValue) => FirebaseManager.RemoteGetValueBoolean(key, defaultValue);
        public static float GetFloat(string key, float defaultValue) => FirebaseManager.RemoteGetValueFloat(key, defaultValue);
    }
}
