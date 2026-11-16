# UI sample

The UI and the game flow of a casual game on Base, without gameplay: scene `Scenes/99_SampleScene` goes through every
GameState with debug buttons, and shows the home, the in-game screen and every popup (result, revive, pause,
settings, shop, daily login, missions, achievements). Copy what you need, swap `GameFlow` for your gameplay.

## Run it

1. Base > Hub > Setup: the Splash must be at Build Settings index 0 (**Create**), with this sample's datas on its
   DataManager (**Add**). Put `99_SampleScene` at index 1.
2. Base > IAP > Sync Catalog from Packs: writes `Assets/Resources/IAPProductCatalog.json` from `Datas/IAPPackDatas`
   (the sample never ships a catalog; set the product ids to your package name first).
3. Open `99_SampleScene` and press Play: Base > Play From Splash starts at the Splash (ATT, data, UMP, Firebase, ads)
   and comes back here. The Game Flow panel (bottom, drag its title to move it) drives every state.

## The flow

| State | Who | UI |
|---|---|---|
| Main | Result closed, Home of the pause popup | `UIScreenMain` (home, daily login, missions, achievements, settings, shop) |
| Load → Init → Ready | Home Play | `UIScreenGame` (Resources): loading, then 3-2-1 |
| Play ↔ Pause | HUD pause, back key, debug Pause | HUD; `UIPopupPause` (Resume, Settings, Home) |
| WaitWin → Win | debug Win | controls hidden, end animation, then `UIPopupResult` |
| WaitLose → ReviveContinue / Lose | debug Lose | controls hidden, `UIPopupRevive` (rewarded ad, countdown), else Lose → `UIPopupResult` |
| Next / Restart → Load | debug Next (after a win), Restart | |

Mode Casual / Racing (debug button) changes what `GameResult` holds: level, stars and score, or place, distance and
coins picked. `UIPopupResult` shows one `StatsItem` row per `GameResult.Row`, the stars and the total.

## Ads

- Revive and "x2 with Ad": rewarded, buttons always shown (Ads.md 4.4).
- The interstitial of a game end (`GameEndAd`, only with `AdConfig.adInterOnComplete`): when the player skips the
  revive (before Lose) and when the result closes (before Main). `AdsManager` decides if it is time, so a second one
  right after the first, or after a rewarded ad, is skipped. Next / Restart show none.

## What is inside

| Folder | Content |
|---|---|
| `Scripts` | `GameFlow` (the states), `GameResult`, `GameEndAd`, `GameFlowTestPanel` (the flow test panel, always shown in the scene), screens `UIScreenMain` / `UIScreenGame`, popups, rows, `SampleBoosterDatas` |
| `Resources` | Popups and `UIScreenGame` (`UIManager` loads `Resources/<ClassName>`: keep the prefab name = the class name) |
| `Prefabs` | Home, daily login cell, mission row, toggles, the shop slots (`IAPButtonExtentExtra`, `IAPButtonExtendAdReward`, `IAPExtraItem`) |
| `Datas` | IAP packs, boosters, daily login, daily missions, achievements, reminder texts |
| `Images` | Shop pictures and reward icons |

Needs: Base (`com.zen.core`), DOTween, Input System. Sounds: `SoundManager` / `MusicManager` (AudioManager in the
Splash); vibration: `HapticManager`.
