using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Base.Ads
{
    /// <summary>
    /// The game's ad unit IDs, one row per PlacementType (Android / iOS lists = waterfall order). The asset lives in the
    /// game (Assets/Resources/ZenAdIds.asset, made by Base > Hub > Setup), never in the package, so a package
    /// update never touches it. A placement without IDs here loads Google's test IDs (warning in the log).
    /// A prefab can still set AdBase.adIdData to override its row.
    /// </summary>
    public class ZenAdIds : ScriptableObject
    {
        public const string ResourcePath = "ZenAdIds";

        [Serializable]
        public class Entry
        {
            [HideInInspector] public string name;   // = placement: Unity labels list elements with the first string field
            public PlacementType placement;
            public List<string> android = new List<string>();
            public List<string> ios = new List<string>();
        }

        public List<Entry> entries = new List<Entry>();

        private static ZenAdIds instance;

        /// <summary>The game's ZenAdIds asset (Resources), or null when the game has none.</summary>
        public static ZenAdIds Instance
        {
            get
            {
                if (instance == null)
                    instance = Resources.Load<ZenAdIds>(ResourcePath);
                return instance;
            }
        }

        /// <summary>IDs of the placement for the running platform, in waterfall order; empty when none are set.</summary>
        public List<string> Get(PlacementType placement)
        {
            var entry = entries.FirstOrDefault(e => e.placement == placement);
            if (entry == null)
                return new List<string>();
            bool ios = Application.platform == RuntimePlatform.IPhonePlayer || Application.platform == RuntimePlatform.OSXPlayer;
            return (ios ? entry.ios : entry.android).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList();
        }

        /// <summary>Adds an empty row for every PlacementType that has none and names every row after its placement
        /// (editor: Base > Hub > Setup, Base > Hub, OnValidate). True when something changed.</summary>
        public bool AddMissingPlacements()
        {
            bool added = false;
            foreach (PlacementType p in Enum.GetValues(typeof(PlacementType)))
            {
                if (p == PlacementType.None || entries.Any(e => e.placement == p))
                    continue;
                entries.Add(new Entry { name = p.ToString(), placement = p });
                added = true;
            }
            if (added)
                entries = entries.OrderBy(e => (int)e.placement).ToList();
            foreach (var e in entries.Where(e => e.name != e.placement.ToString()))
            {
                e.name = e.placement.ToString();   // list label in the inspector
                added = true;
            }
            return added;
        }

        private void OnValidate()
        {
            AddMissingPlacements();
        }
    }
}
