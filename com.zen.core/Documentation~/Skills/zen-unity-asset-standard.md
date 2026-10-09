---
name: "zen-unity-asset-standard"
description: "Áp dụng chuẩn ZPAS v3.0 cho dự án Unity: audit đặt tên, thư mục, thiết lập import texture/audio/mesh, dựng AssetPostprocessor và Naming Linter."
---

# ZPAS v3.0 cho dự án Unity

Mục tiêu: asset tìm được bằng một từ khoá, import tự động đúng thông số, không có asset rác hay tham chiếu chéo.
Mọi asset phải trả lời được: thuộc Parent nào, loại kỹ thuật gì, biến thể/thuộc tính gì.

## 1. Luật chuẩn (tóm tắt ZPAS v3.0)

**Casing (Trinity of Casing)**
| Vai trò | Casing | Ví dụ |
|---|---|---|
| Thư mục, Scene, Entity Prefab (đa biến thể), Global VFX prefab | `Pascal_Snake_Case` | `Vehicle_Lamborghini_Huracan`, `Explosion_Fire_Large` |
| C# class, file `.cs`, Logic-linked prefab (1:1 với code) | `PascalCase` | `VehicleController.cs`, `UIPopupShop.prefab` |
| Mọi sub-asset (texture, mesh, material, anim, audio, blend) | `lowercase_snake_case` | `vehicle_lamborghini_huracan_body_tex_albedo.png` |

**Công thức sub-asset:** `category_parent_part_type_property_variant_number`
- Thứ tự cố định: **part → type → property**. Type infix đứng ngay sau part, trước property.
  Đúng: `ui_ads_gradient_spr_horizontal`, `ui_common_btn_spr_bg_green`. Sai: `ui_ads_spr_gradient_horizontal` (type đứng trước part).
- type (infix cho automation): `mesh` `tex` `mat` `spr` `ani` `anm` `sfx` `bgm` `vfx`
- property: `albedo` `normal` `orm` `clean` `dirty` `pressed` `horizontal` `left`...; hướng, màu, trạng thái là property.
- **Số chỉ khi có nhiều biến thể** của cùng một asset: `sfx_ui_click_01`, `_02`, `_03`. Một asset đơn lẻ không có số
  (`ui_ads_star_rating_spr`, không phải `_01`). Khi có số thì luôn 2 chữ số.
- Chỉ một `_` làm dấu phân tách; cấm `__`, `_` ở đầu/cuối. LOD viết hoa: `..._mesh_LOD0`.
- **Parent luôn có trong tên** = thư mục gần nhất chứa asset, không tính thư mục loại (`Images`, `Materials`,
  `Models`, `Textures`, `Animations`...), viết thường. Giữ nguyên cấu trúc thư mục hiện có: parent thường là thư mục
  module (`Ads`, `CurrencyManager`, `ConfettiFX`). Không bao giờ bỏ parent để tên ngắn hơn, và không di chuyển asset chỉ để đổi tên.
  `Ads/Materials/Multiply.mat` → `ads_multiply_mat`, `ConfettiFX/Materials/Multiply.mat` → `confetti_fx_multiply_mat`,
  `CurrencyManager/SoftCurrency.mat` → `currency_manager_soft_currency_mat`.
- Từ đầu tên trùng parent thì không lặp lại (`Ads/AdsBanner.png` → `ui_ads_banner_spr`, không phải `ui_ads_ads_banner_spr`).
- **Đầu tên:** sprite UI và atlas `ui_<parent>_...`; audio `sfx_<parent>_...` / `bgm_<parent>_...` (sfx/bgm đứng đầu
  như category: ngoại lệ duy nhất của thứ tự part → type); asset khác `<parent>_...`.
- Không lặp nghĩa type: không viết `animation` trong tên `_ani`, `material` trong `_mat` (`FillFXAnimation` → `ads_fill_fx_ani`).
- Từ đầu là part riêng khi là `icon`, `btn`, `button`, `bg`, `bar`, `toggle`: `icon_remove_ads` → `ui_ads_icon_spr_remove_ads`.
- Đổi tên: menu **Base > Rename Assets** (kiểm các luật trên, gợi ý tên, báo tên còn được dùng trong code).

**Từ cấm:** `final, new, temp, test, demo, abc, fix, backup, old, copy, latest, fix_final` (so theo từng từ, tách theo `_` và chữ hoa; `golden` không vi phạm `old`).

**Thư mục:** "One Parent = One Folder". Asset dùng lại ở Parent thứ 2 phải chuyển vào `Commons/` (hoặc `Shared/`) và cập nhật tham chiếu. Cấm tham chiếu chéo giữa các Parent. Cây gốc:
`Production/{Art/{Vehicles,Characters,Environments,Buildings,Props,Commons/{Materials,Textures,Shaders,Animations}}, Fonts, UI/{Main,Shop,Gameplay,Commons}, Scripts/{Core,Managers,Controllers,Handlers,UI,Data}, Audio/{BGM,SFX,Voice}, VFX, Scenes/{Production,Development/Dev_<Name>}}`, `Plugins/` (không sửa), `Settings/`.
Kiểm tra tài liệu dự án (AGENTS.md, CLAUDE.md): nếu dự án đã chốt tên gốc (`Production/Art` hay `_Production/Arts`) thì theo dự án.

**UI:** sprite bắt đầu `ui_`; nút có trạng thái `_normal/_pressed/_disabled/_hover`; toggle `_on/_off`; bar `_background/_fill`; atlas `ui_<parent>_atlas_<function>.spriteatlas` (type infix `atlas`, đứng sau parent như part, ví dụ `ui_ads_atlas_common.spriteatlas`). Prefab UI và script cùng tên (Mirror Mapping: `UIPopupShop.prefab` + `UIPopupShop.cs`).

**Import (mobile):**
- Texture: `_albedo` sRGB On; `_normal` Normal Map + sRGB Off; `_orm` sRGB Off (R=AO, G=Roughness, B=Metallic). ASTC trên Android/iOS; kích thước lũy thừa 2; mipmap Off mặc định; Read/Write Off.
- Mesh: Read/Write Off (trừ khi code đọc đỉnh hoặc MeshCollider cần); nén Medium/High.
- Audio: `_sfx_` → Decompress On Load; `_bgm_` → Streaming hoặc Compressed In Memory.
- Localization: `Production/Shared/Localization/loc_text_<lang>.csv`, `loc_voice_<char>_<line>_<lang>`.
- Tài liệu: `PROJECT_DocType_Topic_vVersion_Language` (ví dụ `ZPAS_Manual_v2.3_VN.pdf`).

## 2. Quy trình làm việc

1. **Đọc bối cảnh**: phiên bản Unity (`ProjectSettings/ProjectVersion.txt`), render pipeline, `Packages/manifest.json`, quy ước riêng của dự án, và những chỗ code nạp asset theo chuỗi (`Resources.Load`, `Resources.LoadAll`, Addressables address). Ghi lại các thư mục bị nạp theo đường dẫn: **không đổi tên/di chuyển chúng** nếu chưa sửa code.
2. **Audit (chỉ đọc)**: viết một Editor script quét `AssetDatabase.GetAllAssetPaths()` (bỏ `Plugins/`, `Editor/`, `TextMesh Pro/`, package) và xuất CSV vào `Logs/Zpas/`:
   - naming: casing theo vai trò, từ cấm, `__`, số 2 chữ số, thiếu type infix;
   - texture: kích thước, POT, Read/Write, mipmap, sRGB, định dạng Android/iOS;
   - audio: độ dài clip, Load Type; mesh: Read/Write.
   Báo cáo cho người dùng bằng con số (bao nhiêu vi phạm theo loại), không liệt kê hết.
3. **Pipeline cho asset mới** (an toàn nhất, làm trước): `AssetPostprocessor` chỉ tác động lên `Assets/Production/**` (không reimport art cũ), dùng `OnPostprocessAllAssets` + `SaveAndReimport` có chặn vòng lặp; đặt thông số theo infix/hậu tố ở mục 1. Kèm `NamingValidator` có menu và hàm CLI `ValidateCli` thoát mã 1 khi có vi phạm để làm CI gate.
4. **Tối ưu asset cũ không đổi tên** (an toàn, lợi ngay): áp thông số import của mục 1 cho texture/audio/mesh hiện có. Chỉ tắt Read/Write sau khi grep code không có `GetPixel(s)/SetPixel(s)/ReadPixels/GetRawTextureData` (texture) hoặc `.vertices/.triangles` trên mesh import. Ghi CSV trước/sau.
5. **Đổi tên / di chuyển theo ZPAS** (rủi ro, cần người dùng duyệt):
   - Luôn dùng `AssetDatabase.RenameAsset` / `MoveAsset` để giữ GUID; không đổi tên bằng hệ thống file.
   - Asset nạp theo chuỗi: sửa code và dữ liệu (level, JSON, ScriptableObject) cùng lúc, hoặc chuyển sang Addressables với address = đường dẫn cũ.
   - Làm theo từng Parent, commit riêng, build thử sau mỗi đợt.
   - Nếu dự án sắp làm lại art (remaster), chỉ làm bước 3-4 và để đổi tên cho art mới.
6. **Kiểm chứng**: compile headless, chạy lại audit, build APK/AAB và so kích thước + RAM trước/sau. Báo số liệu thật, nói rõ phần chưa kiểm tra trên thiết bị.

## 3. Lệnh headless hữu ích

```bash
# Chạy một hàm Editor (Unity phải đóng dự án)
"<UNITY>/Editor/Unity.exe" -batchmode -quit -nographics -projectPath . -buildTarget Android \
  -executeMethod <Namespace.Class.Method> -logFile Logs/<task>.log
# Kiểm tra lỗi compile
grep -E "error CS" Logs/<task>.log
```
Nếu log báo "another Unity instance is running", nhờ người dùng đóng Editor hoặc chạy menu tương ứng trong Editor.

## 4. Nguyên tắc an toàn

- Không đổi `.meta`/GUID thủ công; không xoá asset khi chưa kiểm tra tham chiếu (tìm GUID trong mọi file YAML).
- Không đụng `Plugins/` và package bên thứ ba.
- Mọi thay đổi hàng loạt phải có báo cáo CSV và nằm trong commit riêng để dễ hoàn tác.
- Tên mới không được chứa từ cấm; bản thân công cụ và file tạm cũng tuân thủ.