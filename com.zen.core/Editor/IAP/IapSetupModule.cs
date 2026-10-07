using System.Collections.Generic;
using Base.Setup;
using UnityEditor;
using UnityEngine;

namespace Base
{
    /// <summary>IAP: the game's ZenIapSettings and its Remove Ads product (package name + ".remove.ads" by default).</summary>
    internal class IapSetupModule : IZenSetupModule
    {
        public string Name => "IAP";
        public int Order => 20;

        public IEnumerable<ZenSetupIssue> Check()
        {
            var settings = ZenSetupUtil.FindResource<ZenIapSettings>(ZenIapSettings.ResourcePath);
            string productId = ZenIapSettings.GetRemoveAdsProductId(settings);
            if (settings == null)
                yield return new ZenSetupIssue
                {
                    message = $"No ZenIapSettings in the game: the Remove ADs buttons buy {productId}. Create it to set another id.",
                    type = MessageType.Info,
                    fixLabel = "Create",
                    fix = () => ZenSetupUtil.CreateResource<ZenIapSettings>(ZenIapSettings.ResourcePath),
                };
            else if (!productId.EndsWith(ZenIapSettings.RemoveAdsSuffix))
                yield return new ZenSetupIssue
                {
                    message = $"Remove Ads product id \"{productId}\" must end with \"{ZenIapSettings.RemoveAdsSuffix}\" (leave it empty for {Application.identifier}.{ZenIapSettings.RemoveAdsSuffix}).",
                    type = MessageType.Error,
                    fixLabel = "Open",
                    fix = () => Selection.activeObject = settings,
                };
            else
                yield return new ZenSetupIssue
                {
                    title = $"Remove Ads product: {productId}",
                    message = $"Remove ADs buttons buy {productId}: the IAP catalog (and Google Play / App Store) must have this product.",
                    type = MessageType.Info,
                };
        }
    }
}
