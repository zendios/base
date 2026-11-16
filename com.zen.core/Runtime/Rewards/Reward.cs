using System;
using UnityEngine;

namespace Base
{
    /// <summary>
    /// A reward of a daily login day, a mission or an achievement: coins, gems, a booster or an owned item.
    /// Granted by Rewards.Grant.
    /// </summary>
    [Serializable]
    public class Reward
    {
        [Tooltip("Soft / Hard: CurrencyManager. Booster: adds amount to the item's count. Item: the player owns the item.")]
        public CurrencyType type = CurrencyType.Soft;
        [Tooltip("Booster / Item: the item id in an ItemDatas of DataManager.itemDatas. Empty for Soft / Hard.")]
        public string id;
        [Min(1)]
        public int amount = 100;
        [Tooltip("Optional icon (a booster, Remove Ads...). Empty: Soft / Hard use the icon of CurrencyManager.")]
        public Sprite icon;

        /// <summary>Doubling for a rewarded ad makes sense (coins, boosters), not for gems or an owned item.</summary>
        public bool CanDouble => type == CurrencyType.Soft || type == CurrencyType.Booster;
    }
}
