using System;
using UnityEngine;

namespace Base
{
    /// <summary>
    /// One mission of DailyMissionDatas or one achievement tier of AchievementDatas. ItemData gives id (saved with the
    /// progress, keep it stable) and name (the title, "{0}" = target); its shop fields are not used here.
    /// </summary>
    [Serializable]
    public class MissionData : ItemData
    {
        [Tooltip("The event that moves the progress: Missions.Event* (play, win...) or the game's own key.")]
        public string eventKey = Missions.EventPlay;
        [Min(1)]
        public int target = 1;
        public Reward reward = new Reward();
        [Tooltip("Achievements only: the tiers of one goal share a group.")]
        public string group;
        [Tooltip("Achievements only: 1, 2, 3...; a group shows its lowest unclaimed tier.")]
        public int tier;
        [Tooltip("Optional; empty = the UI's icon for the reward type.")]
        public Sprite icon;

        /// <summary>The name with "{0}" replaced by target ("Win {0} races" → "Win 3 races").</summary>
        public string Title => string.Format(name ?? string.Empty, target);
    }
}
