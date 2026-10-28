using System;
using UnityEngine;

/// <summary>
/// A vehicle with an upgrade level. Base saves only ItemData's fields of a catalogue item, so the upgrade level is
/// saved by SampleVehicleSave (an ISaveSection) next to it.
/// </summary>
[Serializable]
public class SampleVehicleData : ItemData
{
    [Tooltip("Highest upgrade level.")]
    [Range(1, 20)]
    public int maxUpgradeLevel = 5;

    [Tooltip("Soft currency per upgrade level.")]
    public int upgradePrice = 200;

    [HideInInspector]
    public int upgradeLevel;

    public bool IsMaxUpgrade => upgradeLevel >= maxUpgradeLevel;
}

/// <summary>The vehicle catalogue. Add the asset to DataManager.itemDatas.</summary>
public class SampleVehicleDatas : ItemDatas<SampleVehicleData>
{
}
