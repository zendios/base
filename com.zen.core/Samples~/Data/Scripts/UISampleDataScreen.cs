using System.Collections.Generic;
using Base;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Data sample screen: lists the characters and vehicles, buys / selects / upgrades them through UIPopupSampleItem,
/// edits the player's progress section, saves and shows the save file. Every change goes through Base
/// (DataManager, ItemDatas, ISaveSection), so it is the reference for a game's own data.
/// </summary>
public class UISampleDataScreen : UIScreen
{
    [SerializeField] private RectTransform listRoot;
    [Tooltip("Row template (a Button_Text); hidden, cloned per item.")]
    [SerializeField] private Button rowTemplate;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Button addCoinsButton;
    [SerializeField] private Button playButton;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button showSaveButton;

    private const int CoinsPerClick = 1000;
    private readonly List<GameObject> _rows = new List<GameObject>();

    protected override void Awake()
    {
        base.Awake();
        rowTemplate.gameObject.SetActive(false);
        addCoinsButton.onClick.AddListener(AddCoins);
        playButton.onClick.AddListener(PlayLevel);
        saveButton.onClick.AddListener(Save);
        showSaveButton.onClick.AddListener(ShowSave);
    }

    private void OnEnable()
    {
        DataManager.OnLoaded += Refresh;
        UserDataBase.OnSoftCurrencyChanged += OnSoftCurrencyChanged;
    }

    private void OnDisable()
    {
        DataManager.OnLoaded -= Refresh;
        UserDataBase.OnSoftCurrencyChanged -= OnSoftCurrencyChanged;
    }

    private void Start()
    {
        if (DataManager.IsLoaded)
            Refresh();
    }

    private void OnSoftCurrencyChanged(long changed, long value) => Refresh();

    private void Refresh()
    {
        foreach (var row in _rows)
            Destroy(row);
        _rows.Clear();

        if (!DataManager.IsLoaded)
        {
            statusText.text = "Loading...";
            return;
        }

        var user = DataManager.UserData;
        var progress = SamplePlayerProgress.Current;
        statusText.text = $"Coins: {user.totalSoftCurrency}   Level: {progress.levelData.level}   " +
                          $"Nitro: {progress.nitro}   Plays: {user.totalPlay}" +
                          (DataManager.IsLegacySaveMigrated ? "   (migrated from legacy save)" : "");

        var characters = DataManager.Get<SampleCharacterDatas>();
        var vehicles = DataManager.Get<SampleVehicleDatas>();
        if (characters == null || vehicles == null)
        {
            AddRow("Add SampleCharacterDatas and SampleVehicleDatas to DataManager.itemDatas", null);
            return;
        }

        foreach (var character in characters.list)
        {
            var item = character;
            AddRow($"{item.name}   power {item.power}   {StateOf(item, characters.current == item)}",
                () => OpenCharacter(characters, item));
        }
        foreach (var vehicle in vehicles.list)
        {
            var item = vehicle;
            AddRow($"{item.name}   upgrade {item.upgradeLevel}/{item.maxUpgradeLevel}   {StateOf(item, vehicles.current == item)}",
                () => OpenVehicle(vehicles, item));
        }
    }

    private void AddRow(string label, UnityEngine.Events.UnityAction onClick)
    {
        var row = Instantiate(rowTemplate, listRoot);
        row.gameObject.SetActive(true);
        row.GetComponentInChildren<TMP_Text>().text = label;
        row.interactable = onClick != null;
        if (onClick != null)
            row.onClick.AddListener(onClick);
        _rows.Add(row.gameObject);
    }

    private static string StateOf(ItemData item, bool isCurrent)
    {
        if (isCurrent)
            return "[selected]";
        return item.isOwner ? "[owned]" : $"[{item.price} coins]";
    }

    private void OpenCharacter(SampleCharacterDatas datas, SampleCharacterData item)
    {
        UIPopupSampleItem.Show(item.name, $"Power {item.power}\n{StateOf(item, datas.current == item)}",
            buy: item.isOwner ? null : () => Buy(item),
            select: item.isOwner && datas.current != item ? () => Select(datas, item) : null,
            upgrade: null);
    }

    private void OpenVehicle(SampleVehicleDatas datas, SampleVehicleData item)
    {
        var info = $"Upgrade {item.upgradeLevel}/{item.maxUpgradeLevel}\n{StateOf(item, datas.current == item)}";
        if (item.isOwner && !item.IsMaxUpgrade)
            info += $"\nNext upgrade: {item.upgradePrice} coins";
        UIPopupSampleItem.Show(item.name, info,
            buy: item.isOwner ? null : () => Buy(item),
            select: item.isOwner && datas.current != item ? () => Select(datas, item) : null,
            upgrade: item.isOwner && !item.IsMaxUpgrade ? () => Upgrade(item) : null);
    }

    private void Buy(ItemData item)
    {
        if (!Spend(item.price))
            return;
        item.isUnlocked = true;
        item.isOwner = true;
        DataManager.Save(true);
        UIManager.Toast($"{item.name} bought");
        Refresh();
    }

    private void Select<T>(ItemDatas<T> datas, T item) where T : ItemData
    {
        datas.current = item;
        DataManager.Save(true);
        Refresh();
    }

    private void Upgrade(SampleVehicleData item)
    {
        if (!Spend(item.upgradePrice))
            return;
        item.upgradeLevel++;
        DataManager.Save(true);
        Refresh();
    }

    private static bool Spend(int price)
    {
        var user = DataManager.UserData;
        if (user.totalSoftCurrency < price)
        {
            UIManager.Toast("Not enough coins");
            return false;
        }
        user.totalSoftCurrency -= price;
        return true;
    }

    private void AddCoins()
    {
        DataManager.UserData.totalSoftCurrency += CoinsPerClick;
        Refresh();
    }

    // Writes to UserData (Base) and to the game's own section in one action.
    private void PlayLevel()
    {
        var progress = SamplePlayerProgress.Current;
        DataManager.UserData.totalPlay++;
        progress.nitro++;
        progress.levelData.process += 600;
        if (progress.levelData.isLevelUp)
        {
            progress.levelData.LevelUp();
            UIManager.Toast($"Level up: {progress.levelData.level}");
        }
        DataManager.Save(true);
        Refresh();
    }

    private void Save()
    {
        DataManager.Save(false);
        UIManager.Toast("Saved");
    }

    private void ShowSave()
    {
        var json = JsonUtility.ToJson(DataManager.GameData, true);
        if (json.Length > 1500)
            json = json.Substring(0, 1500) + "\n...";
        UIManager.Message("Save file", json);
    }
}
