using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Base.Ads
{
    /// <summary>
    /// A "Remove ADs" button (banner / MREC frames, the settings popup): with Unity IAP it buys the game's Remove Ads
    /// product (IAPSettings); it hides itself without Unity IAP and once ads are removed.
    /// </summary>
    public class RemoveAdsButton : MonoBehaviour
    {
        public Button button;
        public Transform startParticlePosition;

        /// <summary>The game can sell Remove Ads (Unity IAP is installed).</summary>
        public static bool IsAvailable =>
#if USE_IN_APP_PURCHASE
            true;
#else
            false;
#endif

        private void Awake()
        {
#if USE_IN_APP_PURCHASE
            var iap = GetComponent<IAPButtonExtent>();
            if (iap == null)
                iap = gameObject.AddComponent<IAPButtonExtent>();
            iap.productId = IAPSettings.GetRemoveAdsProductId(IAPSettings.Instance);
            iap.button = button != null ? button : GetComponent<Button>();
            iap.startParticlePosition = startParticlePosition != null ? startParticlePosition : transform;
            iap.rewards = new List<IAPButtonExtent.IAPReward> { new IAPButtonExtent.IAPReward { type = CurrencyType.Ad, value = 1 } };
#endif
            UserDataBase.OnRemovedAdsChanged += OnRemovedAdsChanged;
            OnRemovedAdsChanged(AdsManager.IsRemovedAds);
        }

        private void OnDestroy() => UserDataBase.OnRemovedAdsChanged -= OnRemovedAdsChanged;

        private void OnRemovedAdsChanged(bool removed) => gameObject.SetActive(IsAvailable && !removed);
    }
}
