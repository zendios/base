# Base Core (com.zen.core)

Zen Base for Unity games in **one package**, `com.zen.core`: Base.dll, DataManager (save / load, ItemDatas), UI popups,
currency, extensions, vibration, **ads** (AdsManager, AdMob + AdZative native placements, AdConfig, ZenAdIds, UMP,
ATT, Taichi, in-app review / update, splash, debug tools), **IAP** (IAPManager, IAPButtonExtent, UIPopupIAP),
**Firebase** (Analytics, Remote Config, Crashlytics), screens (ScreenUtils) and **Base > Hub**.
`com.zen.plugins.adzative` (native ads plugin, built from zen-android) is its only Zen companion.

Required SDKs: TextMeshPro, uGUI, Input System, AdMob (`com.google.ads.mobile`, OpenUPM), DOTween (free) or DOTween Pro in Assets/Plugins/Demigiant (no ASMDEF needed).
Optional SDKs switch their part on when installed and off when missing, without compile errors:
Unity IAP (`USE_IN_APP_PURCHASE`), Firebase (`USE_FIREBASE`), AppsFlyer (`USE_APPSFLYER`), Play Review
(`USE_IN_APP_REVIEW`), Play App Update (`USE_IN_APP_UPDATE`). Do not add these symbols to Player Settings.
Inside the package the parts still talk through `ZenAnalytics`, `ZenRemote`, `ZenStore` and `ZenToast`, so a missing
SDK only turns its calls into no-ops.

**Docs:** [AGENTS.md](AGENTS.md) (rules for AI agents and new developers),
[Documentation~/Architecture.md](Documentation~/Architecture.md) (managers, API, examples),
[Documentation~/ZPAS.md](Documentation~/ZPAS.md) (naming and folder standard). Base > Hub > Setup (Fix all) copies
`AGENTS.md` to the game's project root so Claude / Cursor / Copilot read it.

## Install in a new game (or an old one)

**Fastest:** drag [Base-Installer.unitypackage](https://github.com/zendios/base/releases/latest/download/Base-Installer.unitypackage)
(every version: [Releases](https://github.com/zendios/base/releases))
into the project (it adds OpenUPM and Base to manifest.json, then removes itself); without DOTween, Base offers to
install the latest free DOTween from dotween.demigiant.com. Then step 2 below. By hand:

1. `Packages/manifest.json`: the two Base packages from OpenUPM (Google publishes AdMob there too), no GitHub account needed:
   ```json
   {
     "scopedRegistries": [
       { "name": "OpenUPM", "url": "https://package.openupm.com", "scopes": [ "com.google", "com.zen" ] }
     ],
     "dependencies": {
       "com.zen.core": "1.2.5",
       "com.zen.plugins.adzative": "1.2.5"
     }
   }
   ```
   The packages come from the public github.com/zendios/base (MIT), built by OpenUPM. Without OpenUPM:
   `https://github.com/zendios/base.git?path=/com.zen.core#1.2.5` (and `/com.zen.plugins.adzative`).
2. **Base > Hub > Setup > SDKs**: tick what the game uses (required: AdMob, EDM4U; mediation: Meta by default;
   AppsFlyer, In-App Review, In-App Update, Unity IAP, Firebase) and press **Install required + selected**. Every SDK
   comes at the version Base was tested with, from its official source: OpenUPM (AdMob, mediation adapters, EDM4U, Play
   plugins), github.com/AppsFlyerSDK (tag v6.17.900; newer AppsFlyer needs a newer Unity), the Unity registry (IAP),
   dl.google.com (Firebase .tgz). **Check latest versions** shows newer ones (Update, not tested yet); **Use x.y.z**
   goes back to the tested version. An old game also gets **Old installs**: .unitypackage copies (Assets/GoogleMobileAds,
   ExternalDependencyManager, Firebase, AppsFlyer, GooglePlayPlugins, Base) to remove. **Unused packages > Scan** lists the
   packages and built-in modules nothing uses (compiled code, scenes / prefabs / assets, package dependencies);
   **Remove selected** keeps a manifest.json.bak for **Restore**.
3. DOTween is required: without it Base stays off (its assemblies need the DOTWEEN symbol, so no compile errors) and a
   dialog offers to download and import the latest free DOTween (official site); DOTween found but no DOTWEEN symbol:
   Base adds the symbol. DOTween Pro only if
   the game uses its features; it is paid, so add it by hand (Base never ships it). No ASMDEF step: Base uses only
   DOTween.dll.
4. **Base > Hub > Setup**: press **Fix all**, then fill the ad IDs (Base > Hub > Ad IDs)
   and put `google-services.json` under `Assets`. The Remove ADs product id is `<package name>.remove.ads`
   (e.g. `com.zen.mygame.remove.ads`): create that product in the IAP catalog / stores, or set another id ending in
   `remove.ads` in `ZenIapSettings`.
5. Splash / sample scenes: **Package Manager > Zen Core > Samples > Scenes > Import**.

## Base > Hub

The only Base menu. Tabs: **Setup** (packages and SDKs with Install, the game's missing settings with Fix / Fix all,
the released versions with their notes: update or roll back), **Ad Config** (changes from the defaults highlighted,
presets, Remote Config template export, Remove Ads product vs the IAP catalog), **Ad IDs** (Android / iOS tabs, the
build target's first; "Paste from clipboard" takes Google Sheet rows like
`native_inter_default_high_220926  ca-app-pub-.../...`, rows of one placement joined in waterfall order high, medium,
low, all), **Build Check** (a release build stops on its errors) and, in zen-unity only, **Release**.
Native pod size and timing are set on each AdZative prefab.

## Who owns what

Values are taken from the highest layer that has them.

| Layer | Where | Owner | On a package update |
|---|---|---|---|
| 4. Remote Config | Firebase console | the live-ops team | n/a |
| 3. Game settings | `Assets/Resources` (ZenAdIds, AdConfig, ZenIapSettings, prefab variants), `Assets/Plugins/Android` | the game | never touched |
| 2. Package defaults | field values in code, Google test IDs | the package | new fields start at their default, old values stay |
| 1. Code and original prefabs | `Packages/com.zen.core` (read-only) | the package | replaced by the new version |

- **Ad IDs**: one row per `PlacementType` in `ZenAdIds`. A placement without an ID loads Google's test ID (warning).
  A prefab can still set `AdBase.adIdData` to override its row.
- **AdConfig**: `AdConfig.Instance` = the game's `Resources/AdConfig`, else the class defaults; `AdConfigRemote`
  applies Remote Config on top. Remote keys never change between versions.
- **Prefabs**: use them as they are, or make a **Prefab Variant** in the game and change only what you need; the
  variant keeps receiving the package's changes.

## Extend instead of editing the package

- Ads: subclass `AdBase` / `AdmobBase`, listen to `AdBase.OnAnyStateChanged`, `ZenStore.OnPurchaseStarted`...
- Remote Config keys of the game: `ZenRemote.OnCollectDefaults += d => d["myKey"] = 3;` and read them in
  `ZenRemote.OnFetched` with `ZenRemote.GetInt("myKey", 3)`.
- Analytics: `ZenAnalytics.LogEvent("my_event", new Dictionary<string, object> { { "level", 3 } })`.
- Game items (skins, boosters...): `class SkinDatas : ItemDatas<SkinData>`, add the asset to `DataManager.itemDatas`;
  it is saved by asset name. `DataManager.Get<SkinDatas>()` returns it.
- Player data: subclass `UserDataBase`.
- Last resort: copy the package folder into the game's `Packages/` (embedded) to patch it; it then stops updating.

## Screens: every ratio, portrait and landscape

- Put `ScreenUtils` on each root canvas (CanvasScaler: Scale With Screen Size). It uses **Expand**: the design area
  (`landscapeReference` 1920×1080 / `portraitReference` 1080×1920) is always fully visible, and the extra space of a
  20:9 phone, a tablet or a foldable goes to the free axis. Anchor UI to the edges / corners so it uses that space;
  put HUD and buttons under a `SafeArea`.
- The reference switches when the device rotates; `ScreenUtils.OnOrientationChanged(bool portrait)` tells your code.
- `OrientationLayout` on any element: save its pose for portrait and for landscape (position, size, anchors, active).
- `tabletMaxScale` (1.3) keeps UI at most 30% physically bigger than on a ~65 mm wide phone.
- `ScreenMetrics`: DPI, mm / dp ↔ pixels ↔ canvas units (`DpToUnits(48, canvas)` = minimum touch target).

## Update and rollback

Each game checks once a day for a newer `zen/*` tag (also **Check for updates** in Base > Hub > Setup). When there is
one, Base > Hub opens on Setup. Pick any released version there (its CHANGELOG notes show under it) and press
**Update to** / **Roll back to**: the two Zen entries of `manifest.json` move to that tag. Nothing in
`Assets/Resources` changes; Setup then lists any new settings.

## Coming from `Assets/Base` (com.zen.base 1.0)

1. Delete `Assets/Base`, install the packages, run **Base > Hub > Setup > Fix all**.
2. Copy each `AdIdData` (Admob_Inter_Default...) ID list into its `ZenAdIds` row; move your `AdConfig.asset` into
   `Assets/Resources` (keep its .meta).
3. Code: `DataManager.AdConfig` -> `AdConfig.Instance`, `FirebaseManager.LogEvent` / `SetUser` / `LogIAP` /
   `LogResourceEarn` / `LogResourceSpend` -> `ZenAnalytics.*`, `AdToast.ShowNotice` outside ads -> `ZenToast.ShowNotice`,
   `FirebaseRemote` -> `ZenRemote` (keys unchanged). Remove `USE_*` symbols from Player Settings.
4. `BoosterDatas` / `OutfitDatas` left Base: keep your own classes with those names and add their assets to
   `DataManager.itemDatas`; existing saves move into them on the first load.

## Developing the packages

zen-unity uses it as an embedded package (`zen-unity/Packages/com.zen.core`), so it is edited in place.
`zen-base/build.sh` builds Base.dll into `com.zen.core/Plugins`; `tools/zen-release.sh <version>` refreshes the
samples / templates, sets the version, commits and tags `zen/<version>`.
**Base > Hub > Release** does it from the editor: the version's notes go into CHANGELOG.md (prefilled from
[Unreleased]), then the script runs.
**Publish** runs `tools/base-publish.sh <version>`: pushes zen and tag zen/<version>, then mirrors both packages into the
public github.com/zendios/base (README / LICENSE from tools/base-public) with tag <version>, which OpenUPM builds.
