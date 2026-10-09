# Base architecture and usage

How a game built on `com.zen.core` is put together, and the API of each part. Signatures are copied from the code
(1.2.7); when the code and this file differ, the code wins. Naming and folders: [ZPAS.md](ZPAS.md). Rules for AI
agents: [AGENTS.md](../AGENTS.md).

## 1. Layers

```
Game (Assets/)          scenes, screens (UIScreen), popups, gameplay, Resources/ZenAdIds + AdConfig + ZenIapSettings
com.zen.core            managers (prefabs), services, UI framework, ads waterfall, IAP, Firebase, Base > Hub
Base.dll (zen-base)     GameStateManager, DataManagerBase, ItemData(s), UIAnim / UIAnimManager, enums
SDKs                    AdMob, EDM4U, DOTween (required); Unity IAP, Firebase, AppsFlyer, Play Review / Update (optional)
```

Optional SDKs switch their part on with a symbol Base sets by itself (`USE_IN_APP_PURCHASE`, `USE_FIREBASE`,
`USE_APPSFLYER`, `USE_IN_APP_REVIEW`, `USE_IN_APP_UPDATE`). Inside the package the parts never call each other
directly: they go through the static hooks `ZenAnalytics`, `ZenRemote`, `ZenStore`, `ZenToast` (section 8), so a
missing SDK only turns calls into no-ops.

**Who owns a value** (highest layer wins): Firebase Remote Config > the game's `Assets/Resources` assets and prefab
variants > the package defaults > the package code and prefabs (read-only).

## 2. A scene set

| Prefab (package) | Lifetime | Put it in |
|---|---|---|
| `ADsManager` / `ADsManager_Portrait` (AdsManager + AdSplash + AdToast + Admob units + Canvas_Base) | DontDestroyOnLoad | the splash scene (build index 0) |
| `DataManager` | DontDestroyOnLoad | the splash scene |
| `GameStateManager` (Extensions/) | DontDestroyOnLoad | the splash scene; every `GameStateManager.*` setter throws without it |
| `FirebaseManager`, `IAPManager` | DontDestroyOnLoad | the splash scene (only do something with their SDK) |
| `UnityMainThreadDispatcher` | DontDestroyOnLoad | nothing: created from `Resources` before the first scene |
| `UIManager` (UIManager + UIAnimManager + CanvasScaler + ScreenUtils) | per scene | every game scene: the root of its screens and popups |
| `Event_System`, `UI_Camera` | per scene | every game scene |
| `CurrencyManager` | per scene | the scenes with a currency HUD |

`AdSplash` (inside ADsManager) is the bootstrapper. Its `Start` runs, in order: mute audio, `ATTHelper.IECheck()`,
`DataManager.IELoad()`, `UMP.Instance.IECheck()`, `AppsFlyerHelper.IEInit()`, `ZenRemote.Init()` + `Fetch()`, in-app
update check, `AdsManager.IEInit()`, preload of the FTUE open interstitial, then loads `loadSceneMainIndex` (Single) and
`loadSceneGameIndex` (Additive, 0 = none), fades audio in, shows the open interstitial, `AdsManager.LoadAds()`.
A game needs no code for any of this: it starts in its main scene with data loaded and ads initialised.

Sample scenes (Package Manager > Base > Samples): `00_SplashScreen_Portrait` (ADsManager_Portrait, DataManager,
FirebaseManager, IAPManager, UnityMainThreadDispatcher) and `99_SampleScene` (UIManager, Canvas_Base_Portrait,
UI_Camera, Event_System).

## 3. Game state: `GameStateManager` (Base.dll, namespace `Base`)

One global state machine. Every change logs `StateChanged: last --> current` and raises the event.

```csharp
public enum GameState { None, Main, Load, Init, Ready, Play, Pause, ReviveContinue, ReviveCheckPoint, WaitLose, Lose,
                        WaitWin, Win, Restart, Next, Shop, Tutorial, Resume, Quit, ReOpen, PassByAd, Other = 999 }

public static GameState CurrentState { get; }      public static GameState Last { get; }
public delegate void StateDelegate(GameState current, GameState last, object data = null);
public static event StateDelegate OnGameStateChanged;

// setters (all public static void)
Main(bool force = false, object data = null)  Load(data)  Init(data)  Ready(data)  Play(data)  Pause()
Restart(data)  Revive(bool isCheckpoint = false, object data = null)  WaitLose(data)  Lose(data)
WaitWin(data)  Win(data)  Resume(data)  Next(data)  Shop(data)  Quit(data)  Other(data)
```

Intended flow: `Main → Load → Init → Ready → Play → (Pause / Resume) → WaitLose → Lose` (or `Revive → Play`), or
`WaitWin → Win → Next → Load`; `Restart → Load`.

```csharp
void OnEnable()  => GameStateManager.OnGameStateChanged += OnState;
void OnDisable() => GameStateManager.OnGameStateChanged -= OnState;
void OnState(GameState current, GameState last, object data)
{
    if (current == GameState.Play) hud.Show();
}
// a level ends
GameStateManager.WaitWin(); ... GameStateManager.Win();
```

`GameStateListener` (component): `GameState state`, `UnityEvent onStateChanged`, `bool listeningOnDisable = true`:
Inspector-only reactions to one state, no code.

## 4. Data: `DataManager` (global namespace, `: DataManagerBase`)

One JSON save file named after `Application.identifier`, holding `GameData { version, timestamp, UserData userData,
GameConfig gameConfig, List<ItemDatasSave> itemDatas }`. Loads on `Start` (`loadOnStart`), saves on pause
(`saveOnPause`) and, in the Editor, on quit.

```csharp
public static IEnumerator IELoad();                 // AdSplash runs it; wait for it before reading data
public static bool IsLoaded { get; }
public static event LoadedDelegate OnLoaded;        // delegate void LoadedDelegate()
public static void Save(bool async);

public static UserData   UserData   { get; set; }   // : UserDataBase : PlayerDataBase
public static GameConfig GameConfig { get; set; }   // : GameConfigBase (plain serializable class, saved in GameData)
public static GameData   GameData   { get; set; }

public static IEnumerable<IItemDatas> ItemDatas { get; }             // the inspector list of ScriptableObjects
public static T Get<T>() where T : ScriptableObject, IItemDatas;     // one of them by type
public static int  BundleVersion { get; }
public static bool IsForceUpdate { get; }           // GameConfig.forceVersion vs Application.version
public static bool InReview      { get; }           // GameConfig.bundleVersion < inspector bundleVersion; false in Editor
```

**UserDataBase** (the game's `UserData` adds its own fields): `isNew`, `isRemovedAds`, `isVIP`, `totalSoftCurrency`,
`totalHardCurrency`, `versionInstall`, `versionCurrent`, `session`, `loginDay`, `totalPlay`, `totalWin`, `totalLose`,
`totalTimePlay`, `currentLevel`, `adBanner`, `adInterstitial`, `adRewarded`, `adRewardSkipped`, `adTotal`, `abTesting`,
`ua_*`, `winStreak`, `loseStreak`, `lifeTimeValue`; events `OnVIPChanged(bool)`, `OnRemovedAdsChanged(bool)`,
`OnSoftCurrencyChanged(long changed, long value)`, `OnHardCurrencyChanged(...)`; `Init()`, `GetDaysSinceInstall()`,
`CheckDailyLogin()`. Setters forward to `ZenAnalytics.SetUser` as user properties.

**GameConfigBase**: `bundleVersion`, `forceVersion`, `inAppUpdate`, `FTUE_Play`, `FTUE_Character`, `FTUE_Level`,
`FTUE_LevelVariant`, `reviveType`, `reviveCountdown`, `reviveFree`, `reviveCountMax`...; filled from Remote Config.

**Items** (skins, boosters, levels...): a `ScriptableObject` per catalogue.

```csharp
[Serializable] public class SkinData : ItemData { public Sprite icon; }     // ItemData: id, name, price, currencyType,
[CreateAssetMenu] public class SkinDatas : ItemDatas<SkinData> { }           // unlock, isUnlocked, isOwner, isSelected, count...
// add the SkinDatas asset to DataManager.itemDatas (inspector); it is saved by asset name
var skins = DataManager.Get<SkinDatas>();
skins.current = skins.GetById("skin_02"); skins.SetChanged(skins.current);   // raises ItemDatas<SkinData>.OnChanged
```

`ItemDatas<T>`: `current`, `favorite`, `list`, `GetById`, `GetByname`, `GetNotUnlocked`, `GetUnlockedBy(CurrencyType)`,
`GetOwnerBy`, `Add(id, count)`, `ResetData()`, static `OnChanged(T current)`.
`UpgradeItem` (level / price / value curves) and `LevelData` (progress, level up) are the other save records.

`enum CurrencyType { Level = 0, Ad = 1, Soft = 2, Hard = 3, Booster = 4, Skin = 5, VIP = 999, Other = 1000 }`

## 5. UI: `UIAnimManager` → `UIManager` → screens, popups, toast, message

**UIAnim** (Base.dll, on every view): show / hide tween. `animationIn` (SlideIn / FadeIn / None) with `positionStart`,
`easeIn`, `timeAnimationIn`; `animationOut` with `positionOut`...; `playAnimAtStart`, `playWithParent`,
`destroyOnHide`, `navigation` (None / Screen / Popup); `status` (IsHide / IsShowing / IsShow / IsHiding).
Fade In / Fade Out always keep the view at its own place (Parent Position). `Show(onStart, onCompleted, onHideCompleted)`,
`Hide(onCompleted, force, destroyOnDone)`.

**UIAnimManager** (Base.dll, on the UIManager prefab root): `static Transform RootTransform` (where loaded popups and
screens are instantiated), `RootRectTransform`, `ScreenList`, `PopupList`, `GetPosBy(rect, MovePosition)`.
The newest one in a loaded scene replaces the previous one.

**UIManager** (per scene; `UIManager.Instance` finds or creates one; `UIManager.prefab` has it with UIAnimManager,
CanvasScaler and ScreenUtils). Every step is a `virtual` method, so a game may subclass it.

```csharp
public static T ShowScreen<T>(bool addToHistory = true) where T : UIScreen;   // scene screen, else Resources/<T>
public static bool Back();                                 // top popup → screen.OnBack → previous screen → OnBackAtRoot
public static T ShowPopup<T>(bool destroyOnHide = true) where T : UIPopupBase<T>;
public static void ClosePopup();  public static void CloseAllPopups();
public static void Toast(string message);                  // ZenToast (AdToast when present, else the log)
public static UIPopupMessage Message(string title, string message, Action onConfirm = null, string confirmLabel = "OK",
                                     Action onCancel = null, string cancelLabel = null);
public static event Action<UIScreen> OnScreenChanged;  public static event Action<UIPopup, bool> OnPopupChanged;
public static event Action OnBackAtRoot;                   // nothing left to close: ask "Quit?"
```

Views: `UIView` (`[RequireComponent(typeof(UIAnim))]`: `Open(onShown)`, `Close(onHidden)`, `IsVisible`, hooks
`OnShow / OnShown / OnHide / OnHidden`, `virtual bool OnBack()`), `UIScreen : UIView` (registers itself; the scene
screen whose UIAnim has `playAnimAtStart` is the start screen), `UIPopup : UIView` (on the popup stack while enabled;
back closes it), `UIPopupBase<T> : UIPopup` (one instance from `Resources/<ClassName>.prefab`: `static T Show()`,
`static void Hide()`, `static T Instance`, `closeButton`).

```csharp
public class UIScreenHome : UIScreen            // prefab UIScreenHome (in the scene, or Resources/UIScreenHome)
{
    protected override void OnShown() => UIManager.Toast("Welcome");
}
public class UIPopupShop : UIPopupBase<UIPopupShop> { }   // prefab Resources/UIPopupShop (variant of Popup_Base)

UIManager.ShowScreen<UIScreenHome>();
UIPopupShop.Show();                                       // or UIManager.ShowPopup<UIPopupShop>()
UIManager.Message("Quit?", "Progress is saved.", onConfirm: Application.Quit, confirmLabel: "Quit",
                  onCancel: null, cancelLabel: "Stay");
```

Prefab building blocks (UI/Prefabs): `Canvas_Base` / `Canvas_Base_Portrait` (CanvasScaler + ScreenUtils),
`Popup_Base` (UIAnim root + dimmed background + content pane: make popups as variants of it), `Button_Base` →
`Button_Icon` / `Button_Text` → `Button_CTA`, `Icon_Base`, `Text_Base`, `Event_System`, `UI_Camera`,
`UIManagerDebug` (a canvas with buttons that call UIManager: drop it under UIManager to try things).

**Screens and ratios**: `ScreenUtils` on each root canvas uses Expand with `landscapeReference` 1920×1080 /
`portraitReference` 1080×1920 and `tabletMaxScale` 1.3; `static bool IsPortrait`, `static event Action<bool>
OnOrientationChanged`. `OrientationLayout` saves a pose per orientation; put HUD under a `SafeArea`.

## 6. Ads: `AdsManager` (namespace `Base.Ads`)

`ADsManager.prefab`: AdsManager + one child `AdBase` per placement (`Admob_Inter_Default`, `AdZative_Inter_Default`...).
IDs come from `Resources/ZenAdIds` (one row per `PlacementType`, Android / iOS lists in waterfall order; a row without
IDs loads Google's test IDs), behaviour from `Resources/AdConfig` (`AdConfig.Instance`; `AdConfigRemote` applies
Remote Config on top). Both are edited in **Base > Hub**.

```csharp
public enum AdType  { Inter, Reward, Banner, AppOpen, Mrec, RewardInter, InGame, Native }
public enum AdState { None, LoadRequest, LoadAvailable, LoadNotAvailable, LoadTimeOut, Offer, ShowRequest, Show,
                      ShowFailed, ShowSuccess, ShowNotAvailable, NotTime, Click, Close, Cancel, Exception }
public enum PlacementType { None = 0, AdmobAppOpenDefault = 10, AdmobBannerDefault = 20, AdmobInterDefault = 30,
    AdmobInterOpen = 31, AdmobMrecDefault = 40, AdmobMrecIAP = 41, AdmobRewardDefault = 50, AdZativeAppOpen = 110,
    AdZativeBanner = 120, AdZativeInterDefault = 130, AdZativeInterOpen = 131, AdZativeMrecDefault = 140,
    AdZativeMrecIAP = 141, AdZativeMrecOpen = 142, AdZativeRewardDefault = 150, AdmobMrecOpen = 42 }
public enum ShowLoadingMode { None, Toast, Full }           // in AdBase

public static bool IsInitialized { get; }   public static bool IsRemovedAds { get; }   // VIP, Remove Ads or InReview
public static bool IsTimeToShowAds { get; } public static float SecondsUntilAdAllowed { get; }
public static IEnumerator IEInit(bool initAds = false);     public static void LoadAds();
public static void ShowInter(Action<AdType, AdState> callback, string itemName, float delayShow = 0f, bool forceShow = false);
public static void ShowReward(Action<AdType, AdState> callback, string itemName,
                              ShowLoadingMode showLoadingMode = ShowLoadingMode.Toast, float delayShow = 0f);
public static void Load(PlacementType p, Action<AdType, AdState> cb, string itemName, ShowLoadingMode mode);
public static void Show(PlacementType p, Action<AdType, AdState> cb, string itemName, float delayShow);
public static void LoadShow(PlacementType p, Action<AdType, AdState> cb, string itemName,
                            ShowLoadingMode mode = ShowLoadingMode.Toast, float delayShow = 0f);
public static bool CheckAvailable(PlacementType p, string item, bool loadIfNotAvaiable = false);
public static void SuppressNextAppOpen(string reason);
// IE* variants of each (IEShowInter, IEShowReward, IELoadShow...) for coroutines
```

```csharp
AdsManager.ShowInter((type, state) => NextLevel(), "level_end");          // state == NotTime: too soon, callback still runs
AdsManager.ShowReward((type, state) =>
{
    if (state == AdState.ShowSuccess) CurrencyManager.AddSoft(100, "reward_coins", button.transform);
}, "revive");
AdBase.OnAnyStateChanged += (ad, state) => Debug.Log($"{ad.placement} {state}");
```

Timing rules (`AdConfig`): `adTimePlayToShow`, `adTimeBetween`, `adInterOnPlay / OnStart / OnComplete / OnFTUE`,
`adBannerReload`, flows `adInterFlow` etc. (`AdFlow { OnlyNative, OnlyDefault, Both }`). A purchase
(`ZenStore.OnPurchaseStarted`) holds interstitials for 60 s. `AdZative` is the native waterfall (Android): layouts,
pods, timing per prefab; see its public API in `Ads/Scripts/AdZative.cs`.

Add a placement: duplicate an `Admob_*` / `AdZative_*` prefab as a variant, set its `placement`, add it under
`ADsManager`'s `Admob` child in the game's ADsManager variant, add the row in Base > Hub > Ad IDs.

## 7. Currency, IAP

**CurrencyManager** (per scene HUD; `CurrencyManager.prefab`):

```csharp
public static int TotalSoft { get; set; }   public static int TotalHard { get; set; }   // proxies of UserData
public static void AddSoft(int value, string itemName, Transform from, Transform end = null, bool showFx = true); // ≤ 1,000,000
public static void AddHard(int value, string itemName, Transform from, Transform end = null, bool showFx = true); // ≤ 1,000
public static bool GrantFromPurchase(int value, CurrencyType currency, string itemName, Transform from, Transform end = null);
// Add* log ZenAnalytics.LogResourceEarn / Spend (negative value = spend) and do nothing without an instance in the scene
```

**IAP** (`USE_IN_APP_PURCHASE`): `IAPManager.prefab` connects the Unity IAP store and loads the default catalog.
`IAPButtonExtent` on a `Button_IAP` variant buys one product and describes its reward; `IAPManager.TryGrant` applies
Remove Ads (`UserData.isRemovedAds`), VIP, Soft / Hard (via `CurrencyManager.GrantFromPurchase`) and raises
`IAPManager.OnGrantReward(IAPButtonExtent, IAPReward)` for everything else (boosters, skins...): the game handles
those. The Remove Ads product id is `ZenIapSettings.RemoveAdsProductId` (default `<bundle id>.remove.ads`).
`UIPopupIAP` (`Resources/UIPopupIAP`) is the shop popup: `UIPopupIAP.Show()`; `ZenStore.OpenShop` points to it.

## 8. Services: the hooks between parts (namespace `Base`, static classes)

```csharp
ZenAnalytics.LogEvent("level_start", new Dictionary<string, object> { { "level", 3 } });
ZenAnalytics.SetUser("vip", true);
ZenAnalytics.LogIAP(screen, productId, IAPStatus.Success, localizedPrice, isoCurrencyCode);
ZenAnalytics.LogResourceEarn(value, currencyName, CurrencyType.Soft, placement);   // LogResourceSpend likewise
// IAPStatus { Show, Click, Success, Failed, Close }

ZenRemote.OnCollectDefaults += d => d["myKey"] = 3;                 // before Init
ZenRemote.OnFetched += () => myValue = ZenRemote.GetInt("myKey", 3); // GetString / GetBool / GetFloat
ZenRemote.Init(); ZenRemote.Fetch();                                 // coroutines; AdSplash runs them

ZenToast.ShowNotice("Saved");            // AdToast when present, else Debug.Log
ZenStore.OpenShop?.Invoke();             // set by the IAP part; OnPurchaseStarted holds interstitials
```

`FirebaseZenBackend` (with `USE_FIREBASE`) registers `ZenAnalytics.Backend` and `ZenRemote.Backend` before the first
scene; without Firebase the calls are no-ops and `Get*` return their defaults. `FirebaseManager` also offers
`LogLevel(...)`, `LogLevelUp`, `LogException`, `RemoteGetValue*`, `FetchAsync`.

**UnityMainThreadDispatcher** (Base.dll): `Enqueue(Action)` / `Enqueue(IEnumerator)` from any thread (SDK callbacks).

## 9. Extend instead of editing the package

- Screens / popups: subclass `UIScreen`, `UIPopupBase<T>`; make prefab variants of `Popup_Base` / `Button_*`.
- Ads: subclass `AdBase` / `AdmobBase`; listen to `AdBase.OnAnyStateChanged`; prefab variants with a new `placement`.
- Data: subclass `UserDataBase` fields in the game's `UserData`, `ItemDatas<T>` per catalogue, `GameConfig` fields.
- UI flow: subclass `UIManager` and override `LoadScreen<T>` (Addressables), `ShowToast`, `ShowMessage`, `HandleBack`.
- Settings: `Resources/AdConfig`, `ZenAdIds`, `ZenIapSettings`, prefab variants: all in the game, never in the package.
- Last resort: embed the package (copy to `Packages/`) to patch it; it then stops updating.

## 10. Lifetimes at a glance

- DontDestroyOnLoad (once, from the splash scene): DataManager, AdsManager, FirebaseManager, IAPManager,
  GameStateManager, UnityMainThreadDispatcher.
- Per scene (the newest one wins, a second one in the same scene is removed): UIManager, UIAnimManager,
  CurrencyManager.
- `GameConfig` is a plain serializable class saved inside GameData, not an asset.
