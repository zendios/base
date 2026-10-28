using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Moves a player's data out of a save written by an older Base (BinaryFormatter). DataManager has already copied
/// userData (currency, VIP, Remove Ads, counters) and kept the old file as .gd.legacy; this copies the game's part.
/// The classes below describe the OLD save: field names must be the old ones, types only need to be compatible.
/// List only what the game needs; anything else in the old save is ignored.
/// </summary>
public static class SampleLegacySave
{
    [Serializable]
    private class OldSave
    {
        public List<ItemData> characterDatas;
        public List<OldVehicle> vehicleDatas;
        public OldUser userData;
    }

    [Serializable]
    private class OldVehicle
    {
        public string id;
        public bool isUnlocked;
        public bool isOwner;
        public bool isSelected;
        public int upgradeLevel;
    }

    [Serializable]
    private class OldUser
    {
        public float _levelPoint = -1;
        public int nitro;
        public float shield;
        public LevelData levelData;
    }

    /// <summary>DataManager.OnLegacySaveLoaded handler.</summary>
    public static void Apply(string json)
    {
        var old = JsonUtility.FromJson<OldSave>(json);

        var characters = DataManager.Get<SampleCharacterDatas>();
        if (characters != null && old.characterDatas != null)
            DataManager.GameData.SetItems(characters.name, old.characterDatas);

        var vehicles = DataManager.Get<SampleVehicleDatas>();
        if (vehicles != null && old.vehicleDatas != null)
        {
            var items = new List<ItemData>();
            foreach (var oldVehicle in old.vehicleDatas)
            {
                items.Add(new ItemData
                {
                    id = oldVehicle.id,
                    isUnlocked = oldVehicle.isUnlocked,
                    isOwner = oldVehicle.isOwner,
                    isSelected = oldVehicle.isSelected,
                });
                var vehicle = vehicles.GetById(oldVehicle.id);
                if (vehicle != null)
                    vehicle.upgradeLevel = Mathf.Clamp(oldVehicle.upgradeLevel, 0, vehicle.maxUpgradeLevel);
            }
            DataManager.GameData.SetItems(vehicles.name, items);
        }

        if (old.userData != null)
        {
            var progress = SamplePlayerProgress.Current;
            if (old.userData._levelPoint >= 0)
                progress.LevelPoint = old.userData._levelPoint;
            progress.nitro = old.userData.nitro;
            progress.shield = old.userData.shield;
            if (old.userData.levelData != null)
                progress.levelData = old.userData.levelData;
        }
    }
}
