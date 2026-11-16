using System;
using UnityEngine;

namespace Base
{
    /// <summary>One day of the daily login calendar (name is optional, "Day 1"...).</summary>
    [Serializable]
    public class DailyLoginData : ItemData
    {
        public Reward reward = new Reward();
        [Tooltip("A bigger reward (last day...): the UI draws the cell differently.")]
        public bool isSpecial;
        [Tooltip("Shown in the calendar cell; empty = the UI's icon for the reward type.")]
        public Sprite icon;
    }

    /// <summary>
    /// The daily login calendar of the game, one item per day in order. Add the asset to DataManager.itemDatas;
    /// DailyLogin reads it with DataManager.Get&lt;DailyLoginDatas&gt;().
    /// </summary>
    [CreateAssetMenu(fileName = "DailyLoginDatas", menuName = "Base/Daily Login Datas")]
    public class DailyLoginDatas : ItemDatas<DailyLoginData>
    {
        [Tooltip("A missed day sends the player back to day 1 (off: the next claim is the next day).")]
        public bool resetOnMiss;
        [Tooltip("Offer coins and boosters doubled for a rewarded ad (never gems or items).")]
        public bool adDouble = true;
        [Tooltip("The home screen opens the calendar by itself when a reward is waiting.")]
        public bool showOnHome = true;
    }
}
