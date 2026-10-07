using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Base
{
    /// <summary>Remote Config backend; com.zen.firebase registers Firebase Remote Config.</summary>
    public interface IZenRemote
    {
        IEnumerator Init(Dictionary<string, object> defaults);
        /// <summary>Fetches and activates; calls done(true) when new values are active.</summary>
        IEnumerator Fetch(Action<bool> done);
        string GetString(string key, string defaultValue);
        int GetInt(string key, int defaultValue);
        bool GetBool(string key, bool defaultValue);
        float GetFloat(string key, float defaultValue);
    }

    /// <summary>
    /// Remote Config for every Zen package (top layer: Remote over the game's settings over the package defaults).
    /// Each package adds its keys and defaults in OnCollectDefaults and reads its values in OnFetched (core:
    /// GameConfig, ads: AdConfig and native timing). Without a backend Init / Fetch do nothing and the getters return
    /// the given defaults.
    /// </summary>
    public static class ZenRemote
    {
        public static IZenRemote Backend { get; set; }

        /// <summary>Called by Init: add your keys with their current (default) values.</summary>
        public static event Action<Dictionary<string, object>> OnCollectDefaults;

        /// <summary>Called after a fetch that activated new values: read your keys here.</summary>
        public static event Action OnFetched;

        public static IEnumerator Init()
        {
            if (Backend == null)
            {
                Debug.Log("[ZenRemote] no backend (com.zen.firebase not installed) --> local values");
                yield break;
            }
            var defaults = new Dictionary<string, object>();
            OnCollectDefaults?.Invoke(defaults);
            yield return Backend.Init(defaults);
        }

        /// <summary>Fetches; OnFetched runs when new values are active (also if that happens after a timeout).</summary>
        public static IEnumerator Fetch()
        {
            if (Backend == null)
                yield break;
            yield return Backend.Fetch(ok =>
            {
                if (!ok || OnFetched == null)
                    return;
                foreach (Action handler in OnFetched.GetInvocationList())
                {
                    try { handler(); }
                    catch (Exception ex) { Debug.LogException(ex); }
                }
            });
        }

        public static string GetString(string key, string defaultValue) => Backend != null ? Backend.GetString(key, defaultValue) : defaultValue;
        public static int GetInt(string key, int defaultValue) => Backend != null ? Backend.GetInt(key, defaultValue) : defaultValue;
        public static bool GetBool(string key, bool defaultValue) => Backend != null ? Backend.GetBool(key, defaultValue) : defaultValue;
        public static float GetFloat(string key, float defaultValue) => Backend != null ? Backend.GetFloat(key, defaultValue) : defaultValue;
    }
}
