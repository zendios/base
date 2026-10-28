# Upgrading a game that has Base in `Assets/Base`

For games made before Base was a package (`Assets/Base`, package.json `base` 0.0.1 or `com.zen.base` 1.0): sources
and the game's own code mixed in one folder, BinaryFormatter saves, global `GameState`. First done on V02D
(see its `Docs/base-upgrade-notes.md`); the same steps apply to every older game.

**Priority: no player loses data.** Never ship before the migration test below passes with a save from the released build.

## 0. Before touching anything

1. Close the project in Unity. Work on a new branch.
2. **Make a save fixture with the released code**: an editor method that builds a mid-game `GameData` (currency, Remove
   Ads, owned / selected items, upgrades, level progress) and writes it with `BinaryFormatter` to
   `Docs/fixtures/legacy-save.gd`. Keep a real device save too (`/Android/data/<package>/files/<package>.gd`).
3. List the game's types that live in `Assets/Base`: compare each `.meta` GUID with the package's. Same GUID = the
   package replaces it; other GUID = the game's (data catalogues, UI, sounds, VFX) or an old Base file.
4. Read the old `DataManager`, `UserData`, `GameConfig`, `GameEnum`: they usually hold game fields and enums.

## 1. Files

- Move the game's files to the game's folder (`git mv` with the `.meta`, or AssetDatabase.MoveAsset): GUIDs stay, every
  reference stays. Delete `Assets/Base`.
- Old Base files with no package twin and no use (old ads scripts, old DataManager, `FirebaseRemote`, `TokenAsset`):
  delete. Copy the IDs of the old `AdIdData` assets first.
- `AdConfig.asset` → `Assets/Resources` (keep its .meta).

## 2. Packages and symbols

- `Packages/manifest.json`: scope `com.zen` on OpenUPM, `com.zen.core` and `com.zen.plugins.adzative` at the same version.
- Remove the `USE_*` symbols added by hand; keep the game's own (e.g. `BCG_RCC`). `DOTWEEN` is needed (Base adds it
  when the Editor opens; add it yourself for a batchmode-only first compile).
- Firebase, AdMob, mediation: Base > Hub > Setup > SDKs, versions Base was tested with.

## 3. Data (the part that keeps players' progress)

Base's `UserData`, `GameConfig`, `GameData` are in the package: the game does not edit them. The game's data goes in:

| Old | New |
|---|---|
| game fields on `UserData` (level point, nitro...) | a `[Serializable]` class with **the same field names**, saved by an `ISaveSection` |
| `DataManager.StageDatas / VehicleDatas...` | a static class of the game (e.g. `GameDatas`) holding copies of the catalogues; its `ISaveSection` saves their save lists |
| game fields on `GameConfig` | a ScriptableObject of the game in Resources (+ `ZenRemote` for remote values, same keys) |
| `GameMode` (game values) | the game's own enum with the **same numbers** (Base has a `GameMode` too: add `-nowarn:0436` to `Assets/csc.rsp`) |

Make the section's JSON use **the old GameData field names** (`stageDatas`, `vehicleDatas`, `userData`...): then
`DataManager.OnLegacySaveLoaded += section.FromJson` converts the old save with no extra code. Base itself copies
`userData` (currency, VIP, Remove Ads, counters) and keeps the old file as `<package>.gd.legacy`.
Fields that were `protected` without `[SerializeField]` were saved by BinaryFormatter but not by JsonUtility: add
`[SerializeField]`. Register the section and the handler from
`[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]`.
Other files the game wrote with `FileData.Save` (BinaryFormatter): switch to `SaveJson` / `LoadJson` and fall back to
`LegacySaveReader.TryReadJson(File.ReadAllBytes(path), ...)` once.

## 4. Code

| Old | New |
|---|---|
| `GameState`, `GameStateManager`, `GameStateListener` (global) | namespace `Base`: add `using Base;` |
| `DataManager.AdConfig`, `AdsManager.Config` | `AdConfig.Instance` |
| `AdsManager.LoadInter(...)` | `StartCoroutine(AdsManager.IELoadInter(...))` |
| `AdsManager.InReview` | `DataManager.InReview` |
| `FirebaseManager.LogEvent`, `FirebaseRemote` | `ZenAnalytics.LogEvent`, `ZenRemote` |
| `Base.Observer` (EventDispatcher) | removed: a C# `event` in the class that used it |
| `PopupBase / PopupMessage / PopupIAP` | `UIPopupBase<T> / UIPopupMessage / UIPopupIAP` |

Older generations (`AdsManager.ShowInterstitial / ShowVideoReward`, `AdsToast`, `UIToast`): `ShowInter / ShowReward`
take `Action<AdType, AdState>` (**parameters swapped**), `AdToast`, `ZenToast`; `CurrencyManager.AddSoft` takes an
`itemName` second. Fix these one by one: a wrong argument order can still compile.

## 5. Scenes and prefabs

- Scripts of Base.dll that changed namespace lose their link (`GameStateManager`, `GameStateListener`): point the
  references to the `Base.*` class (re-add the component, or replace the script fileID in the YAML).
- Scripts that moved into the package with a new GUID (`IAPButtonExtent`, `IAPManager`): point them to the package
  script, then fill new fields (IAP: `productId` from the CodelessIAPButton).
- Splash: Package Manager > Base > Samples > Scenes. **Set DataManager.bundleVersion to the game's version code before
  saving**: its OnValidate writes it into Player Settings.
- Ad IDs: Base > Hub > Ad IDs (old asset `Admob_Inter_Default` → `AdmobInterDefault`, `Zative_*` → `AdZative*`).
- Scan prefabs and build scenes for missing scripts (`GameObjectUtility.GetMonoBehavioursWithMissingScriptCount`) and
  compare with the original commit: only fix what the upgrade broke.

## 6. Done when

- Batchmode compile: 0 errors, no new warnings.
- Migration test with the fixture and a real save: every value (currency, Remove Ads, items, upgrades, progress), then
  save → load again.
- Base > Hub > Setup (Fix all) and Build Check clean; ads, IAP, Remove Ads tried in the Editor and on a device.
- On release, watch `save_legacy_migrated` / `save_legacy_failed` / `save_unreadable` in Analytics.
