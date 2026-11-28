# 開發實作報告

本文件依 `CLAUDE.md` 第 3 節累積記錄每項功能的實作內容。狀態用語：**已完成**、**部分完成**、**尚未實作**、**尚未驗證**。

> 2026-11-27：敵人移動改用 NavMesh，輸入集中到 `GameInput`，並移除沒有使用的程式碼；`EnemyMovementMotor`、`GroundGravity`、`EnemyStatusController` 已刪除。下文提到這些類別的段落是當時的紀錄，詳見最後一節「技術債整理：輸入集中、敵人改用 NavMesh、移除殘留程式碼」。
>
> 2026-11-26：專案結構已調整，原本 `Assets/_Project/` 底下的資料夾移到 `Assets/` 下，本文件中的路徑已全部同步改寫。舊場景 `VerticalSlice`、`SampleScene` 以及只服務它們的程式碼已刪除。下文中提到 `VerticalSlice` 的段落是當時的紀錄，詳見「移除舊版場景與已棄用程式碼」與「專案資料夾結構調整」兩節。

---

## 第一關「鏽巷」灰盒場景（Level_1）

- 紀錄日期：2026-11-22（同日五次修訂：地圖改為 256 × 256 正方形、南側保留上城區、改為上下兩層並移除入口大門、鏽巷整體傾斜而防區保持水平、依 `Ref/level_1_map_v2.png` 改版，見「修改紀錄」）
- 狀態：**已完成**（灰盒階段）。敵人與波次見下一節；塔攻擊、升級、玩家、HUD 與小地圖見第三節「第一關塔攻擊、升級、地面路標、玩家與小地圖」（2026-11-26）。
- 需求來源：`Ref/level_1.txt`、`Ref/第一關地圖.png`；目前配置以 `Ref/level_1_map_v2.png` 為準。

### 功能目的

建立第一關的 3D 灰盒場景，用基本幾何確認地圖配置：外圍高牆、北側黃銅閘門（不可通行）、南側位於下層的上城區（以連續的斜向擋土牆分隔，兩層之間沒有通道）、北高南低的傾斜地形、Y 字形巷弄道路、建築街區、六座鍋爐、三座固定防禦塔（5、7、9 號），以及三個位於水平平台上的防區。場景只提供配置與辨識用途，鍋爐沒有任何遊戲規則。

### 使用技術

- Unity 2022.3.62f3、URP 14.0.12。**沒有新增任何外部套件。**
- 場景物件使用 Unity 內建 Primitive（Cube、Cylinder）；上層與下層地面改用 `TerracePlatform` 產生的多邊形網格，以配合斜向的上下層交界。
- 材質：URP Lit（實體物件）與 URP Unlit（半透明標示與外框線），共 18 個材質資產（新增 `Blockout_Boiler`；`Blockout_Crossroads` 目前已無物件使用，仍保留）。2026-11-26（晚） 道路改用 `Materials/Level1Environment/Env_RoadCobblestone`，`Blockout_Road` 也已無物件使用，仍保留（見「道路無縫銜接與鵝卵石路面」一節）。
- 防區：自訂 `DefenseZone` 元件，以 Ear Clipping 三角化產生填色 `Mesh`，外框使用 `LineRenderer`。
- 水平平台：自訂 `TerracePlatform` 元件，把多邊形擠出成柱體網格（頂面加側壁），並以同一個網格作為 `MeshCollider`。
- 傾斜地形：把鏽巷的地形物件放在同一個旋轉過的父物件 `SlopedTerrain` 底下，整體坡度只由這個父物件的 X 軸旋轉決定。
- 文字標籤：內建 `TextMesh` 搭配內建字型 `LegacyRuntime.ttf`（未使用 TextMeshPro，避免匯入 TMP Essentials）。
- 攝影機：正交投影，自訂 `TopDownMapCamera` 依畫面比例自動取景。2026-11-26 起這台攝影機改名為 `Minimap Camera`，執行時縮成畫面右上角的小地圖；主畫面改為跟隨玩家的第三人稱攝影機（見第三節）。
- 環境光使用 Flat 模式，不需要烘焙光照。2026-11-26 環境光與平行光已調暗，見「敵方路線發光、流動動畫與環境光調暗」一節。

### 實作方式

**新增腳本（皆在既有的 `CyberWizard.Runtime` 組件內）**

| 檔案 | 責任 |
| --- | --- |
| `Assets/Scripts/Gameplay/Level/DefenseZone.cs` | 保存防區編號與邊界頂點；在 `OnEnable`／`OnValidate` 時重建填色網格與外框線。網格標記為 `DontSave`，不寫入場景檔。 |
| `Assets/Scripts/Gameplay/Level/PolygonTriangulator.cs` | 純靜態工具：計算多邊形有向面積，並以 Ear Clipping 切分簡單多邊形（支援凹多邊形與順／逆時針輸入）。 |
| `Assets/Scripts/Gameplay/Level/FixedTower.cs` | 保存塔號，並把 Inspector 上的尺寸參數套用到基座、塔身、砲管三個子物件（2026-11-26 新增可選的砲塔樞紐與砲口）。攻擊、血量與升級由 `FixedTowerCombat` 負責（見第三節）。 |
| `Assets/Scripts/Presentation/TopDownMapCamera.cs` | 依地圖中心、地圖範圍與目前長寬比計算 Orthographic Size 與攝影機位置；只有長寬比改變時才重新計算。 |
| `Assets/Scripts/Gameplay/Level/PolygonPrism.cs` | 純靜態工具：把多邊形擠出成柱體網格資料（水平頂面加往下延伸的側壁），側壁法線一律朝外。頂面三角化沿用 `PolygonTriangulator`。 |
| `Assets/Scripts/Gameplay/Level/TerracePlatform.cs` | 水平平台（地形）：保存邊界頂點與側壁深度，產生平台網格並同步更新同物件上的 `MeshCollider`。網格標記為 `DontSave`；Inspector 修改後延到下一次 `Update` 才重建，避免在 `OnValidate` 中更新碰撞。平台與防區是分開的資料。 |
| `Assets/Tests/EditMode/PolygonTriangulatorTests.cs` | 三角化工具的 EditMode 測試（4 項）。 |
| `Assets/Tests/EditMode/PolygonPrismTests.cs` | 柱體網格工具的 EditMode 測試（3 項）：面數、深度、正面方向與法線朝外。 |

**場景階層（`Assets/Scenes/Level_1.unity`，含波次系統物件，整個場景共 124 個 GameObject（2026-11-26（晚） 六段方塊道路合併為 `Road_Network` 後為 119 個）；波次相關物件見下一節，玩家、攝影機與塔的戰鬥物件見第三節）**

```text
Main Camera                  # 2026-11-26 新增：第三人稱跟隨（PrototypeCameraFollow）+ AudioListener
Minimap Camera               # 原本的全圖俯視攝影機：TopDownMapCamera + MinimapView（右上角小地圖）
Player                       # 2026-11-26 新增，見第三節
Directional Light
Level1_Map
├─ SlopedTerrain             # 旋轉 X = −2°，北高南低；子物件使用本地座標
│  ├─ Ground                 # Ground_Upper（鏽巷地面，TerracePlatform 多邊形，南緣沿斜向擋土牆）
│  ├─ Roads                  # Road_Network（RoadNetwork，2026-11-26（晚） 取代原本六段方塊道路
│  │                         # Road_GateApproach／WestDiagonal／EastDiagonal／InnerLink／South／EastLink）
│  ├─ Walls                  # Wall_North_West/East、Wall_West、Wall_East、
│  │                         # Wall_South（斜向擋土牆，連續無缺口）
│  ├─ Gate_Brass             # Gate_Door、Gate_Pillar_*、Gate_FlankWall_*、Gate_Barrier、
│  │                         # NoPassZone_Gate（紅色禁區）、Gate_Core（Label_Gate 已於 2026-11-26 刪除）
│  ├─ Industrial             # Pipe_NorthWest、Pipe_NorthEast、Pipe_NorthEast_Riser、Pipe_East
│  └─ Buildings              # Block_Central（5 棟）、Block_West（4 棟）、Block_East（1 棟）
├─ Terraces                  # Terrace_05、Terrace_07、Terrace_09（水平平台，含 MeshCollider）
├─ DefenseZones              # DefenseZone_05、DefenseZone_07、DefenseZone_09（各含 Label）
├─ Towers                    # Tower_05、Tower_07、Tower_09（各含 Base、Body、Turret/Barrel、
│                            # Turret/Muzzle/MuzzleFlash、Label；FixedTower + FixedTowerCombat）
├─ Boilers                   # Boiler_01～06（各含 Body、Cap）
├─ UpperCityDistrict         # LowerDistrict_Ground（下層地面，墨綠色，TerracePlatform 多邊形）、
│                            # Wall_UpperCity_West/East/South（下層外圍牆）
└─ EnemyRoutes               # 見下一節
```

**座標與比例**

- 世界 +Z 為北、+X 為東。
- 地圖（含外牆）為 256 × 256 的正方形，中心在世界原點；俯視範圍為 260 × 260。
- 配置座標由參考圖 `Ref/level_1_map_v2.png` 換算：該圖是舊版場景的俯視截圖再加繪新配置，因此用圖中位置不變的 5 棟建築屋頂校正比例（約 4.72 px／單位），再量取道路中線、防區頂點、白色與橘色圓圈的位置。量測誤差約 1～2 單位。
- 地圖分成上下兩層，交界是一道斜向的擋土牆（`Wall_South`），牆中線從西牆 (−128, −92.9) 斜向延伸到東牆 (128, 13.5)，與東西向夾角約 22.6°：
  - 上層是鏽巷（戰鬥區域），位於擋土牆以北。地面整體傾斜 2°（暫定值），以世界原點為軸北高南低：地面高度約為 Z × tan 2°，北緣約 +4.4，擋土牆處約 −3.2（西端）～+0.5（東端）。
  - 下層是上城區，位於擋土牆以南，地面水平，高度 Y = −15（暫定值，修改前為 −10）。約占地圖牆內面積的三分之一。
  - 兩層在擋土牆處的高低差約 11.8（西端）～15.5（東端）。擋土牆從下層地面以下一路延伸到上層地面以上 6。
  - 擋土牆是連續的，上下層之間沒有斜坡或道路相連；敵人從鏽巷左下角的道路生成，不經過下層。
- 三個防區所在的範圍是水平平台（`Terrace_05/07/09`），不跟著地形傾斜：
  - 平台頂面高度 = 平台範圍內最高的地面高度 + 0.6；側壁往下延伸到範圍內最低的地面以下 1，因此平台在下坡（南）側會露出側壁，在上坡（北）側幾乎與地面齊平。
  - 目前頂面高度：防區 5 約 4.88、防區 7 約 3.09、防區 9 約 4.87。
  - 防區標示與塔放在對應平台的頂面上；防區內目前沒有建築（參考圖中的防區內沒有建築）。
  - 防區的原點（即標籤位置）取參考圖中「防區 5／7／9」文字的位置，邊界頂點相對於原點。
- 實體物件（塔、建築、管線、閘門、牆的厚度）維持原尺寸，只改變位置。
- `SlopedTerrain` 底下的物件會跟著地形一起傾斜 2°，包含建築與管線；在這個角度下肉眼幾乎看不出來。鍋爐與塔不在 `SlopedTerrain` 底下，保持直立。
- 道路寬度為 10（`Road_InnerLink` 為 9）。2026-11-26（晚） 起道路資料存在 `Road_Network` 的 `RoadNetwork` 元件中，寬度不變。
- 場景本身是唯一的配置資料來源：所有位置都存在各物件的 Transform 或元件欄位中，程式碼內沒有寫死任何關卡座標。

**防禦塔位置（參考圖白色圓圈）**

| 塔 | 位置 (X, Y, Z) | 砲管朝向（Y 軸） |
| --- | --- | --- |
| `Tower_05` | (−27.5, 4.88, 77.3) | 71° |
| `Tower_07` | (17.2, 3.09, 60.2) | 346° |
| `Tower_09` | (41.1, 4.87, 97.9) | 256° |

三座塔都靠近閘門前的 Y 字路口；砲管朝向是暫定值，一律對準路口 (10, 90)。

**鍋爐（參考圖橘色圓圈）**

| 鍋爐 | 位置 (X, Z) | 所在地面 |
| --- | --- | --- |
| `Boiler_01` | (−20.7, 110.9) | 防區 5 平台頂面 |
| `Boiler_02` | (−118.9, 80.1) | 鏽巷西側空地（取代原本的 `Chimney_NorthWest`） |
| `Boiler_03` | (56.2, 22.8) | 防區 7 平台頂面 |
| `Boiler_04` | (−31.7, −13.9) | 鏽巷中央三角街區 |
| `Boiler_05` | (86.9, −14.6) | 下層（上城區） |
| `Boiler_06` | (−54.1, −72.9) | 下層（上城區） |

- 每座鍋爐由 `Body`（Cylinder，直徑 8、高 6，保留預設碰撞）與 `Cap`（深色 Cylinder，直徑 4.5，無碰撞）組成。尺寸是暫定值。
- 鍋爐只是場景物件，沒有元件或遊戲規則。

**組裝方式**

場景最初由一支一次性的 Editor 腳本，在專案副本上以 Unity 批次模式產生，再把場景與材質（含 `.meta`）複製回本專案。該腳本**沒有**放進專案：若保留，重新執行會覆寫在場景中手動調整的內容。日後修改地圖請直接編輯場景。這一點與 `Ref/後續開發指南.md` 中「先改 Builder 再重建場景」的既有流程不同，該流程只適用於 `VerticalSlice` 場景（2026-11-26 該場景與 Builder 已刪除）。

**碰撞**

- 上層地面（`Ground_Upper`）、下層地面（`LowerDistrict_Ground`）與三個水平平台都有碰撞，日後可以直接在上面行走。兩層地面與平台都使用 `TerracePlatform` 產生的網格作為 `MeshCollider`。平台側壁目前沒有供角色上下的階梯或坡道。
- 保留 Primitive 預設碰撞：牆、閘門、建築、塔、管線、鍋爐本體。
- 已移除碰撞：平面道路、紅色禁區、鍋爐頂蓋（貼地標示或裝飾，避免形成多餘的高低差）。防區與文字標籤本來就沒有碰撞。
- 紅色禁區目前只是視覺標示，實際擋住通行的是閘門本體與兩側牆體。

### 遊戲呈現

- 開啟 `Level_1.unity` 後，全圖俯視畫面（北方在上、整張地圖完整入鏡）在執行時顯示於右上角的小地圖；主畫面改為跟隨玩家的第三人稱視角（2026-11-26，見第三節）。下列顏色與標籤描述以俯視畫面為準。
- 平行光由西北往東南照射，建築陰影落在東南側，上下層的高低差在俯視下可以由斜向擋土牆在下層投下的陰影看出來。
- 地形傾斜在正俯視下看不出來；從斜視角或側面可以看到鏽巷北高南低、防區平台保持水平並在南側露出側壁，以及上城區明顯低於鏽巷。
- 顏色配置：
  - 地面：深灰。
  - 道路：土黃（2026-11-26（晚） 改為鵝卵石貼圖 `Env_RoadCobblestone`）。
  - 建築：兩種灰色。
  - 高牆與擋土牆：暗紫。
  - 塔：象牙白塔身、深藍基座、黑色砲管。
  - 黃銅閘門：黃銅色。
  - 管線：鏽橘色。
  - 鍋爐：較亮的橘色圓柱，頂部有深色圓蓋，俯視下呈橘色圓環，對應參考圖的橘色圓圈。
  - 防區：半透明藍色填色加亮藍外框，底下的平台為藍灰色。
  - 閘門禁區：半透明紅色。
  - 上城區（下層）：墨綠色地面，與鏽巷的深灰地面區分。
- 場景內有文字標籤：「防區 5／7／9」、「5號塔／7號塔／9號塔」。原本的「黃銅閘門（無法通行）」標籤（`Label_Gate`）已於 2026-11-26 依使用者要求刪除。上城區與鍋爐不加文字標籤。
- 道路連通關係（Y 字形，取代原本的中央十字路口）：
  - 閘門 ↔ `Road_GateApproach`（X = 10）↔ 閘門前的 Y 字路口（約 Z = 90）。
  - Y 字路口 ↔ `Road_WestDiagonal`（沿防區 5 東南緣往西南）↔ 左下角與 `Road_South` 會合。
  - Y 字路口 ↔ `Road_EastDiagonal`（夾在防區 7 與防區 9 之間往東南）↔ 東側路口 (97.4, 16.0)。
  - `Road_InnerLink` 沿防區 7 西南緣，連接 `Road_WestDiagonal` (−45.9, 17.6) 與 `Road_South` (24.4, −16.4)，與兩條斜路圍出中央三角街區。
  - `Road_South` 沿擋土牆北側斜向延伸，從西牆 (−126, −82.9) 到東側路口；`Road_EastLink` 從東側路口接到東牆 (126, 19.3)。
  - 下層（上城區）與鏽巷之間沒有道路相連。敵軍從左下角 `Road_WestDiagonal` 與 `Road_South` 交會的路口出發。
  - 2026-11-26（晚）：以上六段合併為 `Road_Network` 中的四條折線，連通關係與中線位置不變，只把各段端點對齊到路口（見「道路無縫銜接與鵝卵石路面」一節）。
- 進入 Play Mode 後會自動開始兩波敵人，詳見下一節「第一關波次與閘門攻防」。

### 可調整參數

| 物件／元件 | 參數 | 用途 |
| --- | --- | --- |
| 任一地圖物件 | Transform | 位置、旋轉、大小；可在 Scene View 直接選取調整。 |
| `Tower_05/07/09` | Transform | 塔的位置與砲管朝向（Y 軸旋轉）。 |
| `FixedTower` | `towerNumber` | 塔號。 |
| `FixedTower` | `baseRadius`、`baseHeight`、`bodyRadius`、`bodyHeight`、`barrelLength`、`barrelThickness` | 基座、塔身、砲管尺寸，修改後自動套用到子物件。 |
| `DefenseZone` | `zoneNumber` | 防區編號。 |
| `DefenseZone` | `boundary` | 防區邊界頂點（相對於該物件的本地 XZ 座標），依序相連並自動封閉。 |
| `DefenseZone` | `outlineWidth`、`outlineLift` | 外框線寬度，以及外框高於填色平面的距離。 |
| `SlopedTerrain` | Transform 的 Rotation X | 鏽巷整體坡度（目前 −2，負值代表北高南低）。修改後平台、防區、塔、鍋爐與路徑點**不會**自動跟著調整高度，需要手動對齊。 |
| `TerracePlatform` | `boundary` | 平台邊界頂點（相對於該物件的本地 XZ 座標）。防區平台目前與對應防區的邊界相同，但兩者是分開的資料，修改其中一個不會影響另一個。 |
| `TerracePlatform` | `depth` | 側壁從頂面往下延伸的深度；需要深到埋入周圍地面。平台頂面高度由物件 Transform 的 Y 決定。 |
| `Ground_Upper`（`TerracePlatform`） | `boundary`、`depth` | 鏽巷地面範圍（南緣沿擋土牆中線）與側壁深度（18，需深到下層地面以下）。它在 `SlopedTerrain` 底下，所以跟著地形傾斜。 |
| `LowerDistrict_Ground` | Transform 的 Position Y、`boundary` | 下層高度（目前 −15）與範圍。修改高度後，下層外圍牆與下層鍋爐**不會**自動跟著調整。 |
| `Wall_South` | Transform | 斜向擋土牆（上下層交界）。 |
| `Boiler_01～06` | Transform；子物件 `Body`、`Cap` 的 Scale | 鍋爐位置與尺寸。 |
| `TopDownMapCamera` | `mapCenter`、`mapSize`、`padding`、`cameraHeight` | 取景範圍、邊緣留白與攝影機高度。目前為中心 (0, 0)、範圍 260 × 260、留白 1、高度 40。 |
| `Assets/Materials/Level1Blockout/*.mat` | Base Color | 各類物件顏色與半透明程度。 |

所有尺寸與位置都是灰盒暫定值，沒有對應到任何已確認的玩法數值。

### 限制與待改善事項

- **配置上的假設（需要確認）：**
  - 依使用者確認，上下層之間不需要通道，已移除斜坡；下層目前只是地形，沒有任何路線經過。
  - 參考圖中沒有中央十字路口與中央排氣孔（`Ref/level_1.txt` 原本要求的物件），因此兩者都已移除，改為參考圖中的 Y 字形路口。
  - 參考圖中的防區內只有塔與鍋爐，原本放在防區平台上的 11 棟建築（`Buildings_OnTerraces`）已全部移除。
  - 參考圖中沒有出現的儲槽與煙囪（`Tank_NorthWest`、`Tank_South`、`Chimney_NorthWest`、`Chimney_East_01/02`、`Chimney_SouthWest`）已移除；`Chimney_NorthWest` 的位置改放 `Boiler_02`。
  - 中央三角街區保留參考圖中可見的 5 棟建築；其中 `Building_Central_B3`、`C1`、`C3` 會碰到新道路，分別往外移動約 3.0、2.3、3.3，與道路保持 1 的間距。參考圖中 B3 與 C3 被塗成道路色，目前仍視為建築。
  - 鍋爐的形狀、尺寸（直徑 8、高 6）與顏色是暫定值；參考圖只標示位置。
  - 塔的砲管朝向沒有依據，暫定一律對準閘門前的 Y 字路口。
  - 下層高度 −15 是依「再低一點」暫定的（修改前 −10）。
  - 參考圖中的道路略有弧度，目前以直線段近似；東側兩條道路會合處以 `Road_EastLink` 直線接到東牆。
  - 傾斜方向假設為北高南低（往上城區方向下降），坡度 2° 是暫定值。
  - 只有防區 5、7、9 做成水平平台；中央三角街區仍跟著地形傾斜。
  - 平台頂面取範圍內最高處，所以平台的下坡側會高出地面：防區 5 南端最高約 5.8，防區 9 南緣約 3.9，防區 7 南緣約 3.3。
  - 上城區（下層）內目前只有地面與兩座鍋爐，沒有建築或道路。
  - 外牆簡化為正方形。
- ~~多段道路在交會處互相重疊，頂面同高，近距離時可能看到 Z-fighting 閃爍~~（2026-11-26（晚） 已改為單一網格、平面投影 UV，重疊處畫面相同，見「道路無縫銜接與鵝卵石路面」一節）。
- 地圖放大後物件相對變小：俯視全圖時，塔號與閘門的文字標籤在 1080p 下已難以閱讀，塔本身只剩小圓點。這些標籤附在維持原尺寸的物件上，所以沒有放大。
- 道路寬度不變而地圖放大，道路以外出現大片空地（深灰色地面）。這些空地沒有任何標示，是否可通行尚未定義。
- 防區只是視覺標示，沒有和塔建立資料關聯。~~尚未提供「某個點是否在防區內」的查詢~~（2026-11-28 已處理，見「待辦清理：測試修正、視覺與流程改善」）。
- ~~防區邊界只能在 Inspector 以數值編輯，沒有 Scene View 的頂點拖曳控制點。~~（2026-11-28 已處理，見「待辦清理：測試修正、視覺與流程改善」）新增或刪除頂點仍需在 Inspector 操作。邊界自交時不會產生填色，並在 Console 顯示警告。
- 道路本身只是可辨識的平面，沒有 NavMesh；敵人路徑與生成點見下一節。原本的入口大門物件已移除。
- 文字標籤使用內建動態字型，中文字形依賴作業系統的後備字型；在沒有中文字型的平台上可能顯示為方框。
- 塔的標籤高度固定在塔底上方 8.5，把塔身調到比這更高時，需要手動移動標籤。
- 從地圖外側的低角度看，北側平台埋在地面下的部分會從外牆底下露出來；俯視與一般斜視角看不到。
- 攝影機高度為 40，是為了讓陰影落在 URP 的 Shadow Distance 內；若使用 Shadow Distance 為 50 的 Balanced 品質設定，改高攝影機後陰影可能消失。
- `Level_1` **沒有**加入 Build Settings（`EditorBuildSettings.asset` 有尚未提交的變更，本次沒有修改它）。
- `Assets/README.md` 與 `Ref/後續開發指南.md` 記載的 EditMode 測試數量（10 項）已與實際不符：新增前實際為 11 項，現在（2026-11-26）EditMode 為 24 項、PlayMode 為 7 項。（2026-11-28 已處理，見「待辦清理：測試修正、視覺與流程改善」）：`Assets/README.md` 已不寫數量；`Ref/後續開發指南.md` 已改為參照本報告。

### 驗證方式

驗證是在專案副本上以 Unity 2022.3.62f3 批次模式執行的；使用者開啟中的編輯器沒有被操作。下表為目前版本（依 `level_1_map_v2.png` 改版後）的結果。

| 項目 | 結果 |
| --- | --- |
| 腳本編譯 | 通過，沒有編譯錯誤或警告。本次沒有修改執行期腳本。 |
| 場景存檔後重新開啟檢查 | 111 個 GameObject，Missing Script 0 個，缺少材質 0 個，`DefenseZone` 3 個，`FixedTower` 3 個，`TerracePlatform` 5 個（3 個防區平台與上、下層地面），鍋爐 6 座。組裝時檢查所有元件的物件參照，沒有指向已刪除物件的參照。 |
| 平台網格與碰撞重建 | 重新開啟場景後五個平台皆有網格（防區 5：25 個三角形；防區 7：16 個；防區 9：19 個；上層地面 10 個；下層地面 10 個），`MeshCollider` 都指向重建後的網格。 |
| 防區網格重建 | 重新開啟場景後三個防區皆有填色三角形（防區 5：7 個；防區 7：4 個；防區 9：5 個），外框點數與邊界頂點數一致。 |
| 俯視攝影機取景 | 以 1:1 輸出截圖，整張地圖完整入鏡。其他長寬比本次沒有重新截圖（攝影機設定沒有修改）。 |
| 畫面辨識 | 把俯視截圖與參考圖並排檢視：三個防區的形狀、三座塔（白色圓圈）、六座鍋爐（橘色圓圈）、Y 字形道路、中央三角街區、斜向擋土牆與下層範圍都與參考圖一致；斜視截圖確認下層明顯低於鏽巷，擋土牆連續、兩層之間沒有通道。 |
| EditMode 測試 | 20 項全數通過。 |
| PlayMode 測試 | 2 項全數通過，詳見下一節。 |
| 在使用者開啟中的 Unity 編輯器內匯入並開啟場景 | **尚未驗證** |
| 在 Inspector 即時修改 `DefenseZone`、`TerracePlatform`、`FixedTower`、`TopDownMapCamera` 參數的互動行為 | **尚未驗證**（程式碼路徑已編譯，但沒有實際手動操作；`TerracePlatform` 依賴編輯模式下的 `Update` 重建網格，也尚未確認） |
| 在 Scene View 選取與拖曳各物件 | **尚未驗證** |
| 以正常速度進入 Play Mode 實際觀看 | **尚未驗證** |

### 修改紀錄

**2026-11-22　地圖改為正方形並放大**

- 修改前：地圖（含外牆）128 × 70 的長方形，7 段道路，109 個 GameObject，攝影機範圍 132 × 80。
- 修改後：地圖（含外牆）256 × 256 的正方形，11 段道路，113 個 GameObject，攝影機範圍 260 × 268。
- 做法：物件位置依新舊牆內範圍的比例換算，物件尺寸與道路寬度不變，防區邊界跟著放大。原本相鄰的道路因為拉開而斷開的地方，新增 `Road_CrossroadsNorth`、`Road_GateApproach`、`Road_South_Link`、`Road_UpperCityEntrance` 四段銜接。
- 場景檔整個重新產生，場景與材質的 GUID 不變；腳本與材質資產沒有修改。

**2026-11-22　地圖南側保留上城區**

- 修改前：整個 256 × 256 都是鏽巷，上城區入口開在地圖南緣的外牆上，入口標示突出到牆外。
- 修改後：地圖仍是 256 × 256 的正方形，但最南側 42 深的範圍保留為上城區；鏽巷南牆北移到 Z −84～−82，入口開在這道牆上；地圖南緣另有一道完整的外牆 `Wall_UpperCity_South`。
- 鏽巷的物件位置依新的牆內範圍（252 × 208）重新換算，物件尺寸與道路寬度不變。
- 新增：`UpperCityDistrict` 群組、材質 `Blockout_UpperCityGround`、外牆 `Wall_UpperCity_South`。GameObject 由 113 個變為 117 個；攝影機範圍由 260 × 268 改為 260 × 260。
- 場景檔整個重新產生，場景與既有材質的 GUID 不變；腳本沒有修改。

**2026-11-22　改為上下兩層、南側道路改斜、移除入口大門**

- 修改前：上城區與鏽巷在同一高度；南牆中央有入口大門（門柱、綠色標示、標籤）；南側道路由 `Road_South_West`、`Road_South_Link`、`Road_South_East` 三段組成。
- 修改後：
  - 上城區改在下層（Y = −10），鏽巷在上層（Y = 0）；鏽巷南牆往下延伸作為擋土牆，下層另有自己的外圍牆。
  - 入口大門的物件全部移除（`UpperCityEntrance` 群組與 `Road_UpperCityEntrance`），南牆中央不再有缺口。
  - 左下角新增斜坡 `Ramp_LowerLeft` 與 `Road_South_RampLanding`，作為敵軍從下層爬上來的位置。
  - 南側道路改為單一斜向道路 `Road_South`（左下到右上），`Road_WestAlley` 與 `Road_CentralDiagonal` 的終點改接到這條路上。
  - 平行光方向由 (55, −35, 0) 改為 (55, 145, 0)，讓南牆的陰影落在下層。
- 材質：刪除已無物件使用的 `Blockout_UpperCityEntrance`；`Blockout_UpperCityGround` 改用在下層地面。GameObject 由 117 個變為 113 個。
- 名稱：依使用者說明，該區名稱仍是「上城區」，只是位置在下層，標籤寫成「上城區（下層）」。
- 場景檔整個重新產生，場景與其餘材質的 GUID 不變；腳本沒有修改。

**2026-11-22　鏽巷整體傾斜、防區平台保持水平、移除上城區標籤**

- 修改前：鏽巷地面水平（Y = 0）；防區只是貼在地面上的半透明標示；上城區有「上城區（下層）」標籤。
- 修改後：
  - 鏽巷的地形物件移到 `SlopedTerrain` 底下，整體傾斜 2°，北高南低。
  - 新增三個水平平台 `Terrace_05/07/09`，防區標示、塔與防區內的建築改放在平台頂面上。
  - 下層外圍牆與 `Ramp_LowerLeft` 移到 `UpperCityDistrict` 群組，保持水平；斜坡頂端改接到傾斜地形的南緣，坡度由約 21° 變為約 15°。
  - 移除 `Label_UpperCityDistrict`。
- 新增程式：`PolygonPrism`、`TerracePlatform`、`PolygonPrismTests`；新增材質 `Blockout_Terrace`。既有腳本沒有修改。
- GameObject 由 113 個變為 118 個；EditMode 測試由 15 項變為 18 項。
- 場景檔整個重新產生，場景與既有材質的 GUID 不變。

**2026-11-22　依 `Ref/level_1_map_v2.png` 改版：新防區、新塔位、鍋爐、斜向上下層交界**

- 修改前：
  - 道路以中央十字路口為中心（含中央排氣孔）。
  - 上下層交界是 Z ≈ −83 的東西向南牆，下層高度 −10，深 42。
  - 三座塔在各防區中央。
  - 防區平台上有 11 棟建築。
  - 鏽巷中有儲槽與煙囪。
- 修改後：
  - 防區 5／7／9 依參考圖重新描邊。
  - 三座塔移到參考圖的白色圓圈位置。
  - 新增 6 座鍋爐（橘色圓圈）。
  - 道路改為 Y 字形加中央三角街區。
  - 上下層交界改為斜向擋土牆，下層高度改為 −15，範圍擴大到約三分之一張地圖。
  - 斜坡改為垂直穿過擋土牆。
- 新增物件：
  - 地面：`Ground_Upper`、`LowerDistrict_Ground`。
  - 道路：`Road_WestDiagonal`、`Road_EastDiagonal`、`Road_InnerLink`、`Road_EastLink`。
  - 鍋爐：`Boilers` 群組。
  - 材質：`Blockout_Boiler`。
- 刪除物件：
  - 地面：`Ground_Plateau`、`UpperCityDistrict_Ground`。
  - 道路：`Road_Crossroads_Central`、`Road_West`、`Road_WestAlley`、`Road_CrossroadsNorth`、`Road_GatePlaza`、`Road_CentralDiagonal`。
  - 地標：`Landmarks`（中央排氣孔）。
  - 工業設施：6 個儲槽與煙囪。
  - 防區平台上的建築：`Buildings_OnTerraces`，共 11 棟。
  - 其他建築：`Building_Central_A1/A2/A3/B1/C4`、`Building_South_01～04`，皆與新道路、新防區或下層重疊。
- 沿用並重新擺放的物件：
  - `Road_South`、`Road_GateApproach`、`Road_South_RampLanding`。
  - 各段牆、`Ramp_LowerLeft`。
  - 平台、防區、塔。
  - 路線與生成點（見下一節）。
- 修改方式：用一次性的 Editor 腳本在專案副本中修改場景，再把場景與新材質（含 `.meta`）複製回本專案；該腳本沒有放進專案。場景與既有材質的 GUID 不變，執行期腳本沒有修改。
- GameObject 由 148 個變為 125 個（含波次系統物件）。

**2026-11-22　移除上下層通道，敵人改由左下角道路出發（使用者指示）**

- 修改前：擋土牆在 X = −100 附近有缺口，`Ramp_LowerLeft` 與 `Road_South_RampLanding` 連接下層與鏽巷；敵人從下層斜坡底部與東牆邊兩處生成，共三條路線。
- 修改後：
  - 刪除 `Ramp_LowerLeft`、`Road_South_RampLanding`。
  - `Wall_South_West/East` 合併為一道連續的 `Wall_South`。
  - 生成點只剩左下角道路上的 `Spawn_SouthWest`。
  - 路線只剩兩條：路徑一直接到閘門；路徑二往東走到東側斜路再左轉往閘門（細節見下一節）。
- GameObject 由 125 個變為 111 個。修改方式同上（專案副本中的一次性 Editor 腳本），沒有修改執行期腳本。

**2026-11-26　刪除黃銅閘門的地面文字標籤**

- 依使用者要求刪除 `Gate_Brass/Label_Gate`（紅色禁區上的「黃銅閘門（無法通行）」文字）。
- 修改前：閘門前的紅色禁區上有白色 `TextMesh` 標籤。修改後：只剩紅色禁區底色，閘門本體、禁區、血條與 HUD 都不變。
- 做法：Unity 編輯器開著時，直接編輯場景檔，移除該物件的 GameObject、Transform、MeshRenderer、TextMesh 四個區塊，以及父物件子清單中的那一項。程式碼與測試都沒有引用這個物件。
- 驗證：在專案副本上截圖，確認文字已消失；EditMode、PlayMode 測試結果見「攻擊範圍改為 Raycast 投影網格、玩家射程放大」一節的修改紀錄。編輯器提示重新載入場景後的狀態**尚未驗證**。

---

## 第一關波次與閘門攻防

- 紀錄日期：2026-11-22
- 狀態：**已完成**（最小可玩流程）。塔攻擊、玩家、獎勵與 HUD 已於 2026-11-26 加入，見第三節；本節描述的波次與閘門規則沒有改變。
- 需求來源：`Ref/開發規劃3.txt`。路線、數值與失敗處理已向使用者確認。

### 功能目的

讓第一關「鏽巷」有實際的敵人進攻流程：
- 共兩波，每個梯隊 4 隻小兵，各梯隊沿指定路線前進。
- 小兵走到黃銅閘門前停下，持續攻擊閘門（血量 100）。
- 閘門被打破即判定失敗。

### 使用技術

- 沿用既有系統：
  - `WaveDirector`：依絕對時間開始波次。
  - `EnemySpawner`：依 ID 生成敵人。
  - `EnemyAgent`：移動與攻擊。
  - `EnemyPath`：路徑點。
  - `LevelScheduleLoader` 與 `LevelScheduleParser`：讀取並驗證 JSON。
  - `BaseCore`：可受傷目標與世界血條。
- 波次資料使用 StreamingAssets 中的 JSON，格式與既有關卡相同（version 1）。2026-11-26（晚） 起 WebGL 執行檔改用 `UnityWebRequest` 讀取，見「StreamingAssets 讀取支援 WebGL」一節。
- 小兵數值沿用既有的 `EnemyLevel0` 資產（使用者指定「舊版」數值）。
- 沒有新增外部套件。

### 實作方式

**新增檔案**

| 檔案 | 責任 |
| --- | --- |
| `Assets/StreamingAssets/Levels/level1_rust_alley.json` | 兩波的波次資料：開始時間、梯隊、生成點、路線、目標。 |
| `Assets/Scripts/Gameplay/GameFlow/GateDefenseFlow.cs` | 關卡流程：`Start` 時讀取 JSON、初始化並開始波次；監聽閘門的 `Destroyed` 與 `AllWavesCompleted`，管理 Ready → Wave → Defeat／Ended 狀態；不含 UI。 |
| `Assets/Tests/EditMode/GateDefenseEditModeTests.cs` | 波次資料內容、移動器跟隨高度（2 項）。 |
| `Assets/Tests/PlayMode/Level1GateDefensePlayModeTests.cs` | 以 20 倍速實際執行 Level_1，從生成一路驗證到失敗後停止（1 項）。 |

**修改既有檔案**（皆為向下相容的新增，`VerticalSlice` 的行為不變）

| 檔案 | 修改前 → 修改後 |
| --- | --- |
| `EnemyMovementMotor.cs` | 只在水平面移動，高度交給重力 → 新增可選的「跟隨目標高度」模式，連同 Y 一起移向路徑點，轉向時只看水平方向。預設關閉。（2026-11-27 已移除，敵人改用 NavMesh，見「技術債整理：輸入集中、敵人改用 NavMesh、移除殘留程式碼」一節） |
| `EnemyAgent.cs` | 新增 `SiegeObjective = "siege"`：走到路徑終點後停住並持續攻擊目標；目標毀壞或沒有目標時也不會離場。新增 `Halt()`：停止所有行動。`Initialize` 新增可選參數 `followPathHeight`：開啟時不加 `GroundGravity`，改由路徑點決定高度。（2026-11-27 已移除，敵人改用 NavMesh，見「技術債整理：輸入集中、敵人改用 NavMesh、移除殘留程式碼」一節） |
| `EnemySpawner.cs` | 新增 Inspector 欄位 `followWaypointHeight`（預設關閉），傳給 `EnemyAgent`。（2026-11-27 已移除，敵人改用 NavMesh，見「技術債整理：輸入集中、敵人改用 NavMesh、移除殘留程式碼」一節） |
| `WaveDirector.cs` | 新增 `StopSchedule()`：停止排程並中止尚未生成完的梯隊。 |
| `EnemyPath.cs` | 新增 `OnDrawGizmos`：在 Scene View 以橘紅色線與球顯示路徑點。 |

修改原因：
- 既有敵人移動時固定高度，只能靠 `GroundGravity` 往下落，無法爬上斜坡或傾斜的鏽巷，所以需要「跟隨路徑點高度」的移動方式。（2026-11-27 已移除，敵人改用 NavMesh，見「技術債整理：輸入集中、敵人改用 NavMesh、移除殘留程式碼」一節）
- 既有的 `attack_base` 目標在抵達後攻擊一次就離場，不符合「停住攻擊閘門」的需求，所以新增 `siege` 目標。

**場景新增物件（`Level_1.unity`）**

```text
Level1_Map
├─ SlopedTerrain/Gate_Brass
│  └─ Gate_Core              # BaseCore，血量 100；位於閘門上方，執行時在上方顯示血條
└─ EnemyRoutes
   ├─ Spawn_SouthWest        # 唯一的生成點 spawn_southwest：左下角道路 (−122.7, −2.75, −81.5)
   ├─ Path_Upper             # path_upper（路徑一），3 個路徑點 WP_00～WP_02
   └─ Path_Right             # path_right（路徑二），4 個路徑點 WP_00～WP_03
Level1_Systems               # EnemySpawner + WaveDirector + GateDefenseFlow
```

**路線**（依使用者指定；路徑點放在道路頂面，依傾斜地形換算高度）

| 路線 | 經過 | 長度 | 以速度 3 走完（計算值） |
| --- | --- | --- | --- |
| `path_upper`（路徑一） | 左下角路口 → 沿 `Road_WestDiagonal` 直接到 Y 字路口 → 閘門前 | 約 237（含生成點到第一個路徑點） | 約 79 秒 |
| `path_right`（路徑二） | 左下角路口 → 沿 `Road_South` 往東，經過第一條路 `Road_InnerLink`，到第二條路 `Road_EastDiagonal` 的路口 (97.4, 16.0) → 左轉沿 `Road_EastDiagonal` 到 Y 字路口 → 閘門前 | 約 370 | 約 123 秒 |

- 兩條路線都從左下角出發、都不經過下層，最後都經過 `Road_GateApproach`，停在閘門擋板前約 4 的位置。
- 2026-11-26（晚）：表中的 `Road_*` 方塊已合併為 `Road_Network`，各段道路中線不變（`Road_GateApproach` 與 `Road_WestDiagonal` 合併為 `GateApproach_WestDiagonal`，`Road_South` 與 `Road_EastLink` 合併為 `South_EastLink`），路線與路徑點沒有修改。
- 「第二條道路」指從左下角往東數的第二條路（東側斜路），這是向使用者確認後的解讀。
- 路線 ID 維持 `path_upper`、`path_right`；原本的 `path_east`、`Spawn_East` 已刪除。生成點 ID 由 `spawn_ramp` 改為 `spawn_southwest`。
- 閘門前的路徑點 `Path_Upper/WP_02` 沿用最初的 `WP_08` 物件，所以 `EnemySpawner.destination` 的參照沒有改變。`EnemySpawner` 的 `secondarySpawnPoint` 已清空。

**波次資料**

| 波次 | 開始時間 | 梯隊（皆由 `spawn_southwest` 生成） |
| --- | --- | --- |
| `wave_01` | 5 秒 | 路徑一 4 隻、路徑二 4 隻 |
| `wave_02` | 45 秒 | 路徑一 4 隻、路徑二 4 隻、路徑二 4 隻 |

- 第二波原本「從東側道路往上」的 4 隻，依使用者選擇改為從左下角出發、走路徑二；數量與時間沒有改變。

- 每個梯隊的生成間隔為 1 秒，目標皆為 `siege`。
- 開始時間與生成間隔是暫定值，使用者沒有指定。

**流程與資料流**

1. 進入 Play Mode：`GateDefenseFlow.Start` 讀取 JSON，交給 `WaveDirector.Initialize` 驗證。驗證內容：敵人 ID、生成點、路線都必須存在。
2. 狀態改為 Wave，開始計時；到達開始時間後，`WaveDirector` 以協程逐隻呼叫 `EnemySpawner.Spawn`。
3. `EnemyAgent` 依序走過路徑點。抵達終點後，以既有的 `EnemyMeleeAttack` 每秒對 `Gate_Core` 發射一次攻擊彈體。
4. `Gate_Core` 血量歸零，觸發 `Destroyed`，`GateDefenseFlow` 依序執行：
   - 呼叫 `WaveDirector.StopSchedule()`，停止生成。
   - 對所有存活敵人呼叫 `Halt()`。
   - 隱藏 `Gate_Door` 與 `Gate_Barrier`。
   - 狀態改為 Defeat，並在 Console 記錄 `[GateDefenseFlow] Defeat：黃銅閘門已被打破`。
5. 若所有波次清除（2026-11-26 起塔與玩家可以消滅敵人，所以可能發生），狀態改為 Ended，並記錄 Victory。
6. 2026-11-26 新增：失敗或勝利時，`GateDefenseFlow` 停用玩家操作並釋放滑鼠游標（若有設定 `player`、`cameraFollow`）。**2026-11-26（晚） 已改為**：勝敗瞬間把 `Time.timeScale` 設為 0.25，玩家操作與滑鼠視角維持啟用；`player` 欄位已移除（見「關卡結束慢動作」一節）。

### 遊戲呈現

- 進入 Play Mode 後不需要任何操作，5 秒後第一波開始。
- 小兵是紅色膠囊體，頭上有血條，全部從鏽巷左下角的道路出現，沿兩條路線前進；下層不會有小兵。
- 路徑一的小兵沿西側斜路直接往閘門走；路徑二的小兵先沿南側道路往東，走到東側斜路再左轉往閘門。
- 小兵到閘門前停下，持續向閘門發射紅色攻擊彈體，閘門上方的血條隨之減少。
- 閘門被打破後，閘門門板與擋板消失，場上小兵全部停住，不再有新的小兵出現。
- 依路線長度計算：第一波路徑一梯隊約在第 84 秒抵達閘門，第一波路徑二梯隊約在第 128 秒才會抵達。停用三座塔時實測在第 94.1 秒判定失敗（上一版為 106.4 秒），此時路徑二的小兵還在路上。
- 塔啟用但玩家不操作時，實測第 98.1 秒失敗（見第三節「限制」）。
- 小地圖（全圖俯視）上小兵只有幾個像素大，因此小地圖另外以紅點標示敵人位置。

### 可調整參數

| 位置 | 參數 | 用途 |
| --- | --- | --- |
| `level1_rust_alley.json` | `startTime`、`count`、`spawnInterval`、`spawnPointId`、`pathId`、`enemyTypeId`、`objective` | 波次時間、每梯數量、生成間隔、生成點、路線、敵人種類、目標。 |
| `Gate_Core`（`BaseCore`） | `maxHealth` | 閘門血量，目前 100。 |
| `Path_*` 的子物件 `WP_xx` | Transform | 路徑點位置；可在 Scene View 拖曳，路線以 Gizmo 線顯示。 |
| `Path_*`（`EnemyPath`） | `pathId`、`waypoints` | 路線 ID 與路徑點順序。 |
| `Spawn_SouthWest` | Transform | 生成點位置。 |
| `Level1_Systems`（`EnemySpawner`） | `spawnPointId`（`spawn_southwest`）、`secondarySpawnPointId`（目前未使用）、`paths`、`enemyDefinitions`、`baseCore`、`behaviorSettings` | 生成點 ID、可用路線、敵人定義、攻擊目標、敵人行為設定（目前未指定，使用程式預設值）。`followWaypointHeight` 已於 2026-11-27 移除。 |
| `Level1_Systems`（`LevelNavMesh`，2026-11-27 新增） | `geometryRoot`（`Level1_Map`）、代理尺寸、坡度、跨步高度、體素與圖塊大小 | 執行時烘焙 NavMesh 的範圍與參數，見最後一節。 |
| `Level1_Systems`（`GateDefenseFlow`） | `scheduleRelativePath`、`gate`、`hideWhenGateDestroyed`、`startOnPlay` | 波次檔案、閘門、破壞後隱藏的物件、是否自動開始。 |
| `Assets/Data/Enemies/EnemyLevel0.asset` | `maxHealth` 50、`moveSpeed` 3、`contactDamage` 3、`attackInterval` 1、`reward` 100（2026-11-26 由 150 改為 100） | 小兵數值。（原本同時被 `VerticalSlice` 使用，該場景已於 2026-11-26 刪除。） |

### 限制與待改善事項

- ~~沒有任何會消滅敵人的機制~~：2026-11-26 已加入塔攻擊與玩家（見第三節）。
- **重疊**：
  - 所有梯隊共用同一個生成點。同一波的路徑一與路徑二梯隊同時生成，生成當下會重疊，離開左下角路口後才分開。
  - 第二波有兩個路徑二梯隊同時生成，會兩隻一組全程重疊前進。
  - 所有小兵停在同一個閘門前的點，會疊在一起。
  - 目前沒有敵人之間的避讓。（2026-11-27 起可在 `EnemyBehaviorSettings.avoidOtherEnemies` 開啟，預設關閉，行為與之前相同。）
- ~~**路徑點與地形分開**：路徑點的高度是依目前的地形坡度算出來的。修改 `SlopedTerrain` 的角度或移動道路後，需要手動調整路徑點。~~ 2026-11-27 起敵人高度由 NavMesh 決定，路徑點只需在地面附近（4 公尺內）；但移動道路後仍需調整路徑點的水平位置。
- ~~**不走重力的敵人**：開啟 `followWaypointHeight` 的敵人不使用 `GroundGravity`，高度完全由路徑點決定。若路徑點不在地面上，敵人會懸空或陷入地面。~~ 2026-11-27 已改用 NavMesh，此限制不再存在。
- **閘門借用 `BaseCore`**：閘門目標使用既有的 `BaseCore` 元件，它的陣營是 Player、名稱含「主堡」語意。目前只有本關使用，沒有另建專用類別。
- **UI 有限**：2026-11-26 起有原型 HUD 顯示勝敗訊息（見第三節）。2026-11-27 起勝敗改由結算面板顯示，並可重新挑戰或回到選關（見「遊戲流程：初始畫面、選關與結算面板」）；同日加入 Esc 暫停面板（見「選關返回按鈕與遊玩中暫停面板」）。
- **波次時間是絕對時間**：不會等上一波清除，與既有關卡規則相同。
- **建置限制**：`Level_1` 仍未加入 Build Settings；PlayMode 測試透過編輯器 API 依路徑載入場景，只能在編輯器中執行。
- **場景組裝**：場景是用一次性的 Editor 腳本在專案副本中組裝的。該腳本沒有放進專案，後續修改請直接編輯場景。

### 驗證方式

驗證在專案副本上以 Unity 2022.3.62f3 批次模式執行，使用者開啟中的編輯器沒有被操作。

| 項目 | 結果 |
| --- | --- |
| 腳本編譯 | 通過，沒有編譯錯誤或警告。 |
| 場景組裝檢查（目前版本） | 111 個 GameObject，Missing Script 0 個，2 條路線，路徑點皆無空值。`EnemySpawner` 的 `spawnPoint`、`destination`、`baseCore`、`paths` 皆指向現有物件，`secondarySpawnPoint` 為空（刻意清空）；場景中沒有指向已刪除物件的參照。 |
| 路線截圖 | 初版在俯視截圖中暫時畫出路線（不存進場景）確認。目前版本的路線**沒有**截圖，只由下方 PlayMode 測試確認。 |
| EditMode 測試 | 20 項全數通過。波次資料測試已改為檢查：第二波的路線為路徑一、路徑二、路徑二，所有梯隊都由 `spawn_southwest` 生成。 |
| PlayMode 測試（目前版本） | 2 項全數通過：既有的 `VerticalSlice` 測試，以及 Level_1 測試。（2026-11-26 新增第三節的 5 項後，PlayMode 共 7 項，全數通過。） |

Level_1 PlayMode 測試確認了以下事項（2026-11-26 起，此測試一開始先停用三座塔，以驗證「無人防守」時的流程）：
- 兩波皆開始：第 5 秒、第 45 秒。
- 20 隻小兵全部生成。
- 每隻小兵第一次出現時，與左下角路口的水平距離都小於 8。
- 小兵的高度始終高於 −6，即沒有進入下層。
- 有小兵走到路徑二在東側斜路的轉彎點附近（2 以內）。
- 行進中小兵腳底與地面的距離始終小於 0.5。
- 第 94.1 秒閘門被打破（上一版 106.4 秒，最初 122.5 秒），狀態變為 Defeat，`Gate_Door` 已隱藏。
- 有小兵停在閘門前。
- 之後 60 幀內沒有新生成，所有小兵位置不變。

第一次執行 PlayMode 測試時失敗：結果是 Ended 而不是 Defeat。原因是 `EnemySpawner` 沒有設定攻擊目標，小兵抵達後依既有邏輯離場。修正方式有兩項：
- 組裝時設定 `baseCore`。
- `siege` 目標在沒有攻擊目標時也停在原地。

修正後重跑，兩項 PlayMode 測試皆通過。

地圖改版後的第一次 PlayMode 測試也失敗了，錯誤為 `EnemySpawner 缺少出生點或終點`：
- 原因：組裝腳本刪除並重建了路徑點，`EnemySpawner.destination` 原本指向 `Path_Upper/WP_08`，因此變成空值。
- 修正：改為沿用既有的路徑點物件。
- 結果：重跑後兩項 PlayMode 測試皆通過。

`Level1GateDefensePlayModeTests.cs` 中「從下層斜坡爬上鏽巷」的檢查已隨斜坡移除而刪除，改為上列的三項檢查：生成位置、不進入下層、路徑二的轉彎點。

尚未驗證的項目：

| 項目 | 結果 |
| --- | --- |
| 在使用者開啟中的 Unity 編輯器內以正常速度遊玩 | **尚未驗證** |
| 閘門血條、攻擊彈體在正常速度下的觀感 | **尚未驗證**（測試只檢查狀態與位置，沒有截圖） |
| `VerticalSlice` 完整遊玩 | 不再適用：該場景已於 2026-11-26 刪除。 |

---

## 第一關塔攻擊、升級、地面路標、玩家與小地圖

- 紀錄日期：2026-11-26
- 需求來源：`Ref/開發規劃4.docx`。以下四點在實作前向使用者確認：
  - 企劃射程 8 在目前地圖打不到任何路線（三座塔離路線約 21～23），改為**射程乘上可調倍率，暫定 ×3**，塔位不動。
  - 「等級 1（max 5）」指**砲塔、塔身、塔底三個部位各自 1～5 級**，各有 4 次升級。
  - 玩家**靠近塔時**，在塔旁顯示 3D 升級提示，按 1／2／3 升級該塔。（2026-11-27 起改為「靠近且視角指向塔」，見「升級提示改為視角指向判定與塔外框」一節。）
  - 主畫面改為**移植舊場景的跟隨攝影機**，原本的全圖俯視改成**右上角小地圖**。
- 狀態：

| 項目 | 狀態 |
| --- | --- |
| 地面路標（紅色半透明箭頭串；2026-11-26 加上 Bloom 光暈與流動動畫） | **已完成** |
| 塔：自動瞄準、發射鉚釘、砲口火焰 | **已完成** |
| 塔：三部位升級與能量扣除 | **已完成** |
| 塔血量 | **部分完成**：有血量與血條，塔身升級會增加血量；但目前沒有敵人會攻擊塔 |
| 消滅敵人獲得能量 | **已完成**（Level_1 只有 Level 0 敵人，實際只用到 100） |
| 出生點灰色煙霧 | **已完成** |
| 玩家（移動、跳躍、攻擊、出生與重生位置） | **已完成**；實際鍵盤滑鼠操作**尚未驗證** |
| 跟隨攝影機、右上角小地圖、HUD | **已完成**；實際操作手感**尚未驗證** |

### 功能目的

讓第一關從「只能看著閘門被打破」變成可以遊玩的防守流程：
- 玩家在敵人出現前，就能從地面箭頭看到下一波會走哪些路線。
- 三座固定塔會自動攻擊射程內的敵人。
- 玩家可以親自攻擊敵人、用擊殺獲得的能量升級塔。

### 使用技術

- Unity 2022.3.62f3、URP 14.0.12；**沒有新增任何外部套件**。
- 數值：`ScriptableObject`（`FixedTowerProfile`），升級表為陣列，第 n 次升級取第 n 個值。
- 網格：
  - 箭頭：程式產生的 `Mesh`（每個箭頭 6 個頂點、4 個三角形）。
  - 鉚釘：程式產生的圓錐 `Mesh`（Unity 沒有內建圓錐）。
- 特效：內建 `ParticleSystem`（砲口火焰、出生點煙霧），材質使用 URP `Particles/Unlit` 加上程式產生的柔邊圓形貼圖。
- 文字：3D 升級提示使用內建 `TextMesh` 與 `LegacyRuntime.ttf`（沒有使用 TextMeshPro）。
- HUD 與小地圖標記：IMGUI（`OnGUI`），原型用。
- 小地圖：同一個場景的第二台攝影機，用 `Camera.rect`（Viewport Rect）縮到右上角；沒有使用 RenderTexture。
- 物件池：鉚釘沿用專案中 `CombatProjectile` 的 `Stack` 物件池做法。

### 實作方式

**新增腳本**（皆在 `CyberWizard.Runtime` 組件內）

| 檔案 | 責任 |
| --- | --- |
| `Scripts/Gameplay/Towers/FixedTowerProfile.cs` | 塔的初始數值、射程倍率、三部位升級表與花費；計算各等級的射程、血量、攻擊、攻速與升級花費。另含 `TowerPart` 列舉（砲塔、塔身、塔底）。 |
| `Scripts/Gameplay/Towers/FixedTowerCombat.cs` | 掛在 `Tower_05/07/09`：三部位等級、血量（`Vitality`）、血條、射程圈（2026-11-26 改為 `GroundRangeIndicator`，見「攻擊範圍改為 Raycast 投影網格」一節）；每幀從 `WaveDirector.AliveEnemies` 找射程內最近的敵人，轉動砲塔，夾角小於門檻時發射鉚釘並播放砲口火焰。`ApplyUpgrade` 只套用升級，不處理能量。 |
| `Scripts/Gameplay/Towers/TowerUpgradeInteractor.cs` | 找出玩家附近（水平距離 12 以內）最近的塔（2026-11-27 改為：視角 Raycast 指向、且水平距離 12 以內的塔）；按 1／2／3 時先檢查是否滿級、再從 `ResourceWallet` 扣能量，成功後呼叫 `ApplyUpgrade`。每次嘗試都發出 `UpgradeAttempted` 事件。 |
| `Scripts/Gameplay/Combat/RivetProjectile.cs` | 鉚釘：圓錐尖端朝飛行方向，追蹤目標直到命中造成傷害；目標中途消失時沿原方向飛到 4 秒後回收。 |
| `Scripts/Presentation/ConeMesh.cs` | 產生圓錐網格（尖端朝 +Z），整個執行期間共用一份。 |
| `Scripts/Presentation/ChevronStrip.cs` | 純計算：沿折線每隔固定距離放一個「>」箭頭，高度沿折線內插。 |
| `Scripts/Presentation/WavePathPreview.cs` | 依 `WaveDirector` 的下一波資料顯示／隱藏各路線的箭頭；同一條路線只建立一次網格。 |
| `Scripts/Presentation/TowerUpgradePrompt.cs` | 3D 升級提示面板：顯示塔號、血量、能量，以及三部位的目前等級、升級後數值與花費；同時顯示該塔射程圈（2026-11-26 起射程圈改為持續顯示，不再由提示面板控制）。內容只在狀態改變時重建。 |
| `Scripts/Presentation/MinimapView.cs` | 把俯視攝影機設成右上角正方形（邊長為畫面高度的 32%），畫外框與玩家（青色）、敵人（紅色）標記點。 |
| `Scripts/Presentation/GateDefenseHud.cs` | 原型 HUD：準星、能量、玩家與閘門血量、波次與下一波倒數、可升級的塔、操作說明、勝敗訊息（2026-11-27 起勝敗訊息移到 `LevelResultPanel`，HUD 只保留關卡無法開始的錯誤）。只讀取狀態。 |

**修改既有檔案**

| 檔案 | 修改前 → 修改後 |
| --- | --- |
| `Gameplay/Level/FixedTower.cs` | 砲管直接掛在塔底下 → 新增可選的 `turretPivot`、`muzzle`。設定後砲管與砲口改放在樞紐底下，可整組轉向；沒設定時行為不變。 |
| `Gameplay/Waves/WaveDirector.cs` | 新增 `TryGetUpcomingWave`（下一個尚未開始的波次）；`StopSchedule` 後回傳 false。 |
| `Gameplay/Enemies/EnemySpawner.cs` | 新增 `TryGetRoutePoints`（出生點＋路線所有路徑點），供箭頭使用。 |
| `Gameplay/GameFlow/GateDefenseFlow.cs` | 新增可選欄位 `player`、`cameraFollow`：波次開始時啟用滑鼠視角（鎖定游標）；勝敗時停用玩家操作並釋放游標。（2026-11-26（晚） 改為勝敗時放慢時間、不停用操作，`player` 欄位移除。） |
| `Data/Enemies/EnemyLevel0/1/2.asset`、`EnemyBoss.asset` | 擊殺獎勵 150／200／300／500 → **100／150／250／400**。 |
| `Editor/VerticalSliceSceneBuilder.cs` | 同步修改上面四個獎勵值，避免重建 VerticalSlice 時被改回。（此檔已於 2026-11-26 刪除。） |
| `Tests/PlayMode/Level1GateDefensePlayModeTests.cs` | 一開始先停用三座塔，維持驗證「無人防守時閘門被打破」。 |

**新增資產**

| 資產 | 內容 |
| --- | --- |
| `Data/Towers/FixedTowerProfile.asset` | 三座塔共用的數值表。 |
| `Materials/Level1Fx/Fx_PathArrow.mat` | URP Unlit 半透明，顏色 (1, 0.08, 0.05)，alpha 0.6。2026-11-26 曾改為 HDR 顏色 (3, 0.24, 0.15)；之後使用者在 Inspector 調為 (0.749, 0.060, 0.037)、alpha 0.5，亮度改由 `WavePathPreview.glowIntensity` 控制（見「敵方路線發光」一節的修改紀錄）。 |
| `Materials/Level1Fx/Fx_RivetMetal.mat` | URP Lit，白色，Metallic 0.35、Smoothness 0.85。 |
| `Materials/Level1Fx/Fx_MuzzleFlash.mat` | URP Particles/Unlit，Additive。 |
| `Materials/Level1Fx/Fx_Smoke.mat` | URP Particles/Unlit，Alpha 混合。 |
| `Materials/Level1Fx/Fx_PromptBackground.mat` | URP Unlit 半透明深色（升級提示背景），Render Queue 2990，畫在文字之前。 |
| `Textures/Fx/Fx_SoftCircle.png` | 128×128 柔邊白色圓形，供粒子使用。 |

**場景變更（`Level_1.unity`，111 → 124 個 GameObject）**

```text
Main Camera            # 新增：Camera（透視，FOV 60）+ PrototypeCameraFollow + AudioListener
Minimap Camera         # 原本的 Main Camera 改名：TopDownMapCamera + MinimapView，depth 10，移除 AudioListener
Player                 # 新增：Capsule + CharacterController + PrototypePlayerController
Level1_Map
├─ Towers/Tower_0X
│  ├─ Turret           # 新增：砲塔樞紐（高度 = 砲管高度）
│  │  ├─ Barrel        # 原本的砲管，移到樞紐底下
│  │  └─ Muzzle/MuzzleFlash   # 新增：發射點與砲口火焰粒子
│  └─ （元件）FixedTowerCombat  # 新增
└─ EnemyRoutes/Spawn_SouthWest/SpawnSmoke   # 新增：灰色煙霧粒子
Level1_Systems         # 新增元件：ResourceWallet、RewardService、PrototypePlayerAttack、
   │                   #   TowerUpgradeInteractor、WavePathPreview、GateDefenseHud
   └─ TowerUpgradePrompt   # 新增
```

- `EnemySpawner.player` 指向玩家：敵人在攻擊距離內會攻擊玩家（既有邏輯）。
- 場景修改用一次性的 Editor 腳本以批次模式完成，執行後已刪除，沒有留在專案中。後續修改請直接編輯場景。

**資料流**

1. 地面路標：`WavePathPreview` 每幀檢查 `WaveDirector.TryGetUpcomingWave`。
   - 有下一波、且目前沒有梯隊正在生成（`PendingSpawnCount == 0`）時，顯示該波每個梯隊的路線箭頭。
   - 該波開始生成時隱藏。排程中止（閘門被打破）後全部隱藏。
   - 結果：第一波的箭頭在關卡一開始（第一波開始前 5 秒）就出現；第一波 4 隻全部生成後（約第 8 秒）換成第二波的箭頭，一直顯示到第 45 秒第二波開始。
2. 塔攻擊：`FixedTowerCombat` 依水平距離在射程內挑最近的敵人，目標離開射程或死亡才換目標。
   - 砲塔以每秒 240° 轉向目標；俯仰限制在 ±35°。
   - 砲管與目標方向夾角小於 8° 且冷卻結束時，發射鉚釘並播放砲口火焰。
   - 冷卻 = 1 ÷ 攻速。
3. 擊殺獎勵：敵人死亡 → `WaveDirector.EnemyDefeated` → `RewardService` 把 `EnemyDefinition.reward` 加進 `ResourceWallet`。
4. 升級：`TowerUpgradeInteractor` → `ResourceWallet.TrySpend` → `FixedTowerCombat.ApplyUpgrade`。
   - 塔身升級：同時增加血量上限與目前血量。
   - 塔底升級：同步更新射程圈。

### 數值（企劃值；標「暫定」者為企劃未指定、由實作暫定）

**塔（`FixedTowerProfile.asset`）**

| 項目 | 值 |
| --- | --- |
| 初始 | 血量 200、射程 8、攻擊 10、攻速 1 發／秒；各部位等級 1，最高 5 |
| 射程倍率 | **3（暫定）**，企劃射程與塔底加成都乘上倍率：實際射程 24／27／30.3／34.2／39 |
| 塔底（射程） | +1／+1.1／+1.3／+1.6，花費 40／80／130／200 |
| 塔身（血量） | +20／+30／+60／+90，花費 40／80／130／200 |
| 砲塔（攻擊、攻速） | 攻擊 +4／+6／+8／+12、攻速 +0.2／+0.2／+0.4／+0.8，花費 40／80／130／200 |
| 鉚釘速度 | 30（暫定） |
| 鉚釘大小 | 直徑 0.45、長 1.4（暫定，`FixedTowerCombat.rivetScale`） |
| 轉向速度／開火夾角／最大俯仰 | 240°/s、8°、35°（暫定） |

**玩家**

| 項目 | 值 |
| --- | --- |
| 移動速度 | 2（企劃） |
| 血量 | 100（企劃） |
| 攻擊 | 10（企劃） |
| 攻速 | 1.5 發／秒（企劃），即間隔 0.667 秒 |
| 攻擊距離 | 3（**暫定**，沿用舊場景，企劃未指定） |
| 跳躍高度 | 2（**暫定**，沿用舊場景） |
| 重生等待 | 3 秒（**暫定**，沿用舊場景） |
| 出生／重生位置 | (19, 4.70, 103)：閘門前道路東側，面向南；避開敵人路線約 5 單位 |

**其他**

| 項目 | 值 |
| --- | --- |
| 擊殺能量 | Level 0／1／2／Boss = 100／150／250／400 |
| 初始能量 | 0（**暫定**） |
| 升級互動距離 | 12（**暫定**，玩家與塔中心的水平距離） |
| 箭頭外形 | 間距 3.5、寬 3.2、長 1.6、線條粗 0.7、離路面 0.12（暫定） |
| 出生點煙霧 | 半徑 9 的圓形範圍、每秒 22 個粒子、大小 5～10、壽命 4～7 秒、預熱（暫定）。2026-11-26（晚） 改為半徑 18、每秒 44 個、大小 10～20，見「出生點灰霧放大」一節 |
| 砲口火焰 | 一次 16 個粒子、壽命 0.06～0.14 秒、錐角 20°（暫定） |

### 遊戲呈現

- **畫面**：第三人稱視角跟在玩家（藍色膠囊）後方，滑鼠轉動視角；畫面中央有準星。
  - 右上角是全圖俯視小地圖：青色點是玩家，紅色點是敵人。
  - 左上角 HUD：能量、玩家血量、閘門血量、波次與下一波倒數、操作說明。
- **操作**：
  - WASD 移動（依攝影機方向）、空白鍵跳躍。
  - 滑鼠左鍵攻擊準星對準、且距離 3 以內的敵人（2026-11-26 放大為 4.5，之後再提升 1 倍為 9）。
  - 靠近塔時按 1 升級砲塔、2 升級塔身、3 升級塔底。（2026-11-27 起還需準星指向該塔。）
- **地面路標**：關卡開始時，兩條路線上鋪滿紅色半透明「>」箭頭，指向敵人前進方向，一路延伸到閘門前；小地圖上也看得到。
- **出生點**：地圖左下角持續冒出大片灰色煙霧，敵人從煙霧中出現。
- **塔**：敵人進入射程後，砲塔轉向敵人，發射淺灰色金屬圓錐（鉚釘），砲口有短暫的黃橘色火焰。塔頂有藍色血條。
- **升級提示**：玩家走到塔附近時（2026-11-27 起還需準星指向該塔，被指向的塔同時顯示外框）：
  - 塔的左側或右側（選靠近畫面中央的一側）出現深色半透明面板，列出三個部位的等級、升級後數值與花費。
  - 能量不足的花費顯示為紅色，足夠時為黃色；按鍵失敗時該行會短暫顯示「能量不足」或「已滿級」。
  - 地面同時顯示該塔的射程圈，HUD 也會顯示「可升級：N號塔」。（2026-11-26 起射程圈為貼地環帶，射程內有敵人時為紅色、沒有時為白色；之後又改為不論玩家遠近都持續顯示。）
- **結束**：閘門被打破或所有波次清除時，畫面中央顯示失敗或勝利訊息，玩家停止操作、滑鼠游標釋放。（2026-11-26（晚） 改為：遊戲時間放慢為 0.25 倍，玩家仍可移動、攻擊與轉動視角，游標維持鎖定。）

### 限制與待改善事項

- **防守缺口**：射程 ×3 之後，閘門前約 20 單位的道路（Y 字路口到閘門）仍不在任何塔的射程內。
  - 玩家不操作時，第 98.1 秒閘門被打破；停用三座塔時是第 94.1 秒。
  - 塔目前只能削弱敵人，閘門前必須靠玩家防守。是否調整倍率或塔位，需要依實際遊玩決定。
- **敵人不會攻擊塔**：塔的血量、塔身升級目前沒有實際作用。塔被摧毀時只會停止攻擊，沒有外觀變化。
- **獎勵值影響 VerticalSlice**：四個敵人資產與 VerticalSlice 共用，獎勵下調會影響 VerticalSlice 的經濟。（VerticalSlice 已於 2026-11-26 刪除，此項不再適用。）
  - 「100／150／250／400 按等級增加」對應到 Level 0／1／2／Boss 是實作時的推定。
- **玩家攻擊距離 3** 沿用舊場景，企劃未指定；配合移動速度 2（比敵人的 3 慢），近戰距離可能偏短。
- **鉚釘材質**：場景沒有烘焙光照或反射探針，環境反射是黑色，Metallic 0.9 時鉚釘幾乎全黑。
  - 目前降到 0.35，主要靠平行光的高光呈現金屬感。
  - 若之後烘焙光照（產生環境反射），可以把 Metallic 調高。
- **箭頭**：
  - 兩條路線的起點（出生點到第一個路徑點）完全重疊，那一小段的箭頭顏色較深。
  - ~~箭頭平放，沒有跟著 2° 坡度傾斜~~（2026-11-28 已處理，見「待辦清理：測試修正、視覺與流程改善」）。仍靠高出路面 0.12 避免閃爍。
- **煙霧**：出生點在地圖角落，半徑 9 的煙霧有一部分飄到外牆外的下層上方。（2026-11-26（晚） 半徑改為 18，飄出牆外的部分更多。）
- **3D 提示**：
  - ~~面板會被建築或塔擋住（TextMesh 有深度測試）~~（2026-11-28 已處理，見「待辦清理：測試修正、視覺與流程改善」）。
  - 塔在攝影機背後時看不到面板，只能看 HUD 的提示文字。
  - 中文字形依賴作業系統字型。
- **既有地圖標籤**：灰盒階段為俯視設計的平放文字（「5號塔」、「防區 5」等），在第三人稱視角下會呈現傾斜或倒轉。
- **HUD 與小地圖**：使用 IMGUI，每幀產生少量字串與列舉器配置；正式 UI 需改用 UI Toolkit 或 uGUI。沒有暫停與重新開始。
- **游標**：波次進行中游標被鎖定；在編輯器中按 Esc 可釋放。沒有暫停選單。2026-11-26（晚） 起關卡結束後游標也維持鎖定（不再釋放）。
- **平台側壁**：玩家能否從各方向走到每座塔 12 以內，**尚未驗證**。平台側壁沒有階梯，有些方向可能需要跳上去。
- **建置**：`Level_1` 仍未加入 Build Settings。（2026-11-26 已加入，並且是 Build Settings 中唯一的場景。）

### 驗證方式

Unity 編輯器當時沒有開啟，所以直接在專案上以 Unity 2022.3.62f3 批次模式執行。

| 項目 | 結果 |
| --- | --- |
| 腳本編譯 | 通過，沒有錯誤或警告。 |
| 場景組裝 | 三座塔的樞紐、砲口、粒子與元件參照皆設定完成；玩家落點由射線打到 `Ground_Upper`（y = 3.60）。場景 124 個 GameObject。 |
| EditMode 測試 | **24 項全數通過**（新增 4 項）。<br>新增項目：數值表與企劃一致（各等級累計值、花費、滿級）、箭頭網格（數量、朝上、方向與高度內插）、退化路徑、圓錐網格（尖端方向、法線朝外）。 |
| PlayMode 測試 | **7 項全數通過**（新增 5 項），詳見下方清單。 |
| 截圖檢查 | 用暫時的截圖測試（已刪除）以 `Camera.Render` 輸出 PNG 並目視確認，詳見下方清單。IMGUI 的 HUD 與小地圖標記點不會出現在 `Camera.Render` 截圖中，**沒有截圖確認**。 |

新增的 PlayMode 測試（`Level1CombatPlayModeTests.cs`）：
- **路標**：
  - 開場顯示 `wave_01` 的兩條路線，材質 alpha 0.6。
  - 第一波生成中隱藏；生成完畢後顯示 `wave_02` 的兩條路線。
  - 第二波開始後全部隱藏。
- **塔**：
  - 三座塔初始數值正確，射程 24。
  - 三座塔都鎖定過敵人、砲塔轉向目標（夾角 < 10°）、播放砲口火焰，並出現鉚釘。
  - 擊殺 ≥ 3 隻，能量 = 擊殺數 × 100。
  - 測試中把閘門血量調高，讓路徑二的敵人能走到 7、9 號塔附近。
- **升級**：
  - 閘門前不在任何塔的範圍內；移到 5 號塔旁時提示面板與射程圈出現。
  - 能量不足時升級失敗。
  - 砲塔連升 4 級後攻擊 40、攻速 2.6，第 5 次回報已滿級。
  - 塔身升級後血量 220／220；塔底升級後射程 27；每次扣款金額正確。
  - 離開後提示與射程圈消失。（2026-11-26 起射程圈持續顯示，測試改為檢查離開後射程圈仍在。）
- **玩家**：
  - 出生點在閘門前 15 以內，血量 100，站在地面上。
  - 受到 100 傷害死亡後，在出生點重生並回滿血。
- **場景**：
  - 主攝影機為跟隨攝影機（透視）。
  - 小地圖攝影機在右上角、為正方形、depth 高於主攝影機。
  - 煙霧粒子播放中，一開始就有超過 20 個粒子。

截圖確認了以下畫面：
- 開場第三人稱畫面與右上角小地圖。
- 全圖俯視：箭頭沿兩條路線到閘門。
- 出生點煙霧與箭頭特寫。
- 塔開火與砲口火焰特寫。
- 鉚釘飛行特寫：Metallic 0.9 時幾乎全黑，調整後為淺灰金屬。
- 升級提示：
  - 第一版字太大，又擋在塔前方，背景也沒有對齊文字。
  - 修正後面板放在塔側邊，背景延後一幀再依文字大小調整，字體大小改為 0.07。

第一次執行時，塔攻擊測試失敗：只有 1 座塔鎖定過敵人。
- 原因：路徑二的敵人約第 110 秒才會經過 7、9 號塔，但閘門在第 98 秒就被路徑一的敵人打破，之後所有敵人停住。
- 這是測試假設錯誤，不是程式錯誤。
- 修正：測試中把閘門血量調高。重跑後通過。

尚未驗證的項目：

| 項目 | 結果 |
| --- | --- |
| 實際鍵盤滑鼠操作：WASD、空白鍵、滑鼠視角與左鍵攻擊、1／2／3 升級 | **尚未驗證**（批次模式沒有輸入；升級由測試直接呼叫 `TryUpgrade`） |
| 正常速度下的遊玩手感與難度 | **尚未驗證** |
| HUD、小地圖標記點、準星的實際顯示 | **尚未驗證** |
| 在 Inspector 調整新參數（例如射程倍率）後的即時效果 | **尚未驗證** |
| `VerticalSlice` 受獎勵調整影響後的遊玩 | 不再適用：該場景已於 2026-11-26 刪除。 |
| 效能（大量鉚釘、粒子） | **尚未驗證** |

---

## 移除舊版場景與已棄用程式碼

- 紀錄日期：2026-11-26
- 狀態：**已完成**
- 需求來源：使用者要求刪除舊版場景，並移除相關的已棄用程式碼。刪除範圍經使用者確認為「完整清除」：除了只有舊場景使用的腳本之外，Level_1 沒有使用的玩法系統（玩家建塔、中立基地、連攜、可建造塔）也一併移除。

### 功能目的

專案已改以 `Level_1` 為主。舊的 `VerticalSlice` 原型場景、URP 範本場景，以及只服務它們的系統仍留在專案中，會增加維護成本，也讓 Level_1 的共用腳本帶著用不到的分支。

### 使用技術

- 先依型別名稱建立腳本間的引用關係，再比對場景檔中的腳本 GUID，找出 Level_1 實際用得到的腳本。
- Level_1 仍在使用、但引用了舊系統的腳本，只刪除與舊系統相關的欄位和分支。
- **沒有新增任何外部套件。**

### 實作方式

**刪除的場景與範本殘留**

- `Assets/Scenes/SampleScene.unity`（URP 範本場景）、`Assets/_Project/Scenes/VerticalSlice.unity`。
- `Assets/TutorialInfo/`、`Assets/Readme.asset`（URP 範本說明）。

**刪除的程式碼**

| 類別 | 檔案 |
| --- | --- |
| 只有 VerticalSlice 使用 | `PrototypeGameFlowController`、`PrototypeMvpSmokeDriver`、`PrototypeHudController`、`PrototypeAudioService`、`Editor/VerticalSliceSceneBuilder`（`Editor` 資料夾因此清空，一併移除） |
| 玩家建塔 | `Building/`：`PrototypeBuildController`、`TowerFactory`、`TowerPlacementValidator` |
| 可建造塔 | `Towers/`：`TowerController`、`TowerDefinition`、`TowerProjectile`、`TowerRegistry`、`ElementType` |
| 中立基地與士兵 | `Neutral/`：`NeutralOutpost`、`NeutralOutpostModule`、`SoldierAgent`、`SoldierRegistry`；`Capture/CaptureZone` |
| 連攜 | `Combo/PrototypeComboSystem` |
| 敵人意圖 | `Enemies/EnemyIntentSelector`（`EnemyIntent` 列舉一併移除） |
| 測試 | `Tests/PlayMode/VerticalSlicePlayModeTests.cs`；`PrototypeCoreEditModeTests.cs` 中測試上述系統的 6 項（level_02 資料、意圖機率、意圖選擇、塔定義、兩項放置規則） |

**刪除的資料與資產**

- `StreamingAssets/Levels/level_01.json`、`level_02.json`（舊場景的波次資料）。Level_1 使用 `level1_rust_alley.json`，予以保留。
- `Data/Towers/` 中的八個可建造塔定義（四元素 × 單體／範圍）。`FixedTowerProfile.asset` 予以保留。
- `UI/`：`PrototypeHud.uxml`、`PrototypeHud.uss`、`PrototypePanelSettings.asset`（舊 HUD；Level_1 使用 IMGUI 的 `GateDefenseHud`）。
- `Resources/PrototypePalette/` 中的 `Road`、`Neutral` 與四個元素材質。`Map`（血條底色）、`Player`、`Enemy` 予以保留。

**修改的腳本（Level_1 行為不變）**

| 檔案 | 修改 |
| --- | --- |
| `Scripts/Gameplay/Enemies/EnemyAgent.cs` | 移除意圖選擇、佔領中立基地、攻擊塔的流程，以及 `TowerRegistry`、`NeutralOutpostModule` 參數。敵人一律沿路線前進，並攻擊玩家與閘門。Level_1 原本就沒有塔登錄與中立基地，舊流程從來不會被選到。 |
| `Scripts/Gameplay/Enemies/EnemySpawner.cs` | 移除 `towerRegistry`、`neutralModule` 欄位與 `SetNeutralModule()`，`Configure()` 不再接受塔登錄參數。（2026-11-27 `Configure()` 已整個移除。） |
| `Scripts/Gameplay/Enemies/BossAbilityController.cs` | 移除對可建造塔的範圍傷害，只保留對玩家的脈衝傷害。 |
| `Scripts/Gameplay/Enemies/EnemyBehaviorSettings.cs` | 只保留 `attackDistance`；移除佔領機率、攻擊塔機率與佔領時間。 |
| `Scripts/Gameplay/Player/PrototypePlayerAttack.cs` | 移除建造模式、連攜倍率的引用，以及瞄準敵方士兵的分支；`Configure()` 只剩玩家與攝影機兩個參數。（2026-11-27 `Configure()` 已整個移除。） |
| `Scripts/Presentation/PrototypeMaterialLibrary.cs` | `PrototypeMaterialRole` 只保留 `Map`、`Player`、`Enemy`，並移除 `GetElementRole()`。 |

**保留但 Level_1 目前沒有使用的資料**

- `Data/Enemies/` 中的 `EnemyLevel1`、`EnemyLevel2`、`EnemyBoss`、`EnemyBehaviorSettings`：屬於敵人資料，之後的關卡可能會用到。Boss 技能（`BossAbilityController`）也一併保留。
- `Assets/UI Toolkit/`（UI Toolkit 預設主題）：舊 HUD 刪除後已沒有面板使用；它屬於套件設定，因此保留。

### 遊戲呈現

Level_1 中玩家看得到的內容沒有改變。專案現在只剩 `Level_1` 一個場景。

### 限制與待改善事項

- 被刪除的玩法（建塔、中立基地、連攜、八種元素塔、舊 HUD）只能從 git 歷史找回。本次沒有 commit，這些檔案中尚未 commit 的修改不在 git 裡，只保留在本次作業的暫存備份中。
- `Ref/後續開發指南.md` 與 `Ref/開發規劃*.md` 仍描述 VerticalSlice 與 Builder 流程。這些是企劃文件，本次沒有修改。
- 程式碼中沒有任何地方引用 `Scripts/Gameplay/Resources/RewardService.cs`，但 Level_1 場景有掛載它，因此保留。

### 驗證方式

驗證結果見「敵方路線發光、流動動畫與環境光調暗」一節的驗證表；本節與下兩節的修改在同一批次中一起驗證。另外以 grep 確認，專案中已沒有任何程式碼引用被刪除的型別。

---

## 專案資料夾結構調整

- 紀錄日期：2026-11-26
- 狀態：**已完成**

### 功能目的

移除多出的一層 `Assets/_Project/`，讓專案資料夾直接位於 `Assets/` 底下。原本外層的 `Assets/Scenes/`（範本場景）已刪除，改由專案的場景資料夾取代。

### 使用技術

在檔案系統中搬移資料夾，並連同 `.meta` 一起搬移，讓 GUID 保持不變，場景、材質與 ScriptableObject 之間的引用不受影響。搬移時 Unity 編輯器已關閉。**沒有新增任何外部套件。**

### 實作方式

目前的 `Assets/` 結構：

```text
Assets/
├─ Data/                 # EnemyDefinition、FixedTowerProfile 等 ScriptableObject
├─ Materials/            # Level1Blockout、Level1Fx
├─ Resources/            # PrototypePalette（Map、Player、Enemy）
├─ Scenes/               # Level_1.unity
├─ Scripts/              # CyberWizard.Runtime（asmdef）
├─ Settings/             # URP 設定、Level1PostProcess（Bloom Volume Profile）
├─ StreamingAssets/      # Levels/level1_rust_alley.json
├─ Tests/                # EditMode、PlayMode（各自的 asmdef）
├─ Textures/
├─ UI Toolkit/           # UI Toolkit 預設主題（套件設定）
├─ README.md             # 原 _Project/README.md，內容已改寫為 Level_1 現況
└─ UniversalRenderPipelineGlobalSettings.asset
```

隨搬移一併修改的設定與程式碼：

| 檔案 | 修改 |
| --- | --- |
| `ProjectSettings/EditorBuildSettings.asset` | 移除 `VerticalSlice` 與 `SampleScene`，加入並啟用 `Assets/Scenes/Level_1.unity`。 |
| `Tests/PlayMode/Level1CombatPlayModeTests.cs`、`Level1GateDefensePlayModeTests.cs` | 場景路徑改為 `Assets/Scenes/Level_1.unity`。 |
| `Assets/README.md` | 改寫為 Level_1 的執行方式、操作方式與資料位置。 |

組件定義（asmdef）以所在資料夾為範圍，與完整路徑無關，因此不需要修改。

### 遊戲呈現

玩家看得到的內容沒有改變；Unity 的 Project 視窗中少了一層資料夾。

### 限制與待改善事項

- ~~`Ref/` 中的企劃文件仍使用 `Assets/_Project/...` 路徑。~~（2026-11-28 已處理，見「待辦清理：測試修正、視覺與流程改善」）：`Ref/後續開發指南.md` 已更新；其他 `Ref/*.md` 沒有這個路徑，`.docx` 未檢查。
- 搬移後尚未在使用者的 Unity 編輯器中開啟，確認 Project 視窗與 Inspector 中的引用（**尚未驗證**）。批次模式下的匯入、編譯與測試都正常。

### 驗證方式

見下一節的驗證表。

---

## 敵方路線發光、流動動畫與環境光調暗

- 紀錄日期：2026-11-26
- 狀態：**已完成**；在使用者編輯器中的實際觀感**尚未驗證**。

### 功能目的

讓地面上的敵方路線箭頭更醒目：加上 3D 光暈（Bloom），並讓箭頭沿前進方向循環流動，直接顯示敵人的行進方向。同時調暗整體環境光，襯托發光的箭頭。

### 使用技術

- URP 14 後製：全域 `Volume` 加上 `VolumeProfile` 中的 `Bloom` 覆寫，並開啟主攝影機 `UniversalAdditionalCameraData.renderPostProcessing`。
- 箭頭材質使用 URP Unlit 的 HDR 顏色（數值大於 1）。只有超過 Bloom 門檻的像素會發光，其他物件不受影響。
- 流動動畫：每幀以 `Time.time × flowSpeed` 計算偏移量，並以箭頭間距為週期循環，重建顯示中路線的網格（使用 `Mesh.MarkDynamic` 並重用 List，不會每幀配置記憶體）。沒有使用自訂 Shader。
- **沒有新增任何外部套件。**

### 實作方式

| 檔案 | 修改 |
| --- | --- |
| `Scripts/Presentation/ChevronStrip.cs` | `Settings` 新增 `Offset`：所有箭頭沿前進方向推進的距離，以 `Spacing` 為週期循環（`Mathf.Repeat`）。預設為 0，與原本的排列相同。 |
| `Scripts/Presentation/WavePathPreview.cs` | 每條路線保存本地座標的路線點與自己的 `Mesh`；`LateUpdate` 依目前偏移量重建顯示中路線的網格。新增 `flowSpeed` 欄位與 `FlowOffset` 屬性。顯示與隱藏的時機不變。 |
| `Materials/Level1Fx/Fx_PathArrow.mat` | `_BaseColor` 由 (1, 0.08, 0.05, 0.6) 改為 HDR (3, 0.24, 0.15, 0.6)。（已被修改紀錄中的做法取代：材質維持一般顏色，HDR 亮度在執行時套用。） |
| `Settings/Level1PostProcess.asset`（新增） | 只含 Bloom 的 Volume Profile：Threshold 1、Intensity 1.5、Scatter 0.7，並開啟 High Quality Filtering。沒有加入 Tonemapping 等其他效果，以免改變整體色調。 |
| `Scenes/Level_1.unity` | 新增 `Global Volume (Bloom)`；`Main Camera` 開啟 Post Processing（小地圖攝影機維持關閉）；調暗環境光與平行光（見下表）。 |
| `Tests/EditMode/Level1CombatEditModeTests.cs` | 新增 1 項測試，驗證偏移量會讓箭頭往前推、超過一個間距後循環，且偏移剛好一個間距時與偏移 0 相同。 |

網格重建的頂點數：每個箭頭 6 個頂點。以路線長約 250 單位、間距 3.5 計算，每條路線約 70 個箭頭（約 430 個頂點）。偏移量變化時，箭頭數量可能增減一個，因此重建前先呼叫 `Mesh.Clear()`，避免三角形索引超出頂點範圍。

### 可調整參數

| 位置 | 參數 | 目前值 | 用途 |
| --- | --- | --- | --- |
| `WavePathPreview`（Level_1 場景） | `flowSpeed` | 3（暫定） | 箭頭流動速度（單位／秒）。設為 0 時箭頭靜止，也不會每幀重建網格。 |
| `Fx_PathArrow.mat` | Base Color | (0.749, 0.060, 0.037)、alpha 0.5（使用者調整） | 箭頭的色相與透明度。URP Unlit 的拾色器不是 HDR，數值會限制在 0～1。 |
| `WavePathPreview`（Level_1 場景） | `glowIntensity` | 4（暫定） | 執行時把 Base Color 的 RGB 乘上此值。結果大於 Bloom 門檻 1 的部分才會發光。 |
| `Level1PostProcess.asset` → Bloom | Threshold／Intensity／Scatter | 1／1.2／0.7（Intensity 由使用者從 1.5 調為 1.2） | 發光門檻、強度與擴散範圍。 |
| Lighting 視窗 → Environment（Flat） | Ambient Color | (0.276, 0.288, 0.312)，修改前 (0.46, 0.48, 0.52) | 環境光，約為原本的 60%（暫定）。 |
| `Directional Light` | Intensity | 0.8，修改前 1.1（暫定） | 平行光強度。 |

### 遊戲呈現

- 下一波的路線箭頭帶有紅色光暈，並以每秒 3 單位的速度持續朝閘門方向流動：新箭頭從出生點端出現，在路線終點消失。
- 整體畫面比之前暗：地面、道路與建築的亮部降低，防區的藍色仍清楚可辨。
- 其他物件（防區外框、砲口火焰等）沒有超過門檻的 HDR 顏色，所以不會發光。

### 限制與待改善事項

- ~~箭頭每走完一個間距，起點和終點會各出現、消失一個箭頭，沒有淡入淡出。~~（2026-11-28 已處理，見「待辦清理：測試修正、視覺與流程改善」）：改為頭尾縮放（不是透明度）。
- Bloom 需要 HDR。目前使用的 High Fidelity 品質等級（Balanced 亦同）有開啟 HDR，但 **Performant 等級的 URP 設定關閉了 HDR**：在該等級下顏色會在 1 被截斷，光暈可能消失或變得很弱（**尚未驗證**）。
- 小地圖攝影機沒有開啟後製，所以小地圖上的箭頭不會發光。
- 調暗環境光後，Level_1 仍然沒有烘焙光照，金屬度高的材質在暗處會更暗。
- 在批次模式下截圖時，攝影機必須輸出到 HDR RenderTexture 才看得到 Bloom；輸出到 ARGB32 時，URP 會使用 LDR 中間緩衝，光暈不會出現。這只影響截圖驗證，不影響遊戲畫面。

### 驗證方式

使用者已關閉 Unity 編輯器，因此三項修改完成後，直接在專案上以 Unity 2022.3.62f3 批次模式執行驗證。

| 項目 | 結果 |
| --- | --- |
| 搬移後重新匯入與編譯 | 通過，沒有錯誤或警告（`warning CS` 0 筆）。 |
| EditMode 測試 | **19 項全數通過**。原本 24 項，移除 6 項已棄用系統的測試，新增 1 項箭頭偏移測試。 |
| PlayMode 測試 | **6 項中 4 項通過，2 項失敗**。原本 7 項，移除了 `VerticalSlicePlayModeTests`。失敗的兩項是 `Towers_AimFireRivetsKillEnemiesAndGrantEnergy`（預期攻擊 10，實際 20）與 `Upgrade_PlayerNearTowerUpgradesEachPartWithEnergy`（預期 14，實際 24），原因是 `Data/Towers/FixedTowerProfile.asset` 的 `attackDamage` 已經是 20，但測試仍寫死企劃值 10。本次作業前的備份中，這個值就已經是 20，與本次修改無關；測試與數值都沒有修改，等待使用者決定。 |
| 截圖檢查 | 以暫時的截圖測試（已刪除）比較修改前後：相隔 0.5 秒的兩張截圖中，箭頭位置確實往前移動；改用 HDR RenderTexture 截圖後，箭頭有明顯的紅色光暈；地面與道路比修改前暗；俯視角度下箭頭形狀仍然清楚。 |
| 在使用者編輯器中的 Scene／Game 視圖觀感、在 Inspector 調整參數的即時效果 | **尚未驗證** |
| 實際遊玩時，流動速度與光暈強度是否合適 | **尚未驗證**（目前皆為暫定值） |

### 修改紀錄

**2026-11-26　修正「地面路徑不發光」**

- 現象：使用者回報地面路徑箭頭不再發光。
- 原因：`Fx_PathArrow.mat` 的 Base Color 被改成 (0.749, 0.060, 0.037, 0.5)，Bloom 強度也由 1.5 改為 1.2，研判是在 Inspector 調整。URP Unlit 的 Base Color 拾色器不是 HDR，在 Inspector 調整後數值會被限制在 0～1，因此低於 Bloom 門檻 1，箭頭不再發光。Bloom 強度的調整不是原因。
- 修改前：亮度直接寫在材質顏色裡（HDR 3.0），只要在 Inspector 調整顏色就會失效。
- 修改後：材質只負責色相與透明度；`WavePathPreview` 新增 `glowIntensity`（暫定 4），建立箭頭時以 `MaterialPropertyBlock` 套用「材質顏色 RGB × glowIntensity、透明度不變」的 HDR 顏色，並新增 `GlowColor` 屬性。使用者調整的顏色、透明度與 Bloom 強度都保留不變。
- 場景沒有修改：`glowIntensity` 使用程式預設值 4，可以在 `WavePathPreview` 的 Inspector 調整。
- 測試：`PathArrows_ShowUpcomingWaveRoutesUntilThatWaveSpawns` 原本寫死「材質 alpha 0.6」，改為檢查 PropertyBlock 中的顏色等於 `GlowColor`、紅色分量大於 1，且透明度與材質相同。
- 限制：`glowIntensity` 只在建立箭頭網格時套用，Play Mode 中修改不會立即生效；在 Play Mode 中修改材質顏色也一樣。小地圖攝影機沒有後製，小地圖上的箭頭不發光，只會因為 HDR 值被截斷而顯得更亮。

驗證（Unity 編輯器開著，因此在專案副本上執行；副本已刪除）：

| 項目 | 結果 |
| --- | --- |
| 編譯 | 沒有錯誤或警告。 |
| EditMode 測試 | **21 項全數通過**。 |
| PlayMode 測試 | 修正後的路標測試**通過**（第一次執行時，`Color` 經過 PropertyBlock 存取後有浮點誤差，改用容許誤差比較後通過）。其餘結果與前一輪相同：7 項中 5 項通過，失敗的 2 項仍是塔攻擊力數值問題，與本次修改無關。 |
| 截圖檢查 | 使用你目前的材質顏色（alpha 0.5）與 Bloom 1.2，實際顏色為 (3.0, 0.24, 0.15, 0.5)，箭頭的紅色光暈恢復。 |
| 在使用者編輯器中進入 Play Mode 的實際畫面 | **尚未驗證** |

---

## 攻擊範圍改為 Raycast 投影網格、玩家射程放大

- 紀錄日期：2026-11-26
- 狀態：**已完成**；在使用者編輯器中的實際觀感**尚未驗證**。
- 需求來源：使用者要求把玩家攻擊範圍放大為原本的 1.5 倍；所有物件的攻擊範圍改用「Raycast 計算範圍圓周投影到地面的點，再用 Mesh 連接」的方式呈現，取樣點暫定 25 個；範圍內沒有敵人時顯示白色，有敵人時顯示紅色。
- 實作時的假設（未另外詢問）：
  - 「所有物件」指目前有顯示攻擊範圍的物件，也就是玩家與三座固定塔。敵人只有近戰距離，沒有範圍顯示，本次沒有新增。
  - 「用 Mesh 連接」做成貼地的環帶（外緣是投影點，往圓心內縮固定寬度），而不是實心圓面，避免遮住防區與路線箭頭。

### 功能目的

原本的射程圈是固定高度的平面圓線，在傾斜地面、平台與道路上會浮空或埋入地面。改為逐點投影到實際碰撞地面後，射程邊界會貼合地形；顏色也直接告訴玩家射程內是否有敵人。

### 使用技術

- `Physics.RaycastNonAlloc`（重用緩衝區、`QueryTriggerInteraction.Ignore`）由取樣點上方往下投射。
- 動態 `Mesh`（`MarkDynamic`）建立環帶：每個取樣點 2 個頂點（外緣、內緣），相鄰點以 2 個三角形連接，最後一點接回第一點。
- 顏色以 `MaterialPropertyBlock` 設定 `_BaseColor`，所有射程圈共用同一個材質。
- **沒有新增任何外部套件。**

### 實作方式

| 檔案 | 修改 |
| --- | --- |
| `Scripts/Presentation/GroundRangeIndicator.cs`（新增） | 射程指示元件。`SampleGround`：沿水平圓周平均取 N 點，從擁有者高度 +20 往下 Raycast（最遠 60），取最近的有效碰撞點；沒打到時使用圓心高度。`BuildRing`：由投影點建立環帶。擁有者位置／旋轉或半徑改變、或重新顯示時才重建，所以靜止的塔只在顯示與升級時計算，玩家移動時每幀 25 條 Raycast。`SetAlert(bool)` 切換白色與紅色。 |
| `Scripts/Presentation/PrototypeRangeCircle.cs`（刪除） | 舊的 `LineRenderer` 平面圓，已沒有任何使用者。 |
| `Scripts/Gameplay/Towers/FixedTowerCombat.cs` | 射程圈改用 `GroundRangeIndicator`（物件名稱仍為 `Attack Range Circle`，寬度 0.4，之後統一為 2，見修改紀錄）。每幀找目標後呼叫 `SetAlert(_target != null)`：目標無效時已重新搜尋整個射程，所以「有目標」就等於「射程內有敵人」。塔被摧毀時恢復白色。 |
| `Scripts/Gameplay/Player/PrototypePlayerAttack.cs` | `attackRange` 預設值由 3 改為 4.5（之後再改為 9，見修改紀錄）；射程圈改用 `GroundRangeIndicator`（寬度 0.15，之後統一為 2，見修改紀錄）。新增 `HasEnemyInRange`：每幀以水平距離檢查 `EnemyAgent.Active` 中是否有存活敵人在射程內。 |
| `Scripts/Gameplay/Enemies/EnemyAgent.cs` | 新增靜態清單 `EnemyAgent.Active`（`OnEnable` 加入、`OnDisable` 移除，進入 Play Mode 時清空），讓玩家不必引用 `WaveDirector` 就能查詢敵人。 |
| `Scripts/Presentation/PrototypeMaterialLibrary.cs` | 新增 `RangeIndicator` 角色。 |
| `Resources/PrototypePalette/RangeIndicator.mat`（新增） | URP Unlit 不透明白色；實際顏色由 `MaterialPropertyBlock` 覆寫。 |
| `Scenes/Level_1.unity` | `Player` 上的 `PrototypePlayerAttack.attackRange` 由 3 改為 4.5（之後再改為 9）。 |
| `Tests/EditMode/Level1CombatEditModeTests.cs` | 新增 2 項：投影會落在方塊頂面與地面、略過可受傷的角色；環帶頂點數、三角形數、高度與內縮距離，以及正面朝上。 |
| `Tests/PlayMode/Level1CombatPlayModeTests.cs` | 新增 1 項：玩家射程（目前為 9）、25 個投影點貼近腳底、開場為白色；三座塔的射程圈半徑等於塔射程；塔鎖定敵人時轉紅；玩家移到敵人旁邊時轉紅。 |

**Raycast 略過的碰撞體**：觸發器、擁有者自己（玩家本身、塔的基座與塔身）、父物件上有 `IDamageable` 的物件（敵人、玩家、塔、閘門核心），以及父物件上有 `RangeIndicatorIgnored` 標記的物件（目前是 `Buildings` 底下的所有房屋，見下方修改紀錄）。其他碰撞體都視為地面，包括牆與閘門柱。

### 可調整參數

`GroundRangeIndicator` 由程式在執行時建立，以下欄位目前只能改程式碼中的預設值（寬度由建立者傳入）；執行中可以在 Inspector 調整。

| 參數 | 目前值 | 用途 |
| --- | --- | --- |
| `sampleCount` | 25（暫定） | 圓周取樣點數，也就是每次重建的 Raycast 數。 |
| `width` | 2，所有射程圈統一（`GroundRangeIndicator.DefaultWidth`；原本塔 0.4、玩家 0.15） | 環帶寬度，由射程邊緣往內延伸。 |
| `lift` | 0.2（暫定） | 環帶高於投影點的距離。道路頂面約比地面高 0.1，而道路沒有碰撞，所以需要高於 0.1。 |
| `rayStartHeight`／`rayLength` | 20／60 | Raycast 起點高度與長度。 |
| `groundMask` | Everything | 視為地面的圖層；目前專案沒有專用的地面圖層。 |
| `idleColor`／`alertColor` | 白 (1, 1, 1)／紅 (1, 0.12, 0.08) | 範圍內沒有／有敵人時的顏色。 |
| `PrototypePlayerAttack.attackRange` | 9（原本 3，× 1.5 為 4.5，再 × 2 為 9） | 玩家攻擊範圍。 |

### 遊戲呈現

- 玩家腳下一直有白色環帶（半徑 9），貼著實際地面；有敵人進入範圍時轉為紅色。
- 三座塔的射程環帶持續顯示（見下方修改紀錄；原本只在玩家靠近時顯示），會沿著平台、道路與地面起伏；塔鎖定敵人時轉為紅色，沒有敵人時為白色。
- 25 個取樣點讓環帶呈現明顯的多邊形折線，而不是平滑的圓。

### 限制與待改善事項

- 相鄰取樣點跨過高低差（例如平台邊緣）時，兩點之間是直線，環帶會穿過平台側壁，有一小段被遮住；取樣點越少越明顯。
- 房屋已略過（見修改紀錄）；投影點打到牆或閘門柱時，環帶仍會爬到物件頂面。若也要略過這些物件，可在其群組根物件掛上 `RangeIndicatorIgnored`。
- 玩家的「射程內有敵人」以水平距離判斷，與實際攻擊條件不同：實際攻擊需要以準星瞄準敵人。
- 環帶材質不是 HDR，紅色射程圈不會產生 Bloom 光暈。
- ~~環帶位在半透明防區填色底下時，顏色會被染上藍色。~~（2026-11-28 已處理，見「待辦清理：測試修正、視覺與流程改善」）：`lift` 由 0.2 改為 0.3。

### 驗證方式

使用者已關閉 Unity 編輯器，因此直接在專案上以 Unity 2022.3.62f3 批次模式執行驗證。

| 項目 | 結果 |
| --- | --- |
| 編譯 | 通過，沒有錯誤或警告。 |
| EditMode 測試 | **21 項全數通過**（新增 2 項）。 |
| PlayMode 測試 | **7 項中 5 項通過**（新增 1 項，通過）。失敗的 2 項與上一節相同（`FixedTowerProfile.asset` 的攻擊力為 20，但測試寫死 10），與本次修改無關。 |
| 截圖檢查 | 以暫時的截圖測試（已刪除）確認：玩家腳下的白色環帶貼地；三座塔射程圈在無敵人時為白色；5 號塔鎖定敵人後轉紅，同時 9 號塔仍為白色；環帶沿平台與道路起伏，並看得到上述平台側壁遮擋與爬上閘門柱的情形。 |
| 實際操作時玩家射程圈跟隨移動的觀感、轉色是否清楚 | **尚未驗證** |

### 修改紀錄

**2026-11-26　塔的射程圈改為持續顯示**

- 需求：只要是有攻擊範圍的物件，範圍都持續顯示（包括塔）。
- 修改前：塔的射程圈只在玩家靠近、升級提示出現時顯示，由 `TowerUpgradePrompt` 開關。修改後：`FixedTowerCombat` 建立射程圈後就保持顯示，只有塔被摧毀時才隱藏；`TowerUpgradePrompt` 不再控制射程圈（移除 `ShowRange`）。
- 範圍：目前有攻擊範圍顯示的物件是玩家與三座塔，玩家的射程圈原本就持續顯示。敵人只有近戰攻擊距離，Boss 有脈衝技能半徑，兩者目前都沒有範圍顯示，也沒有新增。
- 效能：塔是靜止的，射程圈只在建立和升級時投影一次（25 條 Raycast），持續顯示不會增加每幀計算。
- 測試：`Upgrade_PlayerNearTowerUpgradesEachPartWithEnergy` 的「離開後射程圈消失」改為「離開後射程圈仍在」。這項測試目前會因為塔攻擊力數值問題，在更前面就失敗，所以這行檢查實際上沒有執行到。`RangeIndicators_ProjectToGroundAndTurnRedWhenEnemyInRange` 改為檢查開場時三座塔的射程圈已經顯示。

**2026-11-26　玩家攻擊距離再提升 1 倍**

- 修改前 4.5，修改後 9。修改的地方：場景中 `PrototypePlayerAttack.attackRange`、程式預設值、PlayMode 測試的期望值。玩家移到敵人旁的測試距離也由 2 改為 4。
- 瞄準用的 Raycast 長度為 100，不會限制射程 9。

**驗證（以上兩項加上刪除閘門標籤）**

Unity 編輯器開著，所以把 `Assets`、`Packages`、`ProjectSettings` 複製到暫存資料夾，在副本上以批次模式執行。暫時的截圖測試只存在於副本中，副本已刪除。

| 項目 | 結果 |
| --- | --- |
| 編譯 | 沒有錯誤或警告。 |
| EditMode 測試 | **21 項全數通過**。 |
| PlayMode 測試 | **7 項中 5 項通過**（不含副本中的暫時截圖測試）。失敗的 2 項仍是塔攻擊力數值問題（`FixedTowerProfile.asset` 為 20，測試寫死 10），與本次修改無關。 |
| 截圖檢查 | 開場俯視畫面中，三座塔的白色射程圈在玩家不在附近時就已顯示；閘門前已沒有文字標籤；玩家射程圈半徑明顯放大為 9。 |
| 在使用者編輯器中重新載入場景後的狀態、實際遊玩手感 | **尚未驗證** |

**2026-11-28　射程圈寬度統一為 2，投影略過房屋**

- 需求：攻擊範圍線段寬度統一為 2 單位；投影用的 Raycast 忽略房屋。狀態：**已完成**。
- 寬度：
  - 修改前：寬度由建立者傳入，塔 0.4、玩家 0.15。修改後：新增常數 `GroundRangeIndicator.DefaultWidth = 2`，作為 `width` 欄位預設值；`Create` 移除寬度參數，`FixedTowerCombat` 與 `PrototypePlayerAttack` 不再各自指定寬度。新增唯讀屬性 `Width`。
  - 環帶仍由射程邊緣往內延伸，所以射程邊界位置不變：玩家環帶覆蓋半徑 7～9，塔（射程 30）覆蓋 28～30。
- 略過房屋：
  - 新增 `Scripts/Presentation/RangeIndicatorIgnored.cs`：沒有任何欄位的標記元件，表示「此物件與子物件的碰撞體不作為射程圈投影的地面」。碰撞本身不受影響，玩家與敵人仍會被房屋擋住。
  - `GroundRangeIndicator.IsIgnored` 增加 `GetComponentInParent<RangeIndicatorIgnored>()` 檢查；Raycast 穿過房屋，改取下方最近的有效地面點。
  - `Scenes/Level_1.unity`：在 `Level1_Map/.../Buildings` 群組根物件上加入 `RangeIndicatorIgnored`，涵蓋其下所有 `Building_*`。選擇標記元件而不是新增圖層，是為了不必修改 `TagManager` 與每棟房屋的 Layer；之後新增房屋只要放在 `Buildings` 底下即可。
  - 沒有新增任何外部套件。
- 遊戲呈現：射程圈較粗、較容易看清楚；射程圈經過房屋時貼著房屋底下的地面延伸，不再爬上屋頂。從俯視角度看，被房屋蓋住的那一段會被房屋遮住。
- 限制：牆、閘門柱與管線仍視為地面（沒有要求略過）。寬度 2 時，射程內側 2 單位都被環帶覆蓋，可能遮住較多路面箭頭與防區填色，觀感**尚未驗證**。
- 測試修改：
  - EditMode 新增 `GroundRangeIndicator_SkipsSurfacesMarkedIgnored`：父物件有標記的方塊會被穿過，投影點落在下方地面。
  - PlayMode `RangeIndicators_ProjectToGroundAndTurnRedWhenEnemyInRange` 增加檢查：玩家與三座塔的射程圈寬度皆為 2；場景中的 `Buildings` 有 `RangeIndicatorIgnored`。

**驗證（本項）**

Unity 編輯器開著，所以把 `Assets`、`Packages`、`ProjectSettings` 複製到暫存資料夾，在副本上以批次模式執行；暫時的截圖測試只存在於副本中，副本已刪除。

| 項目 | 結果 |
| --- | --- |
| 編譯 | 沒有錯誤。 |
| EditMode 測試 | **28 項全數通過**（新增 1 項）。 |
| PlayMode 測試 | **19 項中 15 項通過**；`RangeIndicators_…` 通過。失敗的 4 項與本次修改無關：`Towers_Aim…`、`Upgrade_PlayerNearTower…` 為已知的塔攻擊力數值問題；`Level1_DefeatShowsResultPanel…`、`Level1_VictoryShowsResultPanel…`（結算面板未顯示）在副本中還原本次修改後重跑，結果相同。 |
| 房屋略過（場景內對照） | 暫時測試把玩家移到 `Building_Central_C1` 旁，使射程圈穿過房屋：有標記時，落在房屋範圍內的 3 個投影點高度約 -1.1（地面）；暫時移除標記後重新投影，同樣 3 點高度約 2.9（屋頂）。 |
| 截圖檢查 | 俯視截圖中玩家與塔的環帶寬度約為 2 單位；玩家環帶穿過房屋時位於房屋底下，沒有爬上屋頂。 |
| 開場位置的投影 | 玩家與三座塔在開場位置的射程圈都沒有取樣點落在房屋上，所以開場畫面與修改前只差在寬度。 |
| 在使用者編輯器中重新載入場景後的狀態、寬度 2 的實際觀感 | **尚未驗證** |

---

## 黃銅大門擊垮後的破碎效果（OpenFracture）

- 紀錄日期：2026-11-26
- 狀態：**已完成**；在使用者編輯器中的實際觀感**尚未驗證**。
- 需求來源：使用者指定以 OpenFracture（https://github.com/dgreenheck/OpenFracture）實作黃銅大門被擊垮後的破碎效果。
- 範圍：只有門板 `Gate_Door` 會破碎。門前護欄 `Gate_Barrier` 仍依原設定在閘門被打破時直接隱藏。

### 功能目的

閘門被打破時，原本只是讓門板消失。改為讓門板裂成碎片並往前倒塌，讓玩家清楚看到「閘門被擊垮」。

### 使用技術

- **外部套件：OpenFracture 1.0.2**（`com.dgreenheck.openfracture`）。
  - MIT 授權，作者 Dan Greenheck。
  - 以 git URL 安裝，並鎖定 commit `6d0ddff64173193f024638c2de2dbeb8a2525965`，避免上游更新改變行為。
  - 使用其 `Fracture` 元件：以隨機平面反覆切割網格，產生指定數量的碎片，每塊碎片帶有 `Rigidbody` 與凸面 `MeshCollider`。
- **隨套件一起加入的依賴**：OpenFracture 的 `package.json` 要求 `com.unity.testtools.codecoverage 1.0.1`。Unity 2022.3 實際解析為 1.2.6，並另外帶入 `com.unity.settings-manager 2.1.0`。這兩個都是 Unity 官方套件，只用於編輯器，本專案沒有使用。
- Unity 物理：`Rigidbody.AddExplosionForce`（`ForceMode.VelocityChange`，與碎片質量無關）。

### 實作方式

| 檔案 | 修改 |
| --- | --- |
| `Packages/manifest.json`、`Packages/packages-lock.json` | 新增 OpenFracture（鎖定 commit）。lock 檔由 Unity 更新，含上述兩個依賴。 |
| `Scripts/CyberWizard.Runtime.asmdef` | 以 GUID 引用 OpenFracture 的執行期組件。該組件的名稱是通用的 `RuntimeAssembly`，改用 GUID 可避免與其他套件混淆。 |
| `Scripts/Presentation/GateShatterOnDestroyed.cs`（新增） | 掛在 `Gate_Door`。監聽 `BaseCore.Destroyed`，觸發時呼叫 `Fracture.CauseFracture()`；在 OpenFracture 的 `onCompleted` 回呼中找到碎片父物件 `Gate_DoorFragments`，對每塊碎片施加一次爆炸速度。提供 `IsShattered`、`FragmentRoot` 屬性與 `Shattered` 事件（2026-11-27 因沒有訂閱者已移除）。 |
| `Scenes/Level_1.unity` | `Gate_Door` 新增 Kinematic `Rigidbody`（OpenFracture 必要元件，設為 Kinematic 讓門平時固定）、`Fracture`、`GateShatterOnDestroyed`（連到 `Gate_Core` 的 `BaseCore`）；並把 `Gate_Door` 從 `GateDefenseFlow.hideWhenGateDestroyed` 移除，清單只剩 `Gate_Barrier`。 |
| `Tests/PlayMode/Level1GateDefensePlayModeTests.cs` | 擴充既有的閘門測試：破碎前門是 Kinematic；被打破後，原門停用、`IsShattered` 為真、至少 10 塊碎片都是非 Kinematic 剛體並帶凸面碰撞體；碎片沒有被銷毀，平均位置往南（關內）移動，且至少 80% 留在地圖內。 |

**觸發方式**：`Fracture` 的 Trigger Type 設為 Collision，但沒有開啟標籤過濾。OpenFracture 只有在開啟標籤過濾時才會因碰撞破碎，所以門不會被碰撞打碎，只會由 `GateShatterOnDestroyed` 觸發。

**爆炸方向**：閘門位於地圖北緣（門在 z ≈ 127，地圖邊界在 128），北側沒有地面。第一版把碎片往北（城內）推，結果碎片全部掉出地圖。目前爆炸中心放在門的北側下方，碎片往南（關內、玩家所在側）與上方噴散，落在閘門前方。

### 可調整參數

| 位置 | 參數 | 目前值 | 用途 |
| --- | --- | --- | --- |
| `Gate_Door` → `Fracture` → Fracture Options | Fragment Count | 24（暫定） | 碎片數量。本次在批次模式下測得同步切割耗時約 4～24 ms。 |
| 同上 | Inside Material | `Blockout_Brass` | 切面的材質。 |
| 同上 | Asynchronous | 關閉 | 開啟後切割會分散在多幀進行；`GateShatterOnDestroyed` 使用 `onCompleted` 回呼，兩種模式都支援。 |
| `Gate_Door` → `GateShatterOnDestroyed` | Explosion Offset | (0, −2, 3)（暫定） | 爆炸中心相對於門中心的世界座標位移。 |
| 同上 | Explosion Strength | 7（暫定） | 碎片獲得的速度變化量。 |
| 同上 | Explosion Radius／Upwards Modifier | 25／1.5（暫定） | 影響半徑；數值越大，碎片越往上噴。 |

### 遊戲呈現

閘門血量歸零的瞬間，黃銅門板裂成 24 塊不規則碎片，往閘門前方與上方噴出後倒落，散落在閘門前的紅色禁區與道路上，並一直留在場上（關卡此時已判定失敗）。兩側門柱、側牆不受影響。

### 限制與待改善事項

- 碎片切割使用隨機平面，每次破碎的形狀都不同。
- ~~碎片會永久留在場景中，沒有淡出或回收~~（2026-11-28 已處理，見「待辦清理：測試修正、視覺與流程改善」）：10 遊戲秒後縮小消失，射程圈投影也會略過碎片。以下為原紀錄：目前關卡在閘門被打破時就結束，所以沒有影響。若之後閘門破壞後遊戲還要繼續，需要評估碎片對敵人、玩家與射程圈投影的影響（碎片不是 `IDamageable`，射程圈會投影到碎片上）。
- 少數碎片仍可能越過門往北飛出地圖（測試只要求至少 80% 留在地圖內）；~~並持續往下墜落~~（2026-11-28 已處理，見「待辦清理：測試修正、視覺與流程改善」）：低於閘門 30 單位即移除。
- 切面材質沿用 `Blockout_Brass`，沒有另外製作斷面質感。
- 沒有破碎音效；OpenFracture 附帶的音效沒有使用。
- 套件以 git URL 安裝：在新電腦上第一次開啟專案時需要安裝 git，並能連到 GitHub。
- OpenFracture 的類別（`Fracture`、`FractureOptions` 等）沒有命名空間；目前與專案程式碼沒有衝突。
- 匯入時 Unity 會提示套件中的 `Samples` 資料夾有 `.meta` 卻沒有資料夾，這是套件本身的狀況，不影響使用。

### 驗證方式

使用者已關閉 Unity 編輯器，因此直接在專案上以 Unity 2022.3.62f3 批次模式執行驗證。

| 項目 | 結果 |
| --- | --- |
| 套件安裝與編譯 | Unity 解析並匯入 OpenFracture 與其依賴，專案與套件的編譯都沒有錯誤或警告。 |
| EditMode 測試 | **21 項全數通過**。 |
| PlayMode 測試 | **7 項中 5 項通過**。擴充後的 `Level1_MinionsClimbToGateBreakItAndThenEverythingStops` 通過。失敗的 2 項與前幾節相同（`FixedTowerProfile.asset` 的攻擊力為 20，但測試寫死 10），與本次修改無關。 |
| 截圖檢查 | 以暫時的截圖測試（已刪除）在破碎前、破碎後 0.25 秒、1 秒與 4 秒截圖：門裂成 24 塊後往前倒塌，4 秒時碎片散落在閘門前方。第一版往北推的碎片會掉出地圖，已依此修正方向。 |
| 實際遊玩中閘門被打破時的觀感、在 Inspector 調整破碎參數 | **尚未驗證** |

---

## StreamingAssets 讀取支援 WebGL

- 紀錄日期：2026-11-26（晚）
- 狀態：**已完成**（編輯器與 WebGL 腳本編譯已驗證；WebGL 執行檔實際執行**尚未驗證**）。

### 功能目的

專案要同時輸出 WebGL 與本地執行檔。WebGL 執行檔的 `Application.streamingAssetsPath` 是伺服器網址，不能用 `System.IO.File` 讀取，原本的 `File.ReadAllText` 會找不到波次 JSON，導致關卡無法開始。

### 使用技術

- `UnityWebRequest.Get` + `SendWebRequest()`（內建模組 `com.unity.modules.unitywebrequest`，專案原本就已啟用）。**沒有新增外部套件。**
- 條件編譯 `#if UNITY_WEBGL && !UNITY_EDITOR`：只有 WebGL 執行檔走網路請求；編輯器與本地執行檔照舊同步讀檔。
- 以 `IEnumerator` 表示「可能需要等待」的讀取，由呼叫端手動 `MoveNext()` 走訪。

### 實作方式

| 檔案 | 修改 |
| --- | --- |
| `Assets/Scripts/Infrastructure/LevelScheduleLoader.cs` | 新增 `Load(relativePath, onCompleted)`：依平台選擇 `LoadWithWebRequest`（WebGL）或 `LoadFromFile`（其他）。新增 `LoadWithWebRequest(uri, relativePath, onCompleted)` 與 `BuildUri(relativePath)`（以 `/` 串接網址）。原有的同步 `TryLoadFromStreamingAssets` 保留，供本地讀檔與 EditMode 測試使用。 |
| `Assets/Scripts/Gameplay/GameFlow/GateDefenseFlow.cs` | `Start()` 改為 `IEnumerator Start()`，以 `while (loading.MoveNext()) yield return loading.Current;` 走訪讀取流程，完成後才初始化 `WaveDirector`、切到 `Ready` 並開始波次。 |

- 本地讀檔的列舉器不會 yield，第一次 `MoveNext()` 就結束，所以編輯器與本地執行檔的啟動時序與修改前相同（同一幀內進入 `Wave`），既有 PlayMode 測試不需要改等待方式。
- WebGL 會等待網路請求完成，這段期間 `CurrentPhase` 維持 `Starting`，HUD 不顯示勝敗訊息。
- 請求失敗或 JSON 解析失敗時，`onCompleted` 收到 `(null, 錯誤訊息)`，`GateDefenseFlow` 進入 `Failed` 並在 Console 與 HUD 顯示原因（與修改前讀檔失敗的處理相同）。
- `LoadWithWebRequest` 在所有平台都會編譯，只有 `Load()` 的選擇受條件編譯影響，因此在編輯器中也能以 `file://` 網址測試同一段程式。

### 遊戲呈現

玩家看不到差異；WebGL 版開場會多等一次網路請求（本機伺服器通常在一幀到數幀內完成）才開始倒數。

### 限制與待改善事項

- **尚未驗證**：實際建置 WebGL 並在瀏覽器（經由 HTTP 伺服器）執行。只驗證了 WebGL 平台的腳本編譯。
- 沒有逾時與重試；請求失敗就直接進入 `Failed`。
- 以 `file://` 直接開啟 WebGL 的 `index.html` 時，瀏覽器通常會擋下請求，需經由 HTTP 伺服器開啟（WebGL 本身的限制）。

### 驗證方式

| 項目 | 結果 |
| --- | --- |
| 編輯器編譯 | 通過，沒有錯誤或警告。 |
| WebGL 平台腳本編譯 | 以暫時的 Editor 腳本（已刪除）呼叫 `PlayerBuildInterface.CompilePlayerScripts`（目標 WebGL），19 個組件編譯成功，含 `CyberWizard.Runtime.dll`，沒有錯誤。 |
| EditMode：`LevelScheduleLoaderEditModeTests`（新增 3 項） | 通過：編輯器中第一次 `MoveNext()` 就同步完成並取得 2 個波次；缺檔時回報含檔名的錯誤；`BuildUri` 以 `/` 串接。 |
| PlayMode：`LevelScheduleLoaderPlayModeTests`（新增 2 項） | 通過：以 `file://` 網址呼叫 `LoadWithWebRequest`，正確解析 `level1_rust_alley`；缺檔時回報錯誤。 |
| 既有關卡流程 | `Level1_MinionsClimbToGateBreakItAndThenEverythingStops` 等 Level_1 PlayMode 測試通過，關卡照常進入 `Wave`。 |
| WebGL 執行檔實際執行 | **尚未驗證** |

---

## 道路無縫銜接與鵝卵石路面

- 紀錄日期：2026-11-26（晚）
- 狀態：**已完成**。

### 功能目的

原本六段道路是各自旋轉的扁平方塊，路口只靠互相重疊：閘門前的 Y 字路口東側斜路沒有接上閘門道路、方塊的直角從路口突出，接縫處也有 Z-fighting。本次讓路面在視覺上無縫銜接，並依 `Ref/第一關地圖.png` 的鵝卵石路面，從 `YughuesFreePavementsMaterials` 選出最接近的材質鋪在道路上。

### 使用技術

- 自訂元件 `RoadNetwork` + 純靜態工具 `RoadMeshBuilder`，以程式產生 `Mesh`（頂點、法線、切線、UV、三角形）。**沒有新增外部套件**；貼圖來自使用者已放入專案的 `YughuesFreePavementsMaterials`。
- 網格做法：每段中線產生一個矩形；折線中間的轉折點補一個外接正多邊形圓盤（24 邊，半徑為半寬），形成圓角；兩端為平頭。
- UV 採平面投影（本地 XZ ÷ 貼圖尺寸），切線固定為 (1, 0, 0, −1)。重疊的三角形取樣到完全相同的貼圖與法線，所以即使深度相同也看不出接縫或閃爍。
- 材質：URP Lit，Specular 工作流程，使用 `T_YFPM_StonesSmooth` 的 `_d`（Base Map）、`_n`（Normal Map）、`_s`（Specular Map）。

### 實作方式

| 檔案 | 責任 |
| --- | --- |
| `Assets/Scripts/Gameplay/Level/RoadMeshBuilder.cs` | `AppendPolyline(points, width, height, textureTileSize, joinSegments, ...)`：把一條折線附加到網格緩衝區；少於兩點、寬度或貼圖尺寸不大於 0 時回傳 false。 |
| `Assets/Scripts/Gameplay/Level/RoadNetwork.cs` | `[ExecuteAlways]`，保存道路清單（名稱、路寬、中線頂點），在 `OnEnable`／`OnValidate` 重建合併網格。網格標記 `DontSave`，不寫入場景檔（與 `TerracePlatform` 相同做法）。沒有碰撞。 |
| `Assets/Materials/Level1Environment/Env_RoadCobblestone.mat` | 新材質。Base Color (1, 0.9, 0.78) 讓石塊偏暖褐色以接近參考圖，Smoothness 0.5（暫定值）。 |
| `Assets/Scenes/Level_1.unity` | 刪除 `Road_GateApproach`、`Road_WestDiagonal`、`Road_EastDiagonal`、`Road_InnerLink`、`Road_South`、`Road_EastLink`；新增 `SlopedTerrain/Roads/Road_Network`（本地 Y = 0.1，與原道路頂面同高；不投射陰影、接收陰影）。GameObject 由 124 個變為 119 個。 |

**道路資料**（`Road_Network` 本地 XZ，與原道路中線相同；端點改成原道路中線的交點）

| 名稱 | 路寬 | 頂點 |
| --- | --- | --- |
| `GateApproach_WestDiagonal` | 10 | (10, 114.07) → (10, 92.07) → (−119.06, −79.92) |
| `EastDiagonal` | 10 | (10, 98.85) → (97.36, 15.98) |
| `South_EastLink` | 10 | (−126, −82.99) → (97.36, 15.98) → (126, 19.31) |
| `InnerLink` | 9 | (−45.89, 17.59) → (24.37, −16.36) |

- 交點由原本各方塊的中線計算：西斜路與閘門道路交於 (10, 92.07)，東斜路延長後交於 (10, 98.85)；左下角 (−119.06, −79.92) 與東側 (97.36, 15.98) 正好是敵人路徑點 `WP_00` 與 `path_right/WP_01` 的位置。
- 原本超出路口的部分（西斜路伸到 (−122.1, −83.9)、東斜路與南路伸過東側路口約 4）已裁掉。
- 接到另一條路的端點放在那條路的中線上，平頭的兩角落在那條路的範圍內（該路不比這條路窄時成立）；轉彎處由圓盤補滿外側。

**材質選擇**：比對套件中 20 種貼圖後，`StonesSmooth`（圓潤的鵝卵石）的形狀最接近參考圖的石板路；`Mediterranean01` 顏色較接近但石塊扁平細碎，像砌牆石。原貼圖偏灰綠，因此以 Base Color 調暖。套件原本的材質使用內建管線的 Standard (Specular setup) shader，在 URP 下會顯示為洋紅色，所以沒有直接使用，也沒有修改套件內的檔案（套件附的 URP `.unitypackage` 未匯入）。

### 可調整參數

| 物件／元件 | 參數 | 用途 |
| --- | --- | --- |
| `Road_Network` → `RoadNetwork` | `roads[].points` | 道路中線頂點（本地 XZ）。中間點自動補圓角；兩端平頭。 |
| 同上 | `roads[].width` | 路寬（目前 10，`InnerLink` 為 9）。 |
| 同上 | `textureTileSize` | 貼圖重複一次的距離，目前 4（暫定值；數值越大石塊越大）。 |
| 同上 | `joinSegments` | 轉角圓盤的邊數，目前 24。 |
| `Road_Network` Transform | Position Y | 路面高度，目前 0.1（與原方塊道路頂面相同）。 |
| `Env_RoadCobblestone` | Base Color、Smoothness、Normal 強度 | 路面色調與光澤（暫定值）。 |

### 遊戲呈現

- 路面為暖褐色鵝卵石，所有路口與轉角都連成一片，沒有缺口、突出的直角或閃爍；俯視小地圖中也能看到石紋。
- 閘門前的 Y 字路口：西斜路以圓角轉進閘門道路，東斜路直接接上閘門道路。
- 道路接到東、西外牆的兩端是平頭，不會穿出牆外。
- 敵人路線、路徑點、路線箭頭都沒有修改，箭頭仍浮在路面上方。

### 限制與待改善事項

- 路口內側仍是直角，沒有內側倒角；參考圖中的大弧度彎道仍以直線段近似。
- 路面只有頂面，沒有路緣石或側面；道路與地面的 0.1 高低差在一般視角看不出來。
- 平面投影 UV 使貼圖方向固定（不隨道路方向旋轉），鵝卵石沒有方向性，所以看不出差異；若改用有方向的石板貼圖需要另外處理。
- 場景沒有烘焙光照與反射探針，環境反射為黑色，路面的高光表現比套件預覽暗；顏色與光澤為暫定值，需由美術確認。
- `Blockout_Road` 材質已無物件使用，仍保留。
- 在使用者開啟中的編輯器內，Inspector 修改道路頂點後即時重建**尚未驗證**。
- `YughuesFreePavementsMaterials` 仍是未提交的新資料夾（含兩個 `.unitypackage`，約 200 MB 的 TGA 貼圖），本次沒有修改它；是否加入版本控制由使用者決定。

### 驗證方式

使用者沒有開啟 Unity 編輯器，直接在專案上以 Unity 2022.3.62f3 批次模式執行。

| 項目 | 結果 |
| --- | --- |
| 場景修改 | 以暫時的 Editor 腳本（已刪除）建立材質、刪除六段道路、建立 `Road_Network` 並存檔。修改前的場景另存一份在暫存資料夾作為備份。 |
| EditMode：`RoadMeshBuilderTests`（新增 4 項） | 通過：直線路是一個朝上的矩形（平頭）；轉角補圓盤且覆蓋外側轉角、不超出半寬；所有頂點 UV 等於平面投影、法線朝上；無效輸入不附加資料。 |
| PlayMode：`Scene_HasFollowCameraMinimapAndSpawnSmoke`（擴充） | 通過：`Roads` 底下只剩 `Road_Network`，含 4 條道路，網格頂點數大於 16，材質為 `Env_RoadCobblestone`。 |
| 截圖檢查 | 以暫時的截圖工具（已刪除）輸出全圖俯視、五個路口特寫與兩張斜視圖，修改前後對照：Y 字路口由斷開變為相連，各路口沒有缺口或突出直角，兩端與外牆齊平，鵝卵石貼圖正確顯示（不是洋紅色）。 |
| 實際遊玩時近距離觀看路面、在 Inspector 調整道路資料 | **尚未驗證** |

---

## 出生點灰霧放大

- 紀錄日期：2026-11-26（晚）
- 狀態：**已完成**。

### 功能目的

依使用者要求，敵方出生點（`Spawn_SouthWest/SpawnSmoke`）的灰霧大小提升為 2 倍、密度提升 1 倍。

### 使用技術

內建 `ParticleSystem`（沿用既有設定與 `Fx_Smoke` 材質）。**沒有新增外部套件，沒有修改程式碼。**

### 實作方式

「大小 ×2」解讀為整團煙霧等比例放大：範圍、粒子尺寸與移動速度一起乘 2，煙霧形狀不變。等比例放大後，單位面積上的粒子覆蓋層數不變；「密度 ×2」再把每秒生成數加倍，覆蓋層數變為 2 倍。

| 參數 | 修改前 | 修改後 |
| --- | --- | --- |
| Shape（Circle）Radius | 9 | 18 |
| Start Size | 5～10 | 10～20 |
| Start Speed | 0.4～1.4 | 0.8～2.8 |
| Velocity over Lifetime（World） | X、Z：−0.4～0.4；Y：0.3～0.9 | X、Z：−0.8～0.8；Y：0.6～1.8 |
| Emission Rate over Time | 22 | 44 |
| Max Particles | 300 | 600 |

壽命（4～7 秒）、顏色、透明度、Size／Color over Lifetime 與預熱都沒有修改。以暫時的 Editor 腳本（已刪除）修改後存入場景。

### 遊戲呈現

左下角出生點的灰霧範圍直徑由約 18 變為約 36，且明顯較濃；敵人從更大片的霧中走出。路線箭頭在霧中仍可看見。

### 限制與待改善事項

- 出生點在地圖角落，半徑 18 的霧有相當大一部分飄到西牆外與擋土牆南側的下層上方。
- 同時存在的粒子約 240 個（44 × 平均壽命 5.5 秒），每個粒子更大，半透明重疊（overdraw）比修改前高；WebGL 或低階顯示卡上的效能**尚未驗證**。
- 若「密度」原意是指透明度而不是粒子數量，需要再調整 Start Color 的 Alpha。

### 驗證方式

| 項目 | 結果 |
| --- | --- |
| PlayMode：`Scene_HasFollowCameraMinimapAndSpawnSmoke`（更新） | 通過：半徑 18、尺寸 10～20、每秒 44 個、上限 600，開場預熱後粒子數大於 40。 |
| 截圖檢查 | 以暫時的 PlayMode 截圖測試（已刪除）在進入場景 10 幀後從斜上方與正上方截圖：出生點被大片濃灰霧覆蓋。 |
| 實際遊玩中的觀感與效能 | **尚未驗證** |

---

## 關卡結束慢動作

- 紀錄日期：2026-11-26（晚）
- 狀態：**已完成**。

### 功能目的

取代原本「勝利或失敗時停用玩家操作並釋放游標」的做法：結束的瞬間把遊戲時間放慢為 0.25 倍，玩家仍可操作，滑鼠視角的轉動速度也跟著變慢。

### 使用技術

- `Time.timeScale`。**沒有新增外部套件。**
- 移動、攻擊冷卻、敵人、粒子等原本就以 `Time.deltaTime`／`Time.time` 計算，自動跟著變慢。
- 滑鼠視角：`Input.GetAxis("Mouse X/Y")`（2026-11-27 起改由 `GameInput.Look` 讀取）是每幀的滑鼠位移，與時間無關，因此在 `PrototypeCameraFollow` 乘上 `Time.timeScale`。
- `Rigidbody.interpolation`：閘門碎片開啟插值，讓慢動作中的碎片平滑移動。

### 實作方式

| 檔案 | 修改 |
| --- | --- |
| `Assets/Scripts/Gameplay/GameFlow/GateDefenseFlow.cs` | 移除 `EndPlayerControl()` 與 `player` 欄位；勝利（`Ended`）與失敗（`Defeat`）時改呼叫 `ApplyEndSlowMotion()`，把 `Time.timeScale` 設為 `endTimeScale`（預設 0.25）。`OnDestroy` 時若曾放慢則還原為 1，避免影響下一個場景。 |
| `Assets/Scripts/Presentation/PrototypeCameraFollow.cs` | 滑鼠靈敏度乘上 `Time.timeScale`。 |
| `Assets/Scripts/Presentation/GateShatterOnDestroyed.cs` | 碎片剛體設為 `RigidbodyInterpolation.Interpolate`。 |

- **物理步長（`Time.fixedDeltaTime`）刻意不縮小**：第一版把步長同比例縮為 0.005，結果閘門碎片的物理結果改變（碎片平均位置往北（關外）偏約 1.2，`Level1_MinionsClimbToGateBreakItAndThenEverythingStops` 的「碎片往南推」檢查失敗）；恢復原步長後連續兩次通過。因此保留原步長 0.02（慢動作中約每 0.08 秒真實時間一步），改以剛體插值維持畫面平滑。
- 結束後 `cameraFollow` 維持啟用，游標保持鎖定；HUD 照常顯示勝敗訊息。（2026-11-27 修改：判定後 2 秒真實時間出現結算面板，此時才關閉滑鼠視角、釋放游標並停用玩家操作；HUD 不再顯示勝敗訊息，見「遊戲流程：初始畫面、選關與結算面板」。）

### 可調整參數

| 物件／元件 | 參數 | 用途 |
| --- | --- | --- |
| `GateDefenseFlow` | `endTimeScale` | 勝敗瞬間套用的時間倍率，預設 0.25（範圍 0.05～1；1 表示不放慢）。 |

### 遊戲呈現

- 閘門被打破或最後一個敵人被清除的瞬間，整個遊戲變成 0.25 倍速：玩家移動、跳躍、攻擊、敵人、塔、閘門碎片與煙霧都變慢，轉動滑鼠時視角也以 1/4 的速度轉動。
- 畫面中央照常顯示「失敗」或「勝利」訊息；玩家仍可在慢動作中移動與攻擊。
- 失敗時敵人仍維持停止；勝利後場上已沒有敵人。

### 限制與待改善事項

- 慢動作會一直持續到離開場景。2026-11-27 起可從結算面板「再次挑戰」或「確認」離開場景，`OnDestroy` 會把時間倍率還原為 1。
- 結束後玩家仍可攻擊、升級塔（升級會花費能量），這些操作不會改變已判定的勝敗結果。
- 失敗後塔仍會攻擊停住的敵人；排程已停止，擊殺後不會轉為勝利。
- 慢動作期間物理步長仍是 0.02 遊戲秒；除了閘門碎片外，若之後加入其他剛體，需要一併開啟插值。
- 滑鼠靈敏度在任何時候都乘上 `Time.timeScale`，所以測試中加速到 20 倍時視角也會轉得較快（實際遊戲中平常為 1，不受影響）。
- 實際用滑鼠轉動視角的速度變化**尚未驗證**（批次模式沒有滑鼠輸入）。

### 驗證方式

| 項目 | 結果 |
| --- | --- |
| PlayMode：`Level1EndSlowMotionPlayModeTests.Victory_SlowsGameTimeAndKeepsPlayerControl`（新增） | 通過：以 20 倍速並直接擊殺所有生成的敵人，勝利瞬間 `Time.timeScale` 為 0.25、`fixedDeltaTime` 不變、`player.InputEnabled` 與 `cameraFollow.GameplayActive` 仍為 true；之後 30 幀遊戲時間前進量小於真實時間的 0.3 倍。 |
| PlayMode：`Level1_MinionsClimbToGateBreakItAndThenEverythingStops`（擴充） | 通過：失敗瞬間 `Time.timeScale` 為 0.25，玩家操作與滑鼠視角維持啟用，碎片皆開啟插值；之後恢復加速檢查碎片落點，結果與修改前相同。 |
| 滑鼠視角轉速、實際遊玩觀感 | **尚未驗證** |

---

## 本輪測試總結（2026-11-26 晚）

| 項目 | 結果 |
| --- | --- |
| EditMode 測試 | **28 項全數通過**（新增 7 項：`RoadMeshBuilderTests` 4 項、`LevelScheduleLoaderEditModeTests` 3 項）。 |
| PlayMode 測試 | **10 項中 8 項通過**（新增 3 項，皆通過）。失敗的 2 項與前幾節相同：`Towers_AimFireRivetsKillEnemiesAndGrantEnergy`、`Upgrade_PlayerNearTowerUpgradesEachPartWithEnergy`，原因是 `FixedTowerProfile.asset` 的攻擊力為 20，但測試寫死 10，與本次修改無關。 |
| 暫時檔案 | 截圖工具、場景修改腳本、WebGL 編譯檢查腳本與截圖測試都已刪除（含 `.meta`）。 |

---

## 遊戲流程：初始畫面、選關與結算面板

- 紀錄日期：2026-11-27（分支 `add_game_flow`）
- 狀態：**已完成**（最小可用流程）。第 2、3 關格子**尚未實作**（按鈕停用，不執行任何動作）。

### 功能目的

把原本只有 `Level_1` 的原型串成完整流程：初始畫面（開始／離開）→ 選關（三個關卡格子，第 1 關進入 `Level_1`）→ 遊玩 → 結算面板（勝利或失敗、再次挑戰、確認回選關）。

### 使用技術

- uGUI（`com.unity.ugui` 1.0.0，專案原本就有的內建套件）：`Canvas`（Screen Space Overlay）、`CanvasScaler`（Scale With Screen Size，參考解析度 1920 × 1080、寬高各半）、`Button`、`Image`、`Text`、`Horizontal/VerticalLayoutGroup`、`EventSystem` + `StandaloneInputModule`（專案使用舊版 Input Manager）。
- 文字使用內建字型 `LegacyRuntime.ttf`，未使用 TextMeshPro（與既有場景一致，避免匯入 TMP Essentials）。按鈕與面板使用內建 UI Sprite（`UISprite`、`Background`）。
- `SceneManager.LoadScene`（依場景名稱）、`Application.CanStreamedLevelBeLoaded` 檢查場景是否已加入 Build Settings。
- `WaitForSecondsRealtime`：結算面板延遲以真實時間計算，不受結束慢動作（`Time.timeScale` = 0.25）影響。
- `CyberWizard.Runtime` 組件新增對 `UnityEngine.UI` 的參考；`CyberWizard.PlayModeTests` 也加入 `UnityEngine.UI`。**沒有新增外部套件。**

### 實作方式

**新增場景**（Build Settings 順序：0 `MainMenu`、1 `LevelSelect`、2 `Level_1`）

| 場景 | 內容 |
| --- | --- |
| `Assets/Scenes/MainMenu.unity` | `Main Camera`（純色背景）、`EventSystem`、`Main Menu Canvas`：標題「CyberWizard TD」，畫面中央下方垂直排列「開始」「離開」兩個按鈕；`MainMenuController` 掛在 Canvas 上。 |
| `Assets/Scenes/LevelSelect.unity` | `Main Camera`、`EventSystem`、`Level Select Canvas`：畫面中央的 `Level Select Panel`（標題「選擇關卡」與三個並排的關卡格子按鈕）；`LevelSelectController` 掛在 Canvas 上。 |

**`Level_1` 新增物件**：`EventSystem`，以及 `Result Canvas`（sortingOrder 100，掛 `LevelResultPanel`）。其下的 `Result Panel`（全螢幕半透明黑底，場景中預設停用）內含 `Window`：標題 `Title`、說明 `Detail`、按鈕列「再次挑戰」「確認」。既有物件沒有修改。

場景與 UI 物件以一次性的編輯器腳本在批次模式中建立，之後腳本已刪除；之後直接在場景中編輯即可。

**新增腳本**

| 檔案 | 責任 |
| --- | --- |
| `Assets/Scripts/Infrastructure/SceneLoading.cs` | 靜態工具。`TryLoad(sceneName)`：名稱空白或未加入 Build Settings 時記錄錯誤並回傳 false，否則以 Single 模式載入。`TryReloadActive()`：以目前場景**名稱**重新載入（以編輯器 API 依路徑開啟的場景 `buildIndex` 為 -1，所以不用 index）。 |
| `Assets/Scripts/Presentation/Menus/MainMenuController.cs` | 在 `OnEnable` 綁定按鈕：「開始」→ `TryLoad(levelSelectSceneName)`；「離開」→ `Application.Quit()`（編輯器中改為停止 Play Mode）。 |
| `Assets/Scripts/Presentation/Menus/LevelSelectController.cs` | 保存 `LevelEntry[]`（按鈕 + 場景名稱）。場景名稱空白的格子設為 `interactable = false` 且不綁定任何動作；有名稱的格子按下後載入該場景。 |
| `Assets/Scripts/Presentation/LevelResultPanel.cs` | 訂閱 `GateDefenseFlow.PhaseChanged`。進入 `Ended`（勝利）或 `Defeat`（失敗）後，等待 `showDelaySeconds` 真實秒數，再設定標題文字與顏色、說明文字（失敗時使用 `GateDefenseFlow.FailureReason`），呼叫 `PrototypeCameraFollow.SetGameplayActive(false)`（關閉滑鼠視角、釋放游標）、把 `PrototypePlayerController.InputEnabled` 設為 false，最後啟用面板。「再次挑戰」→ `TryReloadActive()`；「確認」→ `TryLoad(levelSelectSceneName)`。必要參考缺少時記錄錯誤並停用自身。 |

**修改既有腳本**

| 檔案 | 修改前 → 修改後 |
| --- | --- |
| `Assets/Scripts/Presentation/GateDefenseHud.cs` | 修改前：畫面中央以 IMGUI 顯示「勝利／失敗」橫幅。修改後：勝敗改由結算面板顯示（IMGUI 永遠畫在 uGUI 之上，保留會與面板重疊）；只保留「關卡無法開始」的錯誤訊息。 |

時間倍率：離開 `Level_1` 時 `GateDefenseFlow.OnDestroy` 既有的還原邏輯會把 `Time.timeScale` 設回 1，選單與重新載入的關卡都以正常速度執行（已由測試確認）。

### 可調整參數

| 物件／元件 | 參數 | 用途 |
| --- | --- | --- |
| `MainMenu` / `MainMenuController` | `levelSelectSceneName` | 「開始」載入的場景，預設 `LevelSelect`。 |
| `LevelSelect` / `LevelSelectController` | `levels[i].button`、`levels[i].sceneName` | 每個關卡格子對應的場景；目前第 1 格為 `Level_1`，第 2、3 格留空（尚未開放）。新增關卡時填入場景名稱並加入 Build Settings 即可。 |
| `Level_1` / `LevelResultPanel` | `showDelaySeconds` | 勝敗判定後到出現面板的真實秒數，**暫定值 2**。 |
| `Level_1` / `LevelResultPanel` | `levelSelectSceneName` | 「確認」載入的場景，預設 `LevelSelect`。 |
| `Level_1` / `LevelResultPanel` | `victoryTitle`、`defeatTitle`、`victoryDetail`、`victoryColor`、`defeatColor` | 面板文字與標題顏色（預設「勝利」綠色、「失敗」紅色，沿用原 HUD 的顏色）。 |

### 遊戲呈現

- 啟動後看到深色背景、黃銅色標題「CyberWizard TD」，畫面中央下方由上到下為「開始」「離開」。
- 「開始」進入選關畫面：中央面板顯示「選擇關卡」與三個方框「第 1 關 鏽巷」「第 2 關 尚未開放」「第 3 關 尚未開放」；第 2、3 關為灰色停用狀態，點擊沒有反應。
- 點「第 1 關」進入 `Level_1`，遊玩方式與之前相同。
- 閘門被打破或清除所有波次後，先播放 0.25 倍速慢動作約 2 秒（真實時間），期間仍可操作；接著畫面變暗並出現結算視窗，標題「勝利」（綠）或「失敗」（紅），下方說明「所有波次已清除」或「黃銅閘門已被打破」。此時游標被釋放、視角與玩家操作停止。
- 「再次挑戰」重新載入 `Level_1`；「確認」回到選關畫面。

### 限制與待改善事項

- ~~選關畫面沒有「返回初始畫面」按鈕；遊玩中沒有暫停選單~~：2026-11-27 已補上，見下一節「選關返回按鈕與遊玩中暫停面板」。
- 第 2、3 關沒有場景，格子只是停用的佔位按鈕。
- 沒有關卡解鎖或進度存檔；第 1 關永遠開放。
- 場景切換為同步 `LoadScene`，沒有讀取畫面或轉場效果；`Level_1` 載入時間較長時畫面會短暫停住。
- 選單畫面為基本色塊與內建字型，尚未有美術設計；中文字形依賴作業系統字型後援（與既有 `TextMesh` 標籤相同），在非 Windows 平台的顯示**尚未驗證**。
- ~~結算面板出現後 HUD（左上角狀態）仍會顯示~~（2026-11-28 已處理，見「待辦清理：測試修正、視覺與流程改善」）；`Level_1` 若從編輯器直接開啟遊玩，「確認」仍會前往 `LevelSelect`。
- 慢動作延遲期間（約 2 秒）玩家仍可攻擊或升級塔，與先前的設計相同。
- ~~「離開」在建置版本中呼叫 `Application.Quit()`，WebGL 平台上此呼叫沒有作用。~~（2026-11-28 已處理，見「待辦清理：測試修正、視覺與流程改善」）：WebGL 建置版本隱藏「離開」按鈕。
- 實際以滑鼠點擊按鈕、鍵盤導覽與不同解析度下的版面**尚未驗證**（批次模式無法實際操作與檢視畫面）。

### 驗證方式

| 項目 | 結果 |
| --- | --- |
| 編譯（Unity 2022.3.62f3 批次模式） | 通過，沒有編譯錯誤。 |
| PlayMode：`GameFlowPlayModeTests.MainMenu_StartButtonLoadsLevelSelect`（新增） | 通過：「開始」在「離開」上方；觸發「開始」後使用中場景變為 `LevelSelect`。 |
| PlayMode：`GameFlowPlayModeTests.LevelSelect_FirstSlotLoadsLevel1AndOtherSlotsAreLocked`（新增） | 通過：共 3 個格子，第 2、3 格不可互動；觸發第 1 格後載入 `Level_1`，流程進入 `Wave`。 |
| PlayMode：`GameFlowPlayModeTests.Level1_VictoryShowsResultPanelAndConfirmReturnsToLevelSelect`（新增） | 通過：以 20 倍速擊殺所有敵人後判定勝利，判定瞬間面板尚未出現；延遲後面板顯示「勝利」，滑鼠視角關閉、游標為 `None`、玩家操作停用；觸發「確認」後回到 `LevelSelect`，`Time.timeScale` 為 1。 |
| PlayMode：`GameFlowPlayModeTests.Level1_DefeatShowsResultPanelAndRetryReloadsLevel`（新增） | 通過：直接打破閘門後面板顯示「失敗」並釋放操作；觸發「再次挑戰」後重新載入 `Level_1`（舊流程物件已銷毀、新流程為 `Wave`、面板隱藏、`Time.timeScale` 為 1）。第一次執行時失敗（原本用 `buildIndex` 重新載入，編輯器依路徑載入的場景為 -1），改為依名稱載入後通過。 |
| 既有測試 | EditMode **28 項全數通過**；PlayMode 既有 10 項中 8 項通過，失敗的 2 項（`Towers_AimFireRivetsKillEnemiesAndGrantEnergy`、`Upgrade_PlayerNearTowerUpgradesEachPartWithEnergy`）與上一輪相同，原因是測試寫死攻擊力 10，與本次修改無關。`Victory_SlowsGameTimeAndKeepsPlayerControl` 仍通過（面板延遲出現，判定瞬間玩家操作仍啟用）。 |
| 滑鼠實際點擊、畫面版面、建置版本的「離開」 | **尚未驗證** |

註：測試按鈕以 `Button.onClick.Invoke()` 觸發，未模擬滑鼠點擊與 `EventSystem` 射線檢測。

---

## 選關返回按鈕與遊玩中暫停面板

- 紀錄日期：2026-11-27（分支 `add_game_flow`）
- 狀態：**已完成**。

### 功能目的

- 選關畫面左上角加入箭頭按鈕，讓玩家回到初始畫面。
- 遊玩中按 Esc 暫停遊戲並顯示暫停面板，可「繼續」「重新開始」「中離」。

### 使用技術

- uGUI（與上一節相同）；返回按鈕的箭頭是內建字型的「←」字元（U+2190），不是圖片。
- `Time.timeScale = 0` 暫停；uGUI 的 `EventSystem` 與按鈕顏色過渡使用不受時間倍率影響的時間，暫停中仍可操作。
- 舊版 Input Manager：`Input.GetKeyDown(KeyCode.Escape)`。
- 沿用既有但未使用過的 `GamePhase.Paused`。**沒有新增外部套件。**

### 實作方式

| 檔案 | 修改 |
| --- | --- |
| `Assets/Scripts/Gameplay/GameFlow/GateDefenseFlow.cs` | 新增 `TryPause()`：只在 `Wave` 時有效，記錄目前 `Time.timeScale` 後設為 0，進入 `Paused`。新增 `Resume()`：只在 `Paused` 時有效，還原暫停前的時間倍率並回到 `Wave`。`OnDestroy` 的還原條件從「只有結束慢動作」擴充為「結束慢動作或暫停中」，所以從暫停面板重新開始或中離時時間倍率會回到 1。 |
| `Assets/Scripts/Presentation/LevelPausePanel.cs`（新增） | 每幀檢查 `toggleKey`（預設 Esc），呼叫 `TogglePause()`：`Paused` 時恢復，否則嘗試 `flow.TryPause()`，成功才顯示面板並停用操作（`PrototypeCameraFollow.SetGameplayActive(false)` 釋放游標、`PrototypePlayerController.InputEnabled = false`）。「繼續」→ `flow.Resume()`，隱藏面板並恢復操作；「重新開始」→ `SceneLoading.TryReloadActive()`；「中離」→ `SceneLoading.TryLoad(quitSceneName)`。必要參考缺少時記錄錯誤並停用自身。 |
| `Assets/Scripts/Presentation/Menus/LevelSelectController.cs` | 新增 `backButton` 與 `mainMenuSceneName`（預設 `MainMenu`），按下載入初始畫面。 |
| `Assets/Scripts/Presentation/GateDefenseHud.cs` | 「下一波倒數」在 `Paused` 時也顯示（修改前只在 `Wave` 顯示，暫停時會消失）。 |

**場景修改**（用一次性編輯器腳本建立，腳本已刪除）

- `LevelSelect.unity`：`Level Select Canvas` 底下新增 `Back Button`（錨點左上角，距邊緣 40，120 × 100，文字「←」）。
- `Level_1.unity`：新增 `Pause Canvas`（sortingOrder 90，低於結算面板的 100），掛 `LevelPausePanel`；其下的 `Pause Panel`（全螢幕半透明黑底，預設停用）內含 `Window`：標題「暫停」與垂直排列的「繼續」「重新開始」「中離」。

### 可調整參數

| 物件／元件 | 參數 | 用途 |
| --- | --- | --- |
| `LevelSelect` / `LevelSelectController` | `mainMenuSceneName` | 返回按鈕載入的場景，預設 `MainMenu`。 |
| `Level_1` / `LevelPausePanel` | `toggleKey` | 切換暫停的按鍵，預設 `Escape`。 |
| `Level_1` / `LevelPausePanel` | `quitSceneName` | 「中離」載入的場景，**暫定 `LevelSelect`**（與結算面板「確認」相同）；若要回到初始畫面改為 `MainMenu`。 |

### 遊戲呈現

- 選關畫面左上角有「←」按鈕，按下回到初始畫面。
- 遊玩中按 Esc：遊戲完全靜止（敵人、塔、波次倒數、粒子都停住），畫面變暗並出現「暫停」視窗，游標被釋放。
- 「繼續」或再按一次 Esc：面板關閉，游標重新鎖定，以暫停前的速度繼續。
- 「重新開始」：重新載入 `Level_1`。「中離」：回到選關畫面。
- 已分出勝負（包含結束慢動作的 2 秒與結算面板）後按 Esc 不會有反應。

### 限制與待改善事項

- 暫停只在 `Wave` 狀態有效；`Ready`（目前 `startOnPlay` 開啟，會立刻進入 `Wave`）與 `Starting`（WebGL 讀取波次資料中）時按 Esc 不會暫停。
- 暫停中若閘門被打破或最後一個敵人死亡，`GateDefenseFlow` 只在 `Wave` 時處理這些事件，結果會被忽略。目前 `timeScale` 為 0 時敵人、塔與投射物都不會移動，實際上不會發生，但之後若加入不受時間倍率影響的傷害來源需要處理。
- 在 Unity 編輯器的 Game 視窗中，按 Esc 本身也會讓編輯器釋放游標；繼續後若游標沒有重新鎖定，需點一下 Game 視窗（建置版本不受影響）。
- ~~「中離」沒有確認對話框，按下立即離開~~（2026-11-28 已處理，見「待辦清理：測試修正、視覺與流程改善」）。關卡進度不保留。
- 暫停面板沒有設定選項（音量、靈敏度等）。
- 實際按 Esc、滑鼠點擊按鈕、版面與「←」字元的顯示**尚未驗證**（批次模式無法實際操作與檢視畫面；測試直接呼叫 `TogglePause()` 與 `onClick.Invoke()`）。

### 驗證方式

| 項目 | 結果 |
| --- | --- |
| 編譯（Unity 2022.3.62f3 批次模式） | 通過。 |
| PlayMode：`LevelSelect_BackButtonAtTopLeftReturnsToMainMenu`（新增） | 通過：返回按鈕錨定在左上角；觸發後回到 `MainMenu`。 |
| PlayMode：`Level1_PauseFreezesTimeAndResumeRestoresControl`（新增） | 通過：暫停後 `Paused`、`timeScale` 為 0、面板顯示、滑鼠視角關閉、游標 `None`、玩家操作停用，10 幀內 `WaveDirector.ElapsedTime` 沒有前進；「繼續」後回到 `Wave`、`timeScale` 為 1、面板隱藏、視角與操作恢復；再呼叫兩次 `TogglePause()` 可暫停與恢復。第一次執行時另外檢查繼續後游標為 `Locked` 而失敗：批次模式沒有取得焦點的視窗，Unity 不會鎖定游標，因此移除這項檢查，只檢查 `GameplayActive`。 |
| PlayMode：`Level1_PauseRestartReloadsLevelAtNormalSpeed`（新增） | 通過：暫停中「重新開始」後重新載入 `Level_1`，新流程為 `Wave`、暫停面板隱藏、`timeScale` 為 1。 |
| PlayMode：`Level1_PauseQuitReturnsToLevelSelectAtNormalSpeed`（新增） | 通過：暫停中「中離」後回到 `LevelSelect`，`timeScale` 為 1。 |
| PlayMode：`Level1_DefeatShowsResultPanelAndRetryReloadsLevel`（擴充） | 通過：失敗後呼叫 `TogglePause()` 不會進入暫停，也不會顯示暫停面板。 |
| 全部測試 | EditMode **28 項全數通過**。PlayMode 共 18 項：`GameFlowPlayModeTests` 8 項全數通過（修正游標檢查後單獨重跑）；其餘 10 項中 8 項通過，失敗的 2 項是之前就已失敗的 `Towers_AimFireRivetsKillEnemiesAndGrantEnergy`、`Upgrade_PlayerNearTowerUpgradesEachPartWithEnergy`（測試寫死攻擊力 10），與本次修改無關。 |
| 實際按鍵與滑鼠操作、畫面版面 | **尚未驗證** |

---

## 待辦清理：測試修正、視覺與流程改善

- 紀錄日期：2026-11-28
- 狀態：**已完成**（17 項）；在使用者編輯器中的實際觀感與操作**尚未驗證**。
- 需求來源：從「尚未完成項目」中列出 20 項可直接處理的工作，使用者確認執行其中 17 項（不做：射程圈跨平台邊緣補點、紅色射程圈 Bloom、塔被摧毀的外觀變化）。
- 沒有新增任何外部套件。

### 功能目的

讓測試全部通過，修正合併時遺失的結算事件，並處理已知的視覺與流程限制，讓後續修改有可靠的驗證基準。

### 實作方式

**測試與程式碼狀態**

| 項目 | 修改 |
| --- | --- |
| 結算面板不出現（錯誤修正） | `GateDefenseFlow.ChangePhase()` 在 commit `cfabbd5`（2026-11-27）中遺失 `PhaseChanged?.Invoke(phase)`，`LevelResultPanel` 收不到勝敗事件，勝利或失敗後面板永遠不顯示。已補回。`Level1_Victory…`、`Level1_Defeat…` 兩項 PlayMode 測試因此恢復通過。 |
| 塔數值測試 | `Towers_AimFire…` 與 `Upgrade_PlayerNearTower…` 原本寫死攻擊力 10、攻速 1、射程 24 等值。改為以 `tower.Profile`（`FixedTowerProfile` 資產）的 `GetDamage`／`GetShotsPerSecond`／`GetAttackRange`／`GetMaxHealth`／`TryGetUpgradeCost` 計算期望值；砲塔升級改為迴圈升到 `GetMaxLevel`。之後調整資產數值時不必改測試。EditMode 的數值表測試使用程式預設值，未修改。 |
| `GamePhase.cs` | 工作區中有一處未提交的修改（新增 `GamePhase.Paused`），HEAD 的 `GateDefenseFlow`、`GateDefenseHud`、`LevelPausePanel` 都用到它，沒有它 HEAD 無法編譯。本次沒有修改此檔，只確認它與其他程式一起可以編譯；**尚未提交**。 |
| 文件 | `Ref/後續開發指南.md`：開頭加上註記（VerticalSlice、Builder、UI Toolkit HUD 已刪除，路徑已搬到 `Assets/`），仍存在的路徑改為 `Assets/Scripts`、`Assets/Tests`，已刪除的檔案標示「已刪除」，測試數量改為參照本報告。企劃內容未修改。`Assets/README.md` 已不寫測試數量，不需修改。 |

**射程圈與碎片**

| 項目 | 修改 |
| --- | --- |
| 射程圈被防區染藍 | 防區填色在平台頂面 +0.2，`GroundRangeIndicator.lift` 原本也是 0.2，兩者同高，半透明填色會疊在環帶上。`lift` 改為 0.3，環帶高於填色，深度測試會擋下填色。環帶仍蓋在路線箭頭（+0.12）之上，與修改前相同。 |
| 碎片不作為投影地面 | `GateShatterOnDestroyed` 破碎完成後，在碎片父物件加上 `RangeIndicatorIgnored`。 |
| 碎片清除（新增 `ShatterFragmentCleanup`） | 由 `GateShatterOnDestroyed` 掛在碎片父物件上。每幀檢查碎片重心：低於「閘門高度 − `fragmentKillDepth`」立即移除；經過 `fragmentLifetime` 遊戲秒後，所有碎片在 `fragmentShrinkDuration` 內縮小到 0，再移除父物件。計時使用 `Time.deltaTime`，會跟著結束慢動作放慢。 |

**路線箭頭**

| 項目 | 修改 |
| --- | --- |
| 頭尾淡入淡出 | `ChevronStrip.Settings.FadeDistance`：箭頭中心離折線起點或終點小於此距離時，依比例縮小（`FadeScale`），比例 0 時不產生網格。配合流動動畫，新箭頭從起點逐漸放大，到終點逐漸縮小消失。箭頭材質是 URP Unlit，不讀頂點色，無法逐箭頭調透明度，所以以縮放代替。 |
| 隨坡度傾斜 | `ChevronStrip.Settings.FollowSlope`：箭頭的前後方向改用線段的 3D 方向，左右方向維持水平。路線點之間是直線內插，所以傾斜角度是路線點連線的坡度。 |
| 預設值 | 兩個設定在 `ChevronStrip.Settings.Default` 中關閉（既有測試行為不變）；`WavePathPreview` 新增 Inspector 欄位 `followSlope`（預設開）與 `fadeDistance`（預設 3.5）。 |

**升級提示**

- 新增 `Assets/Shaders/WorldOverlay.shader`（`CyberWizard/WorldOverlay`）：`ZTest Always`、`ZWrite Off`、`Queue = Overlay`、Alpha 混合；顏色為 `_BaseColor` × `_MainTex` 的 Alpha × 頂點色（`_UseVertexColor` 開啟時）。
- `Materials/Level1Fx/Fx_PromptBackground.mat` 改用此 Shader（`_BaseColor` 不變），`m_CustomRenderQueue` 改為 -1（使用 Shader 的 Overlay 佇列）。
- `TowerUpgradePrompt` 在執行時以背景材質的 Shader 建立文字材質：貼上 `LegacyRuntime` 字型貼圖、開啟頂點色（rich text 顏色）、佇列比背景高 1。動態字型重建貼圖時（`Font.textureRebuilt`）重新指定。背景材質不是此 Shader 時記錄警告並沿用字型預設材質。

**防區**

- `DefenseZone.ContainsPoint(Vector3)`：把世界座標轉為防區本地座標，只比較 XZ，以射線交點計數法判斷（`PolygonContains`，凹多邊形與順時針／逆時針皆可）。剛好在邊上的點結果不保證。
- `DefenseZone.SetBoundaryPoint(int, Vector2)`：修改單一頂點並重建填色與外框。
- 新增 `Assets/Editor/DefenseZoneEditor.cs`（新的 `CyberWizard.Editor` 組件，只在編輯器中）：選取防區時，Scene View 每個頂點顯示可拖曳的球形控制點（`Handles.Slider2D`，限制在防區本地水平面）與頂點編號，支援 Undo。新增或刪除頂點仍在 Inspector 操作。

**遊戲流程與 UI**

| 項目 | 修改 |
| --- | --- |
| 結算時隱藏 HUD | `GateDefenseHud` 新增可選欄位 `resultPanel` 與 `IsVisible`；結算面板顯示時 `OnGUI` 不繪製任何內容（準星、狀態欄、提示）。`Level_1` 場景已連上 `Result Canvas` 上的 `LevelResultPanel`。 |
| WebGL 隱藏「離開」 | `MainMenuController.QuitSupported`：`Application.platform` 為 `WebGLPlayer` 時為 false，`Awake` 中隱藏「離開」按鈕。 |
| 中離確認 | `LevelPausePanel`：按「中離」時先顯示確認視窗。確認視窗在第一次需要時，於執行時複製暫停視窗（`panelRoot` 底下包含「中離」按鈕的子物件，即 `Window`），標題改為確認訊息、刪除「重新開始」，「繼續」與「中離」改為「取消」與「確定中離」，外觀與暫停視窗相同。確認視窗開啟時按 Esc 或「取消」回到暫停視窗（仍在暫停中）；「確定中離」才載入選關。找不到對應結構時記錄警告並直接中離。 |

### 可調整參數

| 元件／參數 | 目前值 | 用途 |
| --- | --- | --- |
| `GroundRangeIndicator.lift` | 0.3（原 0.2） | 環帶高於投影點的距離；需高於防區填色（+0.2）。 |
| `GateShatterOnDestroyed.fragmentLifetime` | 10 遊戲秒（暫定） | 碎片保留時間。 |
| `GateShatterOnDestroyed.fragmentShrinkDuration` | 1.5 遊戲秒（暫定） | 縮小到消失的時間。 |
| `GateShatterOnDestroyed.fragmentKillDepth` | 30（暫定） | 低於閘門此距離即視為掉出地圖；下層地面約 −15。 |
| `WavePathPreview.followSlope` | 開 | 箭頭隨坡度傾斜。 |
| `WavePathPreview.fadeDistance` | 3.5（暫定，等於箭頭間距） | 頭尾縮放距離；0 表示不縮放。 |
| `LevelPausePanel.confirmBeforeQuit` | 開 | 中離前是否確認。 |
| `LevelPausePanel.confirmMessage`／`confirmQuitLabel`／`cancelQuitLabel` | 「確定要中離嗎？目前關卡進度不會保留。」／「確定中離」／「取消」 | 確認視窗文字。 |
| `GateDefenseHud.resultPanel` | 場景中的 `LevelResultPanel` | 結算時隱藏 HUD；未設定時 HUD 永遠顯示。 |

### 遊戲呈現

- 勝利或失敗約 2 秒後出現結算面板（修正前不會出現），同時左上角狀態欄與準星消失。
- 防區內的射程圈為純白，不再偏藍。
- 路線箭頭在出生點附近從小逐漸放大，到閘門前逐漸縮小；在斜坡上跟著坡度傾斜（2° 坡度下肉眼不明顯）。
- 塔的升級提示即使被建築或塔擋住也會顯示在最上層。
- 閘門碎片噴散約 10 遊戲秒（慢動作下約 40 秒真實時間）後縮小消失；掉出地圖的碎片立即移除。實際上結算面板出現後玩家通常會離開場景，碎片清除多半看不到。
- 暫停中按「中離」先出現確認視窗。
- WebGL 版本的初始畫面沒有「離開」按鈕。
- 編輯器中選取防區時，可直接在 Scene View 拖曳邊界頂點。

### 限制與待改善事項

- 升級提示永遠在最上層，被擋住時也看得到，所以玩家可能看到牆後塔的提示（目前提示只在玩家靠近且視角指向塔時出現）。小地圖攝影機也會畫出此提示（若其 Culling Mask 包含提示所在圖層，**尚未驗證**）。
- `WorldOverlay` Shader 只在編輯器（Windows）驗證過；WebGL 建置後的顯示**尚未驗證**。
- 箭頭頭尾是縮放而非透明度變化。
- 「中離」確認視窗依賴暫停面板的結構（按鈕在 `panelRoot` 的同一個子物件下）；修改暫停面板結構時需確認仍能找到按鈕。
- WebGL 隱藏「離開」後，初始畫面只剩「開始」按鈕；若「開始」原本與「離開」並排或有版面配置，位置不會自動調整（**尚未驗證**實際版面）。
- 防區查詢只比較水平位置；尚未把塔與防區建立資料關聯。
- 防區頂點的新增與刪除仍需在 Inspector 操作。
- 碎片清除的數值為暫定值。
- `GamePhase.cs` 的必要修改尚未提交。

### 驗證方式

Unity 編輯器開著，所以把 `Assets`、`Packages`、`ProjectSettings` 複製到暫存資料夾，在副本上以 Unity 2022.3.62f3 批次模式執行。暫時的截圖測試只存在於副本中，副本已刪除。

| 項目 | 結果 |
| --- | --- |
| 編譯 | 沒有錯誤或警告（含新的 `CyberWizard.Editor` 組件）。 |
| EditMode 測試 | **31 項全數通過**。新增 3 項：`DefenseZone_ContainsPointUsesLocalHorizontalPolygon`、`ChevronStrip_FadeShrinksArrowsNearBothEnds`、`ChevronStrip_FollowSlopeTiltsArrowsAlongSegment`。 |
| PlayMode 測試 | **20 項全數通過**，原本失敗的 4 項都已通過。新增 `FragmentCleanup_RemovesFragmentsBelowKillHeightAndShrinksTheRest`；擴充：勝敗後 HUD 隱藏、「離開」按鈕在編輯器中顯示、中離確認／取消／Esc 取消／確定中離、每座塔在同編號防區內且閘門不在任何防區內、碎片有 `RangeIndicatorIgnored` 與清除元件且最後被移除。 |
| 截圖：射程圈顏色 | 俯視 5 號塔，防區內 14 個環帶取樣點中 13 點為純白 (1, 1, 1)，1 點為 (0.94, 0.98, 1)，可能落在「防區 5」文字上；修改前的截圖為淡藍色。 |
| 截圖：升級提示 | 在攝影機與提示之間放一個大方塊，提示（背景、白字、紅色花費）仍完整顯示在方塊上方。 |
| 截圖：路線箭頭 | 出生點附近的第一個箭頭明顯較小，之後逐漸放大到原尺寸。 |
| Scene View 拖曳防區頂點、Undo | **尚未驗證**（批次模式無法操作 Scene View）。 |
| WebGL 建置後的「離開」按鈕隱藏與 Overlay Shader | **尚未驗證**。 |
| 實際遊玩觀感（箭頭傾斜、碎片消失、確認視窗版面） | **尚未驗證** |
