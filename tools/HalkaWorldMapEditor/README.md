# HALKA WORLD MAP EDITOR v0.6

.NET 8 / WPF / SkiaSharpのWindows専用マップ編集アプリです。ゲーム本編のバージョンはver2.3です。

## Build

PowerShellで `./scripts/publish-win-x64.ps1` を実行してください。出力は `dist/win-x64/HALKA WORLD MAP EDITOR.exe` です。自己完結版で、同フォルダー内のDLLとnativeファイルも必要です。Unity EditorはEXE起動に必要ありません。

アプリアイコンは `HalkaWorldMapEditor/Assets/map-editor.ico`。変更する場合はPillow入りのPythonで `py scripts/generate-app-icon.py` を実行します。

## 初回起動

EXEの近くにある `unity-project` を自動探索します。見つからなければ `Project...` でUnity Projectフォルダーを指定します。`Assets/Content/Maps/Authoring/*.hwmap.json` がMap選択に並びます。正式マップは屋外 `first_field.hwmap.json` と室内 `halka_house.hwmap.json`、Paletteは `object_catalog.hwcatalog.json` です。Mapごとの未保存変更は切替時に確認します。

操作は [Unity側ドキュメント](../../unity-project/Docs/HALKA_WORLD_MAP_EDITOR.md) に記載しました。Map v4 Schemaは `halka-world-map.schema.json`、Catalog v3 Schemaは `halka-world-catalog.schema.json` です。

Paletteの「地面」には「草」「土」「木床」「壁」が両Mapで並びます。「草」は屋外では通常草地へ戻し、室内では草地Surfaceを明示的に塗ります。室内でも草が表示され、木床を選べば床へ戻せます。左ドラッグは1操作としてUndoでき、右クリックEraseも使えます。屋外で自動生成される草を配置データとして保存しません。

PaletteはMap Typeで絞りません。屋外でベッドや机、室内で草・石・花・木・ベンチ・井戸なども配置できます。家は大型ObjectとしてRoot、見た目の5×4、ドアを除く9つの通行不可セルを区別します。ベッドは2×3の通行不可Objectです。

v0.4ではCatalogのObject定義へAction Pointを追加しました。`playerCellOffset` はRoot相対のPlayer立ち位置、`playerFacing` は上下左右、`actionType` は `none/examine/sit/sleep`、`poseKey` と `interactionText` は任意です。Map JSONにはAction Pointを複写しません。選択したObjectの点はCanvas上で文字と矢印で強調され、InspectorにはLocal/World座標を表示します。上部の `Action Points` でOverlayを切り替えられます。Blocked FootprintとAction Pointは別物で、ベッドのsleep点は占有セル内、クッションの将来用sit点は通行可能セル上です。sit/sleepのゲーム処理はまだありません。

Palette検索欄で名称やdefinitionIdを絞れます。クッション・机・ベンチ・看板・井戸は正式Sprite付きで配置できます。素材一覧、Visual寸法、blocked footprint、Action Pointは[Unity側ドキュメント](../../unity-project/Docs/HALKA_WORLD_MAP_EDITOR.md#v04の正式world-object素材)に記載しました。看板は `sign_basic` 1種類、32×32の1セルです。選択後にInspectorで文面を編集すると、その個体の `signText` としてMap JSONに保存されます。Copy/PasteやMoveでも文面を保ちます。旧4方向の看板は読み込み時に新形式へ移行します。

## 開発とテスト

`dotnet build HalkaWorldMapEditor.sln -c Release`

`dotnet run --project HalkaWorldMapEditor.Tests -c Release`

Unityでは `Halka.Game.Editor.MapAuthoringChecks.Run` を実行してください。`MapDefinition`はJSONから生成されるキャッシュです。手動変更は次回同期で失われます。旧Unity Map Editor Windowは撤去しました。

## 制約

v0.6では新Map作成、Objectの回転・自由Scale、NPC配置、大規模Mapのchunk描画、座る・寝る実行を扱いません。Map v4 JSONが配置のAuthoring Source、Catalog v3 JSONがObject定義のAuthoring Sourceです。Unity生成assetやSceneを直接編集しません。

## v0.5 Entity / Spawn Editor

共通Paletteの「キャラクター / 生き物」にはﾊﾙｶﾁｬﾝ開始位置とカラスが両Mapで表示されます。これらは静的World Objectではなく動的Entityの初期Spawnです。`entity_catalog.hwentitycatalog.json` に種類、既存Sprite、既定向き、Runtime Behavior、最大数、Crowの相対行動範囲を定義します。新しい画像はありません。

選択したEntityはInspectorの座標欄で移動し、向き欄で上下左右を設定します。`Entities` Overlayで表示を切り替えられ、Crowの行動範囲は薄い枠、選択時は強調枠で表示します。Add/Delete/Move/向き変更はUndo/Redoできます。Player StartはWorld全体で1個で、Copyは禁止、Deleteには確認があります。Crowは現行Runtimeの上限1羽ですが、JSON配列は複数個体を表せる形式です。

Map v3は`entitySpawns`へ`instanceId`、`definitionId`、`cell`、`facing`を保存します。旧v2の`player_start`/`crow_spawn` Markerは読み込み時に移行し、正式ファイルはバックアップ後に`scripts/migrate-v04-to-v05.py`で移行しました。道路終端と室内入退室Markerは従来どおりです。開始PlayerはPersistentでMap切替時に生成し直しません。Crowの行動範囲はSpawnからの相対値(-3..+1,-2..+2)で、Map Boundsと通行判定で制限します。

将来Entityを追加する場合は、Sprite、Entity Catalog定義、Unityの明示的Runtime Behavior、Previewを用意するとEditor Paletteに現れます。配置はHALKAがEditorで行います。任意のC#型名をJSONから反射生成する方式は採りません。
# v0.6 Area Transition Editor

**Area Transition [4]** で Map 端の通行可能セルをクリックすると出口を追加します。Inspector で出口 ID、方向、行き先 Map、到着 Cell と向きを変更します。行き先は現在読める Authoring Map から選択でき、**行き先をMap上で選ぶ** で到着セルをクリックして指定できます。選択中は編集を行わず、右クリックまたはキャンセルで元の Map に戻ります。`Area Transitions` Overlay に出口の矢印を表示します。選択後の座標変更、Delete、Undo/Redo、保存/再読込に対応します。

Map JSON は v4 で、各 Map の `areaTransitions[]` に片道接続を保存します。旧 v3 は読み込み時に空配列付き v4 へ移行します。正式 `first_field` と `halka_house` には出口を追加していません。Source が方向側の Map 端にあること、行き先 Map が存在すること、到着セルが通行可能であること、同じ Source と方向の重複がないことを保存前に検証します。逆方向は別の出口として設定します。

Unity Runtime は手動の外向き移動入力で Area Transition を起動し、AUTO では起動しません。Player を保持したまま Map 固有の描画と Crow を切り替え、到着先で Camera を Snap します。House の移動入退室は独立したままです。詳しい JSON 例と接続手順は [Unity側ドキュメント](../../unity-project/Docs/HALKA_WORLD_MAP_EDITOR.md#v06-area-transition-editor) を参照してください。
