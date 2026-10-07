using System;
using UnityEngine;

namespace Base
{
    /// <summary>
    /// Store hooks other packages use without depending on Unity IAP. com.zen.iap sets them; without it they stay null
    /// (a Remove Ads button then hides itself, the currency "+" buttons do nothing).
    /// </summary>
    public static class ZenStore
    {
        /// <summary>Makes a button buy the game's Remove Ads product (com.zen.iap). Args: button object, particle start.</summary>
        public static Action<GameObject, Transform> SetupRemoveAdsButton { get; set; }

        /// <summary>Opens the game's shop (com.zen.iap shows PopupIAP).</summary>
        public static Action OpenShop { get; set; }

        /// <summary>Raised when a purchase starts: com.zen.ads holds interstitials for a while.</summary>
        public static event Action OnPurchaseStarted;

        public static void RaisePurchaseStarted() => OnPurchaseStarted?.Invoke();
    }
}
