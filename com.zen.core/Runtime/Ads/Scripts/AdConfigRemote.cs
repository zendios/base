using System.Collections.Generic;
using UnityEngine;

namespace Base.Ads
{
    /// <summary>
    /// Ad keys of Remote Config: AdConfig values, the Taichi JSON, and one JSON per native placement (key = placement
    /// name, e.g. "AdZative_Inter_Default", see AdZative.ApplyRemoteConfig). Never remove or rename a key: live
    /// Remote Config projects use them.
    /// </summary>
    public static class AdConfigRemote
    {
        private static AdConfig Config => AdConfig.Instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            RemoteConfigManager.OnCollectDefaults -= AddDefaults;
            RemoteConfigManager.OnCollectDefaults += AddDefaults;
            RemoteConfigManager.OnFetched -= Apply;
            RemoteConfigManager.OnFetched += Apply;
        }

        /// <summary>Adds this module's Remote Config keys with their current values (also used by Base > Hub export).</summary>
        public static void AddDefaults(Dictionary<string, object> d)
        {
            var c = Config;
            d["adTimePlayToShow"] = c.adTimePlayToShow;
            d["adTimePlayReduceToShow"] = c.adTimePlayReduceToShow;
            d["adLoadTimeout"] = c.adLoadTimeout;
            d["adTimeBetween"] = c.adTimeBetween;
            d["adAppOpenFlow"] = (int)c.adAppOpenFlow;
            d["adBannerFlow"] = (int)c.adBannerFlow;
            d["adInterFlow"] = (int)c.adInterFlow;
            d["adInterOnFTUE"] = c.adInterOnFTUE;
            d["adInterOnPlay"] = c.adInterOnPlay;
            d["adInterOnStart"] = c.adInterOnStart;
            d["adInterOnComplete"] = c.adInterOnComplete;
            d["adInterVsRewardRatio"] = c.adInterVsRewardRatio;
            d["adMrecOnFTUE"] = c.adMrecOnFTUE;
            d["adBannerReload"] = c.adBannerReload;
            d["adBannerOffset"] = c.adBannerOffset;
            d["adCtrMax"] = c.adCtrMax;
            d["adCtrMin"] = c.adCtrMin;
            d["forceMediaScaleMeta"] = c.forceMediaScaleMeta;
            d["taichi_config_json"] = "";
            d["adRewardFlow"] = (int)c.adRewardFlow;
        }

        private static void Apply()
        {
            var c = Config;
            c.adTimePlayToShow = RemoteConfigManager.GetFloat("adTimePlayToShow", c.adTimePlayToShow);
            c.adTimePlayReduceToShow = RemoteConfigManager.GetFloat("adTimePlayReduceToShow", c.adTimePlayReduceToShow);
            c.adLoadTimeout = RemoteConfigManager.GetFloat("adLoadTimeout", c.adLoadTimeout);
            c.adTimeBetween = RemoteConfigManager.GetFloat("adTimeBetween", c.adTimeBetween);
            c.adAppOpenFlow = (AdFlow)RemoteConfigManager.GetInt("adAppOpenFlow", (int)c.adAppOpenFlow);
            c.adBannerFlow = (AdFlow)RemoteConfigManager.GetInt("adBannerFlow", (int)c.adBannerFlow);
            c.adInterFlow = (AdFlow)RemoteConfigManager.GetInt("adInterFlow", (int)c.adInterFlow);
            c.adInterOnFTUE = RemoteConfigManager.GetInt("adInterOnFTUE", c.adInterOnFTUE);
            c.adInterOnPlay = RemoteConfigManager.GetInt("adInterOnPlay", c.adInterOnPlay);
            c.adInterOnStart = RemoteConfigManager.GetBool("adInterOnStart", c.adInterOnStart);
            c.adInterOnComplete = RemoteConfigManager.GetBool("adInterOnComplete", c.adInterOnComplete);
            c.adInterVsRewardRatio = RemoteConfigManager.GetInt("adInterVsRewardRatio", c.adInterVsRewardRatio);
            c.adMrecOnFTUE = RemoteConfigManager.GetInt("adMrecOnFTUE", c.adMrecOnFTUE);
            c.adBannerReload = RemoteConfigManager.GetInt("adBannerReload", c.adBannerReload);
            c.adBannerOffset = RemoteConfigManager.GetInt("adBannerOffset", c.adBannerOffset);
            c.adCtrMax = RemoteConfigManager.GetFloat("adCtrMax", c.adCtrMax);
            c.adCtrMin = RemoteConfigManager.GetFloat("adCtrMin", c.adCtrMin);
            AdCtr.Max = c.adCtrMax;
            AdCtr.Min = c.adCtrMin;
            c.forceMediaScaleMeta = RemoteConfigManager.GetBool("forceMediaScaleMeta", c.forceMediaScaleMeta);
            c.adRewardFlow = (AdFlow)RemoteConfigManager.GetInt("adRewardFlow", (int)c.adRewardFlow);

            string taichiJson = RemoteConfigManager.GetString("taichi_config_json", "");
            if (TaichiManager.Instance != null && !string.IsNullOrEmpty(taichiJson))
                TaichiManager.Instance.ApplyRemoteConfig(taichiJson);

            foreach (AdBase ad in AdsManager.List)
                if (ad is AdZative zative)
                    zative.ApplyRemoteConfig(RemoteConfigManager.GetString(zative.name, ""));
        }
    }
}
