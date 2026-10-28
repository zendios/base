# ZPAS v3.0: Zendios Project Asset Standard for Unity

Goal: any asset is found with one keyword, imports with the right settings by itself, and nothing is orphaned or
cross-referenced. Every asset name answers three questions: which **Parent** (feature / entity) it belongs to, what
**technical type** it is, and which **variant / property** it is.

Base itself follows this standard (see [its own names](#how-base-applies-it)); a game built on Base follows it too.

## 1. Casing: the Trinity

| Role | Casing | Examples |
|---|---|---|
| Folders, scenes, entity prefabs (several variants), global VFX prefabs | `Pascal_Snake_Case` | `Vehicle_Lamborghini_Huracan`, `Explosion_Fire_Large`, `Button_CTA.prefab` |
| C# classes, `.cs` files, logic-linked prefabs (1:1 with a class) | `PascalCase` | `VehicleController.cs`, `UIPopupShop.prefab` + `UIPopupShop.cs` |
| Every sub-asset (texture, mesh, material, animation, audio, blend) | `lowercase_snake_case` | `vehicle_lamborghini_huracan_body_tex_albedo.png` |

**Mirror mapping:** a prefab that has exactly one class driving it carries that class's name (`UIManager.prefab` +
`UIManager.cs`, `UIPopupMessage.prefab` + `UIPopupMessage.cs`). The `UI` prefix marks UI classes and their prefabs.

## 2. Sub-asset formula

```
category_parent_part_type_property_variant_number
ui_common_btn_spr_bg_green.png      ui_ads_gradient_spr_horizontal.png      vehicle_huracan_body_mesh_LOD0
ui_ads_star_rating_spr.png          sfx_ui_click.wav (one click sound: no number)  sfx_ui_click_01.wav (two variants)
```

- Order is fixed: the **type infix comes right after the part**, before the property. `ui_ads_spr_gradient` is wrong
  (type before part); `ui_ads_gradient_spr` is right.
- A slot is used only when it has content. A number is added **only when there are several variants** of the same
  asset (`sfx_ui_click_01`, `_02`, `_03`); a single asset has no number. Variants that differ by a *property*
  (direction, colour, state) go in the property slot, not in the number: `ui_ads_gradient_spr_horizontal`,
  `ui_ads_gradient_spr_vertical_left`.
- `type` is the infix automation keys on: `mesh` `tex` `mat` `spr` `ani` `anm` `sfx` `bgm` `vfx`.
- `property`: `albedo` `normal` `orm` `clean` `dirty` `pressed` `fill` `horizontal` `left`...; a number, when used, has two digits (`01`).
- One `_` between words only: no `__`, no leading or trailing `_`. LOD stays upper case: `..._mesh_LOD0`.
- The **parent is always in the name**: the nearest folder holding the asset, skipping type folders (`Images`,
  `Materials`, `Models`, `Textures`, `Animations`...), in lower case. The folders stay as they are, so the parent is
  usually the module folder (`Ads`, `CurrencyManager`, `ConfettiFX`). Never drop the parent to make a name shorter, and
  never move an asset only to rename it: `Ads/Materials/Multiply.mat` -> `ads_multiply_mat`,
  `ConfettiFX/Materials/Multiply.mat` -> `confetti_fx_multiply_mat`, `CurrencyManager/SoftCurrency.mat` -> `currency_manager_soft_currency_mat`.
- Words that repeat the parent at the start are not repeated (`Ads/AdsBanner.png` -> `ui_ads_banner_spr`).
- **How a name starts:** UI sprites and atlases `ui_<parent>_...`; audio `sfx_<parent>_...` / `bgm_<parent>_...`
  (sfx / bgm stand first like a category: the only exception to part -> type); every other asset `<parent>_...`.
- Do not repeat the type: no `animation` in an `_ani` name, no `material` in a `_mat` name (`FillFXAnimation` -> `ads_fill_fx_ani`).
- A lead word (`icon`, `btn`, `button`, `bg`, `bar`, `toggle`) is a part on its own: `icon_remove_ads` -> `ui_ads_icon_spr_remove_ads`.
- Rename with **Base > Rename Assets**: it checks these rules, suggests a name, and flags names still used in code.
  The same rules run headless (section 6): `Base.Tools.RenameAssetWindow.ValidateCli` and `.RenameCli`.

**Forbidden words** (compared word by word, split on `_` and on capitals): `final`, `new`, `temp`, `demo`,
`abc`, `fix`, `backup`, `old`, `copy`, `latest`, `fix_final`. (`golden` does not break `old`.)
`test` is allowed (`ButtonTestAd`, `AdsTestAllPanel`). A debug asset is best named by what it does (`UIManagerDebug`, `AdsDebugPanel`).

## 3. Folders: one Parent = one folder

An asset used by a second Parent moves to `Commons/` (or `Shared/`) and the references are updated; a Parent never
references another Parent's folder.

```
Assets/
  Production/
    Art/        Vehicles/  Characters/  Environments/  Buildings/  Props/
                Commons/   Materials/  Textures/  Shaders/  Animations/
    Fonts/
    UI/         Main/  Shop/  Gameplay/  Commons/
    Scripts/    Core/  Managers/  Controllers/  Handlers/  UI/  Data/
    Audio/      BGM/  SFX/  Voice/
    VFX/
    Scenes/     Production/  Development/Dev_<Name>/
    Shared/     Localization/
  Plugins/      (third-party, never edited)
  Resources/    (only what Base and the game load by name: see below)
  Settings/
```

If the project's own docs (`AGENTS.md`, `CLAUDE.md`) already fix the root name (`Production/Art` or `_Production/Arts`),
the project wins.

An old project with type folders (`Texture2D/`, `AudioClip/`, `Images/`...) moves to this tree with
`Base.Tools.ZpasReorganize.Run` (section 6, step 5): the Parent of an asset is the scene (or `Resources`) that uses it.

**Assets loaded by name** must keep their name and folder, or the code changes with them:
`Resources/ZenAdIds`, `Resources/AdConfig`, `Resources/ZenIapSettings`, `Resources/DOTweenSettings`, and every popup
or screen loaded by `UIPopupBase<T>` / `UIManager.LoadScreen<T>()`: `Resources/<ClassName>.prefab`.

## 4. UI assets

- Sprites start with `ui_`: `ui_<parent>_<part>_spr_<property>.png` (`ui_common_btn_spr_border.png`,
  `ui_iap_pack_spr_ios_01.png`).
- Button states `_normal` / `_pressed` / `_disabled` / `_hover`; toggles `_on` / `_off`; bars `_background` / `_fill`.
- Atlases: `ui_<parent>_atlas_<function>.spriteatlas`, e.g. `ui_ads_atlas_common.spriteatlas`.
- Animator controllers: `ui_<parent>_<part>_anm_<name>.controller` (`ui_common_btn_anm_base.controller`).
- Prefabs: variant prefabs in `Pascal_Snake_Case` (`Button_Base`, `Button_Icon`, `Button_Text`, `Canvas_Base`,
  `Canvas_Base_Portrait`, `Popup_Base`, `Text_Base`); class-driven ones in `PascalCase` (`UIPopupIAP`, `UIManager`).

## 5. Import settings (mobile)

| Asset | Rule |
|---|---|
| Texture `_albedo` | sRGB on |
| Texture `_normal` | Normal Map, sRGB off |
| Texture `_orm` | sRGB off; R = AO, G = Roughness, B = Metallic |
| Every texture | ASTC on Android / iOS, power-of-two size, mipmaps off by default, Read/Write off |
| Texture shown much smaller than its size (3D at a distance, a far background scaled down) | mipmaps on: without them it shimmers |
| Mesh | Read/Write off (unless code reads vertices or a MeshCollider needs it), compression Medium / High |
| Audio `_sfx_` | Decompress On Load |
| Audio `_bgm_` | Streaming or Compressed In Memory |

ASTC: a texture without an Android override already gets ASTC when Player Settings > Android > Texture Compression is
ASTC (Unity 6's default); an override only changes the block size. Measure the APK before and after: on a 2D game
(Stickman remaster) forcing ASTC 6x6 on 96 such textures saved 0.3% of the APK and no RAM.

Localization: `Production/Shared/Localization/loc_text_<lang>.csv`, voice `loc_voice_<char>_<line>_<lang>`.
Documents: `PROJECT_DocType_Topic_vVersion_Language` (`ZPAS_Manual_v2.3_VN.pdf`).

## 6. Working on an existing project

1. **Read the context first**: Unity version (`ProjectSettings/ProjectVersion.txt`), render pipeline,
   `Packages/manifest.json`, the project's own conventions, and every place that loads assets by string
   (`Resources.Load`, `Resources.LoadAll`, Addressables). Never rename or move those without changing the code.
2. **Audit (read-only)**: `Base.Tools.ZpasAudit.Run` (`-zpasRoot` to limit it) writes `Logs/Zpas/audit.csv` and
   `summary.txt`: names with the Rename Assets rules, folder casing; texture POT / Read-Write / sprite mipmaps / Android
   format (automatic counts as ASTC when it is) / data-map sRGB; audio load type by length; mesh Read/Write. Plugins,
   Editor and third-party SDK folders are skipped. Report counts per category, not the whole list.
3. **Pipeline for new assets** (safest, do it first): an `AssetPostprocessor` in the game limited to
   `Assets/Production/**` that applies section 5 to new assets (`assetImporter.importSettingsMissing`), and the naming
   gate for CI: `Base.Tools.RenameAssetWindow.ValidateCli` (`Logs/Zpas/naming.csv`, exit code 1 while names fail; the
   menu item is Base > Rename Assets).
4. **Optimize old assets without renaming**: apply section 5 to existing textures / audio / meshes. Turn Read/Write off
   only after grepping the code for `GetPixel(s)` / `SetPixel(s)` / `ReadPixels` / `GetRawTextureData` (textures) and
   `.vertices` / `.triangles` on imported meshes. Keep a before / after CSV.
5. **Rename / move** (risky, needs the owner's approval): always `AssetDatabase.RenameAsset` / `MoveAsset` so GUIDs
   stay; never rename on the file system. Assets loaded by string: change code and data in the same commit, or move to
   Addressables with the old path as the address. One Parent per commit, build after each batch. If a remaster is
   coming, do steps 3-4 only and leave renaming for the new art.
   - Folders: `Base.Tools.ZpasReorganize.Run [-zpasFeatures Menu=Main,Game=Gameplay] [-zpasResourcesFeature Gameplay]`
     writes the plan (`Logs/Zpas/reorganize.csv`); the same call with `-zpasApply` moves the assets and re-points
     Build Settings. Fix scene paths written in Editor tools in the same commit.
   - Names: `Base.Tools.RenameAssetWindow.RenameCli [-zpasRoot <folder>]` writes `Logs/Zpas/rename.csv` (suggestion,
     its check, names still in code); with `-zpasApply` it renames only rows whose suggestion passes and whose old name
     is in no `.cs` file. The rest is done in the window.
6. **Verify**: headless compile, audit again, build the APK / AAB and compare size + RAM before / after. Report real
   numbers and say what was not checked on a device.

```bash
# run an Editor method headless (the project must be closed in the Editor)
"<UNITY>/Editor/Unity.exe" -batchmode -quit -nographics -projectPath . -buildTarget Android \
  -executeMethod <Namespace.Class.Method> -logFile Logs/<task>.log
grep -E "error CS" Logs/<task>.log
```

## 7. Safety rules

- Never edit `.meta` files or GUIDs by hand; never delete an asset before searching every YAML file for its GUID.
- Never touch `Plugins/` or third-party packages; never edit files inside `Packages/com.zen.*` (see
  [Architecture.md](Architecture.md), "Extend instead of editing").
- Every bulk change ships with its CSV report in its own commit, so it can be reverted alone.
- New names (tools and temporary files included) never contain a forbidden word.

## How Base applies it

| Base asset | Role |
|---|---|
| `UIManager.prefab`, `UIManagerDebug.prefab`, `UIPopupMessage.prefab`, `UIPopupIAP.prefab`, `AdsManager`, `DataManager`, `CurrencyManager`, `IAPManager` prefabs | class-driven, `PascalCase` |
| `Button_Base`, `Button_CTA`, `Button_Icon`, `Button_Text`, `Canvas_Base`, `Canvas_Base_Portrait`, `Event_System`, `Icon_Base`, `Popup_Background`, `Popup_Base`, `Text_Base`, `UI_Camera`, `ADsManager_Portrait` | variant prefabs, `Pascal_Snake_Case` |
| `ui_common_btn_spr_border.png`, `ui_common_btn_spr_bg_green.png`, `ui_common_icon_spr_blank.png`, `ui_iap_pack_spr_ios_01.png`, `ui_common_btn_anm_base.controller` | sub-assets, `lowercase_snake_case` |
| `Resources/ZenAdIds.asset`, `Resources/AdConfig.asset`, `Resources/ZenIapSettings.asset` | loaded by name: fixed |
