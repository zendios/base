using UnityEngine;

namespace Base
{
    /// <summary>
    /// The game's store settings (Assets/Resources/ZenIapSettings.asset, made by Base > Hub > Setup).
    /// Products and prices come from the game's Unity IAP catalog.
    /// </summary>
    public class ZenIapSettings : ScriptableObject
    {
        public const string ResourcePath = "ZenIapSettings";

        /// <summary>Every Remove Ads product id ends with this.</summary>
        public const string RemoveAdsSuffix = "remove.ads";

        [Tooltip("Product of the \"Remove ADs\" buttons on banners / MRECs. Empty = <package name>.remove.ads " +
                 "(e.g. com.zen.mygame.remove.ads). A custom id must also end with \"remove.ads\".")]
        public string removeAdsProductId = "";

        /// <summary>The Remove Ads product id: removeAdsProductId, or the package name + ".remove.ads" when it is empty.</summary>
        public string RemoveAdsProductId => GetRemoveAdsProductId(this);

        /// <summary>Same rule without an asset (no ZenIapSettings in the game): package name + ".remove.ads".</summary>
        public static string GetRemoveAdsProductId(ZenIapSettings settings) =>
            settings != null && !string.IsNullOrWhiteSpace(settings.removeAdsProductId)
                ? settings.removeAdsProductId.Trim()
                : $"{Application.identifier}.{RemoveAdsSuffix}";

        private static ZenIapSettings instance;

        public static ZenIapSettings Instance
        {
            get
            {
                if (instance == null)
                    instance = Resources.Load<ZenIapSettings>(ResourcePath);
                return instance;
            }
        }
    }
}
