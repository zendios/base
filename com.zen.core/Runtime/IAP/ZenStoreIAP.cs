#if USE_IN_APP_PURCHASE
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Base
{
    /// <summary>Unity IAP behind the ZenStore hooks: the shop popup and the Remove Ads buttons of the ad frames.</summary>
    internal static class ZenStoreIAP
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            ZenStore.OpenShop = () => UIPopupIAP.Show();
            ZenStore.SetupRemoveAdsButton = SetupRemoveAds;
        }

        private static void SetupRemoveAds(GameObject go, Transform startParticlePosition)
        {
            var iap = go.GetComponent<IAPButtonExtent>() ?? go.AddComponent<IAPButtonExtent>();
            iap.productId = ZenIapSettings.GetRemoveAdsProductId(ZenIapSettings.Instance);
            iap.button = go.GetComponent<Button>();
            iap.startParticlePosition = startParticlePosition;
            iap.rewards = new List<IAPButtonExtent.IAPReward> { new IAPButtonExtent.IAPReward { type = CurrencyType.Ad, value = 1 } };
        }
    }
}
#endif
