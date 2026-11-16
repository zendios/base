using System.Globalization;
using UnityEngine;

namespace Base
{
    /// <summary>
    /// Grants a Reward and saves: daily login, missions and achievements use it, the game can too (chests, gifts...).
    /// Boosters and items are looked up by id in DataManager.itemDatas.
    ///   Rewards.Grant(reward, "chest", button.transform);
    /// </summary>
    public static class Rewards
    {
        /// <summary>Grants reward (amount × times; the fly FX from "from" for currencies) and saves. False when the booster
        /// or item id is in no registered ItemDatas, or the item is already owned.</summary>
        public static bool Grant(Reward reward, string source, Transform from, int times = 1)
        {
            int amount = reward.amount * times;
            switch (reward.type)
            {
                case CurrencyType.Soft:
                case CurrencyType.Hard:
                    AddCurrency(reward.type, amount, source, from);
                    break;
                case CurrencyType.Booster:
                case CurrencyType.Item:
                    var item = Find(reward.id);
                    if (item == null || !GrantItem(item, reward, amount))
                        return false;
                    break;
                default:
                    Debug.LogError("[Rewards] " + source + ": " + reward.type + " is not a reward type (Soft, Hard, Booster, Item).");
                    return false;
            }
            if (DataManager.IsLoaded)
                DataManager.Save(true);
            return true;
        }

        /// <summary>The reward in words for texts shown to the player: "3,000 Cash", "60 Token" (the names of
        /// CurrencyManager), "Nitro", "2 x Nitro", "Remove Ads", "VIP".</summary>
        public static string Describe(Reward reward)
        {
            switch (reward.type)
            {
                case CurrencyType.Soft:
                case CurrencyType.Hard:
                    return reward.amount.ToString("N0", CultureInfo.InvariantCulture) + " " + CurrencyManager.NameOf(reward.type);
                case CurrencyType.Ad:
                    return "Remove Ads";
                case CurrencyType.VIP:
                    return "VIP";
                default:
                    string name = ItemName(reward.id);
                    return reward.amount > 1 ? reward.amount + " x " + name : name;
            }
        }

        /// <summary>The icon of a reward: its own, else the Soft / Hard icon of CurrencyManager, else null.</summary>
        public static Sprite IconOf(Reward reward)
        {
            return reward.icon != null ? reward.icon : CurrencyManager.IconOf(reward.type);
        }

        // The name of the ItemData; the id itself ("booster_nitro" as "Nitro") when no ItemDatas has it (the Editor).
        private static string ItemName(string id)
        {
            foreach (var datas in DataManager.ItemDatas)
            {
                var item = datas.Find(id);
                if (item != null && !string.IsNullOrEmpty(item.name))
                    return item.name;
            }
            string pretty = (id ?? "").Replace("booster_", "").Replace('_', ' ');
            return pretty.Length > 0 ? char.ToUpper(pretty[0]) + pretty.Substring(1) : pretty;
        }

        // Booster: adds to the count. Item: owns it (false when already owned).
        internal static bool GrantItem(ItemData item, Reward reward, int amount)
        {
            if (reward.type == CurrencyType.Booster)
            {
                item.count = Mathf.Max(item.count, 0) + amount;   // ItemData.count starts at -1
                item.isOwner = true;                              // saveList only keeps owned items
                return true;
            }
            if (item.isOwner)
            {
                Debug.LogWarning("[Rewards] item " + reward.id + " is already owned, nothing granted.");
                return false;
            }
            item.isOwner = true;
            item.isUnlocked = true;
            Missions.Report(Missions.EventUnlockItem);
            return true;
        }

        private static void AddCurrency(CurrencyType type, int amount, string source, Transform from)
        {
            if (CurrencyManager.HasInstance)
                CurrencyManager.Add(amount, source, type, from);
            else if (type == CurrencyType.Hard)
                CurrencyManager.TotalHard += amount;
            else
                CurrencyManager.TotalSoft += amount;
        }

        private static ItemData Find(string id)
        {
            foreach (var datas in DataManager.ItemDatas)
            {
                var item = datas.Find(id);
                if (item != null)
                    return item;
            }
            Debug.LogError("[Rewards] no item \"" + id + "\" in DataManager.itemDatas.");
            return null;
        }
    }
}
