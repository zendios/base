using UnityEngine;

namespace Base
{
    /// <summary>
    /// The daily mission pool of the game. Add the asset to DataManager.itemDatas; DailyMissions reads it with
    /// DataManager.Get&lt;DailyMissionDatas&gt;(). Order the pool easy, medium, hard, easy... with missionsPerDay = 3 and
    /// pickRandom off: each day then deals one mission of each difficulty.
    /// </summary>
    [CreateAssetMenu(fileName = "DailyMissionDatas", menuName = "Base/Daily Mission Datas")]
    public class DailyMissionDatas : ItemDatas<MissionData>
    {
        [Tooltip("Missions dealt each day; 0 = all of them.")]
        [Min(0)]
        public int missionsPerDay = 3;
        [Tooltip("Off: the next missions of the pool in order each day. On: a random pick (the same on every device for a day).")]
        public bool pickRandom;
    }
}
