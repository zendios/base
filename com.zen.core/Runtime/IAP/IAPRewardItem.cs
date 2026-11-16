using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Base
{
    /// <summary>
    /// One reward cell of an IAP slot: an Image (the icon: this object's or a child's) and a TMP_Text (this object's or a
    /// child's). IAPButtonExtent fills the cells it finds below it: a row (a full line: Cash, Token, Remove Ads...) takes
    /// the Soft / Hard / Remove Ads / VIP rewards, a grid cell (icon with its count: a booster) takes the Booster / Item
    /// rewards, each in hierarchy order. A cell without a reward is hidden.
    /// </summary>
    public class IAPRewardItem : MonoBehaviour
    {
        [FormerlySerializedAs("isMain")]
        [Tooltip("A full line (currency, Remove Ads). Off = a grid cell (booster, item).")]
        [SerializeField] private bool isRow;

        public bool IsRow => isRow;

        /// <summary>Shows the reward, or hides the cell when it is null.</summary>
        public void Show(Reward reward)
        {
            gameObject.SetActive(reward != null);
            if (reward == null)
                return;
            var icon = GetComponentInChildren<Image>(true);
            var sprite = Rewards.IconOf(reward);
            if (icon != null && sprite != null)
                icon.sprite = sprite;
            var text = GetComponentInChildren<TMP_Text>(true);
            if (text != null)
                text.text = reward.type == CurrencyType.Ad || reward.type == CurrencyType.VIP
                    ? Rewards.Describe(reward)
                    : "x" + reward.amount.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
