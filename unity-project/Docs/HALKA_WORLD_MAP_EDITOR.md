# HALKA WORLD MAP EDITOR v0.3

ゲーム本編は **ver2.0** のままです。開発専用の .NET 8 / WPF / SkiaSharp Windows アプリで、ゲーム画面には出ません。

## 起動と保存

`tools/HalkaWorldMapEditor/scripts/publish-win-x64.ps1` で `dist/win-x64/HALKA WORLD MAP EDITOR.exe` を作ります。EXE と同じフォルダーの DLL と native ファイルも必要です。Unity Project を自動検出できないときは `Project...` で選択します。

`Assets/Content/Maps/Authoring/*.hwmap.json` からマップ一覧を作ります。正式な Map ID は `first_field`（屋外）と `halka_house`（室内）です。選択中のMapに未保存の変更があると、切替前に保存・破棄・キャンセルを選べます。MapごとのZoom/Panを保持します。

JSONが唯一の編集元です。Unityの `Assets/Content/Maps/*.asset` と `FirstDay.unity` は生成結果です。Map JSONを保存すると `.bak` を作り、内容を検証してから原子的に更新します。

## 操作

| 操作 | 内容 |
|---|---|
| 地面「草」「土」 | 屋外のSurface編集。「草」はSurface overrideを消して通常の草地へ戻します。Grass Placementは保存しません。 |
| 室内「木床」「壁」 | 「木床」は壁Surfaceを消してBaseの床へ戻し、「壁」は通行不可Surfaceを塗ります。 |
| Object Palette | Map Typeに合う石・花・木・家・ベッドを選択。左クリックでRoot Cellに配置。 |
| 選択 `[1]` | Spriteの見た目範囲からObjectを選択。InspectorにInstance ID、Root、Visual、Blocked Cellを表示。 |
| Paint `[2]` / Erase `[3]` | Surfaceは左ドラッグで連続編集し、1ドラッグをUndo 1回にまとめます。右クリックはErase。 |
| `Ctrl+Z` / `Ctrl+Y` | Undo / Redo。House移動でもInstance IDは維持します。 |
| `Ctrl+C` / `Ctrl+V` | 選択Objectの種類をコピーし、新しいInstance IDで配置します。 |
| `Delete` | 選択Objectを削除。 |
| ホイール / 中ボタン | Zoom / Pan。Space+左ドラッグでもPan。 |
| `F` / `G` | 全体表示 / Grid切替。 |
| `Ctrl+S` | 現在のMap JSONを検証して保存。 |

Grid、Grass、Collision、座標、Markersの表示を切り替えられます。大型Objectは画像の5×4などの見た目範囲と、通行不可セル群を別々に表示します。HouseのRootはドアの下中央セルで、通行不可はドアを除く9セルです。Houseを移動すると入口前セルはRootから導出されます。ベッドは2×3の通行不可Objectです。

## Map v2 契約

Schemaは `tools/HalkaWorldMapEditor/halka-world-map.schema.json`。各Mapに `mapId`、`mapType`、`baseSurfaceDefinitionId`、`grassMode`、`backdropColor`、Bounds、Surface overrides、Objects、ID付きMarkersを保存します。Base Surfaceを1セルずつ保存しません。屋外Grassは `grassMode=auto` で派生し、室内は `grassMode=none` です。

Objectは `instanceId`、`definitionId`、`rootCell` を保存します。Visualサイズ、Root Anchor、通行不可セルの相対オフセット、Map Type制限はCatalogとUnity Definitionが持ちます。移動・Undo/Redo・再保存でInstance IDを保ちます。回転と自由Scaleはv0.3では扱いません。

古いv1の `first_field` はCoreで読み込み時にv2へ変換できます。正式Mapの移行には `tools/HalkaWorldMapEditor/scripts/migrate-v02-to-v03.py` を用い、元JSONを別途バックアップしてから実行してください。現在の正式JSONはv2へ移行済みです。

Unity Importerが両MapのJSONとCatalogを読み、MapDefinition/Definition assetへ同期します。Runtimeは `MapWorldController2D` のMap IDで屋外と室内を切り替え、家の入退室位置はMarkerとHouse Rootから取得します。SceneとWebGLはMap JSONから再生成します。

## 検証

`dotnet run --project tools/HalkaWorldMapEditor/HalkaWorldMapEditor.Tests -c Release`

Unity側は `Halka.Game.Editor.MapAuthoringChecks.Run` と `Halka.Game.Editor.Version20Checks.Run`。GameVersionは2.0のままです。GUIの破壊操作は正式Mapを直接使わず、コピーで確認してください。
