# HALKA WORLD MAP EDITOR v0.2

.NET 8 / WPF / SkiaSharpのWindows専用マップ編集アプリです。ゲーム本編のバージョンはver2.0です。

## Build

PowerShellで `./scripts/publish-win-x64.ps1` を実行してください。出力は `dist/win-x64/HALKA WORLD MAP EDITOR.exe` です。自己完結版で、同フォルダー内のDLLとnativeファイルも必要です。Unity EditorはEXE起動に必要ありません。

## 初回起動

EXEの近くにある `unity-project` を自動探索します。見つからなければ `Project...` でUnity Projectフォルダーを指定します。`Assets/Content/Maps/Authoring/*.hwmap.json` がMap選択に並びます。正式マップは `first_field.hwmap.json`、Paletteは `object_catalog.hwcatalog.json` です。

操作は [Unity側ドキュメント](../../unity-project/Docs/HALKA_WORLD_MAP_EDITOR.md) に記載しました。JSON Schemaは `halka-world-map.schema.json` です。

## 開発とテスト

`dotnet build HalkaWorldMapEditor.sln -c Release`

`dotnet run --project HalkaWorldMapEditor.Tests -c Release`

Unityでは `Halka.Game.Editor.MapAuthoringChecks.Run` を実行してください。`MapDefinition`はJSONから生成されるキャッシュです。手動変更は次回同期で失われます。旧Unity Map Editor Windowは撤去しました。

## 制約

v0.2の編集対象は屋外SurfaceとStone/Flower/TreeのRoot配置です。House、Player/Crow spawn、室内、Area Transitionは固定Previewです。大規模Mapのchunk描画や複数Mapのゲーム実行時切替は今後の対象です。
