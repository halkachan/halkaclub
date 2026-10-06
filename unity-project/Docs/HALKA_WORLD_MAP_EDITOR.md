# HALKA WORLD MAP EDITOR v0.4

ゲーム本編は **ver2.0** のままです。開発専用の .NET 8 / WPF / SkiaSharp Windows アプリで、ゲーム画面には出ません。

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

## Map v2 契約

Schemaは `tools/HalkaWorldMapEditor/halka-world-map.schema.json`。各Mapに `mapId`、`mapType`、`baseSurfaceDefinitionId`、`grassMode`、`backdropColor`、Bounds、Surface overrides、Objects、ID付きMarkersを保存します。Base Surfaceを1セルずつ保存しません。屋外Grassは `grassMode=auto` で派生します。室内の既定値は `grassMode=none` ですが、草地Surfaceを塗ったセルにはGrassが表示されます。

Objectは `instanceId`、`definitionId`、`rootCell` を保存し、看板だけ任意の `signText` も保存します。Visualサイズ、Root Anchor、通行不可セルの相対オフセット、Action PointはCatalog v3とUnity Definitionが持ちます。旧 `allowedMapTypes` は配置制限に使用しません。移動・Undo/Redo・再保存でInstance IDと看板の文面を保ちます。回転と自由Scaleはv0.4では扱いません。

看板は `sign_basic` 1種類です。選択後にInspectorのText欄で文面を入力し、ApplyまたはEnterで反映します。Action PointはRootの1セル下から上向きで読む位置です。旧4方向の看板IDは読み込み時に `sign_basic` と対応する文面へ移行します。Map JSON v2のままで、屋外の草は引き続き派生状態として保存しません。

古いv1の `first_field` はCoreで読み込み時にv2へ変換できます。正式Mapの移行には `tools/HalkaWorldMapEditor/scripts/migrate-v02-to-v03.py` を用い、元JSONを別途バックアップしてから実行してください。現在の正式JSONはv2へ移行済みです。

Unity Importerが両MapのJSONとCatalogを読み、MapDefinition/Definition assetへ同期します。Runtimeは `MapWorldController2D` のMap IDで屋外と室内を切り替え、家の入退室位置はMarkerとHouse Rootから取得します。SceneとWebGLはMap JSONから再生成します。

## 検証

`dotnet run --project tools/HalkaWorldMapEditor/HalkaWorldMapEditor.Tests -c Release`

Unity側は `Halka.Game.Editor.MapAuthoringChecks.Run` と `Halka.Game.Editor.Version20Checks.Run`。GameVersionは2.0のままです。GUIの破壊操作は正式Mapを直接使わず、コピーで確認してください。

## Action Pointと通行判定

Catalog v3の各Objectには `actionPoints` 配列があります。各点は `id`、Root相対の整数セル `playerCellOffset`、`playerFacing`（up/down/left/right）、`actionType`（none/examine/sit/sleep）、任意の `poseKey` と `interactionText` を持ちます。World座標は `rootCell + playerCellOffset` で算出し、Map JSONには重複保存しません。InspectorのLocal/World座標とCanvas上の E/S/Z + 矢印で確認できます。Object移動、Undo/Redo、Copy/Pasteにもこの計算が追従します。

`blockedCellOffsets` は通行不可の範囲です。Action PointはPlayerの行動位置であり、両者は独立です。ベッドのsleep点は自身のblocked範囲にあっても構いません。クッションはblocked offsetsが空で通行可能な定義です。examineの立ち位置が塞がれた場合は警告として表示します。sit/sleepとPose Keyは将来用メタデータで、本編ver2.0では実行しません。石・花・木の既存メッセージはAction Pointの `interactionText` をUnity Definitionへ同期し、現行RuntimeのExamine表示を維持します。家の入退室は既存の移動遷移のままです。

## v0.4の正式World Object素材

| Definition | Sprite | Visual / blocked | Action Point |
|---|---|---|---|
| `cushion_basic` | `cushion.png` | 32×32 / 通行可能 | Rootの(0,0)、`sit` / `cushion_sit` |
| `desk_basic` | `desk.png` | 64×32 / 横2セル | Rootの(0,-1)、上向き `examine` / 「つくえ。」 |
| `bench_basic` | `bench.png` | 64×32 / 横2セル | (0,-1)、(1,-1)の2席、`sit` / `bench_sit` |
| `sign_basic` | `sign.png` | 32×32 / Rootの1セル | (0,-1)、上向き `examine`、文面は配置ごとの `signText` |
| `well_basic` | `well.png` | 32×32 / Rootの1セル | (0,-1)、上向き `examine` / 「いど。」 |

机とベンチは左下セルをRootとし、見た目の右セルにも通行不可判定を置きます。看板は見た目も通行不可判定もRootの1セルです。クッションは床置きで上を歩けます。看板画像には文字も矢印も描いていません。sit/sleepは引き続き将来用メタデータで、本編ver2.0の座る・寝る処理はありません。正式Mapにはこれらを自動配置しません。

画像の原本は `SourceGeneratedArt/map_editor_v04_objects` に保存し、ゲーム用PNGは `Assets/Content/World` に置きます。追加素材は透明RGBA、32pxセルの整数倍、PPU64、Point、Single Sprite、mipmap無効、無圧縮を守ります。絵は画像生成機能で制作し、技術的後処理では透明化、切り抜き、Nearest縮小、色数整理だけを行います。Catalogのstable IDとSprite path、Visual寸法、Root Anchor、blocked offsets、Action Pointを設定してからUnity同期とCore/Unity検証を実行してください。Map内の配置場所はStandalone Editorで決めます。

## 新しいObjectを追加する手順

1. 正式Spriteを `Assets/Content/World` に追加する。
2. Catalogへ変更されない `definitionId` と表示名、Sprite pathを登録する。Unity Importerが同じIDの `WorldObjectDefinition` を生成・更新する。
3. 実Spriteに合わせてVisual Width/Height、Root Anchorを決める。
4. 通行不可セルをRoot相対の `blockedCellOffsets` で設定する。通行可能なら空配列、`blocksMovement=false` とする。
5. 必要なAction PointをRoot相対位置、Facing、Type、Pose Key、Textで定義する。
6. Paletteは両Mapで共通です。Map Typeによる配置制限は設けません。
7. UnityでAuthoring Importと契約検証を実行する。
8. HALKAがStandalone Editorで正式Map上の配置場所を決めて保存する。

配置済みMapは定義IDとRootだけを参照するため、後でSprite pathを差し替えても配置JSONを作り直す必要はありません。Catalog v2はStandalone側でメモリ上だけv3へ読み替え可能ですが、Unity Importerへ渡す正式Catalogはv3に更新してください。
