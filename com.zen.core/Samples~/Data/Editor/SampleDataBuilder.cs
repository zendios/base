using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// zen-unity only: builds the data sample (assets, UIPopupSampleItem prefab, 98_DataSample scene) from the Base
/// prefabs, so it can be rebuilt after a prefab changes. Batchmode: -executeMethod SampleDataBuilder.Build
/// </summary>
public static class SampleDataBuilder
{
    private const string Root = "Assets/Samples/Base/Data";
    private const string UiPrefabs = "Packages/com.zen.core/Runtime/UI/Prefabs/";

    [MenuItem("Base/Samples/Rebuild Data Sample")]
    public static void Build()
    {
        Directory.CreateDirectory(Root + "/Datas");
        Directory.CreateDirectory(Root + "/Resources");
        Directory.CreateDirectory(Root + "/Scenes");

        var characters = BuildCharacters();
        var vehicles = BuildVehicles();
        BuildPopup();
        BuildScene(characters, vehicles);
        AssetDatabase.SaveAssets();
        Debug.Log("SampleDataBuilder: done");
    }

    private static SampleCharacterDatas BuildCharacters()
    {
        var datas = LoadOrCreate<SampleCharacterDatas>(Root + "/Datas/SampleCharacterDatas.asset");
        datas.list = new List<SampleCharacterData>
        {
            new SampleCharacterData { id = "character_rookie", name = "Rookie", index = 0, price = 0, power = 10, isUnlocked = true, isOwner = true },
            new SampleCharacterData { id = "character_racer", name = "Racer", index = 1, price = 500, power = 25 },
            new SampleCharacterData { id = "character_legend", name = "Legend", index = 2, price = 1500, power = 60 },
        };
        EditorUtility.SetDirty(datas);
        return datas;
    }

    private static SampleVehicleDatas BuildVehicles()
    {
        var datas = LoadOrCreate<SampleVehicleDatas>(Root + "/Datas/SampleVehicleDatas.asset");
        datas.list = new List<SampleVehicleData>
        {
            new SampleVehicleData { id = "vehicle_hatchback", name = "Hatchback", index = 0, price = 0, upgradePrice = 100, isUnlocked = true, isOwner = true },
            new SampleVehicleData { id = "vehicle_pickup", name = "Pickup", index = 1, price = 800, upgradePrice = 200 },
            new SampleVehicleData { id = "vehicle_supercar", name = "Supercar", index = 2, price = 2500, upgradePrice = 500, maxUpgradeLevel = 10 },
        };
        EditorUtility.SetDirty(datas);
        return datas;
    }

    private static void BuildPopup()
    {
        var popupBase = AssetDatabase.LoadAssetAtPath<GameObject>(UiPrefabs + "Popup_Base.prefab");
        var root = (GameObject)PrefabUtility.InstantiatePrefab(popupBase);
        var closeButton = root.GetComponentInChildren<Button>(true);
        var popup = root.AddComponent<UIPopupSampleItem>();

        var content = FindChild(root.transform, "Content");
        var body = NewRect("Body", content);
        Stretch(body);
        var layout = body.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(48, 48, 48, 48);
        layout.spacing = 24;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var title = NewText(body, "Title", 56, 90);
        var info = NewText(body, "Info", 38, 200);
        var buy = NewButton(body, "Buy", 120);
        var select = NewButton(body, "Select", 120);
        var upgrade = NewButton(body, "Upgrade", 120);

        var so = new SerializedObject(popup);
        so.FindProperty("closeButton").objectReferenceValue = closeButton;
        so.FindProperty("titleText").objectReferenceValue = title;
        so.FindProperty("infoText").objectReferenceValue = info;
        so.FindProperty("buyButton").objectReferenceValue = buy;
        so.FindProperty("selectButton").objectReferenceValue = select;
        so.FindProperty("upgradeButton").objectReferenceValue = upgrade;
        so.ApplyModifiedPropertiesWithoutUndo();

        root.name = nameof(UIPopupSampleItem);
        PrefabUtility.SaveAsPrefabAsset(root, Root + "/Resources/UIPopupSampleItem.prefab");
        Object.DestroyImmediate(root);
    }

    private static void BuildScene(SampleCharacterDatas characters, SampleVehicleDatas vehicles)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var dataManager = Instantiate("Packages/com.zen.core/Runtime/DataManager/DataManager.prefab");
        var so = new SerializedObject(dataManager.GetComponent<DataManager>());
        var list = so.FindProperty("itemDatas");
        list.arraySize = 2;
        list.GetArrayElementAtIndex(0).objectReferenceValue = characters;
        list.GetArrayElementAtIndex(1).objectReferenceValue = vehicles;
        so.ApplyModifiedPropertiesWithoutUndo();

        Instantiate("Packages/com.zen.core/Runtime/Extensions/GameStateManager.prefab");
        new GameObject(nameof(SampleDataSetup)).AddComponent<SampleDataSetup>();
        Instantiate(UiPrefabs + "Event_System.prefab");
        var uiCamera = Instantiate(UiPrefabs + "UI_Camera.prefab").GetComponentInChildren<Camera>();
        var uiManager = Instantiate(UiPrefabs + "UIManager.prefab");
        var canvas = uiManager.GetComponent<Canvas>();
        if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera && uiCamera != null)
            canvas.worldCamera = uiCamera;

        BuildScreen(uiManager.transform);

        EditorSceneManager.SaveScene(scene, Root + "/Scenes/98_DataSample.unity");
    }

    private static void BuildScreen(Transform parent)
    {
        var screen = NewRect(nameof(UISampleDataScreen), parent);
        Stretch(screen);
        var component = screen.gameObject.AddComponent<UISampleDataScreen>();
        screen.GetComponent<UIAnim>().playAnimAtStart = true;

        var layout = screen.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(40, 40, 120, 40);
        layout.spacing = 24;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var status = NewText(screen, "Status", 36, 110);

        var actions = NewRect("Actions", screen);
        actions.gameObject.AddComponent<LayoutElement>().preferredHeight = 120;
        var row = actions.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.spacing = 16;
        row.childControlWidth = row.childControlHeight = true;
        row.childForceExpandWidth = row.childForceExpandHeight = true;
        var addCoins = NewButton(actions, "+1000 Coins", 120);
        var play = NewButton(actions, "Play Level", 120);
        var save = NewButton(actions, "Save", 120);
        var showSave = NewButton(actions, "Show Save", 120);

        var listRoot = NewRect("List", screen);
        listRoot.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;
        var list = listRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        list.spacing = 12;
        list.childControlWidth = list.childControlHeight = true;
        list.childForceExpandWidth = true;
        list.childForceExpandHeight = false;
        var rowTemplate = NewButton(listRoot, "Row", 110);
        rowTemplate.GetComponentInChildren<TMP_Text>().alignment = TextAlignmentOptions.MidlineLeft;

        var so = new SerializedObject(component);
        so.FindProperty("listRoot").objectReferenceValue = listRoot;
        so.FindProperty("rowTemplate").objectReferenceValue = rowTemplate;
        so.FindProperty("statusText").objectReferenceValue = status;
        so.FindProperty("addCoinsButton").objectReferenceValue = addCoins;
        so.FindProperty("playButton").objectReferenceValue = play;
        so.FindProperty("saveButton").objectReferenceValue = save;
        so.FindProperty("showSaveButton").objectReferenceValue = showSave;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static TMP_Text NewText(Transform parent, string name, float size, float height)
    {
        var text = Instantiate(UiPrefabs + "Text_Base.prefab", parent).GetComponentInChildren<TMP_Text>();
        text.gameObject.name = name;
        text.fontSize = size;
        text.text = name;
        text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.Center;
        text.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
        return text;
    }

    private static Button NewButton(Transform parent, string label, float height)
    {
        var button = Instantiate(UiPrefabs + "Button_Text.prefab", parent).GetComponent<Button>();
        button.name = "Button_" + label.Replace(" ", "").Replace("+", "");
        button.GetComponentInChildren<TMP_Text>().text = label;
        button.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
        return button;
    }

    private static GameObject Instantiate(string path, Transform parent = null)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
            throw new FileNotFoundException(path);
        return (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static Transform FindChild(Transform root, string name)
    {
        foreach (var child in root.GetComponentsInChildren<Transform>(true))
            if (child.name == name)
                return child;
        throw new KeyNotFoundException(name);
    }

    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null)
            return asset;
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }
}
