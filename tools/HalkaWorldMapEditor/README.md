# HALKA WORLD MAP EDITOR v0.3

.NET 8 / WPF / SkiaSharpのWindows専用マップ編集アプリです。ゲーム本編のバージョンはver2.0です。

## Build

PowerShellで `./scripts/publish-win-x64.ps1` を実行してください。出力は `dist/win-x64/HALKA WORLD MAP EDITOR.exe` です。自己完結版で、同フォルダー内のDLLとnativeファイルも必要です。Unity EditorはEXE起動に必要ありません。

アプリアイコンは `HalkaWorldMapEditor/Assets/map-editor.ico`。変更する場合はPillow入りのPythonで `py scripts/generate-app-icon.py` を実行します。

## 初回起動

EXEの近くにある `unity-project` を自動探索します。見つからなければ `Project...` でUnity Projectフォルダーを指定します。`Assets/Content/Maps/Authoring/*.hwmap.json` がMap選択に並びます。正式マップは屋外 `first_field.hwmap.json` と室内 `halka_house.hwmap.json`、Paletteは `object_catalog.hwcatalog.json` です。Mapごとの未保存変更は切替時に確認します。

操作は [Unity側ドキュメント](../../unity-project/Docs/HALKA_WORLD_MAP_EDITOR.md) に記載しました。JSON Schemaは `halka-world-map.schema.json` です。

Paletteの「地面」には「草」と「土」が並びます。「草」はDirtなどのSurfaceを消して通常の草地へ戻すツール、「土」はDirt Surfaceを配置するツールです。どちらも左ドラッグで連続編集でき、1回のドラッグを1回のUndoで戻せます。右クリックのEraseはショートカットとして残ります。草はJSONへ配置データとして保存されません。

室内Paletteには「木床」「壁」「ベッド」があります。木床は壁Surfaceを消してBase床を戻し、壁は通行不可Surfaceを配置します。家は屋外の大型Objectとして配置・移動でき、Root、見た目の5×4、ドアを除く9つの通行不可セルを区別します。Bedは2×3の通行不可Objectです。Mapごとに選択できるPalette項目をCatalogから絞ります。

## 開発とテスト

`dotnet build HalkaWorldMapEditor.sln -c Release`

`dotnet run --project HalkaWorldMapEditor.Tests -c Release`

Unityでは `Halka.Game.Editor.MapAuthoringChecks.Run` を実行してください。`MapDefinition`はJSONから生成されるキャッシュです。手動変更は次回同期で失われます。旧Unity Map Editor Windowは撤去しました。

## 制約

v0.3では新Map作成、Objectの回転・自由Scale、NPC配置、大規模Mapのchunk描画は扱いません。Map v2のJSONが唯一のAuthoring Sourceで、Unity生成assetやSceneを直接編集しません。
