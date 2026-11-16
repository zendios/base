using System.Collections.Generic;
using Base.Setup;
using UnityEditor;
using UnityEngine;

namespace Base
{
    /// <summary>IAP: the game's IAPSettings and its Remove Ads product (package name + ".remove.ads" by default).</summary>
    internal class IapSetupModule : ISetupModule
    {
        public string Name => "IAP";
        public int Order => 20;

        public IEnumerable<SetupIssue> Check()
        {
            var settings = SetupUtil.FindResource<IAPSettings>(IAPSettings.ResourcePath);
            string productId = IAPSettings.GetRemoveAdsProductId(settings);
            if (settings == null)
                yield return new SetupIssue
                {
                    message = $"No IAPSettings in the game: the Remove ADs buttons buy {productId}. Create it to set another id.",
                    type = MessageType.Info,
                    fixLabel = "Create",
                    fix = () => SetupUtil.CreateResource<IAPSettings>(IAPSettings.ResourcePath),
                };
            else if (!productId.EndsWith(IAPSettings.RemoveAdsSuffix))
                yield return new SetupIssue
                {
                    message = $"Remove Ads product id \"{productId}\" must end with \"{IAPSettings.RemoveAdsSuffix}\" (leave it empty for {Application.identifier}.{IAPSettings.RemoveAdsSuffix}).",
                    type = MessageType.Error,
                    fixLabel = "Open",
                    fix = () => Selection.activeObject = settings,
                };
            else
                yield return new SetupIssue
                {
                    title = $"Remove Ads product: {productId}",
                    message = $"Remove ADs buttons buy {productId}: the IAP catalog (and Google Play / App Store) must have this product.",
                    type = MessageType.Info,
                };
        }
    }
}
