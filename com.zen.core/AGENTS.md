# Working on a game built on Base (com.zen.core): rules for AI agents and new developers

Read this first, then [Documentation~/Architecture.md](Documentation~/Architecture.md) (how the managers work, API
and examples) and [Documentation~/ZPAS.md](Documentation~/ZPAS.md) (naming and folder standard). Anything about ads, Remove
Ads, VIP or IAP rewards: read [Documentation~/Ads.md](Documentation~/Ads.md) first (keys, flows, diagrams).
Game flow, game-state listeners, which popup shows when: read [Documentation~/GameState.md](Documentation~/GameState.md) first.
A game's structure (GameManager → managers → controllers, states, revive, reporting events mid-game): read
[Documentation~/Gameplay.md](Documentation~/Gameplay.md); the UI sample (`Samples~/UI`, scene `99_SampleScene`) is the reference for the GameState flow and the UI.
Remove Ads / VIP turn off interstitial, app open, banner, MREC and native ads but **never rewarded ads** (AdMob or
native): `adRewardFlow` Both = native is the fallback, OnlyNative = native replaces AdMob. The game's own
`AGENTS.md` / `CLAUDE.md`, when there is one, wins over this file.

## Never

1. **Never edit files under `Packages/com.zen.core` or `Packages/com.zen.plugins.adzative`** (in a game they are
   read-only package cache; in zen-unity they are the package source). Extend instead: subclasses, prefab variants,
   the game's `Resources` assets, `UIManager` overrides. See Architecture.md section 9.
2. **Never edit `.meta` files or GUIDs by hand**, never rename or move assets on the file system (use
   `AssetDatabase.RenameAsset` / `MoveAsset`), never delete an asset without searching every YAML file for its GUID.
3. **Never put ad IDs, keys or `google-services.json` in a package or a commit meant to be public.** Ad IDs live in
   `Assets/Resources/AdIds.asset` (edited in Base > Hub > Ad IDs), nowhere else.
4. **Never add `USE_*` symbols to Player Settings** (`USE_FIREBASE`, `USE_IN_APP_PURCHASE`...): Base sets them when
   the SDK is installed. Never add `DOTWEEN` by hand either; Base's bootstrap does.
5. **Never run a Unity or APK build, never publish, push or release** unless the person asked for it in their latest
   message. Compile checks are fine.
6. **Never overwrite an existing prefab or asset** (even an untracked one) to "regenerate" it: edit it, or ask.

## Always

- **Names and folders follow ZPAS** (ZPAS.md): `PascalCase` for classes and 1:1 prefabs (`UIPopupShop.cs` +
  `UIPopupShop.prefab`), `Pascal_Snake_Case` for folders, scenes and variant prefabs (`Button_CTA`),
  `lowercase_snake_case` with a type infix for sprites, textures, audio, animations (`ui_shop_btn_spr_buy_pressed.png`).
  No `final`, `new`, `temp`, `demo`, `old`, `copy`, `backup`, `fix`, `latest` in a name.
- **Use the managers, do not rebuild them**: `GameStateManager` for game state, `DataManager` for save / load,
  `UIManager` / `UIScreen` / `UIPopupBase<T>` for UI, `AdsManager.ShowInter / ShowReward` for ads,
  `CurrencyManager.AddSoft / AddHard` for currency, `AnalyticsManager` / `RemoteConfigManager` / `AdToast.ShowNotice` for analytics,
  remote config and notices. Wait for `DataManager.IsLoaded` (or `OnLoaded`) before reading data.
- **Popups and screens loaded by name** are prefabs in `Assets/Resources/<ClassName>.prefab`, variants of
  `Popup_Base`; a scene's UI lives under the `UIManager` prefab (it holds `UIAnimManager.RootTransform`).
- **Every game scene needs** `UIManager` and `UI_Camera`. The **Splash** (build index 0) holds everything that lives
  for the whole game: `ADsManager`, `DataManager`, `GameStateManager`, `AudioManager`, `Event_System` (+ `FirebaseManager`,
  `IAPManager` when those SDKs are used). A game scene has no EventSystem of its own (the UI sample's scene 99 has none).
  Base > Hub > Setup > Splash: **Create** makes it, **Add** puts the game's ItemDatas on its DataManager.
- **Settings go in the game**: `Resources/AdConfig`, `AdIds`, `IAPSettings`, prefab variants of the package
  prefabs. Base > Hub > Setup > Fix all creates the missing ones.
- **Base > Hub** is the only Base menu: Setup (SDKs, settings checks, update / rollback), Ad Config, Ad IDs,
  Build Check. Use it instead of editing manifest.json or assets by hand.
- **Propose before changing**: for anything beyond the literal ask (renames, refactors, new systems) write the plan
  and wait for approval. Report what was verified (compile, test, device) and what was not.

## Quick map

| Need | Call | Doc |
|---|---|---|
| Change game state | `GameStateManager.Play()` ... `OnGameStateChanged`; who listens, flow, UI by state in [GameState.md](Documentation~/GameState.md) | Architecture §3 |
| Player data, config, items | `DataManager.UserData`, `.GameConfig`, `.Get<SkinDatas>()`, `.Save(true)`; `totalPlay` / `totalWin` / `totalLose` counted by `GameCounters` | §4 |
| Screen / popup / toast / message | `UIManager.ShowScreen<T>()`, `UIPopupX.Show()`, `UIManager.Toast()`, `.Message()` | §5 |
| Interstitial / rewarded, Remove Ads, ad pacing | `AdsManager.ShowInter(cb, "where")`, `.ShowReward(cb, "what")`; keys and flows in [Ads.md](Documentation~/Ads.md) | §6 |
| Coins / gems | `CurrencyManager.AddSoft(v, "why", from)`, `.AddHard(...)` | §7 |
| Analytics / remote config | `AnalyticsManager.LogEvent`, `RemoteConfigManager.GetInt` | §8 |
| Sound / music / vibration | `SoundManager.Play`, `MusicManager.Play` (AudioManager in the Splash), `GameSettings.SoundOn` / `SoundVolume` / `Quality`, `HapticManager.PlayImpact`; settings popup in the UI sample | §11 |
| Daily login reward | `DailyLogin.CanClaim`, `.Claim(from)`, `DailyLoginDatas` in `DataManager.itemDatas`; calendar popup in the UI sample | §11 |
| Daily missions, achievements | `Missions.Report(key, n)`, `DailyMissions.Today` / `.Claim(id, from)`, `Achievements.Visible` / `.Claim`; `DailyMissionDatas`, `AchievementDatas` in `DataManager.itemDatas` | §11 |
| Reminders (local push) | `NotificationManager.ShouldOffer`, `.IETurnOn(done)`, `.TakeRoute()`; `NotificationDatas` in `DataManager.itemDatas` | §11 |
| Rewards (coins, gems, booster, item) | `Reward { type, id, amount }`, `Rewards.Grant(reward, "why", from)` | §11 |
| Versions, SDKs, IDs | Base > Hub | README |

## Managers: what each one does

Lives in the Splash (survives scene loads) / in the game scene / static (no object). Call the one that owns the job;
never keep a second copy of its state.

| Manager | Lives | Job |
|---|---|---|
| `GameStateManager` | Splash | The one game state (Main, Load, Init, Ready, Play, Pause, WaitWin, Win, WaitLose, ReviveContinue, Lose, Next, Restart); `OnGameStateChanged`. |
| `DataManager` | Splash | Save / load of the player (JSON + AES), `ItemDatas` registered on it, `GameConfig`. Read after `IsLoaded`. |
| `UIManager` | Scene | Screens and popups (loaded from `Resources/<ClassName>`), back key, `Toast`, `Message`. `UIAnimManager` holds their root. |
| `AdsManager` | Splash | Ad init, load and show (inter, rewarded, app open, banner), the show clock, Remove Ads. `AdSplash` runs the start-up. |
| `IAPManager` | Splash | Unity IAP, grants a purchase (`TryGrant`), `RestorePurchases`, `GetPack`. |
| `CurrencyManager` | Scene (HUD) | `AddSoft` / `AddHard`, the fly FX, currency names, `OpenShop` (the game's shop). |
| `FirebaseManager` | Splash | Firebase init, Analytics, Remote Config, Crashlytics, Messaging. |
| `AnalyticsManager` | Static | Events to Firebase Analytics; does nothing without Firebase. |
| `RemoteConfigManager` | Static | Remote Config: parts add default keys (`OnCollectDefaults`) and read them in `OnFetched`. |
| `NotificationManager` | Static | Local reminders: permission, schedule, the popup a tapped reminder opens. Shown in the Editor, posts nothing there. |
| `SoundManager` / `MusicManager` | Splash (AudioManager) | 2D sound effects / looping music, both under `GameSettings`. |
| `SFXSampleManager` / `PFXSampleManager` | Scene | Pooled 3D sounds / particle effects at a position (same sound settings). |
| `HapticManager` | Static | `PlayImpact(style)`, silent while vibration is off. |
| `GameSettings` | Static | Sound / music on and volume, vibration, graphics quality, saved. The only place for these settings. |
| `LeaderboardManager`, `TaichiManager` | Persistent | Cached leaderboard; ad-revenue milestone events. |

## Pitfalls: what went wrong before

1. **Splash.** A Splash without `GameStateManager` makes every `GameStateManager.*` setter throw. `AdSplash`'s game scene
   index must be in the Build Settings (0 = none). Play always from the Splash (Base > Play From Splash, on by default).
   When the Splash is done, `AdSplash` calls `Main()` (one scene: home) or `Load()` (main + game scene: straight into
   the game); the game's GameManager calls `Ready()` once level, player and map are set up. Never call them again at start.2. **Game end.** Revive is offered at `WaitLose`, never after `Lose` (`GameCounters` and Missions count every `Lose`).
   One `Win` or `Lose` per game. The game-end interstitial is shown by the popup that ends the game, only with
   `AdConfig.adInterOnComplete`; `AdsManager` decides if it is time. Never call a setter inside the event that
   triggered it: enqueue it (`UnityMainThreadDispatcher.Enqueue`) so every listener sees the first state.
3. **Store.** Put a `RemoveAdsButton` on a Remove Ads button and a `RestorePurchasesButton` on a Restore button: no code.
   The shop is `CurrencyManager.OpenShop`. A purchase holds interstitials by itself (`AdsManager.DelayAdsForPurchase`).
4. **Popups.** The `UIAnim` on the prefab root must be enabled (a disabled one never shows the popup). The prefab name
   is the class name. A tap outside closes it like the back key (`closeOnTapOutside`). Overriding `OnEnable` /
   `OnDisable` / `Awake`: call `base` first, or the popup is not registered on the stack.
5. **Settings.** Sound, music, vibration and quality only through `GameSettings`; never read or write their
   `PlayerPrefs` keys. 2D sounds `SoundManager.Play`, 3D `SFXSampleManager`, vibration `HapticManager`.
6. **Names of game assets.** `Resources/AdIds`, `Resources/IAPSettings`, `Assets/Profiles`: Base loads them by name.
   A game from before the Zen prefix was dropped: Base > Hub > Setup (Core) renames them; Build Check warns about
   them at every build until then.
