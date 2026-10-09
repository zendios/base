# Changelog (com.zen.core)

## [Unreleased]

## [1.2.8] - 2026-10-09
- Documentation~/Skills: the ZPAS standard (zen-unity-asset-standard) and the ZEN rules (zen-unity-rules) ship
  inside the package; the release stops when one is missing.
- ZPAS names: the parent (nearest folder that is not a type folder) is always in the name, a number only when there are
  several variants, part -> type -> property; atlases ui_<parent>_atlas_<function>; audio starts with sfx / bgm.
- Assets renamed to ZPAS (GUIDs kept, references unchanged): Ads images / materials / animations / atlas
  (ui_ads_*, ads_*_mat, ads_*_ani, ui_ads_atlas_common), CurrencyManager (currency_manager_soft_currency_*),
  FX moved into Runtime/FXManager (ConfettiFX, WeatherFX, SmokeFX) with confetti_fx_*, weather_fx_*, smoke_fx_* names,
  Button_IAP -> IAPButtonExtent.
- Base > Rename Assets (Editor): statistics of the folders to fix (third-party SDK folders skipped), a suggested ZPAS
  name per asset, names still used in code flagged, click a name to select the asset, rename the selected ones (GUIDs stay).
- Fixes from the project review: AdmobAppOpen no longer subscribes its static delegate to itself (handlers ran twice);
  AdsManager unsubscribes the DebugMode handler it subscribed (it used a new lambda); VersionInfo subscribes to
  DataManager.OnLoaded in OnEnable and unsubscribes in OnDisable. Base: the BinaryFormatter save / load paths
  (FileData.Save / SaveAsync / Load, DataManagerBase.Save / SaveAsync / Load) are removed; save data is JSON only.
  ClearAllData deletes the save file and the item trial times, and keeps PlayerPrefs (SDK and user settings);
  DataManager overrides it (virtual), so a game that reset everything through PlayerPrefs.DeleteAll must delete its own keys.
- Project: unused packages removed (timeline, visualscripting, ide.vscode); InputSystem_Actions is no longer the
  project-wide actions asset.
- CurrencyManager: a second one in the same scene is removed (warning), the newest scene's HUD is the active one,
  events are unsubscribed on destroy. GameConfig is documented as a plain class (no stray CreateAssetMenu).
  Prefabs Admob_Mrec_Open / AdZative_Mrec_Open for PlacementType *MrecOpen. Sample scenes: GameStateManager in the
  splash scene, CurrencyManager under UIManager in the sample scene.
- Docs for developers and AI agents: AGENTS.md (rules, API map; Base > Hub > Setup copies it to the game's project
  root), Documentation~/Architecture.md (managers, API, examples), Documentation~/ZPAS.md (naming / folder standard).

## [1.2.7] - 2026-10-09
- Fix: 1.2.5 and 1.2.6 on OpenUPM had no Base.dll / Base.Editor.dll (a global *.dll gitignore left them out of the
  public repo), so a new project got hundreds of "type not found" errors (GameState, AdType, UIAnim...). Publish now
  force-adds the packages and stops when a DLL is missing. Use 1.2.7 or later.
- New project defaults: the Android Resolver (EDM4U) auto-resolution is off without the "Enable Android
  Auto-resolution?" prompt (Base Installer and Base write it once; a choice made later stays). After DOTween is imported,
  Base sets its modules: Audio, Sprites, UI on; Physics, Physics2D, UI Toolkit off.
- Base Installer is downloaded from GitHub Releases (github.com/zendios/base/releases, latest:
  releases/latest/download/Base-Installer.unitypackage); Publish creates the release with the CHANGELOG notes.
- UI framework (shipped in 1.2.6): UIManager (screens with history, popup stack, toast, message, back key; every step
  virtual), UIView / UIScreen / UIPopup / `UIPopupBase<T>`, UIPopupMessage (title, message, confirm / cancel),
  UIManagerDebug prefab (test buttons made of ButtonTestAd).
- ZPAS names (shipped in 1.2.6): PopupBase -> UIPopupBase, PopupMessage -> UIPopupMessage, PopupIAP -> UIPopupIAP;
  prefabs Button_*, Canvas_Base*, Popup_*, Icon_Base, Text_Base, UI_Camera; sprites ui_common_* / ui_iap_*.
- UIAnim (shipped in 1.2.6): Fade In / Fade Out always use Parent Position (the view no longer shows pushed to the
  bottom edge); the Inspector hides Position Start / Position Out for Fade.

## [1.2.6] - 2026-10-08
- Easier install: Base's assemblies and DLLs need the DOTWEEN symbol (no compile errors without DOTween); the
  Zen.Bootstrap editor assembly adds DOTWEEN when DOTween is there, or offers to download and import the latest free
  DOTween from dotween.demigiant.com. Publish also puts installer/Base-Installer(-x.y.z).unitypackage in
  github.com/zendios/base: dragged into a project it adds OpenUPM (com.zen, com.google) and Base to manifest.json.
- Package Manager names: com.zen.core is "Base", com.zen.plugins.adzative is "Base Native Ad".

## [1.2.5] - 2026-10-07
- DOTween: Base needs only DOTween.dll (DOTween free or Pro in Assets), no ASMDEF: AdToast's slider tween uses
  DOTween.To instead of the UI module's DOValue and Zen.Core no longer references DOTween.Modules. Setup > Status
  requires DOTween (error + Asset Store link when missing); DOTween Pro stays a by-hand addition.
- Public release: Base > Hub > Release > Publish pushes zen, then mirrors com.zen.core and com.zen.plugins.adzative into
  the public github.com/zendios/base (MIT, tag x.y.z) that OpenUPM builds (tools/base-publish.sh). Games install
  "com.zen.core": "x.y.z" with the OpenUPM scope com.zen, no GitHub account. AdZative now has the same version as
  Base Core (tools/zen-release.sh sets both). Setup adds the com.zen scope with the OpenUPM registry and, for an
  OpenUPM install, lists / updates / rolls back the OpenUPM versions (notes from the public CHANGELOG); git URL
  installs keep working with zen/x.y.z tags. Release shows whether OpenUPM has the published version.

## [1.2.4] - 2026-10-06
- Zen > Hub > Ad IDs: one set of IDs only (the A/B/C ID sets are removed).
- ZenAdIds: Entries rows labelled with their placement.
- Zen > Hub tabs: Ad Config, Ad IDs, Build Check. The IAP tab is the Remove Ads section of Ad Config.
- Ad IDs: Android / iOS tabs (the build target's opens first). "Paste from clipboard" reads Google Sheet rows
  (`native_` = AdZative, app_open / banner / inter / mrec / reward, default / open / in_app, tier high / medium /
  low / all, date suffix ignored; enum names still work) into the open tab; rows of one placement are joined in
  waterfall order.
- The Native Timing tab is removed: pod size and full-screen timing are set on each AdZative prefab (Remote Config
  key = placement name still overrides them; the Remote Config template export reads them from the prefabs).
- AdZative inspector shows only its type's fields: Banner Strip (background, offsets, sizes, root rects) for a banner,
  full-screen timing for interstitial / rewarded / app open, pod size for interstitial / rewarded.
- AdBase.adIdData is hidden in the inspector (IDs live in ZenAdIds; a value already set still overrides).
- **Base > Hub** is the only menu (Zen menus renamed Base): tabs Setup, Ad Config, Ad IDs, Build Check, and Release in
  zen-unity. Removed: Zen > Setup (now the Setup tab), Zen > Check for Updates (button in Setup; the daily check
  stays), Zen > Screen Check, Zen > AdZative > Settings, and the Assets > Create > Zen menus (the Hub creates the
  assets).
- Setup: every released version with its CHANGELOG notes; Update to / Roll back to moves the two Zen entries of
  manifest.json to that tag.
- Release > Export test package: packs com.zen.core (version x.y.z-test.<date>) and com.zen.plugins.adzative into
  Builds/packages (ignored by git) with Client.Pack, to install in a game with file: entries before releasing.
  Nothing is committed, tagged or pushed. A test .tgz install is not update-checked. The same button also exports a
  .unitypackage (Packages/com.zen.core + com.zen.plugins.adzative, imported as embedded packages).
- Hub typography: three text styles only, Title (bold 12), Body (12) and Note (10), plus Body in yellow / green /
  grey for status (ZenHub.Title / Body / Note / Warn / Good / Muted).
- Release notes come from the changes too: the commits that touched zen-unity/Packages, zen-base or tools since
  the current version's release (tag zen/x.y.z, or its "Zen packages x.y.z" commit) fill the notes when [Unreleased]
  is empty. Each commit has a tick (housekeeping ones such as "Stop tracking..." start unticked); "Write notes"
  turns the ticked ones into short lines grouped by area (the part before ": ", e.g. Setup: SDKs: ..., Ad IDs: ...).
- Release (zen-unity only): version + notes (prefilled from [Unreleased]) -> CHANGELOG, then tools/zen-release.sh
  (Base.dll, samples / templates, version, commit, local tag); Push sends the branch and the tag.
  tools/zen-release.sh no longer needs python (perl sets the version).
- Hub buttons use the standard editor styles (toolbars, mini Fix buttons).
- Setup > SDKs: every SDK of a Base game pinned to the version Base was tested with, from its official source
  (OpenUPM: AdMob 11.5.0, EDM4U 1.2.189, mediation adapters Meta 3.20.1 (ticked by default), Liftoff, AppLovin, Unity Ads,
  ironSource, Mintegral, Pangle, In-App Review 1.8.4, In-App Update 1.8.5; github.com/AppsFlyerSDK tag v6.17.900;
  Unity registry: IAP 5.4.3; dl.google.com: Firebase 13.17.0 .tgz). Install required + selected, Use <tested>,
  Check latest versions + Update (not tested yet), and Old installs: .unitypackage copies to remove (to the trash).
  The latest versions are checked by itself once per editor session (Refresh asks again; Unity IAP from the Unity
  registry too); the table is one compact row per SDK, mediation folded to its installed / ticked adapters.
- Setup > Unused packages: Scan lists the manifest's packages and built-in modules the game does not use (no compiled
  assembly of the game or of a package references them, no scene / prefab / asset holds their components or scripts,
  no installed package depends on them); Remove selected backs manifest.json up (Restore puts it back). Base's own
  packages and the SDKs are never listed.

## [1.2.3] - 2026-10-05
- **Zen > Hub**, the game's Zen data in one window:
  - Ad IDs: one table for the 16 placements (Android / iOS, several IDs = waterfall), bulk paste from a sheet or
    the AdMob console, red cells for malformed / test / other-account IDs, duplicate warnings.
  - Ad Config: every field, highlighted when it differs from the package default (Reset), presets (package
    defaults, Test, the game's own saved presets in Assets/ZenProfiles/AdConfig), Firebase Remote Config template
    export (all Zen keys, one JSON per native placement).
  - Native Timing: pod size and full-screen timing of every AdZative prefab in one table; "Variant" makes an
    editable Prefab Variant of a read-only package prefab; Copy JSON = that placement's Remote Config value.
  - IAP: the Remove Ads product in use and whether the IAP catalog has it.
  - Build Check: Test IDs on, malformed / test / other-account ad IDs, no AdMob App ID, Firebase without
    google-services.json. A release (non-Development) build stops on these errors.
- Game settings in the project's root Assets/Resources (no Assets/ZenSettings).

## [1.2.2] - 2026-10-05
- Update notice: once a day (and Zen > Check for Updates) the editor lists the repository's zen/* tags; when a newer
  Zen Core exists, Zen > Setup opens with the new CHANGELOG sections and an "Update to x.y.z" button that moves
  com.zen.core and com.zen.plugins.adzative in manifest.json to that tag. Uses the machine's git access; embedded /
  local copies (zen-unity) are not checked.
- Test All Ads popup: drag handle at the top left, minimize / expand / close at the top right.

## [1.2.1] - 2026-10-02
- No token check any more: TokenManager / TokenSettings removed from Base.dll and Base.Editor.dll, with
  TokenManager.prefab and the game's Resources/TokenAsset. DataManager loads the save and GameStateManager starts for
  any package id.
- AppsFlyerHelper.IEInit stub for games without AppsFlyer; DebugMode no longer imports UnityEngine.WSA (Android builds).

## [1.2.0] - 2026-10-02
- One package again: com.zen.ads, com.zen.iap and com.zen.firebase merged into com.zen.core (Runtime/Ads, Runtime/IAP,
  Runtime/Firebase; assemblies Zen.Core / Zen.Core.Editor). All GUIDs kept. Ads is always present (DebugMode holds
  the ads debug column); IAP / Firebase / AppsFlyer / Play review & update stay optional through versionDefines.
- Install: com.zen.core + com.zen.plugins.adzative only. Zen > Setup lists and installs the optional SDKs.
- Test All Ads popup: minimize / expand / close icons and a drag handle.
- `ScreenUtils`: Expand with one reference resolution per orientation, rotation event, tablet size cap (replaces the
  aspect-ratio table that cut 15-24% of the UI on 20:9 phones, foldables and tablets). `OrientationLayout`,
  `ScreenMetrics` (mm / dp / canvas units), **Zen > Screen Check**.

## [1.1.0] - 2026-10-02
- Base split into four packages by dependency: core, ads, iap, firebase (+ com.zen.plugins.adzative). All GUIDs kept.
- Game data out of the packages: ad unit IDs in `ZenAdIds` (one row per PlacementType), `AdConfig` and
  `ZenIapSettings` in the game's `Assets/ZenSettings/Resources`. `PlacementType` is fixed; the enum generator is gone.
- `AdConfig.Instance` replaces `DataManager.AdConfig`; missing asset = class defaults.
- Services in core: `ZenAnalytics`, `ZenRemote` (Remote Config keys unchanged), `ZenStore`, `ZenToast`.
- `DataManager.itemDatas` saves any game's `ItemDatas` by asset name (`IItemDatas` in Base.dll); Booster / Outfit
  removed from Base, old saves move once.
- Remove ADs button of banners / MRECs (com.zen.iap) buys `<package name>.remove.ads`, or `ZenIapSettings.removeAdsProductId`
  (must end with `remove.ads`); hidden without com.zen.iap.
- `USE_*` symbols come from the installed packages (asmdef versionDefines).
- **Zen > Setup** window; Android templates shipped in core; splash / sample scenes as a sample of com.zen.ads.
- Test All Ads debug panel (Force Ads, every placement), full-screen native ads, native timing per prefab.

## [1.0.0] - 2026-09-28
- Base is a package (package.json at Assets/Base) with assembly definitions Zen.Base / Zen.Base.Ads.Editor.
- Includes the audit P0 fixes (branch fix/p0-audit).
