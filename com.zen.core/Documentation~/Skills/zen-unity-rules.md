---
name: "zen-unity-rules"
description: "Nguyên tắc KISS, code C#, hiệu năng mobile và cách làm việc với Zendios khi sửa code hoặc asset trong dự án ZEN (zen-unity, zen-base, Base, AdZative)."
---

# Nguyên tắc làm việc trên dự án ZEN

Áp dụng khi đọc, sửa code hoặc asset trong `D:\Projects\ZEN` (zen-unity, zen-base, zen-editor, game dùng Base).
Mục tiêu: làm đúng việc được giao, bằng cách đơn giản nhất chạy được, không phá thứ đang chạy.
Rút gọn từ 3 e-book Unity trong `zen-unity/Documents/` (C# style guide; design patterns + SOLID; tối ưu mobile) và đã
chỉnh theo code Base thật. Cùng nội dung với `D:\Projects\ZEN\AGENTS.md`.

## 1. Thứ tự ưu tiên

1. Tin nhắn mới nhất của Zendios.
2. Skill này (cùng nội dung `AGENTS.md` ở gốc repo).
3. `zen-unity/Packages/com.zen.core/AGENTS.md`, `Documentation~/Architecture.md` (API các manager), `Documentation~/ZPAS.md` (đặt tên, thư mục, import asset).
4. Ba e-book trong `zen-unity/Documents/` (chỉ tham khảo).

Code thật đúng hơn tài liệu. Tài liệu sai so với code thì báo lại, không tự sửa cho khớp.
Style của dự án thắng style của e-book. Đang sửa file nào thì theo style của file đó.

## 2. Bối cảnh

- Hiện tại Unity **2022.3.62f3**, đích **Android** (game casual). Sắp tới các dự án sẽ nâng lên **Unity 6**.
  Quy tắc: **chỉ dùng cái chạy được trên 2022.3**, và viết sao cho lên Unity 6 không phải sửa (mục 9).
- UI: **uGUI + TextMeshPro + DOTween**. Không dùng UI Toolkit.
- Lõi dùng chung: `Packages/com.zen.core` (Base), `Packages/com.zen.plugins.adzative`, `Base.dll` build từ `zen-base`.

## 3. Cách làm việc với Zendios

- **Trả lời tiếng Việt**, câu đầu là kết quả. Chữ hiển thị trong game và tool debug viết **tiếng Anh**.
- **Đọc trước, sửa sau.** Việc lớn: (1) đọc và báo cáo, (2) đề xuất kế hoạch, (3) làm sau khi được duyệt. Việc nhỏ, đúng phạm vi, dễ hoàn tác thì làm luôn.
- **Đề xuất trước khi**: đổi tên, di chuyển asset, refactor, thêm hệ thống mới, đổi API công khai, đổi giá trị đã nằm trong save hoặc asset. Kế hoạch kết thúc bằng mục **"Cần bạn duyệt"**, câu hỏi đánh số, có phương án đề xuất.
- **Dùng lại cái đã có**: manager của Base, prefab có sẵn (`Button_*`, `Popup_Base`, `ButtonTestAd`...), API Unity (`UnityEngine.Pool`, `JsonUtility`...). Không tự vẽ UI khi đã có prefab làm được.
- **Báo cáo trung thực**: ghi rõ đã kiểm gì (compile, test, máy thật) và chưa kiểm gì. Số đếm bằng script thì nói vậy. Đếm theo nhóm, không đổ danh sách dài. Báo cáo dài đưa vào file `.md`.
- Thấy hai tài liệu mâu thuẫn: ghi rõ điểm mâu thuẫn, không tự chọn một bên khi việc đó đổi kết quả.

## 4. Năm câu hỏi KISS trước khi viết code

Câu nào trả lời "không" thì dừng lại và chọn cách đơn giản hơn:

1. Vấn đề **có thật ngay bây giờ** (tái hiện được hoặc được yêu cầu)? Không làm cho "sau này có thể cần" (YAGNI).
2. Base hoặc Unity **đã có sẵn** cách giải? Có thì dùng. Code tốt nhất là không phải viết code.
3. Đây là **nguyên nhân gốc**, không phải che triệu chứng (`if (x == null) return;`, `try/catch` nuốt lỗi)?
4. Đây là **thay đổi nhỏ nhất** giải được vấn đề? Không sửa lan sang phần không liên quan.
5. Người mới đọc có **hiểu ngay** không cần giải thích?

## 5. Không bao giờ

1. Sửa `Packages/com.zen.core`, `Packages/com.zen.plugins.adzative`, `Assets/Plugins`, SDK bên thứ ba, trừ khi việc được giao chính là sửa package đó trong `zen-unity`. Game thì mở rộng: kế thừa, prefab variant, override `virtual`.
2. Sửa tay `.meta` hay GUID; đổi tên hoặc di chuyển asset bằng file system (chỉ `AssetDatabase.RenameAsset / MoveAsset`); xoá asset khi chưa tìm GUID trong mọi file YAML.
3. Đổi tên hay di chuyển asset load bằng chuỗi (`Resources.Load`, `Resources/<ClassName>.prefab`, `ZenAdIds`, `AdConfig`, `ZenIapSettings`) mà không sửa code cùng lúc.
4. Đổi giá trị số của enum đã nằm trong save hoặc asset (`CurrencyType`, `PlacementType`, `GameState`...).
5. Build APK/AAB, push, publish, release khi chưa được yêu cầu trong tin nhắn mới nhất. Compile check thì được.
6. Đưa ad ID, key, `google-services.json` vào commit công khai. Thêm tay symbol `USE_*` hay `DOTWEEN`.
7. Để lại code bị comment, `Debug.Log` rác, file tạm; đặt tên mới chứa `final / new / temp / demo / old / copy / backup / fix / latest` (`test` được phép).

## 6. Code C#

### Đặt tên (theo code Base, đã chốt)

| Thứ | Cách viết | Ví dụ |
|---|---|---|
| Class, struct, enum, method, property, event, namespace, const | `PascalCase` | `UIManager`, `ShowPopup<T>()`, `IsLoaded`, `Base.Ads` |
| Field private / protected (kể cả `[SerializeField]`, static) | `_camelCase`; không dùng `m_` / `k_` / `s_` | `_isVip`, `_totalSoftCurrency`, `_instance` |
| Field public | `camelCase` không tiền tố (ưu tiên property thay field public) | `maxCount` |
| Biến cục bộ, tham số | `camelCase` | `itemName`, `delaySeconds` |
| Interface | `I` + tên | `IItemDatas` |
| Event | `On` + điều đã xảy ra | `OnScreenChanged`, `OnGameStateChanged`, `OnLoaded` |
| Bool, hàm trả bool | `is / has / can` | `IsTimeToShowAds`, `HasPopup` |
| Method | động từ | `ShowInter`, `AddSoft` |
| Enum | danh từ số ít; `[Flags]` số nhiều | `AdState`, `PlacementType` |

- Quy tắc `_` (chốt theo `UserDataBase`) chỉ áp cho code **mới**, kể cả field mới thêm vào file cũ. Không đổi tên field cũ
  chỉ để theo quy tắc: field serialize mất giá trị trong prefab/scene, field trong save JSON làm người chơi mất dữ liệu,
  field `protected` làm dự án con lỗi compile. Muốn đổi tên field cũ phải được Zendios duyệt từng trường hợp.
- Tên rõ ý, không viết tắt (trừ `i`, `x`, `y`), có đơn vị khi cần. Không lặp tên class trong field (`score`, không `playerScore`).
- Một file một `MonoBehaviour`, tên file trùng tên class. Prefab gắn 1:1 với class thì trùng tên class (ZPAS).
- Enum lưu vào save: ghi giá trị số tường minh, không đổi số đã có.

### Định dạng

- 4 dấu cách, ngoặc Allman, dòng tối đa khoảng 120 ký tự.
- `if` một lệnh ngắn được bỏ ngoặc (như Base); lồng nhau hoặc thân nhiều dòng thì luôn có ngoặc. `switch` có `default`.
- `var` chỉ khi kiểu đã rõ ở vế phải. Không `#region`.
- Thứ tự trong class: field, property, event, hàm Unity (`Awake`, `OnEnable`, `Start`, `Update`, `OnDisable`, `OnDestroy`), hàm public, hàm private. Hàm cấp cao trước, chi tiết sau.

### Unity và Base

- Field hiện Inspector: `[SerializeField] private/protected`, kèm `[Tooltip]` thay comment, `[Range]` cho số có giới hạn.
- Dữ liệu tĩnh dùng chung: `ScriptableObject` (như `AdConfig`, `ItemDatas<T>`). Save: `DataManager` (JSON + AES), không tự viết hệ thống lưu khác, không dùng `PlayerPrefs` cho dữ liệu quan trọng.
- Subscribe event trong `OnEnable`, gỡ trong `OnDisable` / `OnDestroy`.
- Callback SDK (AdMob, Firebase, IAP) đưa về main thread bằng `UnityMainThreadDispatcher.Enqueue` trước khi gọi Unity API.
- Đọc dữ liệu người chơi sau `DataManager.IsLoaded` / `OnLoaded`. Bước khởi động mới gắn vào `AdSplash`, không tạo luồng khởi động thứ hai.
- Dùng API có sẵn: `GameStateManager` (trạng thái), `UIManager` / `UIScreen` / `UIPopupBase<T>` (UI), `AdsManager.ShowInter / ShowReward` (ads), `CurrencyManager.AddSoft / AddHard`, `ZenAnalytics` / `ZenRemote` / `ZenToast`.

### KISS, DRY, SOLID ở mức vừa đủ

- **DRY**: logic lặp **từ lần thứ ba** mới gom thành hàm.
- **Tách class** khi nó đang có hai lý do thay đổi độc lập và đã khó đọc (khoảng trên 300 dòng). Không chia class nhỏ thành nhiều class.
- **Hàm** làm một việc, ít tham số, không tác dụng phụ ngoài điều tên hàm nói. Hàm mới không nhận cờ `bool` để chạy hai chế độ: viết hai hàm.
- **Comment** giải thích tại sao, không lặp lại code. API public mới có `/// <summary>` ngắn (`.editorconfig` bật CS1591). Không TODO cũ, không nhật ký trong code.
- Logic thuần (level, giá, tiền, `ItemDatas`, `FileData`) nên có unit test (Unity Test Framework hoặc `dotnet test` cho zen-base).

### Pattern: dùng khi nào

| Pattern | Trong ZEN | Quy tắc |
|---|---|---|
| Singleton / static manager | Đã có các manager của Base | Dùng cái có sẵn. **Không tạo singleton mới** khi chưa được duyệt. |
| Observer (event C#) | `OnGameStateChanged`, `AdBase.OnAnyStateChanged`, `UIManager.OnScreenChanged` | `event Action<...>`. Nghe event có sẵn thay vì gọi chéo. Không làm EventBus riêng. |
| State machine | `GameStateManager` | Trạng thái game đi qua nó. Đối tượng khác: `enum` + `switch`; class State chỉ khi từ khoảng 5 trạng thái, chuyển đổi phức tạp. |
| Object pool | `UnityEngine.Pool.ObjectPool<T>` | Khi Instantiate/Destroy lặp lại nhiều. Bật `collectionCheck`, đặt `maxSize`. Không tự viết pool. |
| Flyweight | `ScriptableObject` | Dữ liệu tĩnh dùng chung (item, level, config). |
| Factory, Strategy, Command, Dirty flag | Chưa cần | Chỉ khi có đúng nhu cầu, nêu lý do trong kế hoạch. |
| MVP / MVVM, DI container, UI Toolkit binding | Không dùng | Quá nặng cho game casual. |

## 7. Hiệu năng mobile

### Đo trước, sửa sau

- Không đoán. Profiler (CPU + Memory) trên Development Build chạy **máy Android thật**, gồm máy yếu nhất cần hỗ trợ.
- Chỉ dùng khoảng 65% ngân sách frame để máy không nóng: khoảng **11 ms ở 60 fps**, **22 ms ở 30 fps**.
- Báo cáo tối ưu có số **trước / sau** (ms, GC Alloc, dung lượng APK, RAM), không có thì ghi "chưa đo".
- Không đổi `targetFrameRate`, Quality, Player Settings hàng loạt khi chưa được duyệt.

### Code chạy mỗi frame (`Update`, `LateUpdate`, `FixedUpdate`, coroutine lặp)

- Không cấp phát: không `new` list/array, không nối chuỗi, không LINQ, không Regex, không boxing, không lambda bắt biến.
- Không tìm kiếm: `GetComponent`, `Find*`, `Camera.main` lấy một lần trong `Awake` / `Start` rồi cache.
- `CompareTag`; `Animator.StringToHash`, `Shader.PropertyToID` cache sẵn; cache `WaitForSeconds`.
- Không `Update` rỗng. Việc không cần mỗi frame thì chạy theo event hoặc thưa hơn.
- Không `Debug.Log` trong vòng lặp frame; log chẩn đoán đặt trong `#if UNITY_EDITOR` hoặc hàm `[Conditional]`.
- Ngoài vòng lặp frame (khởi tạo, bấm nút, mở popup) LINQ và nối chuỗi dùng bình thường: ưu tiên dễ đọc.

### Object

- `Awake` / `Start` không làm việc nặng. `Instantiate(prefab, parent)` kèm parent / vị trí; `SetPositionAndRotation`.
- Prefab gắn sẵn component thay vì `AddComponent` lúc chạy. Hierarchy nông.

### UI (uGUI)

- Tách Canvas: tĩnh riêng, phần cập nhật thường xuyên (tiền, đồng hồ) Canvas con riêng.
- Tắt **Raycast Target** trên Image / Text không nhận chạm.
- Ẩn hiện thường xuyên: tắt component `Canvas`, không tắt cả GameObject.
- Không lồng Layout Group trong UI gameplay; bố cục tĩnh dùng anchor. Danh sách dài tái sử dụng ô.
- Popup toàn màn hình: tắt camera 3D / Canvas bị che. Canvas World Space / Camera phải gán camera.
- Kiểm nhiều tỉ lệ màn hình bằng Device Simulator; dùng `ScreenUtils`, `SafeArea` của Base.
- Tool debug được dùng Grid / Layout Group, miễn không chạy trong bản phát hành.

### Asset (đặt tên và import theo ZPAS)

- Tên sub-asset: `parent_part_type_property`, type infix đứng **sau part, trước property**
  (`ui_ads_gradient_spr_horizontal`). **Parent luôn có** = thư mục gần nhất chứa asset, bỏ qua `Images`, `Materials`...
  Giữ cấu trúc thư mục hiện có, không di chuyển asset chỉ để đổi tên: `Ads/Materials/Multiply.mat` → `ads_multiply_mat`,
  `CurrencyManager/SoftCurrency.mat` → `currency_manager_soft_currency_mat`. Sprite UI / atlas bắt đầu `ui_`, audio `sfx_` / `bgm_`.
  **Số chỉ khi có nhiều biến thể** cùng loại (`_01`, `_02`); hướng, màu, trạng thái là property.
  Chi tiết: skill `zen-unity-asset-standard` mục 1.
- Đổi tên: menu **Base > Rename Assets** (kiểm ZPAS, gợi ý tên, báo tên còn trong code trước khi đổi).
- Texture: ASTC, tắt Read/Write, tắt mipmap cho sprite / UI, Max Size nhỏ nhất vẫn đẹp, sprite UI vào Sprite Atlas.
- Mesh: tắt Read/Write (trừ khi code đọc vertex), tắt rig / blend shape / normal / tangent không dùng, nén Medium.
- Audio: nguồn WAV; SFX ngắn Decompress On Load (hoặc ADPCM); nhạc nền Streaming; 22050 Hz; Force To Mono khi không cần nổi.
- Asset thử nghiệm, sample plugin không dùng: đề xuất xoá, không tự xoá.

### Bộ nhớ

- `Resources.UnloadUnusedAssets`, `GC.Collect` chỉ lúc chuyển màn / loading. Incremental GC chỉ bật hay tắt sau khi đo.
- Load asset lớn khi cần, giải phóng khi rời màn. Addressables chỉ khi được duyệt.

## 8. Quyết định đã chốt giữa các tài liệu

| Điểm | Chốt |
|---|---|
| Tiền tố field (`m_` của e-book) | `_camelCase` cho private/protected như `UserDataBase`; không dùng `m_`. Chỉ áp cho code mới |
| Tên event (e-book: `DoorOpened`, `On` cho hàm phát) | Theo Base: event có tiền tố `On` |
| Ngoặc nhọn (e-book: luôn có) | `if` một lệnh được bỏ |
| Singleton (e-book: hạn chế) | Dùng manager có sẵn, không thêm mới |
| LINQ / Regex (e-book: tránh) | Chỉ cấm trong vòng lặp frame |
| JSON (e-book: tránh parse) | Save dùng JSON qua `DataManager`; dữ liệu tĩnh dùng ScriptableObject |
| Từ `test` trong tên (ZPAS cũ cấm) | **Được phép** (Zendios đã chốt): `ButtonTestAd`, `Scripts/Test`, `AdsTestAllPanel` giữ nguyên |
| Texture POT (ZPAS: mọi texture) | Chưa chốt nới cho sprite trong Atlas: hỏi trước khi làm khác ZPAS |

## 9. Sẵn sàng cho Unity 6

Viết code compile được trên cả 2022.3 và Unity 6:

- Dùng `FindFirstObjectByType<T>()` / `FindAnyObjectByType<T>()` / `FindObjectsByType<T>(...)` (có từ 2022.3) thay cho `FindObjectOfType` / `FindObjectsOfType` (lỗi thời ở Unity 6). Code cũ đang dùng thì chỉ đổi khi đã sửa file đó hoặc khi nâng cấp.
- Không dùng API chỉ có ở Unity 6 (`Awaitable`, `Rigidbody.linearVelocity`...) khi dự án còn ở 2022.3.
- Không dựa vào API đã đánh dấu `[Obsolete]` trong 2022.3.

Sau khi nâng lên Unity 6 mới cân nhắc (mỗi mục cần đo và được duyệt): `Awaitable` thay coroutine cho luồng async, GPU Resident Drawer và GPU occlusion culling (chỉ khi dùng URP), Spatial-Temporal Post-Processing, Adaptive Probe Volumes. UI Toolkit runtime binding và MVVM vẫn không dùng.

## 10. Trước khi báo "xong"

- [ ] Compile không lỗi (Unity, hoặc headless `-batchmode -quit -nographics -projectPath . -logFile Logs/<task>.log` rồi tìm `error CS`).
- [ ] Không có cảnh báo mới do mình gây ra.
- [ ] Chỉ đụng file cần thiết; diff đọc hiểu trong vài phút.
- [ ] Tên theo ZPAS; code theo mục 6; vòng lặp frame theo mục 7.
- [ ] Báo cáo ghi rõ đã kiểm gì, chưa kiểm gì, còn gì cần Zendios quyết.