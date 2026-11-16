using System;
using System.Collections.Generic;
using System.Linq;
using Base.Setup;
using UnityEditor;

namespace Base.Ads
{
    /// <summary>Ads: the game's AdConfig and AdIds (Assets/Resources).</summary>
    internal class AdsSetupModule : ISetupModule
    {
        public string Name => "Ads";
        public int Order => 10;

        public IEnumerable<SetupIssue> Check()
        {
            var nextGenIssue = CheckGmaNextGen();
            if (nextGenIssue != null)
                yield return nextGenIssue;

            if (SetupUtil.FindResource<AdConfig>(AdConfig.ResourcePath) == null)
                yield return new SetupIssue
                {
                    message = "No AdConfig in the game: the package defaults are used (frequency, flows, FTUE). Create one to tune them per game.",
                    fixLabel = "Create",
                    fix = () => SetupUtil.CreateResource<AdConfig>(AdConfig.ResourcePath),
                };

            var ids = SetupUtil.FindResource<AdIds>(AdIds.ResourcePath);
            if (ids == null)
            {
                yield return new SetupIssue
                {
                    message = "No AdIds in the game: every placement loads Google test IDs. Create it, then paste the AdMob ad unit IDs.",
                    type = MessageType.Error,
                    fixLabel = "Create",
                    fix = () => SetupUtil.CreateResource<AdIds>(AdIds.ResourcePath, a => a.AddMissingPlacements()),
                };
                yield break;
            }

            if (ids.AddMissingPlacements())
                EditorUtility.SetDirty(ids);
            var empty = Enum.GetValues(typeof(PlacementType)).Cast<PlacementType>()
                .Where(p => p != PlacementType.None && ids.entries.First(e => e.placement == p).android.All(string.IsNullOrWhiteSpace))
                .ToList();
            if (empty.Count > 0)
                yield return new SetupIssue
                {
                    title = $"{empty.Count} placements without an Android ID (test ads)",
                    message = $"{empty.Count} placements have no Android ID and load Google test IDs: {string.Join(", ", empty)}. Fine for placements the game does not use.",
                    type = MessageType.Info,
                    fixLabel = "Open",
                    fix = () => Selection.activeObject = ids,
                };
        }

        // AdZative is built on the GMA Next-Gen SDK. The Google Mobile Ads plugin builds the Standard SDK unless its
        // settings select Next-Gen; then native ads fail on device (NoClassDefFoundError ...ads.mobile.sdk).
        private static SetupIssue CheckGmaNextGen()
        {
            var settings = AssetDatabase.FindAssets("GoogleMobileAdsSettings t:ScriptableObject").Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<UnityEngine.ScriptableObject>).FirstOrDefault(s => s != null);
            if (settings == null)
                return null;

            var so = new SerializedObject(settings);
            var overrideSdk = so.FindProperty("overrideDefaultGmaAndroidSdk");
            var selectedSdk = so.FindProperty("selectedGmaAndroidSdk");
            if (overrideSdk == null || selectedSdk == null || (overrideSdk.boolValue && selectedSdk.intValue == 1))
                return null;

            return new SetupIssue
            {
                title = "Google Mobile Ads builds the Standard Android SDK",
                message = "AdZative native ads need the GMA Next-Gen Android SDK; with the Standard one they fail on device " +
                          "(NoClassDefFoundError com.google.android.libraries.ads.mobile.sdk). Fix selects Next-Gen in GoogleMobileAdsSettings.",
                type = MessageType.Error,
                fixLabel = "Use Next-Gen",
                fix = () =>
                {
                    overrideSdk.boolValue = true;
                    selectedSdk.intValue = 1;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    AssetDatabase.SaveAssets();
                },
            };
        }
    }
}
