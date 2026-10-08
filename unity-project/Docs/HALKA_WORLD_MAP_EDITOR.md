# HALKA WORLD MAP EDITOR v0.6

ゲーム本編は **ver2.5.1** です。開発専用の .NET 8 / WPF / SkiaSharp Windows アプリで、ゲーム画面には出ません。

## 起動と保存

`tools/HalkaWorldMapEditor/scripts/publish-win-x64.ps1` で `dist/win-x64/HALKA WORLD MAP EDITOR.exe` を作ります。EXE と同じフォルダーの DLL と native ファイルも必要です。Unity Project を自動検出できないときは `Project...` で選択します。

`Assets/Content/Maps/Authoring/*.hwmap.json` からマップ一覧を作ります。正式な Map ID は `first_field`（屋外）と `halka_house`（室内）です。選択中のMapに未保存の変更があると、切替前に保存・破棄・キャンセルを選べます。MapごとのZoom/Panを保持します。

JSONが唯一の編集元です。Unityの `Assets/Content/Maps/*.asset` と `FirstDay.unity` は生成結果です。Map JSONを保存すると `.bak` を作り、内容を検証してから原子的に更新します。

## 操作

| 操作 | 内容 |
|---|---|
| 地面「草」「土」「木床」「壁」 | 両Mapで共通。「草」は屋外では通常草地へ戻し、室内では草地Surfaceを塗ります。「木床」は室内ではBaseの床へ戻し、屋外では床Surfaceを塗ります。 |
| Object Palette | Map Typeで絞りません。屋外・室内とも全Objectを選べます。素材未設定のみ `MISSING ASSET` と表示して配置を拒否します。 |
| 選択 `[1]` | Spriteの見た目範囲からObjectを選択。InspectorにInstance ID、Root、Visual、Blocked Cellを表示。 |
| Paint `[2]` / Erase `[3]` | Surfaceは左ドラッグで連続編集し、1ドラッグをUndo 1回にまとめます。右クリックはErase。 |
| `Ctrl+Z` / `Ctrl+Y` | Undo / Redo。House移動でもInstance IDは維持します。 |
| `Ctrl+C` / `Ctrl+V` | 選択Objectの配置情報（看板の文面を含む）をコピーし、新しいInstance IDで配置します。 |
| `Delete` | 選択Objectを削除。 |
| ホイール / 中ボタン | Zoom / Pan。Space+左ドラッグでもPan。 |
| `F` / `G` | 全体表示 / Grid切替。 |
| `Ctrl+S` | 現在のMap JSONを検証して保存。 |

Grid、Grass、Collision、座標、Markers、Action Pointsの表示を切り替えられます。大型Objectは画像の5×4などの見た目範囲と、通行不可セル群を別々に表示します。HouseのRootはドアの下中央セルで、通行不可はドアを除く9セルです。Houseを移動すると入口前セルはRootから導出されます。ベッドは2×3の通行不可Objectです。

## Map v4 契約

Schemaは `tools/HalkaWorldMapEditor/halka-world-map.schema.json`。各Mapに `mapId`、`mapType`、`baseSurfaceDefinitionId`、`grassMode`、`backdropColor`、Bounds、Surface overrides、Objects、ID付きMarkersを保存します。Base Surfaceを1セルずつ保存しません。屋外Grassは `grassMode=auto` で派生します。室内の既定値は `grassMode=none` ですが、草地Surfaceを塗ったセルにはGrassが表示されます。

Objectは `instanceId`、`definitionId`、`rootCell` を保存し、看板だけ任意の `signText` も保存します。Visualサイズ、Root Anchor、通行不可セルの相対オフセット、Action PointはCatalog v3とUnity Definitionが持ちます。旧 `allowedMapTypes` は配置制限に使用しません。移動・Undo/Redo・再保存でInstance IDと看板の文面を保ちます。回転と自由Scaleはv0.4では扱いません。

看板は `sign_basic` 1種類です。選択後にInspectorのText欄で文面を入力し、ApplyまたはEnterで反映します。Action PointはRootの1セル下から上向きで読む位置です。旧4方向の看板IDは読み込み時に `sign_basic` と対応する文面へ移行します。現在のMap JSONはv4で、屋外の草は引き続き派生状態として保存しません。

古いv1/v2/v3のMapはCoreで読み込み時にv4へ変換できます。正式Mapのv2→v3移行には `tools/HalkaWorldMapEditor/scripts/migrate-v04-to-v05.py` を用い、元JSONを別途バックアップしてから実行してください。現在の正式JSONはv4へ移行済みです。

Unity Importerが両MapのJSONとCatalogを読み、MapDefinition/Definition assetへ同期します。Runtimeは `MapWorldController2D` のMap IDで屋外と室内を切り替え、家の入退室位置はMarkerとHouse Rootから取得します。SceneとWebGLはMap JSONから再生成します。

## 検証

`dotnet run --project tools/HalkaWorldMapEditor/HalkaWorldMapEditor.Tests -c Release`

Unity側は `Halka.Game.Editor.MapAuthoringChecks.Run` と `Halka.Game.Editor.Version25Checks.Run`（ver2.0回帰も実行）。GameVersionは2.5です。GUIの破壊操作は正式Mapを直接使わず、コピーで確認してください。

## Action Pointと通行判定

Catalog v3の各Objectには `actionPoints` 配列があります。各点は `id`、Root相対の整数セル `playerCellOffset`、`playerFacing`（up/down/left/right）、`actionType`（none/examine/sit/sleep）、任意の `poseKey` と `interactionText` を持ちます。World座標は `rootCell + playerCellOffset` で算出し、Map JSONには重複保存しません。InspectorのLocal/World座標とCanvas上の E/S/Z + 矢印で確認できます。Object移動、Undo/Redo、Copy/Pasteにもこの計算が追従します。

`blockedCellOffsets` は通行不可の範囲です。Action PointはPlayerの行動位置であり、両者は独立です。ベッドのsleep点は下側の通行可能セルです。クッションはblocked offsetsが空で通行可能な定義です。examineの立ち位置が塞がれた場合は警告として表示します。本編ver2.5では正式Map上のベンチで`bench_sit`から座れます。`cushion_sit`と実際の睡眠は未実装です。石・花・木の既存メッセージはAction Pointの `interactionText` をUnity Definitionへ同期し、現行RuntimeのExamine表示を維持します。家の入退室は既存の移動遷移のままです。

## v0.4の正式World Object素材

| Definition | Sprite | Visual / blocked | Action Point |
|---|---|---|---|
| `cushion_basic` | `cushion.png` | 32×32 / 通行可能 | Rootの(0,0)、`sit` / `cushion_sit` |
| `desk_basic` | `desk.png` | 64×32 / 横2セル | Rootの(0,-1)、上向き `examine` / 「つくえ。」 |
| `bench_basic` | `bench.png` | 64×32 / 横2セル | (0,-1)、(1,-1)の2席、`sit` / `bench_sit` |
| `sign_basic` | `sign.png` | 32×32 / Rootの1セル | (0,-1)、上向き `examine`、文面は配置ごとの `signText` |
| `well_basic` | `well.png` | 32×32 / Rootの1セル | (0,-1)、上向き `examine` / 「いど。」 |

机とベンチは左下セルをRootとし、見た目の右セルにも通行不可判定を置きます。看板は見た目も通行不可判定もRootの1セルです。クッションは床置きで上を歩けます。看板画像には文字も矢印も描いていません。ベンチの左右Action Pointでは`bench_sit`が動作します。クッションで座る処理とベッドで寝る処理はありません。本編ver2.5の正式Mapにはベンチと看板を屋外へ、クッションと机を室内へ初期配置しています。

画像の原本は `SourceGeneratedArt/map_editor_v04_objects` に保存し、ゲーム用PNGは `Assets/Content/World` に置きます。追加素材は透明RGBA、32pxセルの整数倍、PPU64、Point、Single Sprite、mipmap無効、無圧縮を守ります。絵は画像生成機能で制作し、技術的後処理では透明化、切り抜き、Nearest縮小、色数整理だけを行います。Catalogのstable IDとSprite path、Visual寸法、Root Anchor、blocked offsets、Action Pointを設定してからUnity同期とCore/Unity検証を実行してください。初期配置後もStandalone Editorで位置と看板の文章を調整できます。

## 新しいObjectを追加する手順

1. 正式Spriteを `Assets/Content/World` に追加する。
2. Catalogへ変更されない `definitionId` と表示名、Sprite pathを登録する。Unity Importerが同じIDの `WorldObjectDefinition` を生成・更新する。
3. 実Spriteに合わせてVisual Width/Height、Root Anchorを決める。
4. 通行不可セルをRoot相対の `blockedCellOffsets` で設定する。通行可能なら空配列、`blocksMovement=false` とする。
5. 必要なAction PointをRoot相対位置、Facing、Type、Pose Key、Textで定義する。
6. Paletteは両Mapで共通です。Map Typeによる配置制限は設けません。
7. UnityでAuthoring Importと契約検証を実行する。
8. Codexが現行Mapの障害物、出入口、Spawn、Action Pointを調査し、正式Map JSONへ自然な初期配置を追加する。ゲーム内で操作できることまで確認する。
9. HALKAがStandalone Editorで正式Map上の配置を自由に微調整する。

今後のロードマップで追加するObjectや家具も、原則としてCodexが正式Mapへの初期配置まで担当します。ユーザーが「まだ配置しない」と明示した場合は配置しません。既存配置を保護し、通路や出入口を塞がず、Instance IDとMap JSON v4の保存形式を維持します。

配置済みMapは定義IDとRootだけを参照するため、後でSprite pathを差し替えても配置JSONを作り直す必要はありません。Catalog v2はStandalone側でメモリ上だけv3へ読み替え可能ですが、Unity Importerへ渡す正式Catalogはv3に更新してください。

## v0.5 Entity / Spawn Editor

Surfaceと静的World Objectから分離して、`entitySpawns`配列で動的Entityの開始セルと向きを保存します。共有`entity_catalog.hwentitycatalog.json`は`player_main`と`crow_main`を定義します。Playerはゲーム開始時に既存Persistent Playerを移動し、室内遷移は引き続き`interior_entry`/`interior_exit` Markerを使います。CrowはSpawnセルから相対(-3..+1,-2..+2)を歩き、Map外と静的障害物を避けます。既存のDynamic Occupancy、草遮蔽、ガサゴソ、鳴き声を使います。

Palette「キャラクター / 生き物」は両Mapに表示されます。Entityを選択するとInspectorで座標とFacingを編集でき、CrowのWander RegionをOverlayで確認できます。Player StartはWorldで1個、Copy禁止、Delete確認あり。SpawnセルはMap内の通行可能セルに限ります。Crow範囲がMap外へ出る場合はWarningです。Map v2のPlayer/Crow Markerはv3へ移行し、道路と遷移Markerは保持します。

NPC追加時は (1) Sprite (2) Entity Catalog定義 (3) Unity EntityDefinition同期 (4) 明示的Runtime Behavior登録 (5) Preview確認 (6) Editorで配置、の順です。MapDefinitionはJSONから生成されるキャッシュです。RuntimeのActor生成をリフレクションへ任せず、対応するBehaviorを明示的に実装してください。
# v0.6 Area Transition Editor

Map JSON v4 の `areaTransitions` は、Map 間の片道出口を配置単位で保存します。要素は `instanceId`、`transitionId`、`sourceCell`、`exitDirection`、`destinationMapId`、`destinationCell`、`arrivalFacing` です。正式な `first_field` と `halka_house` の配列は空です。道路終端 Marker は候補位置として残し、出口は自動作成しません。v3 を読み込むと空配列を持つ v4 に移行します。

Editor の **Area Transition [4]** を選び、Map の端にある通行可能な Source Cell をクリックします。選択された出口の Inspector で ID、出口方向、行き先 Map、到着 X/Y、到着後の向きを設定し、**Transitionを反映** します。行き先 Map は開ける Authoring Map 一覧から選びます。**行き先をMap上で選ぶ** は現在の Map を保存してから相手 Map を一時表示し、クリックした通行可能セルを到着先に設定します。右クリックまたはキャンセルボタンで選択を中断できます。`Area Transitions` Overlay は出口矢印と行き先を描きます。選択、移動、削除、Undo/Redo、保存と再読込に対応します。

```json
"areaTransitions": [
  {
    "instanceId": "transition_例",
    "transitionId": "north_exit",
    "sourceCell": { "x": 0, "y": 6 },
    "exitDirection": "up",
    "destinationMapId": "forest",
    "destinationCell": { "x": 0, "y": -6 },
    "arrivalFacing": "up"
  }
]
```

この例は形式の説明で、正式 Map へは配置していません。実際に接続するには、先に行き先の `*.hwmap.json` を Authoring フォルダーに用意し、Map ID が実在することを確認します。逆向きの移動が必要なら、行き先 Map に別の Transition を配置します。Source は指定方向の Map 端で通行可能、Destination は存在する Map の通行可能セルである必要があります。同じ Source と方向の重複は Error です。保存前に全 Authoring Map を読んで参照を検証します。

Runtime では、停止中の Player が Source Cell で外向き方向を入力した時だけ切り替えます。通常の歩行入力経路を使い、AUTO の `TryStep` は出口を起動しません。到着後も同じ Player を使い、移動中 Step は生成せず、Map 固有の Surface/Object/Grass/Crow を入れ替え、カメラを即時 Snap します。家の既存 `HouseArea2D` 入退室は独立して維持します。

新しい Map は本 Editor ではまだ作成しません。JSON の作成後に Unity の `MapAuthoringImporter.SyncAll` と `ProjectBuilder.PrepareScene` を実行すると、追加 Map も Runtime registry へ登録されます。テスト用 Map は正式 Authoring フォルダーへ残さず、コピーで編集してください。
