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
            ZenRemote.OnCollectDefaults -= AddDefaults;
            ZenRemote.OnCollectDefaults += AddDefaults;
            ZenRemote.OnFetched -= Apply;
            ZenRemote.OnFetched += Apply;
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
            d["taichi_config_json"] = "";
            d["adRewardFlow"] = (int)c.adRewardFlow;
        }

        private static void Apply()
        {
            var c = Config;
            c.adTimePlayToShow = ZenRemote.GetFloat("adTimePlayToShow", c.adTimePlayToShow);
            c.adTimePlayReduceToShow = ZenRemote.GetFloat("adTimePlayReduceToShow", c.adTimePlayReduceToShow);
            c.adLoadTimeout = ZenRemote.GetFloat("adLoadTimeout", c.adLoadTimeout);
            c.adTimeBetween = ZenRemote.GetFloat("adTimeBetween", c.adTimeBetween);
            c.adAppOpenFlow = (AdFlow)ZenRemote.GetInt("adAppOpenFlow", (int)c.adAppOpenFlow);
            c.adBannerFlow = (AdFlow)ZenRemote.GetInt("adBannerFlow", (int)c.adBannerFlow);
            c.adInterFlow = (AdFlow)ZenRemote.GetInt("adInterFlow", (int)c.adInterFlow);
            c.adInterOnFTUE = ZenRemote.GetInt("adInterOnFTUE", c.adInterOnFTUE);
            c.adInterOnPlay = ZenRemote.GetInt("adInterOnPlay", c.adInterOnPlay);
            c.adInterOnStart = ZenRemote.GetBool("adInterOnStart", c.adInterOnStart);
            c.adInterOnComplete = ZenRemote.GetBool("adInterOnComplete", c.adInterOnComplete);
            c.adInterVsRewardRatio = ZenRemote.GetInt("adInterVsRewardRatio", c.adInterVsRewardRatio);
            c.adMrecOnFTUE = ZenRemote.GetInt("adMrecOnFTUE", c.adMrecOnFTUE);
            c.adBannerReload = ZenRemote.GetInt("adBannerReload", c.adBannerReload);
            c.adBannerOffset = ZenRemote.GetInt("adBannerOffset", c.adBannerOffset);
            c.adRewardFlow = (AdFlow)ZenRemote.GetInt("adRewardFlow", (int)c.adRewardFlow);

            string taichiJson = ZenRemote.GetString("taichi_config_json", "");
            if (TaichiManager.Instance != null && !string.IsNullOrEmpty(taichiJson))
                TaichiManager.Instance.ApplyRemoteConfig(taichiJson);

            foreach (AdBase ad in AdsManager.List)
                if (ad is AdZative zative)
                    zative.ApplyRemoteConfig(ZenRemote.GetString(zative.name, ""));
        }
    }
}
