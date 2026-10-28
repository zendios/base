using Base;
using Base.Ads;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class DataManager : DataManagerBase
{
    [SerializeField]
    [Tooltip("The game's ItemDatas assets (skins, boosters...): saved and loaded by asset name.")]
    protected List<ScriptableObject> itemDatas = new List<ScriptableObject>();

    /// <summary>The game's ItemDatas assets registered on this DataManager.</summary>
    public static IEnumerable<IItemDatas> ItemDatas =>
        instance != null ? instance.itemDatas.OfType<IItemDatas>() : Enumerable.Empty<IItemDatas>();

    /// <summary>The registered ItemDatas asset of type T (e.g. DataManager.Get&lt;SkinDatas&gt;()), or null.</summary>
    public static T Get<T>() where T : ScriptableObject, IItemDatas => ItemDatas.OfType<T>().FirstOrDefault();

    [Header("Build Version for In Review"), SerializeField]
    protected int bundleVersion = 0;
    public static int BundleVersion
    {
        get
        {
            if (instance != null)
                return instance.bundleVersion;
            return 0;
        }
    }

    public void UpdateVersionInfo()
    {
#if UNITY_EDITOR
        // iOS.buildNumber is a free-form string ("" or "1.0.2"): int.Parse would throw and abort OnValidate/Load.
        if (!int.TryParse(PlayerSettings.iOS.buildNumber, out var iosBuildNumber))
            iosBuildNumber = -1;

        if (bundleVersion != iosBuildNumber || bundleVersion != PlayerSettings.Android.bundleVersionCode)
        {
            PlayerSettings.Android.bundleVersionCode = bundleVersion;
            PlayerSettings.iOS.buildNumber = bundleVersion.ToString();
        }

        EditorUtility.SetDirty(this);
#endif
    }

    public static bool IsForceUpdate
    {
        get
        {
            var config = GameConfig;
            if (config == null)
                return false;

            if (config.inAppUpdate == false && string.IsNullOrEmpty(config.forceVersion))
                return false;

            // Both strings are author-edited ("", "1.0-beta"): TryParse instead of throwing on every call.
            if (Version.TryParse(config.forceVersion, out Version newVersion) == false)
                return false;
            if (Version.TryParse(Application.version, out Version appVersion) == false)
                return false;

            if (newVersion > appVersion)
            {
                Debug.Log("IsForceUpdate: " + appVersion.ToString() + " --> " + newVersion.ToString());
                return true;
            }
            return false;
        }
    }

    public static bool InReview
    {
        get
        {
            if (instance == null || IsLoaded == false)
                return false;


            if (Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.OSXEditor)
                return false;

            if (GameConfig != null && GameConfig.bundleVersion < instance.bundleVersion)
            {
                Debug.LogWarning("!!! -------- GameConfig.bundleVersion: " + GameConfig.bundleVersion + " - " + instance.bundleVersion + " !!!");
                return true;
            }
            return false;
        }
    }

    protected static GameData gameData;
    public static GameData GameData
    {
        get
        {
            return gameData;
        }
        set
        {
            gameData = value;
        }
    }

    public static GameConfig GameConfig
    {
        get
        {
            if (gameData != null && gameData.gameConfig != null)
                return gameData.gameConfig;
            return null;
        }
        set
        {
            if (gameData != null)
                gameData.gameConfig = value;
        }
    }

    public static UserData UserData { get; set; }
    //public static CharacterDatas CharacterDatas = null;

    private static readonly List<ISaveSection> _sections = new List<ISaveSection>();

    /// <summary>
    /// Raised once, during load, when the save file was written by an older Base with BinaryFormatter.
    /// The argument is that save as JSON with the old field names (LegacySaveReader). Base has already copied
    /// userData and the old file is kept next to it as .gd.legacy; fill the game's own data from the JSON here
    /// (GameData.SetItems, GameData.SetSection or the registered sections directly).
    /// </summary>
    public static event Action<string> OnLegacySaveLoaded;

    /// <summary>True when this session's data came from an older BinaryFormatter save.</summary>
    public static bool IsLegacySaveMigrated { get; private set; }

    public delegate void LoadedDelegate();
    public static event LoadedDelegate OnLoaded;
    [Space(10)]
    [SerializeField]
    [Tooltip("Event on data loaded")]
    protected UnityEvent onLoadCompleted = null;
    public static bool IsLoaded { get; private set; }

    protected static DataManager instance { get; set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        // Statics survive Enter Play Mode when Domain Reload is disabled: without this the next session
        // starts with IsLoaded == true, the previous session's data and dead event subscribers.
        instance = null;
        gameData = null;
        UserData = null;
        IsLoaded = false;
        OnLoaded = null;
        _sections.Clear();
        OnLegacySaveLoaded = null;
        IsLegacySaveMigrated = false;
    }

    /// <summary>
    /// Saves the game's own data in Base's save file (see ISaveSection). Register before the save loads;
    /// a section registered after loading is filled at once from the loaded save.
    /// </summary>
    public static void RegisterSection(ISaveSection section)
    {
        if (section == null || _sections.Contains(section))
            return;
        if (_sections.Any(x => x.SaveKey == section.SaveKey))
        {
            Debug.LogError("DataManager: save section key already registered: " + section.SaveKey);
            return;
        }

        _sections.Add(section);
        if (IsLoaded)
            ApplySection(section);
    }

    private static void ApplySection(ISaveSection section)
    {
        var json = GameData?.GetSection(section.SaveKey);
        if (json == null)
            return;
        try
        {
            section.FromJson(json);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        UpdateVersionInfo();
    }
#endif

    protected override void Awake()
    {
        if (instance == null)
        {
            base.Awake();
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    protected void Start()
    {
        // Destroy() is deferred to end of frame, so a duplicate still receives Start(): only the singleton loads.
        if (instance != this)
            return;

        if (loadOnStart && IsLoaded == false)
            StartCoroutine(Load());
    }

    private void OnApplicationPause(bool pause)
    {
        if (pause && saveOnPause)
            Save(true);
    }

#if UNITY_EDITOR
    private void OnApplicationQuit()
    {
        Save(false);
    }
#endif

    public static IEnumerator IELoad()
    {
        if (instance != null)
            yield return instance.Load();
    }

    protected IEnumerator Load()
    {
        if (IsLoaded)
        {
            OnLoaded?.Invoke();
            onLoadCompleted?.Invoke();
            yield break;
        }

        UpdateVersionInfo();

        GameConfig = new GameConfig();
        GameData = new GameData();
        UserData = new UserData();

        var data = LoadJson<GameData>(DebugMode.IsOn);
        string legacyJson = null;
        if (data == null && SaveFileExists())
            data = LoadUnreadableSave(out legacyJson);

        if (data is GameData saveData)
        {
            GameData = saveData;

            // An older or corrupted save deserializes with a null userData: keep the fresh instance instead of crashing below.
            if (GameData.userData != null)
                UserData = GameData.userData;
            else
                GameData.userData = UserData;

            if (legacyJson != null)
                RaiseLegacySaveLoaded(legacyJson);

            GameData.MoveLegacyItems(ItemDatas);
            foreach (var datas in ItemDatas)
                datas.UpdateFromSaveData(GameData.GetItems(datas.name));
            foreach (var section in _sections)
                ApplySection(section);
        }

        if (UserData.versionCurrent == 0)
        {
            UserData.isNew = true;
            UserData.versionCurrent = bundleVersion;
        }
        else if (UserData.versionCurrent != bundleVersion)
        {
            UserData.isNew = false;
            UserData.versionCurrent = bundleVersion;
        }
        UserData.Init();

        IsLoaded = true;
        // Write the migrated data as JSON at once: the old file is already kept as .gd.legacy.
        if (IsLegacySaveMigrated)
            Save(false);
        OnLoaded?.Invoke();
        onLoadCompleted?.Invoke();
        yield return null;
    }

    // Same path as FileData (Base.dll): persistentDataPath/<SAVEFILE>.gd, or the .tmp left by an interrupted save.
    private static string SaveFilePath => System.IO.Path.Combine(Application.persistentDataPath, SAVEFILE + ".gd");

    private static bool SaveFileExists()
    {
        var path = SaveFilePath;
        return System.IO.File.Exists(path) || System.IO.File.Exists(path + ".tmp");
    }

    /// <summary>
    /// The save file exists but is not this Base's JSON. An older Base's BinaryFormatter save is converted
    /// (userData copied, the JSON kept for OnLegacySaveLoaded); anything else is only backed up. Either way the
    /// original file is copied aside first, so the next save can never destroy the player's data.
    /// </summary>
    private GameData LoadUnreadableSave(out string legacyJson)
    {
        legacyJson = null;
        var path = SaveFilePath;
        if (!System.IO.File.Exists(path))
            path += ".tmp";

        byte[] bytes;
        try
        {
            bytes = System.IO.File.ReadAllBytes(path);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            return null;
        }

        if (!LegacySaveReader.TryReadJson(bytes, out var json, out var error))
        {
            var backup = BackupSaveFile(path, ".unreadable");
            Debug.LogError($"DataManager: save file unreadable ({error}), kept as {backup}");
            ZenAnalytics.LogEvent("save_unreadable", new Dictionary<string, object> { { "error", error } });
            return null;
        }

        BackupSaveFile(path, ".legacy");
        var data = new GameData();
        try
        {
            JsonUtility.FromJsonOverwrite(json, data);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            ZenAnalytics.LogEvent("save_legacy_failed", new Dictionary<string, object> { { "error", ex.Message } });
            return null;
        }

        legacyJson = json;
        IsLegacySaveMigrated = true;
        Debug.Log("DataManager: older BinaryFormatter save converted to JSON");
        return data;
    }

    private static void RaiseLegacySaveLoaded(string json)
    {
        var result = "ok";
        try
        {
            OnLegacySaveLoaded?.Invoke(json);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            result = ex.GetType().Name;
        }
        ZenAnalytics.LogEvent("save_legacy_migrated", new Dictionary<string, object> { { "result", result } });
    }

    // Copies the file to <file><suffix>; an earlier backup with that name is never overwritten.
    private static string BackupSaveFile(string path, string suffix)
    {
        var backup = path + suffix;
        if (System.IO.File.Exists(backup))
            backup += "-" + DateTime.UtcNow.Ticks;
        try
        {
            System.IO.File.Copy(path, backup);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
        return backup;
    }

    /// <summary>
    /// Saves game data Asynchronously to avoid frame drops (Use for Checkpoints/Pause)
    /// </summary>
    /// <param name="async"></param>
    public static void Save(bool async)
    {
        if (instance == null)
        {
            Debug.LogError("DataManager NULL");
            return;
        }

        if (IsLoaded == false)
        {
            Debug.LogError("DataManager IsLoaded: " + IsLoaded);
            return;
        }

        if (GameData == null)
        {
            Debug.LogError("DataManager GameData NULL");
            return;
        }

        GameData.version = Application.version;
        GameData.timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        GameData.userData = UserData;

        foreach (var datas in ItemDatas)
            GameData.SetItems(datas.name, datas.saveList);
        foreach (var section in _sections)
            GameData.SetSection(section.SaveKey, section.ToJson());

        if (async)
        {
            instance.SaveJsonAsync<GameData>(GameData, (ex) =>
            {
                ZenToast.ShowNotice(ex.Message);
                Debug.LogError("SaveJsonAsync IsLoaded: " + ex.Message);
            }, DebugMode.IsOn);
        }
        else
        {
            instance.SaveJson<GameData>(GameData, (ex) =>
            {
                ZenToast.ShowNotice(ex.Message);
                Debug.LogError("SaveJson IsLoaded: " + ex.Message);
            }, DebugMode.IsOn);
        }
    }

    public void UnlockAll()
    {
        UserData.totalSoftCurrency += 100000;
        UserData.totalHardCurrency += 1000;
        Save(true);

        ZenToast.ShowNotice("Everything is unlocked!");
    }

    /// <summary>Deletes the save file and the trial times (stored in PlayerPrefs under each item's id).</summary>
    public override void ClearAllData()
    {
        base.ClearAllData();
        foreach (var itemDatas in ItemDatas)
            foreach (var item in itemDatas.saveList)
                if (!string.IsNullOrEmpty(item.id))
                    PlayerPrefs.DeleteKey(item.id);
        PlayerPrefs.Save();
    }

    public void ResetData()
    {
        ClearAllData();

        //TODO: Reset Data to build;
        //characterDatas.ResetData();

        UpdateVersionInfo();

        Debug.Log("ResetData DONE!");
    }
}

[Serializable]
public class GameData
{
    public GameData Clone()
    {
        return MemberwiseClone() as GameData;
    }

    public GameData()
    {
        this.version = Application.version;
        this.timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    // Meta data for version control (useful for future updates)
    public string version;
    public long timestamp;
    public UserData userData = new UserData { name = "YOU" };

    public GameConfig gameConfig = new GameConfig();

    /// <summary>Saved state of the game's ItemDatas assets, by asset name.</summary>
    public List<ItemDatasSave> itemDatas = new List<ItemDatasSave>();

    // Saves made before com.zen.core 1.1.0 kept these two lists; MoveLegacyItems moves them into itemDatas once.
    public List<ItemData> boosterDatas = new List<ItemData>();
    public List<ItemData> outfitDatas = new List<ItemData>();

    /// <summary>The game's own data (ISaveSection), as JSON by key.</summary>
    public List<SectionSave> sections = new List<SectionSave>();

    public List<ItemData> GetItems(string key) => itemDatas.FirstOrDefault(x => x.key == key)?.items;

    /// <summary>The JSON saved under key by an ISaveSection, or null.</summary>
    public string GetSection(string key) => sections?.FirstOrDefault(x => x.key == key)?.json;

    public void SetSection(string key, string json)
    {
        sections ??= new List<SectionSave>();
        var save = sections.FirstOrDefault(x => x.key == key);
        if (save == null)
            sections.Add(save = new SectionSave { key = key });
        save.json = json;
    }

    public void SetItems(string key, List<ItemData> items)
    {
        var save = itemDatas.FirstOrDefault(x => x.key == key);
        if (save == null)
            itemDatas.Add(save = new ItemDatasSave { key = key });
        save.items = items;
    }

    /// <summary>Old saves: boosterDatas / outfitDatas go to the registered asset of type BoosterDatas / OutfitDatas.</summary>
    public void MoveLegacyItems(IEnumerable<IItemDatas> registered)
    {
        void Move(List<ItemData> legacy, string typeName)
        {
            var target = registered.FirstOrDefault(d => d.GetType().Name == typeName);
            if (legacy == null || legacy.Count == 0 || target == null || GetItems(target.name) != null)
                return;
            SetItems(target.name, legacy);
            legacy.Clear();
        }
        Move(boosterDatas, "BoosterDatas");
        Move(outfitDatas, "OutfitDatas");
    }
}

[Serializable]
public class ItemDatasSave
{
    public string key;
    public List<ItemData> items = new List<ItemData>();
}

[Serializable]
public class SectionSave
{
    public string key;
    public string json;
}
