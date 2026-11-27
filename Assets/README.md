# CyberWizard TD 可玩原型

目前內容是第一關「鏽巷」（`Level_1`）：敵人沿兩條路線攻向北側黃銅閘門，玩家利用三座固定塔與自身攻擊防守，並用擊殺獲得的能量升級塔。

功能、技術細節與驗證紀錄請見 `Docs/IMPLEMENTATION_REPORT.md`；企劃資料在 `Ref/`。

## 執行

1. 使用 Unity 2022.3.62f3 開啟專案。
2. 開啟 `Assets/Scenes/MainMenu.unity`（Build Settings 第一個場景），進入 Play Mode。
3. 按「開始」進入選關畫面，選「第 1 關」進入 `Level_1`；也可以直接開啟 `Level_1.unity` 遊玩。

遊戲流程：`MainMenu`（開始／離開）→ `LevelSelect`（三個關卡格子，目前只有第 1 關開放；左上角「←」回初始畫面）→ `Level_1` → 結算面板（勝利／失敗、再次挑戰、確認回選關）。

所有場景直接在場景中編輯，沒有場景產生器。

## 操作

- `WASD`：依攝影機方向移動；空白鍵：跳躍；滑鼠：轉動視角。
- 滑鼠左鍵：攻擊準星對準、且在攻擊範圍（9）內的敵人。
- 靠近塔時按 `1`／`2`／`3`：升級砲塔／塔身／塔底。
- `Esc`：暫停／繼續（暫停面板：繼續、重新開始、中離回選關）。

## 畫面提示

- 地面上的紅色發光箭頭沿著下一波敵人的路線，朝閘門方向循環流動。
- 玩家與塔的攻擊範圍以貼地環帶表示：範圍內沒有敵人時為白色，有敵人時為紅色。所有射程圈都持續顯示（塔被摧毀時才隱藏）。
- 右上角是全圖小地圖，左上角 HUD 顯示能量、血量、閘門血量與波次。
- 勝利或失敗後約 2 秒（真實時間）出現結算面板，游標會被釋放，可按「再次挑戰」或「確認」。
- 閘門被打破時，黃銅門板會碎裂倒塌（OpenFracture），碎片散落在閘門前方。

## 資料位置

| 資料 | 位置 |
| --- | --- |
| 場景 | `Assets/Scenes/MainMenu.unity`、`LevelSelect.unity`、`Level_1.unity` |
| 波次 | `Assets/StreamingAssets/Levels/level1_rust_alley.json` |
| 敵人數值 | `Assets/Data/Enemies/*.asset` |
| 塔數值與升級表 | `Assets/Data/Towers/FixedTowerProfile.asset` |
| 敵人 NavMesh（執行時烘焙） | 場景 `Level1_Systems` 的 `LevelNavMesh` 元件 |
| 按鍵配置 | `Assets/Scripts/Infrastructure/GameInput.cs` |
| 後製（Bloom） | `Assets/Settings/Level1PostProcess.asset` |
| 程式碼 | `Assets/Scripts/`（組件 `CyberWizard.Runtime`） |
| 測試 | `Assets/Tests/EditMode`、`Assets/Tests/PlayMode` |

## 外部套件

- OpenFracture 1.0.2（MIT，`Packages/manifest.json` 以 git URL 安裝並鎖定 commit）：閘門破碎效果。第一次開啟專案時需要 git 與 GitHub 連線。

## 測試

在 Test Runner 中執行 EditMode 與 PlayMode 測試。目前結果與已知失敗項目請見 `Docs/IMPLEMENTATION_REPORT.md` 最後一節的驗證表。
