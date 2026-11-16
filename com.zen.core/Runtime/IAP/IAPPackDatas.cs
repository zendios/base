using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Base
{
    /// <summary>
    /// One IAP pack of the game: the store product and what it grants. ItemData gives id (the key of the pack)
    /// and name (the title shown in the shop). An IAPButtonExtent uses the pack with its productId.
    /// </summary>
    [Serializable]
    public class IAPPackData : ItemData
    {
        [Tooltip("Product id of the store catalog (Unity IAP).")]
        public string productId;
        [Tooltip("Granted by IAPManager.TryGrant: Soft / Hard (CurrencyManager), Booster / Item (Rewards.Grant), Ad (Remove Ads), VIP.")]
        public List<Reward> rewards = new List<Reward>();
        [Tooltip("Shown on the pack: \"BEST VALUE\", \"-50%\"... Empty = none.")]
        public string badge;
        public Sprite icon;
        [Tooltip("May be offered in a popup by the game; off = only sold in the shop.")]
        public bool isOffer;

        /// <summary>What the pack grants as one line, from Rewards.Describe: "60 Token, 3,000 Cash, 2 x Nitro".</summary>
        public string Describe()
        {
            return string.Join(", ", rewards.Select(Rewards.Describe));
        }
    }

    /// <summary>
    /// The IAP packs of the game. Add the asset to DataManager.itemDatas; IAPManager.GetPack(id) reads it, an
    /// IAPButtonExtent takes the rewards of the pack with its productId.
    /// </summary>
    [CreateAssetMenu(fileName = "IAPPackDatas", menuName = "Base/IAP Pack Datas")]
    public class IAPPackDatas : ItemDatas<IAPPackData>
    {
    }
}
