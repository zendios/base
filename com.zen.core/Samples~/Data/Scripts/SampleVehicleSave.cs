using System;
using System.Collections.Generic;
using Base;
using UnityEngine;

/// <summary>
/// Saves what Base does not save for a catalogue item: each vehicle's upgrade level, by vehicle id.
/// The pattern for any per-item state of a game (upgrades, stars, best time...).
/// </summary>
public class SampleVehicleSave : ISaveSection
{
    [Serializable]
    private class Entry
    {
        public string id;
        public int upgradeLevel;
    }

    [Serializable]
    private class Data
    {
        public List<Entry> vehicles = new List<Entry>();
    }

    public string SaveKey => "sample_vehicles";

    public string ToJson()
    {
        var data = new Data();
        var vehicles = DataManager.Get<SampleVehicleDatas>();
        if (vehicles != null)
            foreach (var vehicle in vehicles.list)
                if (vehicle.upgradeLevel > 0)
                    data.vehicles.Add(new Entry { id = vehicle.id, upgradeLevel = vehicle.upgradeLevel });
        return JsonUtility.ToJson(data);
    }

    public void FromJson(string json)
    {
        var vehicles = DataManager.Get<SampleVehicleDatas>();
        if (vehicles == null)
            return;

        var data = JsonUtility.FromJson<Data>(json);
        foreach (var entry in data.vehicles)
        {
            var vehicle = vehicles.GetById(entry.id);
            if (vehicle != null)
                vehicle.upgradeLevel = Mathf.Clamp(entry.upgradeLevel, 0, vehicle.maxUpgradeLevel);
        }
    }
}
