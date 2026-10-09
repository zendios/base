# Working on a game built on Base (com.zen.core): rules for AI agents and new developers

Read this first, then [Documentation~/Architecture.md](Documentation~/Architecture.md) (how the managers work, API
and examples) and [Documentation~/ZPAS.md](Documentation~/ZPAS.md) (naming and folder standard). The game's own
`AGENTS.md` / `CLAUDE.md`, when there is one, wins over this file.

## Never

1. **Never edit files under `Packages/com.zen.core` or `Packages/com.zen.plugins.adzative`** (in a game they are
   read-only package cache; in zen-unity they are the package source). Extend instead: subclasses, prefab variants,
   the game's `Resources` assets, `UIManager` overrides. See Architecture.md section 9.
2. **Never edit `.meta` files or GUIDs by hand**, never rename or move assets on the file system (use
   `AssetDatabase.RenameAsset` / `MoveAsset`), never delete an asset without searching every YAML file for its GUID.
3. **Never put ad IDs, keys or `google-services.json` in a package or a commit meant to be public.** Ad IDs live in
   `Assets/Resources/ZenAdIds.asset` (edited in Base > Hub > Ad IDs), nowhere else.
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
  `CurrencyManager.AddSoft / AddHard` for currency, `ZenAnalytics` / `ZenRemote` / `ZenToast` for analytics, remote
  config and notices. Wait for `DataManager.IsLoaded` (or `OnLoaded`) before reading data.
- **Popups and screens loaded by name** are prefabs in `Assets/Resources/<ClassName>.prefab`, variants of
  `Popup_Base`; a scene's UI lives under the `UIManager` prefab (it holds `UIAnimManager.RootTransform`).
- **Every scene needs** `UIManager`, `Event_System`, `UI_Camera`; the splash scene (build index 0) needs
  `ADsManager`, `DataManager`, `GameStateManager` (+ `FirebaseManager`, `IAPManager` when those SDKs are used).
- **Settings go in the game**: `Resources/AdConfig`, `ZenAdIds`, `ZenIapSettings`, prefab variants of the package
  prefabs. Base > Hub > Setup > Fix all creates the missing ones.
- **Base > Hub** is the only Base menu: Setup (SDKs, settings checks, update / rollback), Ad Config, Ad IDs,
  Build Check. Use it instead of editing manifest.json or assets by hand.
- **Propose before changing**: for anything beyond the literal ask (renames, refactors, new systems) write the plan
  and wait for approval. Report what was verified (compile, test, device) and what was not.

## Quick map

| Need | Call | Doc |
|---|---|---|
| Change game state | `GameStateManager.Play()` ... `OnGameStateChanged` | Architecture §3 |
| Player data, config, items | `DataManager.UserData`, `.GameConfig`, `.Get<SkinDatas>()`, `.Save(true)` | §4 |
| Screen / popup / toast / message | `UIManager.ShowScreen<T>()`, `UIPopupX.Show()`, `UIManager.Toast()`, `.Message()` | §5 |
| Interstitial / rewarded | `AdsManager.ShowInter(cb, "where")`, `.ShowReward(cb, "what")` | §6 |
| Coins / gems | `CurrencyManager.AddSoft(v, "why", from)`, `.AddHard(...)` | §7 |
| Analytics / remote config | `ZenAnalytics.LogEvent`, `ZenRemote.GetInt` | §8 |
| Versions, SDKs, IDs | Base > Hub | README |
