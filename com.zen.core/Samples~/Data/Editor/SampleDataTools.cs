using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor tools of the data sample: write a save as an older Base did (BinaryFormatter) to try the migration,
/// delete the save, open its folder. Use them while not playing: DataManager saves again when Play stops.
/// </summary>
public static class SampleDataTools
{
    private const string Menu = "Tools/Zen Samples/";

    private static string SavePath => Path.Combine(Application.persistentDataPath, Application.identifier + ".gd");

    [MenuItem(Menu + "Write Legacy Save (BinaryFormatter)")]
    private static void WriteLegacySave()
    {
        if (!EditorUtility.DisplayDialog("Write legacy save",
                "Replace the save file with an old-format (BinaryFormatter) save?\nPress Play afterwards: " +
                "DataManager converts it and keeps it as .gd.legacy.", "Write", "Cancel"))
            return;

        var save = new LegacyShapes.GameData();
        save.userData.SetSample();
        save.characterDatas.Add(new LegacyShapes.ItemData { id = "character_racer", isUnlocked = true, isOwner = true, isSelected = true });
        save.vehicleDatas.Add(new LegacyShapes.VehicleData { id = "vehicle_pickup", isUnlocked = true, isOwner = true, isSelected = true, upgradeLevel = 2 });

        using (var file = File.Create(SavePath))
            new BinaryFormatter().Serialize(file, save);
        Debug.Log("Legacy save written: " + SavePath);
    }

    [MenuItem(Menu + "Delete Save")]
    private static void DeleteSave()
    {
        if (!EditorUtility.DisplayDialog("Delete save", "Delete the save file and its backups?", "Delete", "Cancel"))
            return;
        foreach (var file in Directory.GetFiles(Application.persistentDataPath, Application.identifier + ".gd*"))
            File.Delete(file);
        Debug.Log("Save deleted: " + SavePath);
    }

    [MenuItem(Menu + "Open Save Folder")]
    private static void OpenSaveFolder() => EditorUtility.RevealInFinder(SavePath);

    // The shape of a save written by an older Base: the class names do not matter to the migration, the field
    // names do (inherited fields included).
    private static class LegacyShapes
    {
        [Serializable]
        public class PlayerDataBase
        {
            public string id = "";
            public string name = "YOU";
            public string countryCode = "";
        }

        [Serializable]
        public class UserDataBase : PlayerDataBase
        {
            protected bool _isRemovedAds;
            protected int _totalSoftCurrency;
            private int _totalHardCurrency;
            private int _totalPlay;
        }

        [Serializable]
        public class LevelData
        {
            protected float _process;
            public int _level = 1;
            public int levelMax = 10;
            public int processBase = 1000;
        }

        [Serializable]
        public class UserData : UserDataBase
        {
            protected float _levelPoint = 0.5f;
            internal int nitro;
            internal float shield;
            public LevelData levelData = new LevelData();

            public void SetSample()
            {
                name = "Legacy Player";
                _isRemovedAds = true;
                _totalSoftCurrency = 4321;
                _levelPoint = 2.5f;
                nitro = 7;
                shield = 0.5f;
                levelData._level = 3;
                typeof(UserDataBase).GetField("_totalHardCurrency", System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic).SetValue(this, 12);
                typeof(UserDataBase).GetField("_totalPlay", System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic).SetValue(this, 25);
            }
        }

        [Serializable]
        public class ItemData
        {
            public string id;
            public bool isUnlocked;
            public bool isOwner;
            public bool isSelected;
            public int count = -1;
        }

        [Serializable]
        public class VehicleData : ItemData
        {
            public int upgradeLevel;
        }

        [Serializable]
        public class GameData
        {
            public string version = "1.0.0";
            public long timestamp;
            public UserData userData = new UserData();
            public List<ItemData> characterDatas = new List<ItemData>();
            public List<VehicleData> vehicleDatas = new List<VehicleData>();
        }
    }
}
