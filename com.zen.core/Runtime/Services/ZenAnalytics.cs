using System.Collections.Generic;

namespace Base
{
    /// <summary>Analytics backend; com.zen.firebase registers Firebase Analytics.</summary>
    public interface IZenAnalytics
    {
        bool IsReady { get; }
        void LogEvent(string eventName, Dictionary<string, object> parameters);
        void LogEventExact(string eventName, Dictionary<string, object> parameters);
        void SetUser(string property, object value);
        void LogIAP(string screen, string productId, IAPStatus status, string localizedPrice, string isoCurrencyCode);
        void LogResourceEarn(int value, string currencyName, CurrencyType currencyType, string placement);
        void LogResourceSpend(int value, string currencyName, CurrencyType currencyType, string placement);
    }

    /// <summary>
    /// Analytics calls of every Zen package. They go to the registered backend (com.zen.firebase sets Firebase);
    /// without a backend they do nothing, so a game without Firebase still compiles and runs.
    /// </summary>
    public static class ZenAnalytics
    {
        public static IZenAnalytics Backend { get; set; }

        /// <summary>True when events are recorded now (backend registered and initialized).</summary>
        public static bool IsReady => Backend != null && Backend.IsReady;

        public static void LogEvent(string eventName, Dictionary<string, object> parameters = null) => Backend?.LogEvent(eventName, parameters);

        /// <summary>
        /// Sends the event as given: no name / value formatting, no "session" added, no readiness check (revenue
        /// events). Values: string, int / long, float / double.
        /// </summary>
        public static void LogEventExact(string eventName, Dictionary<string, object> parameters) => Backend?.LogEventExact(eventName, parameters);

        public static void SetUser(string property, object value) => Backend?.SetUser(property, value);

        public static void LogIAP(string screen, string productId, IAPStatus status, string localizedPrice, string isoCurrencyCode) =>
            Backend?.LogIAP(screen, productId, status, localizedPrice, isoCurrencyCode);

        public static void LogResourceEarn(int value, string currencyName, CurrencyType currencyType, string placement) =>
            Backend?.LogResourceEarn(value, currencyName, currencyType, placement);

        public static void LogResourceSpend(int value, string currencyName, CurrencyType currencyType, string placement) =>
            Backend?.LogResourceSpend(value, currencyName, currencyType, placement);
    }
}
