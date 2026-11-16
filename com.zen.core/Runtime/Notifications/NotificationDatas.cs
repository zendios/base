using System;
using UnityEngine;

namespace Base
{
    /// <summary>
    /// One reminder text: name = title, description = body. Tokens: {day} (next login day), {reward} (its reward),
    /// {count} (rewards waiting). Several texts per type take turns.
    /// </summary>
    [Serializable]
    public class NotificationData : ItemData
    {
        [Tooltip("daily_ready, rewards_waiting, comeback_d3 or comeback_d7 (NotificationPlanner.Type...).")]
        public string type;
    }

    /// <summary>
    /// The reminder texts of the game. Add the asset to DataManager.itemDatas; NotificationManager reads it with
    /// DataManager.Get&lt;NotificationDatas&gt;(). A type without any text is not sent.
    /// </summary>
    [CreateAssetMenu(fileName = "NotificationDatas", menuName = "Base/Notification Datas")]
    public class NotificationDatas : ItemDatas<NotificationData>
    {
    }
}
