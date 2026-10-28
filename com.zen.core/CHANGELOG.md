# Changelog (com.zen.core)

## [1.3.0] - 2026-10-28
- Unity 6000.6.7f1 or newer is required (package.json unity 6000.6 / unityRelease 7f1, also com.zen.plugins.adzative);
  dependencies ugui 2.6.0, Input System 1.20.1. Base.dll / Base.Editor.dll built against the 6000.6.7f1 assemblies
  (zen-base/libs adds UnityEngine.ScriptingModule: PreserveAttribute moved there). Notes and the check list for the
  owner: UNITY66_UPGRADE.md at the repo root.
- Android Minimum API Level 26 (Unity 6.6 drops 25): Setup (Core) flags a lower level, Set 26.
- Gradle templates follow Unity 6.6: Android Gradle Plugin 9.1.1, no jniDebuggable in the launcher debug build type.
- ShortcutManager (Android app shortcuts, zen-shortcuts.aar) removed; to be rewritten.
- Export test package and the DOTween install use AssetPackage.Package.Export / Import (AssetDatabase.ExportPackage /
  ImportPackage are obsolete on 6.6); the export still needs no Unity organization ID.
- ZPAS tools run headless (from the Stickman Dismounting remaster): `RenameAssetWindow.ValidateCli` (CI naming gate,
  `Logs/Zpas/naming.csv`, exit 1 while names fail), `RenameAssetWindow.RenameCli` (dry run, `-zpasApply` renames only
  rows whose suggestion passes and whose old name is in no .cs file), `ZpasAudit.Run` (names with the Rename Assets
  rules + folder casing + import settings; an automatic Android format that resolves to ASTC passes),
  `ZpasReorganize.Run` (type folders -> `Assets/Production/<Type>/<Feature>`, plan first, `-zpasApply` moves with
  GUIDs kept). Rename Assets treats AssetRipper type folders (`Texture2D`, `AudioClip`, `GameObject`...) and
  `Production` / `Art` as type folders when it finds the Parent.
- Docs: ZPAS §3 / §5 / §6 name these tools (the `NamingValidator` it described is `ValidateCli`), when mipmaps stay on,
  and what an ASTC override really saves; Architecture §5 describes the `Button_*` structure and how an old game's
  button moves onto it.
- AdZative app open in the AdMob app open style (Android and iOS): a white card over the game dimmed by a 40% scrim
  (was the interstitial "media top" layout on a dark screen). Portrait: media, icon, headline, attribution, pill CTA;
  landscape: card 60% of the width, media left, details right. The "Continue to app" bar sits above the card and no
  longer covers ad assets; before closeAfterSec it is dimmed (0.6) and inactive, the "Continue in N s" text is gone.
- AdZative layouts (Android and iOS), from the UI/UX review on iPhone 13 Pro Max and Galaxy A12; the top bar (pod
  label, countdown, Next, close) is unchanged:
  - Banner: secondary text #B3FFFFFF (was #86868686, 2.2:1, now 9.9:1); landscape CTA #1A73E8 like portrait and MREC
    (was #03A9F4, 2.6:1); landscape advertiser and store in the secondary colour, stars in the shared star yellow.
    Android uses the colour tokens instead of hard-coded values.
  - Green CTA (media centre) #188038 (was #1E8E3E, 4.2:1, now 5.0:1).
  - Media top, portrait (interstitial, rewarded, app open): media + details centred vertically. A short creative
    (square image, 16:9 video) left up to half the screen empty above it. Android MediaFrame app:zenHugHeight, iOS
    AdZativeMediaAlignHug (was AdZativeMediaAlignBottom).
  - Media centre, landscape: the left column is centred vertically; the spacer between the stars and the body is gone.
  - iOS: a store row with no store / price takes no height (Android wrap_content); it took the free height and left a
    gap above the CTA (measured 80 pt).
  - MREC: light grey (#F1F3F4) behind a creative narrower than the media area.
- Unity 6000.4.8f1 or newer is required (package.json unity / unityRelease, also com.zen.plugins.adzative): iOS 27
  SDK builds must use the UIScene life cycle, which no public Unity 2022.3 has. Dependencies: ugui 2.0.0 (TextMeshPro
  is part of it, com.unity.textmeshpro is gone), Input System 1.19.0.
- AppsFlyer 7.0.3 (Setup > SDKs tested version): enableDebug / init / start awaited in sequence; the 6.x calls and the
  APPSFLYER_7 define are gone. AppsFlyer 7 has no ATT wait: Base asks ATT before AppsFlyer starts (AdSplash).
- Android templates (Editor/AndroidTemplates~) are Unity 6's (AGP 9.0.0, Gradle 9.1); the AGP 7.4.2 overrides (R8
  8.1.56 classpath, androidx.browser 1.8.0, suppressUnsupportedCompileSdk) are gone; -Xss16m (R8 on Meta Audience
  Network) and the Jetifier ignore list stay.
  The main template keeps androidx.multidex:multidex (the manifest's MultiDexApplication): without it the app
  crashed at launch (ClassNotFoundException), measured on a Galaxy A12.
- iOSBuildPostProcess: the Podfile hook (pods at the Player Settings iOS version, dSYM, no stripping) runs at order 45,
  after EDM writes the Podfile (40) and before pod install (50). At 10 it found no Podfile, pods kept iOS 12 / 13 and
  Xcode 27 refused to build them.
- iOS 17.0 minimum: Setup (Core) flags a lower iOS Target minimum version (Set 17.0); the Podfile hook, the Xcode
  targets, Info.plist and the frameworks follow it.
- Setup (Core) flags TextMesh Pro shaders from before Unity 6 in Assets/TextMesh Pro/Shaders (TMP 3.x vertex layout:
  UV packed in texcoord1, scale in texcoord1.y): on Unity 6 text drawn with them is invisible (admobFont SDF uses
  TMP_SDF, so every ButtonTestAd label disappeared). Update copies only the shaders of uGUI's TMP Essential Resources
  over them (same GUIDs); TMP Settings and fonts stay.
- Setup (Core) flags Android Gradle templates older than Base's (Android Gradle Plugin major below the one in
  AndroidTemplates~): Replace copies the old ones to Assets/Plugins/Android/OldTemplates~ and puts Base's in their place
  (manifests and proguard stay). Missing templates are still copied as before.
- Firebase: when the Firebase app cannot be created (no or wrong GoogleService-Info.plist / google-services.json),
  FirebaseManager stops after Analytics instead of throwing on GetSessionIdAsync, which ended AdSplash's coroutine and
  left the game on the splash screen.
- iOSBuildPostProcess: framework Info.plists are only rewritten when they ask for a newer iOS than the app (the
  ITMS-90208 case, e.g. UMP's 100.0); lower ones stay untouched (signed vendor bundles, some not writable from Unity).
  Binary plists are read through plutil (macOS) and one unreadable plist no longer stops the others.
- AdZative iOS layouts measured against the approved Android ones (zen_banner, zen_banner_media, zen_mrec,
  zen_inter_media_top / center, zen_appopen): the banner icon was stretched to 167 x 60 when the ad had no advertiser
  (the badge + stars line locked the text column); stars are ratingBarStyleSmall (Material star, 16 cells, 80 x 16,
  step 0.1; were 64 x 12); badge / CTA / stars give way before fixed sizes; Android margins kept when a neighbour is
  missing (icon, body, store, CTA); no-media ads pack to the top; landscape media ratio and media-center split as on
  Android; Android line heights (includeFontPadding); MREC and fullscreen CTAs full width without a constraint
  conflict; top bar ✕ without the extra 8; app open Continue card reaches the screen edge and still takes taps while
  counting down. Layout margins no longer grow with the safe area (a landscape banner under the Dynamic Island had
  its marginStart 4 turned into 31). Measured in portrait and landscape on the iOS 27 simulator.
- AdZative iOS media Crop (Base's default) fills the MediaView: GADMediaView kept its inner image view on aspect fit,
  so images were letterboxed whatever the slot asked. Fullscreen ads: the native ad view and every asset stay in the safe
  area (AdChoices visible in its corner; the AdMob validator rejects assets outside the native ad view); the screen
  strip next to the media of the "media top" layouts (notch / Dynamic Island) takes the media's grey, not black. App open
  "Continue" card: corners evenly rounded (radius at most half its height, as Android clamps it).
- Setup lists iOS 14 Advertising Support (com.unity.ads.ios-support 1.2.0) as required: ATTHelper / AppsFlyerHelper use
  it, an iOS build did not compile without it. AppsFlyer older than 7.0.0 stops the build with an error that points to
  Setup (Base calls the 7.x API only).
- Setup / iOSBuildPostProcess read an iOS version without a minor part ("16", "12"): the minimum-version check missed
  it, and frameworks declaring one had their plist rewritten.
- AdZative media at the creative's aspect ratio (Android and iOS): full-screen layouts and the MREC hold ad_media in
  a frame (MediaFrame / AdZativeMediaFrame) that takes the space the media took; the media inside is sized to
  MediaContent.aspectRatio, as large as fits, so the whole creative shows (a 1.91:1 image lost 64% of its width in
  portrait, a 1:1 image half its height in the MREC). Media top: next to the details; media center, landscape, MREC:
  centred. The MREC and every layout keep their size (the Unity rect is unchanged). No aspect ratio: fills as before.
- AdZative landscape interstitial / rewarded layouts (Android layout-land, iOS): the native ad view starts below the
  top bar (marginTop = zen_top_bar_height), as the layouts' own comment said; the countdown / Next / ✕ no longer sit
  over the ad's assets.
- Android landscape media-center layout: the row no longer centres its children vertically. With the media's
  marginBottom 16dp, LinearLayout put the media 15 px above the native ad view, and the AdMob validator flagged
  "Advertiser assets outside native ad view" (Galaxy A12).
- AdZative_Inter_Default: closeLockedSec 1 (was 10): ✕ / Next stay dimmed and locked 1 s after the countdown when the
  player's CTR is low.
- AdZative test IDs (AdConfig useIdTest): a slot that allows video tries Google's native video test ID first, then the
  image one. The image ID came first and always filled, so the Fallback waterfall never reached the video ID.
- AdZative fullscreen top bar chips (pod label, countdown, Next, ✕) on Android and iOS: #80000000 instead of
  #33FFFFFF. The bar sits on the media and, with the media cropped, white chips vanished on a light creative.
- AdZative banner / MREC: a "show_failed" with reason not_ready (the ad was taken by another slot sharing the ad unit,
  e.g. banner and MREC both on the Google test ID) no longer destroys the slot; the plugin keeps it waiting and shows
  the next ad itself. Before, the banner stayed empty until the game state changed.
- AppsFlyerHelper waits up to 2.5 s for the conversion data callback as intended (the loop never ran).
- Base > Hub > Release > Export test package packs with tar (UPM tarball layout) instead of Client.Pack, which needs a
  Unity organization ID on Unity 6000.4.
- Base.dll / Base.Editor.dll built against the Unity 6000.4.8f1 assemblies (zen-base/libs, no TextCoreModule).
- Unity 6.4 deprecations: UIManager finds itself with FindAnyObjectByType, the ads debug panels call FindObjectsByType
  without a sort mode, TMP textWrappingMode, DONumber tweens use the text object as their id.
- AdZative on iOS: com.zen.plugins.adzative ships an Objective-C plugin (Plugins/iOS/AdZative, source in zen-ios) with
  the same slots, layouts, events and payloads as the Android AAR: banner / MREC, interstitial, app open, rewarded,
  pods, host-driven ✕ / Next / countdown. IOSAdZativeBridge (DllImport) behind IAdZativeBridge; AdZativeIosBuild makes
  the sources iOS-only with -fobjc-arc and links Network.framework. Base AdZative runs on iOS too (it answered
  LoadNotAvailable before); test mode (AdConfig useIdTest) uses Google's iOS native test IDs, dp -> Canvas units use
  the plugin's pixels per point. Not yet built or run on an iPhone.
- AdmobBanner on iOS: GetScreenScale / GetScreenNativeScale / GetIsDownsampling are implemented again
  (Runtime/Ads/Plugins/iOS/AdmobBannerScreen.m); they left with the old ZativeAd framework and an iOS build could not
  link.
- AdZative pools (Android and iOS): a pool whose expired ads were purged inside fill() could send a second request
  (two load_request) while the first was in flight.
- AdMob 11.6.0 (Base and AdZative depend on it). It brings Unity's External Dependency Manager
  (com.unity.external-dependency-manager, Unity registry) instead of Google's EDM4U, which becomes a disabled stub
  (Firebase's dependency on it is still met). Setup > SDKs lists "EDM (Unity)" 2.1.1 in place of EDM4U.
- Unity IAP 5.4.4 tested. Firebase pinned to 14.0.0 (Base compiles against it).
- A game without Firebase is asked once per editor session to integrate it: Install Firebase / Later / Not in this
  game (remembered per project; Base > Hub > Setup can still install it). AdMob, which Base needs, is already asked for.
- Setup > SDKs: the newest Firebase is the newest firebase-unity-sdk tag whose four archives are on dl.google.com
  (a tag can come before the archives), so Update never points at a version that cannot be downloaded.
- Old games keep their players' data: a save written by an older Base with BinaryFormatter (same <package>.gd file)
  was unreadable since 1.2.8, and the next save overwrote it. DataManager now reads it without BinaryFormatter
  (LegacySaveReader, MS-NRBF to JSON with the old field names), copies userData (currency, VIP, Remove Ads, counters),
  keeps the original as .gd.legacy, raises DataManager.OnLegacySaveLoaded(json) for the game's own data and saves JSON
  at once. Any other unreadable save is kept as .gd.unreadable instead of being overwritten. Analytics events
  save_legacy_migrated, save_legacy_failed, save_unreadable.
- ISaveSection + DataManager.RegisterSection: the game saves its own data (levels, cars, extra player fields...) in
  Base's save file under its own key, without editing the package. GameData.GetSection / SetSection.
- Data sample (Package Manager > Base > Samples > Data, scene 98_DataSample): characters and vehicles as ItemDatas,
  per-item upgrades and extra player fields as ISaveSection, a legacy save handler, a custom UIPopupBase popup;
  Tools > Zen Samples > Write Legacy Save writes an old-format save to try the migration.
- Documentation~/UpgradeOldGame.md: steps to move a game from Assets/Base to the packages without losing saves.
- Unity 6: CurrencyManager compiles (no [SerializeField] on the static SoftName / HardName properties).
- ItemBase.resoucePath / prefabInResource are public: a game's ItemBase subclasses could not read them.
- GameMode left Base: each game has its own modes, so the game declares its own GameMode enum (keep its numbers).
  FirebaseManager.LogLevel takes any enum as mode.
- Setup (Ads): error when Google Mobile Ads builds the Standard Android SDK; AdZative needs Next-Gen (native ads
  failed on device with NoClassDefFoundError ...ads.mobile.sdk). Fix all selects Next-Gen in GoogleMobileAdsSettings.

## [1.2.9] - 2026-10-12
- AdMob is required by Base and by its native ads: com.zen.plugins.adzative now depends on com.google.ads.mobile 11.5.0
  (installing AdZative alone also brings AdMob). USE_ADMOB is set in the scripting symbols while the AdMob SDK is in the
  project (package or the old Assets/GoogleMobileAds import), like USE_ADZATIVE, so AdZative and the game see it too;
  without the SDK the symbol is removed and Base offers to install AdMob 11.5.0; the package next to the old import
  (two GoogleMobileAds.dll) is reported.
- TMP Essential Resources are imported silently when the project has none (no "TMP Importer" window); the same call
  as its "Import TMP Essentials" button, for TextMesh Pro 3.x and the ugui 2.0 of Unity 6.

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
