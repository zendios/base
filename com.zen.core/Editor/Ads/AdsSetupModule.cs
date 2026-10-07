using System;
using System.Collections.Generic;
using System.Linq;
using Base.Setup;
using UnityEditor;

namespace Base.Ads
{
    /// <summary>Ads: the game's AdConfig and ZenAdIds (Assets/Resources).</summary>
    internal class AdsSetupModule : IZenSetupModule
    {
        public string Name => "Ads";
        public int Order => 10;

        public IEnumerable<ZenSetupIssue> Check()
        {
            if (ZenSetupUtil.FindResource<AdConfig>(AdConfig.ResourcePath) == null)
                yield return new ZenSetupIssue
                {
                    message = "No AdConfig in the game: the package defaults are used (frequency, flows, FTUE). Create one to tune them per game.",
                    fixLabel = "Create",
                    fix = () => ZenSetupUtil.CreateResource<AdConfig>(AdConfig.ResourcePath),
                };

            var ids = ZenSetupUtil.FindResource<ZenAdIds>(ZenAdIds.ResourcePath);
            if (ids == null)
            {
                yield return new ZenSetupIssue
                {
                    message = "No ZenAdIds in the game: every placement loads Google test IDs. Create it, then paste the AdMob ad unit IDs.",
                    type = MessageType.Error,
                    fixLabel = "Create",
                    fix = () => ZenSetupUtil.CreateResource<ZenAdIds>(ZenAdIds.ResourcePath, a => a.AddMissingPlacements()),
                };
                yield break;
            }

            if (ids.AddMissingPlacements())
                EditorUtility.SetDirty(ids);
            var empty = Enum.GetValues(typeof(PlacementType)).Cast<PlacementType>()
                .Where(p => p != PlacementType.None && ids.entries.First(e => e.placement == p).android.All(string.IsNullOrWhiteSpace))
                .ToList();
            if (empty.Count > 0)
                yield return new ZenSetupIssue
                {
                    title = $"{empty.Count} placements without an Android ID (test ads)",
                    message = $"{empty.Count} placements have no Android ID and load Google test IDs: {string.Join(", ", empty)}. Fine for placements the game does not use.",
                    type = MessageType.Info,
                    fixLabel = "Open",
                    fix = () => Selection.activeObject = ids,
                };
        }
    }
}
