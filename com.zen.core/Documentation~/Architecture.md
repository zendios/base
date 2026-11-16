# Base architecture and usage

How a game built on `com.zen.core` is put together, and the API of each part. Signatures are copied from the code
(1.2.7); when the code and this file differ, the code wins. Naming and folders: [ZPAS.md](ZPAS.md). Rules for AI
agents: [AGENTS.md](../AGENTS.md).

## 1. Layers

```
Game (Assets/)          scenes, screens (UIScreen), popups, gameplay, Resources/AdIds + AdConfig + IAPSettings
com.zen.core            managers (prefabs), services, UI framework, ads waterfall, IAP, Firebase, Base > Hub
Base.dll (zen-base)     GameStateManager, DataManagerBase, ItemData(s), UIAnim / UIAnimManager, enums
SDKs                    AdMob, EDM (Unity's External Dependency Manager), DOTween (required); Unity IAP, Firebase, AppsFlyer, Play Review / Update (optional)
```

Optional SDKs switch their part on with a symbol Base sets by itself (`USE_IN_APP_PURCHASE`, `USE_FIREBASE`,
`USE_APPSFLYER`, `USE_IN_APP_REVIEW`, `USE_IN_APP_UPDATE`). It is one assembly: the parts call each other directly,
the code of an optional SDK sits under its symbol, so a missing SDK only turns its calls into no-ops (section 8).

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
`DataManager.IELoad()`, `UMP.Instance.IECheck()`, `AppsFlyerHelper.IEInit()`, `RemoteConfigManager.Init()` + `Fetch()`, in-app
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

Every rule of the event, the listeners inside Base, how a game maps states to its UI, pitfalls and a diagram:
[GameState.md](GameState.md).

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
`CheckDailyLogin()`. Setters forward to `AnalyticsManager.SetUser` as user properties.

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
public static void Toast(string message);                  // AdToast.ShowNotice (into the log while no ADsManager is loaded)
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

Button structure (keep it when making variants, a game's button joins Base by matching it):
`Button_Base` root = `Button` + `Animator` (press animation, so the Button has no target graphic) → child
`ButtonBackground` (`Image`, the sprite and colour) → child `Content`. `Button_Icon` puts an `Icon_Base` and
`Button_Text` a `Text_Base` under `Content`; `Button_CTA` has both. To move an old game's button (root `Image` +
`Button`, label or icon as a direct child) onto Base: a variant of the matching `Button_*`, the old sprite on
`ButtonBackground`, the old label / icon values on `Content`'s child, listeners re-added; a size family the game uses
often (a small round button) is a variant in the game, not a new Base prefab.

**Screens and ratios**: `ScreenUtils` on each root canvas uses Expand with `landscapeReference` 1920×1080 /
`portraitReference` 1080×1920 and `tabletMaxScale` 1.3; `static bool IsPortrait`, `static event Action<bool>
OnOrientationChanged`. `OrientationLayout` saves a pose per orientation; put HUD under a `SafeArea`.

## 6. Ads: `AdsManager` (namespace `Base.Ads`)

`ADsManager.prefab`: AdsManager + one child `AdBase` per placement (`Admob_Inter_Default`, `AdZative_Inter_Default`...).
IDs come from `Resources/AdIds` (one row per `PlacementType`, Android / iOS lists in waterfall order; a row without
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

Every key, the full decision flow, Remove Ads / VIP / IAP rewards and diagrams: [Ads.md](Ads.md).
Timing rules (`AdConfig`): `adTimePlayToShow`, `adTimeBetween`, `adInterOnPlay / OnStart / OnComplete / OnFTUE`,
`adBannerReload`, flows `adInterFlow` etc. (`AdFlow { OnlyNative, OnlyDefault, Both }`). A purchase
(`IAPButtonExtent` tapped: `AdsManager.DelayAdsForPurchase`) holds interstitials for 60 s. `AdZative` is the native waterfall (Android): layouts,
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
// Add* log AnalyticsManager.LogResourceEarn / Spend (negative value = spend) and do nothing without an instance in the scene
```

**IAP** (`USE_IN_APP_PURCHASE`): `IAPManager.prefab` connects the Unity IAP store and loads the default catalog.
`IAPButtonExtent` on a `Button_IAP` variant buys one product and describes its reward; `IAPManager.TryGrant` applies
Remove Ads (`UserData.isRemovedAds`), VIP, Soft / Hard (via `CurrencyManager.GrantFromPurchase`) and raises
`IAPManager.OnGrantReward(IAPButtonExtent, IAPReward)` for everything else (boosters, skins...): the game handles
those. The Remove Ads product id is `IAPSettings.RemoveAdsProductId` (default `<bundle id>.remove.ads`).
`UIPopupIAP` (`Resources/UIPopupIAP`) is the shop popup: `UIPopupIAP.Show()`; `CurrencyManager.OpenShop` points to it.
A `RemoveAdsButton` on any button sells Remove Ads (hidden without Unity IAP and once ads are removed); a
`RestorePurchasesButton` restores purchases on iOS (`IAPManager.RestorePurchases`).

## 8. Analytics, Remote Config, notices (namespace `Base`, static classes)

```csharp
AnalyticsManager.LogEvent("level_start", new Dictionary<string, object> { { "level", 3 } });
AnalyticsManager.SetUser("vip", true);
AnalyticsManager.LogIAP(screen, productId, IAPStatus.Success, localizedPrice, isoCurrencyCode);
AnalyticsManager.LogResourceEarn(value, currencyName, CurrencyType.Soft, placement);   // LogResourceSpend likewise
// IAPStatus { Show, Click, Success, Failed, Close }

RemoteConfigManager.OnCollectDefaults += d => d["myKey"] = 3;                 // before Init
RemoteConfigManager.OnFetched += () => myValue = RemoteConfigManager.GetInt("myKey", 3); // GetString / GetBool / GetFloat
RemoteConfigManager.Init(); RemoteConfigManager.Fetch();                                 // coroutines; AdSplash runs them

AdToast.ShowNotice("Saved");            // into the log while no ADsManager is loaded
CurrencyManager.OpenShop?.Invoke();     // the game's shop (the UI sample sets it to UIPopupIAP)
```

`AnalyticsManager` and `RemoteConfigManager` call `FirebaseManager`; without Firebase (`USE_FIREBASE`) the calls are
no-ops and `Get*` return their defaults. `FirebaseManager` also offers
`LogLevel(...)`, `LogLevelUp`, `LogException`, `RemoteGetValue*`, `FetchAsync`.

**UnityMainThreadDispatcher** (Base.dll): `Enqueue(Action)` / `Enqueue(IEnumerator)` from any thread (SDK callbacks).

## 9. Extend instead of editing the package

- Screens / popups: subclass `UIScreen`, `UIPopupBase<T>`; make prefab variants of `Popup_Base` / `Button_*`.
- Ads: subclass `AdBase` / `AdmobBase`; listen to `AdBase.OnAnyStateChanged`; prefab variants with a new `placement`.
- Data: subclass `UserDataBase` fields in the game's `UserData`, `ItemDatas<T>` per catalogue, `GameConfig` fields.
- UI flow: subclass `UIManager` and override `LoadScreen<T>` (Addressables), `ShowToast`, `ShowMessage`, `HandleBack`.
- Settings: `Resources/AdConfig`, `AdIds`, `IAPSettings`, prefab variants: all in the game, never in the package.
- Last resort: embed the package (copy to `Packages/`) to patch it; it then stops updating.

## 10. Lifetimes at a glance

- DontDestroyOnLoad (once, from the splash scene): DataManager, AdsManager, FirebaseManager, IAPManager,
  GameStateManager, UnityMainThreadDispatcher.
- Per scene (the newest one wins, a second one in the same scene is removed): UIManager, UIAnimManager,
  CurrencyManager.
- `GameConfig` is a plain serializable class saved inside GameData, not an asset.

## 11. Settings, audio and the meta features of the UI sample

**GameSettings** (namespace `Base`, static): `SoundOn`, `MusicOn`, `SoundVolume`, `MusicVolume` (0 to 1; 0 also turns
the sound / music off), `Quality` (a QualitySettings level or `QualityAuto`: from the device memory), saved in the save
file (section `settings`, registered by itself), `VibrationOn` (= `HapticManager.IsOn`); events `OnSoundChanged`,
`OnMusicChanged`, `OnVibrationChanged` (`Action<bool>`), `OnSoundVolumeChanged`, `OnMusicVolumeChanged`
(`Action<float>`), raised when the save loads too. A change logs `settings_change {setting, on}`.

**Audio** (`Runtime/Audio`, prefab `AudioManager` in the Splash, DontDestroyOnLoad): `SoundManager.Play(clip, volume,
pitch)` / `PlayClick()` (a few reused AudioSources), `MusicManager.Play(clip)` / `Stop()` (fade between two looping
sources, real time). Both follow GameSettings; without the prefab the calls do nothing. Vibration:
`HapticManager.PlayImpact(style)`, silent while vibration is off.

```csharp
void OnEnable()  { GameSettings.OnSoundChanged += OnSound; GameSettings.OnMusicChanged += OnMusic; }
void OnDisable() { GameSettings.OnSoundChanged -= OnSound; GameSettings.OnMusicChanged -= OnMusic; }
void OnSound(bool on) => sfxSource.mute = !on;
```

**Datas and rewards**: the static data of these features are `ItemDatas` assets added to `DataManager.itemDatas` (read
with `DataManager.Get<T>()`, like the game's skins): `DailyLoginDatas` (`DailyLoginData { reward, isSpecial, icon }`,
one per day; `resetOnMiss`, `adDouble`, `showOnHome`), `DailyMissionDatas` (`MissionData`; `missionsPerDay`,
`pickRandom`) and `AchievementDatas` (`MissionData` with `group` and `tier`). `MissionData { eventKey, target, reward,
group, tier, icon }` uses `ItemData.id` (saved with the progress) and `name` (the title, `{0}` = target, `Title`).
Every reward is `Reward { type, id, amount }`: `Soft` / `Hard` (CurrencyManager), `Booster` (adds to the count of the
item `id` in a registered ItemDatas) or `Item` (`CurrencyType.Item`: the player owns item `id`). `Rewards.Grant(reward,
source, from, times = 1)` grants one and saves; the game can use it for its own chests. `Reward.CanDouble`: only coins
and boosters are offered x2 for an ad. The player's progress stays in save sections, not in the assets. A day opens only
when the device date is later than the saved one: moving the clock back gives nothing.

**Missions.Report(eventKey, amount = 1)** (namespace `Base`, static) is the one call for "the player did something": it
moves the daily missions and the achievements listening to that key. Base reports `Missions.EventPlay` (a game started:
`GameState.Play` not from Pause / Revive), `EventWin`, `EventLose`, `EventWatchAd` (a rewarded ad shown), `EventEarnSoft`
(soft currency gained, the amount) and `EventUnlockItem` (an `Item` reward); the game reports its own keys, and
`unlock_item` for items it sells or gives at start.

**DailyLogin** (namespace `Base`, static): one reward per calendar day (device time) along `DailyLoginDatas`, starting
again after the last day; progress in save section `daily_login`. `IsAvailable` (datas registered and Remote Config
`dailyLogin_enabled`), `CanClaim`, `CanClaimDouble`, `NextDay`, `DayIndex`, `ClaimedCount`, `ClaimedToday`, `Cycle`,
`Claim(Transform from)` (`Rewards.Grant`, fly FX from `from`), `ClaimDouble(from)` (rewarded ad `daily_login_x2`),
`SetLastClaim(DateTime)` (debug), event `OnClaimed(int day, DailyLoginData data, bool doubled)`; logs
`daily_login_claim`.

```csharp
if (DailyLogin.CanClaim) UIPopupDailyLogin.Show();       // the sample's home screen does this once per session
DailyLogin.Claim(claimButton.transform);
```

**DailyMissions** (namespace `Base`, static): each calendar day `missionsPerDay` missions from `DailyMissionDatas`,
taken in order (rotating: order the pool easy, medium, hard for one of each a day) or at random (the same on every
device for the same day); progress in save section `daily_mission`, reset by the first read of a later day.
`IsAvailable` (datas registered and Remote Config `dailyMission_enabled`), `Today` (`DailyMissionProgress { id, value,
claimed, mission, IsDone, CanClaim }`), `HasClaimable`, `NextReset`, `Claim(id, Transform from)`, `ResetToday()`
(debug), event `OnChanged`; logs `daily_mission_claim`.

**Achievements** (namespace `Base`, static): lifetime goals from `AchievementDatas`. Progress is one lifetime counter
per event key, so an achievement added by an update counts what the player already did; each group shows its lowest
unclaimed tier (the last one once all are claimed). Save section `achievement`. `IsAvailable` (Remote Config
`achievement_enabled`), `Visible`, `Progress(a)`, `IsClaimed(a)`, `CanClaim(a)`, `HasClaimable`, `Claim(id, from)`,
event `OnChanged`; logs `achievement_claim {id, group, tier, type, amount}`.

```csharp
Missions.Report("collect_coin", coinsThisRun);             // daily missions and achievements on "collect_coin" advance
if (DailyMissions.HasClaimable || Achievements.HasClaimable) dot.SetActive(true);
DailyMissions.Claim(progress.id, claimButton.transform);
```

**NotificationManager** (namespace `Base`, static, needs `com.unity.mobile.notifications`: define `USE_NOTIFICATIONS`;
without it, or in the Editor, `IsSupported` is false and nothing is sent): local reminders for Daily Login and Daily
Missions. The OS is asked only after the player accepts the offer shown after claiming login day 1, 3 or 7
(`ShouldOffer`, at most 3 offers; iOS shows its dialog once, Android twice). When the game goes to the background the
rules of `NotificationPlanner` are scheduled (today 20:00 when rewards wait, then day +1, +3, +7 at the player's usual
hour, never 21:30–08:00, nothing after day +7) with the texts of `NotificationDatas` (`name` = title, `description` =
body, tokens `{day}` `{reward}` `{count}`); back in the game they are cancelled. A tapped reminder leaves a route for
the home screen. Save section `notifications`, Remote Config `push_enabled`. `RemindersOn`, `IsAllowed`, `IsBlocked`,
`ShouldOffer`, `IETurnOn(done)` (coroutine; opens the OS notification settings once it can no longer ask), `Decline()`,
`TurnOff()`, `TakeRoute()`, `ScheduleTest(seconds)` / `ResetForDebug()` (debug), event `OnChanged`; logs `push_prompt`,
`push_permission`, `push_scheduled`, `push_open`, `push_converted`. Android: turn on "Reschedule on Device Restart" in
Project Settings > Mobile Notifications; iOS: keep "Request Authorization on App Launch" off.

```csharp
remindRow.SetActive(NotificationManager.ShouldOffer);        // after DailyLogin.Claim
StartCoroutine(NotificationManager.IETurnOn(allowed => ...)); // "Remind me" or the Settings toggle
switch (NotificationManager.TakeRoute()) { case NotificationPlanner.RouteDailyLogin: UIPopupDailyLogin.Show(); break; }
```

**UI sample** (Package Manager > Base > Samples > UI, scene `99_SampleScene`, see Gameplay.md for the flow):
`UIScreenMain` (Play starts a game, Daily Login with a "reward waiting" dot, Missions and
Achievements with a "reward ready" dot, Settings, Shop, "Quit?" on the back key at the root), `UIPopupDailyLogin` (`Resources/UIPopupDailyLogin`:
a calendar of `UIDailyLoginCell` / `DailyLogin_Cell`, Claim, x2 with Ad), `UIPopupDailyMission` (`Resources/UIPopupDailyMission`:
`UIDailyMissionRow` / `DailyMission_Row` with a progress bar and Claim / DONE, "Reset in hh:mm:ss" through
`DateTime.ToCountDown()`), `UIPopupAchievements` (the same rows), the reminder offer in `UIPopupDailyLogin`, a Reminders toggle in `UIPopupSettings`, the home screen opening
what a tapped reminder asks for, and the data
of `GAME_DESIGN_SAMPLE_DATAS.md` section 6 in `Datas/` (7-day login, daily missions and achievement groups on the Base events,
`SampleBoosterDatas`, reminder texts), registered on the DataManager of the Splash (Hub Setup > Add), `UIPopupSettings` (`Resources/UIPopupSettings`, a `Popup_Base`
variant: sound / music volume, vibration, quality, Privacy through `UMP`, Rate through `InAppReview`, Restore through
`RestorePurchasesButton` on iOS, Remove Ads through `RemoveAdsButton`, the version through
`VersionInfo`; a button whose part of Base is missing hides itself), `Toggle_Base` (a switch built like `Button_Base`)
and the Game Flow test panel. The logic is in the package; the UI is meant to be copied into the game
(`Assets/Production/UI/...`) and restyled.

**One prefab for portrait and landscape**: `ScreenUtils` (Expand) always shows the whole 1080×1920 or 1920×1080 design
area, so a popup whose `ContentPane` stays within 1000×1000 fits both orientations; a screen uses anchors + `SafeArea`,
and `OrientationLayout` only where the two orientations differ.
