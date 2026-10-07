using System.Collections.Generic;
using UnityEngine;

namespace Base
{
    /// <summary>GameConfig keys of Remote Config (version gate, FTUE A/B).</summary>
    public static class GameConfigRemote
    {
        private static GameConfig Config => DataManager.GameConfig;

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
            if (c == null)
                return;
            d["bundleVersion"] = c.bundleVersion;
            d["forceVersion"] = c.forceVersion;
            d["inAppUpdate"] = c.inAppUpdate;
            d["FTUE_Play"] = c.FTUE_Play;
            d["FTUE_Character"] = c.FTUE_Character;
            d["FTUE_CharacterSelect"] = c.FTUE_CharacterSelect;
            d["FTUE_Level"] = c.FTUE_Level;
            d["FTUE_LevelVariant"] = c.FTUE_LevelVariant;
        }

        private static void Apply()
        {
            var c = Config;
            if (c == null)
                return;
            c.bundleVersion = ZenRemote.GetInt("bundleVersion", c.bundleVersion);
            c.forceVersion = ZenRemote.GetString("forceVersion", c.forceVersion);
            c.inAppUpdate = ZenRemote.GetBool("inAppUpdate", c.inAppUpdate);
            c.FTUE_Play = ZenRemote.GetBool("FTUE_Play", c.FTUE_Play);
            c.FTUE_Level = ZenRemote.GetString("FTUE_Level", c.FTUE_Level);
            c.FTUE_LevelVariant = ZenRemote.GetString("FTUE_LevelVariant", c.FTUE_LevelVariant);
            c.FTUE_Character = ZenRemote.GetString("FTUE_Character", c.FTUE_Character);
            c.FTUE_CharacterSelect = ZenRemote.GetBool("FTUE_CharacterSelect", c.FTUE_CharacterSelect);
        }
    }
}
