# HALKA WORLD MAP EDITOR v0.4

.NET 8 / WPF / SkiaSharpのWindows専用マップ編集アプリです。ゲーム本編のバージョンはver2.0です。

## Build

PowerShellで `./scripts/publish-win-x64.ps1` を実行してください。出力は `dist/win-x64/HALKA WORLD MAP EDITOR.exe` です。自己完結版で、同フォルダー内のDLLとnativeファイルも必要です。Unity EditorはEXE起動に必要ありません。

アプリアイコンは `HalkaWorldMapEditor/Assets/map-editor.ico`。変更する場合はPillow入りのPythonで `py scripts/generate-app-icon.py` を実行します。

## 初回起動

EXEの近くにある `unity-project` を自動探索します。見つからなければ `Project...` でUnity Projectフォルダーを指定します。`Assets/Content/Maps/Authoring/*.hwmap.json` がMap選択に並びます。正式マップは屋外 `first_field.hwmap.json` と室内 `halka_house.hwmap.json`、Paletteは `object_catalog.hwcatalog.json` です。Mapごとの未保存変更は切替時に確認します。

操作は [Unity側ドキュメント](../../unity-project/Docs/HALKA_WORLD_MAP_EDITOR.md) に記載しました。Map v2 Schemaは `halka-world-map.schema.json`、Catalog v3 Schemaは `halka-world-catalog.schema.json` です。

Paletteの「地面」には「草」と「土」が並びます。「草」はDirtなどのSurfaceを消して通常の草地へ戻すツール、「土」はDirt Surfaceを配置するツールです。どちらも左ドラッグで連続編集でき、1回のドラッグを1回のUndoで戻せます。右クリックのEraseはショートカットとして残ります。草はJSONへ配置データとして保存されません。

室内Paletteには「木床」「壁」「ベッド」と、素材待ちの「クッション」「机」があります。木床は壁Surfaceを消してBase床を戻し、壁は通行不可Surfaceを配置します。家は屋外の大型Objectとして配置・移動でき、Root、見た目の5×4、ドアを除く9つの通行不可セルを区別します。Bedは2×3の通行不可Objectです。Mapごとに選択できるPalette項目をCatalogから絞ります。

v0.4ではCatalogのObject定義へAction Pointを追加しました。`playerCellOffset` はRoot相対のPlayer立ち位置、`playerFacing` は上下左右、`actionType` は `none/examine/sit/sleep`、`poseKey` と `interactionText` は任意です。Map JSONにはAction Pointを複写しません。選択したObjectの点はCanvas上で文字と矢印で強調され、InspectorにはLocal/World座標を表示します。上部の `Action Points` でOverlayを切り替えられます。Blocked FootprintとAction Pointは別物で、ベッドのsleep点は占有セル内、クッションの将来用sit点は通行可能セル上です。sit/sleepのゲーム処理はまだありません。

Palette検索欄で名称やdefinitionIdを絞れます。素材未設定のクッション・机・ベンチ・4方向の看板・井戸は `MISSING ASSET` と表示され、Sprite確定まで配置を拒否します。最終アート、サイズ、footprintは未設定です。素材が決まったらCatalogの同じstable `definitionId` にSprite pathと寸法を設定し、必要なblocked offsets・Action Point位置を確定してください。

## 開発とテスト

`dotnet build HalkaWorldMapEditor.sln -c Release`

`dotnet run --project HalkaWorldMapEditor.Tests -c Release`

Unityでは `Halka.Game.Editor.MapAuthoringChecks.Run` を実行してください。`MapDefinition`はJSONから生成されるキャッシュです。手動変更は次回同期で失われます。旧Unity Map Editor Windowは撤去しました。

## 制約

v0.4では新Map作成、Objectの回転・自由Scale、NPC配置、大規模Mapのchunk描画、座る・寝る実行を扱いません。Map v2 JSONが配置のAuthoring Source、Catalog v3 JSONがObject定義のAuthoring Sourceです。Unity生成assetやSceneを直接編集しません。
