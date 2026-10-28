using System;
using UnityEngine;

/// <summary>
/// A playable character. ItemData already holds id, name, price, currencyType, isOwner, isSelected...; add only what
/// the game needs. Static data (power) lives in the asset; the player's state (owned, selected) is saved by Base.
/// </summary>
[Serializable]
public class SampleCharacterData : ItemData
{
    [Tooltip("Static stat shown in the sample; not saved.")]
    public int power = 10;
}

/// <summary>
/// The character catalogue. Add the asset to DataManager.itemDatas: Base saves each item's isUnlocked, isOwner,
/// isSelected and count under the asset name. Read it with DataManager.Get&lt;SampleCharacterDatas&gt;().
/// </summary>
public class SampleCharacterDatas : ItemDatas<SampleCharacterData>
{
}
