using UnityEngine;

namespace Base
{
    /// <summary>
    /// The achievements of the game: MissionData items with group and tier (e.g. "Racer" tiers 1, 2, 3 with targets
    /// 10, 50, 200 on "play"). Add the asset to DataManager.itemDatas; Achievements reads it with
    /// DataManager.Get&lt;AchievementDatas&gt;().
    /// </summary>
    [CreateAssetMenu(fileName = "AchievementDatas", menuName = "Base/Achievement Datas")]
    public class AchievementDatas : ItemDatas<MissionData>
    {
    }
}
